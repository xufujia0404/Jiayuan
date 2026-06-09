#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TJGenerators.Config;
using TJGenerators.Generators;
using TJGenerators.Utils;

namespace TJGenerators.UI
{
    /// <summary>
    /// 各生成器窗口共用的可复用 UI 控件（参数绘制、模型选择器等），供主窗口、Skybox、Music、Sprite 及 DynamicGenerator 统一调用。
    /// </summary>
    public static class UIComponents
    {
        private static readonly Dictionary<string, int> _dropdownPendingSelections = new Dictionary<string, int>();

        private static bool s_ImguiLeftMouseHeld;

        /// <summary>
        /// IMGUI 语义下鼠标左键是否处于按下状态；不访问 <see cref="Input.GetMouseButton"/>，兼容 Player Settings「仅 Input System」。
        /// 使用前应调用 <see cref="SyncImguiLeftMouseHeldFromEvent"/>（或由内部在读取前同步）。
        /// </summary>
        public static bool ImguiLeftMouseHeld => s_ImguiLeftMouseHeld;

        /// <summary>高级参数行统一高度（与折叠内控件对齐）。</summary>
        public static float AdvancedSettingsRowHeight => EditorUiScale.S(33f);

        /// <summary>高级参数行右侧外边距。</summary>
        public static float AdvancedSettingsRowRightPadding => EditorUiScale.S(16f);

        /// <summary>高级参数行右侧控件列宽度。</summary>
        public static float AdvancedSettingsRowControlWidth => EditorUiScale.S(226f);

        public static void SyncImguiLeftMouseHeldFromEvent()
        {
            Event e = Event.current;
            if (e == null)
                return;
            switch (e.rawType)
            {
                case EventType.MouseDown:
                    if (e.button == 0)
                        s_ImguiLeftMouseHeld = true;
                    break;
                case EventType.MouseUp:
                    if (e.button == 0)
                        s_ImguiLeftMouseHeld = false;
                    break;
            }
        }

        /// <summary>占位符仅在“真·空”时绘制，避免 IME 组字阶段与实际输入重叠。</summary>
        private static bool ShouldShowEmptyTextPlaceholder(string currentText)
        {
            if (!string.IsNullOrEmpty(currentText))
                return false;
            if (!string.IsNullOrEmpty(Input.compositionString))
                return false;
            return true;
        }

        public struct AdaptiveLayoutScope : IDisposable
        {
            private readonly bool _isVerticalLayout;
            private bool _beganHorizontal;
            private bool _beganVertical;

            internal AdaptiveLayoutScope(bool isVerticalLayout)
            {
                _isVerticalLayout = isVerticalLayout;
                _beganHorizontal = false;
                _beganVertical = false;
            }

            internal void Begin()
            {
                GUILayout.BeginHorizontal();
                _beganHorizontal = true;
                if (_isVerticalLayout)
                {
                    GUILayout.Space(CommonStyles.OuterMargin);
                    GUILayout.BeginVertical();
                    _beganVertical = true;
                }
            }

            public void Dispose()
            {
                if (_isVerticalLayout && _beganVertical)
                {
                    try
                    {
                        GUILayout.EndVertical();
                        GUILayout.Space(CommonStyles.OuterMargin);
                    }
                    catch
                    {
                        // Swallow IMGUI layout exceptions to avoid cascading "EndLayoutGroup" errors.
                    }
                }

                if (_beganHorizontal)
                {
                    try
                    {
                        GUILayout.EndHorizontal();
                    }
                    catch
                    {
                        // Swallow IMGUI layout exceptions to avoid cascading "EndLayoutGroup" errors.
                    }
                }
            }
        }

        public struct SettingsPanelScope : IDisposable
        {
            private bool _beganOuterVertical;
            private bool _beganScrollView;
            private bool _beganInnerVertical;

            public Vector2 ScrollPosition { get; private set; }

            internal SettingsPanelScope(Vector2 scrollPosition)
            {
                _beganOuterVertical = false;
                _beganScrollView = false;
                _beganInnerVertical = false;
                ScrollPosition = scrollPosition;
            }

            internal void Begin(AdaptiveLayoutParams layout)
            {
                try
                {
                    GUILayout.BeginVertical(
                        GUILayout.Width(layout.LeftPanelWidth),
                        GUILayout.MinWidth(layout.LeftPanelWidth),
                        GUILayout.MaxWidth(layout.LeftPanelWidth)
                    );
                    _beganOuterVertical = true;

                    ScrollPosition = GUILayout.BeginScrollView(
                        ScrollPosition,
                        GUIStyle.none,
                        GUI.skin.verticalScrollbar,
                        GUILayout.Height(layout.SettingsPanelHeight),
                        GUILayout.Width(layout.LeftPanelWidth),
                        GUILayout.MaxWidth(layout.LeftPanelWidth)
                    );
                    _beganScrollView = true;

                    GUILayout.BeginVertical(CommonStyles.SettingsPanelStyle);
                    _beganInnerVertical = true;
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                if (_beganInnerVertical)
                {
                    try
                    {
                        GUILayout.EndVertical();
                    }
                    catch
                    {
                        // Swallow IMGUI layout exceptions to avoid cascading "EndLayoutGroup" errors.
                    }
                    _beganInnerVertical = false;
                }

                if (_beganScrollView)
                {
                    try
                    {
                        GUILayout.EndScrollView();
                    }
                    catch
                    {
                        // Swallow IMGUI layout exceptions to avoid cascading "EndLayoutGroup" errors.
                    }
                    _beganScrollView = false;
                }

                if (_beganOuterVertical)
                {
                    try
                    {
                        GUILayout.EndVertical();
                    }
                    catch
                    {
                        // Swallow IMGUI layout exceptions to avoid cascading "EndLayoutGroup" errors.
                    }
                    _beganOuterVertical = false;
                }
            }
        }

        /// <summary>
        /// 为上一个 GUILayout 控件的矩形区域添加手型光标（用于 LinkStyle 按钮等）。
        /// 应在绘制完控件后立即调用。
        /// </summary>
        public static void AddLinkCursorToLastRect()
        {
            var lastRect = GUILayoutUtility.GetLastRect();
            EditorGUIUtility.AddCursorRect(lastRect, MouseCursor.Link);
        }

        /// <summary>
        /// 绘制链接风格按钮（GreenButtonStyle + 手型光标），点击时执行回调。
        /// </summary>
        /// <param name="text">按钮文案</param>
        /// <param name="onClick">点击时的回调，可为 null</param>
        public static void LinkButton(string text, Action onClick = null)
        {
            if (GUILayout.Button(text, CommonStyles.GreenButtonStyle))
                onClick?.Invoke();
            AddLinkCursorToLastRect();
        }

        /// <summary>
        /// 绘制「透明背景 + 左文案 + 右图标」的小按钮，固定尺寸 110x28。
        /// </summary>
        public static bool DrawTargetObjectButton(string text, Action onClick = null)
        {
            float buttonWidth = EditorUiScale.S(110f);
            float buttonHeight = EditorUiScale.S(28f);
            Rect rect = GUILayoutUtility.GetRect(
                buttonWidth,
                buttonHeight,
                GUILayout.Width(buttonWidth),
                GUILayout.Height(buttonHeight));

            return DrawTargetObjectButtonInRect(rect, text, onClick);
        }

        public static bool DrawTargetObjectButtonInRect(Rect rect, string text, Action onClick = null)
        {
            float edgePadding = EditorUiScale.S(12f);
            float iconDrawSize = EditorUiScale.S(20f);
            float textIconGap = EditorUiScale.S(8f);

            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            if (clicked)
                onClick?.Invoke();

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            var icon = CommonStyles.TargetObjectIconTexture;
            float iconSize = icon != null ? iconDrawSize : 0f;
            float textMaxWidth = rect.width - edgePadding * 2f - iconSize - (iconSize > 0f ? textIconGap : 0f);
            textMaxWidth = Mathf.Max(0f, textMaxWidth);

            Rect textRect = new Rect(rect.x + edgePadding, rect.y, textMaxWidth, rect.height);
            GUI.Label(textRect, text ?? string.Empty, CommonStyles.TargetObjectButtonLabelStyle);

            if (icon != null)
            {
                Rect iconRect = new Rect(
                    rect.xMax - edgePadding - iconDrawSize,
                    rect.y + (rect.height - iconDrawSize) * 0.5f,
                    iconDrawSize,
                    iconDrawSize);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
            }

            return clicked;
        }

        /// <summary>
        /// 绘制一行选择器：标题 + 已选时显示名称与清除/更改按钮，未选时显示“选择XXX”按钮。
        /// 选择按钮统一为 GreenButtonStyle、手型光标；行前 0、行后 5。
        /// </summary>
        /// <param name="headerLabel">标题，如 "纹理走势："</param>
        /// <param name="selectedDisplayName">选中时显示的名称，无选中传 null</param>
        /// <param name="selectButtonText">未选中时按钮文案，如 "选择纹理"</param>
        /// <param name="onClear">点击“清除”时的回调</param>
        /// <param name="onChange">点击“更改”或“选择XXX”时的回调</param>
        public static void DrawSelectorRow(
            string headerLabel,
            string selectedDisplayName,
            string selectButtonText,
            Action onClear,
            Action onChange
        )
        {
            float spaceAfter = EditorUiScale.S(5f);
            GUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(headerLabel))
                GUILayout.Label(headerLabel, CommonStyles.SectionTitleStyle);

            if (!string.IsNullOrEmpty(selectedDisplayName))
            {
                GUILayout.Label(selectedDisplayName, CommonStyles.ContentStyle);
                GUILayout.FlexibleSpace();

                LinkButton("清除", onClear);
                GUILayout.Space(EditorUiScale.S(10f));
                LinkButton("更改", onChange);
            }
            else
            {
                GUILayout.FlexibleSpace();
                LinkButton(selectButtonText, onChange);
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(spaceAfter);
        }

        public static void DrawSeparator()
        {
            GUILayout.Box(GUIContent.none, CommonStyles.SeparatorStyle, GUILayout.ExpandWidth(true));
        }

        /// <summary>
        /// 绘制 2px 高度分割线（不带上下间距），宽度随容器拉伸。
        /// </summary>
        public static void DrawGapLine()
        {
            GUILayout.Box(
                GUIContent.none,
                CommonStyles.GapLineStyle,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(EditorUiScale.S(2f)));
        }

