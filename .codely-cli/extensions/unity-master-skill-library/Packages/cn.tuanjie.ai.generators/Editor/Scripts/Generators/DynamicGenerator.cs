#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TJGenerators;
using TJGenerators.Config;
using TJGenerators.Pipeline;
using TJGenerators.UI;
using TJGenerators.Utils;

namespace TJGenerators.Generators
{
    /// <summary>
    /// 动态生成器 - 完全由配置驱动，无需编写C#代码即可添加新模型
    /// </summary>
    public class DynamicGenerator : ModelGeneratorBase, IGeneratorParameterProvider
    {
        private readonly GeneratorConfig _config;
        private readonly PipelineSettings _pipelineSettings;
        private string _currentEndpointKey = "default";

        // 参数值存储
        private readonly Dictionary<string, object> _parameterValues = new();
        private readonly Dictionary<string, string> _extraRawJsonFields = new();
        private readonly Dictionary<string, int> _dropdownIndices = new();

        // 输入数据
        private string _textPrompt = "";
        private VisualSelectorOptionConfig _selectedType = null;
        private VisualSelectorOptionConfig _selectedStyle = null;
        private MaterialTemplateOptionConfig _selectedPromptTemplate = null;
        private string _imagePath = "";
        private Texture2D _uploadedImage;

        private List<string> _imagePaths = new();

        // 多视图数据
        private List<string> _multiViewPaths = new();
        private List<Texture2D> _multiViewImages = new();
        private int _multiViewCount = 0;
        private int _multiViewMinRequired = 1;
        private string _currentInputMode = "text"; // text, image, multiview

        private string _primaryInputMode = "textOrImage";

        /// <summary>多视图底部四格说明文案。</summary>
        private static readonly string[] s_multiViewFooterLabels =
        {
            "正面 (必需)",
            "左侧",
            "背面",
            "右侧",
        };

        /// <summary>文件选择对话框标题用词（不含「必需」后缀）。</summary>
        private static readonly string[] s_multiViewPickerTitleLabels =
        {
            "正面",
            "左侧",
            "背面",
            "右侧",
        };

        // UI状态
        private bool _advancedFoldout = false;
        // 生成按钮点数（由宿主窗口根据接口查询后注入）
        private int _generateCost = 0;

        /// <summary>
        /// 宿主窗口在生成前才写入参考图时，用于积分预览（文生/图生端点切换）。
        /// </summary>
        private bool _costPreviewHasReferenceImage;

        private bool _addMotionEnabled = false;

        private string _motionDescription = "";

        // GLB选择器数据
        private List<TJGeneratorsGenerationHistoryItem> _convertibleGlbFiles;
        private string[] _convertibleGlbDisplayNames;
        private int _selectedGlbIndex = -1;
        private string _sourceGlbPath = "";
        private string _sourceGlbUrl = "";
        private GameObject _sourceGlbObject;

        // 文件上传数据（用于UniRig等需要上传文件的生成器）
        private string _uploadedFilePath = "";
        private string _uploadedFileName = "";

        private string _uploadedModelAssetPath = "";

        private List<string> _projectMeshAssetPaths;
        private string[] _projectMeshPopupOptions;
        private int _selectedProjectMeshPopupIndex;

        /// <summary>
        /// 为 true 时下一次刷新会重新扫描 Assets 下的网格列表。
        /// 工程变更时置位，避免在 OnGUI 每帧执行 FindAssets 全库遍历导致内存暴涨/编辑器崩溃。
        /// </summary>
        private static bool s_forceProjectMeshRescan = true;

        static DynamicGenerator()
        {
            EditorApplication.projectChanged += () => s_forceProjectMeshRescan = true;
        }

        public DynamicGenerator(GeneratorConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _pipelineSettings = new PipelineSettings(_config);
            InitializeDefaultValues();
        }

        #region 公开API（供外部调用）

        public void SetTextPrompt(string prompt)
        {
            _textPrompt = prompt ?? "";
            _currentInputMode = "text";
            UpdateEndpointForInputMode();
        }

        /// <summary>
        /// 设置图片路径（单图模式），内部转为 SetImagePaths 以保证与多图逻辑一致。
        /// </summary>
        public void SetImagePath(string path)
        {
            SetImagePaths(path == null ? null : new[] { path });
        }

        /// <summary>
        /// 设置多图路径（如 Sprite 多图生组图/多图生图，最多 14 张）。null 或空则清空。
        /// </summary>
        public void SetImagePaths(IList<string> paths)
        {
            if (paths == null || paths.Count == 0)
            {
                _imagePaths.Clear();
                _imagePath = "";
                _uploadedImage = null;
                return;
            }
            _imagePaths = new List<string>(paths);
            _imagePath = _imagePaths[0] ?? "";
            _currentInputMode = "image";
            UpdateEndpointForInputMode();
            if (_imagePaths.Count == 1 && File.Exists(_imagePath))
            {
                _uploadedImage = new Texture2D(2, 2);
                _uploadedImage.LoadImage(File.ReadAllBytes(_imagePath));
            }
            else
            {
                _uploadedImage = null;
            }
        }

        public void SetTypeSelection(VisualSelectorOptionConfig type)
        {
            _selectedType = type;
        }

        public void SetStyleSelection(VisualSelectorOptionConfig style)
        {
            _selectedStyle = style;
        }

        /// <summary>
        /// 设置文生图提示词模板（outputType 为 image 时，模板 prompt 会拼入 <see cref="DynamicRequestJsonBuilder.BuildEnhancedPrompt"/>）
        /// </summary>
        public void SetPromptTemplateSelection(MaterialTemplateOptionConfig template)
        {
            _selectedPromptTemplate = template;
        }

        /// <summary>当前模板 id（模板特定后处理）。</summary>
        public string GetSelectedPromptTemplateId()
        {
            return _selectedPromptTemplate?.id;
        }

        /// <summary>
        /// 设置多视图图片路径
        /// </summary>
        /// <param name="paths">图片路径数组，顺序为：正面、左侧、背面、右侧</param>
        public void SetMultiViewPaths(string[] paths)
        {
            if (paths == null || paths.Length == 0)
                return;

            _multiViewPaths = new List<string>(new string[4]);
            _multiViewImages = new List<Texture2D>(new Texture2D[4]);
            _multiViewCount = 4;
            _multiViewMinRequired = GetMultiViewMinRequired();

            for (int i = 0; i < Math.Min(paths.Length, 4); i++)
            {
                if (!string.IsNullOrEmpty(paths[i]) && File.Exists(paths[i]))
                {
                    _multiViewPaths[i] = paths[i];
                    _multiViewImages[i] = new Texture2D(2, 2);
                    _multiViewImages[i].LoadImage(File.ReadAllBytes(paths[i]));
                }
            }

            _currentInputMode = "multiview";
            UpdateEndpointForInputMode();
        }

        /// <summary>
        /// 以编程方式（非 UI 选择）设置文件上传路径，供 CustomTool 在提交 UniRig 任务时使用。
        /// </summary>
        public void SetFileUploadPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            _uploadedModelAssetPath = assetPath;
            _uploadedFilePath       = PathUtils.ToAbsoluteAssetPath(assetPath);
            _uploadedFileName       = Path.GetFileName(assetPath);
        }

