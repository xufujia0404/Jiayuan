#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Codely.Newtonsoft.Json.Linq;
using TJGenerators.Config;
using TJGenerators.UI;
using TJGenerators.Utils;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace TJGenerators.AssetSearch
{
    /// <summary>
    /// Codely 资产库语义搜索：两列自适应布局，左列为搜索参数，右列为卡片式结果。
    /// </summary>
    public sealed class AssetLibrarySearchWindow : EditorWindow
    {
        /// <summary>
        /// 域/程序集重载时会销毁 EditorWindow；此时不应清会话缓存，否则无法在本次 Editor 会话内还原结果。
        /// </summary>
        private static bool s_suppressClearSessionOnDestroyForDomainReload;

        static AssetLibrarySearchWindow()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () =>
                s_suppressClearSessionOnDestroyForDomainReload = true;
            AssemblyReloadEvents.afterAssemblyReload += () =>
                s_suppressClearSessionOnDestroyForDomainReload = false;
        }

        // ===== Prefs Keys =====
        private const string PrefsQuery   = "TJGenerators.AssetLibrarySearch.QueryText";
        private const string PrefsCat3d   = "TJGenerators.AssetLibrarySearch.Filter3d";
        private const string PrefsCatAnim = "TJGenerators.AssetLibrarySearch.FilterAnimation";

        /// <summary>
        /// 本会话内最后一次成功搜索写入的缓存 id；用于 Play/域重载后从磁盘还原结果（EditorWindow 不持久化 _response）。
        /// </summary>
        private const string SessionLastSearchCacheIdKey = "TJGenerators.AssetLibrarySearch.LastSearchCacheQueryId";

        private const float CardDescriptionPreviewHeight = 84f;

        private const int RerankRetrieveTopK = 10;
        private const int SearchRetrieveTopK = 20;
        private const float LeftBottomStatusBarHeight = 56f;
        private const float FloatingCreditsWidth = 78f;
        private const float FloatingCreditsHeight = 40f;
        private const float FloatingCreditsEdge = 16f;
        private static readonly string[] SearchButtonIconCandidates =
        {
            "Assets/d__AIGen_codelyGenerator_Editor_EditorTextures_Icons_search_icon_white_4x.png",
            "Editor/EditorTextures/Icons/search_icon_white_4x.png",
            "Packages/cn.tuanjie.ai.generators/Editor/EditorTextures/Icons/search_icon_white_4x.png"
        };

        /// <summary>结果区卡片样式左右 margin 之和，须与 <see cref="InitStyles"/> 里 _cardStyle.margin 一致。</summary>
        private const float AssetSearchCardHorizontalMargins = 4f;

        private const float AssetSearchCardMinOuterWidth = 180f;
        private const float SearchListEdgePadding = 10f;
        private const float SearchListItemWidth = 199f;
        private const float SearchListItemHeight = 454f;
        private const float SearchListItemGap = 10f;
        private const float SearchItemInnerEdge = 13f;
        private const float SearchItemLabelGap = 10f;
        private const float SearchItemButtonWidth = 168f;
        private const float SearchItemButtonHeight = 34f;
        private const float SearchItemButtonGap = 10f;
        private const float SearchItemButtonBottom = 13f;

        // ===== Search Params =====
        private string _queryText        = "";
        private bool   _filter3d;
        private bool   _filterAnimation;

        // ===== Layout State =====
        private Vector2 _scrollLeft;
        private Vector2 _scrollRight;
        private float   _resultPanelWidth;
        private float   _leftPanelInnerWidth = 320f;

        // ===== Search State =====
        private bool                _busy;
        private string              _errorText;
        private AssetSearchResponse _response;
        private int _currentCredits;
        private bool _hasLoadedUserInfo;

        // ===== Preview Cache =====
        private readonly Dictionary<string, Texture2D>                          _previewCache   = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, GifAnimation>                       _gifAnimCache   = new Dictionary<string, GifAnimation>();
        private readonly Dictionary<string, (int frameIdx, double nextFrameTime)> _gifState     = new Dictionary<string, (int, double)>();
        private readonly HashSet<string>                                         _previewLoading = new HashSet<string>();
        private readonly HashSet<string>                                         _previewFailed  = new HashSet<string>();

        // ===== Lazy Styles =====
        private GUIStyle  _wordWrap;
        private GUIStyle  _groupQueryTitleStyle;
        private GUIStyle  _resultsHeaderStyle;
        private GUIStyle  _emptyStatePrimaryStyle;
        private GUIStyle  _emptyStateSecondaryStyle;
        private GUIStyle  _cardStyle;
        private GUIStyle  _cardCategorySourceStyle;
        private GUIStyle  _searchItemButtonStyle;
        private Texture2D _cardBgTex;
        private Texture2D _searchListFrameTex;
        private Texture2D _searchListItemFrameTex;
        private Texture2D _searchListItemFrameSelectedTex;
        private Texture2D _searchItemButtonTex;
        private Texture2D _searchButtonIcon;

        // ===== Open =====

        public static void Open()
        {
            var w = GetWindow<AssetLibrarySearchWindow>(false, "Tuanjie AI 资产库搜索", true);
            w.titleContent = new GUIContent("Tuanjie AI 资产库搜索");
            w.minSize = CommonStyles.MainWindowMinSize;
            w.position = UIComponents.GetDefaultMainWindowRect();
        }

        // ===== Lifecycle =====

        private void OnEnable()
        {
            wantsMouseMove = true;
            AssetDownloadService.TaskUpdated += OnDownloadTaskUpdated;
            EditorApplication.update         += OnEditorUpdate;

            RefreshUserInfo();
            TryRestoreResponseFromSessionCache();
        }

        private void OnDisable()
        {
            wantsMouseMove = false;
            AssetDownloadService.TaskUpdated -= OnDownloadTaskUpdated;
            EditorApplication.update         -= OnEditorUpdate;
        }

        private void OnDestroy()
        {
            foreach (var gif in _gifAnimCache.Values)
                foreach (var frame in gif.Frames)
                    if (frame != null) DestroyImmediate(frame);
            _gifAnimCache.Clear();

            if (!s_suppressClearSessionOnDestroyForDomainReload)
                ClearSessionSearchPersistence();
        }

        /// <summary>
        /// 用户关闭窗口后不再保留“上次搜索结果”的会话恢复数据，避免再次打开时空查询框但仍显示旧卡片。
        /// </summary>
        private static void ClearSessionSearchPersistence()
        {
            string queryId = SessionState.GetString(SessionLastSearchCacheIdKey, "");
            if (!string.IsNullOrEmpty(queryId))
                AssetSearchCache.Delete(queryId);
            SessionState.EraseString(SessionLastSearchCacheIdKey);
        }

        private void OnDownloadTaskUpdated(DownloadTaskInfo _) => Repaint();

        private void OnEditorUpdate()
        {
            if (_gifAnimCache.Count == 0) return;
            double now   = EditorApplication.timeSinceStartup;
            bool   dirty = false;
            foreach (var kvp in _gifAnimCache)
            {
                var gif = kvp.Value;
                if (gif.Frames.Length <= 1) continue;
                string url = kvp.Key;
                if (!_gifState.TryGetValue(url, out var s))
                {
                    _gifState[url] = (0, now + gif.Delays[0]);
                    continue;
                }
                if (now < s.nextFrameTime) continue;
                int next = (s.frameIdx + 1) % gif.Frames.Length;
                _gifState[url] = (next, s.nextFrameTime + gif.Delays[next]);
                dirty = true;
            }
            if (dirty) Repaint();
        }

        // ===== Style Init =====

        private void InitStyles()
        {
            if (_wordWrap == null)
                _wordWrap = new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = EditorUiScale.Font(10) };

            if (_groupQueryTitleStyle == null)
                _groupQueryTitleStyle = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };

            if (_resultsHeaderStyle == null)
            {
                _resultsHeaderStyle = new GUIStyle(CommonStyles.ContentStyle)
                {
                    font = CommonStyles.SourceHanSansMediumFont != null ? CommonStyles.SourceHanSansMediumFont : CommonStyles.SourceHanSansRegularFont,
                    fontSize = EditorUiScale.Font(16),
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleLeft
                };
                _resultsHeaderStyle.normal.textColor = Color.white;
                _resultsHeaderStyle.hover.textColor = Color.white;
                _resultsHeaderStyle.active.textColor = Color.white;
                _resultsHeaderStyle.focused.textColor = Color.white;
            }

            if (_emptyStatePrimaryStyle == null)
            {
                _emptyStatePrimaryStyle = new GUIStyle(CommonStyles.ContentStyle)
                {
                    font = CommonStyles.SourceHanSansRegularFont,
                    fontSize = EditorUiScale.Font(14),
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false
                };
                _emptyStatePrimaryStyle.normal.textColor = new Color(200f / 255f, 204f / 255f, 210f / 255f, 1f); // C8CCD2
            }

            if (_emptyStateSecondaryStyle == null)
            {
                _emptyStateSecondaryStyle = new GUIStyle(CommonStyles.ContentStyle)
                {
                    font = CommonStyles.SourceHanSansRegularFont,
                    fontSize = EditorUiScale.Font(14),
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false
                };
                _emptyStateSecondaryStyle.normal.textColor = new Color(102f / 255f, 102f / 255f, 102f / 255f, 1f); // 666666
            }

            if (_cardBgTex == null)
                _cardBgTex = CommonStyles.CreateSolidColorTexture(new Color(0.22f, 0.22f, 0.22f, 1f));

            if (_cardStyle == null)
            {
                _cardStyle = new GUIStyle(GUI.skin.box)
                {
                    padding  = new RectOffset(4, 4, 4, 6),
                    margin   = new RectOffset(0, 0, 0, 0),
                    overflow = new RectOffset(0, 0, 0, 0),
                };
                _cardStyle.normal.background = _searchListItemFrameTex != null ? _searchListItemFrameTex : _cardBgTex;
                _cardStyle.hover.background = _cardStyle.normal.background;
                _cardStyle.active.background = _cardStyle.normal.background;
                _cardStyle.focused.background = _cardStyle.normal.background;
            }

            if (_cardCategorySourceStyle == null)
            {
                _cardCategorySourceStyle = new GUIStyle(CommonStyles.SmallGreenLeftLabelStyle)
                {
                    wordWrap  = false,
                    clipping  = TextClipping.Clip,
                    margin    = new RectOffset(0, 0, 0, 0),
                    padding   = new RectOffset(0, 0, 0, 0),
                    alignment = TextAnchor.MiddleLeft,
                };
            }

            if (_searchListFrameTex == null)
                _searchListFrameTex = LoadSearchListFrameTexture();
            if (_searchListItemFrameTex == null)
            {
                _searchListItemFrameTex = LoadSearchListItemFrameTexture();
                if (_cardStyle != null && _searchListItemFrameTex != null)
                {
                    _cardStyle.normal.background = _searchListItemFrameTex;
                    _cardStyle.hover.background = _searchListItemFrameTex;
                    _cardStyle.active.background = _searchListItemFrameTex;
                    _cardStyle.focused.background = _searchListItemFrameTex;
                }
            }
            if (_searchListItemFrameSelectedTex == null)
                _searchListItemFrameSelectedTex = LoadSearchListItemFrameSelectedTexture();
            if (_searchItemButtonTex == null)
                _searchItemButtonTex = LoadSearchItemButtonTexture();
            if (_searchItemButtonStyle == null)
            {
                _searchItemButtonStyle = new GUIStyle(CommonStyles.ContentStyle)
                {
                    font = CommonStyles.SourceHanSansMediumFont != null ? CommonStyles.SourceHanSansMediumFont : CommonStyles.SourceHanSansRegularFont,
                    fontSize = EditorUiScale.Font(14),
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    wordWrap = false
                };
                _searchItemButtonStyle.normal.textColor = Color.white;
                _searchItemButtonStyle.hover.textColor = Color.white;
                _searchItemButtonStyle.active.textColor = Color.white;
                _searchItemButtonStyle.focused.textColor = Color.white;
            }
        }

        // ===== Main GUI =====

        private void OnGUI()
        {
            if (Event.current.type == EventType.MouseMove)
                Repaint();
            InitStyles();

            var splitLayout = UIComponents.CalculateFixedSplitLayout(
                position.width,
                CommonStyles.MainWindowMinSize.y,
                CommonStyles.LeftPanelFixedWidth,
                CommonStyles.MinHistoryPanelWidth,
                CommonStyles.OuterMargin);
            minSize = new Vector2(splitLayout.WindowMinWidth, splitLayout.WindowMinHeight);
            _resultPanelWidth = splitLayout.RightPanelWidth;
            _leftPanelInnerWidth = CommonStyles.LeftComponentWidth;

            UIComponents.DrawAdaptiveLayoutBackground(
                new Rect(0, 0, position.width, position.height),
                false,
                splitLayout.LeftPanelWidth,
                position.height);

            GUILayout.BeginHorizontal();

            // ===== LEFT PANEL – search parameters =====
            GUILayout.BeginVertical(
                GUILayout.Width(splitLayout.LeftPanelWidth),
                GUILayout.MinWidth(splitLayout.LeftPanelWidth),
                GUILayout.MaxWidth(splitLayout.LeftPanelWidth));
            _scrollLeft = GUILayout.BeginScrollView(
                _scrollLeft, GUIStyle.none, GUI.skin.verticalScrollbar,
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

            DrawParameterPanel();

            GUILayout.EndVertical();
            GUILayout.Space(CommonStyles.LeftContentPadding);
            GUILayout.EndHorizontal();
            GUILayout.Space(CommonStyles.LeftContentPadding);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(splitLayout.GapWidth);

            // ===== RIGHT PANEL – card results =====
            GUILayout.BeginVertical(
                GUILayout.Width(splitLayout.RightPanelWidth),
                GUILayout.MinWidth(splitLayout.RightPanelWidth));
            DrawResultsPanel(splitLayout.RightPanelWidth, position.height);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            DrawLeftBottomStatusBar(splitLayout.LeftPanelWidth);
        }

        // ===== Left Panel =====

        private void DrawParameterPanel()
        {
            UIComponents.DrawSectionTitle("查询（每行一个关键词）", uppercase: false);
            GUILayout.Space(CommonStyles.Space1);
            _queryText = UIComponents.DrawPromptInputBox(
                _queryText,
                "请输入查询关键词（每行一个）",
                "asset_search_query_input");

            GUILayout.Space(CommonStyles.Space2);
            UIComponents.DrawGapLine();
            GUILayout.Space(CommonStyles.Space2);

            UIComponents.DrawSectionTitle("分类过滤（可选）", uppercase: false);
            GUILayout.Space(CommonStyles.Space1);
            _filter3d = CommonStyles.DrawCheckboxRow("3d", _filter3d);
            GUILayout.Space(CommonStyles.Space1);
            _filterAnimation = CommonStyles.DrawCheckboxRow("animation", _filterAnimation);

            GUILayout.Space(CommonStyles.Space2);
            GUI.enabled = !_busy;
            bool searchClicked = UIComponents.DrawGenerateButtonWithIconLayout(
                _busy ? "搜索中…" : "搜索",
                GetSearchButtonIcon(),
                !_busy,
                24f,
                15f,
                GUILayout.Width(_leftPanelInnerWidth),
                GUILayout.MaxWidth(_leftPanelInnerWidth),
                GUILayout.Height(40f),
                GUILayout.ExpandWidth(false));
            if (searchClicked)
            {
                StartSearch();
            }
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_errorText))
            {
                GUILayout.Space(CommonStyles.Space1);
                EditorGUILayout.HelpBox(_errorText, MessageType.Error);
                if (_errorText.IndexOf("AUTH_REQUIRED", StringComparison.Ordinal) >= 0)
                {
                    if (GUILayout.Button("打开 Unity 登录…"))
                        Unity.UniAsset.Manager.Editor.InternalBridge.UnityConnectSession.instance.ShowLogin();
                }
            }
        }

        // ===== Right Panel =====

        /// <summary>资产库结果区与生成窗口历史 ScrollView 共用同一套宽度换算。</summary>
        private static float GetResultsScrollContentWidth(float panelOuterWidth)
            => CommonStyles.HistoryScrollViewLayoutWidth(panelOuterWidth);

        private void DrawResultsPanel(float panelW, float panelH)
        {
            const float edge = 16f;
            const float headerHeight = 24f;
            string header;
            if (_busy)
                header = "搜索中…";
            else if (_response != null && _response.Groups != null && _response.TotalItemCount > 0)
                header = $"结果（共 {_response.TotalItemCount} 条）";
            else
                header = "结果";
            GUILayout.Space(edge);
            GUILayout.BeginHorizontal();
            GUILayout.Space(edge);
            GUILayout.Label(
                header,
                _resultsHeaderStyle,
                GUILayout.Width(Mathf.Max(1f, panelW - edge * 2f)),
                GUILayout.Height(headerHeight));
            GUILayout.Space(edge);
            GUILayout.EndHorizontal();
            GUILayout.Space(edge);

            float listWidth = Mathf.Max(1f, panelW - edge * 2f);
            float usedHeight = edge + headerHeight + edge + edge;
            float listHeight = Mathf.Max(1f, panelH - usedHeight);

            GUILayout.BeginHorizontal();
            GUILayout.Space(edge);
            Rect listRect = GUILayoutUtility.GetRect(
                listWidth,
                listHeight,
                GUILayout.Width(listWidth),
                GUILayout.Height(listHeight));
            GUILayout.Space(edge);
            GUILayout.EndHorizontal();

            if (_searchListFrameTex != null)
                GUI.DrawTexture(listRect, _searchListFrameTex, ScaleMode.StretchToFill, true);
            else
                EditorGUI.DrawRect(listRect, new Color(28f / 255f, 31f / 255f, 40f / 255f, 1f));

            const float listPadding = SearchListEdgePadding;
            Rect listContentRect = new Rect(
                listRect.x + listPadding,
                listRect.y + listPadding,
                Mathf.Max(1f, listRect.width - listPadding * 2f),
                Mathf.Max(1f, listRect.height - listPadding * 2f));

            GUI.BeginGroup(listContentRect);
            GUILayout.BeginArea(new Rect(0f, 0f, listContentRect.width, listContentRect.height));
            _scrollRight = GUILayout.BeginScrollView(
                _scrollRight,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.Width(listContentRect.width),
                GUILayout.Height(listContentRect.height));

            if (_response != null && _response.Groups != null)
            {
                if (_response.Groups.Count == 0)
                {
                    EditorGUILayout.HelpBox("无匹配结果。", MessageType.Info);
                }
                else
                {
                    float availW = Mathf.Max(1f, listContentRect.width);
                    float columnStride = SearchListItemWidth + SearchListItemGap;
                    int cols = Mathf.Max(1, Mathf.FloorToInt((availW + SearchListItemGap) / columnStride));

                    for (int gi = 0; gi < _response.Groups.Count; gi++)
                    {
                        var group = _response.Groups[gi];
                        if (group.Items == null || group.Items.Count == 0) continue;

                        GUILayout.Label(
                            string.IsNullOrEmpty(group.Query) ? "（query）" : group.Query,
                            _groupQueryTitleStyle,
                            GUILayout.Width(availW),
                            GUILayout.MaxWidth(availW));
                        GUILayout.Space(SearchListItemGap);

                        for (int ii = 0; ii < group.Items.Count; ii += cols)
                        {
                            int   rowCount = Mathf.Min(cols, group.Items.Count - ii);

                            GUILayout.BeginHorizontal();
                            for (int j = 0; j < rowCount; j++)
                            {
                                DrawCard(gi, ii + j, group.Query ?? "", group.Items[ii + j]);
                                if (j < rowCount - 1)
                                    GUILayout.Space(SearchListItemGap);
                            }
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();
                            GUILayout.Space(SearchListItemGap);
                        }

                        GUILayout.Space(SearchListItemGap);
                    }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.EndGroup();

            if (!_busy && (_response == null || _response.Groups == null))
                DrawEmptySearchHintClipped(listContentRect);
        }

        private void DrawEmptySearchHintClipped(Rect listContentRect)
        {
            const string primaryText = "搜索结果将显示在这里";
            const string secondaryText = "输入关键词并点击搜索按钮，将为您查找相关的3D资产和动画资源";
            const float labelHeight = 20f;
            const float labelGap = 31f;
            float totalHeight = labelHeight * 2f + labelGap;
            float startY = (listContentRect.height - totalHeight) * 0.5f;

            Rect primaryRect = new Rect(
                8f,
                startY,
                Mathf.Max(1f, listContentRect.width - 16f),
                labelHeight);
            Rect secondaryRect = new Rect(
                primaryRect.x,
                primaryRect.yMax + labelGap,
                primaryRect.width,
                labelHeight);

            GUI.BeginGroup(listContentRect);
            GUI.Label(primaryRect, primaryText, _emptyStatePrimaryStyle);
            GUI.Label(secondaryRect, secondaryText, _emptyStateSecondaryStyle);
            GUI.EndGroup();
        }

        private static Texture2D LoadSearchListFrameTexture()
        {
            string[] candidates =
            {
                "Assets/codelyGenerator/Editor/EditorTextures/Frames/search_list_frame_1x.png",
                "Packages/cn.tuanjie.ai.generators/Editor/EditorTextures/Frames/search_list_frame_1x.png"
            };

            foreach (var path in candidates)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                    return tex;
            }

            string[] guids = AssetDatabase.FindAssets("search_list_frame_1x t:Texture2D");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                    return tex;
            }
            return null;
        }

        private static Texture2D LoadSearchListItemFrameTexture()
        {
            string[] candidates =
            {
                "Assets/d__AIGen_codelyGenerator_Editor_EditorTextures_Frames_search_list_item_frame_4x.png",
                "Assets/codelyGenerator/Editor/EditorTextures/Frames/search_list_item_frame_4x.png",
                "Packages/cn.tuanjie.ai.generators/Editor/EditorTextures/Frames/search_list_item_frame_4x.png"
            };

            foreach (var path in candidates)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                    return tex;
            }

            string[] guids = AssetDatabase.FindAssets("search_list_item_frame_4x t:Texture2D");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                    return tex;
            }
            return null;
        }

        private static Texture2D LoadSearchListItemFrameSelectedTexture()
        {
            string[] candidates =
            {
                "Assets/d__AIGen_codelyGenerator_Editor_EditorTextures_Frames_search_list_item_frame_selected4x.png",
                "Assets/codelyGenerator/Editor/EditorTextures/Frames/search_list_item_frame_selected4x.png",
                "Packages/cn.tuanjie.ai.generators/Editor/EditorTextures/Frames/search_list_item_frame_selected4x.png"
            };

            foreach (var path in candidates)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                    return tex;
            }

            string[] guids = AssetDatabase.FindAssets("search_list_item_frame_selected4x t:Texture2D");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                    return tex;
            }
            return null;
        }

        private static Texture2D LoadSearchItemButtonTexture()
        {
            string[] candidates =
            {
                "Assets/d__AIGen_codelyGenerator_Editor_EditorTextures_Button_search_item_button_4x.png",
                "Assets/codelyGenerator/Editor/EditorTextures/Button/search_item_button_4x.png",
                "Packages/cn.tuanjie.ai.generators/Editor/EditorTextures/Button/search_item_button_4x.png"
            };

            foreach (var path in candidates)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                    return tex;
            }

            string[] guids = AssetDatabase.FindAssets("search_item_button_4x t:Texture2D");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                    return tex;
            }
            return null;
        }

        private Texture2D GetSearchButtonIcon()
        {
            if (_searchButtonIcon == null)
            {
                foreach (var p in SearchButtonIconCandidates)
                {
                    _searchButtonIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                    if (_searchButtonIcon != null)
                        break;
                }
            }
            return _searchButtonIcon;
        }

        private void RefreshUserInfo()
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(
                UserInfoHelper.GetUserInfoCoroutine(
                    ConfigManager.GetUserInfoUrl(),
                    credits =>
                    {
                        if (!credits.HasValue)
                            return;
                        _currentCredits = credits.Value;
                        _hasLoadedUserInfo = true;
                        Repaint();
                    }));
        }

        private void DrawLeftBottomStatusBar(float leftPanelWidth)
        {
            Rect barRect = new Rect(
                0f,
                position.height - LeftBottomStatusBarHeight,
                leftPanelWidth,
                LeftBottomStatusBarHeight);
            EditorGUI.DrawRect(barRect, CommonStyles.WindowBackgroundColor);

            const float contentHeight = 40f;
            Rect contentRect = new Rect(barRect.x, barRect.y, barRect.width, contentHeight);
            DrawLeftBottomProfile(contentRect);

            var style = new GUIStyle(CommonStyles.BalanceStyle)
            {
                font = CommonStyles.SourceHanSansMediumFont != null ? CommonStyles.SourceHanSansMediumFont : CommonStyles.SourceHanSansRegularFont,
                fontSize = EditorUiScale.Font(14),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip
            };
            style.normal.textColor = new Color(1f / 255f, 167f / 255f, 127f / 255f, 1f);

            string text = _hasLoadedUserInfo ? $"点数：{_currentCredits}" : "点数：--";
            float maxW = Mathf.Max(FloatingCreditsWidth, leftPanelWidth - FloatingCreditsEdge * 2f);
            float desiredW = style.CalcSize(new GUIContent(text)).x;
            float w = Mathf.Min(maxW, Mathf.Max(FloatingCreditsWidth, desiredW));
            Rect rect = new Rect(
                leftPanelWidth - FloatingCreditsEdge - w,
                contentRect.y + (contentRect.height - FloatingCreditsHeight) * 0.5f,
                w,
                FloatingCreditsHeight);
            GUI.Label(rect, text, style);
        }

        private void DrawLeftBottomProfile(Rect contentRect)
        {
            const float edge = 16f;
            const float avatarSize = 40f;
            const float gap = 16f;
            const float lineGap = 4f;
            const float nameLineH = 21f;
            const float emailLineH = 14.3f;

            string username = UserInfoHelper.LastUserInfo?.username;
            string email = UserInfoHelper.LastUserInfo?.email;
            string nameText = string.IsNullOrEmpty(username) ? "User123" : username;
            string emailText = string.IsNullOrEmpty(email) ? "user123@unity.cn" : email;

            Rect avatarRect = new Rect(
                contentRect.x + edge,
                contentRect.y + (contentRect.height - avatarSize) * 0.5f,
                avatarSize,
                avatarSize);
            var avatar = CommonStyles.ProfileIconTexture;
            if (avatar != null)
                GUI.DrawTexture(avatarRect, avatar, ScaleMode.ScaleToFit, true);

            float textX = avatarRect.xMax + gap;
            float totalTextH = nameLineH + lineGap + emailLineH;
            float textY = contentRect.y + (contentRect.height - totalTextH) * 0.5f;
            float maxTextW = Mathf.Max(1f, contentRect.xMax - edge - textX - 120f);

            Rect nameRect = new Rect(textX, textY, maxTextW, nameLineH);
            Rect emailRect = new Rect(textX, nameRect.yMax + lineGap, maxTextW, emailLineH);
            GUI.Label(nameRect, nameText, CommonStyles.ProfileNameStyle);
            GUI.Label(emailRect, emailText, CommonStyles.ProfileEmailStyle);
        }

        // ===== Card =====

        private string BuildDescriptionPreviewText(string text, float width, int maxLines)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            float lineHeight = Mathf.Max(14f, _wordWrap.lineHeight > 0f ? _wordWrap.lineHeight : _wordWrap.fontSize * 1.4f);
            float maxHeight = lineHeight * Mathf.Max(1, maxLines);
            var full = new GUIContent(text);
            if (_wordWrap.CalcHeight(full, width) <= maxHeight)
                return text;

            int lo = 0;
            int hi = text.Length;
            string best = "…";
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                string candidate = text.Substring(0, mid).TrimEnd() + "…";
                float h = _wordWrap.CalcHeight(new GUIContent(candidate), width);
                if (h <= maxHeight)
                {
                    best = candidate;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return best;
        }

        /// <summary>
        /// 单行截断并在悬停时通过 <see cref="GUIContent.tooltip"/> 保留全文。
        /// </summary>
        private static string TruncateEndWithEllipsis(string text, GUIStyle style, float maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return text;
            maxWidth = Mathf.Max(1f, maxWidth);
            if (style == null) return text;
            if (style.CalcSize(new GUIContent(text)).x <= maxWidth) return text;
            const string suffix = "…";
            float suffixW = style.CalcSize(new GUIContent(suffix)).x;
            float budget = maxWidth - suffixW;
            if (budget <= 0f) return suffix;
            int lo = 0;
            int hi = text.Length;
            int best = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                string candidate = text.Substring(0, mid);
                if (style.CalcSize(new GUIContent(candidate)).x <= budget)
                {
                    best = mid;
                    lo   = mid + 1;
                }
                else hi = mid - 1;
            }
            if (best <= 0) return suffix;
            string prefix = text.Substring(0, best).TrimEnd();
            return string.IsNullOrEmpty(prefix) ? suffix : prefix + suffix;
        }

        private void DrawCard(int gi, int ii, string groupQuery, AssetSearchItem item)
        {
            Rect cardRect = GUILayoutUtility.GetRect(
                SearchListItemWidth,
                SearchListItemHeight,
                GUILayout.Width(SearchListItemWidth),
                GUILayout.MinWidth(SearchListItemWidth),
                GUILayout.MaxWidth(SearchListItemWidth),
                GUILayout.Height(SearchListItemHeight),
                GUILayout.MinHeight(SearchListItemHeight),
                GUILayout.MaxHeight(SearchListItemHeight));

            bool isHover = cardRect.Contains(Event.current.mousePosition);
            Texture2D frameTex = isHover && _searchListItemFrameSelectedTex != null
                ? _searchListItemFrameSelectedTex
                : _searchListItemFrameTex;
            if (frameTex != null)
                GUI.DrawTexture(cardRect, frameTex, ScaleMode.StretchToFill, true);
            else
                EditorGUI.DrawRect(cardRect, new Color(0.22f, 0.22f, 0.22f, 1f));

            float contentWidth = SearchListItemWidth - SearchItemInnerEdge * 2f;
            float firstButtonY = SearchListItemHeight - SearchItemButtonBottom - SearchItemButtonHeight * 2f - SearchItemButtonGap;
            Rect contentRect = new Rect(
                cardRect.x + SearchItemInnerEdge,
                cardRect.y + SearchItemInnerEdge,
                Mathf.Max(1f, contentWidth),
                Mathf.Max(1f, firstButtonY - SearchItemInnerEdge - SearchItemLabelGap));
            DrawCardContent(contentRect, item);

            bool alreadyImported = AssetDownloadService.TryGetImportedPrefabPath(
                item.Url, item.AssetId, item.PrefabPath, out _);

            float buttonX = cardRect.x + SearchItemInnerEdge;
            Rect primaryBtnRect = new Rect(buttonX, cardRect.y + firstButtonY, SearchItemButtonWidth, SearchItemButtonHeight);
            Rect secondaryBtnRect = new Rect(buttonX, primaryBtnRect.yMax + SearchItemButtonGap, SearchItemButtonWidth, SearchItemButtonHeight);
            if (alreadyImported)
            {
                DrawSearchItemButton(primaryBtnRect, "放入场景", () => ExecutePlaceInScene(item), enabled: true,
                    tooltip: "资源已在项目中，将当前条目对应的 Prefab 放入场景");
                DrawSearchItemButton(secondaryBtnRect, "已在项目中", onClick: null, enabled: false,
                    tooltip: "与当前条目共用同一资源包，无需重复下载");
            }
            else
            {
                DrawSearchItemButton(primaryBtnRect, "下载并放入场景", () => ExecuteDownload(groupQuery, item, true));
                DrawSearchItemButton(secondaryBtnRect, "仅下载到项目", () => ExecuteDownload(groupQuery, item, false));
            }
        }

        private void DrawCardContent(Rect contentRect, AssetSearchItem item)
        {
            bool hasDesc = !string.IsNullOrEmpty(item.Description);
            bool hasCatSrc = !string.IsNullOrEmpty(item.Category) || !string.IsNullOrEmpty(item.Source);
            float metaBlockH = hasCatSrc ? GetCategorySourceMetaLineBlockHeight() : 0f;

            float topHeight;
            if (hasDesc)
                topHeight = Mathf.Max(1f, contentRect.height - CardDescriptionPreviewHeight);
            else if (hasCatSrc)
                topHeight = Mathf.Max(1f, contentRect.height - metaBlockH);
            else
                topHeight = contentRect.height;

            float topW = Mathf.Max(1f, contentRect.width);

            GUI.BeginGroup(contentRect);

            GUILayout.BeginArea(new Rect(0f, 0f, topW, topHeight));

            Rect thumbRect = GUILayoutUtility.GetRect(topW, topW, GUILayout.Width(topW));
            DrawCardThumb(thumbRect, item);
            GUILayout.Space(SearchItemLabelGap);

            if (!string.IsNullOrEmpty(item.Name))
            {
                string nameDisp = TruncateEndWithEllipsis(item.Name, CommonStyles.ModelSelectNameStyle, topW);
                GUILayout.Label(new GUIContent(nameDisp, item.Name), CommonStyles.ModelSelectNameStyle, GUILayout.Width(topW));
                GUILayout.Space(SearchItemLabelGap);
            }

            if (!string.IsNullOrEmpty(item.AssetId))
            {
                GUILayout.Label("asset_id：", CommonStyles.SmallGreyLeftLabelStyle, GUILayout.Width(topW));
                string idDisp = TruncateEndWithEllipsis(item.AssetId, CommonStyles.SmallGreyLeftLabelStyle, topW);
                GUILayout.Label(new GUIContent(idDisp, item.AssetId), CommonStyles.SmallGreyLeftLabelStyle, GUILayout.Width(topW));
            }

            GUILayout.EndArea();

            if (hasDesc)
            {
                Rect previewFull = new Rect(0f, topHeight, contentRect.width, CardDescriptionPreviewHeight);
                if (hasCatSrc)
                {
                    Rect descRect = new Rect(previewFull.x, previewFull.y, previewFull.width, previewFull.height - metaBlockH);
                    Rect metaRect = new Rect(previewFull.x, descRect.yMax, previewFull.width, metaBlockH);
                    DrawCardDescriptionBlock(item, descRect);
                    DrawCategorySourceMetaLine(metaRect, item);
                }
                else
                    DrawCardDescriptionBlock(item, previewFull);
            }
            else if (hasCatSrc)
            {
                Rect metaRect = new Rect(0f, contentRect.height - metaBlockH, contentRect.width, metaBlockH);
                DrawCategorySourceMetaLine(metaRect, item);
            }

            GUI.EndGroup();
        }

        private float GetCategorySourceMetaLineBlockHeight()
        {
            var s = _cardCategorySourceStyle;
            if (s == null)
                return 14f + 4f;
            float lh = s.lineHeight > 0f ? s.lineHeight : Mathf.Max(12f, s.fontSize * 1.35f);
            return lh + 4f;
        }

        private static string BuildCategorySourceDisplayText(AssetSearchItem item)
        {
            bool hasCat = !string.IsNullOrEmpty(item.Category);
            bool hasSrc = !string.IsNullOrEmpty(item.Source);
            if (hasCat && hasSrc)
                return $"{item.Category} / {item.Source}";
            if (hasCat)
                return item.Category;
            return item.Source ?? "";
        }

        private void DrawCategorySourceMetaLine(Rect rect, AssetSearchItem item)
        {
            if (_cardCategorySourceStyle == null)
                return;
            float pad = 4f;
            float w = Mathf.Max(1f, rect.width - pad * 2f);
            string full = BuildCategorySourceDisplayText(item);
            if (string.IsNullOrEmpty(full))
                return;
            string disp = TruncateEndWithEllipsis(full, _cardCategorySourceStyle, w);
            GUI.Label(new Rect(rect.x + pad, rect.y, w, rect.height), new GUIContent(disp, full), _cardCategorySourceStyle);
        }

        private void DrawSearchItemButton(Rect rect, string text, Action onClick, bool enabled = true, string tooltip = null)
        {
            if (_searchItemButtonTex != null)
                GUI.DrawTexture(rect, _searchItemButtonTex, ScaleMode.StretchToFill, true);
            else
                EditorGUI.DrawRect(rect, new Color(0.20f, 0.20f, 0.20f, 1f));

            var content = new GUIContent(text ?? string.Empty, tooltip);
            if (enabled)
            {
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                    onClick?.Invoke();
            }
            else
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Arrow);

            Color prev = GUI.color;
            if (!enabled)
                GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * 0.45f);
            GUI.Label(rect, content, _searchItemButtonStyle);
            GUI.color = prev;
        }

        private void DrawCardDescriptionBlock(AssetSearchItem item, Rect previewRect)
        {
            float contentW = Mathf.Max(1f, previewRect.width - 8f);
            float lineHeight = Mathf.Max(14f, _wordWrap.lineHeight > 0f ? _wordWrap.lineHeight : _wordWrap.fontSize * 1.4f);
            int maxLines = Mathf.Max(1, Mathf.FloorToInt(previewRect.height / lineHeight));
            string preview = BuildDescriptionPreviewText(item.Description, contentW, maxLines);
            Rect textRect = new Rect(previewRect.x, previewRect.y, contentW, previewRect.height);

            EditorGUIUtility.AddCursorRect(textRect, MouseCursor.Link);
            if (GUI.Button(textRect, GUIContent.none, GUIStyle.none))
                AssetDescriptionDetailWindow.Open(item.Name, item.Description);

            GUI.Label(textRect, preview, _wordWrap);
        }

        private void DrawCardThumb(Rect rect, AssetSearchItem item)
        {
            var thumb = GetPreviewTexture(item.PreviewUrl);
            if (thumb != null)
            {
                GUI.DrawTexture(rect, thumb, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
                string label = _previewFailed.Contains(item.PreviewUrl ?? "")
                    ? "暂无预览"
                    : string.IsNullOrEmpty(item.PreviewUrl) ? "暂无预览" : "加载中…";
                GUI.Label(rect, label, CommonStyles.SmallGreyCenterLabelStyle);
                RequestPreviewIfNeeded(item.PreviewUrl);
            }
        }

        private void DrawCardDownload(string groupQuery, AssetSearchItem item, string fallbackQueryId)
        {
            string q = string.IsNullOrEmpty(groupQuery) ? (item.AssetId ?? fallbackQueryId) : groupQuery;

            bool alreadyImported = AssetDownloadService.TryGetImportedPrefabPath(
                item.Url, item.AssetId, item.PrefabPath, out _);

            if (alreadyImported)
            {
                if (GUILayout.Button(new GUIContent("放入场景", "资源已在项目中，将当前条目对应的 Prefab 放入场景"), GUILayout.Height(24)))
                    ExecutePlaceInScene(item);
                using (new EditorGUI.DisabledScope(true))
                    GUILayout.Button(new GUIContent("已在项目中", "与当前条目共用同一资源包，无需重复下载"), GUILayout.Height(20));
            }
            else
            {
                if (GUILayout.Button("下载并放入场景", GUILayout.Height(24)))
                    ExecuteDownload(q, item, instantiateInScene: true);

                var downloadBtnContent = new GUIContent("仅下载到项目", AssetDownloadService.DefaultDestBase);
                if (GUILayout.Button(downloadBtnContent, GUILayout.Height(20)))
                    ExecuteDownload(q, item, instantiateInScene: false);
            }
        }

        private void ExecutePlaceInScene(AssetSearchItem item)
        {
            try
            {
                if (!AssetDownloadService.TryGetImportedPrefabPath(item.Url, item.AssetId, item.PrefabPath, out var path)
                    || string.IsNullOrEmpty(path))
                {
                    TJLog.LogWarning("[AssetLibrarySearch] 放入场景失败：未找到已导入的 Prefab 路径");
                    return;
                }

                AssetPostImportProcessor.InstantiatePrefabInScene(path);
            }
            catch (Exception ex)
            {
                TJLog.LogError($"[AssetLibrarySearch] 放入场景失败: {ex.Message}");
            }
        }

        private void ExecuteDownload(string query, AssetSearchItem item, bool instantiateInScene)
        {
            try
            {
                var req    = CreateDownloadRequest(query, item, instantiateInScene);
                var result = AssetDownloadService.StartDownload(req);
                TJLog.Log(result.Status + " task=" + result.TaskId + " " + result.Message);
            }
            catch (Exception ex)
            {
                TJLog.LogError($"[AssetLibrarySearch] 下载失败: {ex.Message}");
            }
        }

        // ===== Search Logic =====

        private void StartSearch()
        {
            _errorText   = null;
            _response    = null;
            _previewCache.Clear();
            _gifAnimCache.Clear();
            _gifState.Clear();
            _previewFailed.Clear();
            _previewLoading.Clear();

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(_queryText))
            {
                foreach (var line in _queryText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var t = line.Trim();
                    if (!string.IsNullOrEmpty(t)) lines.Add(t);
                }
            }
            if (lines.Count == 0)
            {
                _errorText = "请至少输入一行关键词。";
                return;
            }

            var filter = new List<string>();
            if (_filter3d)        filter.Add("3d");
            if (_filterAnimation) filter.Add("animation");

            var request = new AssetSearchRequest
            {
                Queries          = lines,
                RerankTopK       = RerankRetrieveTopK,
                SearchTopK       = SearchRetrieveTopK,
                FilterByCategory = filter.Count > 0 ? filter : null,
            };

            try
            {
                CodelyTokenProvider.GetToken();
            }
            catch (InvalidOperationException ex)
            {
                _errorText = ex.Message;
                return;
            }

            _busy = true;
            EditorUtility.DisplayProgressBar("资产库", "正在搜索…", 0.4f);

            Task.Run(() =>
            {
                object    ok  = null;
                Exception err = null;
                try   { ok  = AssetSearchService.Search(request); }
                catch (Exception e) { err = e; }

                EditorApplication.delayCall += () =>
                {
                    try { EditorUtility.ClearProgressBar(); } catch { }
                    _busy = false;

                    if (err != null)
                    {
                        _errorText = err is InvalidOperationException ioe ? ioe.Message
                                   : err is HttpRequestException hre    ? hre.Message
                                   : err.Message;
                        TJLog.LogError($"[AssetLibrarySearch] {err}");
                    }
                    else
                    {
                        _response = (AssetSearchResponse)ok;
                        try
                        {
                            var items = BuildSessionPersistenceCacheItems(_response);
                            string queryId = AssetSearchCache.Write(items);
                            SessionState.SetString(SessionLastSearchCacheIdKey, queryId);
                        }
                        catch (Exception ex)
                        {
                            TJLog.LogWarning($"[AssetLibrarySearch] cache write: {ex.Message}");
                        }
                    }
                    Repaint();
                };
            });
        }

        /// <summary>
        /// Play 模式切换或脚本域重载后内存中的搜索结果会丢失；从本会话记录的缓存文件重建。
        /// </summary>
        private void TryRestoreResponseFromSessionCache()
        {
            if (_response != null && _response.Groups != null && _response.Groups.Count > 0)
                return;

            string queryId = SessionState.GetString(SessionLastSearchCacheIdKey, "");
            if (string.IsNullOrEmpty(queryId))
                return;

            var file = AssetSearchCache.Read(queryId);
            if (file == null || file.Items == null || file.Items.Count == 0)
                return;

            var restored = BuildResponseFromCacheFile(file);
            if (restored == null)
                return;

            _response                        = restored;
            _busy                            = false;
            _errorText                       = null;
            _previewCache.Clear();
            _gifAnimCache.Clear();
            _gifState.Clear();
            _previewFailed.Clear();
            _previewLoading.Clear();

            EditorApplication.delayCall += Repaint;
        }

        private static AssetSearchResponse BuildResponseFromCacheFile(AssetSearchCacheFile file)
        {
            if (file?.Items == null || file.Items.Count == 0)
                return null;

            var order = new List<string>();
            var map   = new Dictionary<string, AssetSearchGroup>(StringComparer.Ordinal);

            foreach (var row in file.Items)
            {
                string qKey = row.Query ?? "";
                if (!map.TryGetValue(qKey, out var grp))
                {
                    grp       = new AssetSearchGroup { Query = row.Query ?? "", Items = new List<AssetSearchItem>() };
                    map[qKey] = grp;
                    order.Add(qKey);
                }

                grp.Items.Add(CacheRowToAssetItem(row));
            }

            var groups = new List<AssetSearchGroup>(order.Count);
            int total  = 0;
            foreach (var q in order)
            {
                groups.Add(map[q]);
                total += map[q].Items.Count;
            }

            return new AssetSearchResponse { Groups = groups, TotalItemCount = total };
        }

        private static AssetSearchItem CacheRowToAssetItem(AssetSearchCacheItem row)
        {
            object prefabMeta = null;
            if (!string.IsNullOrEmpty(row.PrefabMeta))
            {
                try { prefabMeta = JToken.Parse(row.PrefabMeta); }
                catch { prefabMeta = row.PrefabMeta; }
            }

            var item = new AssetSearchItem
            {
                AssetId     = row.AssetId,
                Url         = row.Url,
                PrefabPath  = row.PrefabPath,
                Name        = row.Name,
                Category    = row.Category,
                Source      = row.Source,
                Score       = row.Score,
                Keywords    = row.Keywords ?? new List<string>(),
                Description = row.Description ?? "",
                PreviewUrl  = row.PreviewUrl ?? "",
                PrefabMeta  = prefabMeta,
            };

            return item;
        }

        /// <summary>
        /// 供本会话磁盘缓存使用：按接口返回的分组与顺序逐条写入，不按 asset_id 合并。
        /// 若此处去重，域重载或进入 Play 后 <see cref="TryRestoreResponseFromSessionCache"/> 还原的条数会少于首次展示的 TotalItemCount。
        /// （CustomTool 侧 <c>SearchAssetsTool</c> 仍可按 asset_id 合并以缩小缓存。）
        /// </summary>
        private static List<AssetSearchCacheItem> BuildSessionPersistenceCacheItems(AssetSearchResponse response)
        {
            var list = new List<AssetSearchCacheItem>();
            if (response?.Groups == null) return list;

            foreach (var group in response.Groups)
            {
                if (group.Items == null) continue;
                foreach (var item in group.Items)
                {
                    if (string.IsNullOrEmpty(item.AssetId)) continue;
                    list.Add(ToCacheItemRow(group.Query, item));
                }
            }
            return list;
        }

        private static AssetSearchCacheItem ToCacheItemRow(string query, AssetSearchItem item)
        {
            string prefabMetaJson = "";
            if (item.PrefabMeta != null)
                prefabMetaJson = ObjectToJsonStringLocal(item.PrefabMeta);
            else if (item.ExtraFields != null
                     && item.ExtraFields.TryGetValue("prefab_meta", out var pm)
                     && pm != null)
                prefabMetaJson = ObjectToJsonStringLocal(pm);

            return new AssetSearchCacheItem
            {
                Query       = query,
                AssetId     = item.AssetId,
                Url         = item.Url,
                PrefabPath  = item.PrefabPath,
                Name        = item.Name        ?? "",
                Category    = item.Category    ?? "",
                Source      = item.Source      ?? "",
                Score       = item.Score       ?? 0.0,
                Keywords    = item.Keywords    ?? new List<string>(),
                Description = item.Description ?? "",
                PreviewUrl  = item.PreviewUrl  ?? "",
                PrefabMeta  = prefabMetaJson,
            };
        }

        private static string ObjectToJsonStringLocal(object obj)
        {
            if (obj == null) return "";
            if (obj is string s) return s;
            try   { return JToken.FromObject(obj).ToString(); }
            catch { return obj.ToString(); }
        }

        private static string SerializeKeywordsForDownload(List<string> keywords)
        {
            if (keywords == null || keywords.Count == 0) return "";
            var arr = new JArray();
            foreach (var kw in keywords) arr.Add(kw);
            return arr.ToString();
        }

        private static AssetDownloadRequest CreateDownloadRequest(
            string query, AssetSearchItem item, bool instantiateInScene = false)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            return new AssetDownloadRequest
            {
                AssetId            = item.AssetId,
                Name               = item.Name ?? item.AssetId,
                PrefabPath         = item.PrefabPath,
                Url                = item.Url,
                Category           = item.Category,
                Source             = item.Source,
                Description        = item.Description,
                PrefabMeta         = item.PrefabMeta != null ? ObjectToJsonStringLocal(item.PrefabMeta) : "",
                Query              = query,
                Score              = (float)(item.Score ?? 0.0),
                Keywords           = SerializeKeywordsForDownload(item.Keywords),
                PreviewUrl         = item.PreviewUrl,
                InstantiateInScene = instantiateInScene,
            };
        }

        // ===== Preview Fetch =====

        private Texture2D GetPreviewTexture(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            if (IsGifUrl(url) && _gifAnimCache.TryGetValue(url, out var gif))
            {
                int idx = _gifState.TryGetValue(url, out var s) ? s.frameIdx : 0;
                var frame = gif.Frames[idx];
                if (frame != null) return frame;
                // Texture2D 帧已被销毁（例如退出 Play 模式时 Unity 回收了运行时对象）
                // 移除失效缓存，让 RequestPreviewIfNeeded 重新下载
                _gifAnimCache.Remove(url);
                _gifState.Remove(url);
                return null;
            }
            _previewCache.TryGetValue(url, out var t);
            return t;
        }

        private void RequestPreviewIfNeeded(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            bool alreadyLoaded = IsGifUrl(url)
                ? _gifAnimCache.ContainsKey(url)
                : _previewCache.TryGetValue(url, out var t) && t != null;
            if (alreadyLoaded) return;
            if (_previewLoading.Contains(url) || _previewFailed.Contains(url)) return;
            _previewLoading.Add(url);
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                EditorCoroutineUtility.StartCoroutineOwnerless(DownloadPreviewCoroutine(url));
            };
        }

        private static bool IsGifUrl(string url)
            => url != null && url.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);

        private System.Collections.IEnumerator DownloadPreviewCoroutine(string url)
        {
            if (IsGifUrl(url))
            {
                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = 30;
                    yield return request.SendWebRequest();
                    _previewLoading.Remove(url);
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var gif = GifDecoder.DecodeAll(request.downloadHandler.data);
                        if (gif != null)
                        {
                            _gifAnimCache[url] = gif;
                            Repaint();
                        }
                        else
                            _previewFailed.Add(url);
                    }
                    else
                    {
                        _previewFailed.Add(url);
                        TJLog.LogWarning($"[AssetLibrarySearch] 预览图失败: {url}  {request.error}");
                    }
                }
                yield break;
            }

            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = 30;
                yield return request.SendWebRequest();
                _previewLoading.Remove(url);
                if (request.result == UnityWebRequest.Result.Success)
                {
                    var tex = ((DownloadHandlerTexture)request.downloadHandler).texture;
                    if (tex != null)
                    {
                        _previewCache[url] = tex;
                        Repaint();
                    }
                    else
                        _previewFailed.Add(url);
                }
                else
                {
                    _previewFailed.Add(url);
                    TJLog.LogWarning($"[AssetLibrarySearch] 预览图失败: {url}  {request.error}");
                }
            }
        }
    }

    internal sealed class AssetDescriptionDetailWindow : EditorWindow
    {
        private string _titleText = "描述详情";
        private string _fullDescription = string.Empty;
        private Vector2 _scroll;
        private GUIStyle _contentStyle;

        public static void Open(string itemName, string description)
        {
            var window = GetWindow<AssetDescriptionDetailWindow>(true, "描述详情", true);
            window.minSize = new Vector2(EditorUiScale.S(520f), EditorUiScale.S(360f));
            window._titleText = string.IsNullOrEmpty(itemName) ? "描述详情" : itemName;
            window._fullDescription = string.IsNullOrEmpty(description) ? "暂无描述" : description;
            window.Show();
            window.Focus();
        }

        private void OnGUI()
        {
            if (_contentStyle == null)
            {
                _contentStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    fontSize = EditorUiScale.Font(12),
                    alignment = TextAnchor.UpperLeft,
                    clipping = TextClipping.Clip
                };
            }

            GUILayout.Space(CommonStyles.Space1);
            GUILayout.Label(_titleText, EditorStyles.boldLabel);
            GUILayout.Space(CommonStyles.Space1);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _fullDescription = EditorGUILayout.TextArea(
                _fullDescription,
                _contentStyle,
                GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
