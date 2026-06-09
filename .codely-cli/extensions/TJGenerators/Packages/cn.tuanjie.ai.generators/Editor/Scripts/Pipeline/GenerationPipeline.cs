#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Codely.Newtonsoft.Json;
using Codely.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Networking;
using TJGenerators;
using TJGenerators.Generators;
using TJGenerators.Config;
using TJGenerators.Utils;
using Unity.UniAsset.Manager.Editor.InternalBridge;
using Unity.EditorCoroutines.Editor;

namespace TJGenerators.Pipeline
{
    /// <summary>
    /// 统一的生成流程管理 - 处理轮询、下载、Prefab绑定等通用逻辑。
    /// HTTP 传输由 <c>GenerationBackendTransportFactory</c> 与 <c>IGenerationBackendTransport</c> 实现。
    /// </summary>
    public class GenerationPipeline
    {
        private readonly ConfigType _configType;

        private string API_BASE_URL => ConfigManager.GetApiBaseUrl();
        private int MAX_POLL_RETRIES => ConfigManager.GetPollMaxRetries();
        private float POLL_INTERVAL => ConfigManager.GetPollInterval();

        private const string SAVE_DIRECTORY = "Assets/TJGenerators/";
        private const string HISTORY_DIRECTORY = "Assets/TJGenerators/History/";
        
        private IGenerationPipelineHost _host;
        private TJGeneratorsTaskHandle _activeTaskHandle;
        private IGenerationBackendTransport _transport;

        /// <summary>
        /// 当前占用本 Pipeline 的生成任务所属生成器（与 UI 中选中的模型实例可能不一致）。
        /// </summary>
        private ModelGeneratorBase _pipelineBusyGenerator;

        /// <summary>
        /// 当前任务的流水线/后处理配置，由任务入口点（StartGeneration 等）从 generator 取得后存储，
        /// 供后续私有方法直接访问，无需再通过 ModelGeneratorBase 虚方法绕一圈。
        /// </summary>
        private PipelineSettings _pipelineSettings = PipelineSettings.Default;

        // 当前任务的预览图URL（在任务完成时临时存储）
        private string _currentPreviewUrl;

        // 当前任务的源GLB URL（用于模型转换功能）
        private string _currentSourceGlbUrl;

        // 当前任务的音频保存路径（文生音频：开始时创建占位，完成时覆盖）
        private string _currentAudioSavePath;

        /// <summary>当前任务的视频保存路径（视频：开始时创建占位，完成时覆盖）</summary>
        private string _currentVideoSavePath;

        /// <summary>「添加动作」后处理成功时，绑骨后的主模型 Unity 路径（供绑定 Prefab）。</summary>
        private string _postMotionRiggedPath;

        private sealed class MotionSubTaskPollOutcome
        {
            public TJTaskStatusResponse Completed;
            public string Error;
        }

        public GenerationPipeline(IGenerationPipelineHost host, ConfigType configType)
        {
            _host = host;
            _configType = configType;
        }

        /// <summary>
        /// 是否有尚未结束的生成流程（轮询/下载等）。用于 UI 在切换模型下拉后仍保持「生成中」占用状态。
        /// </summary>
        public bool IsPipelineBusy => _pipelineBusyGenerator != null;

        /// <summary>
        /// 将当前进行中的任务绑定到指定生成器（正常启动与任务恢复时调用）。
        /// </summary>
        public void RegisterActiveGenerator(ModelGeneratorBase generator)
        {
            _pipelineBusyGenerator = generator;
        }

        private void EndGenerationState(ModelGeneratorBase generator)
        {
            generator.ResetState();
            if (_pipelineBusyGenerator == generator)
                _pipelineBusyGenerator = null;
        }

        private void EnsureTransport(ModelGeneratorBase generator)
        {
            if (_transport != null) return;
            _transport = GenerationBackendTransportFactory.Create();
        }
        
        // ========== 启动生成任务 ==========
        
        /// <summary>
        /// 启动生成任务
        /// </summary>
        /// <param name="generator">模型生成器</param>
        /// <param name="assetGuid">目标资产GUID</param>
        public IEnumerator StartGeneration(ModelGeneratorBase generator, string assetGuid, TJGeneratorsTaskHandle taskHandle = null)
        {
            _pipelineSettings = generator.GetPipelineSettings();
            _activeTaskHandle = taskHandle;

            // 验证输入
            if (!generator.ValidateInputs(out string errorMessage))
            {
                _host.ShowDialog("输入错误", errorMessage);
                if (_activeTaskHandle != null)
                {
                    _activeTaskHandle.MarkFailed("invalid_input", errorMessage);
                    _activeTaskHandle = null;
                }
                yield break;
            }
            
            // 创建占位符
            generator.CurrentGeneratingTaskId = TJGeneratorsHistoryManager.AddGeneratingPlaceholder(
                generator.GetPrompt(),
                generator.GetImagePath(),
                generator.GetModelVersion(),
                generator.IsTextToModel(),
                assetGuid
            );
            
            if (_activeTaskHandle != null)
            {
                _activeTaskHandle.SetLocalTaskId(generator.CurrentGeneratingTaskId);
            }
            
            generator.IsRunning = true;
            RegisterActiveGenerator(generator);
            generator.ButtonText = "上传中...";
            _host.RefreshHistory();
            _host.Repaint();

            // 文生音频：仅记录保存路径，不写入占位文件，避免空/非法文件触发 FSBTool 报错；完成后在 HandleAudioAsset 中写入并导入
            if (string.Equals(generator.GetOutputType(), "audio", StringComparison.OrdinalIgnoreCase))
            {
                string savePath = _host.GetAudioSavePath(generator);
                if (!string.IsNullOrEmpty(savePath))
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(PathUtils.ToAbsoluteAssetPath(savePath));
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        _currentAudioSavePath = savePath;
                    }
                    catch (Exception e)
                    {
                        TJLog.LogWarning($"[GenerationPipeline] 准备音频保存路径失败: {e.Message}");
                        _currentAudioSavePath = null;
                    }
                }
            }
            