        /// <summary>
        /// 绘制统一风格的窗口头部区域：
        /// 左侧为固定标签，中间由调用方自定义内容（资产名称、未绑定提示等），右侧可选显示点数。
        /// </summary>
        /// <param name="title">左侧标题标签，例如 “目标精灵：”</param>
        /// <param name="drawMiddleContent">中间内容绘制回调（资产名按钮、未绑定文案等）</param>
        /// <param name="showCredits">是否显示点数</param>
        /// <param name="credits">当前点数数值</param>
        public static void DrawHeader(
            string title,
            Action drawMiddleContent,
            bool showCredits,
            int credits
        )
        {
            float headerRowHeight = EditorUiScale.S(26f);
            GUILayout.BeginHorizontal(GUILayout.Height(headerRowHeight));
            GUILayout.BeginVertical(GUILayout.Height(headerRowHeight));
            GUILayout.FlexibleSpace();
            GUILayout.Label(title, CommonStyles.HeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Height(headerRowHeight));
            GUILayout.FlexibleSpace();
            drawMiddleContent?.Invoke();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            if (showCredits)
            {
                GUILayout.BeginVertical(GUILayout.Height(headerRowHeight));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"点数：{credits}", CommonStyles.BalanceStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(CommonStyles.LineSpacing);
        }

        public static void DrawTargetHeaderComposite(
            string title,
            Action<Rect> drawMiddleContent,
            Action onSelectTarget
        )
        {
            float spacing = EditorUiScale.S(CommonStyles.Space1);
            float bottomRowHeight = EditorUiScale.S(34f);
            float buttonWidth = EditorUiScale.S(110f);
            float buttonHeight = EditorUiScale.S(28f);
            float titleH = EditorUiScale.S(20f);
            float totalHeight = titleH + spacing + bottomRowHeight;

            Rect containerRect = GUILayoutUtility.GetRect(0f, totalHeight, GUILayout.ExpandWidth(true), GUILayout.Height(totalHeight));
            Rect titleRect = new Rect(containerRect.x, containerRect.y, containerRect.width, titleH);
            GUI.Label(titleRect, title, CommonStyles.TargetPrefabHeaderStyle);

            Rect rowRect = new Rect(containerRect.x, containerRect.y + titleH + spacing, containerRect.width, bottomRowHeight);
            Rect buttonRect = new Rect(rowRect.xMax - buttonWidth, rowRect.y + (bottomRowHeight - buttonHeight) * 0.5f, buttonWidth, buttonHeight);
            Rect labelRect = new Rect(rowRect.x, rowRect.y, Mathf.Max(0f, buttonRect.xMin - rowRect.x - EditorUiScale.S(16f)), bottomRowHeight);

            drawMiddleContent?.Invoke(labelRect);

            DrawTargetObjectButtonInRect(buttonRect, "选择对象", onSelectTarget);
        }

        /// <summary>
        /// 绘制一行：标签「当前模型：」+ 当前模型名称 + 按钮「选择模型」，点击按钮打开模型选择器窗口。
        /// </summary>
        /// <param name="currentModelName">显示的当前模型名称（如 "未选择" 或具体模型名）</param>
        /// <param name="currentSelectedModel">当前选中的模型信息，传给选择器窗口</param>
        /// <param name="onModelSelected">用户在选择器中确认选择时的回调</param>
        /// <param name="configType">配置类型，决定选择器加载哪类生成器列表</param>
        public static void DrawModelSelector(
            string currentModelName,
            AIModelInfo currentSelectedModel,
            Action<AIModelInfo> onModelSelected,
            ConfigType configType
        )
        {
            const float iconWidth = 30f;
            const float iconHeight = 25f;
            const float iconTextGap = 12f;
            const float rightArrowGap = 10f;
            const float arrowSize = 14f;
            const float contentPaddingX = 16f;
            const float rowHeight = 57f;

            Rect buttonRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                CommonStyles.ModelSelectButtonStyle,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(rowHeight));

            DrawNineSliceBackground(
                buttonRect,
                CommonStyles.ModelSelectButtonStyle.normal.background,
                CommonStyles.ModelSelectButtonStyle.border.left,
                CommonStyles.ModelSelectButtonStyle.normal.background != null ? CommonStyles.ModelSelectButtonStyle.normal.background.height : rowHeight);

            if (GUI.Button(buttonRect, GUIContent.none, GUIStyle.none))
            {
                TJGenerators.TJGeneratorsModelSelectorWindow.ShowWindow(
                    currentSelectedModel,
                    onModelSelected,
                    configType);
            }
            EditorGUIUtility.AddCursorRect(buttonRect, MouseCursor.Link);

            float leftX = buttonRect.x + contentPaddingX;
            float centerY = buttonRect.y + buttonRect.height * 0.5f;

            Texture2D modelIcon = currentSelectedModel?.Icon;
            if (modelIcon != null)
            {
                Rect iconRect = new Rect(leftX, centerY - iconHeight * 0.5f, iconWidth, iconHeight);
                GUI.DrawTexture(iconRect, modelIcon, ScaleMode.ScaleToFit, true);
            }

            float nameX = leftX + iconWidth + iconTextGap;
            Rect nameRect = new Rect(
                nameX,
                buttonRect.y,
                Mathf.Max(80f, buttonRect.width * 0.45f),
                buttonRect.height);
            GUI.Label(nameRect, currentModelName ?? "未选择", CommonStyles.ModelSelectNameStyle);

            string actionText = "切换模型";
            Vector2 actionSize = CommonStyles.ModelSelectActionStyle.CalcSize(new GUIContent(actionText));
            Texture2D arrow = CommonStyles.ModelSelectArrowTexture;
            float rightContentWidth = actionSize.x + rightArrowGap + arrowSize;
            float rightStartX = buttonRect.xMax - contentPaddingX - rightContentWidth;

            Rect actionRect = new Rect(rightStartX, buttonRect.y, actionSize.x, buttonRect.height);
            GUI.Label(actionRect, actionText, CommonStyles.ModelSelectActionStyle);

            if (arrow != null)
            {
                Rect arrowRect = new Rect(
                    actionRect.xMax + rightArrowGap,
                    centerY - arrowSize * 0.5f,
                    arrowSize,
                    arrowSize);
                GUI.DrawTexture(arrowRect, arrow, ScaleMode.ScaleToFit, true);
            }
        }

        /// <summary>
        /// 绘制“文本提示词输入区”：左侧标题 + 单行输入框 + 占位提示，统一使用 CommonStyles.TextFieldStyle/PlaceholderStyle。
        /// </summary>
        /// <param name="title">左侧标题，如“文本提示词”</param>
        /// <param name="placeholder">输入为空时显示的占位文案</param>
        /// <param name="value">当前输入值</param>
        /// <returns>用户输入后的新字符串</returns>
        public static string DrawTextField(string title, string placeholder, string value)
        {
            GUILayout.BeginHorizontal();
            DrawSectionTitle(title ?? "文本提示词", uppercase: false);
            GUILayout.EndHorizontal();

            GUILayout.Space(CommonStyles.Space1);

            return DrawTextInputOnly(placeholder, value);
        }

        /// <summary>
        /// 仅绘制文本输入框本体（不包含标题）。
        /// </summary>
        public static string DrawTextInputOnly(string placeholder, string value)
        {

            GUILayout.BeginHorizontal();
            Rect textFieldRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                CommonStyles.TextFieldStyle,
                GUILayout.ExpandWidth(true)
            );
            string newValue = EditorGUI.TextField(
                textFieldRect,
                value ?? "",
                CommonStyles.TextFieldStyle
            );
            if (ShouldShowEmptyTextPlaceholder(newValue))
                EditorGUI.LabelField(
                    textFieldRect,
                    placeholder ?? "",
                    CommonStyles.PlaceholderStyle
                );
            GUILayout.EndHorizontal();

            return newValue;
        }

        /// <summary>
        /// 绘制可随内容增高的九宫格输入框（normal/hover 两态）。
        /// </summary>
        public static string DrawPromptInputBox(string value, string placeholder, string controlName = "prompt_input_box")
        {
            const float minHeight = 46f;
            const float maxHeight = 200f;
            const float innerPaddingX = 16f;
            const float innerPaddingY = 12f;
            const float horizontalPaddingEstimate = innerPaddingX * 2f;

            string current = value ?? string.Empty;
            string display = string.IsNullOrEmpty(current) ? (placeholder ?? string.Empty) : current;
            float estimatedBoxWidth = Mathf.Max(120f, EditorGUIUtility.currentViewWidth - 80f);
            float estimatedTextWidth = Mathf.Max(80f, estimatedBoxWidth - horizontalPaddingEstimate);
            float styleHeight = CommonStyles.PromptInputTextStyle.CalcHeight(new GUIContent(display), estimatedTextWidth);
            float boxHeight = Mathf.Clamp(styleHeight + innerPaddingY * 2f, minHeight, maxHeight);

            Rect rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                CommonStyles.PromptInputNormalStyle,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(boxHeight));

            bool isHover = rect.Contains(Event.current.mousePosition);
            bool isFocused = GUI.GetNameOfFocusedControl() == controlName;
            GUIStyle bgStyle = (isHover || isFocused) ? CommonStyles.PromptInputHoverStyle : CommonStyles.PromptInputNormalStyle;
            Texture2D promptBg = bgStyle.normal.background;
            float promptRefHeight = promptBg != null ? promptBg.height : minHeight;
            int fixedPromptDestBorder = Mathf.Max(1, Mathf.RoundToInt(bgStyle.border.left * (minHeight / Mathf.Max(1f, promptRefHeight))));
            DrawNineSliceBackground(
                rect,
                promptBg,
                bgStyle.border.left,
                promptRefHeight,
                fixedDestBorder: fixedPromptDestBorder);

            Rect textRect = new Rect(
                rect.x + innerPaddingX,
                rect.y + innerPaddingY,
                Mathf.Max(1f, rect.width - innerPaddingX * 2f),
                Mathf.Max(1f, rect.height - innerPaddingY * 2f));

            GUI.SetNextControlName(controlName);
            string newValue = EditorGUI.TextArea(textRect, current, CommonStyles.PromptInputTextStyle);
            if (ShouldShowEmptyTextPlaceholder(newValue) && !string.IsNullOrEmpty(placeholder))
            {
                GUI.Label(textRect, placeholder, CommonStyles.PromptInputPlaceholderStyle);
            }

            return newValue;
        }

        /// <summary>
        /// 绘制统一小标题组件。
        /// </summary>
        public static void DrawSectionTitle(string title, bool uppercase = true)
        {
            string text = title ?? string.Empty;
            if (uppercase)
                text = text.ToUpperInvariant();
            GUILayout.Label(text, CommonStyles.SectionTitleStyle);
        }

        /// <summary>
        /// 绘制样式化下拉框（触发器 + 独立弹出层）。
        /// </summary>
        public static int DrawStyledDropdown(
            string dropdownId,
            int selectedIndex,
            IList<string> options,
            int separatorBeforeIndex = -1,
            float panelTopGap = 4f,
            float hoverInset = 2f,
            float dropdownWidth = -1f)
        {
            float resolvedWidth = dropdownWidth < 0f ? CommonStyles.LeftComponentWidth : dropdownWidth;
            float triggerHeight = EditorUiScale.S(30f);
            float textPaddingX = EditorUiScale.S(12f);
            float arrowSize = EditorUiScale.S(14f);
            float rightPadding = EditorUiScale.S(12f);
            float scaledPanelTopGap = EditorUiScale.S(panelTopGap);
            float scaledHoverInset = EditorUiScale.S(hoverInset);

            if (options == null || options.Count == 0)
                return selectedIndex;

            if (!string.IsNullOrEmpty(dropdownId) && _dropdownPendingSelections.TryGetValue(dropdownId, out var pending))
            {
                selectedIndex = Mathf.Clamp(pending, 0, options.Count - 1);
                _dropdownPendingSelections.Remove(dropdownId);
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, options.Count - 1);

            Rect triggerRect = resolvedWidth > 0f
                ? GUILayoutUtility.GetRect(
                    GUIContent.none,
                    CommonStyles.DropDownTriggerStyle,
                    GUILayout.Width(resolvedWidth),
                    GUILayout.MinWidth(resolvedWidth),
                    GUILayout.MaxWidth(resolvedWidth),
                    GUILayout.Height(triggerHeight))
                : GUILayoutUtility.GetRect(
                    GUIContent.none,
                    CommonStyles.DropDownTriggerStyle,
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(triggerHeight));

            DrawNineSliceBackground(
                triggerRect,
                CommonStyles.DropDownTriggerStyle.normal.background,
                CommonStyles.DropDownTriggerStyle.border.left,
                CommonStyles.DropDownTriggerStyle.normal.background != null ? CommonStyles.DropDownTriggerStyle.normal.background.height : triggerHeight);

            if (GUI.Button(triggerRect, GUIContent.none, GUIStyle.none))
            {
                Rect popupAnchor = new Rect(triggerRect.x, triggerRect.y + scaledPanelTopGap, triggerRect.width, triggerRect.height);
                PopupWindow.Show(
                    popupAnchor,
                    new StyledDropdownPopup(
                        dropdownId,
                        options,
                        selectedIndex,
                        separatorBeforeIndex,
                        triggerRect.width,
                        Mathf.Max(0f, scaledHoverInset),
                        OnDropdownPopupSelected));
                Event.current.Use();
            }

            Rect textRect = new Rect(
                triggerRect.x + textPaddingX,
                triggerRect.y,
                Mathf.Max(EditorUiScale.S(20f), triggerRect.width - textPaddingX - rightPadding - arrowSize - EditorUiScale.S(8f)),
                triggerRect.height);
            GUI.Label(textRect, options[selectedIndex], CommonStyles.DropDownRowTextStyle);

            var arrow = CommonStyles.DropDownArrowTexture;
            if (arrow != null)
            {
                Rect arrowRect = new Rect(
                    triggerRect.xMax - rightPadding - arrowSize,
                    triggerRect.y + (triggerRect.height - arrowSize) * 0.5f,
                    arrowSize,
                    arrowSize);
                GUI.DrawTexture(arrowRect, arrow, ScaleMode.ScaleToFit, true);
            }

            return selectedIndex;
        }

