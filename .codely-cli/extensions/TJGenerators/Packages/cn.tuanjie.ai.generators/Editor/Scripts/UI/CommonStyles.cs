#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace TJGenerators.UI
{
    /// <summary>
    /// 各生成器窗口共用的样式与纹理（headerStyle、buttonStyle、textFieldStyle 等），
    /// 供各生成器窗口统一使用，避免重复定义与视觉不一致。
    /// </summary>
    public static class CommonStyles
    {
        private const string TextureBasePath = "Packages/cn.tuanjie.ai.generators/Editor/EditorTextures/";
        private const string FontBasePath = "Packages/cn.tuanjie.ai.generators/Editor/Fonts/";
        private const string SourceHanSansRegularFontName = "SourceHanSansSC-Regular.otf";
        private const string SourceHanSansMediumFontName = "SourceHanSansSC-Medium.otf";

        /// <summary>窗口背景色 #222222</summary>
        public static readonly Color WindowBackgroundColor = new Color(34f / 255f, 34f / 255f, 34f / 255f, 1f);
        /// <summary>空区域背景色 #161616</summary>
        public static readonly Color EmptyAreaBackgroundColor = new Color(22f / 255f, 22f / 255f, 22f / 255f, 1f);
        /// <summary>布局分割线颜色 #333333</summary>
        public static readonly Color LayoutSeparatorColor = new Color(51f / 255f, 51f / 255f, 51f / 255f, 1f);
        /// <summary>字体颜色 #333333</summary>
        public static readonly Color FontGrayColor = new Color(156f / 255f, 163f / 255f, 175f / 255f, 1f);
        /// <summary>内容颜色 #D1D5DB</summary>
        public static readonly Color FontContentColor = new Color(209f / 255f, 213f / 255f, 219f / 255f, 1f);
        /// <summary>字体浅绿色 #008060</summary>
        public static readonly Color FontLightGreenColor = new Color(0.35f, 0.78f, 0.72f, 1f);
        /// <summary>提示文字颜色 #6A717F</summary>
        public static readonly Color FontHintColor = new Color(106f / 255f, 113f / 255f, 127f / 255f, 1f);
        /// <summary>主题浅绿色 #0FC596</summary>
        public static readonly Color ThemeLightGreenColor = new Color(15f / 255f, 197f / 255f, 150f / 255f, 1f);
        /// <summary>主题主色 #01A77F</summary>
        public static readonly Color ThemeGreenColor = new Color(1f / 255f, 167f / 255f, 127f / 255f, 1f);
        /// <summary>主题深色 #006F4F</summary>
        public static readonly Color ThemeDarkGreenColor = new Color(0f / 255f, 111f / 255f, 79f / 255f, 1f);
        /// <summary>警告色 #FFA500</summary>
        public static readonly Color ThemeOrangeColor = new Color(1f, 0.7f, 0.3f, 1f);
        /// <summary>灰色 #888888</summary>
        public static readonly Color ThemeGreyColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        /// <summary>生成按钮文字字号（“生成”“生成中...”），随 <see cref="EditorUiScale"/> 缩放。</summary>
        public static int GenerateButtonFontSize => EditorUiScale.Font(12);
        /// <summary>生成按钮文字字重 </summary>
        public static FontStyle GenerateButtonFontStyle = FontStyle.Normal;
        /// <summary>清除按钮符号（图片/多视图槽位）</summary>
        public static readonly string ClearButtonSymbol = "×";
        /// <summary>加号占位符号（空槽位）</summary>
        public static readonly string PlusSymbol = "+";
        /// <summary>行之间的垂直间距（像素）</summary>
        public static float LineSpacing => EditorUiScale.S(20f);
        /// <summary>区块之间的垂直间距（像素），用于生成按钮等</summary>
        public static float SectionSpacing => EditorUiScale.S(32f);
        /// <summary>统一间距节奏 1x（11px 设计值）</summary>
        public static float Space1 => EditorUiScale.S(11f);
        /// <summary>统一间距节奏 2x（22px 设计值）</summary>
        public static float Space2 => EditorUiScale.S(22f);
        /// <summary>统一间距节奏 3x（33px 设计值）</summary>
        public static float Space3 => EditorUiScale.S(33f);

        /// <summary>主生成窗口（Sprite / Sequence / Skybox / 3D / Music）的最小尺寸</summary>
        public static Vector2 MainWindowMinSize =>
            new Vector2(EditorUiScale.S(400f), EditorUiScale.S(800f));

        // ========== 两列自适应布局（设计值经 <see cref="EditorUiScale"/> 缩放）==========

        /// <summary>历史面板最小宽度</summary>
        public static float MinHistoryPanelWidth => EditorUiScale.S(260f);
        /// <summary>左侧面板固定宽度</summary>
        public static float LeftPanelWidth => EditorUiScale.S(400f);
        /// <summary>生成器窗口左侧参数栏固定外宽（500）</summary>
        public static float LeftPanelFixedWidth => EditorUiScale.S(500f);
        /// <summary>左侧内容区内边距</summary>
        public static float LeftContentPadding => EditorUiScale.S(16f);
        /// <summary>左侧组件内容宽度（与 <see cref="LeftPanelFixedWidth"/>、内边距匹配）</summary>
        public static float LeftComponentWidth => EditorUiScale.S(468f);
        /// <summary>切换为垂直布局的窗口宽度阈值</summary>
        public static float LayoutSwitchThreshold => EditorUiScale.S(650f);
        /// <summary>外边距</summary>
        public static float OuterMargin => EditorUiScale.S(10f);
        /// <summary>两列面板第一行元素距离顶部的间距</summary>
        public static float FirstRowTopMargin => EditorUiScale.S(30f);
        /// <summary>设置面板左右内边距</summary>
        public static int PanelPadding => EditorUiScale.Ro(30f);

        /// <summary>
        /// ScrollView 内纵向滚动条占用的横向宽度（条贴在视口右侧）。固定常量在高分屏或不同 Unity 皮肤下容易偏小，导致最右列被压住。
        /// </summary>
        public static float ScrollViewVerticalScrollbarReserveForLayout
        {
            get
            {
                var vs = GUI.skin.verticalScrollbar;
                float fw = (vs != null && vs.fixedWidth > 0.5f) ? vs.fixedWidth : 15f;
                int marginH = vs != null ? (vs.margin.left + vs.margin.right) : 4;
                return EditorUiScale.S(Mathf.Max(26f, fw + marginH + 8f));
            }
        }

        /// <summary>
        /// 历史栏外层宽度减去左右 <see cref="PanelPadding"/>（预览等在 ScrollView 上方、不受滚动条侵占宽度的区域）。
        /// </summary>
        public static float HistoryPanelInnerWidth(float panelOuterWidth)
        {
            float w = panelOuterWidth - PanelPadding * 2f;
            return Mathf.Max(EditorUiScale.S(120f), w);
        }

        /// <summary>
        /// 历史栏内 ScrollView 子内容排版宽度（再扣纵向滚动条占位；网格必须与之一致）。
        /// </summary>
        public static float HistoryScrollViewLayoutWidth(float panelOuterWidth)
        {
            float w = panelOuterWidth - PanelPadding * 2f - ScrollViewVerticalScrollbarReserveForLayout;
            return Mathf.Max(EditorUiScale.S(120f), w);
        }

        private static GUIStyle _settingsPanelStyle;
        private static GUIStyle _historyPanelStyleHorizontal;
        private static GUIStyle _historyPanelStyleVertical;
        private static GUIStyle _windowContentStyle;
        private static float _stylesScaleSnapshot = float.NaN;

        /// <summary>
        /// 在 <see cref="EditorUiScale"/> 变更后丢弃缓存的 GUIStyle，下次访问时按新比例重建。
        /// </summary>
        public static void InvalidateGuiStylesForScaleChange()
        {
            _stylesScaleSnapshot = float.NaN;
            ClearBuiltGuiStyles();
        }

        private static void ClearBuiltGuiStyles()
        {
            _settingsPanelStyle = null;
            _historyPanelStyleHorizontal = null;
            _historyPanelStyleVertical = null;
            _windowContentStyle = null;
            _textStyle = null;
            _headerStyle = null;
            _modelNameStyle = null;
            _contentStyle = null;
            _linkStyle = null;
            _greenButtonStyle = null;
            _buttonStyle = null;
            _textFieldStyle = null;
            _searchTextFieldStyle = null;
            _placeholderStyle = null;
            _imageUploadAreaStyle = null;
            _balanceStyle = null;
            _bottomStatusBarCreditsStyle = null;
            _separatorStyle = null;
            _gapLineStyle = null;
            _statusStyle = null;
            _progressLabelStyle = null;
            _historyTileStyle = null;
            _historyTileSelectedStyle = null;
            _historyLabelStyle = null;
            _generateButtonSolidStyle = null;
            _generateButtonHollowStyle = null;
            _generateButtonBusyStyle = null;
            _centeredGreyMiniLabelStyleSmall = null;
            _centeredGreyLabelStyle = null;
            _smallGreyCenterLabelStyle = null;
            _smallGreyLeftLabelStyle = null;
            _SmallGreenLeftLabelStyle = null;
            _smallGreyLabelStyle = null;
            _placeholderTitleStyle = null;
            _hintLabelStyle = null;
            _clearButtonStyle = null;
            _plusStyle = null;
            _helpBoxStyle = null;
            _miniRedLabelStyle = null;
            _warningLabelStyle = null;
            _pinIconStyle = null;
            _sectionTitleStyle = null;
            _targetPrefabHeaderStyle = null;
            _targetPrefabNameStyle = null;
            _targetObjectButtonLabelStyle = null;
            _checkboxBoxStyle = null;
            _checkboxRowLabelStyle = null;
            _modelSelectButtonStyle = null;
            _modelSelectNameStyle = null;
            _modelSelectActionStyle = null;
            _uploadFrameNormalStyle = null;
            _uploadFrameHoverStyle = null;
            _uploadFrameUploadedStyle = null;
            _uploadTitleStyle = null;
            _uploadHintStyle = null;
            _uploadNoImageStyle = null;
            _uploadAIGenLinkStyle = null;
            _promptInputNormalStyle = null;
            _promptInputHoverStyle = null;
            _promptInputTextStyle = null;
            _promptInputPlaceholderStyle = null;
            _dropDownTriggerStyle = null;
            _dropDownPanelStyle = null;
            _dropDownRowTextStyle = null;
            _advancedFoldoutTitleStyle = null;
            _advancedInputTextStyle = null;
            _profileNameStyle = null;
            _profileEmailStyle = null;
        }

        /// <summary>
        /// 设置面板容器样式（带顶部间距的透明容器）
        /// </summary>
        public static GUIStyle SettingsPanelStyle
        {
            get
            {
                if (_settingsPanelStyle == null)
                {
                    _settingsPanelStyle = new GUIStyle
                    {
                        padding = new RectOffset(PanelPadding, PanelPadding, EditorUiScale.Ro(30f), 0)
                    };
                }
                return _settingsPanelStyle;
            }
        }

        /// <summary>
        /// 独立窗口内容容器样式（带四周内边距）
        /// </summary>
        public static GUIStyle WindowContentStyle
        {
            get
            {
                if (_windowContentStyle == null)
                {
                    _windowContentStyle = new GUIStyle
                    {
                        padding = new RectOffset(PanelPadding, PanelPadding, EditorUiScale.Ro(30f), PanelPadding)
                    };
                }
                return _windowContentStyle;
            }
        }

        /// <summary>
        /// 历史面板容器样式（根据布局模式设置间距）
        /// </summary>
        public static GUIStyle GetHistoryPanelStyle(bool isVerticalLayout)
        {
            if (isVerticalLayout)
            {
                if (_historyPanelStyleVertical == null)
                {
                    _historyPanelStyleVertical = new GUIStyle
                    {
                        padding = new RectOffset(PanelPadding, PanelPadding, EditorUiScale.Ro(30f), 0)
                    };
                }
                return _historyPanelStyleVertical;
            }
            else
            {
                if (_historyPanelStyleHorizontal == null)
                {
                    _historyPanelStyleHorizontal = new GUIStyle
                    {
                        padding = new RectOffset(PanelPadding, PanelPadding, EditorUiScale.Ro(30f), 0)
                    };
                }
                return _historyPanelStyleHorizontal;
            }
        }

        private static Texture2D _textFieldTexture;
        private static Texture2D _imageUploadAreaTexture;
        private static Texture2D _imagePreviewTexture;
        private static Texture2D _separationLineTexture;
        private static Texture2D _modelPreviewTexture;
        private static Texture2D _searchTextFieldTexture;
        private static Texture2D _searchIconTexture;
        private static Texture2D _gapLineTexture;
        private static Texture2D _historyTileDarkTexture;
        private static Texture2D _historyTileSelectedTexture;
        private static Texture2D _buttonNormalTexture;
        private static Texture2D _buttonHoverTexture;
        private static Texture2D _buttonActiveTexture;
        private static Texture2D _multiViewTexture;
        private static Texture2D _multiViewSelectedTexture;
        private static Texture2D _checkboxOffNormalTexture;
        private static Texture2D _checkboxOffHoverTexture;
        private static Texture2D _checkboxOnNormalTexture;
        private static Texture2D _checkboxOnHoverTexture;
        private static Texture2D _targetObjectIconTexture;
        private static Texture2D _modelSelectFrameTexture;
        private static Texture2D _modelSelectArrowTexture;
        private static Texture2D _uploadFrameNormalTexture;
        private static Texture2D _uploadFrameHoverTexture;
        private static Texture2D _uploadFrameUploadedTexture;
        private static Texture2D _inputBoxNormalTexture;
        private static Texture2D _inputBoxHoverTexture;
        private static Texture2D _uploadImageIconTexture;
        private static Texture2D _arrowGreenTexture;
        private static Texture2D _dropDownFrameTexture;
        private static Texture2D _dropDownFrameSelectedTexture;
        private static Texture2D _dropDownArrowTexture;
        private static Texture2D _advancedFoldoutArrowTexture;
        private static Texture2D _advancedInputBoxTexture;
        private static Texture2D _profileIconTexture;
        private static Texture2D _itemBoxNormalTexture;
        private static Texture2D _itemBoxCheckedTexture;
        private static Texture2D _favoriteIconNormalTexture;
        private static Texture2D _favoriteIconCheckedTexture;

        private static GUIStyle _textStyle;
        private static GUIStyle _headerStyle;
        private static GUIStyle _modelNameStyle;
        private static GUIStyle _contentStyle;
        private static GUIStyle _linkStyle;
        private static GUIStyle _greenButtonStyle;
        private static GUIStyle _buttonStyle;
        private static GUIStyle _textFieldStyle;
        private static GUIStyle _searchTextFieldStyle;
        private static GUIStyle _placeholderStyle;
        private static GUIStyle _imageUploadAreaStyle;
        private static GUIStyle _balanceStyle;
        private static GUIStyle _bottomStatusBarCreditsStyle;
        private static GUIStyle _separatorStyle;
        private static GUIStyle _gapLineStyle;
        private static GUIStyle _statusStyle;
        private static GUIStyle _progressLabelStyle;
        private static GUIStyle _historyTileStyle;
        private static GUIStyle _historyTileSelectedStyle;
        private static GUIStyle _historyLabelStyle;
        private static GUIStyle _generateButtonSolidStyle;
        private static GUIStyle _generateButtonHollowStyle;
        private static GUIStyle _generateButtonBusyStyle;
        private static GUIStyle _centeredGreyMiniLabelStyleSmall;
        private static GUIStyle _centeredGreyLabelStyle;
        private static GUIStyle _smallGreyCenterLabelStyle;
        private static GUIStyle _smallGreyLeftLabelStyle;
        private static GUIStyle _SmallGreenLeftLabelStyle;
        private static GUIStyle _smallGreyLabelStyle;
        private static GUIStyle _placeholderTitleStyle;
        private static GUIStyle _hintLabelStyle;
        private static GUIStyle _clearButtonStyle;
        private static GUIStyle _plusStyle;
        private static GUIStyle _helpBoxStyle;
        private static GUIStyle _miniRedLabelStyle;
        private static GUIStyle _warningLabelStyle;
        private static GUIStyle _pinIconStyle;
        private static GUIStyle _sectionTitleStyle;
        private static GUIStyle _targetPrefabHeaderStyle;
        private static GUIStyle _targetPrefabNameStyle;
        private static GUIStyle _targetObjectButtonLabelStyle;
        private static GUIStyle _checkboxBoxStyle;
        private static GUIStyle _checkboxRowLabelStyle;
        private static GUIStyle _modelSelectButtonStyle;
        private static GUIStyle _modelSelectNameStyle;
        private static GUIStyle _modelSelectActionStyle;
        private static GUIStyle _uploadFrameNormalStyle;
        private static GUIStyle _uploadFrameHoverStyle;
        private static GUIStyle _uploadFrameUploadedStyle;
        private static GUIStyle _uploadTitleStyle;
        private static GUIStyle _uploadHintStyle;
        private static GUIStyle _uploadNoImageStyle;
        private static GUIStyle _uploadAIGenLinkStyle;
        private static GUIStyle _promptInputNormalStyle;
        private static GUIStyle _promptInputHoverStyle;
        private static GUIStyle _promptInputTextStyle;
        private static GUIStyle _promptInputPlaceholderStyle;
        private static GUIStyle _dropDownTriggerStyle;
        private static GUIStyle _dropDownPanelStyle;
        private static GUIStyle _dropDownRowTextStyle;
        private static GUIStyle _advancedFoldoutTitleStyle;
        private static GUIStyle _advancedInputTextStyle;
        private static GUIStyle _profileNameStyle;
        private static GUIStyle _profileEmailStyle;
        private static Texture2D _generateButtonBusyTexture;
        private static Texture2D _generateButtonNormalTexture;
        private static Texture2D _generateButtonHoverTexture;
        private static Texture2D _generateButtonActiveTexture;
        private static Texture2D _generateButtonIcon;
        private static Texture2D _generateButtonIconGreen;
        private static Texture2D _generateCostIconTexture;
        private static Texture2D _blackButtonNormalTexture;
        private static Texture2D _balancePillTexture;
        private static Texture2D _greenButtonTexture;
        private static Font _sourceHanSansRegularFont;
        private static Font _sourceHanSansMediumFont;

        public static Texture2D CreateSolidColorTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void EnsureTextures()
        {
            if (_textFieldTexture != null) return;
            _textFieldTexture = EditorGUIUtility.Load(TextureBasePath + "TextToModelInput.png") as Texture2D;
            _imageUploadAreaTexture = EditorGUIUtility.Load(TextureBasePath + "ImageUpload.png") as Texture2D;
            _imagePreviewTexture = EditorGUIUtility.Load(TextureBasePath + "PreviewImageDefault.png") as Texture2D;
            _separationLineTexture = EditorGUIUtility.Load(TextureBasePath + "SeparationLine.png") as Texture2D;
            _modelPreviewTexture = EditorGUIUtility.Load(TextureBasePath + "modelPreviewBackground.png") as Texture2D;
            _searchTextFieldTexture = EditorGUIUtility.Load(TextureBasePath + "Frames/search_input_box_4x.png") as Texture2D;
            _searchIconTexture = EditorGUIUtility.Load(TextureBasePath + "Icons/search_icon_4x.png") as Texture2D;
            _gapLineTexture = EditorGUIUtility.Load(TextureBasePath + "Frames/GapLine.png") as Texture2D;
            _generateButtonIcon = EditorGUIUtility.Load(TextureBasePath + "GenerateIcon.png") as Texture2D;
            _generateButtonIconGreen = EditorGUIUtility.Load(TextureBasePath + "GenerateIconGreen.png") as Texture2D;
            _generateButtonNormalTexture = EditorGUIUtility.Load(TextureBasePath + "Button/green_btn_normal_4x.png") as Texture2D;
            _generateButtonHoverTexture = EditorGUIUtility.Load(TextureBasePath + "Button/green_btn_hover_4x.png") as Texture2D;
            _generateButtonActiveTexture = EditorGUIUtility.Load(TextureBasePath + "Button/green_btn_pressed_4x.png") as Texture2D;
            _generateButtonBusyTexture = EditorGUIUtility.Load(TextureBasePath + "Button/green_btn_pressed_4x.png") as Texture2D;
            _generateCostIconTexture = EditorGUIUtility.Load(TextureBasePath + "Icons/cost_icon.png") as Texture2D;
            _blackButtonNormalTexture = EditorGUIUtility.Load(TextureBasePath + "Button/black_btn_normal_4x.png") as Texture2D;
            _balancePillTexture = EditorGUIUtility.Load(TextureBasePath + "Transparent.png") as Texture2D;
            _greenButtonTexture = EditorGUIUtility.Load(TextureBasePath + "GreenButton.png") as Texture2D;
            _multiViewTexture = EditorGUIUtility.Load(TextureBasePath + "MutiViewBox.png") as Texture2D;
            _multiViewSelectedTexture = EditorGUIUtility.Load(TextureBasePath + "MutiViewSelectedBox.png") as Texture2D;
            _checkboxOffNormalTexture = EditorGUIUtility.Load(TextureBasePath + "Checkbox/CheckboxOffNormal.png") as Texture2D;
            _checkboxOffHoverTexture = EditorGUIUtility.Load(TextureBasePath + "Checkbox/CheckboxOffHover.png") as Texture2D;
            _checkboxOnNormalTexture = EditorGUIUtility.Load(TextureBasePath + "Checkbox/CheckboxOnNormal.png") as Texture2D;
            _checkboxOnHoverTexture = EditorGUIUtility.Load(TextureBasePath + "Checkbox/CheckboxOnHover.png") as Texture2D;
            _targetObjectIconTexture = EditorGUIUtility.Load(TextureBasePath + "Icons/TargetIcon.png") as Texture2D;
            _modelSelectFrameTexture = EditorGUIUtility.Load(TextureBasePath + "Frames/ModelSelectFrame.png") as Texture2D;
            _modelSelectArrowTexture = EditorGUIUtility.Load(TextureBasePath + "Icons/arrow_drop_down_icon_2x.png") as Texture2D;
            _uploadFrameNormalTexture = EditorGUIUtility.Load(TextureBasePath + "Frames/upload_frame_normal_2x.png") as Texture2D;
            _uploadFrameHoverTexture = EditorGUIUtility.Load(TextureBasePath + "Frames/upload_frame_hover_2x.png") as Texture2D;
            _uploadFrameUploadedTexture = EditorGUIUtility.Load(TextureBasePath + "Frames/upload_frame_uploaded_2x.png") as Texture2D;
            _inputBoxNormalTexture = EditorGUIUtility.Load(TextureBasePath + "Frames/input_box_normal_2x.png") as Texture2D;
            _inputBoxHoverTexture = EditorGUIUtility.Load(TextureBasePath + "Frames/input_box_hover_2x.png") as Texture2D;
            _uploadImageIconTexture = EditorGUIUtility.Load(TextureBasePath + "Icons/UploadImageIcon.png") as Texture2D;
            _arrowGreenTexture = EditorGUIUtility.Load(TextureBasePath + "Icons/arrow_green_4x.png") as Texture2D;
            _dropDownFrameTexture = EditorGUIUtility.Load(TextureBasePath + "Frames/drop_down_frame_2x.png") as Texture2D;
            _dropDownFrameSelectedTexture = EditorGUIUtility.Load(TextureBasePath + "Frames/drop_down_frame_selected_2x.png") as Texture2D;
            _dropDownArrowTexture = EditorGUIUtility.Load(TextureBasePath + "Icons/drop_box_arrow_4x.png") as Texture2D;
            _advancedFoldoutArrowTexture = EditorGUIUtility.Load(TextureBasePath + "Icons/drop_box_right_arrow_4x.png") as Texture2D;
            _advancedInputBoxTexture = EditorGUIUtility.Load(TextureBasePath + "Frames/setting_Input_box_2x.png") as Texture2D;
            _profileIconTexture = EditorGUIUtility.Load(TextureBasePath + "Icons/profile_icon_2x.png") as Texture2D;
            _itemBoxNormalTexture = EditorGUIUtility.Load(TextureBasePath + "Frames/item_box_normal_4x.png") as Texture2D;
            _itemBoxCheckedTexture = EditorGUIUtility.Load(TextureBasePath + "Frames/item_box_checked_4x.png") as Texture2D;
            _favoriteIconNormalTexture = EditorGUIUtility.Load(TextureBasePath + "Icons/favorite_icon_normal_4x.png") as Texture2D;
            _favoriteIconCheckedTexture = EditorGUIUtility.Load(TextureBasePath + "Icons/favorite_icon_checked_4x.png") as Texture2D;

            _historyTileDarkTexture = EditorGUIUtility.Load(TextureBasePath + "TileTexture.png") as Texture2D;
            _historyTileSelectedTexture = EditorGUIUtility.Load(TextureBasePath + "TileTextureSelected.png") as Texture2D;
            _buttonNormalTexture = CreateSolidColorTexture(new Color(0.4f, 0.4f, 0.4f, 1f));
            _buttonHoverTexture = CreateSolidColorTexture(ThemeGreyColor);
            _buttonActiveTexture = CreateSolidColorTexture(new Color(0.35f, 0.35f, 0.35f, 1f));

            // 中文字体：思源黑体 Regular / Medium
            _sourceHanSansRegularFont = AssetDatabase.LoadAssetAtPath<Font>(FontBasePath + SourceHanSansRegularFontName);
            if (_sourceHanSansRegularFont == null)
                _sourceHanSansRegularFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Editor/Fonts/" + SourceHanSansRegularFontName);
            _sourceHanSansMediumFont = AssetDatabase.LoadAssetAtPath<Font>(FontBasePath + SourceHanSansMediumFontName);
            if (_sourceHanSansMediumFont == null)
                _sourceHanSansMediumFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Editor/Fonts/" + SourceHanSansMediumFontName);
        }

        private static void EnsureStyles()
        {
            EnsureTextures();
            if (_buttonStyle != null && Mathf.Approximately(_stylesScaleSnapshot, EditorUiScale.Scale))
                return;
            ClearBuiltGuiStyles();

            _textStyle = new GUIStyle
            {
                font = _sourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(12),
                fontStyle = FontStyle.Normal,
            };

            _buttonStyle = new GUIStyle(_textStyle)
            {
                fixedHeight = EditorUiScale.S(30f),
                alignment = TextAnchor.MiddleCenter,
            };
            _buttonStyle.normal.background = _buttonNormalTexture;
            _buttonStyle.hover.background = _buttonHoverTexture;
            _buttonStyle.active.background = _buttonActiveTexture;
            _buttonStyle.focused.background = _buttonNormalTexture;
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.hover.textColor = Color.white;
            _buttonStyle.active.textColor = Color.white;
            _buttonStyle.focused.textColor = Color.white;

            _generateButtonSolidStyle = new GUIStyle(_buttonStyle)
            {
                fixedHeight = EditorUiScale.S(40f),
                imagePosition = ImagePosition.ImageOnly,
                fontSize = EditorUiScale.Font(18),
                fontStyle = FontStyle.Bold,
                font = _sourceHanSansMediumFont != null ? _sourceHanSansMediumFont : _sourceHanSansRegularFont,
                margin = new RectOffset(0, 0, EditorUiScale.Ro(12f), EditorUiScale.Ro(12f)),
                padding = new RectOffset(EditorUiScale.Ro(33f), EditorUiScale.Ro(33f), EditorUiScale.Ro(8f), EditorUiScale.Ro(8f)),
                border = new RectOffset(EditorUiScale.Ro(32f), EditorUiScale.Ro(32f), EditorUiScale.Ro(32f), EditorUiScale.Ro(32f)),
                alignment = TextAnchor.MiddleCenter
            };
            _generateButtonSolidStyle.normal.background = _generateButtonNormalTexture;
            _generateButtonSolidStyle.hover.background = _generateButtonHoverTexture;
            _generateButtonSolidStyle.active.background = _generateButtonActiveTexture;
            _generateButtonSolidStyle.focused.background = _generateButtonNormalTexture;
            _generateButtonSolidStyle.normal.textColor = Color.white;
            _generateButtonSolidStyle.hover.textColor = Color.white;
            _generateButtonSolidStyle.active.textColor = Color.white;
            _generateButtonSolidStyle.focused.textColor = Color.white;

            

            _generateButtonHollowStyle = new GUIStyle(_generateButtonSolidStyle)
            {
                normal = { textColor = ThemeGreenColor, background = _greenButtonTexture},
                hover = { textColor = ThemeGreenColor, background = _greenButtonTexture},
                active = { textColor = ThemeGreenColor, background = _greenButtonTexture},
                focused = { textColor = ThemeGreenColor, background = _greenButtonTexture},
            };

            _generateButtonBusyStyle = new GUIStyle(_generateButtonSolidStyle);
            _generateButtonBusyStyle.normal.background = _generateButtonBusyTexture;
            _generateButtonBusyStyle.hover.background = _generateButtonBusyTexture;
            _generateButtonBusyStyle.active.background = _generateButtonBusyTexture;
            _generateButtonBusyStyle.focused.background = _generateButtonBusyTexture;

            _textFieldStyle = new GUIStyle(_textStyle)
            {
                fixedHeight = EditorUiScale.S(35f),
                normal = { background = _textFieldTexture, textColor = Color.white },
                padding = new RectOffset(EditorUiScale.Ro(10f), EditorUiScale.Ro(10f), EditorUiScale.Ro(5f), EditorUiScale.Ro(5f)),
                margin = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            _textFieldStyle.hover.textColor = Color.white;
            _textFieldStyle.active.textColor = Color.white;
            _textFieldStyle.focused.textColor = Color.white;

            _searchTextFieldStyle = new GUIStyle(_textFieldStyle)
            {
                fixedHeight = EditorUiScale.S(44f),
                normal = { background = _searchTextFieldTexture, textColor = Color.white },
                border = new RectOffset(EditorUiScale.Ro(16f), EditorUiScale.Ro(16f), EditorUiScale.Ro(16f), EditorUiScale.Ro(16f)),
                padding = new RectOffset(EditorUiScale.Ro(10f), EditorUiScale.Ro(10f), EditorUiScale.Ro(10f), EditorUiScale.Ro(10f)),
            };

            _placeholderStyle = new GUIStyle(_textStyle)
            {
                normal = { textColor = Color.gray },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(EditorUiScale.Ro(10f), EditorUiScale.Ro(10f), EditorUiScale.Ro(10f), EditorUiScale.Ro(10f)),
            };

            _imageUploadAreaStyle = new GUIStyle
            {
                normal = { background = _imageUploadAreaTexture },
                padding = new RectOffset(EditorUiScale.Ro(10f), EditorUiScale.Ro(10f), EditorUiScale.Ro(8f), EditorUiScale.Ro(8f)),
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = EditorUiScale.S(98f),
                font = _sourceHanSansRegularFont,
            };

            _headerStyle = new GUIStyle(_textStyle)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = FontGrayColor },
            };

            _modelNameStyle = new GUIStyle(_textStyle)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = FontContentColor },
            };

            _contentStyle = new GUIStyle(_textStyle)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = FontContentColor },
            };

            _linkStyle = new GUIStyle(_textStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = ThemeGreenColor}
            };

            _greenButtonStyle = new GUIStyle
            {
                font = _sourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(12),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(EditorUiScale.Ro(10f), EditorUiScale.Ro(10f), 0, 0),
                normal = { textColor = ThemeGreenColor, background = _greenButtonTexture },
            };

            _balanceStyle = new GUIStyle(_textStyle)
            {
                normal = { textColor = FontLightGreenColor},
            };

            _bottomStatusBarCreditsStyle = new GUIStyle(_textStyle)
            {
                font = _sourceHanSansMediumFont != null ? _sourceHanSansMediumFont : _sourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(14),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip,
                normal = { textColor = ThemeGreenColor },
            };

            _separatorStyle = new GUIStyle
            {
                fixedHeight = Mathf.Max(1f, EditorUiScale.S(1f)),
                margin = new RectOffset(EditorUiScale.Ro(20f), EditorUiScale.Ro(20f), EditorUiScale.Ro(20f), EditorUiScale.Ro(20f))
            };
            _separatorStyle.normal.background = _separationLineTexture;

            _gapLineStyle = new GUIStyle
            {
                fixedHeight = Mathf.Max(1f, EditorUiScale.S(2f)),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                stretchWidth = true
            };
            _gapLineStyle.normal.background = _gapLineTexture;

            _statusStyle = new GUIStyle
            {
                fontSize = EditorUiScale.Font(11),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 1f) },
                padding = new RectOffset(EditorUiScale.Ro(5f), EditorUiScale.Ro(5f), EditorUiScale.Ro(2f), EditorUiScale.Ro(2f)),
                font = _sourceHanSansRegularFont
            };

            _progressLabelStyle = new GUIStyle
            {
                fontSize = EditorUiScale.Font(10),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold,
                font = _sourceHanSansRegularFont
            };

            _historyTileStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                font = _sourceHanSansRegularFont,
                normal = { background = _historyTileDarkTexture }
            };

            _historyTileSelectedStyle = new GUIStyle(_historyTileStyle)
            {
                normal = { background = _historyTileSelectedTexture }
            };

            _centeredGreyMiniLabelStyleSmall = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                font = _sourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(10)
            };
            _centeredGreyLabelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                font = _sourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(12)
            };

            _smallGreyCenterLabelStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ThemeGreyColor },
                fontSize = EditorUiScale.Font(9),
                font = _sourceHanSansRegularFont
            };
            _smallGreyLeftLabelStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                normal = { textColor = ThemeGreyColor },
                fontSize = EditorUiScale.Font(10),
                font = _sourceHanSansRegularFont
            };
            _SmallGreenLeftLabelStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                normal = { textColor = ThemeLightGreenColor },
                fontSize = EditorUiScale.Font(9),
                font = _sourceHanSansRegularFont
            };
            _placeholderTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = EditorUiScale.Font(30),
                normal = { textColor = FontContentColor },
                font = _sourceHanSansRegularFont
            };
            _hintLabelStyle = new GUIStyle(_textStyle)
            {
                normal = { textColor = FontHintColor },
                fontSize = EditorUiScale.Font(9),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
            };
            // 不居中的小号灰字（思源黑体 Regular），用于列表内左对齐提示等
            _smallGreyLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ThemeGreyColor },
                fontSize = EditorUiScale.Font(10),
                font = _sourceHanSansRegularFont
            };
            _clearButtonStyle = new GUIStyle()
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = EditorUiScale.Font(16),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _plusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = EditorUiScale.Font(22),
                normal = { textColor = ThemeGreyColor }
            };
            _helpBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = EditorUiScale.Font(10),
                wordWrap = true
            };
            _miniRedLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                font = _sourceHanSansRegularFont,
                normal = { textColor = Color.red }
            };
            _warningLabelStyle = new GUIStyle(EditorStyles.label)
            {
                font = _sourceHanSansRegularFont,
                normal = { textColor = ThemeOrangeColor }
            };
            _historyLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = EditorUiScale.Font(10),
                normal = { textColor = Color.white },
                hover = { textColor = Color.white },
                font = _sourceHanSansRegularFont
            };
            _pinIconStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = EditorUiScale.Font(15),
                alignment = TextAnchor.MiddleCenter
            };

            _sectionTitleStyle = new GUIStyle(_textStyle)
            {
                font = _sourceHanSansMediumFont != null ? _sourceHanSansMediumFont : _sourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(15),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            _sectionTitleStyle.normal.textColor = Color.white;
            _sectionTitleStyle.hover.textColor = Color.white;
            _sectionTitleStyle.active.textColor = Color.white;
            _sectionTitleStyle.focused.textColor = Color.white;

            _advancedFoldoutTitleStyle = new GUIStyle(_textStyle)
            {
                font = _sourceHanSansMediumFont != null ? _sourceHanSansMediumFont : _sourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(14),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            _advancedFoldoutTitleStyle.normal.textColor = FontGrayColor;
            _advancedFoldoutTitleStyle.hover.textColor = FontGrayColor;
            _advancedFoldoutTitleStyle.active.textColor = FontGrayColor;
            _advancedFoldoutTitleStyle.focused.textColor = FontGrayColor;

            _advancedInputTextStyle = new GUIStyle(_textStyle)
            {
                font = _sourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(12),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(EditorUiScale.Ro(10f), EditorUiScale.Ro(10f), 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0)
            };
            _advancedInputTextStyle.normal.textColor = Color.white;
            _advancedInputTextStyle.hover.textColor = Color.white;
            _advancedInputTextStyle.active.textColor = Color.white;
            _advancedInputTextStyle.focused.textColor = Color.white;

            _profileNameStyle = new GUIStyle(_textStyle)
            {
                font = _sourceHanSansMediumFont != null ? _sourceHanSansMediumFont : _sourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(14),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                clipping = TextClipping.Clip
            };
            _profileNameStyle.normal.textColor = new Color(1f, 1f, 1f, 0.60f);

            _profileEmailStyle = new GUIStyle(_textStyle)
            {
                font = _sourceHanSansMediumFont != null ? _sourceHanSansMediumFont : _sourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(11),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                clipping = TextClipping.Clip
            };
            _profileEmailStyle.normal.textColor = new Color(1f, 1f, 1f, 0.45f);

            _targetPrefabHeaderStyle = new GUIStyle(_textStyle)
            {
                fontStyle = FontStyle.Normal,
                fontSize = EditorUiScale.Font(14),
                alignment = TextAnchor.MiddleLeft
            };
            _targetPrefabHeaderStyle.normal.textColor = new Color(1f, 1f, 1f, 0.25f);
            _targetPrefabHeaderStyle.hover.textColor = _targetPrefabHeaderStyle.normal.textColor;
            _targetPrefabHeaderStyle.active.textColor = _targetPrefabHeaderStyle.normal.textColor;
            _targetPrefabHeaderStyle.focused.textColor = _targetPrefabHeaderStyle.normal.textColor;

            _targetPrefabNameStyle = new GUIStyle(_textStyle)
            {
                fontStyle = FontStyle.Normal,
                fontSize = EditorUiScale.Font(26),
                alignment = TextAnchor.MiddleLeft
            };
            _targetPrefabNameStyle.normal.textColor = Color.white;
            _targetPrefabNameStyle.hover.textColor = Color.white;
            _targetPrefabNameStyle.active.textColor = Color.white;
            _targetPrefabNameStyle.focused.textColor = Color.white;

            _targetObjectButtonLabelStyle = new GUIStyle(_textStyle)
            {
                font = _sourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(14),
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            var targetLabelColor = new Color(1f, 1f, 1f, 0.6f);
            _targetObjectButtonLabelStyle.normal.textColor = targetLabelColor;
            _targetObjectButtonLabelStyle.hover.textColor = targetLabelColor;
            _targetObjectButtonLabelStyle.active.textColor = targetLabelColor;
            _targetObjectButtonLabelStyle.focused.textColor = targetLabelColor;
            _targetObjectButtonLabelStyle.onNormal.textColor = targetLabelColor;
            _targetObjectButtonLabelStyle.onHover.textColor = targetLabelColor;
            _targetObjectButtonLabelStyle.onActive.textColor = targetLabelColor;
            _targetObjectButtonLabelStyle.onFocused.textColor = targetLabelColor;

            float checkboxDesignSize = EditorUiScale.S(30f);
            _checkboxBoxStyle = new GUIStyle
            {
                stretchWidth = false,
                stretchHeight = false,
                fixedWidth = checkboxDesignSize,
                fixedHeight = checkboxDesignSize,
                margin = new RectOffset(0, EditorUiScale.Ro(6f), EditorUiScale.Ro(2f), EditorUiScale.Ro(2f)),
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
                overflow = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };
            _checkboxBoxStyle.normal.background = _checkboxOffNormalTexture;
            _checkboxBoxStyle.hover.background = _checkboxOffHoverTexture != null ? _checkboxOffHoverTexture : _checkboxOffNormalTexture;
            _checkboxBoxStyle.active.background = _checkboxOffHoverTexture != null ? _checkboxOffHoverTexture : _checkboxOffNormalTexture;
            _checkboxBoxStyle.focused.background = _checkboxOffHoverTexture != null ? _checkboxOffHoverTexture : _checkboxOffNormalTexture;
            _checkboxBoxStyle.onNormal.background = _checkboxOnNormalTexture != null ? _checkboxOnNormalTexture : _checkboxOffNormalTexture;
            _checkboxBoxStyle.onHover.background = _checkboxOnHoverTexture != null ? _checkboxOnHoverTexture : _checkboxOnNormalTexture;
            _checkboxBoxStyle.onActive.background = _checkboxOnHoverTexture != null ? _checkboxOnHoverTexture : _checkboxOnNormalTexture;
            _checkboxBoxStyle.onFocused.background = _checkboxOnHoverTexture != null ? _checkboxOnHoverTexture : _checkboxOnNormalTexture;

            _checkboxRowLabelStyle = new GUIStyle(_textStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                margin = new RectOffset(0, 0, EditorUiScale.Ro(2f), EditorUiScale.Ro(2f)),
                stretchWidth = true
            };
            _checkboxRowLabelStyle.normal.textColor = FontContentColor;
            _checkboxRowLabelStyle.hover.textColor = FontContentColor;
            _checkboxRowLabelStyle.active.textColor = FontContentColor;
            _checkboxRowLabelStyle.focused.textColor = FontContentColor;
            _checkboxRowLabelStyle.onNormal.textColor = FontContentColor;
            _checkboxRowLabelStyle.onHover.textColor = FontContentColor;
            _checkboxRowLabelStyle.onActive.textColor = FontContentColor;
            _checkboxRowLabelStyle.onFocused.textColor = FontContentColor;

            _modelSelectButtonStyle = new GUIStyle
            {
                fixedHeight = EditorUiScale.S(56f),
                padding = new RectOffset(EditorUiScale.Ro(16f), EditorUiScale.Ro(16f), EditorUiScale.Ro(14f), EditorUiScale.Ro(14f)),
                margin = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleLeft,
                border = new RectOffset(EditorUiScale.Ro(8f), EditorUiScale.Ro(8f), EditorUiScale.Ro(8f), EditorUiScale.Ro(8f))
            };
            _modelSelectButtonStyle.normal.background = _modelSelectFrameTexture;
            _modelSelectButtonStyle.hover.background = _modelSelectFrameTexture;
            _modelSelectButtonStyle.active.background = _modelSelectFrameTexture;
            _modelSelectButtonStyle.focused.background = _modelSelectFrameTexture;

            _modelSelectNameStyle = new GUIStyle(_textStyle)
            {
                fontSize = EditorUiScale.Font(16),
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            _modelSelectNameStyle.normal.textColor = Color.white;
            _modelSelectNameStyle.hover.textColor = Color.white;
            _modelSelectNameStyle.active.textColor = Color.white;
            _modelSelectNameStyle.focused.textColor = Color.white;

            _modelSelectActionStyle = new GUIStyle(_textStyle)
            {
                fontSize = EditorUiScale.Font(14),
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip
            };
            var actionColor = new Color(164f / 255f, 164f / 255f, 164f / 255f, 1f);
            _modelSelectActionStyle.normal.textColor = actionColor;
            _modelSelectActionStyle.hover.textColor = actionColor;
            _modelSelectActionStyle.active.textColor = actionColor;
            _modelSelectActionStyle.focused.textColor = actionColor;

            _uploadFrameNormalStyle = new GUIStyle
            {
                fixedHeight = EditorUiScale.S(210f),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(EditorUiScale.Ro(4f), EditorUiScale.Ro(4f), EditorUiScale.Ro(4f), EditorUiScale.Ro(4f)),
                stretchWidth = true
            };
            _uploadFrameNormalStyle.normal.background = _uploadFrameNormalTexture;
            _uploadFrameNormalStyle.hover.background = _uploadFrameNormalTexture;
            _uploadFrameNormalStyle.active.background = _uploadFrameNormalTexture;
            _uploadFrameNormalStyle.focused.background = _uploadFrameNormalTexture;

            _uploadFrameHoverStyle = new GUIStyle(_uploadFrameNormalStyle);
            _uploadFrameHoverStyle.normal.background = _uploadFrameHoverTexture != null ? _uploadFrameHoverTexture : _uploadFrameNormalTexture;
            _uploadFrameHoverStyle.hover.background = _uploadFrameHoverStyle.normal.background;
            _uploadFrameHoverStyle.active.background = _uploadFrameHoverStyle.normal.background;
            _uploadFrameHoverStyle.focused.background = _uploadFrameHoverStyle.normal.background;

            _uploadFrameUploadedStyle = new GUIStyle(_uploadFrameNormalStyle);
            _uploadFrameUploadedStyle.normal.background = _uploadFrameUploadedTexture != null ? _uploadFrameUploadedTexture : _uploadFrameNormalTexture;
            _uploadFrameUploadedStyle.hover.background = _uploadFrameUploadedStyle.normal.background;
            _uploadFrameUploadedStyle.active.background = _uploadFrameUploadedStyle.normal.background;
            _uploadFrameUploadedStyle.focused.background = _uploadFrameUploadedStyle.normal.background;

            _uploadTitleStyle = new GUIStyle(_textStyle)
            {
                fontSize = EditorUiScale.Font(14),
                alignment = TextAnchor.MiddleCenter
            };
            var uploadTitleColor = new Color(200f / 255f, 204f / 255f, 210f / 255f, 1f);
            _uploadTitleStyle.normal.textColor = uploadTitleColor;
            _uploadTitleStyle.hover.textColor = uploadTitleColor;
            _uploadTitleStyle.active.textColor = uploadTitleColor;
            _uploadTitleStyle.focused.textColor = uploadTitleColor;

            _uploadHintStyle = new GUIStyle(_textStyle)
            {
                fontSize = EditorUiScale.Font(12),
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            var uploadHintColor = new Color(102f / 255f, 102f / 255f, 102f / 255f, 1f);
            _uploadHintStyle.normal.textColor = uploadHintColor;
            _uploadHintStyle.hover.textColor = uploadHintColor;
            _uploadHintStyle.active.textColor = uploadHintColor;
            _uploadHintStyle.focused.textColor = uploadHintColor;

            _uploadNoImageStyle = new GUIStyle(_textStyle)
            {
                fontSize = EditorUiScale.Font(14),
                alignment = TextAnchor.MiddleRight
            };
            _uploadNoImageStyle.normal.textColor = uploadHintColor;
            _uploadNoImageStyle.hover.textColor = uploadHintColor;
            _uploadNoImageStyle.active.textColor = uploadHintColor;
            _uploadNoImageStyle.focused.textColor = uploadHintColor;

            _uploadAIGenLinkStyle = new GUIStyle(_textStyle)
            {
                fontSize = EditorUiScale.Font(14),
                alignment = TextAnchor.MiddleLeft
            };
            _uploadAIGenLinkStyle.normal.textColor = ThemeGreenColor;
            _uploadAIGenLinkStyle.hover.textColor = ThemeGreenColor;
            _uploadAIGenLinkStyle.active.textColor = ThemeGreenColor;
            _uploadAIGenLinkStyle.focused.textColor = ThemeGreenColor;

            _promptInputNormalStyle = new GUIStyle
            {
                fixedHeight = 0,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                // 2x 资源对应更大的九宫格切片，避免圆角被拉成切角。
                border = new RectOffset(EditorUiScale.Ro(14f), EditorUiScale.Ro(14f), EditorUiScale.Ro(14f), EditorUiScale.Ro(14f)),
                stretchWidth = true,
                stretchHeight = true
            };
            _promptInputNormalStyle.normal.background = _inputBoxNormalTexture;
            _promptInputNormalStyle.hover.background = _inputBoxNormalTexture;
            _promptInputNormalStyle.active.background = _inputBoxNormalTexture;
            _promptInputNormalStyle.focused.background = _inputBoxNormalTexture;

            _promptInputHoverStyle = new GUIStyle(_promptInputNormalStyle);
            _promptInputHoverStyle.normal.background = _inputBoxHoverTexture != null ? _inputBoxHoverTexture : _inputBoxNormalTexture;
            _promptInputHoverStyle.hover.background = _promptInputHoverStyle.normal.background;
            _promptInputHoverStyle.active.background = _promptInputHoverStyle.normal.background;
            _promptInputHoverStyle.focused.background = _promptInputHoverStyle.normal.background;

            _promptInputTextStyle = new GUIStyle(_textStyle)
            {
                font = _sourceHanSansMediumFont != null ? _sourceHanSansMediumFont : _sourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(14),
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0)
            };
            _promptInputTextStyle.normal.textColor = Color.white;
            _promptInputTextStyle.hover.textColor = Color.white;
            _promptInputTextStyle.active.textColor = Color.white;
            _promptInputTextStyle.focused.textColor = Color.white;

            _promptInputPlaceholderStyle = new GUIStyle(_promptInputTextStyle);
            var promptPlaceholderColor = new Color(102f / 255f, 102f / 255f, 102f / 255f, 1f);
            _promptInputPlaceholderStyle.normal.textColor = promptPlaceholderColor;
            _promptInputPlaceholderStyle.hover.textColor = promptPlaceholderColor;
            _promptInputPlaceholderStyle.active.textColor = promptPlaceholderColor;
            _promptInputPlaceholderStyle.focused.textColor = promptPlaceholderColor;

            _dropDownTriggerStyle = new GUIStyle
            {
                fixedHeight = EditorUiScale.S(30f),
                padding = new RectOffset(EditorUiScale.Ro(12f), EditorUiScale.Ro(12f), EditorUiScale.Ro(8f), EditorUiScale.Ro(8f)),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(EditorUiScale.Ro(8f), EditorUiScale.Ro(8f), EditorUiScale.Ro(8f), EditorUiScale.Ro(8f)),
                stretchWidth = true
            };
            _dropDownTriggerStyle.normal.background = _dropDownFrameTexture;
            _dropDownTriggerStyle.hover.background = _dropDownFrameTexture;
            _dropDownTriggerStyle.active.background = _dropDownFrameTexture;
            _dropDownTriggerStyle.focused.background = _dropDownFrameTexture;

            _dropDownPanelStyle = new GUIStyle
            {
                padding = new RectOffset(0, 0, EditorUiScale.Ro(4f), EditorUiScale.Ro(4f)),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
                stretchWidth = true,
                stretchHeight = true
            };
            _dropDownPanelStyle.normal.background = null;
            _dropDownPanelStyle.hover.background = null;
            _dropDownPanelStyle.active.background = null;
            _dropDownPanelStyle.focused.background = null;

            _dropDownRowTextStyle = new GUIStyle(_textStyle)
            {
                fontSize = EditorUiScale.Font(14),
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            _dropDownRowTextStyle.normal.textColor = Color.white;
            _dropDownRowTextStyle.hover.textColor = Color.white;
            _dropDownRowTextStyle.active.textColor = Color.white;
            _dropDownRowTextStyle.focused.textColor = Color.white;
            _stylesScaleSnapshot = EditorUiScale.Scale;
        }

        /// <summary>
        /// 绘制「自定义方框 + 独立文字」的勾选行。
        /// </summary>
        public static bool DrawCheckboxRow(string label, bool value)
        {
            GUILayout.BeginHorizontal();
            var box = CheckboxBoxStyle;
            float rowH = Mathf.Max(box.fixedHeight, EditorGUIUtility.singleLineHeight);
            bool newValue = GUILayout.Toggle(
                value,
                GUIContent.none,
                box,
                GUILayout.Width(box.fixedWidth),
                GUILayout.Height(rowH));
            GUILayout.Label(label, CheckboxRowLabelStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(rowH));
            GUILayout.EndHorizontal();
            return newValue;
        }

        public static GUIStyle HeaderStyle
        {
            get { EnsureStyles(); return _headerStyle; }
        }

        public static GUIStyle ModelNameStyle
        {
            get { EnsureStyles(); return _modelNameStyle; }
        }

        public static GUIStyle ContentStyle
        {
            get { EnsureStyles(); return _contentStyle; }
        }

        public static GUIStyle LinkStyle
        {
            get { EnsureStyles(); return _linkStyle; }
        }

        public static GUIStyle GreenButtonStyle
        {
            get { EnsureStyles(); return _greenButtonStyle; }
        }

        public static GUIStyle ButtonStyle
        {
            get { EnsureStyles(); return _buttonStyle; }
        }

        public static GUIStyle TextFieldStyle
        {
            get { EnsureStyles(); return _textFieldStyle; }
        }

        /// <summary>搜索框专用（较矮的输入框，fixedHeight 22）</summary>
        public static GUIStyle SearchTextFieldStyle
        {
            get { EnsureStyles(); return _searchTextFieldStyle; }
        }

        public static GUIStyle PlaceholderStyle
        {
            get { EnsureStyles(); return _placeholderStyle; }
        }

        public static GUIStyle ImageUploadAreaStyle
        {
            get { EnsureStyles(); return _imageUploadAreaStyle; }
        }

        public static Texture2D ImagePreviewTexture
        {
            get { EnsureTextures(); return _imagePreviewTexture; }
        }

        public static GUIStyle BalanceStyle
        {
            get { EnsureStyles(); return _balanceStyle; }
        }

        /// <summary>主窗口底部状态栏右侧「点数」文案样式（主题绿、右对齐）</summary>
        public static GUIStyle BottomStatusBarCreditsStyle
        {
            get { EnsureStyles(); return _bottomStatusBarCreditsStyle; }
        }

        public static GUIStyle SeparatorStyle
        {
            get { EnsureStyles(); return _separatorStyle; }
        }

        public static GUIStyle GapLineStyle
        {
            get { EnsureStyles(); return _gapLineStyle; }
        }

        public static GUIStyle StatusStyle
        {
            get { EnsureStyles(); return _statusStyle; }
        }

        public static GUIStyle ProgressLabelStyle
        {
            get { EnsureStyles(); return _progressLabelStyle; }
        }

        public static GUIStyle HistoryTileStyle
        {
            get { EnsureStyles(); return _historyTileStyle; }
        }

        public static GUIStyle HistoryTileSelectedStyle
        {
            get { EnsureStyles(); return _historyTileSelectedStyle; }
        }

        public static GUIStyle HistoryLabelStyle
        {
            get { EnsureStyles();  return _historyLabelStyle; }
        }

        public static GUIStyle GenerateButtonSolidStyle
        {
            get { EnsureStyles(); return _generateButtonSolidStyle; }
        }

        public static GUIStyle GenerateButtonHollowStyle
        {
            get { EnsureStyles(); return _generateButtonHollowStyle; }
        }

        public static GUIStyle GenerateButtonBusyStyle
        {
            get { EnsureStyles(); return _generateButtonBusyStyle; }
        }

        public static GUIStyle CenteredGreyMiniLabelStyleSmall
        {
            get { EnsureStyles(); return _centeredGreyMiniLabelStyleSmall; }
        }

        public static GUIStyle CenteredGreyLabelStyle
        {
            get { EnsureStyles(); return _centeredGreyLabelStyle; }
        }

        public static GUIStyle SmallGreyCenterLabelStyle
        {
            get { EnsureStyles(); return _smallGreyCenterLabelStyle; }
        }

        public static GUIStyle SmallGreyLeftLabelStyle
        {
            get { EnsureStyles(); return _smallGreyLeftLabelStyle; }
        }

        public static GUIStyle SmallGreenLeftLabelStyle
        {
            get { EnsureStyles(); return _SmallGreenLeftLabelStyle; }
        }

        public static GUIStyle PlaceholderTitleStyle
        {
            get { EnsureStyles(); return _placeholderTitleStyle; }
        }

        public static GUIStyle SmallGreyLabelStyle
        {
            get { EnsureStyles(); return _smallGreyLabelStyle; }
        }

        public static GUIStyle HintLabelStyle
        {
            get { EnsureStyles(); return _hintLabelStyle; }
        }

        public static GUIStyle ClearButtonStyle
        {
            get { EnsureStyles(); return _clearButtonStyle; }
        }

        public static GUIStyle PlusStyle
        {
            get { EnsureStyles(); return _plusStyle; }
        }

        public static GUIStyle HelpBoxStyle
        {
            get { EnsureStyles(); return _helpBoxStyle; }
        }

        public static GUIStyle MiniRedLabelStyle
        {
            get { EnsureStyles(); return _miniRedLabelStyle; }
        }

        public static GUIStyle WarningLabelStyle
        {
            get { EnsureStyles(); return _warningLabelStyle; }
        }

        public static GUIStyle PinIconStyle
        {
            get { EnsureStyles(); return _pinIconStyle; }
        }

        public static GUIStyle SectionTitleStyle
        {
            get { EnsureStyles(); return _sectionTitleStyle; }
        }

        public static GUIStyle TargetPrefabHeaderStyle
        {
            get { EnsureStyles(); return _targetPrefabHeaderStyle; }
        }

        public static GUIStyle TargetPrefabNameStyle
        {
            get { EnsureStyles(); return _targetPrefabNameStyle; }
        }

        public static GUIStyle TargetObjectButtonLabelStyle
        {
            get { EnsureStyles(); return _targetObjectButtonLabelStyle; }
        }

        public static GUIStyle CheckboxBoxStyle
        {
            get { EnsureStyles(); return _checkboxBoxStyle; }
        }

        public static GUIStyle CheckboxRowLabelStyle
        {
            get { EnsureStyles(); return _checkboxRowLabelStyle; }
        }

        public static GUIStyle ModelSelectButtonStyle
        {
            get { EnsureStyles(); return _modelSelectButtonStyle; }
        }

        public static GUIStyle ModelSelectNameStyle
        {
            get { EnsureStyles(); return _modelSelectNameStyle; }
        }

        public static GUIStyle ModelSelectActionStyle
        {
            get { EnsureStyles(); return _modelSelectActionStyle; }
        }

        public static GUIStyle UploadFrameNormalStyle
        {
            get { EnsureStyles(); return _uploadFrameNormalStyle; }
        }

        public static GUIStyle UploadFrameHoverStyle
        {
            get { EnsureStyles(); return _uploadFrameHoverStyle; }
        }

        public static GUIStyle UploadFrameUploadedStyle
        {
            get { EnsureStyles(); return _uploadFrameUploadedStyle; }
        }

        public static GUIStyle UploadTitleStyle
        {
            get { EnsureStyles(); return _uploadTitleStyle; }
        }

        public static GUIStyle UploadHintStyle
        {
            get { EnsureStyles(); return _uploadHintStyle; }
        }

        public static GUIStyle UploadNoImageStyle
        {
            get { EnsureStyles(); return _uploadNoImageStyle; }
        }

        public static GUIStyle UploadAIGenLinkStyle
        {
            get { EnsureStyles(); return _uploadAIGenLinkStyle; }
        }

        public static GUIStyle PromptInputNormalStyle
        {
            get { EnsureStyles(); return _promptInputNormalStyle; }
        }

        public static GUIStyle PromptInputHoverStyle
        {
            get { EnsureStyles(); return _promptInputHoverStyle; }
        }

        public static GUIStyle PromptInputTextStyle
        {
            get { EnsureStyles(); return _promptInputTextStyle; }
        }

        /// <summary>多行提示词输入框占位文案（灰色），勿用于 <see cref="EditorGUI.TextField"/> / <see cref="EditorGUI.TextArea"/> 的实际输入。</summary>
        public static GUIStyle PromptInputPlaceholderStyle
        {
            get { EnsureStyles(); return _promptInputPlaceholderStyle; }
        }

        public static GUIStyle DropDownTriggerStyle
        {
            get { EnsureStyles(); return _dropDownTriggerStyle; }
        }

        public static GUIStyle DropDownRowTextStyle
        {
            get { EnsureStyles(); return _dropDownRowTextStyle; }
        }

        public static GUIStyle DropDownPanelStyle
        {
            get { EnsureStyles(); return _dropDownPanelStyle; }
        }

        public static Texture2D GenerateButtonIcon
        {
            get { EnsureTextures(); return _generateButtonIcon; }
        }

        public static Texture2D GenerateButtonIconGreen
        {
            get { EnsureTextures(); return _generateButtonIconGreen; }
        }

        public static Texture2D GenerateCostIconTexture
        {
            get { EnsureTextures(); return _generateCostIconTexture; }
        }

        public static Texture2D BlackButtonNormalTexture
        {
            get { EnsureTextures(); return _blackButtonNormalTexture; }
        }

        public static Texture2D SearchIconTexture
        {
            get { EnsureTextures(); return _searchIconTexture; }
        }

        public static Texture2D MultiViewTexture
        {
            get { EnsureTextures(); return _multiViewTexture; }
        }

        public static Texture2D MultiViewSelectedTexture
        {
            get { EnsureTextures(); return _multiViewSelectedTexture; }
        }

        public static Texture2D TargetObjectIconTexture
        {
            get { EnsureTextures(); return _targetObjectIconTexture; }
        }

        public static Texture2D ModelSelectArrowTexture
        {
            get { EnsureTextures(); return _modelSelectArrowTexture; }
        }

        public static Texture2D UploadImageIconTexture
        {
            get { EnsureTextures(); return _uploadImageIconTexture; }
        }

        public static Texture2D ArrowGreenTexture
        {
            get { EnsureTextures(); return _arrowGreenTexture; }
        }

        public static Texture2D DropDownFrameSelectedTexture
        {
            get { EnsureTextures(); return _dropDownFrameSelectedTexture; }
        }

        public static Texture2D DropDownArrowTexture
        {
            get { EnsureTextures(); return _dropDownArrowTexture; }
        }

        public static Texture2D AdvancedFoldoutArrowTexture
        {
            get { EnsureTextures(); return _advancedFoldoutArrowTexture; }
        }

        public static Texture2D AdvancedInputBoxTexture
        {
            get { EnsureTextures(); return _advancedInputBoxTexture; }
        }

        public static Texture2D ProfileIconTexture
        {
            get { EnsureTextures(); return _profileIconTexture; }
        }

        public static Texture2D ItemBoxNormalTexture
        {
            get { EnsureTextures(); return _itemBoxNormalTexture; }
        }

        public static Texture2D ItemBoxCheckedTexture
        {
            get { EnsureTextures(); return _itemBoxCheckedTexture; }
        }

        public static Texture2D FavoriteIconNormalTexture
        {
            get { EnsureTextures(); return _favoriteIconNormalTexture; }
        }

        public static Texture2D FavoriteIconCheckedTexture
        {
            get { EnsureTextures(); return _favoriteIconCheckedTexture; }
        }

        public static Font SourceHanSansRegularFont
        {
            get { EnsureTextures(); return _sourceHanSansRegularFont; }
        }

        public static GUIStyle AdvancedFoldoutTitleStyle
        {
            get { EnsureStyles(); return _advancedFoldoutTitleStyle; }
        }

        public static GUIStyle AdvancedInputTextStyle
        {
            get { EnsureStyles(); return _advancedInputTextStyle; }
        }

        public static GUIStyle ProfileNameStyle
        {
            get { EnsureStyles(); return _profileNameStyle; }
        }

        public static GUIStyle ProfileEmailStyle
        {
            get { EnsureStyles(); return _profileEmailStyle; }
        }

        public static Font SourceHanSansMediumFont
        {
            get { EnsureTextures(); return _sourceHanSansMediumFont; }
        }

        public static Vector2 GenerateButtonIconSize =>
            new Vector2(EditorUiScale.S(18f), EditorUiScale.S(18f));
    }
}
#endif
