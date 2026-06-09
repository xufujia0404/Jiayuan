using UnityEngine;
using UnityEngine.UI;
using TowerDefense.Data;
using TowerDefense.Tower;
using TowerDefense.Core;

namespace TowerDefense.UI
{
    public class TowerSelectPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform _towerButtonContainer;
        [SerializeField] private GameObject _towerButtonPrefab;
        [SerializeField] private Text _slotInfoText;
        
        [Header("Text Settings")]
        [SerializeField] private Font _buttonFont;
        
        
        [Header("Tower Data")]
        [SerializeField] private TowerData[] _availableTowers;
        
        private TowerSlot _currentSlot;
        private TowerData[] _towerDatas;
        
        private void Awake()
        {
            Debug.Log($"TowerSelectPanel Awake - _panel: {_panel != null}, _container: {_towerButtonContainer != null}, _prefab: {_towerButtonPrefab != null}, _text: {_slotInfoText != null}");
            
            if (_panel != null)
            {
                _panel.SetActive(true);
            }
            
            LoadTowerData();
            RefreshTowerButtons();
        }
        
        private void LoadTowerData()
        {
            if (_availableTowers != null && _availableTowers.Length > 0)
            {
                _towerDatas = _availableTowers;
            }
            else
            {
                _towerDatas = Resources.LoadAll<TowerData>("Data/Towers");
                
                if (_towerDatas.Length == 0)
                {
                    _towerDatas = Resources.LoadAll<TowerData>("Data");
                }
            }
        }
        
        public void Show(TowerSlot slot)
        {
            _currentSlot = slot;
            
            if (_panel == null)
            {
                Debug.LogWarning("⚠️ TowerSelectPanel 的 _panel 字段未设置！");
                return;
            }
            
            RefreshTowerButtons();
            _panel.SetActive(true);
        }
        
        public void Hide()
        {
            _currentSlot = null;
            
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }
        
        private void RefreshTowerButtons()
        {
            foreach (Transform child in _towerButtonContainer)
            {
                Destroy(child.gameObject);
            }
            
            foreach (var towerData in _towerDatas)
            {
                CreateTowerButton(towerData);
            }
        }
        
        private void CreateTowerButton(TowerData towerData)
        {
            GameObject buttonObj;
            
            if (_towerButtonPrefab == null)
            {
                buttonObj = new GameObject($"TowerButton_{towerData.towerName}");
                buttonObj.transform.SetParent(_towerButtonContainer, false);
                
                Button button = buttonObj.AddComponent<Button>();
                Image bgImage = buttonObj.AddComponent<Image>();
                bgImage.color = new Color(0.4f, 0.4f, 0.6f);
                
                RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
                buttonRect.sizeDelta = new Vector2(70, 85);
                
                if (towerData.icon != null)
                {
                    GameObject iconObj = new GameObject("Icon");
                    iconObj.transform.SetParent(buttonObj.transform, false);
                    
                    Image iconImage = iconObj.AddComponent<Image>();
                    iconImage.sprite = towerData.icon;
                    iconImage.preserveAspect = true;
                    
                    RectTransform iconRect = iconObj.GetComponent<RectTransform>();
                    iconRect.anchorMin = new Vector2(0.5f, 0.4f);
                    iconRect.anchorMax = new Vector2(0.5f, 1f);
                    iconRect.offsetMin = new Vector2(-30, 0);
                    iconRect.offsetMax = new Vector2(30, 0);
                }
                
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(buttonObj.transform, false);
                
                Outline outline = textObj.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(2, -2);
                
                Text text = textObj.AddComponent<Text>();
                text.text = $"{GetTowerPrice(towerData)}";
                
                if (_buttonFont != null)
                {
                    text.font = _buttonFont;
                }
                
                text.fontSize = 26;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.LowerCenter;
                text.color = new Color(1f, 0.9f, 0f);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                
                RectTransform textRect = textObj.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = new Vector2(1f, 0.35f);
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                
                bool canAfford = GameManager.Instance.HasEnoughGold(GetTowerPrice(towerData));
            button.interactable = canAfford;
            Debug.Log($"🔘 [动态按钮] 添加点击监听器: {towerData.towerName}");
            button.onClick.AddListener(() => {
                Debug.Log($"🖱️ [动态按钮] Lambda被调用！");
                OnTowerSelected(towerData);
            });
            Debug.Log($"🔘 [动态按钮] 点击监听器已添加！");
                
            if (!canAfford)
                {
                    bgImage.color = new Color(0.3f, 0.3f, 0.3f);
                }
            }
            else
            {
                buttonObj = Instantiate(_towerButtonPrefab, _towerButtonContainer);
                buttonObj.name = $"TowerButton_{towerData.towerName}";
                
                TowerButton towerButton = buttonObj.GetComponent<TowerButton>();
                if (towerButton != null)
                {
                    towerButton.Initialize(towerData, this);
                    Debug.Log($"📌 TowerButton 组件已初始化: {towerData.towerName}");
                }
                else
                {
                    Debug.LogWarning($"⚠️ TowerButton 预制体没有 TowerButton 组件，使用手动设置");
                    SetupButtonManually(buttonObj, towerData);
                }
            }
        }
        
