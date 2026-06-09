using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TowerDefense.Data;
using TowerDefense.Core;

namespace TowerDefense.UI
{
    public class ShopPanel : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _shopPanel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Image _dimOverlay;  // 半透明遮罩

        [Header("Currency Display")]
        [SerializeField] private Text _goldAmountText;
        [SerializeField] private Text _diamondAmountText;

        [Header("Tabs")]
        [SerializeField] private Button _tabResource;
        [SerializeField] private Button _tabHero;
        [SerializeField] private Button _tabTower;
        [SerializeField] private Button _tabSpecial;
        [SerializeField] private GameObject _tabIndicator;  // 滑动指示器
        [SerializeField] private Color _tabActiveColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color _tabInactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        [Header("Content")]
        [SerializeField] private Transform _itemContainer;
        [SerializeField] private GameObject _shopItemPrefab;
        [Header("Confirm Dialog")]
        [SerializeField] private GameObject _confirmDialog;
        [SerializeField] private Text _confirmTitleText;
        [SerializeField] private Text _confirmDescText;
        [SerializeField] private Text _confirmPriceText;
        [SerializeField] private Button _confirmBuyButton;
        [SerializeField] private Button _confirmCancelButton;

        [Header("Feedback")]
        [SerializeField] private GameObject _feedbackPanel;
        [SerializeField] private Text _feedbackText;
        [SerializeField] private float _feedbackDuration = 1.5f;

        [Header("Data")]
        [SerializeField] private string _jsonPath = "Data/ShopItems";

        private ShopData _shopData;
        private ShopCategory _currentCategory;
        private ShopItemData _pendingBuyItem;
        private List<GameObject> _spawnedItems = new List<GameObject>();
        private Dictionary<ShopCategory, Button> _tabButtons = new Dictionary<ShopCategory, Button>();
        private Dictionary<ShopCategory, Vector3> _tabPositions = new Dictionary<ShopCategory, Vector3>();
        private GameObject _torchRoot;
        private MovingClouds _clouds;
        private ParticleSystem _snowParticles;
        private int _snowOriginalSortOrder;
        private Coroutine _feedbackCoroutine;

        private void Awake()
        {
            _tabButtons[ShopCategory.Resource] = _tabResource;
            _tabButtons[ShopCategory.Hero] = _tabHero;
            _tabButtons[ShopCategory.Tower] = _tabTower;
            _tabButtons[ShopCategory.Special] = _tabSpecial;

            if (_closeButton != null)
                _closeButton.onClick.AddListener(CloseShop);

            foreach (var kvp in _tabButtons)
            {
                if (kvp.Value != null)
                {
                    var cat = kvp.Key;
                    kvp.Value.onClick.AddListener(() => SwitchTab(cat));
                    // 记录每个Tab的位置用于指示器滑动
                    _tabPositions[cat] = kvp.Value.transform.localPosition;
                }
            }

            // 确认弹窗按钮
            if (_confirmBuyButton != null)
                _confirmBuyButton.onClick.AddListener(OnConfirmBuy);
            if (_confirmCancelButton != null)
                _confirmCancelButton.onClick.AddListener(OnCancelBuy);

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

            LoadShopData();
        }

        private void Start()
        {
            if (_shopPanel != null)
                _shopPanel.SetActive(false);
            if (_confirmDialog != null)
                _confirmDialog.SetActive(false);
            if (_feedbackPanel != null)
                _feedbackPanel.SetActive(false);
        }

        private void LoadShopData()
        {
            var jsonFile = Resources.Load<TextAsset>(_jsonPath);
            if (jsonFile != null)
            {
                _shopData = JsonUtility.FromJson<ShopData>(jsonFile.text);
                Debug.Log($"[ShopPanel] Loaded {_shopData.items.Length} items from JSON");
            }
            else
            {
                Debug.LogError($"[ShopPanel] Failed to load JSON: Resources/{_jsonPath}");
                _shopData = new ShopData { items = new ShopItemData[0] };
            }
        }

