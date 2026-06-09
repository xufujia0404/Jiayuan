#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Codely.Newtonsoft.Json.Linq;
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
    /// TJGenerators 图片生成窗口（文生图 / 图生图）。
    /// </summary>
    public class TJGeneratorsImageWindow : GenerationWindowBase, IGenerationPipelineHost
    {
        // ========== 固定配置 ==========
        protected override ConfigType WindowConfigType => ConfigType.Image;
        protected override string LogTag => "[TJGeneratorsImage]";

        [SerializeField]
        private string textPrompt = "";

        [SerializeField]
        private TJGeneratorsAssetReference targetImageAsset;

        private const int MaxReferenceImages = 14;
        private readonly List<string> referenceImagePaths = new List<string>();
        private readonly List<Texture2D> referenceUploadedImages = new List<Texture2D>();

        private static readonly Dictionary<string, TJGeneratorsImageWindow> imageOpenWindows =
            new Dictionary<string, TJGeneratorsImageWindow>();

        private Texture2D imagePreviewTexture;
        [SerializeField]
        private string forcedGeneratorId;

        /// <summary>outputType 为 image 且配置启用时可选；prompt 经 DynamicRequestJsonBuilder.BuildEnhancedPrompt 拼为前缀</summary>
        private MaterialTemplateOptionConfig selectedPromptTemplate;
        private JObject frontierSequenceProfilesRoot;
        private string frontierSequenceResolvedConfigPath;
        private readonly List<string> frontierSequenceProfileIds = new List<string>();
        private readonly List<string> frontierSequenceProfileNames = new List<string>();
        [SerializeField] private int frontierSequenceProfileIndex;
        [SerializeField] private string frontierSequenceProfileId;

        private const string UnityTerrainHeightmapTemplateId = "unity_terrain_heightmap";

        [SerializeField]
        private bool terrainHeightmapGaussianBlur = true;

        [SerializeField]
        private bool terrainHeightmapMedian3x3 = true;

        [SerializeField]
        [Range(0.5f, 3f)]
        private float terrainHeightmapBlurSigma = 1.2f;

        [SerializeField]
        private bool terrainHeightmapRemapFoldout = true;

        [SerializeField]
        private bool terrainHeightmapPercentileNormalize = true;

        [SerializeField]
        [Range(0f, 0.2f)]
        private float terrainHeightmapPercentileLow = 0.05f;

        [SerializeField]
        [Range(0.8f, 1f)]
        private float terrainHeightmapPercentileHigh = 0.95f;

        [SerializeField]
        [Range(0.35f, 2.5f)]
        private float terrainHeightmapHeightGamma = 1f;

        [SerializeField]
        [Range(0f, 1f)]
        private float terrainHeightmapRemapOutMin = 0.02f;

        [SerializeField]
        [Range(0f, 1f)]
        private float terrainHeightmapRemapOutMax = 0.98f;

        // ========== 序列帧切图/抠图 ==========
        [SerializeField]
        private int spriteSliceColumns = 4;
        [SerializeField]
        private int spriteSliceRows = 3;
        [SerializeField]
        [Range(1f, 60f)]
        private float spriteSliceFps = 12f;
        [SerializeField]
        private bool spriteSliceLoop = true;
        [SerializeField]
        [Range(0.05f, 0.35f)]
        private float chromaKeyTolerance = 0.16f;
        [SerializeField]
        [Range(0f, 0.3f)]
        private float chromaFeather = 0.04f;
        private Texture2D processedPreviewTexture;
        private string processedPreviewSourcePath;
        private bool processedPreviewValid;
        [SerializeField]
        [Range(1f, 6f)]
        private float historyMainPreviewZoom = 1f;
        [SerializeField]
        private Vector2 historyMainPreviewPan = Vector2.zero;
        private bool isDraggingMainPreview;

        // ========== 静态入口 ==========
        public static void ShowWindow()
        {
            var rect = GetDefaultMainWindowRect();
            var window = GetWindowWithRect<TJGeneratorsImageWindow>(
                rect,
                utility: false,
                title: "TJGenerators 图片生成",
                focus: true
            );
            window.forcedGeneratorId = null;
            window.titleContent = new GUIContent("TJGenerators 图片生成");
            // Handle window reuse: if the window is already alive, OnEnable may not run again.
            // Rebuild generator list for normal image mode immediately.
            window.InitializeGeneratorsFromConfig(ConfigType.Image);
            window.ApplyForcedGeneratorFilterIfNeeded();
            window.LoadFrontierSequenceProfilesIfNeeded();
            window.Repaint();
        }

        public static void ShowFrontierSequenceWindow()
        {
            const string title = "TJGenerators 序列帧（Frontier）";
            var rect = GetDefaultMainWindowRect();
            var window = GetWindowWithRect<TJGeneratorsImageWindow>(
                rect,
                utility: false,
                title: title,
                focus: true
            );
            window.forcedGeneratorId = "frontier-effect";
            window.titleContent = new GUIContent(title);
            // Handle window reuse: if the window is already alive, OnEnable may not run again.
            // Force-lock generator to frontier-effect immediately.
            window.InitializeGeneratorsFromConfig(ConfigType.Image);
            window.ApplyForcedGeneratorFilterIfNeeded();
            window.LoadFrontierSequenceProfilesIfNeeded();
            window.Repaint();
        }

        public static void OpenForAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                ShowWindow();
                return;
            }

            // 允许绑定 jpg / jpeg / png
            if (
                !assetPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                && !assetPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                && !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            )
            {
                ErrorDialogUtils.ShowErrorDialog(
                    "TJGenerators 图片生成",
                    "仅支持绑定 .jpg / .jpeg / .png 的图片资产。\r\n\r\n建议先创建“生成图片”新资产。",
                    "[TJGeneratorsImage]"
                );
                return;
            }

            GenerationWindowBase.OpenForAsset(
                assetPath,
                imageOpenWindows,
                "[TJGeneratorsImage]",
                "TJGenerators 图片 - {0}",
                () =>
                {
                    var window = CreateInstance<TJGeneratorsImageWindow>();
                    SetDefaultWindowSize(window);
                    return window;
                },
                (w, r) => w.targetImageAsset = r,
                ShowWindow
            );
        }

        // ========== 生命周期 ==========
        private void OnEnable()
        {
            wantsMouseMove = true;
            InitializeGeneratorsFromConfig(ConfigType.Image);
            ApplyForcedGeneratorFilterIfNeeded();
            EnsureWindowTitle();
            LoadFrontierSequenceProfilesIfNeeded();

            EnsureTargetImage();

            // 延迟加载历史记录，避免 OnEnable 内触发多次重绘
            EditorApplication.delayCall += () =>
            {
                if (this == null)
                    return;
                generationHistory = TJGeneratorsHistoryManager.LoadHistoryForAsset(
                    GetCurrentImageAssetGuid()
                );
                selectedHistoryIndex = generationHistory.Count > 0 ? 0 : -1;
                Repaint();
            };

            // 获取用户积分
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
            if (targetImageAsset != null && !string.IsNullOrEmpty(targetImageAsset.guid))
            {
                imageOpenWindows.Remove(targetImageAsset.guid);
            }

            imagePreviewTexture = null;
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
        protected override string GetCurrentAssetGuid() => GetCurrentImageAssetGuid();

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
            maxSize = new Vector2(10000f, 10000f);
            isVerticalLayout = false;
            currentHistoryPanelWidth = splitLayout.RightPanelWidth;
            _effectiveLeftPanelWidth = CommonStyles.LeftComponentWidth;

            if (_generators == null || _generators.Count == 0)
            {
                EditorGUI.DrawRect(
                    new Rect(0, 0, position.width, position.height),
                    CommonStyles.WindowBackgroundColor
                );
                EditorGUILayout.HelpBox("未找到可用的图片生成器，请检查配置", MessageType.Error);
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
                    "目标图片",
                    DrawHeaderTargetContentRect,
                    SelectTargetImageAsset
                );
                GUILayout.Space(CommonStyles.Space2);

                if (string.IsNullOrEmpty(forcedGeneratorId))
                {
                    UIComponents.DrawModelSelector(
                        currentSelectedModel?.Name ?? _currentGenerator?.DisplayName ?? "未选择",
                        currentSelectedModel,
                        OnModelSelected,
                        ConfigType.Image
                    );
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("模型：", GUILayout.Width(44));
                    GUILayout.Label(_currentGenerator?.DisplayName ?? "Frontier");
                    GUILayout.EndHorizontal();
                }

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

                DrawTerrainHeightmapAfterGenerationSection();

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
            if (targetImageAsset != null && targetImageAsset.IsValid())
            {
                string imageName = Path.GetFileNameWithoutExtension(targetImageAsset.GetPath());
                if (GUILayout.Button(imageName, CommonStyles.LinkStyle))
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        targetImageAsset.GetPath()
                    );
                    if (texture != null)
                    {
                        EditorGUIUtility.PingObject(texture);
                        Selection.activeObject = texture;
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
            if (targetImageAsset != null && targetImageAsset.IsValid())
            {
                string imageName = Path.GetFileNameWithoutExtension(targetImageAsset.GetPath());
                if (GUI.Button(rect, imageName, CommonStyles.TargetPrefabNameStyle))
                    SelectTargetImageAsset();
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            }
            else
            {
                GUI.Label(rect, "未绑定（生成时自动创建）", CommonStyles.ContentStyle);
            }
        }

        private void SelectTargetImageAsset()
        {
            if (targetImageAsset == null || !targetImageAsset.IsValid())
                return;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(targetImageAsset.GetPath());
            if (tex != null)
            {
                EditorGUIUtility.PingObject(tex);
                Selection.activeObject = tex;
            }
        }

        private GeneratorConfig GetActiveImageGeneratorConfig()
        {
            return _currentGenerator == null
                ? null
                : GetGeneratorConfigFromIndex(_currentGenerator.GeneratorId);
        }

        /// <summary>
        /// 是否处于「生成序列帧（Frontier）」专用工具：仅当通过 <see cref="ShowFrontierSequenceWindow"/> 打开且锁定 frontier-effect 时为真。
        /// instruction 模板与 knowledge 参考图只在此模式下生效；普通图片窗口中选 Frontier 模型不走该管线。
        /// </summary>
        private bool IsFrontierSequenceMode()
        {
            return string.Equals(forcedGeneratorId, "frontier-effect", StringComparison.OrdinalIgnoreCase);
        }

        private bool HasFrontierSequenceProfile()
        {
            return !string.IsNullOrEmpty(frontierSequenceProfileId);
        }

        private void ShowPromptTemplateSelectorWindow()
        {
            var cfg = GetActiveImageGeneratorConfig();
            if (cfg?.promptTemplateSelector?.options == null || cfg.promptTemplateSelector.options.Count == 0)
            {
                ErrorDialogUtils.ShowErrorDialog(
                    "提示词模板不可用",
                    "当前模型未配置提示词模板选项（options 为空）",
                    LogTag
                );
                return;
            }

            TJGeneratorsMaterialTemplateSelectorWindow.ShowWindow(
                cfg.promptTemplateSelector.options,
                OnPromptTemplateSelected,
                string.IsNullOrEmpty(cfg.promptTemplateSelector.title)
                    ? "选择提示词模板"
                    : cfg.promptTemplateSelector.title,
                showPreviewThumbnails: false
            );
        }

        private void OnPromptTemplateSelected(MaterialTemplateOptionConfig template)
        {
            selectedPromptTemplate = template;

            if (_currentGenerator is DynamicGenerator dg)
                dg.SetPromptTemplateSelection(selectedPromptTemplate);

            Repaint();
        }

        private void DrawPromptTemplateSelector()
        {
            var cfg = GetActiveImageGeneratorConfig();
            string title =
                !string.IsNullOrEmpty(cfg?.promptTemplateSelector?.title)
                    ? cfg.promptTemplateSelector.title + "："
                    : "提示词模板：";

            GUILayout.Space(10);
            UIComponents.DrawSelectorRow(
                title,
                selectedPromptTemplate?.name,
                "选择模板",
                () =>
                {
                    selectedPromptTemplate = null;
                    if (_currentGenerator is DynamicGenerator dg)
                        dg.SetPromptTemplateSelection(null);
                    Repaint();
                },
                ShowPromptTemplateSelectorWindow
            );

            GUILayout.Space(10);
        }

        private void DrawFrontierSequenceProfileSelector()
        {
            // 兜底：某些窗口生命周期顺序下，forcedGeneratorId 可能在 OnEnable 之后才写入。
            // Frontier 模式且列表为空时，这里主动重载一次配置，避免“明明有配置却提示找不到”。
            if (IsFrontierSequenceMode() && frontierSequenceProfileNames.Count == 0)
                LoadFrontierSequenceProfilesIfNeeded();

            GUILayout.Space(10);
            GUILayout.Label("指令模板：", CommonStyles.HeaderStyle);

            if (frontierSequenceProfileNames.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(frontierSequenceResolvedConfigPath)
                        ? "未找到模板配置，请检查 package 内 Editor/Config/FrontierSequenceProfiles.json"
                        : $"未找到可用模板，配置文件：{frontierSequenceResolvedConfigPath}",
                    MessageType.Warning
                );
                return;
            }

            int newIndex = EditorGUILayout.Popup(
                "模板",
                Mathf.Clamp(frontierSequenceProfileIndex, 0, frontierSequenceProfileNames.Count - 1),
                frontierSequenceProfileNames.ToArray()
            );
            if (newIndex != frontierSequenceProfileIndex)
            {
                frontierSequenceProfileIndex = newIndex;
                frontierSequenceProfileId = frontierSequenceProfileIds[frontierSequenceProfileIndex];
            }

            GUILayout.Space(8);
        }

        private void DrawInputSection()
        {
            // Frontier 序列帧模式下，始终使用默认 instruction/profile，
            // 不向用户暴露模板选择 UI，避免额外认知负担。
            if (!IsFrontierSequenceMode())
                DrawPromptTemplateSelector();

            UIComponents.DrawSectionTitle("文本提示词", uppercase: false);
            GUILayout.Space(CommonStyles.Space1);
            textPrompt = UIComponents.DrawPromptInputBox(
                textPrompt,
                "描述你想要生成的图片...",
                "image_prompt_input"
            );

            GUILayout.Space(CommonStyles.Space2);

            DrawReferenceImagesSection();
        }

        private void DrawReferenceImagesSection()
        {
            GUILayout.BeginHorizontal();
            UIComponents.DrawSectionTitle("参考图片（可选）", uppercase: false);
            GUILayout.EndHorizontal();
            GUILayout.Space(CommonStyles.Space1);

            float thumbSize = 64f;
            float clearSize = 20f;
            float availableWidth = Mathf.Max(
                300f,
                Mathf.Min(position.width - 60f, _effectiveLeftPanelWidth - 60f)
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

            var allParams = GetCurrentGeneratorParameters();
            List<ParameterConfig> filteredParams = null;
            if (allParams != null && allParams.Count > 0)
            {
                filteredParams = new List<ParameterConfig>(allParams.Count);
                for (int i = 0; i < allParams.Count; i++)
                {
                    var p = allParams[i];
                    if (p == null || string.IsNullOrEmpty(p.id))
                        continue;

                    if (p.id == "isSegmentation" || p.id == "qValue" || p.id == "resizeWidth")
                        continue;
                    if (IsFrontierSequenceMode() &&
                        (string.Equals(p.id, "aspectRatio", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(p.id, "aspect_ratio", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    filteredParams.Add(p);
                }
            }

            showAdvancedSettings = UIComponents.DrawAdvancedSettingsFoldout(
                showAdvancedSettings,
                provider,
                filteredParams
            );

            if (provider is DynamicGenerator dyn)
            {
                bool hasRef = referenceImagePaths != null && referenceImagePaths.Count > 0;
                dyn.SyncReferenceImagesForCostPreview(hasRef);
            }
            SyncGenerationCostWithCurrentGeneratorState();
        }

        private void DrawGenerationSection()
        {
            bool canGenerate =
                _currentGenerator != null && !string.IsNullOrWhiteSpace(textPrompt);
            UIComponents.DrawGenerationSection(
                isGenerating,
                generationProgress,
                generationStatus,
                canGenerate,
                StartGeneration,
                null,
                Repaint,
                currentGenerationCost
            );
        }

        private bool IsUnityTerrainHeightmapTemplateSelected()
        {
            return string.Equals(
                selectedPromptTemplate?.id,
                UnityTerrainHeightmapTemplateId,
                StringComparison.OrdinalIgnoreCase
            );
        }

        /// <summary>地形模板：后处理选项与「一键生成地形」位于生成按钮下方，顺序为「生成 → 后处理设置 → 建地形」。</summary>
        private void DrawTerrainHeightmapAfterGenerationSection()
        {
            if (!IsUnityTerrainHeightmapTemplateSelected())
                return;

            UIComponents.DrawSeparator();
            GUILayout.Space(4);

            GUILayout.Label("地形高度图（生成后）", CommonStyles.HeaderStyle);
            GUILayout.Space(6);

            GUILayout.Label(
                "在右侧历史记录中选中对应 PNG 后，应用后处理并创建场景地形。",
                CommonStyles.SmallGreyLabelStyle
            );
            GUILayout.Space(8);

            terrainHeightmapMedian3x3 = EditorGUILayout.ToggleLeft(
                "后处理：Median 3x3 去尖刺（散点离群点）",
                terrainHeightmapMedian3x3
            );

            GUILayout.Space(4);
            terrainHeightmapGaussianBlur = EditorGUILayout.ToggleLeft(
                "后处理：高斯模糊平滑",
                terrainHeightmapGaussianBlur
            );
            if (terrainHeightmapGaussianBlur)
            {
                EditorGUI.indentLevel++;
                terrainHeightmapBlurSigma = EditorGUILayout.Slider(
                    "模糊强度 (σ)",
                    terrainHeightmapBlurSigma,
                    0.5f,
                    3f
                );
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(8);
            terrainHeightmapRemapFoldout = EditorGUILayout.Foldout(
                terrainHeightmapRemapFoldout,
                "高度重映射（类似 Terrain Tools · Height Remap）",
                true
            );
            if (terrainHeightmapRemapFoldout)
            {
                EditorGUI.indentLevel++;
                terrainHeightmapPercentileNormalize = EditorGUILayout.ToggleLeft(
                    "百分位拉伸（去掉极暗/极亮离群点再起有效对比）",
                    terrainHeightmapPercentileNormalize
                );
                EditorGUI.BeginDisabledGroup(!terrainHeightmapPercentileNormalize);
                terrainHeightmapPercentileLow = EditorGUILayout.Slider(
                    new GUIContent(
                        "低端截断",
                        "低于该百分位的亮度视作海平面一端，类似压低海底噪声"
                    ),
                    terrainHeightmapPercentileLow,
                    0f,
                    0.2f
                );
                terrainHeightmapPercentileHigh = EditorGUILayout.Slider(
                    new GUIContent(
                        "高端截断",
                        "高于该百分位的亮度视作山顶一端"
                    ),
                    terrainHeightmapPercentileHigh,
                    0.8f,
                    1f
                );
                EditorGUI.EndDisabledGroup();
                if (terrainHeightmapPercentileHigh <= terrainHeightmapPercentileLow)
                    terrainHeightmapPercentileHigh =
                        Mathf.Min(1f, terrainHeightmapPercentileLow + 0.02f);

                terrainHeightmapHeightGamma = EditorGUILayout.Slider(
                    new GUIContent(
                        "高度曲线 Gamma",
                        "1 = 线性；小于 1 中间调抬高（更陡）；大于 1 更平（更多平原）"
                    ),
                    terrainHeightmapHeightGamma,
                    0.35f,
                    2.5f
                );

                EditorGUILayout.LabelField(
                    "输出垂直范围（归一化高度映射到 [最低, 最高]）",
                    CommonStyles.SmallGreyLabelStyle
                );
                terrainHeightmapRemapOutMin = EditorGUILayout.Slider(
                    new GUIContent("输出最低", "地形最凹处对应高度图灰度下限"),
                    terrainHeightmapRemapOutMin,
                    0f,
                    1f
                );
                terrainHeightmapRemapOutMax = EditorGUILayout.Slider(
                    new GUIContent("输出最高", "地形最高处对应高度图灰度上限"),
                    terrainHeightmapRemapOutMax,
                    0f,
                    1f
                );
                if (terrainHeightmapRemapOutMax <= terrainHeightmapRemapOutMin)
                    terrainHeightmapRemapOutMax =
                        Mathf.Min(1f, terrainHeightmapRemapOutMin + 0.02f);

                EditorGUI.indentLevel--;
            }

            GUILayout.Space(10);

            var selectedHistoryItem =
                selectedHistoryIndex >= 0 && selectedHistoryIndex < generationHistory.Count
                    ? generationHistory[selectedHistoryIndex]
                    : null;
            bool canTerrain =
                CanGenerateTerrainFromHistoryItem(selectedHistoryItem);
            EditorGUI.BeginDisabledGroup(!canTerrain);
            if (GUILayout.Button("一键生成地形", GUILayout.Height(28)))
                GenerateTerrainFromHeightmap(selectedHistoryIndex);
            EditorGUI.EndDisabledGroup();

            if (!canTerrain)
            {
                GUILayout.Space(4);
                GUILayout.Label(
                    "请先在历史中选中由本模板生成的已完成 PNG。",
                    CommonStyles.SmallGreyLabelStyle
                );
            }
        }

        private void DrawHistoryPanel(float panelWidth)
        {
            float historyPanelHeight = isVerticalLayout ? position.height * 0.45f : position.height;
            float historyPanelInner = CommonStyles.HistoryPanelInnerWidth(panelWidth);
            float historyScrollInner = CommonStyles.HistoryScrollViewLayoutWidth(panelWidth);
            EnsureHistorySelectionAndFallback();

            UIComponents.BeginHistoryPanel(panelWidth, historyPanelHeight, isVerticalLayout);

            Texture2D historyPreviewTex = null;
            if (selectedHistoryIndex >= 0 && selectedHistoryIndex < generationHistory.Count)
            {
                var selectedItem = generationHistory[selectedHistoryIndex];
                if (!selectedItem.isGenerating)
                    historyPreviewTex = GetPreviewTextureForHistoryItem(selectedItem);
            }

            // 没有选择历史时，回退到当前绑定资产预览
            if (historyPreviewTex == null)
                historyPreviewTex = imagePreviewTexture;

            // processedPreview 需要绑定“当前实际处理的源图”（可能来自历史，也可能来自目标资产）。
            // 否则当用户未选中历史、直接处理目标图时，会出现“抠图没反应/预览不更新”的错觉。
            string activeSourcePath = GetActivePreviewSourcePath()?.Replace('\\', '/');
            historyPreviewTex = GetEffectivePreviewTexture(historyPreviewTex, activeSourcePath);

            float previewBlockHeight = DrawHistoryTexturePreviewWithSliceOverlay(
                historyPreviewTex,
                historyPanelInner
            );

            float scrollHeight = Mathf.Max(120f, historyPanelHeight - previewBlockHeight - 12f);
            historyScrollPosition = GUILayout.BeginScrollView(
                historyScrollPosition,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.Height(scrollHeight));

            if (generationHistory.Count == 0)
                UIComponents.DrawHistoryEmptyState();
            else
                DrawHistoryGrid(historyScrollInner);

            DrawHistoryActions();
            GUILayout.EndScrollView();

            UIComponents.EndHistoryPanel();
        }

        /// <summary>
        /// 校正历史列表与选中索引。不在此处把绑定目标塞进历史，避免与精灵窗口不一致（无真实生成记录时不应出现占位条目）。
        /// </summary>
        private void EnsureHistorySelectionAndFallback()
        {
            if (generationHistory == null)
                generationHistory = new List<TJGeneratorsGenerationHistoryItem>();

            if (generationHistory.Count > 0)
                selectedHistoryIndex = Mathf.Clamp(selectedHistoryIndex, 0, generationHistory.Count - 1);
            else
                selectedHistoryIndex = -1;
        }

        private Texture2D GetEffectivePreviewTexture(Texture2D historyPreviewTex, string selectedHistoryPath)
        {
            // Non-frontier windows must never show cutout/slicing processed previews.
            if (!IsFrontierSequenceMode())
            {
                if (processedPreviewValid || processedPreviewTexture != null)
                    ResetProcessedPreview();
                return historyPreviewTex;
            }

            if (!processedPreviewValid || processedPreviewTexture == null)
                return historyPreviewTex;

            if (string.IsNullOrEmpty(selectedHistoryPath) || !string.Equals(selectedHistoryPath, processedPreviewSourcePath, StringComparison.OrdinalIgnoreCase))
            {
                ResetProcessedPreview();
                return historyPreviewTex;
            }

            return processedPreviewTexture;
        }

        private float DrawHistoryTexturePreviewWithSliceOverlay(Texture2D previewTex, float panelWidth)
        {
            // 预览窗口跟随整体窗口拉伸：宽高都自适应变化，不强制 1:1。
            float previewWidth = Mathf.Max(140f, panelWidth - 12f);
            float previewHeight = Mathf.Max(
                140f,
                isVerticalLayout ? position.height * 0.32f : position.height * 0.56f
            );
            Rect areaRect = GUILayoutUtility.GetRect(
                previewWidth,
                previewHeight,
                GUILayout.ExpandWidth(true)
            );

            Event evt = Event.current;
            EditorGUI.DrawRect(areaRect, new Color(0.12f, 0.12f, 0.12f, 1f));

            if (previewTex == null)
                return areaRect.height + 8f;

            Rect drawRect = FitRectKeepAspect(areaRect, previewTex.width, previewTex.height);
            var texCoords = HandlePreviewZoomAndPanInput(drawRect, evt);
            GUI.DrawTextureWithTexCoords(drawRect, previewTex, texCoords, true);

            // Frontier 序列帧工具：切割线应随时可见（无论当前预览源来自历史还是目标图），便于直接调整参数。
            if (IsFrontierSequenceMode() && !string.IsNullOrEmpty(GetActivePreviewSourcePath()))
                DrawSliceGridOverlay(drawRect, Mathf.Max(1, spriteSliceColumns), Mathf.Max(1, spriteSliceRows), texCoords);

            return areaRect.height + 8f;
        }

        private Rect HandlePreviewZoomAndPanInput(Rect drawRect, Event evt)
        {
            float zoom = Mathf.Clamp(historyMainPreviewZoom, 1f, 6f);
            float visibleW = 1f / zoom;
            float visibleH = 1f / zoom;

            historyMainPreviewPan.x = Mathf.Clamp(historyMainPreviewPan.x, 0f, 1f - visibleW);
            historyMainPreviewPan.y = Mathf.Clamp(historyMainPreviewPan.y, 0f, 1f - visibleH);

            if (evt != null)
            {
                if (evt.type == EventType.ScrollWheel && drawRect.Contains(evt.mousePosition))
                {
                    float oldZoom = zoom;
                    float relX = Mathf.Clamp01((evt.mousePosition.x - drawRect.x) / Mathf.Max(1f, drawRect.width));
                    float relY = Mathf.Clamp01((evt.mousePosition.y - drawRect.y) / Mathf.Max(1f, drawRect.height));
                    float focusU = historyMainPreviewPan.x + relX * (1f / oldZoom);
                    float focusV = historyMainPreviewPan.y + relY * (1f / oldZoom);

                    zoom = Mathf.Clamp(oldZoom + (-evt.delta.y * 0.12f), 1f, 6f);
                    historyMainPreviewZoom = zoom;

                    float newVisibleW = 1f / zoom;
                    float newVisibleH = 1f / zoom;
                    historyMainPreviewPan.x = Mathf.Clamp(focusU - relX * newVisibleW, 0f, 1f - newVisibleW);
                    historyMainPreviewPan.y = Mathf.Clamp(focusV - relY * newVisibleH, 0f, 1f - newVisibleH);

                    evt.Use();
                    Repaint();
                }
                else if (evt.type == EventType.MouseDown && evt.button == 0 && drawRect.Contains(evt.mousePosition) && zoom > 1.001f)
                {
                    isDraggingMainPreview = true;
                    evt.Use();
                }
                else if (evt.type == EventType.MouseDrag && evt.button == 0 && isDraggingMainPreview && zoom > 1.001f)
                {
                    float panStepX = evt.delta.x / Mathf.Max(1f, drawRect.width) * (1f / zoom);
                    float panStepY = evt.delta.y / Mathf.Max(1f, drawRect.height) * (1f / zoom);
                    historyMainPreviewPan.x = Mathf.Clamp(historyMainPreviewPan.x - panStepX, 0f, 1f - (1f / zoom));
                    historyMainPreviewPan.y = Mathf.Clamp(historyMainPreviewPan.y - panStepY, 0f, 1f - (1f / zoom));
                    evt.Use();
                    Repaint();
                }
                else if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
                {
                    isDraggingMainPreview = false;
                }
            }

            if (zoom <= 1.001f)
                historyMainPreviewPan = Vector2.zero;

            return new Rect(
                historyMainPreviewPan.x,
                historyMainPreviewPan.y,
                1f / zoom,
                1f / zoom
            );
        }

        private static Rect FitRectKeepAspect(Rect outer, int texW, int texH)
        {
            float srcAspect = texW / Mathf.Max(1f, texH);
            float dstAspect = outer.width / Mathf.Max(1f, outer.height);

            if (dstAspect > srcAspect)
            {
                float w = outer.height * srcAspect;
                float x = outer.x + (outer.width - w) * 0.5f;
                return new Rect(x, outer.y, w, outer.height);
            }

            float h = outer.width / Mathf.Max(0.01f, srcAspect);
            float y = outer.y + (outer.height - h) * 0.5f;
            return new Rect(outer.x, y, outer.width, h);
        }

        private static void DrawSliceGridOverlay(Rect drawRect, int cols, int rows, Rect texCoords)
        {
            if (cols <= 1 && rows <= 1)
                return;

            Handles.BeginGUI();
            Color prevColor = Handles.color;
            // 用红色提高在绿幕/绿底图上的可见性
            Handles.color = new Color(1f, 0f, 0f, 0.9f);

            for (int c = 1; c < cols; c++)
            {
                // 分割线固定在图片坐标系，并随预览的缩放/平移（texCoords）一起变化：
                // 当用户放大到某一帧细节时，网格线应自然移出视野（只在边缘可见），而不是始终铺满屏幕。
                float u = c / (float)cols;
                float nx = (u - texCoords.x) / texCoords.width;
                if (nx <= 0f || nx >= 1f)
                    continue;
                float x = drawRect.x + drawRect.width * nx;
                Handles.DrawLine(new Vector2(x, drawRect.y), new Vector2(x, drawRect.yMax));
            }
            for (int r = 1; r < rows; r++)
            {
                float v = r / (float)rows;
                float ny = (v - texCoords.y) / texCoords.height;
                if (ny <= 0f || ny >= 1f)
                    continue;
                float y = drawRect.y + drawRect.height * ny;
                Handles.DrawLine(new Vector2(drawRect.x, y), new Vector2(drawRect.xMax, y));
            }

            Handles.color = prevColor;
            Handles.EndGUI();
        }

        private void DrawHistoryGrid(float historyContentWidth)
        {
            float tileWidth = EditorUiScale.S(currentHistoryTileSize);
            float labelHeight = currentHistoryTileSize >= 100f ? EditorUiScale.S(40f) : EditorUiScale.S(32f);
            float tileHeight = tileWidth + labelHeight;
            int itemsPerRow = ComputeHistoryItemsPerRow(historyContentWidth, tileWidth);

            for (int i = 0; i < generationHistory.Count; i += itemsPerRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = 0; j < itemsPerRow && (i + j) < generationHistory.Count; j++)
                {
                    int index = i + j;
                    var item = generationHistory[index];
                    bool isSelected = selectedHistoryIndex == index;

                    GUILayout.BeginVertical(
                        GetScaledHistoryTileStyle(isSelected),
                        GUILayout.Width(tileWidth),
                        GUILayout.Height(tileHeight)
                    );

                    float previewSize = GetScaledHistoryPreviewSize(tileWidth);
                    Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);

                    DrawImageHistoryPreview(previewRect, item);

                    if (
                        !item.isGenerating
                        && Event.current.type == EventType.MouseDown
                        && previewRect.Contains(Event.current.mousePosition)
                    )
                    {
                        if (selectedHistoryIndex != index)
                            ResetProcessedPreview();
                        selectedHistoryIndex = index;
                        Event.current.Use();
                        Repaint();
                    }

                    if (
                        !item.isGenerating
                        && Event.current.type == EventType.ContextClick
                        && previewRect.Contains(Event.current.mousePosition)
                    )
                    {
                        ShowHistoryContextMenu(index);
                        Event.current.Use();
                    }

                    GUILayout.Label(GetHistoryUserPromptLabel(item), CommonStyles.HistoryLabelStyle);
                    GUILayout.Label(
                        GetModelDisplayLabelFromIndex(item.modelVersion),
                        CommonStyles.SmallGreyCenterLabelStyle
                    );

                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawImageHistoryPreview(Rect rect, TJGeneratorsGenerationHistoryItem item)
        {
            if (item == null || item.isGenerating)
            {
                UIComponents.DrawLoadingSpinner(rect, CommonStyles.SmallGreyLabelStyle, Repaint);
                return;
            }

            if (!string.IsNullOrEmpty(item.modelPath))
            {
                if (
                    historyPreviewCache.TryGetValue(item.modelPath, out var cached)
                    && cached != null
                )
                {
                    GUI.DrawTexture(rect, cached, ScaleMode.ScaleToFit);
                    return;
                }

                var assetTex = AssetDatabase.LoadAssetAtPath<Texture2D>(item.modelPath);
                if (assetTex != null)
                {
                    historyPreviewCache[item.modelPath] = assetTex;
                    GUI.DrawTexture(rect, assetTex, ScaleMode.ScaleToFit);
                    return;
                }

                string absPath = PathUtils.ToAbsoluteAssetPath(item.modelPath);
                if (File.Exists(absPath))
                {
                    // 异步加载本地预览图到缓存，避免OnGUI卡顿
                    EnqueuePreviewLoad(item.modelPath, absPath, false);
                }
            }

            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 1f));
            var iconRect = new Rect(
                rect.x + rect.width / 4,
                rect.y + rect.height / 4,
                rect.width / 2,
                rect.height / 2
            );
            GUI.Label(iconRect, EditorGUIUtility.IconContent("d_Texture2D Icon"));
        }

        private Texture2D GetPreviewTextureForHistoryItem(TJGeneratorsGenerationHistoryItem item)
        {
            if (item == null || item.isGenerating)
                return null;

            if (!string.IsNullOrEmpty(item.modelPath))
            {
                if (
                    historyPreviewCache.TryGetValue(item.modelPath, out var cached)
                    && cached != null
                )
                    return cached;

                var assetTex = AssetDatabase.LoadAssetAtPath<Texture2D>(item.modelPath);
                if (assetTex != null)
                {
                    historyPreviewCache[item.modelPath] = assetTex;
                    return assetTex;
                }

                string absPath = PathUtils.ToAbsoluteAssetPath(item.modelPath);
                if (File.Exists(absPath))
                {
                    EnqueuePreviewLoad(item.modelPath, absPath, false);
                }
            }

            // 可选：如果历史项已经有 URL 预览缓存，也可以复用
            if (
                item.isTextToModel
                && !string.IsNullOrEmpty(item.previewImageUrl)
                && urlPreviewCache.TryGetValue(item.previewImageUrl, out var urlTex)
                && urlTex != null
            )
            {
                return urlTex;
            }

            return null;
        }

        private void DrawHistoryActions()
        {
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();

            GUI.enabled =
                selectedHistoryIndex >= 0 && selectedHistoryIndex < generationHistory.Count;

            if (GUILayout.Button("应用到当前图片", GUILayout.Height(25)))
                ApplyHistoryToImage(selectedHistoryIndex);

            if (GUILayout.Button("在项目中显示", GUILayout.Height(25)))
                ShowHistoryInProject(selectedHistoryIndex);

            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            GUILayout.Label("预览缩放", CommonStyles.SmallGreyLabelStyle, GUILayout.Width(56));
            historyMainPreviewZoom = GUILayout.HorizontalSlider(
                historyMainPreviewZoom,
                1f,
                6f,
                GUILayout.Width(90)
            );

            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            // Frontier-only: cutout/slicing utilities should not leak into generic image UI.
            if (IsFrontierSequenceMode())
            {
                DrawSpriteCutoutAndSliceSection();
                GUILayout.Space(10);
            }
        }

        private static string GetHistoryUserPromptLabel(TJGeneratorsGenerationHistoryItem item)
        {
            if (item == null)
                return "";
            string prompt = item.prompt ?? "";
            const string key = "用户需求：";
            int idx = prompt.LastIndexOf(key, StringComparison.Ordinal);
            string userPrompt = idx >= 0 ? prompt.Substring(idx + key.Length).Trim() : prompt.Trim();
            if (string.IsNullOrEmpty(userPrompt))
                userPrompt = "（无提示词）";
            return userPrompt.Length > 20 ? userPrompt.Substring(0, 17) + "..." : userPrompt;
        }

        private void DrawSpriteCutoutAndSliceSection()
        {
            string activeSourcePath = GetActivePreviewSourcePath();

            GUILayout.BeginVertical("box");
            GUILayout.Label("抠图与切割", CommonStyles.HeaderStyle);

            GUILayout.Space(4);
            GUILayout.Label("导入图片后可直接抠图/切割/生成动画（无需先走 AI 生成）。", CommonStyles.SmallGreyLabelStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("导入本地图片…", GUILayout.Height(22)))
                ImportLocalImageToHistory();

            if (GUILayout.Button("使用当前选中图片", GUILayout.Height(22)))
                AddSelectedProjectTextureToHistory();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            if (string.IsNullOrEmpty(activeSourcePath))
            {
                EditorGUILayout.HelpBox("未找到可处理的图片：请先在右侧历史中选一张，或点击上方“导入本地图片/使用当前选中图片”。", MessageType.Info);
                GUILayout.EndVertical();
                return;
            }

            chromaKeyTolerance = EditorGUILayout.Slider("绿幕容差", chromaKeyTolerance, 0.05f, 0.35f);
            chromaFeather = EditorGUILayout.Slider("边缘羽化", chromaFeather, 0f, 0.3f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("执行抠图并预览", GUILayout.Height(24)))
                ApplyGreenScreenCutoutPreview();
            if (GUILayout.Button("恢复原图预览", GUILayout.Height(24)))
            {
                ResetProcessedPreview();
                Repaint();
            }
            GUILayout.EndHorizontal();

            spriteSliceColumns = Mathf.Max(1, EditorGUILayout.IntField("切割列数", spriteSliceColumns));
            spriteSliceRows = Mathf.Max(1, EditorGUILayout.IntField("切割行数", spriteSliceRows));
            spriteSliceFps = EditorGUILayout.Slider("动画 FPS", spriteSliceFps, 1f, 60f);
            spriteSliceLoop = EditorGUILayout.ToggleLeft("动画循环 (Loop)", spriteSliceLoop);
            EditorGUILayout.HelpBox("预览图中的红色线条为切割线。", MessageType.Info);

            if (GUILayout.Button("切割并导出为 Sprite", GUILayout.Height(28)))
                SliceSelectedHistoryToSprites();

            GUILayout.EndVertical();
        }

        private void ImportLocalImageToHistory()
        {
            string file = EditorUtility.OpenFilePanel("选择要导入的图片", "", "png,jpg,jpeg");
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
                return;

            if (!AssetDatabase.IsValidFolder("Assets/TJGenerators"))
                AssetDatabase.CreateFolder("Assets", "TJGenerators");
            if (!AssetDatabase.IsValidFolder("Assets/TJGenerators/Imported"))
                AssetDatabase.CreateFolder("Assets/TJGenerators", "Imported");

            string ext = Path.GetExtension(file);
            if (string.IsNullOrEmpty(ext))
                ext = ".png";
            ext = ext.ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                ext = ".png";

            string baseName = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrEmpty(baseName))
                baseName = "ImportedImage";

            string dstAssetPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/TJGenerators/Imported/{baseName}{ext}");
            string dstAbs = PathUtils.ToAbsoluteAssetPath(dstAssetPath);
            try
            {
                string dir = Path.GetDirectoryName(dstAbs);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.Copy(file, dstAbs, true);
            }
            catch (Exception e)
            {
                ErrorDialogUtils.ShowErrorDialog("导入失败", e.Message, LogTag);
                return;
            }

            AssetDatabase.ImportAsset(dstAssetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(dstAssetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            AddImageAssetPathToHistoryAndSelect(dstAssetPath);
            Repaint();
        }

        private void AddSelectedProjectTextureToHistory()
        {
            var tex = Selection.activeObject as Texture2D;
            if (tex == null)
            {
                Debug.LogWarning($"{LogTag} 请先在 Project 里选中一张 Texture2D 图片（png/jpg）。");
                return;
            }

            string p = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(p))
            {
                Debug.LogWarning($"{LogTag} 无法获取选中图片的路径。");
                return;
            }

            p = p.Replace('\\', '/');
            if (!p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                && !p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                && !p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"{LogTag} 当前仅支持 png/jpg/jpeg。");
                return;
            }

            AddImageAssetPathToHistoryAndSelect(p);
            Repaint();
        }

        private void AddImageAssetPathToHistoryAndSelect(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            assetPath = assetPath.Replace('\\', '/');
            if (!File.Exists(PathUtils.ToAbsoluteAssetPath(assetPath)))
            {
                ErrorDialogUtils.ShowErrorDialog("错误", "图片文件不存在，无法加入历史。", LogTag);
                return;
            }

            EnsureHistorySelectionAndFallback();

            // 去重：已在历史中则直接选中
            for (int i = 0; i < generationHistory.Count; i++)
            {
                var it = generationHistory[i];
                if (it != null && !string.IsNullOrEmpty(it.modelPath)
                    && string.Equals(it.modelPath.Replace('\\', '/'), assetPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (selectedHistoryIndex != i)
                        ResetProcessedPreview();
                    selectedHistoryIndex = i;
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                    return;
                }
            }

            generationHistory.Insert(0, new TJGeneratorsGenerationHistoryItem
            {
                modelPath = assetPath,
                isGenerating = false,
                prompt = "（手动导入图片）",
                modelVersion = "manual_import"
            });
            ResetProcessedPreview();
            selectedHistoryIndex = 0;

            var assetObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (assetObj != null)
            {
                Selection.activeObject = assetObj;
                EditorGUIUtility.PingObject(assetObj);
            }
        }

        private void ShowHistoryContextMenu(int index)
        {
            if (index < 0 || index >= generationHistory.Count)
                return;
            var item = generationHistory[index];
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("应用到当前图片"), false, () => ApplyHistoryToImage(index));
            menu.AddItem(new GUIContent("在项目中显示"), false, () => ShowHistoryInProject(index));

            if (CanGenerateTerrainFromHistoryItem(item))
                menu.AddItem(
                    new GUIContent("一键生成地形"),
                    false,
                    () => GenerateTerrainFromHeightmap(index)
                );

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
                        GetCurrentImageAssetGuid()
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

        private void ApplyHistoryToImage(int index)
        {
            if (index < 0 || index >= generationHistory.Count)
                return;
            var item = generationHistory[index];

            if (item.isGenerating)
            {
                Debug.LogWarning($"{LogTag} 请等待该条生成完成后再应用。");
                return;
            }

            if (
                string.IsNullOrEmpty(item.modelPath)
                || !File.Exists(PathUtils.ToAbsoluteAssetPath(item.modelPath))
            )
            {
                ErrorDialogUtils.ShowErrorDialog("错误", "该历史记录的图片文件不存在。", LogTag);
                if (!string.IsNullOrEmpty(item.modelPath))
                    TJGeneratorsHistoryManager.RemoveFromHistory(item.modelPath);
                generationHistory = TJGeneratorsHistoryManager.LoadHistoryForAsset(
                    GetCurrentImageAssetGuid()
                );
                selectedHistoryIndex = generationHistory.Count > 0 ? 0 : -1;
                Repaint();
                return;
            }

            if (targetImageAsset == null || !targetImageAsset.IsValid())
            {
                Debug.LogWarning($"{LogTag} 请先绑定或创建目标图片资产。");
                return;
            }

            string targetPath = targetImageAsset.GetPath();
            if (
                !targetPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                && !targetPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                && !targetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            )
            {
                ErrorDialogUtils.ShowErrorDialog(
                    "错误",
                    "目标图片资产只支持 .jpg / .jpeg / .png。",
                    LogTag
                );
                return;
            }

            if (
                !EditorUtility.DisplayDialog(
                    "确认替换",
                    $"确定要将选中的历史应用到 {Path.GetFileName(targetPath)} 吗？",
                    "确定",
                    "取消"
                )
            )
            {
                return;
            }

            try
            {
                string srcAbsolute = PathUtils.ToAbsoluteAssetPath(item.modelPath);
                string dstAbsolute = PathUtils.ToAbsoluteAssetPath(targetPath);
                ReleaseTextureHandlesForTargetOverwrite(targetPath);
                File.Copy(srcAbsolute, dstAbsolute, true);

                AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);

                var importer = AssetImporter.GetAtPath(targetPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.SaveAndReimport();
                }

                // 从 AssetDatabase 重新加载，避免残留对已覆盖文件的引用（与精灵窗口一致）
                imagePreviewTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(targetPath);
                if (imagePreviewTexture != null)
                {
                    Selection.activeObject = imagePreviewTexture;
                    EditorGUIUtility.PingObject(imagePreviewTexture);
                }

                TJGeneratorsGenerationLabel.EnableLabel(targetImageAsset);
            }
            catch (Exception e)
            {
                ErrorDialogUtils.ShowErrorDialog("错误", "应用失败: " + e.Message, LogTag);
            }

            Repaint();
        }

        /// <summary>
        /// 覆盖目标纹理资产前释放本窗口持有的引用，避免 Windows 下文件仍被 Unity/预览占用导致 File.Copy 失败。
        /// 行为对齐 <see cref="TJGeneratorsSpriteWindow"/> 在应用历史前对 <c>spritePreviewTexture</c> 的处理。
        /// </summary>
        private void ReleaseTextureHandlesForTargetOverwrite(string targetAssetPath)
        {
            if (string.IsNullOrEmpty(targetAssetPath))
                return;

            targetAssetPath = targetAssetPath.Replace('\\', '/');

            if (
                processedPreviewValid
                && !string.IsNullOrEmpty(processedPreviewSourcePath)
                && string.Equals(
                    processedPreviewSourcePath.Replace('\\', '/'),
                    targetAssetPath,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                ResetProcessedPreview();
            }

            if (imagePreviewTexture != null)
            {
                string pt = AssetDatabase.GetAssetPath(imagePreviewTexture).Replace('\\', '/');
                if (string.Equals(pt, targetAssetPath, StringComparison.OrdinalIgnoreCase))
                    imagePreviewTexture = null;
            }

            var keysToRemove = new List<string>();
            foreach (var kv in historyPreviewCache)
            {
                if (string.Equals(kv.Key.Replace('\\', '/'), targetAssetPath, StringComparison.OrdinalIgnoreCase))
                {
                    keysToRemove.Add(kv.Key);
                    continue;
                }

                if (kv.Value == null)
                    continue;
                string cachedPath = AssetDatabase.GetAssetPath(kv.Value).Replace('\\', '/');
                if (string.Equals(cachedPath, targetAssetPath, StringComparison.OrdinalIgnoreCase))
                    keysToRemove.Add(kv.Key);
            }

            foreach (var k in keysToRemove)
                historyPreviewCache.Remove(k);
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

        private void ApplyGreenScreenCutoutPreview()
        {
            string sourcePath = GetActivePreviewSourcePath();
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogWarning($"{LogTag} 未找到可处理的预览图，请先生成或选中一张历史图片。");
                return;
            }

            Texture2D src = SpriteSequencePostProcessService.LoadReadableTextureFromAssetPath(sourcePath);
            if (src == null)
            {
                ErrorDialogUtils.ShowErrorDialog("错误", "无法读取选中的历史图片。", LogTag);
                return;
            }

            try
            {
                Texture2D cutout = SpriteSequencePostProcessService.BuildGreenScreenCutoutTexture(src, chromaKeyTolerance, chromaFeather);
                ReplaceProcessedPreview(cutout, sourcePath);
                Repaint();
            }
            finally
            {
                DestroyImmediate(src);
            }
        }

        private void SliceSelectedHistoryToSprites()
        {
            string sourcePath = GetActivePreviewSourcePath();
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogWarning($"{LogTag} 未找到可切割的图片，请先生成或选中一张历史图片。");
                return;
            }

            Texture2D sourceTexture = processedPreviewValid && processedPreviewTexture != null && string.Equals(processedPreviewSourcePath, sourcePath, StringComparison.OrdinalIgnoreCase)
                ? processedPreviewTexture
                : LoadReadableTextureFromAssetPath(sourcePath);

            if (sourceTexture == null)
            {
                ErrorDialogUtils.ShowErrorDialog("错误", "无法读取选中的历史图片。", LogTag);
                return;
            }

            bool shouldDestroyLoaded = sourceTexture != processedPreviewTexture;
            try
            {
                var sliceResult = SpriteSequencePostProcessService.SliceTextureToSpritesAndAnimation(
                    sourceTexture,
                    sourcePath,
                    spriteSliceColumns,
                    spriteSliceRows,
                    spriteSliceFps,
                    spriteSliceLoop
                );
                string outputDir = sliceResult.OutputDirectory;
                int exported = sliceResult.ExportedCount;
                string clipPath = sliceResult.AnimationClipPath;
                string msg = string.IsNullOrEmpty(clipPath)
                    ? $"已导出 {exported} 张 Sprite。\n路径：{outputDir}"
                    : $"已导出 {exported} 张 Sprite，并创建动画文件。\nSprite路径：{outputDir}\n动画路径：{clipPath}";
                Debug.Log($"{LogTag} 切割完成：\n{msg}");
                EditorGUIUtility.PingObject(
                    !string.IsNullOrEmpty(clipPath)
                        ? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(clipPath)
                        : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outputDir)
                );
            }
            finally
            {
                if (shouldDestroyLoaded)
                    DestroyImmediate(sourceTexture);
            }
        }

        private string GetSelectedHistoryModelPath()
        {
            if (selectedHistoryIndex < 0 || selectedHistoryIndex >= generationHistory.Count)
                return null;
            var item = generationHistory[selectedHistoryIndex];
            if (item == null || string.IsNullOrEmpty(item.modelPath))
                return null;
            return item.modelPath.Replace('\\', '/');
        }

        private string GetActivePreviewSourcePath()
        {
            string historyPath = GetSelectedHistoryModelPath();
            if (!string.IsNullOrEmpty(historyPath))
                return historyPath;

            if (targetImageAsset != null && targetImageAsset.IsValid())
            {
                string p = targetImageAsset.GetPath();
                if (!string.IsNullOrEmpty(p))
                    return p.Replace('\\', '/');
            }

            return null;
        }

        private static Texture2D LoadReadableTextureFromAssetPath(string assetPath)
        {
            string abs = PathUtils.ToAbsoluteAssetPath(assetPath);
            if (string.IsNullOrEmpty(abs) || !File.Exists(abs))
                return null;
            byte[] bytes = File.ReadAllBytes(abs);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                DestroyImmediate(tex);
                return null;
            }
            return tex;
        }

        private static Texture2D BuildGreenScreenCutoutTexture(Texture2D src, float tolerance, float feather)
        {
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            Color[] pixels = src.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                Color.RGBToHSV(c, out float h, out float s, out float v);

                float hueDist = Mathf.Abs(h - (1f / 3f));
                hueDist = Mathf.Min(hueDist, 1f - hueDist);
                float hueGate = Mathf.Clamp01(1f - hueDist / Mathf.Lerp(0.22f, 0.08f, Mathf.Clamp01(tolerance * 2f)));
                float satGate = Mathf.Clamp01((s - 0.12f) / 0.35f);
                float lumGate = Mathf.Clamp01((v - 0.08f) / 0.25f);
                float dominance = Mathf.Clamp01((c.g - Mathf.Max(c.r, c.b) - 0.01f) / Mathf.Max(0.02f, tolerance));
                float similarity = 1f - Vector3.Distance(new Vector3(c.r, c.g, c.b), new Vector3(0f, 1f, 0f)) / 1.73205f;
                similarity = Mathf.Clamp01(similarity);

                float key = hueGate * satGate * lumGate * dominance * similarity;
                float soften = Mathf.Max(0.001f, feather * 2f + 0.015f);
                key = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((key - 0.08f) / soften));

                c.a *= (1f - key);
                if (c.a < 0.001f)
                {
                    c.a = 0f;
                }
                else
                {
                    // 轻量去绿边：半透明边缘处压低过量绿通道，减少绿色描边
                    float maxRb = Mathf.Max(c.r, c.b);
                    float despill = key * 0.7f * Mathf.Clamp01(1f - c.a);
                    c.g = Mathf.Lerp(c.g, maxRb, despill);
                }
                pixels[i] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void ReplaceProcessedPreview(Texture2D newTexture, string sourcePath)
        {
            ResetProcessedPreview();
            processedPreviewTexture = newTexture;
            processedPreviewSourcePath = sourcePath;
            processedPreviewValid = processedPreviewTexture != null;
        }

        private void ResetProcessedPreview()
        {
            if (processedPreviewTexture != null)
                DestroyImmediate(processedPreviewTexture);
            processedPreviewTexture = null;
            processedPreviewSourcePath = null;
            processedPreviewValid = false;
        }

        private static string CreateSpriteSliceOutputFolder(string sourceAssetPath)
        {
            string sourceDir = Path.GetDirectoryName(sourceAssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(sourceDir))
                sourceDir = "Assets/TJGenerators";

            string sourceName = Path.GetFileNameWithoutExtension(sourceAssetPath);
            string folderName = $"{sourceName}_slices_{DateTime.Now:yyyyMMdd_HHmmss}";
            string baseFolder = $"{sourceDir}/{folderName}";
            string unique = AssetDatabase.GenerateUniqueAssetPath(baseFolder);
            EnsureAssetFolder(unique);
            return unique;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            string normalized = folderPath.Replace("\\", "/").TrimEnd('/');
            string[] parts = normalized.Split('/');
            if (parts.Length == 0)
                return;
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string CreateSpriteSequenceAnimationClip(
            string outputDir,
            List<string> spriteAssetPaths,
            float fps,
            bool loop
        )
        {
            if (string.IsNullOrEmpty(outputDir) || spriteAssetPaths == null || spriteAssetPaths.Count == 0)
                return null;

            var sprites = new List<Sprite>();
            for (int i = 0; i < spriteAssetPaths.Count; i++)
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(spriteAssetPaths[i]);
                if (sp != null)
                    sprites.Add(sp);
            }
            if (sprites.Count == 0)
                return null;

            string clipPath = AssetDatabase.GenerateUniqueAssetPath($"{outputDir}/sprite_sequence.anim");
            var clip = new AnimationClip
            {
                frameRate = Mathf.Max(1f, fps)
            };

            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

            var keys = new ObjectReferenceKeyframe[sprites.Count];
            float invFps = 1f / Mathf.Max(1f, clip.frameRate);
            for (int i = 0; i < sprites.Count; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i * invFps,
                    value = sprites[i]
                };
            }
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            // 通过序列化方式设置循环选项
            var so = new SerializedObject(clip);
            var settings = so.FindProperty("m_AnimationClipSettings");
            if (settings != null)
            {
                var loopProp = settings.FindPropertyRelative("m_LoopTime");
                if (loopProp != null)
                    loopProp.boolValue = loop;
                so.ApplyModifiedProperties();
            }

            AssetDatabase.CreateAsset(clip, clipPath);
            AssetDatabase.ImportAsset(clipPath, ImportAssetOptions.ForceUpdate);
            return clipPath;
        }

        /// <summary>
        /// Frontier 序列帧模式：必须能从 FrontierSequenceProfiles.json 解析出非空 instructions，否则禁止提交生成。
        /// </summary>
        private bool TryValidateFrontierSequenceInstructionsForGeneration(out string failureMessage)
        {
            failureMessage = null;
            if (!IsFrontierSequenceMode())
                return true;

            LoadFrontierSequenceProfilesIfNeeded();
            if (frontierSequenceProfilesRoot == null)
            {
                failureMessage =
                    "未找到或无法读取 FrontierSequenceProfiles.json。\n\n请确认包内含 Editor/Config/FrontierSequenceProfiles.json，或通过 Package Manager 正确安装 cn.tuanjie.ai.generators。";
                return false;
            }

            if (string.IsNullOrEmpty(frontierSequenceProfileId))
            {
                failureMessage =
                    "序列帧模板不可用：请检查 FrontierSequenceProfiles.json 中的 profiles 与 defaultProfileId 是否有效。";
                return false;
            }

            string envelopeRaw = BuildFrontierSequenceEnvelopeRawFromSelectedProfile(referenceImagePaths);
            if (string.IsNullOrEmpty(envelopeRaw))
            {
                failureMessage = "无法根据当前模板构建序列帧指令包（frontier_sequence_envelope），请检查配置文件。";
                return false;
            }

            string instructions = ExtractInstructionsFromEnvelopeRaw(envelopeRaw);
            if (string.IsNullOrWhiteSpace(instructions))
            {
                failureMessage =
                    "当前 profile 的 instructions 为空或缺失，请编辑 FrontierSequenceProfiles.json 填写完整指令。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// profile 中声明的 knowledge 布局参考：必须在磁盘可读，且必须出现在本次合并后的上传路径列表中（与用户图去重后仍须能覆盖到布局文件）。
        /// </summary>
        private bool TryValidateFrontierKnowledgeForSubmit(List<string> mergedAbsolutePaths, out string failureMessage)
        {
            failureMessage = null;
            if (!IsFrontierSequenceMode())
                return true;

            var expectedRel = GetKnowledgeLocalImagePathsFromSelectedProfile();
            if (expectedRel == null || expectedRel.Count == 0)
                return true;

            var mergedFull = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in mergedAbsolutePaths)
            {
                if (string.IsNullOrEmpty(p))
                    continue;
                try
                {
                    mergedFull.Add(Path.GetFullPath(p));
                }
                catch
                {
                    mergedFull.Add(p);
                }
            }

            var missingOnDisk = new List<string>();
            var missingInMerge = new List<string>();

            foreach (var rel in expectedRel)
            {
                string abs = NormalizeToAbsoluteImagePath(rel);
                if (string.IsNullOrEmpty(abs) || !File.Exists(abs))
                {
                    missingOnDisk.Add(rel);
                    continue;
                }

                string full;
                try
                {
                    full = Path.GetFullPath(abs);
                }
                catch
                {
                    full = abs;
                }

                if (!mergedFull.Contains(full))
                    missingInMerge.Add($"{rel} → {full}");
            }

            if (missingOnDisk.Count > 0)
            {
                failureMessage =
                    "布局参考图（knowledge）无法读取，已阻止提交（避免请求不带布局参考）。\n\n"
                    + BuildFrontierKnowledgeDiagnosticText(missingOnDisk);
                return false;
            }

            if (missingInMerge.Count > 0)
            {
                failureMessage =
                    "布局参考图未进入本次上传列表，已阻止提交。\n"
                    + string.Join("\n", missingInMerge);
                return false;
            }

            return true;
        }

        private static string BuildFrontierKnowledgeDiagnosticText(IReadOnlyList<string> missingRelativePaths)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("请核对包 cn.tuanjie.ai.generators 内路径及本机文件是否存在。");
            string pkg = PathUtils.TryGetTjGeneratorsPackageRoot();
            if (!string.IsNullOrEmpty(pkg))
                sb.AppendLine($"包根目录（PackageInfo.resolvedPath）：\n{pkg}\n");

            foreach (var rel in missingRelativePaths)
            {
                sb.AppendLine($"配置中的路径：{rel}");
                string abs = NormalizeToAbsoluteImagePath(rel);
                sb.AppendLine($"解析后的绝对路径：{(string.IsNullOrEmpty(abs) ? "(解析失败)" : abs)}");
                if (string.IsNullOrEmpty(abs))
                {
                    sb.AppendLine();
                    continue;
                }

                if (File.Exists(abs))
                    sb.AppendLine("说明：文件在磁盘上存在，但未通过校验（请重试或检查路径大小写）。");
                else
                {
                    string dir = Path.GetDirectoryName(abs);
                    sb.AppendLine("说明：该文件在磁盘上不存在。");
                    if (!string.IsNullOrEmpty(dir))
                    {
                        sb.AppendLine($"期望所在目录：{dir}");
                        if (Directory.Exists(dir))
                        {
                            try
                            {
                                var files = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly);
                                int n = Math.Min(files.Length, 20);
                                sb.AppendLine($"上述目录内现有文件（最多列 20 个）：");
                                for (int i = 0; i < n; i++)
                                    sb.AppendLine($"  • {Path.GetFileName(files[i])}");
                                if (files.Length > 20)
                                    sb.AppendLine("  …");
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                        else
                        {
                            sb.AppendLine("上述目录不存在（包可能未完整导入或路径错误）。");
                        }
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        // ========== 生成 ==========
        private void StartGeneration()
        {
            if (string.IsNullOrWhiteSpace(textPrompt))
            {
                ErrorDialogUtils.ShowErrorDialog("错误", "请输入文本提示词。", LogTag);
                return;
            }

            int frontierUserRefCount = 0;
            List<string> effectiveReferencePaths;

            // 必须先加载 profile；指令校验也依赖 profile。
            if (IsFrontierSequenceMode())
            {
                LoadFrontierSequenceProfilesIfNeeded();
                if (!TryValidateFrontierSequenceInstructionsForGeneration(out string frontierInstrFail))
                {
                    ErrorDialogUtils.ShowErrorDialog("缺少序列帧指令配置", frontierInstrFail, LogTag);
                    return;
                }
            }

            if (IsFrontierSequenceMode())
            {
                var split = BuildEffectiveReferenceImagePathsWithUserCount(referenceImagePaths);
                effectiveReferencePaths = split.paths;
                frontierUserRefCount = split.userImageCount;
            }
            else
            {
                effectiveReferencePaths = new List<string>(referenceImagePaths);
            }

            bool hasImage = effectiveReferencePaths.Count > 0;

            if (_currentGenerator == null)
            {
                ErrorDialogUtils.ShowErrorDialog("错误", "未选择可用的生成模型。", LogTag);
                return;
            }

            if (IsFrontierSequenceMode() && !TryValidateFrontierKnowledgeForSubmit(effectiveReferencePaths, out string knowledgeFail))
            {
                ErrorDialogUtils.ShowErrorDialog("布局参考图（knowledge）未就绪", knowledgeFail, LogTag);
                return;
            }

            EnsureTargetImage();

            isGenerating = true;
            generationStatus = "准备中...";
            generationProgress = 0f;

            if (_currentGenerator is DynamicGenerator dynamicGen)
            {
                dynamicGen.SetParameter("isSegmentation", false);
                dynamicGen.ClearExtraRawJsonFields();

                string finalPrompt = textPrompt.Trim();
                if (IsFrontierSequenceMode())
                {
                    dynamicGen.SetPromptTemplateSelection(null);
                    string envelopeRaw = BuildFrontierSequenceEnvelopeRawFromSelectedProfile(referenceImagePaths);
                    if (!string.IsNullOrEmpty(envelopeRaw))
                    {
                        dynamicGen.SetExtraRawJsonField("frontier_sequence_envelope", envelopeRaw);
                        string instructions = ExtractInstructionsFromEnvelopeRaw(envelopeRaw);
                        if (!string.IsNullOrEmpty(instructions))
                            finalPrompt = BuildPromptWithInstructionsFallback(finalPrompt, instructions);

                        if (effectiveReferencePaths.Count > 0)
                            finalPrompt = FrontierSequenceImageOrderHint.AppendToPrompt(
                                finalPrompt,
                                effectiveReferencePaths.Count,
                                frontierUserRefCount
                            );
                    }
                }
                else
                {
                    dynamicGen.SetPromptTemplateSelection(selectedPromptTemplate);
                }
                dynamicGen.SetTextPrompt(finalPrompt);
                dynamicGen.SetImagePaths(hasImage ? effectiveReferencePaths : null);
            }

            string assetGuid = targetImageAsset?.guid ?? "";
            EditorCoroutineUtility.StartCoroutineOwnerless(
                _pipeline.StartGeneration(_currentGenerator, assetGuid)
            );
        }

        // ========== IGenerationPipelineHost ==========
        public TJGeneratorsAssetReference GetTargetAsset() => targetImageAsset;

        public void StartGeneration(ModelGeneratorBase generator)
        {
            if (generator == _currentGenerator)
                StartGeneration();
        }

        public void RefreshHistory()
        {
            generationHistory = TJGeneratorsHistoryManager.LoadHistoryForAsset(
                GetCurrentImageAssetGuid()
            );
            selectedHistoryIndex = generationHistory.Count > 0 ? 0 : -1;
            Repaint();
        }

        public void ShowPreviewModel(string assetPath)
        {
            // 图片窗口不需要 3D/Prefab 预览
        }

        public string GetTextureSavePath(ModelGeneratorBase generator)
        {
            if (!AssetDatabase.IsValidFolder("Assets/TJGenerators"))
                AssetDatabase.CreateFolder("Assets", "TJGenerators");
            if (!AssetDatabase.IsValidFolder("Assets/TJGenerators/History"))
                AssetDatabase.CreateFolder("Assets/TJGenerators", "History");

            // 先用 .jpg 占位符路径（主图 savePath 会保留这个扩展名）
            string uniqueName = "Image_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg";
            return AssetDatabase.GenerateUniqueAssetPath(
                "Assets/TJGenerators/History/" + uniqueName
            );
        }

        public void OnTextureSaved(string savePath, ModelGeneratorBase generator)
        {
            // 地形高度图后处理改为「一键生成地形」时执行，此处仅保留后端原图

            // 设置导入器
            var textureImporter = AssetImporter.GetAtPath(savePath) as TextureImporter;
            if (textureImporter != null)
            {
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.SaveAndReimport();
            }

            TJGeneratorsGenerationLabel.EnableLabel(TJGeneratorsAssetReference.FromPath(savePath));

            // 3) 同步更新绑定资产（并刷新预览）
            string desiredExt = Path.GetExtension(savePath).ToLowerInvariant();
            EnsureTargetImage(desiredExt);
            string targetPath = targetImageAsset.GetPath();
            if (
                targetPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || targetPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                || targetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            )
            {
                try
                {
                    ReleaseTextureHandlesForTargetOverwrite(targetPath);
                    File.Copy(
                        PathUtils.ToAbsoluteAssetPath(savePath),
                        PathUtils.ToAbsoluteAssetPath(targetPath),
                        true
                    );
                    AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);

                    var targetImporter = AssetImporter.GetAtPath(targetPath) as TextureImporter;
                    if (targetImporter != null)
                    {
                        targetImporter.textureType = TextureImporterType.Default;
                        targetImporter.SaveAndReimport();
                    }
                }
                catch (Exception e)
                {
                    TJLog.LogWarning($"{LogTag} 复制到目标图片失败: {e.Message}");
                }
            }

            imagePreviewTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(targetPath);
            if (imagePreviewTexture != null)
            {
                Selection.activeObject = imagePreviewTexture;
                EditorGUIUtility.PingObject(imagePreviewTexture);
            }

            TJGeneratorsGenerationLabel.EnableLabel(targetImageAsset);

            // 4) 更新生成状态（历史刷新由 GenerationPipeline 在 CompletePlaceholder 后统一处理）
            generationStatus = "完成";
            generationProgress = 1f;
            isGenerating = false;
        }

        public string GetAudioSavePath(ModelGeneratorBase generator) => null;

        public void OnAudioSaved(string savePath, ModelGeneratorBase generator) { }

        public string GetVideoSavePath(ModelGeneratorBase generator) => null;
        public void OnVideoSaved(string savePath, ModelGeneratorBase generator) { }

        void IGenerationPipelineHost.Repaint()
        {
            Repaint();
        }

        /// <summary>
        /// 允许一键生成：已完成、本地 PNG 存在；且（历史里保存了地形模板 id，或当前窗口正选中地形高度图模板）。
        /// 避免仅依赖 <see cref="TJGeneratorsGenerationHistoryItem.promptTemplateId"/>（旧历史或序列化前记录为空时按钮长期灰色）。
        /// </summary>
        private bool CanGenerateTerrainFromHistoryItem(TJGeneratorsGenerationHistoryItem item)
        {
            if (item == null || item.isGenerating || string.IsNullOrEmpty(item.modelPath))
                return false;
            if (!item.modelPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!File.Exists(PathUtils.ToAbsoluteAssetPath(item.modelPath)))
                return false;

            if (
                string.Equals(
                    item.promptTemplateId,
                    UnityTerrainHeightmapTemplateId,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                return true;

            return IsUnityTerrainHeightmapTemplateSelected();
        }

        /// <summary>
        /// 复制历史中的原始高度图 → 后处理写入单独 PNG → 按 PNG 宽高设置 Terrain 世界尺寸并创建场景地形。
        /// </summary>
        private void GenerateTerrainFromHeightmap(int historyIndex)
        {
            if (historyIndex < 0 || historyIndex >= generationHistory.Count)
                return;

            var item = generationHistory[historyIndex];
            if (!CanGenerateTerrainFromHistoryItem(item))
            {
                ErrorDialogUtils.ShowErrorDialog(
                    "无法生成地形",
                    "请选择由「Unity 地形高度图」模板生成且已完成的 PNG 历史记录。",
                    LogTag
                );
                return;
            }

            var hmOpts = new TerrainHeightmapPostProcessOptions
            {
                median3x3 = terrainHeightmapMedian3x3,
                gaussianBlur = terrainHeightmapGaussianBlur,
                gaussianSigma = terrainHeightmapBlurSigma,
                percentileNormalization = terrainHeightmapPercentileNormalize,
                percentileLow = terrainHeightmapPercentileLow,
                percentileHigh = terrainHeightmapPercentileHigh,
                heightGamma = terrainHeightmapHeightGamma,
                remapOutputMin = terrainHeightmapRemapOutMin,
                remapOutputMax = Mathf.Max(
                    terrainHeightmapRemapOutMax,
                    terrainHeightmapRemapOutMin + 0.01f
                ),
            };

            var (_, _, _, error) = TerrainCreationUtils.PostProcessAndCreateTerrain(
                item.modelPath, hmOpts);

            if (!string.IsNullOrEmpty(error))
                ErrorDialogUtils.ShowErrorDialog("地形生成失败", error, LogTag);

            Repaint();
        }

        // ========== 辅助方法 ==========
        private string GetCurrentImageAssetGuid() => targetImageAsset?.guid ?? "";

        private void OnModelSelected(AIModelInfo model) => OnModelSelectedBase(model);

        private void ApplyForcedGeneratorFilterIfNeeded()
        {
            if (string.IsNullOrEmpty(forcedGeneratorId) || _generators == null || _generators.Count == 0)
                return;

            var filtered = new List<ModelGeneratorBase>();
            for (int i = 0; i < _generators.Count; i++)
            {
                var g = _generators[i];
                if (string.Equals(g.GeneratorId, forcedGeneratorId, StringComparison.OrdinalIgnoreCase))
                    filtered.Add(g);
            }
            if (filtered.Count == 0)
                return;

            _generators = filtered;
            _currentGeneratorIndex = 0;
            _currentGenerator = _generators[0];
            currentSelectedModel = BuildModelInfoFromGenerator(_currentGenerator);
            EnsureWindowTitle();
        }

        private void EnsureWindowTitle()
        {
            if (titleContent != null && !string.IsNullOrEmpty(titleContent.text))
                return;

            string title = string.IsNullOrEmpty(forcedGeneratorId)
                ? "TJGenerators 图片生成"
                : "TJGenerators 序列帧（Frontier）";
            titleContent = new GUIContent(title);
        }

        private void LoadFrontierSequenceProfilesIfNeeded()
        {
            frontierSequenceProfilesRoot = null;
            frontierSequenceResolvedConfigPath = null;
            frontierSequenceProfileIds.Clear();
            frontierSequenceProfileNames.Clear();
            frontierSequenceProfileIndex = 0;

            if (!IsFrontierSequenceMode())
            {
                frontierSequenceProfileId = null;
                return;
            }

            try
            {
                if (!FrontierSequenceProfileConfigLoader.TryLoad(out frontierSequenceProfilesRoot, out frontierSequenceResolvedConfigPath)
                    || frontierSequenceProfilesRoot == null)
                    return;
                var profiles = frontierSequenceProfilesRoot["profiles"] as JArray;
                if (profiles == null || profiles.Count == 0)
                    return;

                foreach (var token in profiles)
                {
                    if (token is not JObject item)
                        continue;
                    string id = item["id"]?.ToString();
                    if (string.IsNullOrEmpty(id))
                        continue;
                    string name = item["name"]?.ToString();
                    frontierSequenceProfileIds.Add(id);
                    frontierSequenceProfileNames.Add(string.IsNullOrEmpty(name) ? id : $"{name} ({id})");
                }

                if (frontierSequenceProfileIds.Count == 0)
                    return;

                string defaultId = frontierSequenceProfilesRoot["defaultProfileId"]?.ToString();
                string targetId = !string.IsNullOrEmpty(frontierSequenceProfileId) ? frontierSequenceProfileId : defaultId;
                int idx = !string.IsNullOrEmpty(targetId)
                    ? frontierSequenceProfileIds.FindIndex(x => string.Equals(x, targetId, StringComparison.OrdinalIgnoreCase))
                    : -1;
                frontierSequenceProfileIndex = idx >= 0 ? idx : 0;
                frontierSequenceProfileId = frontierSequenceProfileIds[frontierSequenceProfileIndex];
            }
            catch (Exception e)
            {
                TJLog.LogWarning($"{LogTag} 读取序列帧模板配置失败: {e.Message}");
            }
        }

        private string BuildFrontierSequenceEnvelopeRawFromSelectedProfile(List<string> userReferenceImagePaths)
        {
            if (!IsFrontierSequenceMode() || frontierSequenceProfilesRoot == null || string.IsNullOrEmpty(frontierSequenceProfileId))
                return null;

            var profiles = frontierSequenceProfilesRoot["profiles"] as JArray;
            if (profiles == null)
                return null;

            JObject profile = null;
            foreach (var token in profiles)
            {
                if (token is not JObject item)
                    continue;
                if (string.Equals(item["id"]?.ToString(), frontierSequenceProfileId, StringComparison.OrdinalIgnoreCase))
                {
                    profile = item;
                    break;
                }
            }
            if (profile == null)
                return null;

            var envelope = new JObject
            {
                ["instructions"] = profile["instructions"]?.ToString() ?? "",
                ["knowledge_refs"] = profile["knowledge_refs"] is JArray refs
                    ? (JArray)refs.DeepClone()
                    : new JArray(),
                // 明确声明双通道：用户上传图与 knowledge 图分离，避免角色身份被 knowledge 覆盖
                ["reference_channel_policy"] = new JObject
                {
                    ["user_reference_channel"] = "imageUrls",
                    ["knowledge_reference_channel"] = "frontier_sequence_envelope.knowledge_refs",
                    ["identity_priority"] = "user_reference_first",
                    ["knowledge_usage"] = "style_or_motion_only"
                },
                ["user_reference_refs"] = BuildUserReferenceRefs(userReferenceImagePaths)
            };
            return envelope.ToString();
        }

        private List<string> BuildEffectiveReferenceImagePaths(List<string> userReferenceImagePaths)
        {
            return BuildEffectiveReferenceImagePathsWithUserCount(userReferenceImagePaths).paths;
        }

        /// <summary>
        /// 合并用户参考图与 profile 内 knowledge 本地图；<paramref name="userImageCount"/> 为「前半段」张数（用户上传），后半段为布局参考。
        /// </summary>
        private (List<string> paths, int userImageCount) BuildEffectiveReferenceImagePathsWithUserCount(
            List<string> userReferenceImagePaths
        )
        {
            var merged = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int userImageCount = 0;

            if (userReferenceImagePaths != null)
            {
                for (int i = 0; i < userReferenceImagePaths.Count; i++)
                {
                    string p = NormalizeToAbsoluteImagePath(userReferenceImagePaths[i]);
                    if (string.IsNullOrEmpty(p) || !File.Exists(p) || !seen.Add(p))
                        continue;
                    merged.Add(p);
                    userImageCount++;
                }
            }

            var knowledgePaths = GetKnowledgeLocalImagePathsFromSelectedProfile();
            for (int i = 0; i < knowledgePaths.Count; i++)
            {
                string p = NormalizeToAbsoluteImagePath(knowledgePaths[i]);
                if (string.IsNullOrEmpty(p) || !File.Exists(p) || !seen.Add(p))
                    continue;
                merged.Add(p);
            }

            return (merged, userImageCount);
        }

        private List<string> GetKnowledgeLocalImagePathsFromSelectedProfile()
        {
            var paths = new List<string>();
            if (!IsFrontierSequenceMode() || frontierSequenceProfilesRoot == null || string.IsNullOrEmpty(frontierSequenceProfileId))
                return paths;

            var profiles = frontierSequenceProfilesRoot["profiles"] as JArray;
            if (profiles == null)
                return paths;

            JObject profile = null;
            foreach (var token in profiles)
            {
                if (token is not JObject item)
                    continue;
                if (string.Equals(item["id"]?.ToString(), frontierSequenceProfileId, StringComparison.OrdinalIgnoreCase))
                {
                    profile = item;
                    break;
                }
            }

            if (profile?["knowledge_refs"] is not JArray refs || refs.Count == 0)
                return paths;

            foreach (var token in refs)
            {
                if (token is not JObject item)
                    continue;
                string localPath = item["local_path"]?.ToString();
                if (string.IsNullOrEmpty(localPath))
                    localPath = item["image_path"]?.ToString();
                if (string.IsNullOrEmpty(localPath))
                    localPath = item["path"]?.ToString();
                if (!string.IsNullOrEmpty(localPath))
                    paths.Add(localPath);
            }

            return paths;
        }

        private static string NormalizeToAbsoluteImagePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            if (Path.IsPathRooted(path))
                return path;
            return PathUtils.ToAbsoluteAssetPath(path.Replace("\\", "/"));
        }

        private static JArray BuildUserReferenceRefs(List<string> userReferenceImagePaths)
        {
            var result = new JArray();
            if (userReferenceImagePaths == null || userReferenceImagePaths.Count == 0)
                return result;

            for (int i = 0; i < userReferenceImagePaths.Count; i++)
            {
                string path = userReferenceImagePaths[i];
                if (string.IsNullOrEmpty(path))
                    continue;

                result.Add(new JObject
                {
                    ["index"] = i,
                    ["source"] = "user_upload",
                    ["role"] = "identity_primary",
                    ["path"] = path,
                    ["name"] = Path.GetFileName(path)
                });
            }

            return result;
        }

        private static string ExtractInstructionsFromEnvelopeRaw(string envelopeRaw)
        {
            if (string.IsNullOrEmpty(envelopeRaw))
                return null;
            try
            {
                var obj = JObject.Parse(envelopeRaw);
                return obj["instructions"]?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string BuildPromptWithInstructionsFallback(string prompt, string instructions)
        {
            if (string.IsNullOrWhiteSpace(instructions))
                return prompt ?? "";
            if (string.IsNullOrWhiteSpace(prompt))
                return prompt ?? "";
            return instructions + "\n\n通道约束：用户上传参考图用于角色身份与外观；knowledge 参考图仅用于网格/切片布局，不得用于风格或角色外观。\n\n用户需求：" + prompt;
        }

        protected override void OnModelSelectedBase(AIModelInfo model)
        {
            base.OnModelSelectedBase(model);
            selectedPromptTemplate = null;
            if (_currentGenerator is DynamicGenerator dg)
                dg.SetPromptTemplateSelection(null);
        }

        private void EnsureTargetImage()
        {
            // 初始化阶段：只在未绑定/无效时创建占位图，不强制改动用户已绑定的扩展名。
            if (targetImageAsset != null && targetImageAsset.IsValid())
                return;

            EnsureTargetImage(".jpg");
        }

        private void EnsureTargetImage(string desiredExt)
        {
            desiredExt = (desiredExt ?? ".jpg").Trim();
            if (!desiredExt.StartsWith("."))
                desiredExt = "." + desiredExt;
            desiredExt = desiredExt.ToLowerInvariant();

            if (targetImageAsset != null && targetImageAsset.IsValid())
            {
                string targetPath = targetImageAsset.GetPath();
                string targetExt = Path.GetExtension(targetPath).ToLowerInvariant();
                if (string.Equals(targetExt, desiredExt, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/TJGenerators"))
                AssetDatabase.CreateFolder("Assets", "TJGenerators");
            if (!AssetDatabase.IsValidFolder("Assets/TJGenerators/History"))
                AssetDatabase.CreateFolder("Assets/TJGenerators", "History");

            // 与其他工具一致：默认占位/目标图片也放入 History 目录，避免根目录堆积文件
            string path = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/TJGenerators/History/New Image" + desiredExt
            );
            path = CreateBlankImage(path);

            if (string.IsNullOrEmpty(path))
            {
                TJLog.LogError($"{LogTag} 无法创建图片资产");
                return;
            }

            targetImageAsset = TJGeneratorsAssetReference.FromPath(path);
            titleContent = new GUIContent(
                $"TJGenerators 图片 - {Path.GetFileNameWithoutExtension(path)}"
            );

            if (!string.IsNullOrEmpty(targetImageAsset.guid))
                imageOpenWindows[targetImageAsset.guid] = this;

            Repaint();
        }

        /// <summary>
        /// 创建空白图片资产（根据扩展名创建 JPG/PNG）。
        /// </summary>
        public static string CreateBlankImage(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
            {
                path = Path.ChangeExtension(path, ".jpg");
                ext = ".jpg";
            }

            string absolutePath = PathUtils.ToAbsoluteAssetPath(path);

            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var blank =
                ext == ".png"
                    ? new Texture2D(4, 4, TextureFormat.RGBA32, false)
                    : new Texture2D(4, 4, TextureFormat.RGB24, false);
            var pixels = new Color[16];
            // 与「生成精灵」占位一致：PNG 全透明；JPG 无 alpha 时用与历史缩略图占位相近的深灰，避免一开始整片发白。
            Color fill = ext == ".png" ? Color.clear : new Color(0.2f, 0.2f, 0.2f);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = fill;
            blank.SetPixels(pixels);
            blank.Apply();

            if (ext == ".png")
            {
                File.WriteAllBytes(absolutePath, blank.EncodeToPNG());
            }
            else
            {
                File.WriteAllBytes(absolutePath, blank.EncodeToJPG(75));
            }
            DestroyImmediate(blank);

            // 导入并设置类型
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.SaveAndReimport();
            }

            TJGeneratorsGenerationLabel.EnableLabel(TJGeneratorsAssetReference.FromPath(path));
            return path;
        }
    }
}
#endif