        /// <summary>
        /// 兼容旧调用：Popup 模式下不需要额外输入拦截。
        /// </summary>
        public static void HandleDropdownOverlayInput()
        {
        }

        /// <summary>
        /// 兼容旧调用：Popup 模式下不需要额外 overlay 绘制。
        /// </summary>
        public static void DrawDropdownOverlays()
        {
        }

        private static void OnDropdownPopupSelected(string dropdownId, int selectedIndex)
        {
            if (string.IsNullOrEmpty(dropdownId))
                return;
            _dropdownPendingSelections[dropdownId] = selectedIndex;
        }

        private sealed class StyledDropdownPopup : PopupWindowContent
        {
            private readonly string _dropdownId;
            private readonly IList<string> _options;
            private readonly int _selectedIndex;
            private readonly int _separatorBeforeIndex;
            private readonly float _width;
            private readonly float _hoverInset;
            private readonly Action<string, int> _onSelected;

            public StyledDropdownPopup(
                string dropdownId,
                IList<string> options,
                int selectedIndex,
                int separatorBeforeIndex,
                float width,
                float hoverInset,
                Action<string, int> onSelected)
            {
                _dropdownId = dropdownId;
                _options = options;
                _selectedIndex = selectedIndex;
                _separatorBeforeIndex = separatorBeforeIndex;
                _width = Mathf.Max(EditorUiScale.S(120f), width);
                _hoverInset = Mathf.Max(0f, hoverInset);
                _onSelected = onSelected;
            }

            public override void OnOpen()
            {
                if (editorWindow != null)
                    editorWindow.wantsMouseMove = true;
            }

            public override Vector2 GetWindowSize()
            {
                float rowHeight = EditorUiScale.S(30f);
                float panelPaddingY = EditorUiScale.S(4f);
                float h = panelPaddingY * 2f + rowHeight * _options.Count;
                return new Vector2(_width, h);
            }

