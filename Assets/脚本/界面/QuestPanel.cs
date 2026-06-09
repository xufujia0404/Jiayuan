using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TowerDefense.Data;
using TowerDefense.Save;

namespace TowerDefense.UI
{
    public class QuestPanel : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _questPanel;
        [SerializeField] private Button _closeButton;

        [Header("Tabs")]
        [SerializeField] private Button _tabDaily;
        [SerializeField] private Button _tabMain;
        [SerializeField] private Button _tabAchievement;
        [SerializeField] private Button _tabEvent;
        [SerializeField] private Color _tabActiveColor = new Color(1f, 0.78f, 0.2f, 1f);
        [SerializeField] private Color _tabInactiveColor = new Color(0.3f, 0.6f, 0.3f, 1f);

        [Header("Content")]
        [SerializeField] private Transform _itemContainer;
        [SerializeField] private GameObject _questItemPrefab;

        [Header("Info Bar")]
        [SerializeField] private Text _resetTimerText;
        [SerializeField] private Text _activityText;

        [Header("Activity Bar")]
        [SerializeField] private Slider _activitySlider;
        [SerializeField] private Image[] _chestIcons;
        [SerializeField] private Sprite _chestLocked;
        [SerializeField] private Sprite _chestUnlocked;
        [SerializeField] private Sprite _chestClaimed;

        [Header("Data")]
        [SerializeField] private string _jsonPath = "Data/QuestItems";

        private QuestData _questData;
        private QuestCategory _currentCategory;
        private List<GameObject> _spawnedItems = new List<GameObject>();
        private Dictionary<QuestCategory, Button> _tabButtons = new Dictionary<QuestCategory, Button>();
        private GameObject _torchRoot;
        private MovingClouds _clouds;
        private ParticleSystem _snowParticles;
        private int _snowOriginalSortOrder;

        public void SetReferences(GameObject questPanel, Button closeButton, Button tabDaily, Button tabMain, Button tabAchievement, Button tabEvent, Transform itemContainer, GameObject questItemPrefab, Text resetTimerText, Text activityText, Slider activitySlider, Image[] chestIcons)
        {
            _questPanel = questPanel;
            _closeButton = closeButton;
            _tabDaily = tabDaily;
            _tabMain = tabMain;
            _tabAchievement = tabAchievement;
            _tabEvent = tabEvent;
            _itemContainer = itemContainer;
            _questItemPrefab = questItemPrefab;
            _resetTimerText = resetTimerText;
            _activityText = activityText;
            _activitySlider = activitySlider;
            _chestIcons = chestIcons;
        }

        private void Awake()
        {
            _tabButtons[QuestCategory.Daily] = _tabDaily;
            _tabButtons[QuestCategory.Main] = _tabMain;
            _tabButtons[QuestCategory.Achievement] = _tabAchievement;
            _tabButtons[QuestCategory.Event] = _tabEvent;

            if (_closeButton != null)
                _closeButton.onClick.AddListener(CloseQuest);

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

            LoadQuestData();
        }

        private void Start()
        {
            if (_questPanel != null)
                _questPanel.SetActive(false);
        }

        private void LoadQuestData()
        {
            var jsonFile = Resources.Load<TextAsset>(_jsonPath);
            if (jsonFile != null)
            {
                _questData = JsonUtility.FromJson<QuestData>(jsonFile.text);
                Debug.Log($"[QuestPanel] Loaded {_questData.items.Length} quests from JSON");
            }
            else
            {
                Debug.LogError($"[QuestPanel] Failed to load JSON: Resources/{_jsonPath}");
                _questData = new QuestData { items = new QuestItemData[0], dailyActivity = 0, dailyActivityMax = 120 };
                return;
            }

            ApplySaveData();
        }

        private void ApplySaveData()
        {
            var save = SaveSystem.Instance;
            if (save == null || save.CurrentSave == null) return;

            var questSave = save.CurrentSave.quests;
            if (questSave == null || questSave.items == null || questSave.items.Count == 0) return;

            // 每日任务重置检查
            string today = System.DateTime.Now.ToString("yyyyMMdd");
            if (questSave.dailyResetDate != today)
            {
                foreach (var item in _questData.items)
                {
                    if (item.category == QuestCategory.Daily)
                    {
                        item.currentCount = 0;
                        item.status = QuestStatus.InProgress;
                    }
                }
                questSave.dailyActivity = 0;
                questSave.dailyResetDate = today;
            }
            else
            {
                _questData.dailyActivity = questSave.dailyActivity;
            }

            // 恢复每个任务的进度
            foreach (var savedItem in questSave.items)
            {
                foreach (var questItem in _questData.items)
                {
                    if (questItem.id == savedItem.id)
                    {
                        questItem.currentCount = savedItem.currentCount;
                        questItem.status = (QuestStatus)savedItem.status;
                        break;
                    }
                }
            }

            Debug.Log($"[QuestPanel] Applied save data to quests");
        }

        private void PersistQuestData()
        {
            var save = SaveSystem.Instance;
            if (save == null || save.CurrentSave == null || _questData == null) return;

            var questSave = save.CurrentSave.quests;
            questSave.items.Clear();
            questSave.dailyActivity = _questData.dailyActivity;
            questSave.dailyResetDate = System.DateTime.Now.ToString("yyyyMMdd");

            foreach (var item in _questData.items)
            {
                questSave.items.Add(new GameSaveData.QuestItemSave
                {
                    id = item.id,
                    currentCount = item.currentCount,
                    status = (int)item.status
                });
            }

            save.SaveGame();
        }