        public void SetParameter(string parameterId, object value)
        {
            if (string.IsNullOrEmpty(parameterId))
                return;

            string prev = _parameterValues.TryGetValue(parameterId, out var old) ? old?.ToString() : null;
            string next = value?.ToString();
            if (string.Equals(prev, next, StringComparison.Ordinal))
                return;

            _parameterValues[parameterId] = value;

            // 如果是下拉选项，同步更新索引
            var param = _config.parameters?.Find(p => p.id == parameterId);
            if (param?.options != null)
            {
                for (int i = 0; i < param.options.Count; i++)
                {
                    if (param.options[i].value == next)
                    {
                        _dropdownIndices[parameterId] = i;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 由宿主在绘制参考图区域时同步，以便未写入 <see cref="_imagePaths"/> 前也能按图生端点预估积分。
        /// </summary>
        public void SyncReferenceImagesForCostPreview(bool hasReferenceImage)
        {
            _costPreviewHasReferenceImage = hasReferenceImage;
        }

        /// <summary>
        /// 与即将提交生成时一致的 API 端点，用于查询积分消耗。
        /// </summary>
        public string GetEffectiveApiEndpointForCredit()
        {
            var uiLayout = _config.uiLayout ?? new UILayoutConfig();

            if (uiLayout.showGlbSelector || uiLayout.showFileUpload)
            {
                string convertEp = _config.GetEndpoint("default");
                if (!string.IsNullOrEmpty(convertEp))
                    return convertEp;
            }

            var (isMultiViewOnly, isDualMode) = ResolveUILayoutModes(uiLayout);
            string modeKey;
            if (isMultiViewOnly || (isDualMode && _primaryInputMode == "multiview"))
                modeKey = "multiview";
            else if (HasReferenceImageInputForCredit())
                modeKey = "image";
            else
                modeKey = "text";

            string endpoint = _config.GetEndpoint(modeKey);
            if (!string.IsNullOrEmpty(endpoint))
                return endpoint;

            endpoint = _config.GetEndpoint(_currentEndpointKey);
            if (!string.IsNullOrEmpty(endpoint))
                return endpoint;

            return _config.GetEndpoint("text");
        }

        /// <summary>
        /// 影响因素哈希；变化时应重新查询积分。
        /// </summary>
        public int ComputeCostFactorsHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (_primaryInputMode?.GetHashCode() ?? 0);
                hash = hash * 31 + (_currentInputMode?.GetHashCode() ?? 0);
                hash = hash * 31 + (HasReferenceImageInputForCredit() ? 1 : 0);
                hash = hash * 31 + (_addMotionEnabled ? 1 : 0);
                hash = hash * 31 + (string.IsNullOrWhiteSpace(_motionDescription) ? 0 : _motionDescription.Trim().GetHashCode());
                hash = hash * 31 + (GeneratorId?.GetHashCode() ?? 0);
                if (_parameterValues != null)
                {
                    foreach (var kv in _parameterValues)
                        hash = hash * 31 + ((kv.Key ?? "") + ":" + (kv.Value?.ToString() ?? "")).GetHashCode();
                }
                return hash;
            }
        }

        private bool HasReferenceImageInputForCredit() =>
            HasReferenceImageInput() || _costPreviewHasReferenceImage;

        /// <summary>
        /// 设置生成按钮展示的预计总积分（主任务 + 已选后处理子任务之和）。
        /// </summary>
        public void SetGenerateCost(int cost)
        {
            _generateCost = Mathf.Max(0, cost);
        }

        /// <summary>
        /// 收集当前选项下预计会触发的全部扣费任务（用于按钮积分预览）。
        /// </summary>
        public void BuildEstimatedCostComponents(List<GenerationCreditHelper.CostComponent> components)
        {
            components.Clear();
            components.Add(
                new GenerationCreditHelper.CostComponent(GeneratorId, GetEffectiveApiEndpointForCredit())
            );

            if (!GetAddMotionEnabled())
                return;

            var unirigCfg = ConfigManager.GetGeneratorConfig(ConfigType.Generator, "unirig");
            string unirigEndpoint = unirigCfg?.GetEndpoint("default");
            if (!string.IsNullOrEmpty(unirigEndpoint))
                components.Add(new GenerationCreditHelper.CostComponent("unirig", unirigEndpoint));

            if (string.IsNullOrWhiteSpace(_motionDescription))
                return;

            var motionCfg = ConfigManager.GetGeneratorConfig(ConfigType.Generator, "hunyuan-motion");
            string motionEndpoint = motionCfg?.GetEndpoint("default");
            if (!string.IsNullOrEmpty(motionEndpoint))
                components.Add(new GenerationCreditHelper.CostComponent("hunyuan-motion", motionEndpoint));
        }

        /// <summary>
        /// 设置额外的 JSON 字段（原样拼接到请求体中，value 需为合法 JSON 片段）。
        /// 用于扩展配置外字段，例如 custom gems 结构中的 instructions / knowledge_refs。
        /// </summary>
        public void SetExtraRawJsonField(string fieldName, string rawJsonValue)
        {
            if (string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(rawJsonValue))
                return;

            _extraRawJsonFields[fieldName] = rawJsonValue;
        }

        /// <summary>
        /// 清理所有额外 JSON 字段。
        /// </summary>
        public void ClearExtraRawJsonFields()
        {
            _extraRawJsonFields.Clear();
        }

        public object GetParameter(string parameterId)
        {
            return _parameterValues.TryGetValue(parameterId, out var value) ? value : null;
        }

        #endregion

        #region 基本信息（从配置读取）

        public override string DisplayName => _config.displayName ?? _config.id;
        public override string GeneratorId => _config.id;
        public override string ApiEndpoint
        {
            get
            {
                string endpoint = _config.GetEndpoint(_currentEndpointKey);
                if (!string.IsNullOrEmpty(endpoint))
                    return endpoint;

                endpoint = _config.GetEndpoint(GetEndpointKeyForInputMode(_currentInputMode));
                if (!string.IsNullOrEmpty(endpoint))
                    return endpoint;

                endpoint = _config.GetEndpoint("default");
                if (!string.IsNullOrEmpty(endpoint))
                    return endpoint;

                if (_config.endpoints != null && _config.endpoints.Count > 0)
                    return _config.endpoints[0].value ?? "";

                return "";
            }
        }

        #endregion

        #region 初始化默认值

        private void InitializeDefaultValues()
        {
            if (_config.parameters == null)
                return;

            foreach (var param in _config.parameters)
            {
                if (param.options != null && param.options.Count > 0)
                {
                    // 下拉选项：找到默认值的索引
                    int defaultIndex = 0;
                    if (!string.IsNullOrEmpty(param.defaultValue))
                    {
                        for (int i = 0; i < param.options.Count; i++)
                        {
                            if (param.options[i].value == param.defaultValue)
                            {
                                defaultIndex = i;
                                break;
                            }
                        }
                    }
                    _dropdownIndices[param.id] = defaultIndex;
                    _parameterValues[param.id] = param.options[defaultIndex].value;
                }
                else
                {
                    // 其他类型：使用默认值
                    _parameterValues[param.id] = ParseDefaultValue(param);
                }
            }
        }

        private object ParseDefaultValue(ParameterConfig param)
        {
            if (string.IsNullOrEmpty(param.defaultValue))
            {
                return param.type switch
                {
                    "int" => 0,
                    "float" => 0f,
                    "bool" => false,
                    "string" => "",
                    _ => "",
                };
            }

            return param.type switch
            {
                "int" => int.TryParse(param.defaultValue, out int i) ? i : 0,
                "float" => float.TryParse(param.defaultValue, out float f) ? f : 0f,
                "bool" => param.defaultValue.ToLower() == "true",
                _ => param.defaultValue,
            };
        }

        #endregion

        #region UI绘制

        private static (bool isMultiViewOnly, bool isDualMode) ResolveUILayoutModes(
            UILayoutConfig uiLayout)
        {
            bool isMultiViewOnly =
                uiLayout.showMultiView && !uiLayout.showTextInput && !uiLayout.showImageUpload;
            bool isDualMode =
                (uiLayout.showTextInput || uiLayout.showImageUpload) && uiLayout.showMultiView;
            return (isMultiViewOnly, isDualMode);
        }

        public override void DrawParametersUI(IGenerationPipelineHost context)
        {
            bool is3DMainWindow = context is TJGenerators3DModelWindow;
            float sectionSpacing = CommonStyles.Space2; // 组件与组件之间的间距（24）
            float titleToControlSpacing = CommonStyles.Space1; // 标题与自身功能区间距（12）
            float foldoutSpacing = CommonStyles.Space2; // 折叠区域之间的间距（24）

            var uiLayout = _config.uiLayout ?? new UILayoutConfig();

            var (isMultiViewOnly, isDualMode) = ResolveUILayoutModes(uiLayout);

            // GLB选择器区域（用于模型转换）
            if (uiLayout.showGlbSelector)
            {
                DrawGlbSelector(context);
            }

            // 文件上传区域（用于UniRig等）
            if (uiLayout.showFileUpload)
            {
                DrawFileUpload(context);
            }

            if (isDualMode)
            {
                GUILayout.BeginHorizontal();
                UIComponents.DrawSectionTitle("生成模式", uppercase: false);
                GUILayout.EndHorizontal();
                GUILayout.Space(titleToControlSpacing);
                float rowHeight = EditorUiScale.S(22f);
                int selectedIndex = _primaryInputMode == "multiview" ? 1 : 0;
                string[] modeOptions = new string[]
                {
                    "文生与图生",
                    "多视图生成",
                };
                int newIndex;
                if (is3DMainWindow)
                {
                    newIndex = UIComponents.DrawStyledDropdown(
                        "3d_model_primary_input_mode_dropdown",
                        selectedIndex,
                        modeOptions,
                        separatorBeforeIndex: -1,
                        panelTopGap: 4f,
                        hoverInset: 2f);
                }
                else
                {
                    Rect dropdownRect = EditorGUILayout.GetControlRect(false, rowHeight);
                    var guiOptions = new GUIContent[] { new GUIContent(modeOptions[0]), new GUIContent(modeOptions[1]) };
                    newIndex = EditorGUI.Popup(dropdownRect, selectedIndex, guiOptions);
                }
                _primaryInputMode = newIndex == 1 ? "multiview" : "textOrImage";
                GUILayout.Space(sectionSpacing);

                if (_primaryInputMode == "textOrImage")
                {
                    DrawTextAndImageInputs(context, uiLayout, sectionSpacing, titleToControlSpacing);
                }
                else
                {
                    EnsureMultiViewInit();
                    DrawMultiViewHeaderRow(context, uiLayout, rowHeight, foldoutSpacing);
                    DrawMultiViewArea(context);
                }
            }
            else if (isMultiViewOnly)
            {
                EnsureMultiViewInit();
                float rowHeight = EditorUiScale.S(22f);
                DrawMultiViewHeaderRow(context, uiLayout, rowHeight, foldoutSpacing);
                DrawMultiViewArea(context);
            }
            else
            {
                // 仅文生/图生
                DrawTextAndImageInputs(context, uiLayout, sectionSpacing, titleToControlSpacing);
            }

            if (is3DMainWindow) GUILayout.Space(sectionSpacing);
            UIComponents.DrawGapLine();
            if (is3DMainWindow) GUILayout.Space(sectionSpacing);
            // 高级参数（折叠）
            if (_config.parameters != null && _config.parameters.Count > 0)
            {
                if (!is3DMainWindow)
                    GUILayout.Space(foldoutSpacing);
                _advancedFoldout = UIComponents.DrawAdvancedSettingsFoldout(
                    _advancedFoldout,
                    this,
                    _config.parameters,
                    uiLayout.advancedLabel ?? "高级设置"
                );
            }

            if (ShouldShowEnableMotionUi())
            {
                if (is3DMainWindow) GUILayout.Space(sectionSpacing);
                UIComponents.DrawGapLine();
                if (is3DMainWindow) GUILayout.Space(sectionSpacing);
                if (!is3DMainWindow)
                    GUILayout.Space(foldoutSpacing);
                UIComponents.DrawSectionTitle("后处理", uppercase: false);
                GUILayout.Space(titleToControlSpacing);

                if (is3DMainWindow)
                {
                    float boxSize = EditorUiScale.S(30f);
                    Rect rowRect = GUILayoutUtility.GetRect(
                        0f,
                        UIComponents.AdvancedSettingsRowHeight,
                        GUILayout.ExpandWidth(true),
                        GUILayout.Height(UIComponents.AdvancedSettingsRowHeight));
                    UIComponents.GetAdvancedSettingsRowColumnLayout(rowRect, out Rect labelRect, out float controlX);
                    GUI.Label(labelRect, "添加动作", CommonStyles.AdvancedFoldoutTitleStyle);
                    Rect toggleRect = new Rect(
                        controlX,
                        rowRect.y + (rowRect.height - boxSize) * 0.5f,
                        boxSize,
                        boxSize);
                    _addMotionEnabled = GUI.Toggle(toggleRect, _addMotionEnabled, GUIContent.none, CommonStyles.CheckboxBoxStyle);
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    _addMotionEnabled = EditorGUILayout.Toggle("添加动作", _addMotionEnabled);
                    GUILayout.EndHorizontal();
                }

                if (_addMotionEnabled)
                {
                    if (is3DMainWindow)
                    {
                        GUILayout.Space(titleToControlSpacing);
                        Rect rowRect = GUILayoutUtility.GetRect(
                            0f,
                            UIComponents.AdvancedSettingsRowHeight,
                            GUILayout.ExpandWidth(true),
                            GUILayout.Height(UIComponents.AdvancedSettingsRowHeight));
                        UIComponents.GetAdvancedSettingsRowColumnLayout(rowRect, out Rect labelRect, out float controlX);
                        Rect inputRect = new Rect(
                            controlX,
                            rowRect.y,
                            UIComponents.AdvancedSettingsRowControlWidth,
                            UIComponents.AdvancedSettingsRowHeight);
                        GUI.Label(labelRect, "动作描述", CommonStyles.AdvancedFoldoutTitleStyle);
                        var bgTex = CommonStyles.AdvancedInputBoxTexture;
                        if (bgTex != null)
                            UIComponents.DrawNineSliceFixed(inputRect, bgTex, 8, 4);
                        else
                            EditorGUI.DrawRect(inputRect, new Color(34f / 255f, 34f / 255f, 34f / 255f, 1f));
                        _motionDescription = EditorGUI.TextField(inputRect, _motionDescription ?? string.Empty, CommonStyles.AdvancedInputTextStyle);
                    }
                    else
                    {
                        GUILayout.Space(CommonStyles.LineSpacing);
                        _motionDescription = UIComponents.DrawTextField(
                            "动作描述",
                            "输入动作，如：walk、run、jump...",
                            _motionDescription
                        );
                    }
                }
            }

            GUILayout.Space(sectionSpacing);
            DrawTopGenerateButton(context, uiLayout);

            if (context is GenerationWindowBase costHost)
                costHost.TryRefreshGenerationCostFromGenerator(this);
        }

        private bool ShouldShowEnableMotionUi() => _pipelineSettings.ShouldShowEnableMotionUi();

        private void DrawTextAndImageInputs(
            IGenerationPipelineHost context,
            UILayoutConfig uiLayout,
            float sectionSpacing,
            float titleToControlSpacing
        )
        {
            bool is3DMainWindow = context is TJGenerators3DModelWindow;

            if (is3DMainWindow)
            {
                if (uiLayout.showImageUpload)
                {
                    DrawImageUploadSection(
                        context,
                        uiLayout.imageUploadLabel ?? "参考图片",
                        titleToControlSpacing,
                        sectionSpacing,
                        useShowcaseStyle: true,
                        addTrailingSectionSpacing: false
                    );
                }

                if (uiLayout.showTextInput)
                {
                    if (uiLayout.showImageUpload)
                        GUILayout.Space(sectionSpacing);

                    UIComponents.DrawSectionTitle(uiLayout.textInputLabel ?? "文本提示词（可选）", uppercase: false);
                    GUILayout.Space(titleToControlSpacing);
                    _textPrompt = UIComponents.DrawPromptInputBox(
                        _textPrompt,
                        uiLayout.textInputPlaceholder ?? "在此处输入文本提示...",
                        "generator_main_prompt_input");
                }
                return;
            }

            if (uiLayout.showTextInput)
            {
                _textPrompt = UIComponents.DrawTextField(
                    uiLayout.textInputLabel ?? "文本提示词（可选）",
                    uiLayout.textInputPlaceholder ?? "在此处输入文本提示...",
                    _textPrompt
                );
                GUILayout.Space(CommonStyles.LineSpacing);
            }

            if (uiLayout.showImageUpload)
            {
                DrawImageUploadSection(
                    context,
                    uiLayout.imageUploadLabel ?? "参考图片",
                    titleToControlSpacing,
                    sectionSpacing,
                    useShowcaseStyle: false,
                    addTrailingSectionSpacing: true
                );
            }
        }

        private void DrawImageUploadSection(
            IGenerationPipelineHost context,
            string title,
            float titleToControlSpacing,
            float sectionSpacing,
            bool useShowcaseStyle,
            bool addTrailingSectionSpacing)
        {
            float rowHeight = EditorUiScale.S(21f);
            GUILayout.BeginHorizontal(GUILayout.Height(rowHeight));
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            UIComponents.DrawSectionTitle(title, uppercase: false);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            if (!useShowcaseStyle)
            {
                UIComponents.LinkButton(
                    "生成参考图",
                    () =>
                    {
                        AIReferenceImageWindow.Show(
                            (path, texture) =>
                            {
                                _imagePath = path;
                                _uploadedImage = texture;
                                context.Repaint();
                            }
                        );
                    }
                );
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(titleToControlSpacing);

            if (useShowcaseStyle)
            {
                UIComponents.DrawUploadImageLargeComponent(
                    ref _imagePath,
                    ref _uploadedImage,
                    () =>
                    {
                        AIReferenceImageWindow.Show(
                            (path, texture) =>
                            {
                                _imagePath = path;
                                _uploadedImage = texture;
                                _currentInputMode = string.IsNullOrEmpty(path) ? "text" : "image";
                                UpdateEndpointForInputMode();
                                context.Repaint();
                            }
                        );
                    },
                    context.Repaint,
                    onUserChanged: () =>
                    {
                        _currentInputMode = string.IsNullOrEmpty(_imagePath) ? "text" : "image";
                        UpdateEndpointForInputMode();
                    }
                );
            }
            else
            {
                UIComponents.DrawSingleImageUpload(
                    null,
                    ref _imagePath,
                    ref _uploadedImage,
                    context.Repaint,
                    onUserChanged: () =>
                    {
                        _currentInputMode = string.IsNullOrEmpty(_imagePath) ? "text" : "image";
                        UpdateEndpointForInputMode();
                    }
                );
            }
            if (addTrailingSectionSpacing)
                GUILayout.Space(sectionSpacing);
        }

        private void DrawTopGenerateButton(IGenerationPipelineHost context, UILayoutConfig uiLayout)
        {
            GUILayout.BeginHorizontal();
            bool windowPipelineBusy = context is GenerationWindowBase gwb && gwb.IsPipelineBusy;
            bool busy = IsRunning || windowPipelineBusy;
            bool canGenerate = !busy;
            if (uiLayout.showGlbSelector)
            {
                canGenerate =
                    canGenerate
                    && !string.IsNullOrEmpty(_sourceGlbPath)
                    && !string.IsNullOrEmpty(_sourceGlbUrl);
            }
            else if (uiLayout.showFileUpload)
            {
                canGenerate =
                    canGenerate && !string.IsNullOrEmpty(_uploadedFilePath);
            }
            else if (canGenerate)
            {
                canGenerate = ValidateInputs(out _);
            }
            GUI.enabled = canGenerate;

            string buttonLabel = uiLayout.showGlbSelector ? "转换" : "生成";
            string buttonText = busy ? "生成中..." : buttonLabel;
            bool clicked = UIComponents.DrawGenerateButtonWithCost(
                buttonText,
                _generateCost,
                canGenerate,
                GUILayout.Width(CommonStyles.LeftComponentWidth),
                GUILayout.MinWidth(CommonStyles.LeftComponentWidth),
                GUILayout.MaxWidth(CommonStyles.LeftComponentWidth),
                GUILayout.Height(EditorUiScale.S(40f)));

            if (clicked)
            {
                if (ValidateInputs(out string error))
                {
                    if (uiLayout.showGlbSelector || uiLayout.showFileUpload)
                    {
                        _currentEndpointKey = "default";
                    }
                    else
                    {
                        var (isMultiViewOnly, isDualMode) = ResolveUILayoutModes(uiLayout);

                        if (isMultiViewOnly || (isDualMode && _primaryInputMode == "multiview"))
                        {
                            _currentInputMode = "multiview";
                        }
                        else
                        {
                            if (HasReferenceImageInput())
                                _currentInputMode = "image";
                            else
                                _currentInputMode = "text";
                        }
                        UpdateEndpointForInputMode();
                    }
                    context.StartGeneration(this);
                }
                else
                {
                    ErrorDialogUtils.ShowErrorDialog("输入不完整", error, "[DynamicGenerator]");
                }
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        private void EnsureMultiViewInit()
        {
            if (_multiViewPaths.Count < 4)
            {
                _multiViewPaths = new List<string>(new string[4]);
                _multiViewImages = new List<Texture2D>(new Texture2D[4]);
            }
            _multiViewCount = 4;
            _multiViewMinRequired = GetMultiViewMinRequired();
        }

        private int GetMultiViewMinRequired()
        {
            return _config?.uiLayout?.multiViewMinRequired ?? 2;
        }

        private void DrawMultiViewHeaderRow(
            IGenerationPipelineHost context,
            UILayoutConfig uiLayout,
            float rowHeight,
            float foldoutSpacing
        )
        {
            GUILayout.Space(foldoutSpacing);
            GUILayout.BeginHorizontal(GUILayout.Height(rowHeight));
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            GUILayout.Label(uiLayout.multiViewLabel ?? "多视图生成", CommonStyles.HeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            UIComponents.LinkButton(
                "生成参考图",
                () =>
                {
                    AIReferenceImageWindow.ShowMultiView(
                        (paths, textures) =>
                        {
                            TJLog.Log(
                                $"[DynamicGenerator][MultiView] reference callback: "
                                    + $"pathsLen={(paths == null ? -1 : paths.Length)}, "
                                    + $"texturesLen={(textures == null ? -1 : textures.Length)}"
                            );

                            EnsureMultiViewInit();

                            int count = Math.Min(4, paths == null ? 0 : paths.Length);
                            for (int i = 0; i < count; i++)
                            {
                                string p = paths[i];
                                bool fileExists = !string.IsNullOrEmpty(p) && File.Exists(p);

                                Texture2D tex = null;
                                if (textures != null && i < textures.Length)
                                    tex = textures[i];

                                TJLog.Log(
                                    $"[DynamicGenerator][MultiView] slot[{i}]: "
                                        + $"pathEmpty={string.IsNullOrEmpty(p)}, fileExists={fileExists}, texNull={tex == null}"
                                );

                                if (!string.IsNullOrEmpty(p) && fileExists)
                                {
                                    _multiViewPaths[i] = p;

                                    if (tex == null)
                                    {
                                        try
                                        {
                                            tex = new Texture2D(2, 2);
                                            tex.LoadImage(File.ReadAllBytes(p));
                                        }
                                        catch (Exception e)
                                        {
                                            TJLog.LogWarning(
                                                $"[DynamicGenerator][MultiView] slot[{i}] reload texture failed: {e.Message}"
                                            );
                                        }
                                    }

                                    _multiViewImages[i] = tex;

                                    TJLog.Log(
                                        $"[DynamicGenerator][MultiView] slot[{i}] after set: imageNull={_multiViewImages[i] == null}"
                                    );
                                }
                            }
                            context.Repaint();
                        },
                        GetMultiViewMinRequired()
                    );
                }
            );
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawGlbSelector(IGenerationPipelineHost context)
        {
            // 统一间距常量
            float sectionSpacing = CommonStyles.Space1;

            var uiLayout = _config.uiLayout ?? new UILayoutConfig();

            GUILayout.BeginVertical();
            UIComponents.DrawSectionTitle("源GLB文件", uppercase: false);
            GUILayout.EndVertical();

            GUILayout.Space(sectionSpacing);

            GUILayout.BeginVertical();
            GUILayout.Label(
                uiLayout.glbSelectorLabel
                    ?? "选择要转换的GLB文件，将混元生成的GLB模型转换为其他格式",
                CommonStyles.SmallGreyLabelStyle
            );
            GUILayout.EndVertical();

            GUILayout.Space(sectionSpacing);

            // 刷新可转换文件列表
            RefreshConvertibleGlbFiles();

            GUILayout.BeginHorizontal();

            if (_convertibleGlbFiles == null || _convertibleGlbFiles.Count == 0)
            {
                GUILayout.BeginVertical();
                GUILayout.Label("暂无可转换的GLB文件", CommonStyles.HelpBoxStyle);
                GUILayout.EndVertical();
            }
            else
            {
                GUILayout.BeginVertical();
                EditorGUI.BeginChangeCheck();
                _selectedGlbIndex = EditorGUILayout.Popup(
                    _selectedGlbIndex,
                    _convertibleGlbDisplayNames
                );
                if (
                    EditorGUI.EndChangeCheck()
                    && _selectedGlbIndex >= 0
                    && _selectedGlbIndex < _convertibleGlbFiles.Count
                )
                {
                    var selectedItem = _convertibleGlbFiles[_selectedGlbIndex];
                    _sourceGlbPath = selectedItem.modelPath;
                    _sourceGlbUrl = selectedItem.sourceGlbUrl;

                    // 同步ObjectField显示
                    _sourceGlbObject = AssetDatabase.LoadAssetAtPath<GameObject>(_sourceGlbPath);
                }
                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();

            // 显示选中文件的预览
            if (_sourceGlbObject != null)
            {
                GUILayout.Space(sectionSpacing);
                GUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(
                    _sourceGlbObject,
                    typeof(GameObject),
                    false,
                    GUILayout.Height(EditorUiScale.S(40f))
                );
                GUILayout.EndHorizontal();
            }

            // 提示信息
            GUILayout.Space(sectionSpacing);
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                uiLayout.glbSelectorHint
                    ?? "列表中只显示可转换的模型（需要先用混元3D生成GLB格式的模型）",
                CommonStyles.HelpBoxStyle
            );
            GUILayout.EndHorizontal();
            GUILayout.Space(sectionSpacing);
        }

        private void DrawFileUpload(IGenerationPipelineHost context)
        {
            float sectionSpacing = CommonStyles.Space1;

            var uiLayout = _config.uiLayout ?? new UILayoutConfig();

            GUILayout.BeginVertical();
            UIComponents.DrawSectionTitle(uiLayout.fileUploadLabel ?? "绑骨模型", uppercase: false);
            GUILayout.EndVertical();

            GUILayout.Space(sectionSpacing);

            GUILayout.BeginVertical();
            GUILayout.Label(
                "从当前工程的 Assets 中选择 FBX / GLB / OBJ。",
                CommonStyles.SmallGreyLabelStyle
            );
            GUILayout.EndVertical();

            GUILayout.Space(sectionSpacing);

            RefreshProjectMeshModelsForUpload();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("刷新模型列表", GUILayout.Width(EditorUiScale.S(100f))))
            {
                s_forceProjectMeshRescan = true;
                context.Repaint();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(EditorUiScale.S(6f));

            GUILayout.BeginHorizontal();

            if (_projectMeshAssetPaths == null || _projectMeshAssetPaths.Count == 0)
            {
                GUILayout.BeginVertical();
                GUILayout.Label(
                    "Assets 内未找到 FBX / GLB / OBJ，请将模型导入工程后刷新本窗口。",
                    CommonStyles.HelpBoxStyle
                );
                GUILayout.EndVertical();
            }
            else
            {
                GUILayout.BeginVertical();
                EditorGUI.BeginChangeCheck();
                _selectedProjectMeshPopupIndex = EditorGUILayout.Popup(
                    _selectedProjectMeshPopupIndex,
                    _projectMeshPopupOptions
                );
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyProjectMeshFromPopup(_selectedProjectMeshPopupIndex);
                    context.Repaint();
                }
                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(sectionSpacing);

            if (!string.IsNullOrEmpty(_uploadedFilePath))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("已选择文件:", CommonStyles.SmallGreyLabelStyle);
                GUILayout.Label(_uploadedFileName, CommonStyles.ContentStyle);
                if (GUILayout.Button("×", GUILayout.Width(EditorUiScale.S(20f))))
                {
                    ClearFileUploadSelection();
                    context.Repaint();
                }
                GUILayout.EndHorizontal();
            }

            GameObject preview =
                !string.IsNullOrEmpty(_uploadedModelAssetPath)
                    ? AssetDatabase.LoadAssetAtPath<GameObject>(_uploadedModelAssetPath)
                    : null;
            if (preview != null)
            {
                GUILayout.Space(sectionSpacing);
                GUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(preview, typeof(GameObject), false, GUILayout.Height(EditorUiScale.S(40f)));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(sectionSpacing);
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                uiLayout.fileUploadHint ?? "支持 FBX、GLB、OBJ 格式的 3D 模型文件",
                CommonStyles.HelpBoxStyle
            );
            GUILayout.EndHorizontal();
        }

        private void ClearFileUploadSelection()
        {
            _uploadedFilePath = "";
            _uploadedFileName = "";
            _uploadedModelAssetPath = "";
            _selectedProjectMeshPopupIndex = 0;
        }

        private void ApplyProjectMeshFromPopup(int popupIndex)
        {
            if (popupIndex <= 0)
            {
                ClearFileUploadSelection();
                return;
            }

            int assetIdx = popupIndex - 1;
            if (
                _projectMeshAssetPaths == null
                || assetIdx < 0
                || assetIdx >= _projectMeshAssetPaths.Count
            )
            {
                ClearFileUploadSelection();
                return;
            }

            string assetPath = _projectMeshAssetPaths[assetIdx];
            _uploadedModelAssetPath = assetPath;
            _uploadedFilePath = PathUtils.ToAbsoluteAssetPath(assetPath);
            _uploadedFileName = Path.GetFileName(assetPath);
        }

        private void RefreshProjectMeshModelsForUpload()
        {
            if (
                _projectMeshAssetPaths != null
                && _projectMeshPopupOptions != null
                && !s_forceProjectMeshRescan
            )
                return;

            s_forceProjectMeshRescan = false;
            _projectMeshAssetPaths = PathUtils.FindMeshModelAssetPathsInAssets();
            int n = _projectMeshAssetPaths.Count;
            _projectMeshPopupOptions = new string[n + 1];
            _projectMeshPopupOptions[0] = "— 选择项目内模型 —";
            for (int i = 0; i < n; i++)
                _projectMeshPopupOptions[i + 1] = _projectMeshAssetPaths[i];

            SyncProjectMeshPopupIndexFromUploadState();
        }

        private void SyncProjectMeshPopupIndexFromUploadState()
        {
            if (_projectMeshAssetPaths == null || _projectMeshAssetPaths.Count == 0)
            {
                _selectedProjectMeshPopupIndex = 0;
                return;
            }

            if (string.IsNullOrEmpty(_uploadedFilePath) || !File.Exists(_uploadedFilePath))
            {
                _selectedProjectMeshPopupIndex = 0;
                return;
            }

            string rel =
                !string.IsNullOrEmpty(_uploadedModelAssetPath)
                    ? _uploadedModelAssetPath
                    : PathUtils.TryGetAssetsRelativePathFromAbsolute(_uploadedFilePath);

            if (string.IsNullOrEmpty(rel))
            {
                _selectedProjectMeshPopupIndex = 0;
                return;
            }

            int idx = _projectMeshAssetPaths.FindIndex(p =>
                string.Equals(p, rel, StringComparison.OrdinalIgnoreCase)
            );
            _selectedProjectMeshPopupIndex = idx >= 0 ? idx + 1 : 0;
        }

        private void RefreshConvertibleGlbFiles()
        {
            _convertibleGlbFiles = TJGeneratorsHistoryManager.GetConvertibleGlbFiles();

            if (_convertibleGlbFiles != null && _convertibleGlbFiles.Count > 0)
            {
                _convertibleGlbDisplayNames = new string[_convertibleGlbFiles.Count];
                for (int i = 0; i < _convertibleGlbFiles.Count; i++)
                {
                    var item = _convertibleGlbFiles[i];
                    string fileName = Path.GetFileName(item.modelPath);
                    string prompt = string.IsNullOrEmpty(item.prompt) ? "" : $" ({item.prompt})";
                    if (prompt.Length > 20)
                        prompt = prompt.Substring(0, 17) + "...)";
                    _convertibleGlbDisplayNames[i] = fileName + prompt;
                }

                // 如果当前选择无效，重置
                if (_selectedGlbIndex < 0 || _selectedGlbIndex >= _convertibleGlbFiles.Count)
                {
                    // 尝试根据当前路径找到对应的索引
                    if (!string.IsNullOrEmpty(_sourceGlbPath))
                    {
                        _selectedGlbIndex = _convertibleGlbFiles.FindIndex(f =>
                            f.modelPath == _sourceGlbPath
                        );
                    }
                }
            }
            else
            {
                _convertibleGlbDisplayNames = new string[0];
                _selectedGlbIndex = -1;
            }
        }

        private void DrawMultiViewArea(IGenerationPipelineHost context)
        {
            // 统一间距常量
            float labelControlSpacing = EditorUiScale.S(6f);
            float sectionSpacing = CommonStyles.Space1;
            GUILayout.Space(sectionSpacing);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var uiLayout = _config.uiLayout ?? new UILayoutConfig();
            GUILayout.Label(uiLayout.multiViewHint, CommonStyles.SmallGreyLabelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(sectionSpacing);

            // 4个图片上传槽位 - 居中排列（单层 Horizontal + FlexibleSpace 避免嵌套导致 EndLayoutGroup 错位）
            float multiViewBoxSize = EditorUiScale.S(70f);
            float multiViewSpacing = EditorUiScale.S(15f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            for (int i = 0; i < 4; i++)
            {
                DrawMultiViewSlot(context, i, multiViewBoxSize);
                if (i < 3)
                    GUILayout.Space(multiViewSpacing);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // 标签
            GUILayout.Space(labelControlSpacing);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            for (int i = 0; i < 4; i++)
            {
                GUILayout.Label(
                    s_multiViewFooterLabels[i],
                    CommonStyles.SmallGreyCenterLabelStyle,
                    GUILayout.Width(multiViewBoxSize)
                );
                if (i < 3)
                    GUILayout.Space(multiViewSpacing);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawMultiViewSlot(IGenerationPipelineHost context, int index, float boxSize)
        {
            // 确保列表足够大
            while (_multiViewPaths.Count <= index)
                _multiViewPaths.Add(null);
            while (_multiViewImages.Count <= index)
                _multiViewImages.Add(null);

            GUILayout.BeginVertical(GUILayout.Width(boxSize));

            Rect boxRect = GUILayoutUtility.GetRect(boxSize, boxSize);

            // 绘制背景框
            var bgTexture =
                index == 0 ? CommonStyles.MultiViewSelectedTexture : CommonStyles.MultiViewTexture;
            GUI.DrawTexture(boxRect, bgTexture, ScaleMode.StretchToFill);

            // 计算清除按钮位置
            Rect clearRect = Rect.zero;
            if (_multiViewImages[index] != null)
            {
                float clearBtnSize = EditorUiScale.S(16f);
                clearRect = new Rect(
                    boxRect.x + boxRect.width - clearBtnSize - EditorUiScale.S(2f),
                    boxRect.y + EditorUiScale.S(2f),
                    clearBtnSize,
                    clearBtnSize
                );
            }

            // 处理点击事件
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (clearRect != Rect.zero && clearRect.Contains(evt.mousePosition))
                {
                    _multiViewPaths[index] = null;
                    _multiViewImages[index] = null;
                    evt.Use();
                    context.Repaint();
                }
                else if (boxRect.Contains(evt.mousePosition))
                {
                    evt.Use();
                    int slotIndex = index;
                    EditorApplication.delayCall += () =>
                    {
                        while (_multiViewPaths.Count <= slotIndex)
                            _multiViewPaths.Add(null);
                        while (_multiViewImages.Count <= slotIndex)
                            _multiViewImages.Add(null);
                        string path = EditorUtility.OpenFilePanel(
                            $"选择{s_multiViewPickerTitleLabels[slotIndex]}图片",
                            "",
                            "jpg,png"
                        );
                        if (!string.IsNullOrEmpty(path))
                        {
                            _multiViewPaths[slotIndex] = path;
                            _multiViewImages[slotIndex] = new Texture2D(2, 2);
                            _multiViewImages[slotIndex].LoadImage(File.ReadAllBytes(path));
                            context.Repaint();
                        }
                    };
                }
            }

            // 绘制图片预览或加号
            if (_multiViewImages[index] != null)
            {
                float padding = 3f;
                Rect previewRect = new Rect(
                    boxRect.x + padding,
                    boxRect.y + padding,
                    boxRect.width - padding * 2,
                    boxRect.height - padding * 2
                );
                GUI.DrawTexture(previewRect, _multiViewImages[index], ScaleMode.ScaleToFit);

                GUI.Label(clearRect, CommonStyles.ClearButtonSymbol, CommonStyles.ClearButtonStyle);
            }
            else
            {
                GUI.Label(boxRect, CommonStyles.PlusSymbol, CommonStyles.PlusStyle);
            }

            GUILayout.EndVertical();
        }

        #endregion

        #region 端点与输入模式

        private void UpdateEndpointForInputMode()
        {
            // Update endpoint key based on current input mode
            _currentEndpointKey = GetEndpointKeyForInputMode(_currentInputMode);
        }

        private static string GetEndpointKeyForInputMode(string inputMode)
        {
            return inputMode switch
            {
                "multiview" => "multiview",
                "image" => "image",
                _ => "text",
            };
        }

        #endregion

        #region 验证

        private bool TryValidateMultiViewInputs(out string errorMessage)
        {
            if (_multiViewCount <= 0 || _multiViewPaths == null)
            {
                errorMessage =
                    $"至少需要{GetMultiViewMinRequired()}张图片进行多视图生成";
                return false;
            }
            if (
                _multiViewPaths.Count == 0
                || string.IsNullOrEmpty(_multiViewPaths[0])
                || !File.Exists(_multiViewPaths[0])
            )
            {
                errorMessage = "正面图片是必需的，且文件必须存在。";
                return false;
            }
            int uploadedCount = 0;
            for (int i = 0; i < _multiViewPaths.Count; i++)
            {
                if (
                    !string.IsNullOrEmpty(_multiViewPaths[i])
                    && File.Exists(_multiViewPaths[i])
                )
                    uploadedCount++;
            }
            if (uploadedCount < _multiViewMinRequired)
            {
                errorMessage =
                    uploadedCount > 0
                        ? $"多视图至少需要{_multiViewMinRequired}张图片"
                        : "请上传多视图图片";
                return false;
            }
            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 是否有参考图输入（路径已填或多图列表非空）。与 <see cref="IsTextToModel"/> 的语义不同，勿混用。
        /// </summary>
        private bool HasReferenceImageInput() =>
            !string.IsNullOrEmpty(_imagePath)
            || (_imagePaths != null && _imagePaths.Count > 0);

        /// <summary>
        /// 文生图/图生图等：后端要求必须有用户输入的提示词，不能只传参考图或仅依赖模板前缀。
        /// </summary>
        private bool OutputTypeRequiresUserTextPrompt()
        {
            string ot = _config?.outputType;
            return string.Equals(ot, "image", StringComparison.OrdinalIgnoreCase);
        }

        public override bool ValidateInputs(out string errorMessage)
        {
            var uiLayout = _config.uiLayout ?? new UILayoutConfig();
            if (uiLayout.showMultiView)
                _multiViewMinRequired = GetMultiViewMinRequired();

            if (
                ShouldShowEnableMotionUi()
                && _addMotionEnabled
                && string.IsNullOrWhiteSpace(_motionDescription)
            )
            {
                errorMessage = "已勾选添加动作，请输入动作描述";
                return false;
            }

            // GLB选择器模式
            if (uiLayout.showGlbSelector)
            {
                if (string.IsNullOrEmpty(_sourceGlbPath))
                {
                    errorMessage = "请选择源GLB文件";
                    return false;
                }

                if (string.IsNullOrEmpty(_sourceGlbUrl))
                {
                    errorMessage = "未找到该GLB文件的服务器URL，只有通过AI生成的GLB模型才能转换";
                    return false;
                }

                errorMessage = null;
                return true;
            }

            // 文件上传模式
            if (uiLayout.showFileUpload)
            {
                if (string.IsNullOrEmpty(_uploadedFilePath))
                {
                    errorMessage = "请选择要绑骨的模型文件";
                    return false;
                }

                if (!File.Exists(_uploadedFilePath))
                {
                    errorMessage = "选择的文件不存在";
                    return false;
                }

                errorMessage = null;
                return true;
            }

            var (isMultiViewOnly, isDualMode) = ResolveUILayoutModes(uiLayout);

            // 双模式：仅校验当前选中模式的输入
            if (isDualMode)
            {
                if (_primaryInputMode == "multiview")
                    return TryValidateMultiViewInputs(out errorMessage);
                // textOrImage：仅校验文生/图生
                bool hasText = !string.IsNullOrWhiteSpace(_textPrompt);
                bool hasImage = HasReferenceImageInput();
                if (OutputTypeRequiresUserTextPrompt())
                {
                    if (!hasText)
                    {
                        errorMessage = "请输入提示词";
                        return false;
                    }
                }
                else if (!hasText && !hasImage)
                {
                    errorMessage = "请输入提示词或上传图片";
                    return false;
                }
                errorMessage = null;
                return true;
            }

            // 仅多视图：只校验多视图
            if (isMultiViewOnly)
                return TryValidateMultiViewInputs(out errorMessage);

            // Rodin：须存在有效提示词或磁盘上可读的参考图（与自动 conditionMode 一致；fuse 仅在有图无文时）
            if (DynamicRequestJsonBuilder.IsRodinGenerator(_config))
            {
                var requestCtx = CreateRequestBuildContext();
                bool hasText = !string.IsNullOrWhiteSpace(
                    DynamicRequestJsonBuilder.BuildEnhancedPrompt(requestCtx)
                );
                bool hasValidImage = DynamicRequestJsonBuilder.RodinHasAnyValidReferenceImage(
                    requestCtx
                );
                if (!hasText && !hasValidImage)
                {
                    errorMessage = "请输入提示词或上传有效的参考图片";
                    return false;
                }
                errorMessage = null;
                return true;
            }

            // 仅文生/图生：校验文本或图片
            bool hasTextCheck = !string.IsNullOrWhiteSpace(_textPrompt);
            bool hasImageCheck = HasReferenceImageInput();
            bool hasMultiView = false;
            if (_multiViewCount > 0)
            {
                int uploadedCount = 0;
                for (int i = 0; i < _multiViewPaths.Count; i++)
                {
                    if (!string.IsNullOrEmpty(_multiViewPaths[i]))
                        uploadedCount++;
                }
                if (uploadedCount >= _multiViewMinRequired)
                    hasMultiView = true;
                else if (uploadedCount > 0)
                {
                    errorMessage = $"多视图至少需要{_multiViewMinRequired}张图片";
                    return false;
                }
            }

            if (OutputTypeRequiresUserTextPrompt())
            {
                if (!hasTextCheck)
                {
                    errorMessage = "请输入提示词";
                    return false;
                }
            }
            else if (!hasTextCheck && !hasImageCheck && !hasMultiView)
            {
                errorMessage = "请输入提示词或上传图片";
                return false;
            }

            errorMessage = null;
            return true;
        }

        #endregion

        #region API请求构建（动态JSON）

        private DynamicRequestBuildContext CreateRequestBuildContext()
        {
            return new DynamicRequestBuildContext(
                _config,
                _textPrompt,
                _selectedType,
                _selectedStyle,
                _selectedPromptTemplate,
                _imagePath,
                _imagePaths,
                _multiViewPaths,
                _multiViewCount,
                _currentInputMode,
                _parameterValues,
                _extraRawJsonFields,
                _sourceGlbUrl
            );
        }

        public override object BuildRequestData()
        {
            var uiLayout = _config.uiLayout ?? new UILayoutConfig();

            // 文件上传模式：返回MultipartRequestData
            if (uiLayout.showFileUpload && !string.IsNullOrEmpty(_uploadedFilePath))
            {
                return new MultipartRequestData
                {
                    FilePath = _uploadedFilePath,
                    FileName = _uploadedFileName,
                    FileFieldName = "file",
                    AdditionalFields = null,
                };
            }

            return new DynamicRequestData
            {
                JsonContent = DynamicRequestJsonBuilder.BuildRequestJson(
                    CreateRequestBuildContext()
                ),
            };
        }

        #endregion

        #region 响应解析（委托给 DynamicTaskResponseResolver）

        private DynamicTaskResponseContext CreateResponseContext()
        {
            return new DynamicTaskResponseContext(
                _config,
                _parameterValues,
                _sourceGlbPath,
                GeneratorId
            );
        }

        public override string GetDownloadUrl(TJTaskStatusResponse response) =>
            DynamicTaskResponseResolver.GetDownloadUrl(CreateResponseContext(), response);

        public override string[] GetDownloadUrls(TJTaskStatusResponse response) =>
            DynamicTaskResponseResolver.GetDownloadUrls(CreateResponseContext(), response);

        public override string GetPreviewImageUrl(TJTaskStatusResponse response) =>
            DynamicTaskResponseResolver.GetPreviewImageUrl(CreateResponseContext(), response);

        public override string GetRenderedImageUrl(TJTaskStatusResponse response) =>
            DynamicTaskResponseResolver.GetRenderedImageUrl(CreateResponseContext(), response);

        public override string GetAnimationUrl(TJTaskStatusResponse response) =>
            DynamicTaskResponseResolver.GetAnimationUrl(CreateResponseContext(), response);

        public override string GetWalkingAnimationUrl(TJTaskStatusResponse response) =>
            DynamicTaskResponseResolver.GetWalkingAnimationUrl(CreateResponseContext(), response);

        public override string GetRunningAnimationUrl(TJTaskStatusResponse response) =>
            DynamicTaskResponseResolver.GetRunningAnimationUrl(CreateResponseContext(), response);

        public override string GetModelFileName() =>
            DynamicTaskResponseResolver.GetModelFileName(CreateResponseContext());

        #endregion

        #region 输出类型、Motion UI 状态与流水线配置

        public override string GetOutputType() => _pipelineSettings.GetOutputType();

        public string AudioFormat => _pipelineSettings.AudioFormat;

        public override bool GetAddMotionEnabled() =>
            ShouldShowEnableMotionUi() && _addMotionEnabled;

        public override string GetMotionDescription() => _motionDescription ?? "";

        public override PipelineSettings GetPipelineSettings() => _pipelineSettings;

        #endregion

        #region 任务恢复

        public override InterruptedTaskData CreateInterruptedTaskData(
            string backendTaskId,
            string targetAssetGuid
        )
        {
            return new InterruptedTaskData
            {
                backendTaskId = backendTaskId,
                localTaskId = CurrentGeneratingTaskId,
                prompt = _textPrompt,
                imagePath = GetImagePath(),
                modelVersion = _config.id,
                isTextToModel = IsTextToModel(),
                timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                sessionId = TJGeneratorsTaskRecovery.SessionId,
                targetAssetGuid = targetAssetGuid ?? "",
                convertToFBX = _pipelineSettings.NeedsConversion(),
                status = "pending",
            };
        }

        #endregion

        #region 历史记录

        public override string GetPrompt() => _textPrompt;

        public override string GetImagePath() =>
            (_imagePaths != null && _imagePaths.Count > 0) ? _imagePaths[0] : _imagePath;

        public override string GetModelVersion() => _config.id;

        public override bool IsTextToModel() =>
            (_imagePaths == null || _imagePaths.Count == 0) && string.IsNullOrEmpty(_imagePath);

        #endregion
    }
}
#endif
