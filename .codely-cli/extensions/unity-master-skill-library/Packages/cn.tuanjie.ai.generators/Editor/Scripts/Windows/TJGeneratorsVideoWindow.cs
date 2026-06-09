#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Unity.EditorCoroutines.Editor;
using TJGenerators.Config;
using TJGenerators.Generators;
using TJGenerators.Pipeline;
using TJGenerators.UI;
using TJGenerators.Utils;

namespace TJGenerators
{
    /// <summary>
    /// TJGenerators 视频生成窗口 - 文生视频 / 图生视频
    /// </summary>
    public class TJGeneratorsVideoWindow : GenerationWindowBase, IGenerationPipelineHost
    {
        // ========== 基类抽象属性实现 ==========
        protected override ConfigType WindowConfigType => ConfigType.Video;
        protected override string LogTag => "[TJGeneratorsVideo]";

        // ========== 窗口特定字段 ==========
        [SerializeField]
        private string textPrompt = "";

        [SerializeField]
        private TJGeneratorsAssetReference targetVideoAsset;

        private const int MaxReferenceImages = 1;
        private readonly List<string> referenceImagePaths = new List<string>();
        private readonly List<Texture2D> referenceUploadedImages = new List<Texture2D>();

        private Texture2D videoPreviewTexture;
        private double _lastProgressRepaintTime;

        private static readonly Dictionary<string, TJGeneratorsVideoWindow> s_videoOpenWindows =
            new Dictionary<string, TJGeneratorsVideoWindow>();

        // ========== 静态入口 ==========

        public static void ShowWindow()
        {
            var rect = GetDefaultMainWindowRect();
            var window = GetWindowWithRect<TJGeneratorsVideoWindow>(
                rect,
                utility: false,
                title: "TJGenerators 视频生成",
                focus: true
            );
            window.titleContent = new GUIContent("TJGenerators 视频生成");
        }

        /// <summary>
        /// 从指定资产路径打开视频生成窗口。
        /// </summary>
        public static void OpenForAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                ShowWindow();
                return;
            }