        public void OpenQuest()
        {
            if (_questPanel != null)
                _questPanel.SetActive(true);

            if (_torchRoot != null)
                _torchRoot.SetActive(false);

            if (_clouds != null)
                _clouds.IsPaused = true;

            if (_snowParticles != null)
            {
                var renderer = _snowParticles.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                    renderer.sortingOrder = -10;
                _snowParticles.Pause(true);
            }

            ApplySaveData();
            SwitchTab(QuestCategory.Daily);
        }

        public void CloseQuest()
        {
            if (_questPanel != null)
                _questPanel.SetActive(false);

            if (_torchRoot != null)
                _torchRoot.SetActive(true);

            if (_clouds != null)
                _clouds.IsPaused = false;

            if (_snowParticles != null)
            {
                var renderer = _snowParticles.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                    renderer.sortingOrder = _snowOriginalSortOrder;
                _snowParticles.Play(true);
            }
        }

        private void SwitchTab(QuestCategory category)
        {
            _currentCategory = category;
            UpdateTabVisuals();
            RefreshItemList();
            UpdateInfoBar();
        }

        private void UpdateTabVisuals()
        {
            foreach (var kvp in _tabButtons)
            {
                if (kvp.Value == null) continue;
                var colors = kvp.Value.colors;
                colors.normalColor = kvp.Key == _currentCategory ? _tabActiveColor : _tabInactiveColor;
                colors.selectedColor = colors.normalColor;
                colors.highlightedColor = kvp.Key == _currentCategory ? _tabActiveColor : new Color(0.8f, 0.8f, 0.8f, 1f);
                kvp.Value.colors = colors;
            }
        }

        private void UpdateInfoBar()
        {
            if (_activityText != null && _questData != null)
                _activityText.text = $"⭐ {_questData.dailyActivity}";

            if (_activitySlider != null && _questData != null)
            {
                _activitySlider.maxValue = _questData.dailyActivityMax;
                _activitySlider.value = _questData.dailyActivity;
            }

            UpdateChestIcons();
        }

        private void UpdateChestIcons()
        {
            if (_questData == null || _chestIcons == null) return;
            int[] thresholds = { 30, 60, 90, 120 };
            for (int i = 0; i < _chestIcons.Length && i < thresholds.Length; i++)
            {
                if (_chestIcons[i] == null) continue;
                _chestIcons[i].sprite = _questData.dailyActivity >= thresholds[i] ? _chestUnlocked : _chestLocked;
            }
        }

        private void RefreshItemList()
        {
            foreach (var item in _spawnedItems)
            {
                if (item != null) DestroyImmediate(item);
            }
            _spawnedItems.Clear();

            if (_itemContainer == null || _questItemPrefab == null)
            {
                Debug.LogWarning("[QuestPanel] Item container or prefab is null");
                return;
            }

            foreach (var questData in _questData.items)
            {
                if (questData.category != _currentCategory) continue;

                GameObject itemObj = Instantiate(_questItemPrefab, _itemContainer);
                itemObj.SetActive(true);
                _spawnedItems.Add(itemObj);

                var questItemUI = itemObj.GetComponent<QuestItemUI>();
                if (questItemUI != null)
                    questItemUI.Setup(questData, this);
            }
        }

        public void OnClaimClicked(QuestItemData questData)
        {
            if (questData.status != QuestStatus.Claimable)
            {
                Debug.Log($"[QuestPanel] {questData.name} 不可领取");
                return;
            }

            var wallet = PlayerWallet.Instance;
            if (wallet == null)
            {
                Debug.LogWarning("[QuestPanel] PlayerWallet not found");
                return;
            }

            if (questData.rewards != null)
            {
                foreach (var reward in questData.rewards)
                {
                    switch (reward.rewardType)
                    {
                        case RewardType.Gold: wallet.AddGold(reward.amount); break;
                        case RewardType.Diamond: wallet.AddDiamond(reward.amount); break;
                        case RewardType.Stamina: wallet.AddStamina(reward.amount); break;
                        case RewardType.Exp: PlayerLevelSystem.Instance.AddExp(reward.amount); break;
                    }
                }
            }

            questData.status = QuestStatus.Claimed;
            _questData.dailyActivity += 10;
            Debug.Log($"[QuestPanel] 领取成功: {questData.name}");

            PersistQuestData();
            RefreshItemList();
            UpdateInfoBar();
        }

        public void OnGoToClicked(QuestItemData questData)
        {
            Debug.Log($"[QuestPanel] 前往: {questData.name}");
            CloseQuest();
        }

        public static string GetCategoryName(QuestCategory category)
        {
            return category switch
            {
                QuestCategory.Daily => "每日任务",
                QuestCategory.Main => "主线任务",
                QuestCategory.Achievement => "成就任务",
                QuestCategory.Event => "活动任务",
                _ => "未知"
            };
        }

        public static string GetRewardSymbol(RewardType type)
        {
            return type switch
            {
                RewardType.Gold => "🪙",
                RewardType.Diamond => "💎",
                RewardType.Stamina => "⚡",
                RewardType.Item => "📦",
                RewardType.Exp => "⭐",
                _ => ""
            };
        }
    }
}