        public void OpenShop()
        {
            if (_shopPanel != null)
                _shopPanel.SetActive(true);

            // 半透明遮罩
            if (_dimOverlay != null)
            {
                _dimOverlay.gameObject.SetActive(true);
                _dimOverlay.color = new Color(0, 0, 0, 0.6f);
            }

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

            LoadShopData();
            UpdateCurrencyDisplay();
            SwitchTab(ShopCategory.Resource);
        }

        public void CloseShop()
        {
            if (_shopPanel != null)
                _shopPanel.SetActive(false);

            if (_dimOverlay != null)
                _dimOverlay.gameObject.SetActive(false);

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

        private void UpdateCurrencyDisplay()
        {
            var wallet = PlayerWallet.Instance;
            if (wallet != null)
            {
                if (_goldAmountText != null)
                    _goldAmountText.text = wallet.Gold.ToString();
                if (_diamondAmountText != null)
                    _diamondAmountText.text = wallet.Diamond.ToString();
            }
        }

        private void SwitchTab(ShopCategory category)
        {
            _currentCategory = category;
            UpdateTabVisuals();
            MoveTabIndicator(category);
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
                colors.highlightedColor = kvp.Key == _currentCategory ? _tabActiveColor : new Color(0.8f, 0.8f, 0.8f, 1f);
                kvp.Value.colors = colors;
            }
        }

        private void MoveTabIndicator(ShopCategory category)
        {
            if (_tabIndicator == null || !_tabPositions.ContainsKey(category)) return;
            StartCoroutine(AnimateTabIndicator(_tabPositions[category]));
        }

        private IEnumerator AnimateTabIndicator(Vector3 targetPos)
        {
            Vector3 startPos = _tabIndicator.transform.localPosition;
            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // ease-out
                t = 1f - (1f - t) * (1f - t);
                _tabIndicator.transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            _tabIndicator.transform.localPosition = targetPos;
        }

        private void RefreshItemList()
        {
            foreach (var item in _spawnedItems)
            {
                if (item != null) Destroy(item);
            }
            _spawnedItems.Clear();

            if (_itemContainer == null || _shopItemPrefab == null)
            {
                Debug.LogWarning("[ShopPanel] Item container or prefab is null");
                return;
            }

            foreach (var itemData in _shopData.items)
            {
                if (itemData.category != _currentCategory) continue;

                GameObject itemObj = Instantiate(_shopItemPrefab, _itemContainer);
                _spawnedItems.Add(itemObj);

                var shopItemUI = itemObj.GetComponent<ShopItemUI>();
                if (shopItemUI != null)
                {
                    shopItemUI.Setup(itemData, this);
                }
            }
        }

        /// <summary>
        /// 点击购买时，先弹出确认框
        /// </summary>
        public void OnBuyClicked(ShopItemData itemData)
        {
            if (itemData.isLimited && itemData.purchasedCount >= itemData.limitCount)
            {
                ShowFeedback("已达购买上限!");
                return;
            }

            _pendingBuyItem = itemData;
            ShowConfirmDialog(itemData);
        }

        private void ShowConfirmDialog(ShopItemData itemData)
        {
            if (_confirmDialog == null)
            {
                // 没有确认弹窗则直接购买
                ExecuteBuy(itemData);
                return;
            }

            if (_confirmTitleText != null)
                _confirmTitleText.text = $"购买 {itemData.name}?";

            if (_confirmDescText != null)
            {
                string rewardText = ShopItemUI.BuildRewardText(itemData);
                _confirmDescText.text = string.IsNullOrEmpty(rewardText) ? itemData.description : $"获得: {rewardText}";
            }

            if (_confirmPriceText != null)
            {
                int price = itemData.discount > 0
                    ? itemData.price * (100 - itemData.discount) / 100
                    : itemData.price;

                if (itemData.currencyType == CurrencyType.Free)
                {
                    _confirmPriceText.text = "免费领取";
                    _confirmPriceText.color = Color.green;
                }
                else
                {
                    string symbol = GetCurrencySymbol(itemData.currencyType);
                    _confirmPriceText.text = $"花费: {symbol} {price}";
                    _confirmPriceText.color = Color.white;
                }
            }

            _confirmDialog.SetActive(true);
        }