            GenerationWindowBase.OpenForAsset(
                assetPath,
                s_videoOpenWindows,
                "[TJGeneratorsVideo]",
                "TJGenerators 视频 - {0}",
                () =>
                {
                    var window = CreateInstance<TJGeneratorsVideoWindow>();
                    SetDefaultWindowSize(window);
                    return window;
                },
                (w, r) => w.targetVideoAsset = r,
                ShowWindow
            );
        }

        // ========== 生命周期 ==========

        private void OnEnable()
        {
            wantsMouseMove = true;
            InitializeGeneratorsFromConfig(ConfigType.Video);

            EnsureTargetVideo();

            EditorApplication.delayCall += () =>
            {
                if (this == null)
                    return;
                generationHistory = TJGeneratorsHistoryManager.LoadHistoryForAsset(
                    GetCurrentVideoAssetGuid()
                );
                selectedHistoryIndex = generationHistory.Count > 0 ? 0 : -1;
                Repaint();
            };

            EditorCoroutineUtility.StartCoroutineOwnerless(
                UserInfoHelper.GetUserInfoCoroutine(
                    ConfigManager.GetUserInfoUrl(),
                    OnUserInfoLoaded
                )
            );

            CheckAndRecoverInterruptedTasks();
        }

        private void OnDisable()
        {
            wantsMouseMove = false;
            if (targetVideoAsset != null && !string.IsNullOrEmpty(targetVideoAsset.guid))
            {
                s_videoOpenWindows.Remove(targetVideoAsset.guid);
            }

            videoPreviewTexture = null;
            ClearPreviewCaches();

            foreach (var tex in referenceUploadedImages)
            {
                if (tex != null)
                    DestroyImmediate(tex);
            }
            referenceUploadedImages.Clear();
            referenceImagePaths.Clear();
        }

        // ========== 任务恢复 ==========

        protected override string GetCurrentAssetGuid() => GetCurrentVideoAssetGuid();

        protected override void SetHistory(List<TJGeneratorsGenerationHistoryItem> history) =>
            generationHistory = history;

        protected override void OnGeneratorRestoredFromTask(ModelGeneratorBase generator)
        {
            base.OnGeneratorRestoredFromTask(generator);
            isGenerating = true;
            generationStatus = "恢复中...";
            Repaint();
        }

        // ========== UI ==========

        private void OnGUI()
        {
            if (Event.current.type == EventType.MouseMove)
                Repaint();
            var splitLayout = UIComponents.CalculateFixedSplitLayout(
                position.width,
                CommonStyles.MainWindowMinSize.y,
                CommonStyles.LeftPanelFixedWidth,
                CommonStyles.MinHistoryPanelWidth,
                CommonStyles.OuterMargin);
            minSize = new Vector2(splitLayout.WindowMinWidth, splitLayout.WindowMinHeight);
            isVerticalLayout = false;
            currentHistoryPanelWidth = splitLayout.RightPanelWidth;
            _effectiveLeftPanelWidth = CommonStyles.LeftComponentWidth;

            if (_generators == null || _generators.Count == 0)
            {
                EditorGUI.DrawRect(
                    new Rect(0, 0, position.width, position.height),
                    CommonStyles.WindowBackgroundColor
                );
                EditorGUILayout.HelpBox("未找到可用的视频生成器，请检查 GeneratorConfig.json 中的 videoGenerators", MessageType.Error);
                return;
            }

            UIComponents.DrawAdaptiveLayoutBackground(
                new Rect(0, 0, position.width, position.height),
                false,
                splitLayout.LeftPanelWidth,
                position.height
            );

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(
                GUILayout.Width(splitLayout.LeftPanelWidth),
                GUILayout.MinWidth(splitLayout.LeftPanelWidth),
                GUILayout.MaxWidth(splitLayout.LeftPanelWidth));
            scrollPosition = GUILayout.BeginScrollView(
                scrollPosition,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.Height(position.height - LeftBottomStatusBarHeight),
                GUILayout.Width(splitLayout.LeftPanelWidth),
                GUILayout.MaxWidth(splitLayout.LeftPanelWidth));
            GUILayout.Space(CommonStyles.LeftContentPadding);
            GUILayout.BeginHorizontal();
            GUILayout.Space(CommonStyles.LeftContentPadding);
            GUILayout.BeginVertical(
                GUILayout.Width(CommonStyles.LeftComponentWidth),
                GUILayout.MinWidth(CommonStyles.LeftComponentWidth),
                GUILayout.MaxWidth(CommonStyles.LeftComponentWidth));

                UIComponents.DrawTargetHeaderComposite(
                    "目标视频",
                    DrawHeaderTargetContentRect,
                    SelectTargetVideoAsset
                );
                GUILayout.Space(CommonStyles.Space2);

                UIComponents.DrawModelSelector(
                    currentSelectedModel?.Name ?? _currentGenerator?.DisplayName ?? "未选择",
                    currentSelectedModel,
                    OnModelSelected,
                    ConfigType.Video
                );

                GUILayout.Space(CommonStyles.Space2);
                UIComponents.DrawGapLine();
                GUILayout.Space(CommonStyles.Space2);

                DrawInputSection();

                GUILayout.Space(CommonStyles.Space2);
                UIComponents.DrawGapLine();
                GUILayout.Space(CommonStyles.Space2);

                DrawConfigurationSection();

                GUILayout.Space(CommonStyles.Space2);

                DrawGenerationSection();

                GUILayout.Space(CommonStyles.Space2);
            GUILayout.EndVertical();
            GUILayout.Space(CommonStyles.LeftContentPadding);
            GUILayout.EndHorizontal();
            GUILayout.Space(CommonStyles.LeftContentPadding);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.Space(splitLayout.GapWidth);

            DrawHistoryPanel(currentHistoryPanelWidth);
            GUILayout.EndHorizontal();
            DrawLeftBottomStatusBar(splitLayout.LeftPanelWidth);
        }

        private void DrawHeaderTargetContent()
        {
            if (targetVideoAsset != null && targetVideoAsset.IsValid())
            {
                string videoName = Path.GetFileNameWithoutExtension(targetVideoAsset.GetPath());
                if (GUILayout.Button(videoName, CommonStyles.LinkStyle))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                        targetVideoAsset.GetPath()
                    );
                    if (obj != null)
                    {
                        EditorGUIUtility.PingObject(obj);
                        Selection.activeObject = obj;
                    }
                }

                UIComponents.AddLinkCursorToLastRect();
            }
            else
            {
                GUILayout.Label("未绑定（生成时自动创建）", CommonStyles.ContentStyle);
            }
        }

        private void DrawHeaderTargetContentRect(Rect rect)
        {
            if (targetVideoAsset != null && targetVideoAsset.IsValid())
            {
                string videoName = Path.GetFileNameWithoutExtension(targetVideoAsset.GetPath());
                if (GUI.Button(rect, videoName, CommonStyles.TargetPrefabNameStyle))
                    SelectTargetVideoAsset();
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            }
            else
            {
                GUI.Label(rect, "未绑定（生成时自动创建）", CommonStyles.ContentStyle);
            }
        }

        private void SelectTargetVideoAsset()
        {
            if (targetVideoAsset == null || !targetVideoAsset.IsValid())
                return;
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetVideoAsset.GetPath());
            if (obj != null)
            {
                EditorGUIUtility.PingObject(obj);
                Selection.activeObject = obj;
            }
        }

        private void OnModelSelected(AIModelInfo model) => OnModelSelectedBase(model);

        private void DrawInputSection()
        {
            string singlePath = referenceImagePaths.Count > 0 ? referenceImagePaths[0] : "";
            Texture2D singleTex = referenceUploadedImages.Count > 0 ? referenceUploadedImages[0] : null;

            UIComponents.DrawSectionTitle("参考图片（可选）", uppercase: false);
            GUILayout.Space(CommonStyles.Space1);
            UIComponents.DrawUploadImageLargeComponent(
                ref singlePath,
                ref singleTex,
                null,
                Repaint
            );

            referenceImagePaths.Clear();
            referenceUploadedImages.Clear();
            if (!string.IsNullOrEmpty(singlePath))
            {
                referenceImagePaths.Add(singlePath);
                if (singleTex != null)
                    referenceUploadedImages.Add(singleTex);
            }

            GUILayout.Space(CommonStyles.Space2);
            UIComponents.DrawSectionTitle("文本提示词", uppercase: false);
            GUILayout.Space(CommonStyles.Space1);
            textPrompt = UIComponents.DrawPromptInputBox(
                textPrompt,
                "描述你想要生成的视频内容...",
                "video_prompt_input"
            );
        }

        private void DrawReferenceImageSection()
        {
            GUILayout.BeginHorizontal();
            UIComponents.DrawSectionTitle("参考图片（可选）", uppercase: false);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            float thumbSize = 80f;
            float clearSize = 20f;
            float availableWidth = Mathf.Max(
                300f,
                position.width - 60f
            );

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            UIComponents.DrawReferenceImagesGrid(
                referenceImagePaths,
                referenceUploadedImages,
                MaxReferenceImages,
                availableWidth,
                thumbSize,
                clearSize,
                "+ 添加图片",
                "选择参考图片",
                "jpg,jpeg,png",
                $"最多可选择 {MaxReferenceImages} 张参考图片",
                Repaint
            );
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawConfigurationSection()
        {
            var provider = _currentGenerator as IGeneratorParameterProvider;

            // Filter out mode parameter (handled by input mode)
            var allParams = GetCurrentGeneratorParameters();
            showAdvancedSettings = UIComponents.DrawAdvancedSettingsFoldout(
                showAdvancedSettings,
                provider,
                allParams
            );
            SyncGenerationCostWithCurrentGeneratorState();
        }

        private void DrawGenerationSection()
        {
            bool canGenerate = !string.IsNullOrWhiteSpace(textPrompt);
            UIComponents.DrawGenerationSection(
                isGenerating,
                generationProgress,
                generationStatus,
                canGenerate,
                StartGeneration,
                null,
                () =>
                {
                    double t = EditorApplication.timeSinceStartup;
                    if (t - _lastProgressRepaintTime > 0.1)
                    {
                        _lastProgressRepaintTime = t;
                        Repaint();
                    }
                },
                currentGenerationCost);
        }

        private void DrawHistoryPanel(float panelWidth)
        {
            float historyPanelHeight = isVerticalLayout ? position.height * 0.45f : position.height;

            UIComponents.BeginHistoryPanel(panelWidth, historyPanelHeight, isVerticalLayout);

            // Preview area
            Texture2D historyPreviewTex = null;
            if (selectedHistoryIndex >= 0 && selectedHistoryIndex < generationHistory.Count)
            {
                var selectedItem = generationHistory[selectedHistoryIndex];
                if (!selectedItem.isGenerating && !string.IsNullOrEmpty(selectedItem.modelPath))
                {
                    historyPreviewTex = GetPreviewTextureForHistoryItem(selectedItem);
                }
            }

            if (historyPreviewTex == null)
                historyPreviewTex = videoPreviewTexture;

            float previewBlockHeight = UIComponents.DrawHistoryTexturePreview(
                historyPreviewTex,
                isVerticalLayout,
                panelWidth,
                historyPanelHeight
            );

            float scrollHeight = Mathf.Max(0f, historyPanelHeight - previewBlockHeight - 100f);
            historyScrollPosition = GUILayout.BeginScrollView(
                historyScrollPosition,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.Height(scrollHeight));

            if (generationHistory.Count == 0)
                UIComponents.DrawHistoryEmptyState();
            else
                DrawHistoryGrid();

            GUILayout.EndScrollView();

            DrawHistoryActions();

            UIComponents.EndHistoryPanel();
        }

        private void DrawHistoryGrid()
        {
            for (int i = 0; i < generationHistory.Count; i++)
            {
                if (i > 0)
                    GUILayout.Space(6f);

                var item = generationHistory[i];
                bool isSelected = selectedHistoryIndex == i;

                GUILayout.BeginHorizontal(GUIStyle.none, GUILayout.Height(50));

                Rect rowRect = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));

                if (Event.current.type == EventType.Repaint)
                {
                    Color bgColor = isSelected
                        ? new Color(0.2f, 0.3f, 0.5f, 1f)
                        : new Color(0.15f, 0.15f, 0.15f, 1f);
                    EditorGUI.DrawRect(rowRect, bgColor);
                }

                // Video icon + info
                Rect iconRect = new Rect(rowRect.x + 8, rowRect.y + 10, 30, 30);
                GUI.Label(iconRect, EditorGUIUtility.IconContent("d_MovieIcon"));

                Rect labelRect = new Rect(rowRect.x + 44, rowRect.y + 5, rowRect.width - 54, 20);
                string displayName = item.isGenerating ? "生成中..." : item.GetDisplayName();
                GUI.Label(labelRect, displayName, CommonStyles.HistoryLabelStyle);

                Rect subLabelRect = new Rect(rowRect.x + 44, rowRect.y + 25, rowRect.width - 54, 18);
                GUI.Label(subLabelRect, GetModelDisplayLabelFromIndex(item.modelVersion), CommonStyles.SmallGreyCenterLabelStyle);

                if (item.isGenerating)
                {
                    Repaint();
                }
                else if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    selectedHistoryIndex = i;
                    Event.current.Use();
                    Repaint();
                }
                else if (Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
                {
                    ShowHistoryContextMenu(i);
                    Event.current.Use();
                }

                GUILayout.EndHorizontal();
            }
        }

        private Texture2D GetPreviewTextureForHistoryItem(TJGeneratorsGenerationHistoryItem item)
        {
            if (item == null || item.isGenerating)
                return null;

            // Try to get preview from URL cache first
            if (!string.IsNullOrEmpty(item.previewImageUrl)
                && urlPreviewCache.TryGetValue(item.previewImageUrl, out var urlTex)
                && urlTex != null)
            {
                return urlTex;
            }

            // Try to load from the video path (Unity will generate a thumbnail for video assets)
            if (!string.IsNullOrEmpty(item.modelPath))
            {
                if (historyPreviewCache.TryGetValue(item.modelPath, out var cached) && cached != null)
                    return cached;

                var assetTex = AssetDatabase.LoadAssetAtPath<Texture2D>(item.modelPath);
                if (assetTex != null)
                {
                    historyPreviewCache[item.modelPath] = assetTex;
                    return assetTex;
                }
            }

            return null;
        }

        private void ShowHistoryContextMenu(int index)
        {
            if (index < 0 || index >= generationHistory.Count)
                return;
            var item = generationHistory[index];
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("在项目中显示"), false, () => ShowHistoryInProject(index));
            menu.AddSeparator("");

            if (!string.IsNullOrEmpty(item.modelPath))
                menu.AddItem(
                    new GUIContent("在资源管理器中显示"),
                    false,
                    () => EditorUtility.RevealInFinder(item.modelPath)
                );

            menu.AddSeparator("");

            menu.AddItem(
                new GUIContent("从历史记录中移除"),
                false,
                () =>
                {
                    TJGeneratorsHistoryManager.RemoveFromHistory(item.modelPath);
                    generationHistory = TJGeneratorsHistoryManager.LoadHistoryForAsset(
                        GetCurrentVideoAssetGuid()
                    );
                    if (generationHistory.Count == 0)
                        selectedHistoryIndex = -1;
                    else if (selectedHistoryIndex >= generationHistory.Count)
                        selectedHistoryIndex = Mathf.Max(0, generationHistory.Count - 1);
                    Repaint();
                }
            );

            menu.ShowAsContext();
        }

        private void ShowHistoryInProject(int index)
        {
            if (index < 0 || index >= generationHistory.Count)
                return;

            var item = generationHistory[index];
            if (string.IsNullOrEmpty(item.modelPath))
                return;

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(item.modelPath);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
        }

        private void DrawHistoryActions()
        {
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();

            bool canApply = selectedHistoryIndex >= 0 && selectedHistoryIndex < generationHistory.Count
                && targetVideoAsset != null && targetVideoAsset.IsValid();

            GUI.enabled = canApply;
            if (GUILayout.Button("应用到当前视频", GUILayout.Height(25)))
                ApplyHistoryToVideo(selectedHistoryIndex);

            GUI.enabled =
                selectedHistoryIndex >= 0 && selectedHistoryIndex < generationHistory.Count;
            if (GUILayout.Button("在项目中显示", GUILayout.Height(25)))
                ShowHistoryInProject(selectedHistoryIndex);

            GUI.enabled = true;

            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }

        private void ApplyHistoryToVideo(int index)
        {
            if (index < 0 || index >= generationHistory.Count)
                return;
            var item = generationHistory[index];

            if (item.isGenerating)
            {
                Debug.LogWarning($"{LogTag} 请等待该条生成完成后再应用。");
                return;
            }

            if (string.IsNullOrEmpty(item.modelPath) || !File.Exists(PathUtils.ToAbsoluteAssetPath(item.modelPath)))
            {
                ErrorDialogUtils.ShowErrorDialog("错误", "视频文件不存在，可能已被删除。", LogTag);
                if (!string.IsNullOrEmpty(item.modelPath))
                    TJGeneratorsHistoryManager.RemoveFromHistory(item.modelPath);
                generationHistory = TJGeneratorsHistoryManager.LoadHistoryForAsset(GetCurrentVideoAssetGuid());
                Repaint();
                return;
            }

            if (targetVideoAsset == null || !targetVideoAsset.IsValid())
            {
                Debug.LogWarning($"{LogTag} 请先创建或选择目标视频资产。");
                return;
            }

            string targetPath = targetVideoAsset.GetPath();
            if (!EditorUtility.DisplayDialog(
                "确认替换",
                $"确定要将选中的历史应用到 {Path.GetFileName(targetPath)} 吗？",
                "确定", "取消"))
            {
                return;
            }

            try
            {
                string srcAbsolute = PathUtils.ToAbsoluteAssetPath(item.modelPath);
                string dstAbsolute = PathUtils.ToAbsoluteAssetPath(targetPath);
                string targetExt = Path.GetExtension(targetPath).ToLowerInvariant();
                string sourceExt = Path.GetExtension(item.modelPath).ToLowerInvariant();

                // If extensions differ, update target path
                if (!string.Equals(targetExt, sourceExt, StringComparison.OrdinalIgnoreCase))
                {
                    targetPath = Path.ChangeExtension(targetPath, sourceExt);
                    dstAbsolute = PathUtils.ToAbsoluteAssetPath(targetPath);

                    // Delete old file
                    string oldPath = targetVideoAsset.GetPath();
                    string oldAbsolute = PathUtils.ToAbsoluteAssetPath(oldPath);
                    if (File.Exists(oldAbsolute) && !string.Equals(oldAbsolute, dstAbsolute, StringComparison.OrdinalIgnoreCase))
                    {
                        AssetDatabase.DeleteAsset(oldPath);
                    }

                    targetVideoAsset = TJGeneratorsAssetReference.FromPath(targetPath);
                    titleContent = new GUIContent($"TJGenerators 视频 - {Path.GetFileNameWithoutExtension(targetPath)}");
                    string newGuid = targetVideoAsset.guid;
                    if (!string.IsNullOrEmpty(newGuid))
                        s_videoOpenWindows[newGuid] = this;
                }

                string targetDir = Path.GetDirectoryName(dstAbsolute);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                File.Copy(srcAbsolute, dstAbsolute, true);
                AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);

                TJGeneratorsGenerationLabel.EnableLabel(targetVideoAsset);
                TJLog.Log($"[TJGeneratorsVideo] 已将历史视频应用到 {targetPath}");
            }
            catch (Exception e)
            {
                ErrorDialogUtils.ShowErrorDialog("错误", $"应用失败: {e.Message}", LogTag);
            }

            Repaint();
        }

        // ========== 生成 ==========

        private void StartGeneration()
        {
            if (_currentGenerator == null)
                return;

            if (string.IsNullOrWhiteSpace(textPrompt))
            {
                ErrorDialogUtils.ShowErrorDialog("错误", "请输入文本提示词。", LogTag);
                return;
            }

            bool hasImage = referenceImagePaths.Count > 0;

            EnsureTargetVideo();

            if (_currentGenerator is DynamicGenerator dynamicGen)
            {
                dynamicGen.SetTextPrompt(textPrompt.Trim());
                dynamicGen.SetImagePaths(hasImage ? referenceImagePaths : null);

                // Auto-set mode based on whether a reference image is provided:
                // image present → reference_image, text-only → text_to_video
                string mode = hasImage ? "reference_image" : "text_to_video";
                dynamicGen.SetParameter("mode", mode);
            }

            isGenerating = true;
            generationStatus = "准备中...";
            generationProgress = 0f;

            string assetGuid = targetVideoAsset?.guid ?? "";
            EditorCoroutineUtility.StartCoroutineOwnerless(
                _pipeline.StartGeneration(_currentGenerator, assetGuid)
            );
        }

        // ========== IGenerationPipelineHost ==========

        public TJGeneratorsAssetReference GetTargetAsset() => targetVideoAsset;

        public void StartGeneration(ModelGeneratorBase generator)
        {
            if (generator == _currentGenerator)
                StartGeneration();
        }

        public void RefreshHistory()
        {
            generationHistory = TJGeneratorsHistoryManager.LoadHistoryForAsset(
                GetCurrentVideoAssetGuid()
            );
            selectedHistoryIndex = generationHistory.Count > 0 ? 0 : -1;
            Repaint();
        }

        public void ShowPreviewModel(string assetPath)
        {
            // Video window doesn't need 3D/Prefab preview
        }

        public string GetTextureSavePath(ModelGeneratorBase generator) => null;

        public void OnTextureSaved(string savePath, ModelGeneratorBase generator) { }

        public string GetAudioSavePath(ModelGeneratorBase generator) => null;

        public void OnAudioSaved(string savePath, ModelGeneratorBase generator) { }

        public string GetVideoSavePath(ModelGeneratorBase generator)
        {
            if (!AssetDatabase.IsValidFolder("Assets/TJGenerators"))
                AssetDatabase.CreateFolder("Assets", "TJGenerators");
            if (!AssetDatabase.IsValidFolder("Assets/TJGenerators/History"))
                AssetDatabase.CreateFolder("Assets/TJGenerators", "History");

            string uniqueName = "Video_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4";
            return AssetDatabase.GenerateUniqueAssetPath(
                "Assets/TJGenerators/History/" + uniqueName
            );
        }

        public void OnVideoSaved(string savePath, ModelGeneratorBase generator)
        {
            TJLog.Log($"[TJGeneratorsVideo] OnVideoSaved: {savePath}");
            TJGeneratorsGenerationLabel.EnableLabel(TJGeneratorsAssetReference.FromPath(savePath));

            // Copy to target asset if bound
            if (targetVideoAsset != null && targetVideoAsset.IsValid())
            {
                string targetPath = targetVideoAsset.GetPath();
                try
                {
                    string sourceExt = Path.GetExtension(savePath).ToLowerInvariant();
                    string targetExt = Path.GetExtension(targetPath).ToLowerInvariant();

                    // Update target path extension if needed
                    if (!string.Equals(sourceExt, targetExt, StringComparison.OrdinalIgnoreCase))
                    {
                        string newPath = Path.ChangeExtension(targetPath, sourceExt);
                        string oldAbsolute = PathUtils.ToAbsoluteAssetPath(targetPath);
                        if (File.Exists(oldAbsolute))
                            AssetDatabase.DeleteAsset(targetPath);

                        targetPath = newPath;
                        targetVideoAsset = TJGeneratorsAssetReference.FromPath(targetPath);
                        titleContent = new GUIContent($"TJGenerators 视频 - {Path.GetFileNameWithoutExtension(targetPath)}");
                        string newGuid = targetVideoAsset.guid;
                        if (!string.IsNullOrEmpty(newGuid))
                            s_videoOpenWindows[newGuid] = this;
                    }

                    string srcAbsolute = PathUtils.ToAbsoluteAssetPath(savePath);
                    string dstAbsolute = PathUtils.ToAbsoluteAssetPath(targetPath);
                    string targetDir = Path.GetDirectoryName(dstAbsolute);
                    if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);
                    File.Copy(srcAbsolute, dstAbsolute, true);
                    AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
                    TJGeneratorsGenerationLabel.EnableLabel(targetVideoAsset);
                    TJLog.Log($"[TJGeneratorsVideo] 视频已复制到目标路径: {targetPath}");
                }
                catch (Exception e)
                {
                    TJLog.LogWarning($"[TJGeneratorsVideo] 复制到目标视频失败: {e.Message}");
                }
            }

            generationStatus = "完成";
            generationProgress = 1f;
            isGenerating = false;
            RefreshHistory();
            Repaint();
        }

        void IGenerationPipelineHost.Repaint()
        {
            Repaint();
        }

        // ========== 辅助方法 ==========

        private string GetCurrentVideoAssetGuid() => targetVideoAsset?.guid ?? "";

        private void EnsureTargetVideo()
        {
            if (targetVideoAsset != null && targetVideoAsset.IsValid())
                return;

            if (!AssetDatabase.IsValidFolder("Assets/TJGenerators"))
                AssetDatabase.CreateFolder("Assets", "TJGenerators");

            string videoPath = AssetDatabase.GenerateUniqueAssetPath("Assets/TJGenerators/New Video.mp4");
            videoPath = CreateBlankVideo(videoPath);
            if (string.IsNullOrEmpty(videoPath))
            {
                TJLog.LogError("[TJGeneratorsVideo] 无法创建视频占位资产");
                return;
            }
            targetVideoAsset = TJGeneratorsAssetReference.FromPath(videoPath);
            titleContent = new GUIContent($"TJGenerators 视频 - {Path.GetFileNameWithoutExtension(videoPath)}");
            if (!string.IsNullOrEmpty(targetVideoAsset.guid))
                s_videoOpenWindows[targetVideoAsset.guid] = this;
            Repaint();
        }

        /// <summary>
        /// 创建一个空白的 mp4 占位文件。由于 Unity 无法从零生成有效视频，
        /// 这里创建最小化的占位文件，生成完成后会被实际视频覆盖。
        /// </summary>
        public static string CreateBlankVideo(string path)
        {
            path = Path.ChangeExtension(path, ".mp4");
            string absolutePath = PathUtils.ToAbsoluteAssetPath(path);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            // Write minimal placeholder bytes (will be overwritten on generation complete)
            File.WriteAllBytes(absolutePath, new byte[0]);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            TJGeneratorsGenerationLabel.EnableLabel(TJGeneratorsAssetReference.FromPath(path));
            return path;
        }
    }
}
#endif
