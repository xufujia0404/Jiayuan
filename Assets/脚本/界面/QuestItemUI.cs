using UnityEngine;
using UnityEngine.UI;
using TowerDefense.Data;

namespace TowerDefense.UI
{
    public class QuestItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _descText;
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private Text _progressText;
        [SerializeField] private Image _rewardIcon;
        [SerializeField] private Text _rewardText;
        [SerializeField] private Text _expRewardText;
        [SerializeField] private Button _actionButton;
        [SerializeField] private Text _actionText;
        [SerializeField] private GameObject _tagObject;
        [SerializeField] private Text _tagText;

        private QuestItemData _itemData;
        private QuestPanel _questPanel;

        public void SetReferences(Image iconImage, Text nameText, Text descText, Slider progressSlider, Text progressText, Image rewardIcon, Text rewardText, Text expRewardText, Button actionButton, Text actionText, GameObject tagObject, Text tagText)
        {
            _iconImage = iconImage;
            _nameText = nameText;
            _descText = descText;
            _progressSlider = progressSlider;
            _progressText = progressText;
            _rewardIcon = rewardIcon;
            _rewardText = rewardText;
            _expRewardText = expRewardText;
            _actionButton = actionButton;
            _actionText = actionText;
            _tagObject = tagObject;
            _tagText = tagText;
        }

        public void Setup(QuestItemData itemData, QuestPanel questPanel)
        {
            _itemData = itemData;
            _questPanel = questPanel;

            // Name & Description
            if (_nameText != null)
                _nameText.text = itemData.name;

            if (_descText != null)
                _descText.text = itemData.description;

            // Progress
            if (_progressSlider != null)
            {
                _progressSlider.maxValue = itemData.targetCount;
                _progressSlider.value = itemData.currentCount;
            }

            if (_progressText != null)
                _progressText.text = $"{itemData.currentCount}/{itemData.targetCount}";

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

            // Reward
            int expAmount = 0;
            if (itemData.rewards != null && itemData.rewards.Length > 0)
            {
                var primaryReward = itemData.rewards[0];
                if (_rewardText != null)
                    _rewardText.text = primaryReward.rewardType == RewardType.Exp
                        ? $"⭐x{primaryReward.amount}"
                        : $"{QuestPanel.GetRewardSymbol(primaryReward.rewardType)}x{primaryReward.amount}";

                foreach (var r in itemData.rewards)
                {
                    if (r.rewardType == RewardType.Exp)
                        expAmount += r.amount;
                }
            }

            if (_expRewardText != null)
            {
                _expRewardText.gameObject.SetActive(expAmount > 0);
                if (expAmount > 0)
                    _expRewardText.text = $"⭐+{expAmount}";
            }

            // Tag
            if (_tagObject != null)
            {
                bool hasTag = !string.IsNullOrEmpty(itemData.tag);
                _tagObject.SetActive(hasTag);
                if (hasTag && _tagText != null)
                    _tagText.text = itemData.tag;
            }

            // Action Button
            if (_actionButton != null)
            {
                _actionButton.onClick.RemoveAllListeners();

                if (itemData.status == QuestStatus.Claimed)
                {
                    _actionButton.interactable = false;
                    if (_actionText != null)
                        _actionText.text = "已完成";
                }
                else if (itemData.status == QuestStatus.Claimable)
                {
                    _actionButton.interactable = true;
                    if (_actionText != null)
                        _actionText.text = "领取";

                    var colors = _actionButton.colors;
                    colors.normalColor = new Color(1f, 0.65f, 0.15f, 1f);
                    colors.highlightedColor = new Color(1f, 0.75f, 0.3f, 1f);
                    _actionButton.colors = colors;

                    _actionButton.onClick.AddListener(OnClaimClicked);
                }
                else
                {
                    _actionButton.interactable = true;
                    if (_actionText != null)
                        _actionText.text = "前往";

                    var colors = _actionButton.colors;
                    colors.normalColor = new Color(0.3f, 0.75f, 0.3f, 1f);
                    colors.highlightedColor = new Color(0.4f, 0.85f, 0.4f, 1f);
                    _actionButton.colors = colors;

                    _actionButton.onClick.AddListener(OnGoToClicked);
                }
            }
        }

        private void SetCategoryFallbackColor(QuestCategory category)
        {
            _iconImage.sprite = null;
            _iconImage.color = category switch
            {
                QuestCategory.Daily => new Color(1f, 0.85f, 0.2f, 1f),
                QuestCategory.Main => new Color(0.3f, 0.7f, 1f, 1f),
                QuestCategory.Achievement => new Color(0.8f, 0.5f, 1f, 1f),
                QuestCategory.Event => new Color(1f, 0.4f, 0.4f, 1f),
                _ => Color.white
            };
        }

        private void OnClaimClicked()
        {
            if (_questPanel != null && _itemData != null)
                _questPanel.OnClaimClicked(_itemData);
        }

        private void OnGoToClicked()
        {
            if (_questPanel != null && _itemData != null)
                _questPanel.OnGoToClicked(_itemData);
        }
    }
}