            // 视频：记录保存路径
            if (string.Equals(generator.GetOutputType(), "video", StringComparison.OrdinalIgnoreCase))
            {
                string savePath = _host.GetVideoSavePath(generator);
                if (!string.IsNullOrEmpty(savePath))
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(PathUtils.ToAbsoluteAssetPath(savePath));
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        _currentVideoSavePath = savePath;
                    }
                    catch (Exception e)
                    {
                        TJLog.LogWarning($"[GenerationPipeline] 准备视频保存路径失败: {e.Message}");
                        _currentVideoSavePath = null;
                    }
                }
            }
            
            // 发送API请求
            yield return SendGenerationRequest(generator, assetGuid);
        }
        
        /// <summary>
        /// 从已成功提交的后端任务ID开始轮询和下载，跳过 HTTP 提交阶段。
        /// 由 CustomTool 两阶段模式使用：外部同步提交后，由此方法接管剩余流程。
        /// </summary>
        public IEnumerator StartFromSubmittedTask(
            ModelGeneratorBase generator,
            string assetGuid,
            string backendTaskId,
            TJGeneratorsTaskHandle taskHandle = null)
        {
            _pipelineSettings = generator.GetPipelineSettings();
            _activeTaskHandle = taskHandle;

            // 创建历史占位符（与 StartGeneration 保持一致）
            generator.CurrentGeneratingTaskId = TJGeneratorsHistoryManager.AddGeneratingPlaceholder(
                generator.GetPrompt(),
                generator.GetImagePath(),
                generator.GetModelVersion(),
                generator.IsTextToModel(),
                assetGuid
            );

            if (_activeTaskHandle != null)
                _activeTaskHandle.SetLocalTaskId(generator.CurrentGeneratingTaskId);

            generator.IsRunning = true;
            RegisterActiveGenerator(generator);
            generator.ButtonText = "生成中...";
            _host.RefreshHistory();
            _host.Repaint();

            // 注册到 interrupted tasks，支持 domain reload 自动恢复
            generator.CurrentBackendTaskId = backendTaskId;
            var taskData = generator.CreateInterruptedTaskData(backendTaskId, assetGuid);
            TJGeneratorsTaskRecovery.AddInterruptedTask(taskData);
            TJGeneratorsTaskRecovery.MarkAsRecovering(backendTaskId);

            // 通知 handle：任务已在后端创建，进入轮询阶段
            if (_activeTaskHandle != null)
            {
                _activeTaskHandle.SetBackendTaskId(backendTaskId);
                _activeTaskHandle.SetStatus("pending");
                _activeTaskHandle.NotifyCreated();
            }

            TJLog.Log($"[GenerationPipeline] StartFromSubmittedTask: 跳过提交，直接轮询 backendTaskId={backendTaskId}");

            // 音频任务：初始化保存路径（与 StartGeneration 保持一致）
            if (string.Equals(generator.GetOutputType(), "audio", StringComparison.OrdinalIgnoreCase))
            {
                string savePath = _host.GetAudioSavePath(generator);
                if (!string.IsNullOrEmpty(savePath))
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(PathUtils.ToAbsoluteAssetPath(savePath));
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        _currentAudioSavePath = savePath;
                    }
                    catch (Exception e)
                    {
                        TJLog.LogWarning($"[GenerationPipeline] 准备音频保存路径失败: {e.Message}");
                        _currentAudioSavePath = null;
                    }
                }
            }

            // 视频任务：初始化保存路径
            if (string.Equals(generator.GetOutputType(), "video", StringComparison.OrdinalIgnoreCase))
            {
                string savePath = _host.GetVideoSavePath(generator);
                if (!string.IsNullOrEmpty(savePath))
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(PathUtils.ToAbsoluteAssetPath(savePath));
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        _currentVideoSavePath = savePath;
                    }
                    catch (Exception e)
                    {
                        TJLog.LogWarning($"[GenerationPipeline] 准备视频保存路径失败: {e.Message}");
                        _currentVideoSavePath = null;
                    }
                }
            }

            EnsureTransport(generator);

            // 直接进入轮询阶段
            yield return PollTaskStatus(generator, backendTaskId);
        }

        /// <summary>
        /// 发送生成请求到API
        /// </summary>
        private IEnumerator SendGenerationRequest(ModelGeneratorBase generator, string assetGuid)
        {
            string endpoint = generator.ApiEndpoint;
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                HandleError(generator, $"No API endpoint configured for generator '{generator.GeneratorId}'.");
                yield break;
            }

            string url = API_BASE_URL + endpoint;
            TJLog.Log($"[GenerationPipeline] Building request payload...");
            var requestData = generator.BuildRequestData();
            TJLog.Log($"[GenerationPipeline] 请求数据类型: {requestData?.GetType().Name ?? "null"}");

            // 在发送HTTP请求之前保存占位任务记录（使用localTaskId作为backendTaskId占位符）
            // 防止domain reload发生在HTTP请求等待期间导致任务记录丢失
            var submittingTaskData = generator.CreateInterruptedTaskData(generator.CurrentGeneratingTaskId, assetGuid);
            submittingTaskData.status = "submitting";
            TJGeneratorsTaskRecovery.AddInterruptedTask(submittingTaskData);

            EnsureTransport(generator);
            TJTaskResponse response = null;
            string transportError = null;

            // 检查是否是Multipart文件上传请求
            if (requestData is MultipartRequestData multipartData)
            {
                TJLog.Log($"[GenerationPipeline] 发送Multipart请求到: {url}");
                yield return _transport.CreateTaskMultipart(url, multipartData, r => response = r, e => transportError = e);
            }
            else
            {
                // 根据请求数据类型选择序列化方式
                string jsonData;
                if (requestData is DynamicRequestData dynamicData)
                {
                    // DynamicGenerator返回的已经是JSON字符串
                    jsonData = dynamicData.JsonContent;
                }
                else
                {
                    jsonData = JsonUtility.ToJson(requestData);
                }
                
                TJLog.Log($"[GenerationPipeline] 发送请求到: {url}");
                TJLog.Log($"[GenerationPipeline] 请求体: {jsonData}");
                
                byte[] postData = System.Text.Encoding.UTF8.GetBytes(jsonData);
                yield return _transport.CreateTask(url, postData, r => response = r, e => transportError = e);
            }

            if (!string.IsNullOrEmpty(transportError))
            {
                TJGeneratorsTaskRecovery.RemoveInterruptedTask(generator.CurrentGeneratingTaskId);
                HandleError(generator, transportError);
                yield break;
            }

            if (response != null && !string.IsNullOrEmpty(response.taskId))
            {
                TJLog.Log($"[GenerationPipeline] 任务ID: {response.taskId}");

                // 用真实backendTaskId替换占位记录
                TJGeneratorsTaskRecovery.RemoveInterruptedTask(generator.CurrentGeneratingTaskId);
                var taskData = generator.CreateInterruptedTaskData(response.taskId, assetGuid);
                TJGeneratorsTaskRecovery.AddInterruptedTask(taskData);
                generator.CurrentBackendTaskId = response.taskId;

                // Mark as actively recovering so TaskRecoveryHelper won't start a duplicate pipeline
                // if the 3D model window opens (OnEnable) while this task is still being polled.
                TJGeneratorsTaskRecovery.MarkAsRecovering(response.taskId);

                if (_activeTaskHandle != null)
                {
                    _activeTaskHandle.SetBackendTaskId(response.taskId);
                    _activeTaskHandle.SetStatus(string.IsNullOrEmpty(response.status) ? "pending" : response.status);
                    _activeTaskHandle.NotifyCreated();
                }

                // 开始轮询
                generator.ButtonText = "生成中...";
                _host.Repaint();
                EditorCoroutineUtility.StartCoroutineOwnerless(PollTaskStatus(generator, response.taskId));
            }
            else
            {
                TJGeneratorsTaskRecovery.RemoveInterruptedTask(generator.CurrentGeneratingTaskId);
                HandleError(generator, "响应数据无效");
            }
        }

        // ========== 轮询任务状态 ==========
        
        /// <summary>
        /// 轮询任务状态直到完成或失败
        /// </summary>
        public IEnumerator PollTaskStatus(ModelGeneratorBase generator, string taskId)
        {
            _pipelineSettings = generator.GetPipelineSettings();
            EnsureTransport(generator);
            string url = ConfigManager.GetPollStatusUrl(taskId);
            
            bool taskCompleted = false;
            int retryCount = 0;
            
            while (!taskCompleted && retryCount < MAX_POLL_RETRIES)
            {
                retryCount++;
                TJLog.Log($"[GenerationPipeline] 轮询 {retryCount}/{MAX_POLL_RETRIES}");

                TJTaskStatusResponse response = null;
                string transportError = null;
                yield return _transport.PollStatus(taskId, url, r => response = r, e => transportError = e);

                if (!string.IsNullOrEmpty(transportError))
                {
                    if (retryCount >= MAX_POLL_RETRIES)
                    {
                        HandleError(generator, transportError);
                        yield break;
                    }
                    yield return WaitSeconds(POLL_INTERVAL);
                    continue;
                }

                if (response != null)
                {
                    TJLog.Log($"[GenerationPipeline] 任务状态: {response.status}, 进度: {response.progress}");

                    // 更新UI状态
                    generator.UpdateButtonStatus(response.status, response.progress);
                    UpdateHistoryProgress(generator, response.progress);
                    if (_activeTaskHandle != null)
                    {
                        // 轮询时提前提取预览图URL（后端可能在generating阶段就返回）
                        if (string.IsNullOrEmpty(_activeTaskHandle.PreviewUrl))
                        {
                            string previewUrl = generator.GetPreviewImageUrl(response);
                            if (!string.IsNullOrEmpty(previewUrl))
                                _activeTaskHandle.SetPreviewUrl(previewUrl);
                        }
                        _activeTaskHandle.UpdateProgress(response.status, response.progress);
                    }
                    _host.Repaint();

                    if (response.status == "completed")
                    {
                        // 原子移除：只有成功移除任务记录的协程才执行下载，防止多个 PollTaskStatus
                        // 协程（因 domain reload 重复恢复导致）同时触发重复下载
                        bool shouldDownload = true;
                        if (!string.IsNullOrEmpty(generator.CurrentBackendTaskId))
                        {
                            shouldDownload = TJGeneratorsTaskRecovery.RemoveInterruptedTask(generator.CurrentBackendTaskId);
                            if (!shouldDownload)
                                TJLog.Log($"[GenerationPipeline] 任务 {generator.CurrentBackendTaskId} 已被其他协程处理，跳过重复下载。");
                        }
                        if (shouldDownload)
                        {
                            TJLog.Log("[GenerationPipeline] 任务完成，开始下载...");
                            yield return CompleteTask(generator, response);
                        }
                        taskCompleted = true;
                    }
                    else if (response.status == "failed" || response.status == "error" || response.status == "cancelled")
                    {
                        string detail = !string.IsNullOrEmpty(response.error) ? response.error
                            : (!string.IsNullOrEmpty(response.message) ? response.message : null);
                        
                        // 为特定错误提供更详细的错误信息
                        string enhancedError = EnhanceErrorMessage(detail, generator);
                        string msg;
                        if (response.status == "cancelled")
                            msg = !string.IsNullOrEmpty(detail) ? $"任务已取消: {detail}" : "任务已取消";
                        else
                            msg = !string.IsNullOrEmpty(enhancedError) ? enhancedError : 
                                    (!string.IsNullOrEmpty(detail) ? $"任务失败: {detail}" : $"任务失败: {response.status}");
                        
                        HandleError(generator, msg, response.status == "cancelled" ? "cancelled" : "error");
                        taskCompleted = true;
                    }
                }
                else
                {
                    HandleError(generator, "响应数据无效");
                    taskCompleted = true;
                }
                
                // 等待下次轮询
                if (!taskCompleted && retryCount < MAX_POLL_RETRIES)
                {
                    yield return WaitSeconds(POLL_INTERVAL);
                }
            }
            
            if (!taskCompleted && retryCount >= MAX_POLL_RETRIES)
            {
                HandlePollingTimeout(generator, "轮询超时，任务可能仍在后端运行。重新打开窗口可继续等待。");
            }
        }
        
        // ========== 任务完成处理 ==========
        
        /// <summary>
        /// 处理任务完成
        /// </summary>
        private IEnumerator CompleteTask(ModelGeneratorBase generator, TJTaskStatusResponse response)
        {
            // 提取预览图URL（用于历史记录显示）
            string previewImageUrl = generator.GetPreviewImageUrl(response);
            if (!string.IsNullOrEmpty(previewImageUrl))
            {
                TJLog.Log($"[GenerationPipeline] 获取到预览图URL: {previewImageUrl}");
            }
            
            // 将预览图URL存储到generator临时状态中
            _currentPreviewUrl = previewImageUrl;
            
            if (_activeTaskHandle != null)
            {
                _activeTaskHandle.SetPreviewUrl(previewImageUrl);
            }

            // 根据输出类型分流处理
            string outputType = generator.GetOutputType();
            if (string.Equals(outputType, "audio", StringComparison.OrdinalIgnoreCase))
            {
                yield return HandleAudioAsset(generator, response);
                yield break;
            }
            if (string.Equals(outputType, "video", StringComparison.OrdinalIgnoreCase))
            {
                yield return HandleVideoAsset(generator, response);
                yield break;
            }
            if (string.Equals(outputType, "sprite_sequence", StringComparison.OrdinalIgnoreCase))
            {
                yield return HandleSpriteSequenceAsset(generator, response);
                yield break;
            }
            if (outputType != "model" &&
                !string.Equals(outputType, "rigged-model", StringComparison.OrdinalIgnoreCase))
            {
                // 非3D模型资产（texture / 天空盒 / sprite）：走纹理下载流程
                yield return HandleTextureAsset(generator, response);
                yield break;
            }
            
            // ===== 以下为3D模型资产的原有流程 =====
            
            // 检查是否需要转换
            if (_pipelineSettings.NeedsConversion())
            {
                generator.ButtonText = "转换中...";
                _host.Repaint();

                // 根据配置的转换类型选择转换方式
                string convertType = _pipelineSettings.GetConvertType();
                if (convertType == "url")
                {
                    // URL转换：使用源URL进行转换
                    string sourceUrl = _pipelineSettings.GetConvertSourceUrl(response);
                    string convertEndpoint = _pipelineSettings.GetConvertEndpoint();
                    yield return ConvertByUrl(generator, sourceUrl, convertEndpoint);
                }
                else
                {
                    // TaskId转换：使用原始任务ID进行转换
                    yield return ConvertToFBX(generator, response.taskId);
                }
            }
            else
            {
                // 直接下载模型
                generator.ButtonText = "下载中...";
                _host.Repaint();

                string modelUrl = generator.GetDownloadUrl(response);

                // 获取 rendered_image (webp) 用作 FBX 主贴图（通过配置映射）
                string renderedImageUrl = generator.GetRenderedImageUrl(response);
                bool isFBX = !string.IsNullOrEmpty(modelUrl) && modelUrl.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);

                // 如果是GLB文件，保存URL用于后续转换
                if (!string.IsNullOrEmpty(modelUrl) && modelUrl.Contains(".glb"))
                {
                    _currentSourceGlbUrl = modelUrl;
                }

                if (!string.IsNullOrEmpty(modelUrl))
                {
                    // 根据URL确定实际文件扩展名
                    string fileName = generator.GetModelFileName();
                    string actualExtension = GetExtensionFromUrl(modelUrl);
                    if (!string.IsNullOrEmpty(actualExtension))
                    {
                        // 替换文件扩展名为实际的扩展名
                        string baseName = Path.GetFileNameWithoutExtension(fileName);
                        fileName = baseName + actualExtension;
                    }
                    string savePath = GetModelSavePath(fileName);

                    TJLog.Log($"[GenerationPipeline] 开始下载: {modelUrl}");
                    
                    // 检查是否有动画URL（用于带动画的模型）
                    string animationUrl = generator.GetAnimationUrl(response);
                    string walkingAnimUrl = generator.GetWalkingAnimationUrl(response);
                    string runningAnimUrl = generator.GetRunningAnimationUrl(response);
                    bool hasAnimations = !string.IsNullOrEmpty(animationUrl) || 
                                         !string.IsNullOrEmpty(walkingAnimUrl) || 
                                         !string.IsNullOrEmpty(runningAnimUrl);
                    
                    yield return DownloadModel(generator, modelUrl, savePath, isFBX, renderedImageUrl, 
                        hasAnimations ? response : null);
                }
                else
                {
                    // 始终走 HandleError：在 _activeTaskHandle 为 null 时仍须 RemovePlaceholder/RefreshHistory，否则会留下一直转圈的历史项
                    HandleError(generator, "未找到模型下载URL");
                }
            }
        }
        
        // ========== 纹理/图片资产处理（texture / 天空盒 / sprite）==========
        
        /// <summary>
        /// 处理非3D模型资产的下载和保存（天空盒、贴图、精灵图等）。支持单图或多图（如 image_urls 数组）。
        /// </summary>
        private IEnumerator HandleTextureAsset(ModelGeneratorBase generator, TJTaskStatusResponse response)
        {
            EnsureTransport(generator);
            string[] downloadUrls = generator.GetDownloadUrls(response);
            if (downloadUrls == null || downloadUrls.Length == 0)
            {
                string singleUrl = generator.GetDownloadUrl(response);
                if (string.IsNullOrEmpty(singleUrl))
                {
                    HandleError(generator, "未找到纹理资产下载URL");
                    yield break;
                }
                downloadUrls = new[] { singleUrl };
            }

            string firstSavePath = _host.GetTextureSavePath(generator);
            if (string.IsNullOrEmpty(firstSavePath))
            {
                HandleError(generator, "无法确定纹理资产保存路径");
                yield break;
            }

            firstSavePath = firstSavePath.Replace('\\', '/');

            string dir = Path.GetDirectoryName(firstSavePath);
            string baseName = Path.GetFileNameWithoutExtension(firstSavePath);
            // 不使用预设的扩展名，而是根据实际下载的图片格式确定
            // string ext = Path.GetExtension(firstSavePath);

            _currentPreviewUrl = downloadUrls.Length > 0 ? downloadUrls[0] : null;

            var savePaths = new List<string>();

            for (int i = 0; i < downloadUrls.Length; i++)
            {
                string url = downloadUrls[i];
                if (string.IsNullOrEmpty(url)) continue;

                // 先使用临时路径，下载后根据实际格式确定扩展名
                string savePath = i == 0 ? firstSavePath : Path.Combine(dir, baseName + "_" + i);
                savePaths.Add(savePath);

                generator.ButtonText = downloadUrls.Length > 1 ? $"下载中 ({i + 1}/{downloadUrls.Length})..." : "下载中...";
                _host.Repaint();

                TJLog.Log($"[GenerationPipeline] 开始下载纹理资产 [{i + 1}/{downloadUrls.Length}]: {url} -> {savePath}");

                byte[] imageData = null;
                string downloadError = null;
                yield return _transport.DownloadBytes(url, bytes => imageData = bytes, err => downloadError = err);

                if (!string.IsNullOrEmpty(downloadError))
                {
                    HandleError(generator, downloadError);
                    yield break;
                }
                if (imageData == null || imageData.Length == 0)
                {
                    HandleError(generator, "下载的纹理数据为空");
                    yield break;
                }

                TJLog.Log($"[GenerationPipeline] 纹理下载完成 [{i + 1}/{downloadUrls.Length}], 大小: {imageData.Length} bytes");

                // 根据实际图片格式确定扩展名
                string actualExtension = GetImageExtensionFromData(imageData, url);
                if (i == 0)
                {
                    // 主图：扩展名与字节格式一致（如后端 PNG 则保存为 .png），不再沿用占位路径的 .jpg
                    string pathWithActualExt =
                        Path.ChangeExtension(firstSavePath, actualExtension).Replace('\\', '/');
                    if (
                        string.Equals(
                            pathWithActualExt,
                            firstSavePath,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        savePath = firstSavePath;
                    }
                    else
                    {
                        string absPlaceholder = PathUtils.ToAbsoluteAssetPath(firstSavePath);
                        if (File.Exists(absPlaceholder))
                        {
                            if (AssetDatabase.LoadMainAssetAtPath(firstSavePath) != null)
                                AssetDatabase.DeleteAsset(firstSavePath);
                            else
                                File.Delete(absPlaceholder);
                        }

                        savePath = AssetDatabase.GenerateUniqueAssetPath(pathWithActualExt);
                    }
                }
                else
                {
                    savePath = Path.ChangeExtension(savePath, actualExtension).Replace('\\', '/');
                    savePath = AssetDatabase.GenerateUniqueAssetPath(savePath); // 确保非主图路径唯一
                }
                savePaths[i] = savePath; // 更新保存路径

                string absoluteSavePath = PathUtils.ToAbsoluteAssetPath(savePath);
                string directory = Path.GetDirectoryName(absoluteSavePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(absoluteSavePath, imageData);
                AssetDatabase.ImportAsset(savePath, ImportAssetOptions.ForceUpdate);

                yield return null;

                // 计算 effectivePreviewUrl（此时 savePaths[0] 文件已写入磁盘）
                {
                    string ep = _currentPreviewUrl;

                    // Priority 2: result URL（Sprite / 材质 / 天空盒 等图片类型）
                    if (string.IsNullOrEmpty(ep) && downloadUrls != null && downloadUrls.Length > 0)
                        ep = downloadUrls[0];

                    // Priority 3: 本地文件 URI（文件已存在）
                    if (string.IsNullOrEmpty(ep))
                    {
                        string fullPath = Path.GetFullPath(savePaths[0]);
                        if (File.Exists(fullPath))
                            ep = "file://" + fullPath.Replace('\\', '/');
                    }

                    generator.CurrentPreviewUrl = ep;
                }

                if (i == 0)
                {
                    _host.OnTextureSaved(savePath, generator);
                }
            }

            // 使用实际保存的路径（savePaths[0] 可能因扩展名改变而与 firstSavePath 不同）
            string actualModelPath = savePaths.Count > 0 ? savePaths[0] : firstSavePath;
            CompleteGeneration(generator, actualModelPath, downloadUrls, savePaths);
        }

        /// <summary>
        /// 处理 2D 序列帧：下载多帧图片，导入为 Sprite，并生成 AnimationClip。
        /// 历史记录以生成的 AnimationClip 路径作为 modelPath。
        /// </summary>
        private IEnumerator HandleSpriteSequenceAsset(ModelGeneratorBase generator, TJTaskStatusResponse response)
        {
            EnsureTransport(generator);

            string[] frameUrls = generator.GetDownloadUrls(response);
            if (frameUrls == null || frameUrls.Length == 0)
            {
                string singleUrl = generator.GetDownloadUrl(response);
                if (string.IsNullOrEmpty(singleUrl))
                {
                    HandleError(generator, "未找到序列帧下载URL");
                    yield break;
                }
                frameUrls = new[] { singleUrl };
            }

            // 读取参数：fps / loop（DynamicGenerator 会将参数写入请求）
            int fps = 12;
            bool loop = true;
            if (generator is IGeneratorParameterProvider paramProvider)
            {
                if (paramProvider.GetParameter("fps") is int fpsInt) fps = fpsInt;
                else if (int.TryParse(paramProvider.GetParameter("fps")?.ToString(), out int fpsParsed)) fps = fpsParsed;

                if (paramProvider.GetParameter("loop") is bool loopBool) loop = loopBool;
                else if (bool.TryParse(paramProvider.GetParameter("loop")?.ToString(), out bool loopParsed)) loop = loopParsed;
            }
            fps = Mathf.Clamp(fps, 1, 60);

            // 生成保存目录
            if (!AssetDatabase.IsValidFolder("Assets/TJGenerators"))
                AssetDatabase.CreateFolder("Assets", "TJGenerators");
            if (!AssetDatabase.IsValidFolder("Assets/TJGenerators/History"))
                AssetDatabase.CreateFolder("Assets/TJGenerators", "History");

            string folderName = "Sequence_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folderPath = "Assets/TJGenerators/History/" + folderName;
            string absFolder = PathUtils.ToAbsoluteAssetPath(folderPath);
            if (!Directory.Exists(absFolder))
                Directory.CreateDirectory(absFolder);

            var frameSpritePaths = new List<string>();

            // 计算参考图尺寸与 PPU，用于等比调整序列帧的 spritePixelsPerUnit
            // 确保序列帧动画播放时精灵在场景中的大小与参考图一致
            int refWidth = 0;
            float refPPU = 100f;
            string refImagePath = generator.GetImagePath();
            if (!string.IsNullOrEmpty(refImagePath))
            {
                try
                {
                    string absRefPath = PathUtils.ToAbsoluteAssetPath(refImagePath);
                    if (File.Exists(absRefPath))
                    {
                        var refTex = new Texture2D(2, 2);
                        if (refTex.LoadImage(File.ReadAllBytes(absRefPath)))
                            refWidth = refTex.width;
                        UnityEngine.Object.DestroyImmediate(refTex);
                    }
                    var refImporter = AssetImporter.GetAtPath(refImagePath) as TextureImporter;
                    if (refImporter != null)
                        refPPU = refImporter.spritePixelsPerUnit;
                }
                catch { /* 读取失败则使用默认值 */ }
            }

            for (int i = 0; i < frameUrls.Length; i++)
            {
                string url = frameUrls[i];
                if (string.IsNullOrEmpty(url)) continue;

                generator.ButtonText = $"下载中 ({i + 1}/{frameUrls.Length})...";
                _host.Repaint();

                byte[] imageData = null;
                string downloadError = null;
                yield return _transport.DownloadBytes(url, bytes => imageData = bytes, err => downloadError = err);

                if (!string.IsNullOrEmpty(downloadError))
                {
                    HandleError(generator, downloadError);
                    yield break;
                }
                if (imageData == null || imageData.Length == 0)
                {
                    HandleError(generator, "下载的帧数据为空");
                    yield break;
                }

                string ext = GetImageExtensionFromData(imageData, url);
                string frameAssetPath = $"{folderPath}/frame_{i + 1:0000}.{ext.TrimStart('.')}";
                frameAssetPath = AssetDatabase.GenerateUniqueAssetPath(frameAssetPath);
                File.WriteAllBytes(PathUtils.ToAbsoluteAssetPath(frameAssetPath), imageData);
                AssetDatabase.ImportAsset(frameAssetPath, ImportAssetOptions.ForceUpdate);

                var importer = AssetImporter.GetAtPath(frameAssetPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    // 等比设置 PPU：使序列帧与参考图在场景中显示相同大小
                    if (refWidth > 0)
                    {
                        var frameTex = new Texture2D(2, 2);
                        if (frameTex.LoadImage(imageData) && frameTex.width > 0)
                            importer.spritePixelsPerUnit = refPPU * frameTex.width / refWidth;
                        UnityEngine.Object.DestroyImmediate(frameTex);
                    }
                    importer.SaveAndReimport();
                }
                TJGeneratorsGenerationLabel.EnableLabel(TJGeneratorsAssetReference.FromPath(frameAssetPath));

                frameSpritePaths.Add(frameAssetPath);
                yield return null;
            }

            if (frameSpritePaths.Count == 0)
            {
                HandleError(generator, "未生成任何帧图片");
                yield break;
            }

            // 创建历史用 AnimationClip（唯一路径）
            string historyClipPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{folderName}.anim");
            var historyClip = new AnimationClip();
            historyClip.frameRate = fps;
            AssetDatabase.CreateAsset(historyClip, historyClipPath);
            AssetDatabase.ImportAsset(historyClipPath, ImportAssetOptions.ForceUpdate);

            // 构建 Sprite 序列
            var sprites = frameSpritePaths
                .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p))
                .Where(s => s != null)
                .ToList();

            if (sprites.Count == 0)
            {
                HandleError(generator, "导入帧图片失败（未加载到 Sprite）");
                yield break;
            }

            // 写入曲线（SpriteRenderer.m_Sprite）
            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

            var keys = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i / (float)fps,
                    value = sprites[i]
                };
            }

            ApplySpriteCurveToClip(historyClip, binding, keys, loop);
            EditorUtility.SetDirty(historyClip);
            AssetDatabase.SaveAssets();

            // 若绑定了目标 AnimationClip，则同步更新它（保留历史 clip 独立路径）
            var targetAsset = _host.GetTargetAsset();
            if (targetAsset != null && targetAsset.IsValid())
            {
                string targetPath = targetAsset.GetPath();
                var targetClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);
                if (targetClip != null)
                {
                    targetClip.frameRate = fps;
                    ApplySpriteCurveToClip(targetClip, binding, keys, loop);
                    EditorUtility.SetDirty(targetClip);
                    AssetDatabase.SaveAssets();
                    TJGeneratorsGenerationLabel.EnableLabel(TJGeneratorsAssetReference.FromPath(targetPath));
                }
            }

            // 计算 effectivePreviewUrl（frameSpritePaths 已全部写入磁盘）
            {
                string ep = _currentPreviewUrl;  // Priority 1: API preview_url

                // Priority 2: 首帧 CDN 图片 URL（frameUrls[0]，有效图片预览）
                if (string.IsNullOrEmpty(ep) && frameUrls != null && frameUrls.Length > 0)
                    ep = frameUrls[0];

                // Priority 3: 本地 .anim 文件路径
                if (string.IsNullOrEmpty(ep))
                {
                    string fullPath = Path.GetFullPath(historyClipPath);
                    if (File.Exists(fullPath))
                        ep = "file://" + fullPath.Replace('\\', '/');
                }

                generator.CurrentPreviewUrl = ep;
            }

            CompleteGeneration(generator, historyClipPath);
        }

        private static void ApplySpriteCurveToClip(AnimationClip clip, EditorCurveBinding binding, ObjectReferenceKeyframe[] keys, bool loop)
        {
            if (clip == null) return;
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            try
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }
            catch
            {
                // 某些 Unity 版本下 AnimationClipSettings 可能不可用，忽略 loop 设置
            }
        }

        /// <summary>
        /// 处理文生音频：下载到开始时创建的占位路径，覆盖后重新导入，应用到同一 AudioClip。
        /// </summary>
        private IEnumerator HandleAudioAsset(ModelGeneratorBase generator, TJTaskStatusResponse response)
        {
            string url = generator.GetDownloadUrl(response);
            if (string.IsNullOrEmpty(url))
            {
                HandleError(generator, "未找到音频下载URL");
                yield break;
            }
            string savePath = _currentAudioSavePath;
            if (string.IsNullOrEmpty(savePath))
            {
                HandleError(generator, "无法确定音频保存路径（占位未创建）");
                yield break;
            }

            generator.ButtonText = "下载中...";
            _host.Repaint();

            TJLog.Log($"[GenerationPipeline] 开始下载音频: {url} -> {savePath}");
            _currentPreviewUrl = url;

            using (UnityWebRequest uwr = UnityWebRequest.Get(url))
            {
                uwr.downloadHandler = new DownloadHandlerBuffer();
                yield return uwr.SendWebRequest();

                float timeout = 120f;
                float timeElapsed = 0f;
                float interval = 0.5f;
                while (uwr.result == UnityWebRequest.Result.InProgress && timeElapsed < timeout)
                {
                    double startWait = EditorApplication.timeSinceStartup;
                    while (EditorApplication.timeSinceStartup - startWait < interval)
                        yield return null;
                    timeElapsed += interval;
                }

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    HandleError(generator, ErrorDialogUtils.GetFriendlyErrorMessage(uwr, "下载音频失败"));
                    yield break;
                }

                byte[] audioData = uwr.downloadHandler.data;
                if (audioData == null || audioData.Length == 0)
                {
                    HandleError(generator, "下载的音频数据为空");
                    yield break;
                }

                string configuredAudioFormat = generator.GetPipelineSettings()?.AudioFormat ?? "wav";
                string extWithDot = GetAudioExtensionFromData(audioData, url, configuredAudioFormat);
                savePath = Path.ChangeExtension(savePath, extWithDot);
                TJLog.Log(
                    $"[GenerationPipeline] 音频扩展名（魔数/URL/配置）: {extWithDot}, 保存路径: {savePath}"
                );

                string absoluteSavePath = PathUtils.ToAbsoluteAssetPath(savePath);
                string directory = Path.GetDirectoryName(absoluteSavePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(absoluteSavePath, audioData);
                AssetDatabase.ImportAsset(savePath, ImportAssetOptions.ForceUpdate);

                // 计算 effectivePreviewUrl（优先 backend audio URL，fallback 到本地文件）
                {
                    string ep = _currentPreviewUrl;  // = audio_url（由 Step 2 config 驱动）

                    if (string.IsNullOrEmpty(ep))
                    {
                        string fullPath = Path.GetFullPath(savePath);
                        if (File.Exists(fullPath))
                            ep = "file://" + fullPath.Replace('\\', '/');
                    }

                    generator.CurrentPreviewUrl = ep;
                }

                _host.OnAudioSaved(savePath, generator);
            }

            CompleteGeneration(generator, savePath);
        }

        /// <summary>
        /// 处理视频资产：下载到保存路径，导入后通知 Host。
        /// </summary>
        private IEnumerator HandleVideoAsset(ModelGeneratorBase generator, TJTaskStatusResponse response)
        {
            string url = generator.GetDownloadUrl(response);
            if (string.IsNullOrEmpty(url))
            {
                HandleError(generator, "未找到视频下载URL");
                yield break;
            }
            string savePath = _currentVideoSavePath;
            if (string.IsNullOrEmpty(savePath))
            {
                HandleError(generator, "无法确定视频保存路径");
                yield break;
            }

            generator.ButtonText = "下载中...";
            _host.Repaint();

            TJLog.Log($"[GenerationPipeline] 开始下载视频: {url} -> {savePath}");

            using (UnityWebRequest uwr = UnityWebRequest.Get(url))
            {
                uwr.downloadHandler = new DownloadHandlerBuffer();
                yield return uwr.SendWebRequest();

                float timeout = ConfigManager.GetDownloadTimeout();
                float timeElapsed = 0f;
                float interval = 0.5f;
                while (uwr.result == UnityWebRequest.Result.InProgress && timeElapsed < timeout)
                {
                    double startWait = EditorApplication.timeSinceStartup;
                    while (EditorApplication.timeSinceStartup - startWait < interval)
                        yield return null;
                    timeElapsed += interval;
                }

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    HandleError(generator, ErrorDialogUtils.GetFriendlyErrorMessage(uwr, "下载视频失败"));
                    yield break;
                }

                byte[] videoData = uwr.downloadHandler.data;
                if (videoData == null || videoData.Length == 0)
                {
                    HandleError(generator, "下载的视频数据为空");
                    yield break;
                }

                // 根据实际视频格式确定扩展名
                string actualExtension = GetVideoExtensionFromData(videoData, url);
                savePath = Path.ChangeExtension(savePath, actualExtension);
                TJLog.Log($"[GenerationPipeline] 视频格式检测: {actualExtension}, 保存路径: {savePath}");

                string absoluteSavePath = PathUtils.ToAbsoluteAssetPath(savePath);
                string directory = Path.GetDirectoryName(absoluteSavePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(absoluteSavePath, videoData);
                AssetDatabase.ImportAsset(savePath, ImportAssetOptions.ForceUpdate);

                // 设置预览 URL
                {
                    string ep = _currentPreviewUrl;
                    if (string.IsNullOrEmpty(ep))
                    {
                        string fullPath = Path.GetFullPath(savePath);
                        if (File.Exists(fullPath))
                            ep = "file://" + fullPath.Replace('\\', '/');
                    }
                    generator.CurrentPreviewUrl = ep;
                }

                _host.OnVideoSaved(savePath, generator);
            }

            CompleteGeneration(generator, savePath);
        }

        /// <summary>
        /// 根据视频文件头部魔数或 URL 扩展名判断视频格式。
        /// </summary>
        private static string GetVideoExtensionFromData(byte[] data, string url)
        {
            // MP4: ftyp box
            if (data != null && data.Length > 8)
            {
                // Check for ftyp atom (MP4/MOV); ftyp is typically at offset 4
                if (data.Length > 11 && data[4] == 'f' && data[5] == 't' && data[6] == 'y' && data[7] == 'p')
                    return ".mp4";
            }

            // Fallback: check URL extension
            if (!string.IsNullOrEmpty(url))
            {
                if (url.Contains(".mp4")) return ".mp4";
                if (url.Contains(".webm")) return ".webm";
                if (url.Contains(".mov")) return ".mov";
            }

            return ".mp4";
        }

        /// <summary>
        /// 根据魔数 / URL / 配置决定音频落盘扩展名；将常见「AAC in MP4」规范为 .m4a，避免 Unity 将 .mp4 当视频导入触发 WindowsVideoMedia 错误。
        /// </summary>
        private static string GetAudioExtensionFromData(
            byte[] data,
            string url,
            string configuredAudioFormat
        )
        {
            string normalized = TJGeneratorsAudioAssetPathUtility.NormalizeImportedAudioFileExtension(
                configuredAudioFormat
            );
            string fallbackDot = "." + normalized;

            if (data == null || data.Length < 16)
                return FallbackAudioExtensionFromUrl(url, fallbackDot);

            if (
                data.Length >= 12
                && data[0] == 'R'
                && data[1] == 'I'
                && data[2] == 'F'
                && data[3] == 'F'
                && data[8] == 'W'
                && data[9] == 'A'
                && data[10] == 'V'
                && data[11] == 'E'
            )
                return ".wav";

            if (
                data.Length >= 4
                && data[0] == 'f'
                && data[1] == 'L'
                && data[2] == 'a'
                && data[3] == 'C'
            )
                return ".flac";

            if (
                data.Length >= 4
                && data[0] == 'O'
                && data[1] == 'g'
                && data[2] == 'g'
                && data[3] == 'S'
            )
                return ".ogg";

            if (data.Length >= 3 && data[0] == 'I' && data[1] == 'D' && data[2] == '3')
                return ".mp3";
            if ((data[0] & 0xFF) == 0xFF && (data[1] & 0xE0) == 0xE0)
                return ".mp3";

            if (
                data.Length > 11
                && data[4] == 'f'
                && data[5] == 't'
                && data[6] == 'y'
                && data[7] == 'p'
            )
                return ".m4a";

            if (
                data.Length >= 12
                && data[0] == 'F'
                && data[1] == 'O'
                && data[2] == 'R'
                && data[3] == 'M'
                && data[8] == 'A'
                && data[9] == 'I'
                && data[10] == 'F'
                && data[11] == 'F'
            )
                return ".aiff";

            return FallbackAudioExtensionFromUrl(url, fallbackDot);
        }

        private static string FallbackAudioExtensionFromUrl(string url, string fallbackDot)
        {
            if (string.IsNullOrEmpty(url))
                return fallbackDot;
            try
            {
                string pathPart = url;
                int q = pathPart.IndexOf('?', StringComparison.Ordinal);
                if (q >= 0)
                    pathPart = pathPart.Substring(0, q);
                string ext = Path.GetExtension(pathPart);
                if (string.IsNullOrEmpty(ext))
                    return fallbackDot;
                ext = ext.ToLowerInvariant();
                if (ext == ".mp4")
                    return ".m4a";
                if (ext == ".mp3" || ext == ".mpeg")
                    return ".mp3";
                if (ext == ".wav")
                    return ".wav";
                if (ext == ".ogg")
                    return ".ogg";
                if (ext == ".flac")
                    return ".flac";
                if (ext == ".m4a" || ext == ".aac")
                    return ".m4a";
            }
            catch
            {
                // ignore
            }

            return fallbackDot;
        }
        
        // ========== FBX转换 ==========

        /// <summary>
        /// 转换模型为FBX格式
        /// </summary>
        private IEnumerator ConvertToFBX(ModelGeneratorBase generator, string originalTaskId)
        {
            string endpoint = ConfigOptionsLoader.LoadEndpoint("tripo", "convert", "task/tripo-convert-model");
            string url = $"{API_BASE_URL}{endpoint}";
            
            var requestData = new ConvertModelRequestData
            {
                faceLimit = _pipelineSettings.GetConversionFaceLimit(),
                format = "FBX",
                originalModelTaskId = originalTaskId
            };
            
            string jsonData = JsonUtility.ToJson(requestData);
            TJLog.Log($"[GenerationPipeline] 转换请求: {jsonData}");
            
            byte[] postData = System.Text.Encoding.UTF8.GetBytes(jsonData);
            
            using (UnityWebRequest uwr = new UnityWebRequest(url, "POST"))
            {
                uwr.uploadHandler = new UploadHandlerRaw(postData);
                uwr.downloadHandler = new DownloadHandlerBuffer();
                uwr.SetRequestHeader("Content-Type", "application/json");
                
                string token = UnityConnectSession.instance.GetAccessToken();
                uwr.SetRequestHeader("Authorization", $"Bearer {token}");
                uwr.SetRequestHeader("source", "codely");
                
                yield return uwr.SendWebRequest();
                
                // 等待响应
                float timeout = 5000f;
                float timeElapsed = 0f;
                float interval = 0.5f;
                
                while (uwr.result == UnityWebRequest.Result.InProgress && timeElapsed < timeout)
                {
                    double startWait = EditorApplication.timeSinceStartup;
                    while (EditorApplication.timeSinceStartup - startWait < interval)
                    {
                        yield return null;
                    }
                    timeElapsed += interval;
                }
                
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string jsonResponse = uwr.downloadHandler.text;
                        TJLog.Log($"[GenerationPipeline] 转换响应: {TJLog.TruncateJsonFields(jsonResponse)}");
                        
                        TJTaskResponse response = JsonUtility.FromJson<TJTaskResponse>(jsonResponse);
                        
                        if (response != null && !string.IsNullOrEmpty(response.taskId))
                        {
                            TJLog.Log($"[GenerationPipeline] 转换任务ID: {response.taskId}");
                            EditorCoroutineUtility.StartCoroutineOwnerless(PollConvertTask(generator, response.taskId));
                        }
                        else
                        {
                            HandleError(generator, "转换响应数据无效");
                        }
                    }
                    catch (Exception e)
                    {
                        HandleError(generator, $"解析转换响应失败: {e.Message}");
                    }
                }
                else
                {
                    HandleError(generator, ErrorDialogUtils.GetFriendlyErrorMessage(uwr, "转换请求失败"));
                }
            }
        }
        
        /// <summary>
        /// 通过URL进行模型格式转换（如GLB转FBX）- 异步任务
        /// </summary>
        private IEnumerator ConvertByUrl(ModelGeneratorBase generator, string sourceUrl, string convertEndpoint)
        {
            if (string.IsNullOrEmpty(sourceUrl))
            {
                HandleError(generator, "源URL为空，无法进行格式转换");
                yield break;
            }

            // 使用配置的端点或默认端点
            string endpoint = convertEndpoint ?? ConfigOptionsLoader.LoadEndpoint(generator.GeneratorId, "convert", "task/hunyuan-3d-format-conversions");
            string url = $"{API_BASE_URL}{endpoint}";
            
            var requestData = new UrlConversionRequest
            {
                glbUrl = sourceUrl,
                responseFormat = "fbx"
            };

            string jsonData = requestData.ToJson();
            TJLog.Log($"[GenerationPipeline] URL转换请求: {jsonData}");
            
            byte[] postData = System.Text.Encoding.UTF8.GetBytes(jsonData);
            
            // 用于存储任务ID
            string taskId = null;
            string errorMessage = null;
            
            using (UnityWebRequest uwr = new UnityWebRequest(url, "POST"))
            {
                uwr.uploadHandler = new UploadHandlerRaw(postData);
                uwr.downloadHandler = new DownloadHandlerBuffer();
                uwr.SetRequestHeader("Content-Type", "application/json");
                
                string token = UnityConnectSession.instance.GetAccessToken();
                uwr.SetRequestHeader("Authorization", $"Bearer {token}");
                uwr.SetRequestHeader("source", "codely");
                
                yield return uwr.SendWebRequest();
                
                // 等待响应
                float timeout = 60f;
                float timeElapsed = 0f;
                float interval = 0.5f;
                
                while (uwr.result == UnityWebRequest.Result.InProgress && timeElapsed < timeout)
                {
                    double startWait = EditorApplication.timeSinceStartup;
                    while (EditorApplication.timeSinceStartup - startWait < interval)
                    {
                        yield return null;
                    }
                    timeElapsed += interval;
                }
                
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string jsonResponse = uwr.downloadHandler.text;
                        TJLog.Log($"[GenerationPipeline] URL转换任务响应: {TJLog.TruncateJsonFields(jsonResponse)}");

                        // 解析任务响应，获取taskId
                        TJTaskResponse response = JsonUtility.FromJson<TJTaskResponse>(jsonResponse);

                        if (response != null && !string.IsNullOrEmpty(response.taskId))
                        {
                            TJLog.Log($"[GenerationPipeline] URL转换任务ID: {response.taskId}");
                            taskId = response.taskId;
                        }
                        else
                        {
                            TJLog.LogError("[GenerationPipeline] URL转换响应中未找到任务ID");
                            errorMessage = "格式转换失败：未返回任务ID";
                        }
                    }
                    catch (Exception e)
                    {
                        TJLog.LogError($"[GenerationPipeline] 解析URL转换响应失败: {e.Message}");
                        errorMessage = $"解析转换响应失败: {e.Message}";
                    }
                }
                else
                {
                    TJLog.LogError($"[GenerationPipeline] URL转换请求失败: {uwr.error}");
                    TJLog.LogError($"[GenerationPipeline] 响应: {uwr.downloadHandler?.text}");
                    errorMessage = ErrorDialogUtils.GetFriendlyErrorMessage(uwr, "格式转换请求失败");
                }
            }

            // 如果获取到任务ID，开始轮询转换状态
            if (!string.IsNullOrEmpty(taskId))
            {
                yield return PollUrlConvertTask(generator, taskId);
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                HandleError(generator, errorMessage, errorMessage.StartsWith("转换已取消") ? "cancelled" : "error");
            }
        }
        
        /// <summary>
        /// 轮询URL转换任务状态
        /// </summary>
        private IEnumerator PollUrlConvertTask(ModelGeneratorBase generator, string taskId)
        {
            generator.ButtonText = "转换中...";
            _host.Repaint();

            string url = ConfigManager.GetPollStatusUrl(taskId);

            bool taskCompleted = false;
            int retryCount = 0;

            while (!taskCompleted && retryCount < MAX_POLL_RETRIES)
            {
                retryCount++;
                TJLog.Log($"[GenerationPipeline] URL转换轮询 {retryCount}/{MAX_POLL_RETRIES}");
                
                string token = UnityConnectSession.instance.GetAccessToken();
                if (string.IsNullOrEmpty(token))
                {
                    HandleError(generator, "认证token为空");
                    yield break;
                }
                
                UnityWebRequest uwr = UnityWebRequest.Get(url);
                uwr.downloadHandler = new DownloadHandlerBuffer();
                uwr.SetRequestHeader("Authorization", $"Bearer {token}");
                uwr.SetRequestHeader("source", "codely");
                
                yield return uwr.SendWebRequest();
                
                // 等待请求完成
                float requestTimeout = 30f;
                float requestElapsed = 0f;
                while (!uwr.isDone && requestElapsed < requestTimeout)
                {
                    requestElapsed += Time.deltaTime;
                    yield return null;
                }
                
                if (!uwr.isDone)
                {
                    uwr.Abort();
                    uwr.Dispose();
                    yield return WaitSeconds(POLL_INTERVAL);
                    continue;
                }
                
                // 用于存储解析结果
                string fbxUrl = null;
                string errorMessage = null;
                bool shouldContinue = false;
                
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = uwr.downloadHandler.text;
                    TJLog.Log($"[GenerationPipeline] URL转换状态响应: {TJLog.TruncateJsonFields(jsonResponse)}");
                    uwr.Dispose();
                    
                    try
                    {
                        // 解析为任务状态响应（Hunyuan转换任务返回标准格式）
                        TJTaskStatusResponse statusResponse = JsonUtility.FromJson<TJTaskStatusResponse>(jsonResponse);
                        
                        if (statusResponse != null)
                        {
                            TJLog.Log($"[GenerationPipeline] URL转换状态: {statusResponse.status}");
                            
                            if (statusResponse.status == "completed")
                            {
                                // 从pipelineSettings获取转换后的下载URL
                                string resultFbxUrl = _pipelineSettings.GetConvertDownloadUrl(statusResponse);
                                // 如果pipelineSettings没有实现，回退到默认路径
                                if (string.IsNullOrEmpty(resultFbxUrl))
                                {
                                    resultFbxUrl = statusResponse.output?.data?.result?.fbx_url;
                                }

                                if (!string.IsNullOrEmpty(resultFbxUrl))
                                {
                                    TJLog.Log($"[GenerationPipeline] URL转换完成，FBX URL: {resultFbxUrl}");
                                    fbxUrl = resultFbxUrl;
                                    taskCompleted = true;
                                }
                                else
                                {
                                    TJLog.LogWarning("[GenerationPipeline] 转换完成但未找到FBX URL");
                                    errorMessage = "转换完成但未找到FBX URL";
                                    taskCompleted = true;
                                }
                            }
                            else if (statusResponse.status == "failed" || statusResponse.status == "error" || statusResponse.status == "cancelled")
                            {
                                string detail = !string.IsNullOrEmpty(statusResponse.error) ? statusResponse.error
                                    : (!string.IsNullOrEmpty(statusResponse.message) ? statusResponse.message : null);
                                errorMessage = statusResponse.status == "cancelled"
                                    ? (!string.IsNullOrEmpty(detail) ? $"转换已取消: {detail}" : "转换已取消")
                                    : (!string.IsNullOrEmpty(detail) ? $"转换失败: {detail}" : $"转换失败: {statusResponse.status}");
                                taskCompleted = true;
                            }
                            else
                            {
                                // 继续轮询
                                shouldContinue = true;
                            }
                        }
                        else
                        {
                            TJLog.LogWarning("[GenerationPipeline] 无法解析URL转换状态响应");
                            shouldContinue = true;
                        }
                    }
                    catch (Exception e)
                    {
                        TJLog.LogError($"[GenerationPipeline] 解析URL转换状态失败: {e.Message}");
                        shouldContinue = true;
                    }
                }
                else
                {
                    TJLog.LogError($"[GenerationPipeline] URL转换状态查询失败: {uwr.error}");
                    uwr.Dispose();
                    shouldContinue = true;
                }
                
                // 处理结果
                if (!string.IsNullOrEmpty(fbxUrl))
                {
                    generator.ButtonText = "下载中...";
                    _host.Repaint();

                    // 根据URL确定实际文件扩展名
                    string fileName = generator.GetModelFileName();
                    string actualExtension = GetExtensionFromUrl(fbxUrl);
                    if (!string.IsNullOrEmpty(actualExtension))
                    {
                        string baseName = Path.GetFileNameWithoutExtension(fileName);
                        fileName = baseName + actualExtension;
                    }
                    string savePath = GetModelSavePath(fileName);

                    yield return DownloadModel(generator, fbxUrl, savePath, isFBX: true);
                    yield break;
                }
                else if (!string.IsNullOrEmpty(errorMessage))
                {
                    HandleError(generator, errorMessage, errorMessage.StartsWith("转换已取消") ? "cancelled" : "error");
                    yield break;
                }
                else if (shouldContinue)
                {
                    yield return WaitSeconds(POLL_INTERVAL);
                }
            }
            
            if (!taskCompleted && retryCount >= MAX_POLL_RETRIES)
            {
                HandlePollingTimeout(generator, "转换轮询超时");
            }
        }
        
        /// <summary>
        /// 轮询转换任务状态
        /// </summary>
        private IEnumerator PollConvertTask(ModelGeneratorBase generator, string taskId)
        {
            generator.ButtonText = "转换中...";

            string url = ConfigManager.GetPollStatusUrl(taskId);
            
            bool taskCompleted = false;
            int retryCount = 0;
            
            while (!taskCompleted && retryCount < MAX_POLL_RETRIES)
            {
                retryCount++;
                
                string token = UnityConnectSession.instance.GetAccessToken();
                if (string.IsNullOrEmpty(token))
                {
                    HandleError(generator, "认证token为空");
                    yield break;
                }
                
                UnityWebRequest uwr = UnityWebRequest.Get(url);
                uwr.downloadHandler = new DownloadHandlerBuffer();
                uwr.SetRequestHeader("Authorization", $"Bearer {token}");
                uwr.SetRequestHeader("source", "codely");
                
                yield return uwr.SendWebRequest();
                
                // 等待请求完成
                float requestTimeout = 30f;
                float requestElapsed = 0f;
                while (!uwr.isDone && requestElapsed < requestTimeout)
                {
                    requestElapsed += Time.deltaTime;
                    yield return null;
                }
                
                if (!uwr.isDone)
                {
                    uwr.Abort();
                    uwr.Dispose();
                    yield return WaitSeconds(POLL_INTERVAL);
                    continue;
                }
                
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = uwr.downloadHandler.text;
                    uwr.Dispose();
                    
                    TJTaskStatusResponse response = null;
                    try
                    {
                        response = JsonUtility.FromJson<TJTaskStatusResponse>(jsonResponse);
                    }
                    catch (Exception e)
                    {
                        TJLog.LogError($"[GenerationPipeline] 转换JSON解析错误: {e.Message}");
                        HandleError(generator, $"解析转换响应失败: {e.Message}");
                        taskCompleted = true;
                        continue;
                    }
                    
                    if (response != null)
                    {
                        UpdateConvertStatusUI(generator, response.status);
                        _host.Repaint();
                        
                        if (response.status == "completed")
                        {
                            TJLog.Log("[GenerationPipeline] 转换完成，开始下载FBX...");

                            // 从pipelineSettings获取转换后的下载URL
                            string modelUrl = _pipelineSettings.GetConvertDownloadUrl(response);
                            // 如果pipelineSettings没有实现，回退到默认路径
                            if (string.IsNullOrEmpty(modelUrl))
                            {
                                modelUrl = response.output?.data?.result?.model;
                            }
                            
                            if (!string.IsNullOrEmpty(modelUrl))
                            {
                                // 根据URL确定实际文件扩展名
                                string fileName = "TJGeneratorsModel.fbx";
                                string actualExtension = GetExtensionFromUrl(modelUrl);
                                if (!string.IsNullOrEmpty(actualExtension))
                                {
                                    fileName = "TJGeneratorsModel" + actualExtension;
                                }
                                string savePath = GetModelSavePath(fileName);

                                TJLog.Log($"[GenerationPipeline] 开始下载FBX: {modelUrl}");
                                yield return DownloadModel(generator, modelUrl, savePath, isFBX: true);
                            }
                            else
                            {
                                HandleError(generator, "转换响应中未找到模型URL");
                            }
                            taskCompleted = true;
                        }
                        else if (response.status == "failed" || response.status == "error" || response.status == "cancelled")
                        {
                            string detail = !string.IsNullOrEmpty(response.error) ? response.error
                                : (!string.IsNullOrEmpty(response.message) ? response.message : null);
                            string msg = response.status == "cancelled"
                                ? (!string.IsNullOrEmpty(detail) ? $"转换已取消: {detail}" : "转换已取消")
                                : (!string.IsNullOrEmpty(detail) ? $"转换失败: {detail}" : $"转换失败: {response.status}");
                            HandleError(generator, msg, response.status == "cancelled" ? "cancelled" : "error");
                            taskCompleted = true;
                        }
                    }
                    else
                    {
                        HandleError(generator, "转换响应数据无效");
                        taskCompleted = true;
                    }
                }
                else
                {
                    uwr.Dispose();
                }
                
                if (!taskCompleted)
                {
                    yield return WaitSeconds(POLL_INTERVAL);
                }
            }
        }
        
        private void UpdateConvertStatusUI(ModelGeneratorBase generator, string status)
        {
            generator.ButtonText = status switch
            {
                "pending" => "转换等待中...",
                "processing" => "转换中...",
                "completed" => "转换完成",
                "failed" => "转换失败",
                "error" => "转换错误",
                _ => "转换中..."
            };
        }
        
        // ========== 下载模型 ==========
        
        /// <summary>
        /// 下载模型文件。当 isFBX 且 renderedImageUrl 非空时，会下载 webp 贴图并应用到 FBX 材质。
        /// 如果 response 不为 null，还会下载动画模型文件。
        /// </summary>
        private IEnumerator DownloadModel(ModelGeneratorBase generator, string modelUrl, string savePath, bool isFBX = false, string renderedImageUrl = null, TJTaskStatusResponse response = null)
        {
            string uniquePath = GetUniqueFilePath(savePath, BuildDownloadModelAssetFileName(modelUrl, savePath));
            bool isZipFile = savePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || 
                             modelUrl.Contains(".zip");
            
            using (UnityWebRequest uwr = UnityWebRequest.Get(modelUrl))
            {
                uwr.downloadHandler = new DownloadHandlerBuffer();
                
                yield return uwr.SendWebRequest();
                
                float timeout = 300f;
                float timeElapsed = 0f;
                float interval = 0.5f;
                
                while (uwr.result == UnityWebRequest.Result.InProgress && timeElapsed < timeout)
                {
                    double startWait = EditorApplication.timeSinceStartup;
                    while (EditorApplication.timeSinceStartup - startWait < interval)
                    {
                        yield return null;
                    }
                    timeElapsed += interval;
                }
                
                if (uwr.result == UnityWebRequest.Result.ConnectionError ||
                    uwr.result == UnityWebRequest.Result.ProtocolError)
                {
                    TJLog.LogError($"[GenerationPipeline] 下载失败: {uwr.error}");
                    HandleError(generator, ErrorDialogUtils.GetFriendlyErrorMessage(uwr, "下载模型失败"));
                }
                else if (uwr.result == UnityWebRequest.Result.Success)
                {
                    byte[] modelData = uwr.downloadHandler.data;
                    string finalModelPath = uniquePath;
                    
                    if (isZipFile)
                    {
                        // 处理ZIP文件：解压并找到OBJ文件
                        finalModelPath = ExtractZipAndGetModelPath(modelData, uniquePath);
                        if (string.IsNullOrEmpty(finalModelPath))
                        {
                            HandleError(generator, "解压ZIP文件失败或未找到模型文件");
                            yield break;
                        }
                    }
                    else
                    {
                        File.WriteAllBytes(PathUtils.ToAbsoluteAssetPath(uniquePath), modelData);
                    }
                    
                    AssetDatabase.Refresh();

                    // 下载 rendered_image 并作为主贴图应用到模型
                    string renderedTexturePath = null;
                    if (!string.IsNullOrEmpty(renderedImageUrl))
                    {
                        string modelDir = Path.GetDirectoryName(finalModelPath);
                        string renderedBase = Path.GetFileNameWithoutExtension(finalModelPath);
                        string renderedFileName = $"{renderedBase}_render.webp";
                        renderedTexturePath = Path.Combine(modelDir, renderedFileName).Replace("\\", "/");
                        yield return DownloadRenderedImage(renderedImageUrl, renderedTexturePath);
                    }

                    // FBX模型后处理（含可选 rendered 贴图）
                    if (isFBX)
                    {
                        // 如果有动画文件需要下载，主模型不需要导入动画
                        bool hasAnimations = response != null && (
                            !string.IsNullOrEmpty(generator.GetAnimationUrl(response)) ||
                            !string.IsNullOrEmpty(generator.GetWalkingAnimationUrl(response)) ||
                            !string.IsNullOrEmpty(generator.GetRunningAnimationUrl(response))
                        );
                        ModelPostProcessing(finalModelPath, renderedTexturePath, hasAnimations);
                        AssetDatabase.Refresh();
                    }

                    // OBJ模型后处理（设置纹理等）
                    if (finalModelPath.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                    {
                        ObjModelPostProcessing(finalModelPath);
                        AssetDatabase.Refresh();
                    }

                    // GLB模型后处理（使用gltfast导入）
                    if (finalModelPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
                    {
                        GlbModelPostProcessing(finalModelPath);
                        AssetDatabase.Refresh();
                    }

                    // 下载动画模型文件（如果有）
                    if (response != null)
                    {
                        yield return DownloadAnimationModels(generator, response, finalModelPath);
                    }

                    // 混元 Motion 等：动画面片在单一主 FBX 内且无单独动画下载 URL 时，从主 FBX 建单状态自循环控制器
                    if (_pipelineSettings.GetPostProcessingSingleClipLoopAnimatorController()
                        && finalModelPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                        && !generator.GetAddMotionEnabled())
                    {
                        string modelDir = Path.GetDirectoryName(finalModelPath)?.Replace("\\", "/") ?? "";
                        string baseName = Path.GetFileNameWithoutExtension(finalModelPath);
                        CreateSingleClipLoopAnimatorController(modelDir, baseName, finalModelPath);
                    }

                    string modelPathForBind = finalModelPath;
                    if (generator.GetAddMotionEnabled())
                    {
                        _postMotionRiggedPath = null;
                        yield return RunMotionPostProcessing(
                            generator,
                            finalModelPath,
                            generator.GetMotionDescription(),
                            renderedTexturePath
                        );
                        if (!string.IsNullOrEmpty(_postMotionRiggedPath))
                            modelPathForBind = _postMotionRiggedPath;
                    }

                    // 绑定到Prefab：UniRig + 混元 Motion 后的 FBX 姿态/尺度已由管线决定，勿再套后处理里的 modelScale/rotation。
                    bool addMotion = generator.GetAddMotionEnabled();
                    float bindScale = addMotion ? 1f : _pipelineSettings.GetModelScale();
                    Vector3 bindRotation = addMotion ? Vector3.zero : _pipelineSettings.GetModelRotation();
                    BindModelToPrefab(modelPathForBind, bindScale, bindRotation);
                    
                    // 完成任务
                    CompleteGeneration(generator, modelPathForBind);
                }
            }
        }

        /// <summary>
        /// 轮询子任务（UniRig / 混元 Motion）直到完成或失败，不触发主任务的 HandleError。
        /// </summary>
        private IEnumerator PollSimpleTaskUntilComplete(
            ModelGeneratorBase generator,
            string taskId,
            string phaseLabel,
            MotionSubTaskPollOutcome outcome
        )
        {
            outcome.Completed = null;
            outcome.Error = null;
            EnsureTransport(generator);
            string pollUrl = ConfigManager.GetPollStatusUrl(taskId);

            for (int retry = 0; retry < MAX_POLL_RETRIES; retry++)
            {
                TJTaskStatusResponse resp = null;
                string transportError = null;
                yield return _transport.PollStatus(taskId, pollUrl, r => resp = r, e => transportError = e);

                if (!string.IsNullOrEmpty(transportError))
                {
                    yield return WaitSeconds(POLL_INTERVAL);
                    continue;
                }

                if (resp == null)
                {
                    outcome.Error = "无效响应";
                    yield break;
                }

                generator.ButtonText = $"{phaseLabel} {resp.status}...";
                _host.Repaint();

                if (resp.status == "completed")
                {
                    outcome.Completed = resp;
                    yield break;
                }

                if (resp.status == "failed" || resp.status == "error" || resp.status == "cancelled")
                {
                    outcome.Error = resp.status == "cancelled"
                        ? (!string.IsNullOrEmpty(resp.error) ? resp.error : "任务已取消")
                        : (!string.IsNullOrEmpty(resp.error)
                            ? resp.error
                            : (!string.IsNullOrEmpty(resp.message) ? resp.message : resp.status));
                    yield break;
                }

                yield return WaitSeconds(POLL_INTERVAL);
            }

            outcome.Error = "轮询超时";
        }

        private static string GetMappedDownloadUrl(TJTaskStatusResponse response, GeneratorConfig cfg)
        {
            if (response?.output?.data?.result == null || cfg?.responseMapping == null)
                return null;
            string path = cfg.responseMapping.downloadUrlPath;
            if (string.IsNullOrEmpty(path))
                path = "model";
            return PathUtils.GetString(response.output.data.result, path);
        }

        private void SetupRiggedCharacterImport(string assetPath) =>
            RiggedModelPostProcessUtils.SetupRiggedCharacterImport(assetPath);

        /// <summary>
        /// 主模型落地后：上传至 UniRig 绑骨，再请求混元 Motion，将动作剪辑绑定到绑骨 FBX。
        /// </summary>
        /// <param name="renderedTexturePath">主流程 rendered_image；仅在无法从进 UniRig 前的模型复用材质时作为回退。</param>
        private IEnumerator RunMotionPostProcessing(
            ModelGeneratorBase generator,
            string extractedModelPath,
            string motionDescription,
            string renderedTexturePath = null
        )
        {
            _postMotionRiggedPath = null;
            if (string.IsNullOrEmpty(extractedModelPath))
                yield break;

            string absMesh = PathUtils.ToAbsoluteAssetPath(extractedModelPath);
            if (!File.Exists(absMesh))
            {
                TJLog.LogWarning($"[GenerationPipeline] 后处理动作：模型文件不存在: {extractedModelPath}");
                yield break;
            }

            EnsureTransport(generator);

            var unirigCfg = ConfigManager.GetGeneratorConfig(ConfigType.Generator, "unirig");
            var motionCfg = ConfigManager.GetGeneratorConfig(ConfigType.Generator, "hunyuan-motion");
            if (unirigCfg == null || motionCfg == null)
            {
                TJLog.LogWarning("[GenerationPipeline] 后处理动作：未找到 unirig 或 hunyuan-motion 配置，跳过后处理");
                yield break;
            }

            string modelDir = Path.GetDirectoryName(extractedModelPath)?.Replace("\\", "/") ?? "";
            string baseName = Path.GetFileNameWithoutExtension(extractedModelPath);

            string unirigEndpoint = unirigCfg.GetEndpoint("default");
            if (string.IsNullOrEmpty(unirigEndpoint))
            {
                TJLog.LogWarning("[GenerationPipeline] 后处理动作：UniRig 端点未配置");
                yield break;
            }

            string unirigUrl = API_BASE_URL + unirigEndpoint;
            var multipart = new MultipartRequestData
            {
                FilePath = absMesh,
                FileName = Path.GetFileName(absMesh) ?? "model.fbx",
                FileFieldName = "file",
                AdditionalFields = null,
            };

            TJTaskResponse createResp = null;
            string createErr = null;
            generator.ButtonText = "提交绑骨任务...";
            _host.Repaint();
            yield return _transport.CreateTaskMultipart(
                unirigUrl,
                multipart,
                r => createResp = r,
                e => createErr = e
            );

            if (!string.IsNullOrEmpty(createErr) || createResp == null || string.IsNullOrEmpty(createResp.taskId))
            {
                TJLog.LogWarning($"[GenerationPipeline] 后处理动作：UniRig 提交失败: {createErr ?? "无 taskId"}");
                yield break;
            }

            var unirigOutcome = new MotionSubTaskPollOutcome();
            yield return PollSimpleTaskUntilComplete(generator, createResp.taskId, "绑骨", unirigOutcome);
            if (unirigOutcome.Completed == null)
            {
                TJLog.LogWarning(
                    $"[GenerationPipeline] 后处理动作：绑骨未完成: {unirigOutcome.Error ?? "未知错误"}"
                );
                yield break;
            }

            string riggedUrl = GetMappedDownloadUrl(unirigOutcome.Completed, unirigCfg);
            if (string.IsNullOrEmpty(riggedUrl))
            {
                TJLog.LogWarning("[GenerationPipeline] 后处理动作：绑骨响应中无模型 URL");
                yield break;
            }

            string riggedExt = GetExtensionFromUrl(riggedUrl) ?? ".fbx";
            string riggedSavePath = Path.Combine(modelDir, baseName + "_rigged" + riggedExt).Replace("\\", "/");
            generator.ButtonText = "下载绑骨模型...";
            _host.Repaint();
            yield return DownloadFile(riggedUrl, riggedSavePath);
            AssetDatabase.Refresh();

            if (!File.Exists(PathUtils.ToAbsoluteAssetPath(riggedSavePath)))
            {
                TJLog.LogWarning("[GenerationPipeline] 后处理动作：绑骨模型下载失败");
                yield break;
            }

            if (riggedSavePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                SetupRiggedCharacterImport(riggedSavePath);

            AssetDatabase.Refresh();

            // 若 Unity 未能自动识别全部必要骨骼（如 UniRig 输出中 Chest/Neck 等被漏掉），补全映射并重新导入
            if (riggedSavePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                RiggedModelPostProcessUtils.TryFixHumanoidBoneMapping(riggedSavePath);

            int materialsApplied = ApplyMaterialsFromSourceModelToRiggedModel(extractedModelPath, riggedSavePath);
            if (materialsApplied == 0 && !string.IsNullOrEmpty(renderedTexturePath))
                ApplyRenderedTextureToImportedModel(riggedSavePath, renderedTexturePath);

            _postMotionRiggedPath = riggedSavePath;

            if (string.IsNullOrWhiteSpace(motionDescription))
            {
                TJLog.Log("[GenerationPipeline] 后处理动作：无动作描述，仅完成绑骨");
                yield break;
            }

            string motionEndpoint = motionCfg.GetEndpoint("default");
            if (string.IsNullOrEmpty(motionEndpoint))
            {
                TJLog.LogWarning("[GenerationPipeline] 后处理动作：混元 Motion 端点未配置");
                yield break;
            }

            string motionUrl = API_BASE_URL + motionEndpoint;
            var motionPayload = new HyMotionPostPayload
            {
                inputText = motionDescription.Trim(),
                actionDuration = 5f,
                cfgStrength = 5f,
                randomSeedList = "0",
            };
            string motionJson = JsonUtility.ToJson(motionPayload);
            byte[] motionBytes = Encoding.UTF8.GetBytes(motionJson);

            TJTaskResponse motionCreate = null;
            string motionCreateErr = null;
            generator.ButtonText = "提交动作生成...";
            _host.Repaint();
            yield return _transport.CreateTask(motionUrl, motionBytes, r => motionCreate = r, e => motionCreateErr = e);

            if (
                !string.IsNullOrEmpty(motionCreateErr)
                || motionCreate == null
                || string.IsNullOrEmpty(motionCreate.taskId)
            )
            {
                TJLog.LogWarning(
                    $"[GenerationPipeline] 后处理动作：混元 Motion 提交失败: {motionCreateErr ?? "无 taskId"}"
                );
                yield break;
            }

            var motionOutcome = new MotionSubTaskPollOutcome();
            yield return PollSimpleTaskUntilComplete(
                generator,
                motionCreate.taskId,
                "动作生成",
                motionOutcome
            );
            if (motionOutcome.Completed == null)
            {
                TJLog.LogWarning(
                    $"[GenerationPipeline] 后处理动作：动作任务未完成: {motionOutcome.Error ?? "未知"}"
                );
                yield break;
            }

            string motionFbxUrl = GetMappedDownloadUrl(motionOutcome.Completed, motionCfg);
            if (string.IsNullOrEmpty(motionFbxUrl))
            {
                TJLog.LogWarning("[GenerationPipeline] 后处理动作：混元 Motion 响应中无下载 URL");
                yield break;
            }

            string motionExt = GetExtensionFromUrl(motionFbxUrl) ?? ".fbx";
            string motionSavePath = Path.Combine(modelDir, baseName + "_motion" + motionExt).Replace("\\", "/");
            generator.ButtonText = "下载动作模型...";
            _host.Repaint();
            yield return DownloadFile(motionFbxUrl, motionSavePath);
            AssetDatabase.Refresh();

            if (!File.Exists(PathUtils.ToAbsoluteAssetPath(motionSavePath)))
            {
                TJLog.LogWarning("[GenerationPipeline] 后处理动作：动作文件下载失败");
                yield break;
            }

            if (motionSavePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                SetupAnimationImport(motionSavePath);

            AssetDatabase.Refresh();

            string riggedBaseName = Path.GetFileNameWithoutExtension(riggedSavePath);
            generator.ButtonText = "创建动画控制器...";
            _host.Repaint();
            CreateSingleClipLoopAnimatorControllerFromMotionClip(modelDir, riggedBaseName, motionSavePath);
        }
        
        /// <summary>
        /// 下载动画模型文件（动画模型、行走动画、奔跑动画）
        /// </summary>
        private IEnumerator DownloadAnimationModels(ModelGeneratorBase generator, TJTaskStatusResponse response, string mainModelPath)
        {
            string modelDir = Path.GetDirectoryName(mainModelPath);
            string baseName = Path.GetFileNameWithoutExtension(mainModelPath);
            
            // 获取动画URL
            string animationUrl = generator.GetAnimationUrl(response);
            string walkingAnimUrl = generator.GetWalkingAnimationUrl(response);
            string runningAnimUrl = generator.GetRunningAnimationUrl(response);
            
            // 记录下载的动画路径
            string animPath = null;
            string walkPath = null;
            string runPath = null;
            
            // 下载动画模型
            if (!string.IsNullOrEmpty(animationUrl))
            {
                generator.ButtonText = "下载动画...";
                _host.Repaint();

                string animExt = GetExtensionFromUrl(animationUrl) ?? ".fbx";
                animPath = Path.Combine(modelDir, baseName + "_animation" + animExt).Replace("\\", "/");
                TJLog.Log($"[GenerationPipeline] 下载动画模型: {animationUrl} -> {animPath}");
                yield return DownloadFile(animationUrl, animPath);
            }

            // 下载行走动画
            if (!string.IsNullOrEmpty(walkingAnimUrl))
            {
                generator.ButtonText = "下载行走动画...";
                _host.Repaint();

                string walkExt = GetExtensionFromUrl(walkingAnimUrl) ?? ".fbx";
                walkPath = Path.Combine(modelDir, baseName + "_walking" + walkExt).Replace("\\", "/");
                TJLog.Log($"[GenerationPipeline] 下载行走动画: {walkingAnimUrl} -> {walkPath}");
                yield return DownloadFile(walkingAnimUrl, walkPath);
            }

            // 下载奔跑动画
            if (!string.IsNullOrEmpty(runningAnimUrl))
            {
                generator.ButtonText = "下载奔跑动画...";
                _host.Repaint();

                string runExt = GetExtensionFromUrl(runningAnimUrl) ?? ".fbx";
                runPath = Path.Combine(modelDir, baseName + "_running" + runExt).Replace("\\", "/");
                TJLog.Log($"[GenerationPipeline] 下载奔跑动画: {runningAnimUrl} -> {runPath}");
                yield return DownloadFile(runningAnimUrl, runPath);
            }

            // Refresh 先让 AssetDatabase 注册新文件（此时以默认 Generic 导入）；
            // 之后再调用 SetupAnimationImport，此时 AssetImporter.GetAtPath 才不为 null，
            // 才能成功将动画 FBX 重新导入为 Humanoid，使其能驱动人形骨骼。
            AssetDatabase.Refresh();

            if (!string.IsNullOrEmpty(animPath) && animPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                SetupAnimationImport(animPath);
            if (!string.IsNullOrEmpty(walkPath) && walkPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                SetupAnimationImport(walkPath);
            if (!string.IsNullOrEmpty(runPath) && runPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                SetupAnimationImport(runPath);

            // 自动创建 Animator Controller
            if (!string.IsNullOrEmpty(animPath) || !string.IsNullOrEmpty(walkPath) || !string.IsNullOrEmpty(runPath))
            {
                generator.ButtonText = "创建动画控制器...";
                _host.Repaint();

                CreateAnimatorController(modelDir, baseName, animPath, walkPath, runPath);
            }
        }
        
        /// <summary>
        /// 下载单个文件
        /// </summary>
        private IEnumerator DownloadFile(string url, string savePath)
        {
            string absolutePath = PathUtils.ToAbsoluteAssetPath(savePath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            using (UnityWebRequest uwr = UnityWebRequest.Get(url))
            {
                uwr.downloadHandler = new DownloadHandlerBuffer();
                yield return uwr.SendWebRequest();
                
                float timeout = 120f;
                float timeElapsed = 0f;
                float interval = 0.5f;
                
                while (uwr.result == UnityWebRequest.Result.InProgress && timeElapsed < timeout)
                {
                    double startWait = EditorApplication.timeSinceStartup;
                    while (EditorApplication.timeSinceStartup - startWait < interval)
                    {
                        yield return null;
                    }
                    timeElapsed += interval;
                }
                
                if (uwr.result == UnityWebRequest.Result.Success && uwr.downloadHandler?.data != null)
                {
                    File.WriteAllBytes(absolutePath, uwr.downloadHandler.data);
                    TJLog.Log($"[GenerationPipeline] 文件下载完成: {savePath}");
                }
                else
                {
                    TJLog.LogWarning($"[GenerationPipeline] 文件下载失败: {url}, error: {uwr.error}");
                }
            }
        }
        
        private void SetupAnimationImport(string assetPath) =>
            RiggedModelPostProcessUtils.SetupAnimationImport(assetPath);
        
        /// <summary>
        /// 自动创建 Animator Controller 并配置基本动画状态
        /// </summary>
        /// <param name="modelDir">模型目录</param>
        /// <param name="baseName">模型基础名称</param>
        /// <param name="animPath">指定动画路径（如 Backflip）</param>
        /// <param name="walkPath">行走动画路径</param>
        /// <param name="runPath">奔跑动画路径</param>
        private void CreateAnimatorController(string modelDir, string baseName, string animPath, string walkPath, string runPath)
        {
            try
            {
                // 规范化：若 modelDir 被误拼成 Assets/Assets/...，去掉多余前缀，避免 Unity/文件系统解析出 ...\Assets\Assets\...
                const string assetsPrefix = "Assets/";
                if (modelDir != null && modelDir.StartsWith(assetsPrefix + assetsPrefix, StringComparison.OrdinalIgnoreCase))
                    modelDir = modelDir.Substring(assetsPrefix.Length);
                modelDir = modelDir?.Replace("\\", "/") ?? "";

                // 创建 Animator Controller 路径
                string controllerPath = Path.Combine(modelDir, baseName + "_Controller.controller").Replace("\\", "/");
                string controllerDir = Path.GetDirectoryName(controllerPath).Replace("\\", "/");
                string absoluteControllerDir = PathUtils.ToAbsoluteAssetPath(controllerDir);
                if (!string.IsNullOrEmpty(absoluteControllerDir) && !Directory.Exists(absoluteControllerDir))
                    Directory.CreateDirectory(absoluteControllerDir);

                // 检查是否已存在
                if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) != null)
                {
                    TJLog.Log($"[GenerationPipeline] Animator Controller 已存在: {controllerPath}");
                    return;
                }
                
                // 创建 Animator Controller
                var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                if (controller == null)
                {
                    TJLog.LogWarning($"[GenerationPipeline] 无法创建 Animator Controller: {controllerPath}");
                    return;
                }
                
                // 添加参数
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                controller.AddParameter("Action", AnimatorControllerParameterType.Trigger);  // 用于触发特技动作
                
                // 获取根状态机
                var rootStateMachine = controller.layers[0].stateMachine;
                
                // 提取动画剪辑
                AnimationClip animClip = null;
                AnimationClip walkClip = null;
                AnimationClip runClip = null;
                
                if (!string.IsNullOrEmpty(animPath))
                    animClip = GetAnimationClipFromFbx(animPath);
                if (!string.IsNullOrEmpty(walkPath))
                    walkClip = GetAnimationClipFromFbx(walkPath);
                if (!string.IsNullOrEmpty(runPath))
                    runClip = GetAnimationClipFromFbx(runPath);
                
                // 创建 Idle 状态 — 兜底状态；有行走动画时复用其 clip，避免 Play 时出现 T-pose
                var idleState = rootStateMachine.AddState("Idle");
                if (walkClip != null) idleState.motion = walkClip;

                // 添加 Walk 状态
                AnimatorState walkState = null;
                if (walkClip != null)
                {
                    walkState = rootStateMachine.AddState("Walk");
                    walkState.motion = walkClip;

                    // 添加 Idle -> Walk 过渡
                    var idleToWalk = idleState.AddTransition(walkState);
                    idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                    idleToWalk.duration = 0.2f;

                    // 添加 Walk -> Idle 过渡
                    var walkToIdle = walkState.AddTransition(idleState);
                    walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
                    walkToIdle.duration = 0.2f;

                    TJLog.Log($"[GenerationPipeline] 添加 Walk 状态: {walkClip.name}");
                }

                // 添加 Run 状态
                AnimatorState runState = null;
                if (runClip != null)
                {
                    runState = rootStateMachine.AddState("Run");
                    runState.motion = runClip;

                    // 添加 Walk -> Run 过渡
                    if (walkState != null)
                    {
                        var walkToRun = walkState.AddTransition(runState);
                        walkToRun.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
                        walkToRun.duration = 0.15f;

                        // 添加 Run -> Walk 过渡
                        var runToWalk = runState.AddTransition(walkState);
                        runToWalk.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed");
                        runToWalk.duration = 0.15f;
                    }

                    // 添加 Idle -> Run 过渡
                    var idleToRun = idleState.AddTransition(runState);
                    idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
                    idleToRun.duration = 0.2f;

                    // 添加 Run -> Idle 过渡
                    var runToIdle = runState.AddTransition(idleState);
                    runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
                    runToIdle.duration = 0.2f;

                    TJLog.Log($"[GenerationPipeline] 添加 Run 状态: {runClip.name}");
                }

                // 添加 Action 状态（特技动作，如 Backflip）
                AnimatorState actionState = null;
                if (animClip != null)
                {
                    actionState = rootStateMachine.AddState("Action");
                    actionState.motion = animClip;

                    // 从 AnyState 到 Action（通过 Trigger 触发）
                    var anyToAction = rootStateMachine.AddAnyStateTransition(actionState);
                    anyToAction.AddCondition(AnimatorConditionMode.If, 0, "Action");
                    anyToAction.duration = 0.1f;
                    anyToAction.canTransitionToSelf = false;

                    // Action 完成后回落：优先返回 Walk（衔接循环），否则返回 Idle
                    var actionReturn = actionState.AddTransition(walkState ?? idleState);
                    actionReturn.hasExitTime = true;
                    actionReturn.exitTime = 0.9f;
                    actionReturn.duration = 0.2f;

                    TJLog.Log($"[GenerationPipeline] 添加 Action 状态: {animClip.name}");
                }

                // 默认状态优先级：Action > Walk > Idle
                // Action 优先：点 Play 即看到用户请求的动作；Walk 次之；无 clip 时 Idle（T-pose 属预期）
                if (actionState != null)
                    rootStateMachine.defaultState = actionState;
                else if (walkState != null)
                    rootStateMachine.defaultState = walkState;
                else
                    rootStateMachine.defaultState = idleState;
                
                // 保存控制器
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                TJLog.Log($"[GenerationPipeline] Animator Controller 创建完成: {controllerPath}");
                
                // 尝试将控制器应用到主模型
                ApplyAnimatorControllerToModel(modelDir, baseName, controllerPath);
            }
            catch (Exception e)
            {
                TJLog.LogError($"[GenerationPipeline] 创建 Animator Controller 失败: {e.Message}");
            }
        }

        /// <summary>
        /// 从主 FBX 取第一条非内部剪辑，创建 AnimatorController：Entry 指向的默认状态即该剪辑，
        /// 并添加该状态到自身的过渡（Exit Time），用于循环播放。
        /// </summary>
        private void CreateSingleClipLoopAnimatorController(string modelDir, string baseName, string mainFbxUnityPath)
        {
            try
            {
                const string assetsPrefix = "Assets/";
                if (modelDir != null && modelDir.StartsWith(assetsPrefix + assetsPrefix, StringComparison.OrdinalIgnoreCase))
                    modelDir = modelDir.Substring(assetsPrefix.Length);
                modelDir = modelDir?.Replace("\\", "/") ?? "";

                string controllerPath = Path.Combine(modelDir, baseName + "_Controller.controller").Replace("\\", "/");
                string controllerDir = Path.GetDirectoryName(controllerPath)?.Replace("\\", "/") ?? "";
                string absoluteControllerDir = PathUtils.ToAbsoluteAssetPath(controllerDir);
                if (!string.IsNullOrEmpty(absoluteControllerDir) && !Directory.Exists(absoluteControllerDir))
                    Directory.CreateDirectory(absoluteControllerDir);

                if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) != null)
                {
                    TJLog.Log($"[GenerationPipeline] Animator Controller 已存在，跳过单剪辑循环控制器: {controllerPath}");
                    return;
                }

                AnimationClip clip = GetAnimationClipFromFbx(mainFbxUnityPath);
                if (clip == null)
                {
                    TJLog.LogWarning($"[GenerationPipeline] 无法从主模型提取动画剪辑，跳过单剪辑循环控制器: {mainFbxUnityPath}");
                    return;
                }

                var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                if (controller == null)
                {
                    TJLog.LogWarning($"[GenerationPipeline] 无法创建 Animator Controller: {controllerPath}");
                    return;
                }

                var sm = controller.layers[0].stateMachine;
                AnimatorState previousDefault = sm.defaultState;
                string stateName = string.IsNullOrEmpty(clip.name) ? "Motion" : clip.name;
                var motionState = sm.AddState(stateName);
                motionState.motion = clip;
                motionState.writeDefaultValues = true;
                sm.defaultState = motionState;
                if (previousDefault != null && previousDefault != motionState)
                    sm.RemoveState(previousDefault);

                var selfLoop = motionState.AddTransition(motionState);
                selfLoop.hasExitTime = true;
                selfLoop.exitTime = 1f;
                selfLoop.duration = 0f;
                selfLoop.offset = 0f;
                selfLoop.hasFixedDuration = true;

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                TJLog.Log($"[GenerationPipeline] 单剪辑循环 Animator Controller 已创建: {controllerPath} (clip={clip.name})");

                ApplyAnimatorControllerToModel(modelDir, baseName, controllerPath);
            }
            catch (Exception e)
            {
                TJLog.LogError($"[GenerationPipeline] 创建单剪辑循环 Animator Controller 失败: {e.Message}");
            }
        }

        private void CreateSingleClipLoopAnimatorControllerFromMotionClip(
            string modelDir,
            string targetRigBaseName,
            string motionFbxUnityPath)
        {
            string path = RiggedModelPostProcessUtils.CreateSingleClipLoopAnimatorControllerFromMotionClip(
                modelDir, targetRigBaseName, motionFbxUnityPath);
            if (!string.IsNullOrEmpty(path))
                ApplyAnimatorControllerToModel(modelDir, targetRigBaseName, path);
        }

        private AnimationClip GetAnimationClipFromFbx(string fbxPath) =>
            RiggedModelPostProcessUtils.GetAnimationClipFromFbx(fbxPath);
        
        /// <summary>
        /// 将 Animator Controller 应用到主模型
        /// </summary>
        private void ApplyAnimatorControllerToModel(string modelDir, string baseName, string controllerPath)
        {
            // modelDir 是 Unity 相对路径，如 "Assets/TJGenerators/xxx"
            // 转换为绝对路径来查找文件
            string absoluteDir = PathUtils.ToAbsoluteAssetPath(modelDir);
            
            // 查找主模型文件
            var modelFiles = Directory.GetFiles(absoluteDir, baseName + "*.fbx")
                .Where(f => !f.Contains("_animation") && !f.Contains("_walking") && !f.Contains("_running"))
                .ToList();
            
            if (modelFiles.Count == 0)
            {
                TJLog.LogWarning($"[GenerationPipeline] 未找到主模型文件");
                return;
            }
            
            string mainModelPath = PathUtils.AbsolutePathToAssetsRelative(modelFiles[0]);
            
            // 加载模型
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(mainModelPath);
            if (modelPrefab == null)
            {
                TJLog.LogWarning($"[GenerationPipeline] 无法加载模型: {mainModelPath}");
                return;
            }
            
            // 加载 Animator Controller
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (controller == null)
            {
                TJLog.LogWarning($"[GenerationPipeline] 无法加载 Animator Controller: {controllerPath}");
                return;
            }
            
            // 检查模型是否有 Animator 组件
            var animator = modelPrefab.GetComponent<Animator>();
            if (animator == null)
            {
                // 仅 Prefab 资产支持 EditPrefabContentsScope；FBX 等导入资产无法通过此方式添加组件
                var assetPath = AssetDatabase.GetAssetPath(modelPrefab);
                if (!string.IsNullOrEmpty(assetPath) &&
                    assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    TJLog.Log($"[GenerationPipeline] 模型没有 Animator 组件，尝试添加");
                    using (var editScope = new PrefabUtility.EditPrefabContentsScope(assetPath))
                    {
                        var prefabRoot = editScope.prefabContentsRoot;
                        animator = prefabRoot.GetComponent<Animator>();
                        if (animator == null)
                            animator = prefabRoot.AddComponent<Animator>();
                        animator.runtimeAnimatorController = controller;
                        animator.applyRootMotion = false;
                    }
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    TJLog.Log($"[GenerationPipeline] 跳过非 Prefab 资产的 Animator 添加: {assetPath}");
                }
            }
            else
            {
                animator.runtimeAnimatorController = controller;
                EditorUtility.SetDirty(animator);
                AssetDatabase.SaveAssets();
            }
            
            TJLog.Log($"[GenerationPipeline] Animator Controller 已应用到模型: {mainModelPath}");
        }
        
        /// <summary>
        /// 下载 Tripo rendered_image (webp) 到模型目录，供 FBX 材质使用。
        /// </summary>
        private IEnumerator DownloadRenderedImage(string imageUrl, string unityRelativePath)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", unityRelativePath));
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            using (UnityWebRequest uwr = UnityWebRequest.Get(imageUrl))
            {
                uwr.downloadHandler = new DownloadHandlerBuffer();
                yield return uwr.SendWebRequest();
                
                float timeout = 60f;
                float timeElapsed = 0f;
                while (uwr.result == UnityWebRequest.Result.InProgress && timeElapsed < timeout)
                {
                    timeElapsed += 0.5f;
                    yield return null;
                }
                
                if (uwr.result == UnityWebRequest.Result.Success && uwr.downloadHandler?.data != null)
                {
                    File.WriteAllBytes(fullPath, uwr.downloadHandler.data);
                    TJLog.Log($"[GenerationPipeline] rendered_image 已下载: {unityRelativePath}, size={uwr.downloadHandler.data.Length}");
                    AssetDatabase.Refresh();
                }
                else
                {
                    TJLog.LogWarning($"[GenerationPipeline] rendered_image 下载失败: {imageUrl}, error={uwr.error}");
                }
            }
        }
        
        /// <summary>
        /// 解压ZIP文件并返回模型文件路径
        /// </summary>
        private string ExtractZipAndGetModelPath(byte[] zipData, string originalPath)
        {
            try
            {
                // 使用绝对路径做解压与目录操作，避免当前目录为 Assets 时产生 ...\Assets\Assets\...
                string directory = Path.GetDirectoryName(originalPath).Replace("\\", "/");
                string extractFolderRelative = Path.Combine(directory, Path.GetFileNameWithoutExtension(originalPath)).Replace("\\", "/");
                string extractFolder = PathUtils.ToAbsoluteAssetPath(extractFolderRelative);

                if (Directory.Exists(extractFolder))
                    Directory.Delete(extractFolder, true);
                Directory.CreateDirectory(extractFolder);

                // 写入临时zip文件
                string tempZipPath = Path.Combine(Path.GetTempPath(), $"TJGenerators_temp_{Guid.NewGuid()}.zip");
                File.WriteAllBytes(tempZipPath, zipData);

                // 解压到目标目录（绝对路径）
                ZipFile.ExtractToDirectory(tempZipPath, extractFolder);

                // 删除临时zip文件
                File.Delete(tempZipPath);

                // 查找OBJ文件
                string[] objFiles = Directory.GetFiles(extractFolder, "*.obj", SearchOption.AllDirectories);
                if (objFiles.Length > 0)
                {
                    string objFile = objFiles[0];
                    string objDir = Path.GetDirectoryName(objFile);

                    if (objDir != extractFolder)
                    {
                        foreach (string file in Directory.GetFiles(objDir))
                        {
                            string destFile = Path.Combine(extractFolder, Path.GetFileName(file));
                            if (!File.Exists(destFile))
                            {
                                File.Move(file, destFile);
                            }
                        }
                        objFile = Path.Combine(extractFolder, Path.GetFileName(objFile));
                    }

                    TJLog.Log($"[GenerationPipeline] 解压完成，找到OBJ文件: {objFile}");
                    return PathUtils.AbsolutePathToAssetsRelative(objFile);
                }

                // 如果没有OBJ，查找GLB文件
                string[] glbFiles = Directory.GetFiles(extractFolder, "*.glb", SearchOption.AllDirectories);
                if (glbFiles.Length > 0)
                {
                    TJLog.Log($"[GenerationPipeline] 解压完成，找到GLB文件: {glbFiles[0]}");
                    return PathUtils.AbsolutePathToAssetsRelative(glbFiles[0]);
                }

                // 查找FBX文件
                string[] fbxFiles = Directory.GetFiles(extractFolder, "*.fbx", SearchOption.AllDirectories);
                if (fbxFiles.Length > 0)
                {
                    TJLog.Log($"[GenerationPipeline] 解压完成，找到FBX文件: {fbxFiles[0]}");
                    return PathUtils.AbsolutePathToAssetsRelative(fbxFiles[0]);
                }

                TJLog.LogError("[GenerationPipeline] ZIP文件中未找到支持的模型文件（OBJ/GLB/FBX）");
                return null;
            }
            catch (Exception e)
            {
                TJLog.LogError($"[GenerationPipeline] 解压ZIP文件失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// OBJ模型后处理（设置材质和纹理）
        /// </summary>
        private void ObjModelPostProcessing(string assetPath)
        {
            ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (modelImporter != null)
            {
                string directoryPath = Path.GetDirectoryName(assetPath);
                
                // 配置模型导入设置
                modelImporter.importNormals = ModelImporterNormals.Calculate;  // 自动计算法线
                modelImporter.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;  // 使用面积和角度权重计算
                modelImporter.importBlendShapes = true;  // 导入混合形状
                modelImporter.importTangents = ModelImporterTangents.CalculateMikk;  // 计算切线
                
                // 搜索并重新映射材质
                modelImporter.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnTextureName, ModelImporterMaterialSearch.Local);
                modelImporter.SaveAndReimport();
                AssetDatabase.Refresh();
                
                // 设置法线贴图类型
                foreach (string filePath in Directory.GetFiles(directoryPath))
                {
                    string extension = Path.GetExtension(filePath).ToLower();
                    if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
                    {
                        string fileName = Path.GetFileName(filePath).ToLower();
                        string unityPath = filePath.Replace("\\", "/");
                        
                        // 检查是否是法线贴图
                        if (fileName.Contains("normal") || fileName.Contains("_n.") || fileName.Contains("_norm"))
                        {
                            TextureImporter textureImporter = AssetImporter.GetAtPath(unityPath) as TextureImporter;
                            if (textureImporter != null)
                            {
                                textureImporter.textureType = TextureImporterType.NormalMap;
                                textureImporter.SaveAndReimport();
                            }
                        }
                    }
                }
                
                TJLog.Log($"[GenerationPipeline] OBJ模型导入设置已配置: 法线计算={modelImporter.importNormals}, 切线计算={modelImporter.importTangents}");
                TJLog.Log($"[GenerationPipeline] OBJ模型后处理完成: {assetPath}");
            }
        }

        /// <summary>
        /// GLB模型后处理（gltfast会自动处理导入，这里只做额外设置）
        /// </summary>
        private void GlbModelPostProcessing(string assetPath)
        {
            // GLB文件由gltfast插件自动导入处理
            // 这里可以添加额外的后处理逻辑，如材质调整等
            TJLog.Log($"[GenerationPipeline] GLB模型导入完成: {assetPath}");

            // 确保资产被正确导入
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        // ========== Prefab绑定 ==========
        
        /// <summary>
        /// 将生成的模型绑定到目标 Prefab。
        /// 仅替换名为 "GeneratedModel" 或 "Placeholder" 的子对象，保留其他子对象和根组件。
        /// </summary>
        public void BindModelToPrefab(string modelPath, float scale = 1f, Vector3 rotation = default)
        {
            // 如果没有指定旋转，使用默认值
            if (rotation == default)
            {
                rotation = new Vector3(0f, 0f, 0f);
            }
            
            var targetAsset = _host.GetTargetAsset();
            if (targetAsset == null || !targetAsset.IsValid())
                return;
            
            string prefabPath = targetAsset.GetPath();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                TJLog.LogError($"[GenerationPipeline] 无法加载目标Prefab: {prefabPath}");
                return;
            }
            
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelPrefab == null)
            {
                TJLog.LogError($"[GenerationPipeline] 无法加载生成的模型: {modelPath}");
                return;
            }
            
            // Use prefabPath directly — GetPrefabAssetPathOfNearestInstanceRoot only works on scene
            // instances, not on prefab assets loaded via LoadAssetAtPath (returns "" for assets).
            string prefabAssetPath = prefabPath.Replace("\\", "/");
            using (var editScope = new PrefabUtility.EditPrefabContentsScope(prefabAssetPath))
            {
                var prefabRoot = editScope.prefabContentsRoot;

                // 仅删除 "GeneratedModel" 和 "Placeholder" 子对象，保留其他子对象
                for (int i = prefabRoot.transform.childCount - 1; i >= 0; i--)
                {
                    var child = prefabRoot.transform.GetChild(i).gameObject;
                    if (child.name == "GeneratedModel" || child.name == "Placeholder")
                    {
                        UnityEngine.Object.DestroyImmediate(child);
                    }
                }

                // 实例化模型作为子对象
                var modelInstance = PrefabUtility.InstantiatePrefab(modelPrefab, prefabRoot.transform) as GameObject;
                if (modelInstance != null)
                {
                    modelInstance.name = "GeneratedModel";
                    modelInstance.transform.localPosition = Vector3.zero;
                    modelInstance.transform.localRotation = Quaternion.Euler(rotation);
                    modelInstance.transform.localScale = new Vector3(scale, scale, scale);

                    // 绑定到 Prefab 时，若模型材质缺失或使用错误着色器，自动应用默认材质，避免在场景中显示为紫色。
                    ApplyDefaultMaterialIfMissing(modelInstance);
                }

                // 设置根对象的 Animator：Avatar 取自模型，AnimatorController 从同目录查找
                var animator = prefabRoot.GetComponent<Animator>();
                if (animator == null) animator = prefabRoot.AddComponent<Animator>();

                if (modelInstance != null)
                {
                    var modelAnimator = modelInstance.GetComponent<Animator>();
                    if (modelAnimator != null && modelAnimator.avatar != null)
                        animator.avatar = modelAnimator.avatar;
                }

                if (animator.runtimeAnimatorController == null)
                {
                    string modelDir2     = Path.GetDirectoryName(modelPath);
                    string modelBaseName = Path.GetFileNameWithoutExtension(modelPath);
                    string ctrlPath      = Path.Combine(modelDir2, modelBaseName + "_Controller.controller").Replace("\\", "/");
                    var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);
                    if (ctrl != null) animator.runtimeAnimatorController = ctrl;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            TJLog.Log($"[GenerationPipeline] 模型已绑定到Prefab: {prefabPath}");
            }

            /// <summary>
            /// 获取或创建默认白色材质，用于给缺失或错误着色器的模型补材质。
            /// </summary>
            private static Material GetOrCreateDefaultMaterial()
            {
                const string defaultMatPath = "Assets/TJGenerators/DefaultWhite.mat";

                var existing = AssetDatabase.LoadAssetAtPath<Material>(defaultMatPath);
                if (existing != null)
                {
                    // 确保已经存在的默认材质真的是“白色”，防止之前被误调成紫色等颜色。
                    bool changed = false;
                    if (existing.HasProperty("_BaseColor"))
                    {
                        existing.SetColor("_BaseColor", Color.white);
                        changed = true;
                    }
                    if (existing.HasProperty("_Color"))
                    {
                        existing.SetColor("_Color", Color.white);
                        changed = true;
                    }

                    if (changed)
                    {
                        EditorUtility.SetDirty(existing);
                        AssetDatabase.SaveAssets();
                    }

                    return existing;
                }

                // 确保保存材质的目录存在
                const string folderParent = "Assets";
                const string folderName = "TJGenerators";
                if (!AssetDatabase.IsValidFolder($"{folderParent}/{folderName}"))
                {
                    AssetDatabase.CreateFolder(folderParent, folderName);
                }

                var shader = TJMaterialShaderUtility.ResolveSurfaceLitShader();

                if (shader == null)
                {
                    // 找不到合适的 Shader 时不创建材质，调用方需要处理 null。
                    return null;
                }

                var mat = new Material(shader);

                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", Color.white);
                }
                if (mat.HasProperty("_Color"))
                {
                    mat.SetColor("_Color", Color.white);
                }

                AssetDatabase.CreateAsset(mat, defaultMatPath);
                AssetDatabase.SaveAssets();

                return mat;
            }

            /// <summary>
            /// 将缺失或错误着色器的材质替换为默认材质，作用于实际生成的模型实例。
            /// </summary>
            private static void ApplyDefaultMaterialIfMissing(GameObject root)
            {
                if (root == null) return;

                var defaultMat = GetOrCreateDefaultMaterial();
                if (defaultMat == null)
                {
                    // Shader 未找到时不强行修改材质，避免引入不可预期结果。
                    return;
                }

                var renderers = root.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    if (renderer == null || renderer.sharedMaterials == null) continue;

                    var mats = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var mat = mats[i];
                        bool missingShader = mat == null || mat.shader == null ||
                                             mat.shader.name == "Hidden/InternalErrorShader";
                        if (missingShader)
                        {
                            mats[i] = defaultMat;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        renderer.sharedMaterials = mats;
                    }
                }
            }

        /// <summary>
        /// 替换模型但保留根对象上的 AnimatorController 和脚本组件。
        /// 仅删除名为 "GeneratedModel" 或 "Placeholder" 的子对象，保留其他子对象和根组件。
        /// </summary>
        public void ReplaceModelPreservingController(
            string modelPath, float scale = 1f, Vector3 rotation = default,
            string animationPath = null, string walkingAnimationPath = null, string runningAnimationPath = null)
        {
            if (rotation == default)
            {
                rotation = new Vector3(0f, 0f, 0f);
            }

            var targetAsset = _host.GetTargetAsset();
            if (targetAsset == null || !targetAsset.IsValid())
            {
                TJLog.LogError("[GenerationPipeline] ReplaceModelPreservingController: 目标资产无效");
                return;
            }

            string prefabPath = targetAsset.GetPath();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                TJLog.LogError($"[GenerationPipeline] 无法加载目标Prefab: {prefabPath}");
                return;
            }

            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelPrefab == null)
            {
                TJLog.LogError($"[GenerationPipeline] 无法加载生成的模型: {modelPath}");
                return;
            }

            // Use prefabPath directly — GetPrefabAssetPathOfNearestInstanceRoot only works on scene
            // instances, not on prefab assets loaded via LoadAssetAtPath (returns "" for assets).
            string prefabAssetPath = prefabPath.Replace("\\", "/");
            using (var editScope = new PrefabUtility.EditPrefabContentsScope(prefabAssetPath))
            {
                var prefabRoot = editScope.prefabContentsRoot;

                // 保存 Animator 信息
                var animator = prefabRoot.GetComponent<Animator>();
                RuntimeAnimatorController savedController = null;
                bool savedApplyRootMotion = false;
                if (animator != null)
                {
                    savedController = animator.runtimeAnimatorController;
                    savedApplyRootMotion = animator.applyRootMotion;
                }

                // 仅删除 "GeneratedModel" 和 "Placeholder" 子对象
                for (int i = prefabRoot.transform.childCount - 1; i >= 0; i--)
                {
                    var child = prefabRoot.transform.GetChild(i).gameObject;
                    if (child.name == "GeneratedModel" || child.name == "Placeholder")
                    {
                        UnityEngine.Object.DestroyImmediate(child);
                    }
                }

                // 移除根对象的 MeshFilter/MeshRenderer
                var existingMeshFilter = prefabRoot.GetComponent<MeshFilter>();
                var existingMeshRenderer = prefabRoot.GetComponent<MeshRenderer>();
                if (existingMeshFilter != null)
                    UnityEngine.Object.DestroyImmediate(existingMeshFilter);
                if (existingMeshRenderer != null)
                    UnityEngine.Object.DestroyImmediate(existingMeshRenderer);

                // 实例化新模型
                var modelInstance = PrefabUtility.InstantiatePrefab(modelPrefab, prefabRoot.transform) as GameObject;
                if (modelInstance != null)
                {
                    modelInstance.name = "GeneratedModel";
                    modelInstance.transform.localPosition = Vector3.zero;
                    modelInstance.transform.localRotation = Quaternion.Euler(rotation);
                    modelInstance.transform.localScale = new Vector3(scale, scale, scale);
                }

                // 恢复 Animator 设置
                if (animator == null)
                {
                    animator = prefabRoot.GetComponent<Animator>();
                }
                if (animator == null)
                {
                    animator = prefabRoot.AddComponent<Animator>();
                }

                if (savedController != null)
                {
                    animator.runtimeAnimatorController = savedController;
                    animator.applyRootMotion = savedApplyRootMotion;
                }

                // 用新模型的 Avatar 替换
                if (modelInstance != null)
                {
                    var modelAnimator = modelInstance.GetComponent<Animator>();
                    if (modelAnimator != null && modelAnimator.avatar != null)
                    {
                        animator.avatar = modelAnimator.avatar;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 更新 AnimatorController 中的动画剪辑
            if (!string.IsNullOrEmpty(animationPath) ||
                !string.IsNullOrEmpty(walkingAnimationPath) ||
                !string.IsNullOrEmpty(runningAnimationPath))
            {
                var loadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (loadedPrefab != null)
                {
                    var anim = loadedPrefab.GetComponent<Animator>();
                    if (anim != null && anim.runtimeAnimatorController != null)
                    {
                        string controllerPath = AssetDatabase.GetAssetPath(anim.runtimeAnimatorController);
                        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                        if (controller != null)
                        {
                            UpdateAnimatorControllerClips(controller, animationPath, walkingAnimationPath, runningAnimationPath);
                        }
                    }
                }
            }

            TJLog.Log($"[GenerationPipeline] 模型已替换（保留控制器）: {prefabPath}");
        }

        /// <summary>
        /// 遍历 AnimatorController 的状态机，按状态名匹配更新动画剪辑。
        /// "Action" → animationPath, "Walk"/"Idle" → walkingAnimationPath, "Run" → runningAnimationPath
        /// </summary>
        public void UpdateAnimatorControllerClips(
            AnimatorController controller,
            string animationPath, string walkingAnimationPath, string runningAnimationPath)
        {
            if (controller == null) return;

            AnimationClip actionClip = null;
            AnimationClip walkClip = null;
            AnimationClip runClip = null;

            if (!string.IsNullOrEmpty(animationPath))
                actionClip = GetAnimationClipFromFbx(animationPath);
            if (!string.IsNullOrEmpty(walkingAnimationPath))
                walkClip = GetAnimationClipFromFbx(walkingAnimationPath);
            if (!string.IsNullOrEmpty(runningAnimationPath))
                runClip = GetAnimationClipFromFbx(runningAnimationPath);

            bool changed = false;

            foreach (var layer in controller.layers)
            {
                foreach (var state in layer.stateMachine.states)
                {
                    string stateName = state.state.name;

                    if (stateName == "Action" && actionClip != null)
                    {
                        state.state.motion = actionClip;
                        changed = true;
                        TJLog.Log($"[GenerationPipeline] 更新 Action 状态剪辑: {actionClip.name}");
                    }
                    else if ((stateName == "Walk" || stateName == "Idle") && walkClip != null)
                    {
                        state.state.motion = walkClip;
                        changed = true;
                        TJLog.Log($"[GenerationPipeline] 更新 {stateName} 状态剪辑: {walkClip.name}");
                    }
                    else if (stateName == "Run" && runClip != null)
                    {
                        state.state.motion = runClip;
                        changed = true;
                        TJLog.Log($"[GenerationPipeline] 更新 Run 状态剪辑: {runClip.name}");
                    }
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                TJLog.Log("[GenerationPipeline] AnimatorController 剪辑已更新");
            }
        }

        // ========== 辅助方法 ==========
        
        /// <summary>
        /// 完成生成任务。多图时 savePaths 与 imageUrls 数量一致，会拆成多条历史（一图一格）。
        /// </summary>
        private void CompleteGeneration(ModelGeneratorBase generator, string modelPath, string[] imageUrls = null, List<string> savePaths = null)
        {
            // 移除中断任务记录
            if (!string.IsNullOrEmpty(generator.CurrentBackendTaskId))
            {
                TJGeneratorsTaskRecovery.RemoveInterruptedTask(generator.CurrentBackendTaskId);
            }

            // 图片/音频/序列帧类型：effectivePreviewUrl 已由各 Handle* 方法写入 generator
            // 3D 模型/带动画角色类型：generator.CurrentPreviewUrl 为 null，在此处计算
            string effectivePreviewUrl = generator.CurrentPreviewUrl;

            if (string.IsNullOrEmpty(effectivePreviewUrl))
            {
                // Priority 1: API preview URL（来自轮询，仅模型/角色类型走此分支）
                effectivePreviewUrl = _currentPreviewUrl;

                // Priority 2: 轮询阶段通过 SetPreviewUrl 设置的预览URL（避免被覆盖丢失）
                if (string.IsNullOrEmpty(effectivePreviewUrl) && _activeTaskHandle != null)
                    effectivePreviewUrl = _activeTaskHandle.PreviewUrl;

                // Priority 3: 本地文件 URI（imageUrls 对模型类型为 null，不走 Priority 2）
                if (string.IsNullOrEmpty(effectivePreviewUrl) && !string.IsNullOrEmpty(modelPath))
                {
                    string fullPath = PathUtils.ToAbsoluteAssetPath(modelPath);
                    if (File.Exists(fullPath))
                        effectivePreviewUrl = "file://" + fullPath.Replace('\\', '/');
                }
            }

            // 清理临时状态
            _currentPreviewUrl = null;
            generator.CurrentPreviewUrl = null;

            if (!string.IsNullOrEmpty(generator.CurrentGeneratingTaskId))
            {
                string promptTemplateId = null;
                if (generator is DynamicGenerator dgPrompt)
                    promptTemplateId = dgPrompt.GetSelectedPromptTemplateId();

                if (savePaths != null && savePaths.Count > 1 && imageUrls != null && imageUrls.Length == savePaths.Count)
                {
                    TJGeneratorsHistoryManager.CompletePlaceholderMultiImage(
                        generator.CurrentGeneratingTaskId,
                        savePaths,
                        imageUrls,
                        promptTemplateId
                    );
                }
                else
                {
                    TJGeneratorsHistoryManager.CompletePlaceholder(
                        generator.CurrentGeneratingTaskId,
                        modelPath,
                        effectivePreviewUrl,
                        _currentSourceGlbUrl,
                        null,
                        promptTemplateId
                    );
                }
            }

            // 清理其他临时状态
            _currentSourceGlbUrl = null;
            _currentAudioSavePath = null;
            _currentVideoSavePath = null;

            // 重置状态
            EndGenerationState(generator);

            if (_activeTaskHandle != null)
            {
                _activeTaskHandle.MarkCompleted(modelPath, effectivePreviewUrl);
                _activeTaskHandle = null;
            }
            
            // 刷新UI
            _host.RefreshHistory();
            _host.ShowPreviewModel(modelPath);
            _host.Repaint();
            
            // 刷新用户积分
            _host.RefreshUserInfo();
            
            TJLog.Log($"[GenerationPipeline] 生成完成: {modelPath}");
        }
        
        /// <summary>
        /// 增强错误消息，为特定API错误提供更有用的信息
        /// </summary>
        private string EnhanceErrorMessage(string originalError, ModelGeneratorBase generator)
        {
            if (string.IsNullOrEmpty(originalError)) return null;
            
            // Meshy动画角色生成的特定错误处理
            if (generator != null && generator.GetModelVersion().Contains("animation"))
            {
                // Step 3 rig failed - 骨骼绑定失败
                if (originalError.Contains("step 3 rig failed") || 
                    (originalError.Contains("422") && originalError.Contains("Pose estimation failed")))
                {
                    return "动画绑定失败：您的提示词描述的可能不是一个角色。请确保描述的是有身体结构的角色（如人类、动物、机器人），而不是物品（如食物、车辆、建筑）。";
                }
            }
            
            // 通用API错误处理
            if (originalError.Contains("422"))
            {
                return "请求参数错误：请检查您的输入是否符合要求，特别是提示词内容和格式。";
            }
            
            if (originalError.Contains("429"))
            {
                return "请求频率过高：API调用次数超出限制，请稍后重试。";
            }
            
            if (originalError.Contains("401"))
            {
                return "认证失败：API密钥可能无效或账户配额不足，请检查配置。";
            }
            
            if (originalError.Contains("500") || originalError.Contains("503"))
            {
                return "服务器错误：后端服务暂时不可用，请稍后重试。";
            }
            
            return null; // 返回null使用原始错误消息
        }

        /// <summary>
        /// 处理错误
        /// </summary>
        public void HandleError(ModelGeneratorBase generator, string message, string status = "error")
        {
            TJLog.LogError($"[GenerationPipeline] {message}");
            _host.ShowDialog("错误", message);
            
            if (_activeTaskHandle != null)
            {
                _activeTaskHandle.MarkFailed(status, message);
                _activeTaskHandle = null;
            }
            
            // 移除任务记录
            if (!string.IsNullOrEmpty(generator.CurrentBackendTaskId))
            {
                TJGeneratorsTaskRecovery.RemoveInterruptedTask(generator.CurrentBackendTaskId);
            }
            
            // 移除占位符
            if (!string.IsNullOrEmpty(generator.CurrentGeneratingTaskId))
            {
                TJGeneratorsHistoryManager.RemovePlaceholder(generator.CurrentGeneratingTaskId);
            }
            
            EndGenerationState(generator);
            _host.RefreshHistory();
            _host.Repaint();
        }
        
        /// <summary>
        /// 处理轮询超时（不移除任务记录，允许重连）
        /// </summary>
        private void HandlePollingTimeout(ModelGeneratorBase generator, string message)
        {
            TJLog.LogError($"[GenerationPipeline] {message}");
            _host.ShowDialog("超时", message);
            
            if (_activeTaskHandle != null)
            {
                _activeTaskHandle.MarkFailed("polling_timeout", message);
                _activeTaskHandle = null;
            }
            
            // 更新任务状态但不移除
            if (!string.IsNullOrEmpty(generator.CurrentBackendTaskId))
            {
                TJGeneratorsTaskRecovery.UpdateTaskStatus(generator.CurrentBackendTaskId, "polling_timeout");
            }
            
            EndGenerationState(generator);
            _host.Repaint();
        }
        
        /// <summary>
        /// 更新历史记录中的进度
        /// </summary>
        private void UpdateHistoryProgress(ModelGeneratorBase generator, int progress)
        {
            if (progress > 0 && !string.IsNullOrEmpty(generator.CurrentGeneratingTaskId))
            {
                TJGeneratorsHistoryManager.UpdatePlaceholderProgress(generator.CurrentGeneratingTaskId, progress);
                _host.RefreshHistory();
            }
        }

        /// <summary>
        /// 从URL中提取文件扩展名
        /// </summary>
        private string GetExtensionFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            // 移除查询参数
            int queryIndex = url.IndexOf('?');
            if (queryIndex > 0)
                url = url.Substring(0, queryIndex);

            // 查找常见的3D模型扩展名
            string[] extensions = { ".fbx", ".glb", ".gltf", ".obj", ".zip" };
            foreach (var ext in extensions)
            {
                if (url.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    return ext;
            }

            return null;
        }

        /// <summary>
        /// 对下载 URL 做 MD5 取前 16 个十六进制字符 + 扩展名作为槽内模型文件名；URL 为空时用随机 GUID，扩展名优先 URL、否则沿用分组路径。
        /// </summary>
        private string BuildDownloadModelAssetFileName(string modelUrl, string groupingPath)
        {
            string ext = GetExtensionFromUrl(modelUrl);
            if (string.IsNullOrEmpty(ext))
                ext = Path.GetExtension(groupingPath);
            if (string.IsNullOrEmpty(ext))
                ext = ".fbx";

            string source = string.IsNullOrEmpty(modelUrl) ? Guid.NewGuid().ToString("N") : modelUrl;
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(source));
                var sb = new StringBuilder(16);
                for (int i = 0; i < 8; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString() + ext.ToLowerInvariant();
            }
        }

        /// <summary>
        /// 根据图片数据或URL确定图片扩展名
        /// </summary>
        /// <param name="imageData">图片字节数据</param>
        /// <param name="url">图片URL（备用）</param>
        /// <returns>图片扩展名（如 .png, .jpg, .webp）</returns>
        private string GetImageExtensionFromData(byte[] imageData, string url)
        {
            // 1. 首先检查文件头（magic bytes）来确定格式
            if (imageData != null && imageData.Length >= 8)
            {
                // PNG: 89 50 4E 47 0D 0A 1A 0A
                if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
                {
                    return ".png";
                }

                // JPEG: FF D8 FF
                if (imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
                {
                    return ".jpg";
                }

                // GIF: 47 49 46 38
                if (imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x38)
                {
                    return ".gif";
                }

                // WebP: 52 49 46 46 ... 57 45 42 50
                if (imageData[0] == 0x52 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x46 &&
                    imageData.Length >= 12 && imageData[8] == 0x57 && imageData[9] == 0x45 && imageData[10] == 0x42 && imageData[11] == 0x50)
                {
                    return ".webp";
                }
            }

            // 2. 尝试从 URL 中提取扩展名
            if (!string.IsNullOrEmpty(url))
            {
                // 移除查询参数
                int queryIndex = url.IndexOf('?');
                if (queryIndex > 0)
                    url = url.Substring(0, queryIndex);

                string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" };
                foreach (var ext in imageExtensions)
                {
                    if (url.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    {
                        // 统一 .jpeg 为 .jpg
                        return ext == ".jpeg" ? ".jpg" : ext;
                    }
                }
            }

            // 3. 默认返回 .png
            return ".png";
        }

        /// <summary>
        /// 获取模型保存路径
        /// </summary>
        private string GetModelSavePath(string fileName)
        {
            if (!AssetDatabase.IsValidFolder("Assets/TJGenerators"))
            {
                AssetDatabase.CreateFolder("Assets", "TJGenerators");
            }
            
            var targetAsset = _host.GetTargetAsset();
            if (targetAsset != null && targetAsset.IsValid())
            {
                string prefabName = Path.GetFileNameWithoutExtension(targetAsset.GetPath());
                string ext = Path.GetExtension(fileName);
                return Path.Combine(SAVE_DIRECTORY, $"{prefabName}{ext}");
            }
            return Path.Combine(SAVE_DIRECTORY, fileName);
        }

        /// <summary>
        /// 在 <c>Assets/TJGenerators/History/{分组名}/01/</c> 下分配序号子目录。
        /// 分组目录名来自 <paramref name="groupingPath"/> 的文件基名（如 Prefab「new mesh 1」）；槽内模型文件名优先用 <paramref name="diskFileNameFromUrl"/>（通常为 URL 的 MD5 短名 + 扩展名）。
        /// </summary>
        private string GetUniqueFilePath(string groupingPath, string diskFileNameFromUrl = null)
        {
            string fileExtension = Path.GetExtension(groupingPath);
            string fileName = !string.IsNullOrEmpty(diskFileNameFromUrl)
                ? diskFileNameFromUrl
                : Path.GetFileName(groupingPath);
            if (string.IsNullOrEmpty(fileName))
                fileName = "Model" + (string.IsNullOrEmpty(fileExtension) ? ".fbx" : fileExtension);

            string baseLabel = Path.GetFileNameWithoutExtension(groupingPath);
            if (string.IsNullOrEmpty(baseLabel))
                baseLabel = "Model";
            string groupFolderName = PathUtils.SanitizeAssetFolderName(baseLabel);

            if (!AssetDatabase.IsValidFolder("Assets/TJGenerators"))
                AssetDatabase.CreateFolder("Assets", "TJGenerators");
            if (!AssetDatabase.IsValidFolder("Assets/TJGenerators/History"))
                AssetDatabase.CreateFolder("Assets/TJGenerators", "History");

            const string historyRoot = "Assets/TJGenerators/History";
            string groupPath = $"{historyRoot}/{groupFolderName}".Replace("\\", "/");

            if (!AssetDatabase.IsValidFolder(groupPath))
            {
                AssetDatabase.CreateFolder(historyRoot, groupFolderName);
            }

            if (!AssetDatabase.IsValidFolder(groupPath))
            {
                TJLog.LogError($"[GenerationPipeline] 无法创建 History 分组目录: {groupPath}");
                groupFolderName = $"Model_{Guid.NewGuid():N}";
                groupPath = $"{historyRoot}/{groupFolderName}".Replace("\\", "/");
                AssetDatabase.CreateFolder(historyRoot, groupFolderName);
            }

            bool SlotFolderIsUnused(string slot)
            {
                string folderAssetPath = $"{groupPath}/{slot}";
                if (AssetDatabase.IsValidFolder(folderAssetPath))
                    return false;
                string absSlot = PathUtils.ToAbsoluteAssetPath(folderAssetPath);
                return absSlot == null || !Directory.Exists(absSlot);
            }

            for (int index = 1; index < 10000; index++)
            {
                string slot = index.ToString("D2");
                if (!SlotFolderIsUnused(slot))
                    continue;

                AssetDatabase.CreateFolder(groupPath, slot);
                if (!AssetDatabase.IsValidFolder($"{groupPath}/{slot}"))
                    continue;

                string candidate = $"{groupPath}/{slot}/{fileName}".Replace("\\", "/");
                return AssetDatabase.GenerateUniqueAssetPath(candidate);
            }

            string fallbackSlot = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
            AssetDatabase.CreateFolder(groupPath, fallbackSlot);
            string fallbackPath = $"{groupPath}/{fallbackSlot}/{fileName}".Replace("\\", "/");
            TJLog.LogWarning($"[GenerationPipeline] History 序号子目录已满，改用时间戳: {fallbackPath}");
            return AssetDatabase.GenerateUniqueAssetPath(fallbackPath);
        }

        private static string RendererHierarchyPath(Transform modelRoot, Renderer renderer) =>
            RiggedModelPostProcessUtils.RendererHierarchyPath(modelRoot, renderer);

        private int ApplyMaterialsFromSourceModelToRiggedModel(string sourceAssetPath, string riggedAssetPath) =>
            RiggedModelPostProcessUtils.ApplyMaterialsFromSourceModelToRiggedModel(sourceAssetPath, riggedAssetPath);

        /// <summary>
        /// 将 Tripo rendered_image 等贴图应用到已导入模型资源下所有 Renderer 材质（主贴图 / URP _BaseMap / _MainTex）。
        /// 用于主 FBX 后处理；绑骨后仅在无法从源模型复用材质时作为回退。
        /// </summary>
        private void ApplyRenderedTextureToImportedModel(string assetPath, string renderedTexturePath)
        {
            if (string.IsNullOrEmpty(renderedTexturePath))
                return;

            Texture2D renderedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(renderedTexturePath);
            if (renderedTex == null)
            {
                TJLog.LogWarning($"[GenerationPipeline] 无法加载 rendered 贴图: {renderedTexturePath}");
                return;
            }

            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (modelPrefab == null)
                return;

            var renderers = modelPrefab.GetComponentsInChildren<Renderer>();
            int appliedCount = 0;
            foreach (var rend in renderers)
            {
                if (rend.sharedMaterials == null) continue;
                foreach (var mat in rend.sharedMaterials)
                {
                    if (mat == null) continue;
                    mat.mainTexture = renderedTex;
                    if (mat.HasProperty("_BaseMap"))
                        mat.SetTexture("_BaseMap", renderedTex);
                    if (mat.HasProperty("_MainTex"))
                        mat.SetTexture("_MainTex", renderedTex);
                    EditorUtility.SetDirty(mat);
                    appliedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            TJLog.Log($"[GenerationPipeline] 已将 rendered_image 贴图应用到 {appliedCount} 个材质: {renderedTexturePath} -> {assetPath}");
        }
        
        /// <summary>
        /// 模型后处理（提取纹理、设置法线贴图等）。renderedTexturePath 为 Tripo rendered_image (webp) 的 Unity 相对路径时，会将其设为所有材质的主贴图。
        /// </summary>
        /// <param name="assetPath">模型路径</param>
        /// <param name="renderedTexturePath">渲染贴图路径</param>
        /// <param name="hasSeparateAnimations">是否有单独的动画文件（如果有，主模型不导入动画）</param>
        private void ModelPostProcessing(string assetPath, string renderedTexturePath = null, bool hasSeparateAnimations = false)
        {
            ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (modelImporter != null)
            {
                string parentDir = Path.GetDirectoryName(assetPath)?.Replace("\\", "/") ?? "";
                string safeBase = PathUtils.SanitizeAssetFolderName(Path.GetFileNameWithoutExtension(assetPath));
                // 每个 FBX 单独子目录，避免 Tripo 等固定贴图名（如 tripo_model_basecolor）在同一父目录下互相覆盖。
                string extractDirRelative = string.IsNullOrEmpty(parentDir)
                    ? $"{safeBase}.fbm"
                    : $"{parentDir}/{safeBase}.fbm";

                string absExtract = PathUtils.ToAbsoluteAssetPath(extractDirRelative);
                if (!string.IsNullOrEmpty(absExtract))
                    Directory.CreateDirectory(absExtract);

                modelImporter.ExtractTextures(extractDirRelative);

                // 在 Refresh 之前，先单独导入法线贴图并设置类型，避免 NormalMap settings 弹窗
                if (!string.IsNullOrEmpty(absExtract) && Directory.Exists(absExtract))
                {
                    foreach (string filePath in Directory.GetFiles(absExtract))
                    {
                        string ext = Path.GetExtension(filePath).ToLowerInvariant();
                        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                            continue;

                        string fileName = Path.GetFileName(filePath);
                        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                        bool treatAsNormalMap = fileName.StartsWith("Normal", StringComparison.OrdinalIgnoreCase)
                            || nameWithoutExt.EndsWith("_normal", StringComparison.OrdinalIgnoreCase);
                        if (!treatAsNormalMap)
                            continue;

                        string unityPath = extractDirRelative.TrimEnd('/') + "/" + fileName;
                        AssetDatabase.ImportAsset(unityPath, ImportAssetOptions.ForceUpdate);
                        TextureImporter ti = AssetImporter.GetAtPath(unityPath) as TextureImporter;
                        if (ti != null)
                        {
                            ti.textureType = TextureImporterType.NormalMap;
                            ti.SaveAndReimport();
                        }
                    }
                }

                AssetDatabase.Refresh();

                // 如果有单独的动画文件，主模型设置为 Humanoid 但不导入动画
                if (hasSeparateAnimations)
                {
                    modelImporter.animationType = ModelImporterAnimationType.Human;
                    modelImporter.importAnimation = false;
                    TJLog.Log($"[GenerationPipeline] 主模型设置为 Humanoid，禁用动画导入（动画在单独文件中）");
                }

                modelImporter.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnTextureName, ModelImporterMaterialSearch.Local);
                modelImporter.SaveAndReimport();
                AssetDatabase.Refresh();

                ApplyRenderedTextureToImportedModel(assetPath, renderedTexturePath);

            }
        }
        
        /// <summary>
        /// 等待指定秒数（Editor环境兼容）
        /// </summary>
        private IEnumerator WaitSeconds(float seconds)
        {
            double startTime = EditorApplication.timeSinceStartup;
            while (EditorApplication.timeSinceStartup - startTime < seconds)
            {
                yield return null;
            }
        }
    }
}
#endif
