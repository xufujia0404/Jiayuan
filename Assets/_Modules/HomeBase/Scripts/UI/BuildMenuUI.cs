using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sttop5.Shared.Player;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 建造菜单 UI，显示可建造的建筑列表，支持分类筛选。
    /// 点击建筑按钮后进入建造模式（由 BuildModeController 管理）。
    /// </summary>
    public class BuildMenuUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private Transform _buttonContainer;
        [SerializeField] private GameObject _buildButtonPrefab;
        [SerializeField] private Button _closeButton;

        [Header("分类标签")]
        [SerializeField] private Button _tabResource;
        [SerializeField] private Button _tabPortal;
        [SerializeField] private Button _tabStorage;
        [SerializeField] private Button _tabDecoration;
        [SerializeField] private Button _tabSpecial;
        [SerializeField] private Color _tabActiveColor = new Color(0.3f, 0.7f, 1f, 1f);
        [SerializeField] private Color _tabInactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("组件引用")]
        [SerializeField] private BuildingManager _buildingManager;
        [SerializeField] private BuildModeController _buildModeController;

        private BuildingType _currentTab = BuildingType.ResourceGen;
        private readonly Dictionary<BuildingType, Button> _tabButtons = new Dictionary<BuildingType, Button>();

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);

            // 注册分类标签点击事件
            _tabButtons[BuildingType.ResourceGen] = _tabResource;
            _tabButtons[BuildingType.Portal] = _tabPortal;
            _tabButtons[BuildingType.Storage] = _tabStorage;
            _tabButtons[BuildingType.Decoration] = _tabDecoration;
            _tabButtons[BuildingType.Special] = _tabSpecial;

            foreach (var kvp in _tabButtons)
            {
                if (kvp.Value != null)
                {
                    var type = kvp.Key;
                    kvp.Value.onClick.AddListener(() => SwitchTab(type));
                }
            }
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);
        }

        private void OnEnable()
        {
            RefreshButtons();
            UpdateTabVisuals();
        }

        #region 分类筛选

        /// <summary>
        /// 切换分类标签。
        /// </summary>
        public void SwitchTab(BuildingType type)
        {
            _currentTab = type;
            RefreshButtons();
            UpdateTabVisuals();
        }

        private void UpdateTabVisuals()
        {
            foreach (var kvp in _tabButtons)
            {
                if (kvp.Value == null) continue;
                var img = kvp.Value.GetComponent<Image>();
                if (img != null)
                {
                    img.color = kvp.Key == _currentTab ? _tabActiveColor : _tabInactiveColor;
                }
            }
        }

        #endregion

        #region 按钮生成

        /// <summary>
        /// 根据当前分类筛选并生成建筑按钮。
        /// </summary>
        public void RefreshButtons()
        {
            if (_buttonContainer == null || _buildingManager == null) return;

            // 清除旧按钮
            foreach (Transform child in _buttonContainer)
            {
                Destroy(child.gameObject);
            }

            var buildings = _buildingManager.AvailableBuildings;
            if (buildings == null) return;

            var profile = PlayerProfile.Instance;

            foreach (var data in buildings)
            {
                if (data == null) continue;
                // 按分类筛选
                if (data.buildingType != _currentTab) continue;

                GameObject buttonObj;
                if (_buildButtonPrefab != null)
                {
                    buttonObj = Instantiate(_buildButtonPrefab, _buttonContainer);
                }
                else
                {
                    buttonObj = CreateDefaultButton(data);
                }

                SetupButton(buttonObj, data, profile);
            }
        }

        private GameObject CreateDefaultButton(BuildingData data)
        {
            var go = new GameObject($"Btn_{data.buildingName}");
            go.transform.SetParent(_buttonContainer, false);

            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160f, 60f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            var button = go.AddComponent<Button>();

            // 名称文本
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(go.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            var levelData = data.GetLevelData(1);
            text.text = $"{data.buildingName}\n{levelData.buildCost}G";

            return go;
        }

        private void SetupButton(GameObject buttonObj, BuildingData data, PlayerProfile profile)
        {
            var button = buttonObj.GetComponent<Button>();
            if (button == null) return;

            var levelData = data.GetLevelData(1);
            bool canAfford = profile != null && profile.HasEnoughGold(levelData.buildCost);

            // 负担不起时变灰
            if (!canAfford)
            {
                var colors = button.colors;
                colors.normalColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                colors.highlightedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
                button.colors = colors;
            }

            button.onClick.AddListener(() =>
            {
                if (_buildModeController != null)
                {
                    _buildModeController.EnterBuildMode(data);
                    Close();
                }
            });
        }

        #endregion

        #region 打开/关闭

        public void Open()
        {
            gameObject.SetActive(true);
            RefreshButtons();
            UpdateTabVisuals();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        #endregion
    }
}