        private void OnConfirmBuy()
        {
            if (_confirmDialog != null)
                _confirmDialog.SetActive(false);

            if (_pendingBuyItem != null)
            {
                ExecuteBuy(_pendingBuyItem);
                _pendingBuyItem = null;
            }
        }

        private void OnCancelBuy()
        {
            if (_confirmDialog != null)
                _confirmDialog.SetActive(false);
            _pendingBuyItem = null;
        }

        private void ExecuteBuy(ShopItemData itemData)
        {
            int price = itemData.discount > 0
                ? itemData.price * (100 - itemData.discount) / 100
                : itemData.price;

            var wallet = PlayerWallet.Instance;
            if (wallet == null)
            {
                Debug.LogWarning("[ShopPanel] PlayerWallet not found");
                return;
            }

            bool success = itemData.currencyType switch
            {
                CurrencyType.Gold => wallet.SpendGold(price),
                CurrencyType.Diamond => wallet.SpendDiamond(price),
                CurrencyType.Free => true,
                _ => false
            };

            if (!success)
            {
                string curName = itemData.currencyType switch
                {
                    CurrencyType.Gold => "金币不足",
                    CurrencyType.Diamond => "钻石不足",
                    _ => "无法购买"
                };
                ShowFeedback(curName);
                return;
            }

            Debug.Log($"[ShopPanel] 购买成功: {itemData.name}, 花费: {price}");

            // 发放奖励
            if (itemData.rewardGold > 0)
                wallet.AddGold(itemData.rewardGold);
            if (itemData.rewardDiamond > 0)
                wallet.AddDiamond(itemData.rewardDiamond);
            if (itemData.rewardStamina > 0)
                wallet.AddStamina(itemData.rewardStamina);

            itemData.purchasedCount++;

            ShowFeedback($"购买成功! {itemData.name}");
            UpdateCurrencyDisplay();
            RefreshItemList();
        }

        private void ShowFeedback(string message)
        {
            if (_feedbackPanel == null || _feedbackText == null) return;

            if (_feedbackCoroutine != null)
                StopCoroutine(_feedbackCoroutine);

            _feedbackCoroutine = StartCoroutine(ShowFeedbackCoroutine(message));
        }

        private IEnumerator ShowFeedbackCoroutine(string message)
        {
            _feedbackPanel.SetActive(true);
            _feedbackText.text = message;

            // 淡入
            var canvasGroup = _feedbackPanel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                float fadeIn = 0.2f;
                float t = 0f;
                while (t < fadeIn)
                {
                    t += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeIn);
                    yield return null;
                }
                canvasGroup.alpha = 1f;

                // 停留
                yield return new WaitForSeconds(_feedbackDuration);

                // 淡出
                float fadeOut = 0.3f;
                t = 0f;
                while (t < fadeOut)
                {
                    t += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeOut);
                    yield return null;
                }
                canvasGroup.alpha = 0f;
            }
            else
            {
                yield return new WaitForSeconds(_feedbackDuration);
            }

            _feedbackPanel.SetActive(false);
            _feedbackCoroutine = null;
        }

        public static string GetCategoryName(ShopCategory category)
        {
            return category switch
            {
                ShopCategory.Resource => "资源",
                ShopCategory.Hero => "英雄",
                ShopCategory.Tower => "防御塔",
                ShopCategory.Special => "特惠",
                _ => "未知"
            };
        }

        public static string GetCurrencySymbol(CurrencyType type)
        {
            return type switch
            {
                CurrencyType.Gold => "🪙",
                CurrencyType.Diamond => "💎",
                CurrencyType.Free => "🎁",
                _ => ""
            };
        }
    }
}