        private int GetTowerPrice(TowerData towerData)
        {
            if (towerData.levels != null && towerData.levels.Length > 0)
            {
                return towerData.levels[0].cost;
            }
            return 0;
        }
        
        private void SetupButtonManually(GameObject buttonObj, TowerData towerData)
        {
            Text nameText = buttonObj.GetComponentInChildren<Text>();
            if (nameText != null)
            {
                nameText.text = towerData.towerName;
            }
            
            Image[] images = buttonObj.GetComponentsInChildren<Image>();
            foreach (var img in images)
            {
                if (img.gameObject != buttonObj && towerData.icon != null && img.sprite == null)
                {
                    img.sprite = towerData.icon;
                    img.preserveAspect = true;
                    break;
                }
            }
            
            Button button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => OnTowerSelected(towerData));
                
                bool canAfford = GameManager.Instance.HasEnoughGold(towerData.levels[0].cost);
                button.interactable = canAfford;
            }
        }
        
        public void OnTowerSelected(TowerData towerData)
        {
            Debug.Log($"📞 OnTowerSelected 被调用！塔: {towerData?.towerName}, _currentSlot: {_currentSlot != null}");
            
            if (_currentSlot == null || towerData == null)
            {
                Debug.LogError("❌ _currentSlot 或 towerData 是 null！");
                return;
            }
            
            Debug.Log($"📦 准备放置塔: {towerData.towerName}, 消耗金币: {towerData.levels[0].cost}");
            
            bool success = _currentSlot.PlaceTower(towerData);
            Debug.Log($"📦 PlaceTower 返回: {success}");
            
            if (success)
            {
                Debug.Log("✅ 塔放置成功！");
                // 面板常驻，不隐藏；刷新按钮状态
                _currentSlot = null;
                UpdateButtonStates();
            }
            else
            {
                Debug.LogWarning("⚠️ 塔放置失败！");
            }
        }
        
        public void UpdateButtonStates()
        {
            foreach (Transform child in _towerButtonContainer)
            {
                TowerButton button = child.GetComponent<TowerButton>();
                if (button != null)
                {
                    button.UpdateState();
                }
            }
        }
        
        private void OnEnable()
        {
            EventBus.Subscribe<GoldChangedEvent>(OnGoldChanged);
        }
        
        private void OnDisable()
        {
            EventBus.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
        }
        
        private void OnGoldChanged(GoldChangedEvent e)
        {
            UpdateButtonStates();
        }
    }
    
    public class TowerButton : MonoBehaviour
    {
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _costText;
        [SerializeField] private Text _damageText;
        [SerializeField] private Text _rangeText;
        [SerializeField] private Image _icon;
        
        private TowerData _towerData;
        private TowerSelectPanel _panel;
        
        public void Initialize(TowerData towerData, TowerSelectPanel panel)
        {
            _towerData = towerData;
            _panel = panel;
            
            if (_nameText != null)
            {
                _nameText.text = towerData.towerName;
            }
            
            TowerData.TowerStats stats = towerData.levels[0];
            if (_costText != null)
            {
                _costText.text = $"{stats.cost} G";
            }
            
            if (_damageText != null)
            {
                _damageText.text = $"伤害: {stats.damage}";
            }
            
            if (_rangeText != null)
            {
                _rangeText.text = $"范围: {stats.attackRange}";
            }
            
            if (towerData.icon != null)
            {
                if (_icon != null)
                {
                    _icon.sprite = towerData.icon;
                    _icon.gameObject.SetActive(true);
                }
                
                Image[] allImages = GetComponentsInChildren<Image>();
                foreach (var img in allImages)
                {
                    if (img.gameObject != gameObject && img.sprite == null)
                    {
                        img.sprite = towerData.icon;
                        img.preserveAspect = true;
                        break;
                    }
                }
            }
            else
            {
                if (_icon != null)
                {
                    _icon.gameObject.SetActive(false);
                }
            }
            
            UpdateState();
            
            Button button = GetComponent<Button>();
            if (button != null)
            {
                Debug.Log($"🔘 找到Button组件，准备添加点击监听器...");
                button.onClick.AddListener(OnClicked);
                Debug.Log($"🔘 点击监听器已添加！当前监听器数量: {button.onClick.GetPersistentEventCount()}");
            }
            else
            {
                Debug.LogError("❌ 找不到Button组件！");
            }
        }
        
        public void UpdateState()
        {
            Button button = GetComponent<Button>();
            if (button != null)
            {
                bool canAfford = GameManager.Instance.HasEnoughGold(_towerData.levels[0].cost);
                button.interactable = canAfford;
                
                Image image = GetComponent<Image>();
                if (image != null)
                {
                    image.color = canAfford ? Color.white : Color.grey;
                }
            }
        }
        
        private void OnClicked()
        {
            Debug.Log($"🖱️ TowerButton.OnClicked 被调用！塔: {_towerData?.towerName}, panel: {_panel != null}");
            
            if (_panel != null)
            {
                Debug.Log($"📞 调用 _panel.OnTowerSelected...");
                _panel.OnTowerSelected(_towerData);
            }
            else
            {
                Debug.LogError("❌ _panel 是 null！");
            }
        }
    }
}
