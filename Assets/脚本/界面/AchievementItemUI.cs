using UnityEngine;
using UnityEngine.UI;
using TowerDefense.Data;

namespace TowerDefense.UI
{
    public class AchievementItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image _iconBg;
        [SerializeField] private Text _iconSymbol;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _descText;
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private Text _progressText;
        [SerializeField] private Text _rewardText;
        [SerializeField] private Button _claimButton;
        [SerializeField] private Text _claimButtonText;
        [SerializeField] private GameObject _completedOverlay;

        private AchievementItemData _data;
        private AchievementPanel _panel;

        public void Setup(AchievementItemData data, AchievementPanel panel)
        {
            _data = data;
            _panel = panel;

            // 图标
            if (_iconBg != null && !string.IsNullOrEmpty(data.iconColorHex))
            {
                Color c;
                if (ColorUtility.TryParseHtmlString(data.iconColorHex, out c))
                    _iconBg.color = c;
            }

            if (_iconSymbol != null)
                _iconSymbol.text = data.iconSymbol ?? "";

            // 文字
            if (_nameText != null)
                _nameText.text = data.name;

            if (_descText != null)
                _descText.text = data.description;

            // 进度条
            if (_progressSlider != null)
            {
                _progressSlider.maxValue = Mathf.Max(data.targetCount, 1);
                _progressSlider.value = Mathf.Min(data.currentCount, data.targetCount);
            }

            if (_progressText != null)
                _progressText.text = $"{Mathf.Min(data.currentCount, data.targetCount)}/{data.targetCount}";

            // 奖励
            if (_rewardText != null)
                _rewardText.text = BuildRewardText(data);

            // 按钮状态
            bool isClaimable = data.status == AchievementStatus.Claimable;
            bool isClaimed = data.status == AchievementStatus.Claimed;

            if (_claimButton != null)
            {
                _claimButton.gameObject.SetActive(isClaimable);
                _claimButton.interactable = isClaimable;
                _claimButton.onClick.RemoveAllListeners();
                if (isClaimable)
                    _claimButton.onClick.AddListener(OnClaimClicked);
            }

            if (_claimButtonText != null)
            {
                if (isClaimable)
                    _claimButtonText.text = "领取";
            }

            // 已完成: 显示奖励+已领取标记
            if (_rewardText != null)
            {
                string rewardStr = BuildRewardText(data);
                if (isClaimed)
                    _rewardText.text = string.IsNullOrEmpty(rewardStr) ? "已领取" : $"{rewardStr} (已领取)";
                else
                    _rewardText.text = rewardStr;
            }

            // 已完成遮罩
            if (_completedOverlay != null)
                _completedOverlay.SetActive(isClaimed);
        }

        private string BuildRewardText(AchievementItemData data)
        {
            if (data.rewards == null || data.rewards.Length == 0) return "";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var r in data.rewards)
            {
                string sym = r.rewardType switch
                {
                    RewardType.Gold => "[金]",
                    RewardType.Diamond => "[钻]",
                    RewardType.Stamina => "[体]",
                    RewardType.Exp => "[经]",
                    _ => ""
                };
                parts.Add($"{sym}x{r.amount}");
            }
            return string.Join("  ", parts);
        }

        private void OnClaimClicked()
        {
            if (_panel != null && _data != null)
                _panel.OnClaimClicked(_data);
        }
    }
}
