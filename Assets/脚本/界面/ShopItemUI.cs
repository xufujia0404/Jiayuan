using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TowerDefense.Data;
using TowerDefense.Core;

namespace TowerDefense.UI
{
    public class ShopItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _cardBackground;  // 卡片背景
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _descText;
        [SerializeField] private Text _priceText;
        [SerializeField] private Text _originalPriceText;  // 原价（划线价）
        [SerializeField] private Text _tagText;
        [SerializeField] private Text _discountText;
        [SerializeField] private Button _buyButton;
        [SerializeField] private GameObject _tagObject;
        [SerializeField] private GameObject _discountObject;
        [SerializeField] private GameObject _soldOutObject;
        [SerializeField] private GameObject _originalPriceObject;  // 原价区域
        [SerializeField] private CanvasGroup _canvasGroup;  // 用于动画

        // 卡片颜色方案 - 按分类
        private static readonly Color ResourceCardColor = new Color(0.25f, 0.22f, 0.15f, 0.95f);
        private static readonly Color HeroCardColor = new Color(0.15f, 0.18f, 0.28f, 0.95f);
        private static readonly Color TowerCardColor = new Color(0.15f, 0.25f, 0.18f, 0.95f);
        private static readonly Color SpecialCardColor = new Color(0.28f, 0.15f, 0.22f, 0.95f);

        // 标签颜色
        private static readonly Color TagNewColor = new Color(0.2f, 0.8f, 1f, 1f);
        private static readonly Color TagHotColor = new Color(1f, 0.4f, 0.2f, 1f);
        private static readonly Color TagDailyColor = new Color(0.3f, 0.9f, 0.4f, 1f);
        private static readonly Color TagLimitColor = new Color(1f, 0.75f, 0.1f, 1f);

        private ShopItemData _itemData;
        private ShopPanel _shopPanel;

        public void Setup(ShopItemData itemData, ShopPanel shopPanel)
        {
            _itemData = itemData;
            _shopPanel = shopPanel;

            // 卡片背景色
            if (_cardBackground != null)
            {
                _cardBackground.color = itemData.category switch
                {
                    ShopCategory.Resource => ResourceCardColor,
                    ShopCategory.Hero => HeroCardColor,
                    ShopCategory.Tower => TowerCardColor,
                    ShopCategory.Special => SpecialCardColor,
                    _ => new Color(0.2f, 0.2f, 0.2f, 0.9f)
                };
            }

            if (_nameText != null)
                _nameText.text = itemData.name;

            if (_descText != null)
            {
                string rewardText = BuildRewardText(itemData);
                _descText.text = string.IsNullOrEmpty(rewardText) ? itemData.description : rewardText;
            }

            // Price - 折扣时显示划线原价
            if (_priceText != null)
            {
                if (itemData.currencyType == CurrencyType.Free)
                {
                    _priceText.text = "免费";
                    _priceText.color = Color.green;
                }
                else if (itemData.discount > 0)
                {
                    int discountedPrice = itemData.price * (100 - itemData.discount) / 100;
                    string symbol = ShopPanel.GetCurrencySymbol(itemData.currencyType);
                    _priceText.text = $"{symbol}{discountedPrice}";
                    _priceText.color = new Color(1f, 0.85f, 0.2f); // 金色高亮折扣价

                    // 显示原价
                    if (_originalPriceText != null)
                        _originalPriceText.text = $"{itemData.price}";
                    if (_originalPriceObject != null)
                        _originalPriceObject.SetActive(true);
                }
                else
                {
                    string symbol = ShopPanel.GetCurrencySymbol(itemData.currencyType);
                    _priceText.text = $"{symbol}{itemData.price}";
                    _priceText.color = Color.white;
                }
            }

            // 没有折扣时隐藏原价
            if (itemData.discount <= 0 && _originalPriceObject != null)
                _originalPriceObject.SetActive(false);

            // Tag - 带颜色
            if (_tagObject != null)
            {
                bool hasTag = !string.IsNullOrEmpty(itemData.tag);
                _tagObject.SetActive(hasTag);
                if (hasTag && _tagText != null)
                {
                    _tagText.text = itemData.tag;
                    // 标签颜色
                    var tagImage = _tagObject.GetComponent<Image>();
                    if (tagImage != null)
                    {
                        tagImage.color = itemData.tag.ToLower() switch
                        {
                            "new" => TagNewColor,
                            "hot" => TagHotColor,
                            "daily" => TagDailyColor,
                            "限时" => TagLimitColor,
                            _ => TagNewColor
                        };
                    }
                }
            }

            // Discount badge
            if (_discountObject != null)
            {
                bool hasDiscount = itemData.discount > 0;
                _discountObject.SetActive(hasDiscount);
                if (hasDiscount && _discountText != null)
                    _discountText.text = $"-{itemData.discount}%";
            }

            // Icon
            if (_iconImage != null)
            {
                if (!string.IsNullOrEmpty(itemData.iconPath))
                {
                    var sprite = Resources.Load<Sprite>(itemData.iconPath);
                    if (sprite != null)
                    {
                        _iconImage.sprite = sprite;
                        _iconImage.color = Color.white;
                    }
                    else
                    {
                        SetCategoryFallbackColor(itemData.category);
                    }
                }
                else
                {
                    SetCategoryFallbackColor(itemData.category);
                }
            }

            // Sold out check - 更醒目
            bool isSoldOut = itemData.isLimited && itemData.purchasedCount >= itemData.limitCount;
            if (_soldOutObject != null)
                _soldOutObject.SetActive(isSoldOut);

            if (_buyButton != null)
            {
                _buyButton.interactable = !isSoldOut;

                // 售罄时按钮变灰
                var buyImage = _buyButton.GetComponent<Image>();
                if (buyImage != null)
                {
                    buyImage.color = isSoldOut
                        ? new Color(0.3f, 0.3f, 0.3f, 1f)
                        : new Color(0.2f, 0.75f, 0.3f, 1f);  // 绿色可购买
                }

                _buyButton.onClick.RemoveAllListeners();
                _buyButton.onClick.AddListener(OnBuyClicked);
            }

            // 限购信息 - 在描述后追加
            if (_descText != null && itemData.isLimited && itemData.limitCount > 0)
            {
                int remaining = itemData.limitCount - itemData.purchasedCount;
                _descText.text += $"\n<color=#FFD700>限购{itemData.limitCount}次 (剩余{remaining}次)</color>";
            }
        }

        private void SetCategoryFallbackColor(ShopCategory category)
        {
            _iconImage.sprite = null;
            _iconImage.color = category switch
            {
                ShopCategory.Resource => new Color(1f, 0.85f, 0.2f, 1f),
                ShopCategory.Hero => new Color(0.3f, 0.7f, 1f, 1f),
                ShopCategory.Tower => new Color(0.4f, 1f, 0.4f, 1f),
                ShopCategory.Special => new Color(1f, 0.4f, 0.8f, 1f),
                _ => Color.white
            };
        }

        public static string BuildRewardText(ShopItemData data)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (data.rewardGold > 0) parts.Add($"🪙×{data.rewardGold}");
            if (data.rewardDiamond > 0) parts.Add($"💎×{data.rewardDiamond}");
            if (data.rewardStamina > 0) parts.Add($"⚡×{data.rewardStamina}");
            return string.Join("  ", parts);
        }

        private void OnBuyClicked()
        {
            if (_shopPanel != null && _itemData != null)
            {
                _shopPanel.OnBuyClicked(_itemData);
            }
        }
    }
}
