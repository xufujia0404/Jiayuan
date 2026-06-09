using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TowerDefense.Data;

namespace TowerDefense.UI
{
    public class AchievementPanel : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _closeButton;

        [Header("Header")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _totalCountText;
        [SerializeField] private Slider _totalProgressSlider;
        [SerializeField] private Text _totalPercentText;

        [Header("Milestones")]
        [SerializeField] private Transform _milestoneContainer;
        [SerializeField] private GameObject _milestonePrefab;

        [Header("Tabs")]
        [SerializeField] private Button _tabAll;
        [SerializeField] private Button _tabGrowth;
        [SerializeField] private Button _tabBattle;
        [SerializeField] private Button _tabCollection;
        [SerializeField] private Button _tabExploration;
        [SerializeField] private Button _tabFun;
        [SerializeField] private Color _tabActiveColor = new Color(0.45f, 0.75f, 0.3f, 1f);
        [SerializeField] private Color _tabInactiveColor = new Color(0.95f, 0.93f, 0.85f, 1f);

        [Header("Content")]
        [SerializeField] private Transform _itemContainer;
        [SerializeField] private GameObject _achievementItemPrefab;

        [Header("Claim All")]
        [SerializeField] private Button _claimAllButton;

        [Header("Data")]
        [SerializeField] private string _jsonPath = "Data/Achievements";

        private AchievementData _data;
        private bool _isOpen;
        private AchievementCategory _currentCategory = AchievementCategory.All;
        private List<GameObject> _spawnedItems = new List<GameObject>();
        private Dictionary<AchievementCategory, Button> _tabButtons = new Dictionary<AchievementCategory, Button>();
        private GameObject _torchRoot;
        private MovingClouds _clouds;
        private ParticleSystem _snowParticles;
        private int _snowOriginalSortOrder;

        private void Awake()
        {
            _tabButtons[AchievementCategory.All] = _tabAll;
            _tabButtons[AchievementCategory.Growth] = _tabGrowth;
            _tabButtons[AchievementCategory.Battle] = _tabBattle;
            _tabButtons[AchievementCategory.Collection] = _tabCollection;
            _tabButtons[AchievementCategory.Exploration] = _tabExploration;
            _tabButtons[AchievementCategory.Fun] = _tabFun;

            if (_closeButton != null)
                _closeButton.onClick.AddListener(ClosePanel);

            if (_claimAllButton != null)
                _claimAllButton.onClick.AddListener(ClaimAll);

            foreach (var kvp in _tabButtons)
            {
                if (kvp.Value != null)
                {
                    var cat = kvp.Key;
                    kvp.Value.onClick.AddListener(() => SwitchTab(cat));
                }
            }

            _torchRoot = GameObject.Find("火把");
            GameObject bgObj = GameObject.Find("背景");
            if (bgObj != null)
            {
                _clouds = bgObj.GetComponent<MovingClouds>();
                _snowParticles = bgObj.GetComponentInChildren<ParticleSystem>();
                if (_snowParticles != null)
                {
                    var renderer = _snowParticles.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                        _snowOriginalSortOrder = renderer.sortingOrder;
                }
            }

            LoadData();
        }

        private void Start()
        {
            // 首次启动时隐藏面板，但避免在 OpenPanel 调用后误关
            if (_panel != null && !_isOpen)
                _panel.SetActive(false);
        }

        private void LoadData()
        {
            var jsonFile = Resources.Load<TextAsset>(_jsonPath);
            if (jsonFile != null)
            {
                _data = JsonUtility.FromJson<AchievementData>(jsonFile.text);
                Debug.Log($"[AchievementPanel] Loaded {_data.items.Length} achievements");
            }
            else
            {
                Debug.LogError($"[AchievementPanel] Failed to load: Resources/{_jsonPath}");
                _data = new AchievementData { items = new AchievementItemData[0] };
            }
        }

        public void OpenPanel()
        {
            _isOpen = true;
            if (_panel != null)
                _panel.SetActive(true);

            if (_torchRoot != null) _torchRoot.SetActive(false);
            if (_clouds != null) _clouds.IsPaused = true;
            if (_snowParticles != null)
            {
                var r = _snowParticles.GetComponent<ParticleSystemRenderer>();
                if (r != null) r.sortingOrder = -10;
                _snowParticles.Pause(true);
            }

            LoadData();
            UpdateHeader();
            SwitchTab(AchievementCategory.All);
        }

        public void ClosePanel()
        {
            _isOpen = false;
            if (_panel != null)
                _panel.SetActive(false);

            if (_torchRoot != null) _torchRoot.SetActive(true);
            if (_clouds != null) _clouds.IsPaused = false;
            if (_snowParticles != null)
            {
                var r = _snowParticles.GetComponent<ParticleSystemRenderer>();
                if (r != null) r.sortingOrder = _snowOriginalSortOrder;
                _snowParticles.Play(true);
            }
        }

        private void SwitchTab(AchievementCategory category)
        {
            _currentCategory = category;
            UpdateTabVisuals();
            RefreshItemList();
        }

        private void UpdateTabVisuals()
        {
            foreach (var kvp in _tabButtons)
            {
                if (kvp.Value == null) continue;
                var colors = kvp.Value.colors;
                colors.normalColor = kvp.Key == _currentCategory ? _tabActiveColor : _tabInactiveColor;
                colors.selectedColor = colors.normalColor;
                colors.highlightedColor = kvp.Key == _currentCategory
                    ? _tabActiveColor
                    : new Color(0.85f, 0.83f, 0.75f, 1f);
                kvp.Value.colors = colors;
            }
        }

        private void UpdateHeader()
        {
            if (_data == null) return;

            int total = _data.items.Length;
            int completed = _data.items.Count(i => i.status == AchievementStatus.Claimed);
            float pct = total > 0 ? (float)completed / total : 0f;

            if (_totalCountText != null)
                _totalCountText.text = $"{completed}/{total}";

            if (_totalProgressSlider != null)
            {
                _totalProgressSlider.maxValue = total;
                _totalProgressSlider.value = completed;
            }

            if (_totalPercentText != null)
                _totalPercentText.text = $"{(int)(pct * 100)}%";

            // 一键领取按钮
            if (_claimAllButton != null)
            {
                bool hasClaimable = _data.items.Any(i => i.status == AchievementStatus.Claimable);
                _claimAllButton.interactable = hasClaimable;
            }
        }

        private void RefreshItemList()
        {
            foreach (var item in _spawnedItems)
            {
                if (item != null) DestroyImmediate(item);
            }
            _spawnedItems.Clear();

            if (_itemContainer == null || _achievementItemPrefab == null) return;

            var items = _data.items
                .Where(i => _currentCategory == AchievementCategory.All || i.category == _currentCategory)
                .OrderBy(i => (int)i.status)
                .ThenBy(i => i.sortIndex);

            foreach (var itemData in items)
            {
                GameObject itemObj = Instantiate(_achievementItemPrefab, _itemContainer);
                itemObj.SetActive(true);
                _spawnedItems.Add(itemObj);

                var ui = itemObj.GetComponent<AchievementItemUI>();
                if (ui != null)
                    ui.Setup(itemData, this);
            }
        }

        public void OnClaimClicked(AchievementItemData itemData)
        {
            if (itemData.status != AchievementStatus.Claimable) return;

            var wallet = PlayerWallet.Instance;
            if (wallet == null) return;

            GrantRewards(itemData.rewards);

            itemData.status = AchievementStatus.Claimed;
            Debug.Log($"[AchievementPanel] 领取: {itemData.name}");

            UpdateHeader();
            RefreshItemList();
        }

        private void ClaimAll()
        {
            var wallet = PlayerWallet.Instance;
            if (wallet == null) return;

            var claimable = _data.items.Where(i => i.status == AchievementStatus.Claimable).ToList();
            if (claimable.Count == 0) return;

            foreach (var item in claimable)
            {
                GrantRewards(item.rewards);
                item.status = AchievementStatus.Claimed;
            }

            Debug.Log($"[AchievementPanel] 一键领取 {claimable.Count} 个成就");
            UpdateHeader();
            RefreshItemList();
        }

        private void GrantRewards(AchievementReward[] rewards)
        {
            if (rewards == null) return;
            var wallet = PlayerWallet.Instance;
            foreach (var r in rewards)
            {
                switch (r.rewardType)
                {
                    case RewardType.Gold: wallet?.AddGold(r.amount); break;
                    case RewardType.Diamond: wallet?.AddDiamond(r.amount); break;
                    case RewardType.Stamina: wallet?.AddStamina(r.amount); break;
                    case RewardType.Exp: PlayerLevelSystem.Instance?.AddExp(r.amount); break;
                }
            }
        }

        public void UpdateAchievementProgress(int id, int currentCount)
        {
            if (_data == null) return;
            var item = _data.items.FirstOrDefault(i => i.id == id);
            if (item == null) return;

            item.currentCount = currentCount;
            if (item.currentCount >= item.targetCount && item.status == AchievementStatus.InProgress)
            {
                item.status = AchievementStatus.Claimable;
                Debug.Log($"[AchievementPanel] 成就可领取: {item.name}");
            }
        }

        public static string GetCategoryName(AchievementCategory cat)
        {
            return cat switch
            {
                AchievementCategory.All => "全部",
                AchievementCategory.Growth => "成长",
                AchievementCategory.Battle => "战斗",
                AchievementCategory.Collection => "收集",
                AchievementCategory.Exploration => "探索",
                AchievementCategory.Fun => "趣味",
                _ => "未知"
            };
        }
    }
}