            public override void OnGUI(Rect rect)
            {
                float rowHeight = EditorUiScale.S(30f);
                float panelPaddingY = EditorUiScale.S(4f);
                float textPaddingX = EditorUiScale.S(12f);

                if (Event.current.type == EventType.MouseMove && editorWindow != null)
                    editorWindow.Repaint();

                EditorGUI.DrawRect(rect, CommonStyles.WindowBackgroundColor);
                GUI.Box(rect, GUIContent.none, CommonStyles.DropDownPanelStyle);

                float currentY = rect.y + panelPaddingY;
                for (int i = 0; i < _options.Count; i++)
                {
                    Rect rowRect = new Rect(rect.x, currentY, rect.width, rowHeight);
                    bool isHover = rowRect.Contains(Event.current.mousePosition);
                    if (isHover || i == _selectedIndex)
                    {
                        Rect hoverRect = new Rect(
                            rowRect.x + _hoverInset,
                            rowRect.y,
                            Mathf.Max(0f, rowRect.width - _hoverInset * 2f),
                            rowRect.height);

                        if (CommonStyles.DropDownFrameSelectedTexture != null)
                            GUI.DrawTexture(hoverRect, CommonStyles.DropDownFrameSelectedTexture, ScaleMode.StretchToFill, true);
                        else
                            EditorGUI.DrawRect(hoverRect, CommonStyles.ThemeGreenColor);
                    }

                    Rect rowTextRect = new Rect(rowRect.x + textPaddingX, rowRect.y, rowRect.width - textPaddingX * 2f, rowRect.height);
                    GUI.Label(rowTextRect, _options[i], CommonStyles.DropDownRowTextStyle);

                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rowRect.Contains(Event.current.mousePosition))
                    {
                        _onSelected?.Invoke(_dropdownId, i);
                        editorWindow.Close();
                        Event.current.Use();
                        return;
                    }

                    currentY += rowHeight;
                }
            }
        }

        public static string DrawSearchTextField(string value, string placeholder, params GUILayoutOption[] options)
        {
            float height = EditorUiScale.S(44f);
            float padding = EditorUiScale.S(10f);
            float iconSize = EditorUiScale.S(18f);
            float iconGap = EditorUiScale.S(10f);

            Rect rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                CommonStyles.SearchTextFieldStyle,
                options != null && options.Length > 0
                    ? options
                    : new[] { GUILayout.ExpandWidth(true), GUILayout.Height(height) });

            // 背景（4x 九宫格，radius=4 -> sourceBorder=16）
            var bgTex = CommonStyles.SearchTextFieldStyle.normal.background;
            if (bgTex != null)
                DrawNineSliceScaled(rect, bgTex, 16, bgTex.height);

            // 图标
            var searchIcon = CommonStyles.SearchIconTexture;
            float iconX = rect.x + padding;
            if (searchIcon != null)
            {
                Rect iconRect = new Rect(iconX, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize);
                GUI.DrawTexture(iconRect, searchIcon, ScaleMode.ScaleToFit, true);
            }

            // 输入区域
            float textX = rect.x + padding + iconSize + iconGap;
            Rect textRect = new Rect(
                textX,
                rect.y + 1f,
                Mathf.Max(1f, rect.xMax - padding - textX),
                rect.height - 2f);

            EditorGUIUtility.AddCursorRect(textRect, MouseCursor.Text);

            var textStyle = new GUIStyle(GUIStyle.none)
            {
                font = CommonStyles.SourceHanSansMediumFont != null ? CommonStyles.SourceHanSansMediumFont : CommonStyles.SourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(14),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0)
            };
            textStyle.normal.textColor = Color.white;
            textStyle.hover.textColor = Color.white;
            textStyle.active.textColor = Color.white;
            textStyle.focused.textColor = Color.white;

            string current = value ?? string.Empty;
            string newValue = EditorGUI.TextField(textRect, current, textStyle);

            if (ShouldShowEmptyTextPlaceholder(newValue) && !string.IsNullOrEmpty(placeholder))
            {
                var placeholderStyle = new GUIStyle(textStyle);
                placeholderStyle.normal.textColor = new Color(102f / 255f, 102f / 255f, 102f / 255f, 1f);
                GUI.Label(textRect, placeholder, placeholderStyle);
            }

            return newValue;
        }

        /// <summary>
        /// 绘制带图标的生成按钮；图标由调用方传入（如 CommonStyles.GenerateButtonIcon / GenerateButtonIconGreen），icon 为 null 时使用默认图标。
        /// </summary>
        public static bool DrawGenerateButton(
            string text,
            GUIStyle style,
            Texture2D icon,
            bool showIcon = true,
            params GUILayoutOption[] options
        )
        {
            Texture2D iconToUse = showIcon ? (icon != null ? icon : CommonStyles.GenerateButtonIcon) : null;
            GUIContent content = new GUIContent(showIcon ? (" " + text) : text, iconToUse);
            Rect rect = GUILayoutUtility.GetRect(
                content,
                style,
                options != null && options.Length > 0 ? options : new[] { GUILayout.ExpandWidth(true) });
            EditorGUIUtility.SetIconSize(CommonStyles.GenerateButtonIconSize);
            bool enabled = GUI.enabled;
            DrawGenerateButtonNineSliceBackground(rect, style, enabled);
            GUIStyle contentStyle = CreateContentOnlyStyle(style);
            contentStyle.imagePosition = showIcon ? ImagePosition.ImageLeft : ImagePosition.TextOnly;
            contentStyle.alignment = TextAnchor.MiddleCenter;
            return GUI.Button(rect, content, contentStyle);
        }

        public static bool DrawGenerateButtonWithCost(
            string text,
            int cost,
            bool enabled,
            params GUILayoutOption[] options)
        {
            float costIconSize = EditorUiScale.S(19f);
            float titleCostGap = EditorUiScale.S(12f);
            float iconTextGap = EditorUiScale.S(6f);

            GUIStyle btnStyle = enabled ? CommonStyles.GenerateButtonSolidStyle : CommonStyles.GenerateButtonBusyStyle;
            GUIStyle titleStyle = new GUIStyle(CommonStyles.ContentStyle)
            {
                font = CommonStyles.SourceHanSansMediumFont != null ? CommonStyles.SourceHanSansMediumFont : CommonStyles.SourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(18),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = Color.white;
            titleStyle.hover.textColor = Color.white;
            titleStyle.active.textColor = Color.white;
            titleStyle.focused.textColor = Color.white;

            GUIStyle costStyle = new GUIStyle(CommonStyles.ContentStyle)
            {
                font = CommonStyles.SourceHanSansMediumFont != null ? CommonStyles.SourceHanSansMediumFont : CommonStyles.SourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(16),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            costStyle.normal.textColor = Color.white;
            costStyle.hover.textColor = Color.white;
            costStyle.active.textColor = Color.white;
            costStyle.focused.textColor = Color.white;

            GUI.enabled = enabled;
            Rect buttonRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                btnStyle,
                options != null && options.Length > 0 ? options : new[] { GUILayout.ExpandWidth(true), GUILayout.Height(EditorUiScale.S(40f)) });
            bool clicked = enabled && GUI.Button(buttonRect, GUIContent.none, GUIStyle.none);
            GUI.enabled = true;
            DrawGenerateButtonNineSliceBackground(buttonRect, btnStyle, enabled);

            string safeText = string.IsNullOrEmpty(text) ? "生成模型" : text;
            string safeCost = Mathf.Max(0, cost).ToString();
            float titleWidth = titleStyle.CalcSize(new GUIContent(safeText)).x;
            float costTextWidth = costStyle.CalcSize(new GUIContent(safeCost)).x;
            float groupWidth = titleWidth + titleCostGap + costIconSize + iconTextGap + costTextWidth;
            float startX = buttonRect.x + Mathf.Max(0f, (buttonRect.width - groupWidth) * 0.5f);
            float centerY = buttonRect.center.y;

            Rect titleRect = new Rect(startX, centerY - EditorUiScale.S(12f), titleWidth, EditorUiScale.S(24f));
            GUI.Label(titleRect, safeText, titleStyle);

            float costGroupX = titleRect.xMax + titleCostGap;
            Rect iconRect = new Rect(costGroupX, centerY - costIconSize * 0.5f, costIconSize, costIconSize);
            var costIcon = CommonStyles.GenerateCostIconTexture;
            if (costIcon != null)
                GUI.DrawTexture(iconRect, costIcon, ScaleMode.ScaleToFit, true);
            else
                EditorGUI.DrawRect(iconRect, Color.white);

            Rect costRect = new Rect(iconRect.xMax + iconTextGap, centerY - EditorUiScale.S(10f), costTextWidth + EditorUiScale.S(2f), EditorUiScale.S(20f));
            GUI.Label(costRect, safeCost, costStyle);
            return clicked;
        }

        /// <summary>
        /// 按新 UI 生成按钮底图绘制，支持自定义图标尺寸与图文间距。
        /// </summary>
        public static bool DrawGenerateButtonWithIconLayout(
            string text,
            Texture2D icon,
            bool enabled,
            float iconSize,
            float iconTextGap,
            params GUILayoutOption[] options)
        {
            GUIStyle btnStyle = enabled ? CommonStyles.GenerateButtonSolidStyle : CommonStyles.GenerateButtonBusyStyle;
            GUIStyle titleStyle = new GUIStyle(CommonStyles.ContentStyle)
            {
                font = CommonStyles.SourceHanSansMediumFont != null ? CommonStyles.SourceHanSansMediumFont : CommonStyles.SourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(18),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = Color.white;
            titleStyle.hover.textColor = Color.white;
            titleStyle.active.textColor = Color.white;
            titleStyle.focused.textColor = Color.white;

            GUI.enabled = enabled;
            Rect buttonRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                btnStyle,
                options != null && options.Length > 0 ? options : new[] { GUILayout.ExpandWidth(true), GUILayout.Height(EditorUiScale.S(40f)) });
            bool clicked = enabled && GUI.Button(buttonRect, GUIContent.none, GUIStyle.none);
            GUI.enabled = true;
            DrawGenerateButtonNineSliceBackground(buttonRect, btnStyle, enabled);

            string safeText = string.IsNullOrEmpty(text) ? "生成" : text;
            float textWidth = titleStyle.CalcSize(new GUIContent(safeText)).x;
            float safeIconSize = Mathf.Max(0f, iconSize);
            float safeGap = safeIconSize > 0f ? Mathf.Max(0f, iconTextGap) : 0f;
            float groupWidth = safeIconSize > 0f ? safeIconSize + safeGap + textWidth : textWidth;
            float startX = buttonRect.x + Mathf.Max(0f, (buttonRect.width - groupWidth) * 0.5f);
            float centerY = buttonRect.center.y;

            float textX = startX;
            if (safeIconSize > 0f)
            {
                Rect iconRect = new Rect(startX, centerY - safeIconSize * 0.5f, safeIconSize, safeIconSize);
                if (icon != null)
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                textX = iconRect.xMax + safeGap;
            }

            Rect textRect = new Rect(textX, centerY - EditorUiScale.S(12f), textWidth + EditorUiScale.S(2f), EditorUiScale.S(24f));
            GUI.Label(textRect, safeText, titleStyle);
            return clicked;
        }

        private static void DrawGenerateButtonNineSliceBackground(Rect rect, GUIStyle style, bool enabled)
        {
            Texture2D tex = ResolveGenerateButtonTexture(style, rect, enabled);
            if (tex == null)
                return;

            const int sourceBorder = 32;
            const float referenceHeight = 160f;
            DrawNineSliceScaled(rect, tex, sourceBorder, referenceHeight);
        }

        private static Texture2D ResolveGenerateButtonTexture(GUIStyle style, Rect rect, bool enabled)
        {
            SyncImguiLeftMouseHeldFromEvent();

            Texture2D normal = style.normal.background;
            Texture2D hover = style.hover.background != null ? style.hover.background : normal;
            Texture2D active = style.active.background != null ? style.active.background : hover;
            if (!enabled)
                return normal != null ? normal : active;

            bool isHover = rect.Contains(Event.current.mousePosition);
            bool isPressing = isHover && ImguiLeftMouseHeld;
            if (isPressing)
                return active != null ? active : hover;
            if (isHover)
                return hover != null ? hover : normal;
            return normal;
        }

        /// <summary>
        /// 九宫格各块之间的默认重叠（设计约 1px × <see cref="EditorUiScale"/>），减轻缩放/舍入导致的接缝线。
        /// </summary>
        public static float DefaultNineSliceOverlapPixels => Mathf.Max(0.5f, EditorUiScale.S(1f));

        /// <summary>
        /// overlapPx：负数表示使用 <see cref="DefaultNineSliceOverlapPixels"/>；0 表示块与块之间不重叠。
        /// </summary>
        private static float ResolveNineSliceOverlap(float overlapPx)
        {
            if (overlapPx < 0f)
                return DefaultNineSliceOverlapPixels;
            return Mathf.Max(0f, overlapPx);
        }

        /// <summary>
        /// 将矩形对齐到物理像素网格，减轻 <see cref="GUI.DrawTextureWithTexCoords"/> 在子像素坐标下
        /// 左/上缘与右/下缘采样不对称（常见表现为边框或虚线「上左细、右下粗」）。
        /// </summary>
        private static Rect SnapRectToPixelGrid(Rect r)
        {
            float ppp = Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
            float Snap(float v) => Mathf.Round(v * ppp) / ppp;
            float xMin = Snap(r.xMin);
            float yMin = Snap(r.yMin);
            float xMax = Snap(r.xMax);
            float yMax = Snap(r.yMax);
            float minSpan = 1f / ppp;
            if (xMax - xMin < minSpan)
                xMax = xMin + minSpan;
            if (yMax - yMin < minSpan)
                yMax = yMin + minSpan;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>
        /// 统一九宫格绘制入口：
        /// - sourceBorder: 源贴图切片边界（像素）
        /// - fixedDestBorder > 0 时使用固定目标边界；否则按 referenceHeight 等比缩放
        /// - overlapPx: 负数（默认）为 <see cref="DefaultNineSliceOverlapPixels"/> 填充重叠；0 为关闭
        /// </summary>
        public static void DrawNineSlice(
            Rect targetRect,
            Texture2D texture,
            int sourceBorder,
            float referenceHeight,
            int fixedDestBorder = -1,
            float overlapPx = -1f)
        {
            if (texture == null)
                return;

            targetRect = SnapRectToPixelGrid(targetRect);

            int destBorder = fixedDestBorder > 0
                ? fixedDestBorder
                // 缩放时用 Floor，避免 8.8 -> 9 这类上舍入导致切线不连续出现固定竖缝
                : Mathf.Max(1, Mathf.FloorToInt(sourceBorder * (targetRect.height / Mathf.Max(1f, referenceHeight))));
            DrawNineSliceRemap(targetRect, texture, sourceBorder, destBorder, ResolveNineSliceOverlap(overlapPx));
        }

        /// <summary>
        /// 便捷重载：按 referenceHeight 自动缩放目标边界。
        /// </summary>
        public static void DrawNineSliceScaled(
            Rect targetRect,
            Texture2D texture,
            int sourceBorder,
            float referenceHeight,
            float overlapPx = -1f)
        {
            DrawNineSlice(targetRect, texture, sourceBorder, referenceHeight, -1, overlapPx);
        }

        /// <summary>
        /// 便捷重载：使用固定目标边界。
        /// </summary>
        public static void DrawNineSliceFixed(
            Rect targetRect,
            Texture2D texture,
            int sourceBorder,
            int fixedDestBorder,
            float overlapPx = -1f)
        {
            DrawNineSlice(targetRect, texture, sourceBorder, Mathf.Max(1f, targetRect.height), fixedDestBorder, overlapPx);
        }

        /// <summary>
        /// 便捷重载：直接使用 GUIStyle 的 background/border，按样式贴图高度缩放。
        /// </summary>
        public static void DrawNineSliceWithStyle(
            Rect targetRect,
            GUIStyle style,
            int fixedDestBorder = -1,
            float overlapPx = -1f)
        {
            if (style == null || style.normal.background == null)
                return;
            Texture2D texture = style.normal.background;
            int sourceBorder = Mathf.Max(1, style.border.left);
            DrawNineSlice(targetRect, texture, sourceBorder, texture.height, fixedDestBorder, overlapPx);
        }

        private static void DrawNineSliceRemap(Rect targetRect, Texture2D texture, int sourceBorder, int destBorder, float overlapPx)
        {
            int src = Mathf.Clamp(sourceBorder, 1, Mathf.Min(texture.width / 2 - 1, texture.height / 2 - 1));
            float dst = Mathf.Clamp(destBorder, 1f, Mathf.Min((targetRect.width - 1f) * 0.5f, (targetRect.height - 1f) * 0.5f));
            float overlap = Mathf.Max(0f, overlapPx);
            if (overlap > 0f && dst >= 1f)
            {
                // 大角区时若重叠仍取 ~1px，边/中心会过多侵入角区，虚线边框内侧易被盖住（上/左边更细），
                // 圆弧过渡也易被邻片采样“折线化”。随 dst 缩小允许的最大重叠。
                float capByCorner = Mathf.Clamp(2.0f / dst, 0.1f, 1f);
                overlap = Mathf.Min(overlap, capByCorner);
            }

            // 用“宽高拆三段 + xMax/yMax 串联”保证 patch 之间无缝拼接（避免 1px 缝/线）。
            float leftW = dst;
            float rightW = dst;
            float midW = Mathf.Max(0f, targetRect.width - leftW - rightW);
            float topH = dst;
            float bottomH = dst;
            float midH = Mathf.Max(0f, targetRect.height - topH - bottomH);

            Rect r00 = new Rect(targetRect.x, targetRect.y, leftW, topH);
            Rect r10 = new Rect(r00.xMax - overlap, targetRect.y, midW + overlap * 2f, topH);
            Rect r20 = new Rect(r10.xMax - overlap, targetRect.y, rightW + overlap, topH);

            Rect r01 = new Rect(targetRect.x, r00.yMax - overlap, leftW, midH + overlap * 2f);
            Rect r11 = new Rect(r01.xMax - overlap, r10.yMax - overlap, midW + overlap * 2f, midH + overlap * 2f);
            Rect r21 = new Rect(r11.xMax - overlap, r20.yMax - overlap, rightW + overlap, midH + overlap * 2f);

            Rect r02 = new Rect(targetRect.x, r01.yMax - overlap, leftW, bottomH + overlap);
            Rect r12 = new Rect(r02.xMax - overlap, r11.yMax - overlap, midW + overlap * 2f, bottomH + overlap);
            Rect r22 = new Rect(r12.xMax - overlap, r21.yMax - overlap, rightW + overlap, bottomH + overlap);

            // 绘制顺序：先中区，再四边，最后四角。
            // - 若中区在边条之后画，r11 会叠在 r10/r01 向内的重叠带上，易吃掉贴图里靠内侧的虚线/描边（常见为顶、左边更细）。
            // - 边条仍会叠在角块矩形上；四角最后绘制，盖住侵入的直边采样，保留圆角柔和过渡。
            DrawPatchPx(r11, texture, src, src, texture.width - src * 2, texture.height - src * 2, insetLeft: false, insetRight: false, insetTop: false, insetBottom: false);
            DrawPatchPx(r10, texture, src, 0, texture.width - src * 2, src, insetLeft: false, insetRight: false, insetTop: true, insetBottom: false);
            DrawPatchPx(r12, texture, src, texture.height - src, texture.width - src * 2, src, insetLeft: false, insetRight: false, insetTop: false, insetBottom: true);
            DrawPatchPx(r01, texture, 0, src, src, texture.height - src * 2, insetLeft: true, insetRight: false, insetTop: false, insetBottom: false);
            DrawPatchPx(r21, texture, texture.width - src, src, src, texture.height - src * 2, insetLeft: false, insetRight: true, insetTop: false, insetBottom: false);

            DrawPatchPx(r00, texture, 0, 0, src, src, insetLeft: true, insetRight: false, insetTop: true, insetBottom: false);
            DrawPatchPx(r20, texture, texture.width - src, 0, src, src, insetLeft: false, insetRight: true, insetTop: true, insetBottom: false);
            DrawPatchPx(r02, texture, 0, texture.height - src, src, src, insetLeft: true, insetRight: false, insetTop: false, insetBottom: true);
            DrawPatchPx(r22, texture, texture.width - src, texture.height - src, src, src, insetLeft: false, insetRight: true, insetTop: false, insetBottom: true);
        }

        private static void DrawPatchPx(
            Rect targetRect,
            Texture2D texture,
            int sx,
            int sy,
            int sw,
            int sh,
            bool insetLeft,
            bool insetRight,
            bool insetTop,
            bool insetBottom)
        {
            if (targetRect.width <= 0f || targetRect.height <= 0f || sw <= 0 || sh <= 0)
                return;

            float texW = texture.width;
            float texH = texture.height;
            float insetX = 0.5f / texW;
            float insetY = 0.5f / texH;

            float uMin = (sx / texW) + (insetLeft ? insetX : 0f);
            float uMax = ((sx + sw) / texW) - (insetRight ? insetX : 0f);
            // 注意：Unity 的 UV v 轴从下到上；sy 是从贴图顶部开始的像素坐标
            float vMin = 1f - ((sy + sh) / texH) + (insetBottom ? insetY : 0f);
            float vMax = 1f - (sy / texH) - (insetTop ? insetY : 0f);
            Rect uvRect = Rect.MinMaxRect(uMin, vMin, uMax, vMax);
            GUI.DrawTextureWithTexCoords(targetRect, texture, uvRect, true);
        }

        private static void DrawPatchPx(Rect targetRect, Texture2D texture, int sx, int sy, int sw, int sh)
        {
            // 默认保持旧行为：四边都内缩（用于外层边缘防止采样污染）。
            DrawPatchPx(targetRect, texture, sx, sy, sw, sh, insetLeft: true, insetRight: true, insetTop: true, insetBottom: true);
        }

        private static GUIStyle CreateContentOnlyStyle(GUIStyle baseStyle)
        {
            var style = new GUIStyle(baseStyle);
            style.normal.background = null;
            style.hover.background = null;
            style.active.background = null;
            style.focused.background = null;
            style.onNormal.background = null;
            style.onHover.background = null;
            style.onActive.background = null;
            style.onFocused.background = null;
            style.border = new RectOffset(0, 0, 0, 0);
            return style;
        }

        private static void DrawNineSliceBackground(
            Rect rect,
            Texture2D texture,
            int sourceBorder,
            float referenceHeight,
            int fixedDestBorder = -1,
            float overlapPx = -1f)
        {
            DrawNineSlice(rect, texture, Mathf.Max(1, sourceBorder), Mathf.Max(1f, referenceHeight), fixedDestBorder, overlapPx);
        }

        /// <summary>
        /// 绘制生成区域：生成按钮 + 生成中时的进度条与状态文本。
        /// </summary>
        /// <param name="isGenerating">是否正在生成</param>
        /// <param name="progress">进度 0~1</param>
        /// <param name="status">状态文本</param>
        /// <param name="canGenerate">是否允许点击生成（如提示词非空）</param>
        /// <param name="onGenerate">点击生成时的回调</param>
        /// <param name="drawExtraBetweenButtonAndProgress">可选，在按钮与进度条之间绘制额外内容</param>
        /// <param name="repaint">可选，生成中时调用的刷新回调（用于动画，调用方可做节流）</param>
        public static void DrawGenerationSection(
            bool isGenerating,
            float progress,
            string status,
            bool canGenerate,
            Action onGenerate,
            Action drawExtraBetweenButtonAndProgress = null,
            Action repaint = null,
            int generationCost = 0
        )
        {
            GUILayout.BeginHorizontal();
            bool canClick = !isGenerating && canGenerate;
            string buttonText = isGenerating ? "生成中..." : "生成";
            int safeCost = Mathf.Max(0, generationCost);
            bool clicked = DrawGenerateButtonWithCost(
                buttonText,
                safeCost,
                canClick,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(40f));

            if (clicked)
                onGenerate?.Invoke();
            GUILayout.EndHorizontal();

            drawExtraBetweenButtonAndProgress?.Invoke();

            if (isGenerating)
            {
                GUILayout.Space(8);
                GUILayout.BeginHorizontal();
                Rect progressRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(progressRect, Mathf.Clamp01(progress), "");
                GUI.Label(
                    progressRect,
                    $"{Mathf.RoundToInt(progress * 100)}%",
                    CommonStyles.ProgressLabelStyle
                );
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                GUILayout.Space(25);
                GUILayout.Label(status ?? "", CommonStyles.StatusStyle);
                GUILayout.EndHorizontal();
                repaint?.Invoke();
            }
        }

        /// <summary>
        /// 绘制历史记录为空时的占位提示。
        /// </summary>
        public static void DrawHistoryEmptyState()
        {
            GUILayout.Label("暂无历史记录", CommonStyles.CenteredGreyLabelStyle);
        }

        /// <summary>
        /// 在指定矩形内绘制“生成中”旋转加载图标，用于历史项等占位。
        /// </summary>
        /// <param name="rect">绘制区域</param>
        /// <param name="fallbackStyle">图标不可用时使用的文字样式，默认 CommonStyles.SmallGreyCenterLabelStyle</param>
        /// <param name="repaint">可选，用于驱动动画的刷新回调</param>
        public static void DrawLoadingSpinner(
            Rect rect,
            GUIStyle fallbackStyle = null,
            Action repaint = null
        )
        {
            if (Event.current.type == EventType.Repaint)
            {
                var spinIcon = EditorGUIUtility.IconContent("Loading");
                if (spinIcon != null && spinIcon.image != null)
                {
                    var iconRect = new Rect(
                        rect.x + rect.width / 4,
                        rect.y + rect.height / 4,
                        rect.width / 2,
                        rect.height / 2
                    );
                    float angle = (float)(EditorApplication.timeSinceStartup * 180) % 360f;
                    var matrixBackup = GUI.matrix;
                    GUIUtility.RotateAroundPivot(angle, iconRect.center);
                    GUI.DrawTexture(iconRect, spinIcon.image, ScaleMode.ScaleToFit);
                    GUI.matrix = matrixBackup;
                }
                else
                {
                    var style = fallbackStyle ?? CommonStyles.SmallGreyCenterLabelStyle;
                    GUI.Label(rect, "生成中...", style);
                }
            }
            repaint?.Invoke();
        }

        /// <summary>
        /// 绘制矩形边框（上、下、左、右四条边）。
        /// </summary>
        /// <param name="rect">目标矩形</param>
        /// <param name="color">边框颜色</param>
        /// <param name="thickness">边框厚度，默认 1</param>
        public static void DrawRectOutline(Rect rect, Color color, float thickness = 1f)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(
                new Rect(rect.x, rect.yMax - thickness, rect.width, thickness),
                color
            );
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(
                new Rect(rect.xMax - thickness, rect.y, thickness, rect.height),
                color
            );
        }

        /// <summary>
        /// 绘制高级设置折叠区：Foldout「高级设置」+ 展开时按配置绘制各高级参数。
        /// </summary>
        /// <param name="expanded">当前折叠状态，返回新状态</param>
        /// <param name="provider">参数提供者，为 null 时展开内容为空</param>
        /// <param name="parameters">参数列表，为 null 或空时展开内容为空</param>
        /// <param name="foldoutLabel">折叠标题，默认「高级设置」</param>
        /// <returns>新的 expanded 状态</returns>
        public static bool DrawAdvancedSettingsFoldout(
            bool expanded,
            IGeneratorParameterProvider provider,
            List<ParameterConfig> parameters,
            string foldoutLabel = "高级设置"
        )
        {
            bool hasParams = parameters != null && parameters.Count > 0;
            // 仅当存在参数时才绘制高级设置折叠项
            if (!hasParams)
                return false;

            const float rowHeight = 20f;
            const float arrowW = 15f;
            const float arrowH = 10f;
            const float arrowCenterGapToLabelRight = 12f;

            Rect rowRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(rowHeight));

            string labelText = (foldoutLabel ?? "高级设置").ToUpperInvariant();
            GUI.Label(rowRect, labelText, CommonStyles.AdvancedFoldoutTitleStyle);

            Vector2 labelSize = CommonStyles.AdvancedFoldoutTitleStyle.CalcSize(new GUIContent(labelText));
            float labelRight = rowRect.x + labelSize.x;
            float arrowCenterX = labelRight + arrowCenterGapToLabelRight;

            var arrowTex = CommonStyles.AdvancedFoldoutArrowTexture;
            if (arrowTex != null)
            {
                Rect arrowRect = new Rect(
                    arrowCenterX - arrowW * 0.5f,
                    rowRect.y + (rowHeight - arrowH) * 0.5f,
                    arrowW,
                    arrowH);

                Matrix4x4 old = GUI.matrix;
                float angle = expanded ? 90f : 0f;
                GUIUtility.RotateAroundPivot(angle, arrowRect.center);
                GUI.DrawTexture(arrowRect, arrowTex, ScaleMode.ScaleToFit, true);
                GUI.matrix = old;
            }

            EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);
            bool newExpanded = expanded;
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rowRect.Contains(Event.current.mousePosition))
            {
                newExpanded = !expanded;
                Event.current.Use();
            }

            if (newExpanded && provider != null)
            {
                GUILayout.Space(CommonStyles.Space1);
                bool drewAny = false;
                foreach (var param in parameters)
                {
                    if (param == null)
                        continue;
                    if (!string.IsNullOrEmpty(param.dependsOn))
                    {
                        var depVal = provider.GetParameter(param.dependsOn);
                        if (depVal == null || depVal.ToString() != param.dependsValue)
                            continue;
                    }

                    if (drewAny)
                        GUILayout.Space(CommonStyles.Space1);
                    DrawAdvancedParameter(provider, param);
                    drewAny = true;
                }
            }

            return newExpanded;
        }

        private static void DrawAdvancedParameter(IGeneratorParameterProvider provider, ParameterConfig param)
        {
            if (provider == null || param == null)
                return;

            switch (param.type)
            {
                case "dropdown":
                    DrawAdvancedDropdown(provider, param);
                    break;
                case "int":
                    DrawAdvancedIntField(provider, param);
                    break;
                case "float":
                    DrawAdvancedFloatField(provider, param);
                    break;
                case "bool":
                    DrawAdvancedBoolField(provider, param);
                    break;
                case "json":
                    DrawAdvancedJsonField(provider, param);
                    break;
                case "string":
                default:
                    DrawAdvancedStringField(provider, param);
                    break;
            }
        }

        /// <summary>
        /// 计算高级设置行左侧标签区域与右侧控件列起始 X（与 <see cref="GetAdvancedRowRects"/> 边距一致）。
        /// </summary>
        public static void GetAdvancedSettingsRowColumnLayout(Rect rowRect, out Rect labelRect, out float controlColumnLeft)
        {
            controlColumnLeft = rowRect.xMax - AdvancedSettingsRowRightPadding - AdvancedSettingsRowControlWidth;
            labelRect = new Rect(rowRect.x, rowRect.y, Mathf.Max(1f, controlColumnLeft - rowRect.x), rowRect.height);
        }

        private static void GetAdvancedRowRects(Rect rowRect, out Rect labelRect, out Rect controlRect, float controlHeight)
        {
            float controlX = rowRect.xMax - AdvancedSettingsRowRightPadding - AdvancedSettingsRowControlWidth;
            float controlY = rowRect.y + (rowRect.height - controlHeight) * 0.5f;
            controlRect = new Rect(controlX, controlY, AdvancedSettingsRowControlWidth, controlHeight);
            labelRect = new Rect(rowRect.x, rowRect.y, Mathf.Max(1f, controlX - rowRect.x), rowRect.height);
        }

        private static void DrawAdvancedDropdown(IGeneratorParameterProvider provider, ParameterConfig param)
        {
            if (param.options == null || param.options.Count == 0)
                return;

            object currentVal = provider.GetParameter(param.id);
            int index = 0;
            if (currentVal != null)
            {
                string valStr = currentVal.ToString();
                for (int i = 0; i < param.options.Count; i++)
                {
                    if (param.options[i].value == valStr)
                    {
                        index = i;
                        break;
                    }
                }
            }

            string[] labels = new string[param.options.Count];
            for (int i = 0; i < param.options.Count; i++)
                labels[i] = param.options[i].label;

            GUILayout.BeginHorizontal();
            GUILayout.Label(param.label ?? string.Empty, CommonStyles.AdvancedFoldoutTitleStyle, GUILayout.Height(AdvancedSettingsRowHeight));
            GUILayout.FlexibleSpace();
            int newIndex = DrawStyledDropdown(
                "advanced_" + (param.id ?? "dropdown"),
                index,
                labels,
                separatorBeforeIndex: -1,
                panelTopGap: 4f,
                hoverInset: 2f,
                dropdownWidth: AdvancedSettingsRowControlWidth);
            GUILayout.Space(AdvancedSettingsRowRightPadding);
            GUILayout.EndHorizontal();

            if (newIndex != index)
                provider.SetParameter(param.id, param.options[newIndex].value);
        }

        private static void DrawAdvancedIntField(IGeneratorParameterProvider provider, ParameterConfig param)
        {
            int value = 0;
            if (provider.GetParameter(param.id) != null)
                int.TryParse(provider.GetParameter(param.id).ToString(), out value);

            Rect rowRect = GUILayoutUtility.GetRect(0f, AdvancedSettingsRowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(AdvancedSettingsRowHeight));
            GetAdvancedRowRects(rowRect, out var labelRect, out var controlRect, EditorUiScale.S(30f));
            GUI.Label(labelRect, param.label ?? string.Empty, CommonStyles.AdvancedFoldoutTitleStyle);
            string oldText = value.ToString();
            string committedText = DrawAdvancedInputTextField(controlRect, oldText, delayed: true);
            if (!string.Equals(committedText, oldText, StringComparison.Ordinal))
            {
                if (int.TryParse(committedText, out int parsed))
                {
                    if (param.min != 0 || param.max != 0)
                        parsed = (int)Mathf.Clamp((float)parsed, param.min, param.max);
                    if (parsed != value)
                        provider.SetParameter(param.id, parsed);
                }
            }
        }

        private static void DrawAdvancedFloatField(IGeneratorParameterProvider provider, ParameterConfig param)
        {
            float value = 0f;
            if (provider.GetParameter(param.id) != null)
                float.TryParse(provider.GetParameter(param.id).ToString(), out value);

            Rect rowRect = GUILayoutUtility.GetRect(0f, AdvancedSettingsRowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(AdvancedSettingsRowHeight));
            GetAdvancedRowRects(rowRect, out var labelRect, out var controlRect, EditorUiScale.S(30f));
            GUI.Label(labelRect, param.label ?? string.Empty, CommonStyles.AdvancedFoldoutTitleStyle);
            string oldText = value.ToString();
            string committedText = DrawAdvancedInputTextField(controlRect, oldText, delayed: true);
            if (!string.Equals(committedText, oldText, StringComparison.Ordinal))
            {
                if (float.TryParse(committedText, out float parsed))
                {
                    if (param.min != 0 || param.max != 0)
                        parsed = Mathf.Clamp(parsed, param.min, param.max);
                    if (Math.Abs(parsed - value) > 1e-6f)
                        provider.SetParameter(param.id, parsed);
                }
            }
        }

        private static void DrawAdvancedBoolField(IGeneratorParameterProvider provider, ParameterConfig param)
        {
            bool value = false;
            if (provider.GetParameter(param.id) is bool vb)
                value = vb;
            else if (bool.TryParse(provider.GetParameter(param.id)?.ToString(), out bool parsed))
                value = parsed;

            Rect rowRect = GUILayoutUtility.GetRect(0f, AdvancedSettingsRowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(AdvancedSettingsRowHeight));
            GetAdvancedRowRects(rowRect, out var labelRect, out var controlRect, EditorUiScale.S(30f));
            GUI.Label(labelRect, param.label ?? string.Empty, CommonStyles.AdvancedFoldoutTitleStyle);
            float boxSize = Mathf.Min(30f, Mathf.Min(controlRect.width, controlRect.height));
            Rect boxRect = new Rect(
                controlRect.x,
                controlRect.y + (controlRect.height - boxSize) * 0.5f,
                boxSize,
                boxSize);
            bool newValue = GUI.Toggle(boxRect, value, GUIContent.none, CommonStyles.CheckboxBoxStyle);
            if (newValue != value)
                provider.SetParameter(param.id, newValue);
        }

        private static void DrawAdvancedStringField(IGeneratorParameterProvider provider, ParameterConfig param)
        {
            string value = provider.GetParameter(param.id)?.ToString() ?? "";
            Rect rowRect = GUILayoutUtility.GetRect(0f, AdvancedSettingsRowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(AdvancedSettingsRowHeight));
            GetAdvancedRowRects(rowRect, out var labelRect, out var controlRect, EditorUiScale.S(30f));
            GUI.Label(labelRect, param.label ?? string.Empty, CommonStyles.AdvancedFoldoutTitleStyle);
            string newValue = DrawAdvancedInputTextField(controlRect, value, delayed: false);
            if (newValue != value)
                provider.SetParameter(param.id, newValue);
        }

        private static string DrawAdvancedInputTextField(Rect rect, string value, bool delayed)
        {
            var bgTex = CommonStyles.AdvancedInputBoxTexture;
            if (bgTex != null)
                DrawNineSliceFixed(rect, bgTex, 8, 4);
            else
                EditorGUI.DrawRect(rect, new Color(34f / 255f, 34f / 255f, 34f / 255f, 1f));

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Text);
            if (delayed)
                return EditorGUI.DelayedTextField(rect, value ?? string.Empty, CommonStyles.AdvancedInputTextStyle);
            return EditorGUI.TextField(rect, value ?? string.Empty, CommonStyles.AdvancedInputTextStyle);
        }

        private static void DrawAdvancedJsonField(IGeneratorParameterProvider provider, ParameterConfig param)
        {
            string value = provider.GetParameter(param.id)?.ToString() ?? "";
            Rect rowRect = GUILayoutUtility.GetRect(0f, EditorUiScale.S(20f), GUILayout.ExpandWidth(true), GUILayout.Height(EditorUiScale.S(20f)));
            GUI.Label(rowRect, param.label ?? string.Empty, CommonStyles.AdvancedFoldoutTitleStyle);
            GUILayout.Space(CommonStyles.Space1);
            string newValue = EditorGUILayout.TextArea(value, GUILayout.MinHeight(EditorUiScale.S(48f)));
            if (newValue != value)
                provider.SetParameter(param.id, newValue);
        }

        /// <summary>
        /// 绘制新版大图上传组件（normal/hover/uploaded 三态）。
        /// - 点击框体任意位置：打开本地图片上传。
        /// - 点击「用AI生成 + 箭头」：触发 onAIGenClicked，不进入本地上传。
        /// - 已上传后仅显示预览图。
        /// </summary>
        public static void DrawUploadImageLargeComponent(
            ref string imagePath,
            ref Texture2D uploadedImage,
            Action onAIGenClicked,
            Action repaint,
            Action onUserChanged = null,
            string aiActionLabel = "用AI生成")
        {
            float frameHeight = EditorUiScale.S(210f);
            float iconSize = EditorUiScale.S(44f);
            float uploadedPreviewMaxWidth = EditorUiScale.S(420f);
            float uploadedPreviewHeight = EditorUiScale.S(176f);
            float clearBtnSize = EditorUiScale.S(30f);

            Rect rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                CommonStyles.UploadFrameNormalStyle,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(frameHeight));

            rect = SnapRectToPixelGrid(rect);

            bool isUploaded = uploadedImage != null;
            bool isHover = rect.Contains(Event.current.mousePosition);
            GUIStyle frameStyle = isUploaded
                ? CommonStyles.UploadFrameUploadedStyle
                : (isHover ? CommonStyles.UploadFrameHoverStyle : CommonStyles.UploadFrameNormalStyle);
            // 虚线框对 patch 重叠敏感；外框按像素对齐，减轻左/上缘采样偏细
            DrawNineSliceBackground(
                rect,
                frameStyle.normal.background,
                frameStyle.border.left,
                frameStyle.normal.background != null ? frameStyle.normal.background.height : frameHeight,
                fixedDestBorder: -1,
                overlapPx: 0f);

            Rect aiHitRect = Rect.zero;
            if (!isUploaded)
            {
                var uploadIcon = CommonStyles.UploadImageIconTexture;
                if (uploadIcon != null)
                {
                    Rect iconRect = new Rect(
                        rect.x + (rect.width - iconSize) * 0.5f,
                        rect.y + EditorUiScale.S(38f),
                        iconSize,
                        iconSize);
                    GUI.DrawTexture(iconRect, uploadIcon, ScaleMode.ScaleToFit, true);
                }

                Rect titleRect = new Rect(rect.x, rect.y + EditorUiScale.S(86f), rect.width, EditorUiScale.S(20f));
                GUI.Label(titleRect, "上传图片", CommonStyles.UploadTitleStyle);

                Rect hintRect = new Rect(rect.x + EditorUiScale.S(79f), rect.y + EditorUiScale.S(110f), Mathf.Max(EditorUiScale.S(80f), rect.width - EditorUiScale.S(138f)), EditorUiScale.S(32f));
                GUI.Label(
                    hintRect,
                    "支持png/jpgl/jpeg/webp，文件大小最大不超过10M，分辨率最低要求128*128，最高限制4096*4096",
                    CommonStyles.UploadHintStyle);

                if (onAIGenClicked != null)
                {
                    float aiY = rect.y + EditorUiScale.S(165f);
                    float noImageWidth = CommonStyles.UploadNoImageStyle.CalcSize(new GUIContent("没有图片？")).x;
                    float aiGenWidth = CommonStyles.UploadAIGenLinkStyle.CalcSize(new GUIContent(aiActionLabel ?? "用AI生成")).x;
                    float arrowWidth = EditorUiScale.S(4f);
                    float gap1 = 0f;
                    float gap2 = EditorUiScale.S(6f);
                    float total = noImageWidth + gap1 + aiGenWidth + gap2 + arrowWidth;
                    float startX = rect.x + (rect.width - total) * 0.5f;

                    Rect noImageRect = new Rect(startX, aiY, noImageWidth, EditorUiScale.S(24f));
                    Rect aiTextRect = new Rect(noImageRect.xMax + gap1, aiY, aiGenWidth, EditorUiScale.S(24f));
                    Rect arrowRect = new Rect(aiTextRect.xMax + gap2, aiY + EditorUiScale.S(9.5f), arrowWidth, EditorUiScale.S(7f));
                    aiHitRect = new Rect(noImageRect.x, aiY, total, EditorUiScale.S(24f));

                    GUI.Label(noImageRect, "没有图片？", CommonStyles.UploadNoImageStyle);
                    GUI.Label(aiTextRect, aiActionLabel ?? "用AI生成", CommonStyles.UploadAIGenLinkStyle);
                    var greenArrow = CommonStyles.ArrowGreenTexture;
                    if (greenArrow != null)
                        GUI.DrawTexture(arrowRect, greenArrow, ScaleMode.ScaleToFit, true);
                    EditorGUIUtility.AddCursorRect(aiHitRect, MouseCursor.Link);
                }
            }
            else
            {
                float scaledWidth = uploadedImage.height > 0
                    ? uploadedPreviewHeight * ((float)uploadedImage.width / uploadedImage.height)
                    : uploadedPreviewMaxWidth;
                scaledWidth = Mathf.Min(uploadedPreviewMaxWidth, scaledWidth);
                Rect imageRect = new Rect(
                    rect.x + (rect.width - scaledWidth) * 0.5f,
                    rect.y + (rect.height - uploadedPreviewHeight) * 0.5f,
                    scaledWidth,
                    uploadedPreviewHeight);
                GUI.DrawTexture(imageRect, uploadedImage, ScaleMode.ScaleToFit, true);

                Rect clearBtnRect = new Rect(
                    imageRect.x + imageRect.width - clearBtnSize + EditorUiScale.S(4f),
                    imageRect.y - EditorUiScale.S(4f),
                    clearBtnSize,
                    clearBtnSize);
                GUI.Label(clearBtnRect, CommonStyles.ClearButtonSymbol, CommonStyles.ClearButtonStyle);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && clearBtnRect.Contains(Event.current.mousePosition))
                {
                    imagePath = string.Empty;
                    if (uploadedImage != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(uploadedImage)))
                        UnityEngine.Object.DestroyImmediate(uploadedImage);
                    uploadedImage = null;
                    Event.current.Use();
                    onUserChanged?.Invoke();
                    repaint?.Invoke();
                    return;
                }
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                if (!isUploaded && aiHitRect != Rect.zero && aiHitRect.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    onAIGenClicked?.Invoke();
                    repaint?.Invoke();
                    return;
                }

                string path = EditorUtility.OpenFilePanel("选择图片", "", "jpg,png,jpeg,webp");
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    if (uploadedImage != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(uploadedImage)))
                        UnityEngine.Object.DestroyImmediate(uploadedImage);
                    uploadedImage = null;

                    imagePath = path;
                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(File.ReadAllBytes(path)))
                    {
                        uploadedImage = tex;
                        onUserChanged?.Invoke();
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(tex);
                    }
                }
                Event.current.Use();
                repaint?.Invoke();
            }
        }

        /// <summary>
        /// 绘制单图上传区域：标题 + 可点击区域 + 预览/占位 + 清除按钮 + 文件选择逻辑。
        /// </summary>
        /// <param name="label">标题文本，如「参考图片（可选）」</param>
        /// <param name="imagePath">当前图片路径，会被方法更新</param>
        /// <param name="uploadedImage">当前预览纹理，会被方法更新（调用方需负责 DestroyImmediate 旧纹理）</param>
        /// <param name="repaint">刷新回调</param>
        /// <param name="onUserChanged">用户清除或成功选择本地文件后调用（非程序赋值）</param>
        public static void DrawSingleImageUpload(
            string label,
            ref string imagePath,
            ref Texture2D uploadedImage,
            Action repaint,
            Action onUserChanged = null
        )
        {
            float targetHeight = EditorUiScale.S(110f);
            if (!string.IsNullOrEmpty(label))
            {
                GUILayout.BeginHorizontal();
                DrawSectionTitle(label, uppercase: false);
                GUILayout.EndHorizontal();
                GUILayout.Space(CommonStyles.Space1);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Box("", CommonStyles.ImageUploadAreaStyle, GUILayout.ExpandWidth(true));
            Rect buttonRect = GUILayoutUtility.GetLastRect();

            Rect clearBtnRect = Rect.zero;
            if (uploadedImage != null)
            {
                float scaledWidth =
                    targetHeight * ((float)uploadedImage.width / uploadedImage.height);
                Rect imageRect = GetCenteredImageRect(buttonRect, scaledWidth, targetHeight);
                float clearBtnSize = EditorUiScale.S(22f);
                clearBtnRect = new Rect(
                    imageRect.x + imageRect.width - clearBtnSize + EditorUiScale.S(4f),
                    imageRect.y - EditorUiScale.S(4f),
                    clearBtnSize,
                    clearBtnSize
                );
            }

            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (clearBtnRect != Rect.zero && clearBtnRect.Contains(evt.mousePosition))
                {
                    imagePath = "";
                    if (uploadedImage != null)
                    {
                        if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(uploadedImage)))
                            UnityEngine.Object.DestroyImmediate(uploadedImage);
                        uploadedImage = null;
                    }
                    evt.Use();
                    onUserChanged?.Invoke();
                    repaint?.Invoke();
                }
                else if (buttonRect.Contains(evt.mousePosition))
                {
                    string path = EditorUtility.OpenFilePanel("选择图片", "", "jpg,png");
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        if (uploadedImage != null)
                        {
                            UnityEngine.Object.DestroyImmediate(uploadedImage);
                            uploadedImage = null;
                        }
                        imagePath = path;
                        var tex = new Texture2D(2, 2);
                        if (tex.LoadImage(File.ReadAllBytes(path)))
                        {
                            uploadedImage = tex;
                            onUserChanged?.Invoke();
                        }
                        else
                            UnityEngine.Object.DestroyImmediate(tex);
                    }
                    evt.Use();
                }
            }

            if (uploadedImage != null)
            {
                float scaledWidth =
                    targetHeight * ((float)uploadedImage.width / uploadedImage.height);
                Rect imageRect = GetCenteredImageRect(buttonRect, scaledWidth, targetHeight);
                GUI.DrawTexture(imageRect, uploadedImage, ScaleMode.ScaleToFit);
                GUI.Label(
                    clearBtnRect,
                    CommonStyles.ClearButtonSymbol,
                    CommonStyles.ClearButtonStyle
                );
            }
            else if (CommonStyles.ImagePreviewTexture != null)
            {
                float previewMax = EditorUiScale.S(200f);
                float previewH = EditorUiScale.S(70f);
                float topPad = EditorUiScale.S(10f);
                float sidePad = EditorUiScale.S(20f);
                float scaledWidth = Mathf.Min(previewMax, buttonRect.width - sidePad);
                Rect imageRect = new Rect(
                    buttonRect.x + (buttonRect.width - scaledWidth) / 2,
                    buttonRect.y + topPad,
                    scaledWidth,
                    previewH
                );
                GUI.DrawTexture(imageRect, CommonStyles.ImagePreviewTexture, ScaleMode.ScaleToFit);

                float hintBottomPadding = EditorUiScale.S(8f);
                float hintHeight = EditorUiScale.S(30f);
                Rect hintRect = new Rect(
                    buttonRect.x,
                    buttonRect.yMax - hintBottomPadding - hintHeight,
                    buttonRect.width,
                    hintHeight
                );
                GUI.Label(
                    hintRect,
                    "点击上传图片（支持JPG、PNG格式）",
                    CommonStyles.HintLabelStyle
                );
            }

            GUILayout.EndHorizontal();
        }

        private static Rect GetCenteredImageRect(
            Rect buttonRect,
            float scaledWidth,
            float targetHeight
        )
        {
            return new Rect(
                buttonRect.x + (buttonRect.width - scaledWidth) / 2,
                buttonRect.y + (buttonRect.height / 2) - (targetHeight / 2),
                scaledWidth,
                targetHeight
            );
        }

        /// <summary>
        /// 绘制参考图片缩略图网格：每张图带清除按钮，并支持「+ 添加图片」。
        /// 由调用方传入 imagePaths/uploadedImages，方法内部负责增删与加载缩略图。
        /// </summary>
        public static void DrawReferenceImagesGrid(
            List<string> imagePaths,
            List<Texture2D> uploadedImages,
            int maxReferenceImages,
            float availableWidth,
            float thumbSize,
            float clearSize,
            string addButtonLabel,
            string openFileTitle,
            string openFileFilter,
            string maxDialogMessage,
            Action repaint,
            Action onReferenceImagesMutated = null
        )
        {
            if (imagePaths == null || uploadedImages == null)
                return;

            int perRow = Mathf.Max(1, (int)(availableWidth / (thumbSize + 8f)));

            for (int row = 0; row * perRow < imagePaths.Count; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < perRow; col++)
                {
                    int i = row * perRow + col;
                    if (i >= imagePaths.Count)
                        break;

                    Texture2D thumb = i < uploadedImages.Count ? uploadedImages[i] : null;

                    GUILayout.BeginVertical(
                        GUILayout.Width(thumbSize + clearSize),
                        GUILayout.Height(thumbSize + clearSize)
                    );

                    Rect thumbRect = GUILayoutUtility.GetRect(thumbSize, thumbSize);
                    if (thumb != null)
                        GUI.DrawTexture(thumbRect, thumb, ScaleMode.ScaleToFit);
                    else
                        GUI.Box(thumbRect, "");

                    Rect clearBtnRect = new Rect(
                        thumbRect.xMax - clearSize,
                        thumbRect.y,
                        clearSize,
                        clearSize
                    );

                    if (GUI.Button(clearBtnRect, CommonStyles.ClearButtonSymbol, CommonStyles.ClearButtonStyle))
                    {
                        imagePaths.RemoveAt(i);
                        if (i < uploadedImages.Count)
                        {
                            if (uploadedImages[i] != null)
                                UnityEngine.Object.DestroyImmediate(uploadedImages[i]);
                            uploadedImages.RemoveAt(i);
                        }

                        onReferenceImagesMutated?.Invoke();
                        repaint?.Invoke();
                        GUIUtility.ExitGUI();
                    }

                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            bool atMax = imagePaths.Count >= maxReferenceImages;
            GUI.enabled = !atMax;

            if (GUILayout.Button(addButtonLabel, GUILayout.Width(EditorUiScale.S(120f)), GUILayout.Height(EditorUiScale.S(28f))))
            {
                string path = EditorUtility.OpenFilePanel(openFileTitle, "", openFileFilter);
                if (!string.IsNullOrEmpty(path))
                {
                    if (imagePaths.Contains(path))
                    {
                        GUIUtility.ExitGUI();
                    }
                    else if (imagePaths.Count >= maxReferenceImages)
                    {
                        Debug.LogWarning($"[TJGenerators] {maxDialogMessage}");
                    }
                    else
                    {
                        imagePaths.Add(path);
                        var tex = new Texture2D(2, 2);
                        if (File.Exists(path) && tex.LoadImage(File.ReadAllBytes(path)))
                            uploadedImages.Add(tex);
                        else
                        {
                            UnityEngine.Object.DestroyImmediate(tex);
                            uploadedImages.Add(null);
                        }
                        onReferenceImagesMutated?.Invoke();
                        repaint?.Invoke();
                    }
                }
            }

            GUI.enabled = true;
            if (atMax)
                GUILayout.Label($"（最多 {maxReferenceImages} 张）", CommonStyles.StatusStyle);
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 主编辑器窗口内居中矩形；连续打开多个 Codely 主窗口时在中心基础上叠加偏移（与生成器窗口一致）。
        /// </summary>
        private static int _mainWindowOpenCountForDefaultRect;

        public static Rect GetDefaultMainWindowRect()
        {
            return GetDefaultMainWindowRect(
                EditorUiScale.S(1200f),
                EditorUiScale.S(900f),
                EditorUiScale.S(24f),
                20);
        }

        /// <summary>
        /// 主编辑器窗口内居中矩形；连续打开多个 Codely 主窗口时在中心基础上叠加偏移（与生成器窗口一致）。
        /// <paramref name="width"/>、<paramref name="height"/>、<paramref name="stackOffset"/> 为最终布局像素，不再二次缩放。
        /// </summary>
        public static Rect GetDefaultMainWindowRect(float width, float height, float stackOffset, int wrapCount = 20)
        {
            Rect mainRect = EditorGUIUtility.GetMainWindowPosition();

            float offset = stackOffset * _mainWindowOpenCountForDefaultRect;
            var rect = new Rect(
                mainRect.x + (mainRect.width - width) * 0.5f + offset,
                mainRect.y + (mainRect.height - height) * 0.5f + offset,
                width,
                height);

            float maxX = mainRect.xMax - rect.width;
            float maxY = mainRect.yMax - rect.height;
            rect.x = Mathf.Clamp(rect.x, mainRect.x, maxX);
            rect.y = Mathf.Clamp(rect.y, mainRect.y, maxY);

            _mainWindowOpenCountForDefaultRect =
                (_mainWindowOpenCountForDefaultRect + 1) % Mathf.Max(1, wrapCount);
            return rect;
        }

        /// <summary>
        /// 两列自适应布局的计算结果
        /// </summary>
        public struct AdaptiveLayoutParams
        {
            public bool IsVerticalLayout;
            public float LeftPanelWidth;
            public float HistoryPanelWidth;
            public float SettingsPanelHeight;
        }

        /// <summary>
        /// 固定左右分栏布局的计算结果：
        /// - 左侧宽度固定
        /// - 右侧宽度自适应并受最小宽度保护
        /// </summary>
        public struct FixedSplitLayoutParams
        {
            public float LeftPanelWidth;
            public float RightPanelWidth;
            public float GapWidth;
            public float WindowMinWidth;
            public float WindowMinHeight;
        }

        /// <summary>
        /// 计算两列自适应布局参数
        /// </summary>
        /// <param name="windowWidth">窗口宽度</param>
        /// <param name="windowHeight">窗口高度</param>
        /// <returns>布局参数</returns>
        public static AdaptiveLayoutParams CalculateAdaptiveLayout(
            float windowWidth,
            float windowHeight)
        {
            bool isVertical = windowWidth < CommonStyles.LayoutSwitchThreshold;

            float historyWidth = isVertical
                ? windowWidth - CommonStyles.OuterMargin * 2
                : Mathf.Max(windowWidth - CommonStyles.LeftPanelWidth - CommonStyles.OuterMargin, CommonStyles.MinHistoryPanelWidth);

            float leftWidth = isVertical
                ? windowWidth - CommonStyles.OuterMargin * 2
                : CommonStyles.LeftPanelWidth;

            float settingsHeight = isVertical
                ? windowHeight * 0.55f
                : windowHeight;

            return new AdaptiveLayoutParams
            {
                IsVerticalLayout = isVertical,
                LeftPanelWidth = leftWidth,
                HistoryPanelWidth = historyWidth,
                SettingsPanelHeight = settingsHeight
            };
        }

        /// <summary>
        /// 计算固定左右分栏布局参数。
        /// 左侧固定宽度，右侧根据窗口宽度拉伸，并保证不小于 minRightPanelWidth。
        /// </summary>
        public static FixedSplitLayoutParams CalculateFixedSplitLayout(
            float windowWidth,
            float minWindowHeight,
            float leftPanelFixedWidth,
            float minRightPanelWidth,
            float gapWidth)
        {
            float safeLeft = Mathf.Max(1f, leftPanelFixedWidth);
            float safeRightMin = Mathf.Max(1f, minRightPanelWidth);
            float safeGap = Mathf.Max(0f, gapWidth);
            float windowMinWidth = safeLeft + safeGap + safeRightMin;
            float rightWidth = Mathf.Max(safeRightMin, windowWidth - safeLeft - safeGap);
            return new FixedSplitLayoutParams
            {
                LeftPanelWidth = safeLeft,
                RightPanelWidth = rightWidth,
                GapWidth = safeGap,
                WindowMinWidth = windowMinWidth,
                WindowMinHeight = Mathf.Max(1f, minWindowHeight)
            };
        }

        /// <summary>
        /// 开始两列自适应布局
        /// </summary>
        public static void BeginAdaptiveLayout(bool isVerticalLayout)
        {
            GUILayout.BeginHorizontal();
            if (isVerticalLayout)
            {
                GUILayout.Space(CommonStyles.OuterMargin);
                GUILayout.BeginVertical();
            }
        }

        /// <summary>
        /// 结束两列自适应布局
        /// </summary>
        public static void EndAdaptiveLayout(bool isVerticalLayout)
        {
            if (isVerticalLayout)
            {
                GUILayout.EndVertical();
                GUILayout.Space(CommonStyles.OuterMargin);
            }
            GUILayout.EndHorizontal();
        }

        public static AdaptiveLayoutScope BeginAdaptiveLayoutScope(bool isVerticalLayout)
        {
            var scope = new AdaptiveLayoutScope(isVerticalLayout);
            scope.Begin();
            return scope;
        }

        /// <summary>
        /// 开始设置面板（左侧面板），自动处理顶部间距
        /// </summary>
        /// <param name="scrollPosition">滚动位置</param>
        /// <param name="layout">布局参数</param>
        /// <returns>更新后的滚动位置</returns>
        public static Vector2 BeginSettingsPanel(Vector2 scrollPosition, AdaptiveLayoutParams layout)
        {
            GUILayout.BeginVertical(GUILayout.Width(layout.LeftPanelWidth), GUILayout.MinWidth(layout.LeftPanelWidth), GUILayout.MaxWidth(layout.LeftPanelWidth));
            var newScrollPos = GUILayout.BeginScrollView(scrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar,
                GUILayout.Height(layout.SettingsPanelHeight), GUILayout.Width(layout.LeftPanelWidth), GUILayout.MaxWidth(layout.LeftPanelWidth));
            GUILayout.BeginVertical(CommonStyles.SettingsPanelStyle);
            return newScrollPos;
        }

        /// <summary>
        /// 结束设置面板（左侧面板）
        /// </summary>
        public static void EndSettingsPanel()
        {
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        public static SettingsPanelScope BeginSettingsPanelScope(Vector2 scrollPosition, AdaptiveLayoutParams layout)
        {
            var scope = new SettingsPanelScope(scrollPosition);
            scope.Begin(layout);
            return scope;
        }

        /// <summary>
        /// 开始历史面板（右侧面板），自动处理顶部间距和内边距
        /// </summary>
        /// <param name="panelWidth">面板宽度</param>
        /// <param name="panelHeight">面板高度</param>
        /// <param name="isVerticalLayout">是否为垂直布局</param>
        public static void BeginHistoryPanel(float panelWidth, float panelHeight, bool isVerticalLayout)
        {
            GUILayout.BeginVertical(GUILayout.Width(panelWidth), GUILayout.MinWidth(panelWidth), GUILayout.Height(panelHeight));
            GUILayout.BeginVertical(CommonStyles.GetHistoryPanelStyle(isVerticalLayout));
        }

        /// <summary>
        /// 结束历史面板（右侧面板）
        /// </summary>
        public static void EndHistoryPanel()
        {
            GUILayout.EndVertical();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// 在历史面板顶部绘制选中条目的纹理预览。
        /// - 单列（垂直布局）时，高度固定为历史面板高度的一半；
        /// - 双列（水平布局）时，高度最多 200，且不超过可用宽度。
        /// 返回整个预览区（含下方间距）的高度，供调用方计算滚动区域高度。
        /// </summary>
        public static float DrawHistoryTexturePreview(
            Texture2D previewTex,
            bool isVerticalLayout,
            float panelWidth,
            float historyPanelHeight)
        {
            if (previewTex == null)
                return 0f;

            float maxPreviewWidth = Mathf.Max(0f, CommonStyles.HistoryPanelInnerWidth(panelWidth));
            if (maxPreviewWidth <= 0f || historyPanelHeight <= 0f)
                return 0f;

            float previewHeight = isVerticalLayout
                ? Mathf.Max(0f, historyPanelHeight * 0.5f)
                : Mathf.Min(maxPreviewWidth, 200f);

            float previewWidth = Mathf.Min(maxPreviewWidth, previewHeight);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var previewRect = GUILayoutUtility.GetRect(previewWidth, previewHeight);
            GUI.DrawTexture(previewRect, previewTex, ScaleMode.ScaleToFit);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);

            return previewHeight + 5f;
        }

        /// <summary>
        /// 绘制两列自适应布局的背景色（含分割线）
        /// </summary>
        /// <param name="windowRect">窗口矩形区域</param>
        /// <param name="isVerticalLayout">是否为垂直布局</param>
        /// <param name="leftPanelWidth">左侧面板宽度（水平布局时使用）</param>
        /// <param name="settingsPanelHeight">设置面板高度（垂直布局时使用）</param>
        public static void DrawAdaptiveLayoutBackground(
            Rect windowRect,
            bool isVerticalLayout,
            float leftPanelWidth,
            float settingsPanelHeight)
        {
            const float separatorThickness = 1f;

            if (isVerticalLayout)
            {
                Rect topSeparatorRect = new Rect(0, 0, windowRect.width, separatorThickness);
                Rect topRect = new Rect(0, separatorThickness, windowRect.width, settingsPanelHeight - separatorThickness);
                Rect middleSeparatorRect = new Rect(0, settingsPanelHeight, windowRect.width, separatorThickness);
                Rect bottomRect = new Rect(0, settingsPanelHeight + separatorThickness, windowRect.width, windowRect.height - settingsPanelHeight - separatorThickness);
                EditorGUI.DrawRect(topSeparatorRect, CommonStyles.LayoutSeparatorColor);
                EditorGUI.DrawRect(topRect, CommonStyles.WindowBackgroundColor);
                EditorGUI.DrawRect(middleSeparatorRect, CommonStyles.LayoutSeparatorColor);
                EditorGUI.DrawRect(bottomRect, CommonStyles.EmptyAreaBackgroundColor);
            }
            else
            {
                Rect leftTopSeparatorRect = new Rect(0, 0, leftPanelWidth, separatorThickness);
                Rect leftRect = new Rect(0, separatorThickness, leftPanelWidth, windowRect.height - separatorThickness);
                Rect middleSeparatorRect = new Rect(leftPanelWidth, 0, separatorThickness, windowRect.height);
                Rect rightTopSeparatorRect = new Rect(leftPanelWidth + separatorThickness, 0, windowRect.width - leftPanelWidth - separatorThickness, separatorThickness);
                Rect rightRect = new Rect(leftPanelWidth + separatorThickness, separatorThickness, windowRect.width - leftPanelWidth - separatorThickness, windowRect.height - separatorThickness);
                EditorGUI.DrawRect(leftTopSeparatorRect, CommonStyles.LayoutSeparatorColor);
                EditorGUI.DrawRect(leftRect, CommonStyles.WindowBackgroundColor);
                EditorGUI.DrawRect(middleSeparatorRect, CommonStyles.LayoutSeparatorColor);
                EditorGUI.DrawRect(rightTopSeparatorRect, CommonStyles.LayoutSeparatorColor);
                EditorGUI.DrawRect(rightRect, CommonStyles.EmptyAreaBackgroundColor);
            }
        }
    }
}
#endif
