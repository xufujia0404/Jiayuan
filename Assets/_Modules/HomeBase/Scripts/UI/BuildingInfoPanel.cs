using UnityEngine;
using UnityEngine.UI;
using Sttop5.Shared.Player;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 建筑信息弹窗，显示已放置建筑的详情、升级和拆除功能。
    /// </summary>
    public class BuildingInfoPanel : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _levelText;
        [SerializeField] private Text _descText;
        [SerializeField] private Text _upgradeCostText;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Button _demolishButton;
        [SerializeField] private Button _closeButton;

        [Header("组件引用")]
        [SerializeField] private BuildingManager _buildingManager;

        private BuildingBase _currentBuilding;

        public bool IsShowing => _currentBuilding != null;

        private void Awake()
        {
            if (_upgradeButton != null)
                _upgradeButton.onClick.AddListener(OnUpgradeClicked);
            if (_demolishButton != null)
                _demolishButton.onClick.AddListener(OnDemolishClicked);
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            if (_upgradeButton != null)
                _upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
            if (_demolishButton != null)
                _demolishButton.onClick.RemoveListener(OnDemolishClicked);
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);
        }

        /// <summary>
        /// 显示指定建筑的信息弹窗。
        /// </summary>
        public void Show(BuildingBase building)
        {
            if (building == null || building.Data == null) return;

            _currentBuilding = building;
            RefreshDisplay();
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 关闭弹窗。
        /// </summary>
        public void Close()
        {
            _currentBuilding = null;
            gameObject.SetActive(false);
        }

        private void RefreshDisplay()
        {
            if (_currentBuilding == null) return;

            var data = _currentBuilding.Data;

            if (_nameText != null)
                _nameText.text = data.buildingName;

            if (_levelText != null)
                _levelText.text = $"Lv.{_currentBuilding.CurrentLevel}";

            if (_descText != null)
                _descText.text = data.description;

            // 升级按钮状态
            if (_upgradeButton != null)
            {
                bool canUpgrade = !_currentBuilding.IsMaxLevel;
                _upgradeButton.gameObject.SetActive(canUpgrade);

                if (canUpgrade)
                {
                    var nextLevel = data.GetLevelData(_currentBuilding.CurrentLevel + 1);
                    var profile = PlayerProfile.Instance;
                    bool canAfford = profile != null && profile.HasEnoughGold(nextLevel.upgradeCost);

                    if (_upgradeCostText != null)
                        _upgradeCostText.text = $"{nextLevel.upgradeCost}G";

                    _upgradeButton.interactable = canAfford;

                    var colors = _upgradeButton.colors;
                    colors.normalColor = canAfford ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
                    _upgradeButton.colors = colors;
                }
            }
        }

        private void OnUpgradeClicked()
        {
            if (_currentBuilding == null) return;

            if (_currentBuilding.Upgrade())
            {
                RefreshDisplay();
                Debug.Log($"[BuildingInfoPanel] 建筑升级成功: {_currentBuilding.Data.buildingName}");
            }
        }

        private void OnDemolishClicked()
        {
            if (_currentBuilding == null) return;

            int refund = _currentBuilding.GetSellValue();

            if (_buildingManager != null)
            {
                _buildingManager.RemoveBuilding(_currentBuilding);
            }
            else
            {
                _currentBuilding.Demolish();
            }

            _currentBuilding = null;
            gameObject.SetActive(false);

            Debug.Log($"[BuildingInfoPanel] 建筑已拆除，返还 {refund}G");
        }
    }
}
