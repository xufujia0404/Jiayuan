using UnityEngine;
using UnityEngine.UI;
using TowerDefense.Core;

namespace TowerDefense.UI
{
    public class TowerInfoPanel : MonoBehaviour
    {
        public static TowerInfoPanel Instance { get; private set; }

        [Header("References")]
        [SerializeField] private GameObject _panel;
        
        [Header("Tower Info")]
        [SerializeField] private Text _towerNameText;
        [SerializeField] private Text _towerLevelText;
        [SerializeField] private Text _damageText;
        [SerializeField] private Text _rangeText;
        [SerializeField] private Text _attackSpeedText;
        
        [Header("Buttons")]
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Text _upgradeCostText;
        [SerializeField] private Button _sellButton;
        [SerializeField] private Text _sellValueText;
        [SerializeField] private Button _closeButton;
        
        private TowerDefense.Tower.Tower _currentTower;
        
        private void Awake()
        {
            Instance = this;

            if (_panel != null)
            {
                _panel.SetActive(false);
            }
            
            if (_upgradeButton != null)
            {
                _upgradeButton.onClick.AddListener(OnUpgradeClicked);
            }
            
            if (_sellButton != null)
            {
                _sellButton.onClick.AddListener(OnSellClicked);
            }
            
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Hide);
            }
        }
        
        public void Show(TowerDefense.Tower.Tower tower)
        {
            _currentTower = tower;
            
            UpdateTowerInfo();
            
            if (_panel != null)
            {
                _panel.SetActive(true);
            }
            
            tower.OnUpgrade += OnTowerUpgraded;
            tower.OnSell += OnTowerSold;
        }
        
        public void Hide()
        {
            if (_currentTower != null)
            {
                _currentTower.OnUpgrade -= OnTowerUpgraded;
                _currentTower.OnSell -= OnTowerSold;
            }
            
            _currentTower = null;
            
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }
        
        private void UpdateTowerInfo()
        {
            if (_currentTower == null) return;
            
            var data = _currentTower.Data;
            var stats = _currentTower.CurrentStats;
            
            if (_towerNameText != null)
            {
                _towerNameText.text = data.towerName;
            }
            
            if (_towerLevelText != null)
            {
                _towerLevelText.text = $"等级 {_currentTower.CurrentLevel}/{data.levels.Length}";
            }
            
            if (_damageText != null)
            {
                _damageText.text = $"伤害: {stats.damage}";
            }
            
            if (_rangeText != null)
            {
                _rangeText.text = $"攻击范围: {stats.attackRange}";
            }
            
            if (_attackSpeedText != null)
            {
                _attackSpeedText.text = $"攻击速度: {stats.attackSpeed:0.0}";
            }
            
            UpdateUpgradeButton();
            UpdateSellButton();
        }
        
        private void UpdateUpgradeButton()
        {
            if (_upgradeButton == null || _upgradeCostText == null) return;
            
            if (_currentTower.IsMaxLevel)
            {
                _upgradeButton.interactable = false;
                _upgradeCostText.text = "已满级";
                _upgradeCostText.color = Color.grey;
            }
            else
            {
                int upgradeCost = _currentTower.GetUpgradeCost();
                bool canAfford = GameManager.Instance.HasEnoughGold(upgradeCost);
                
                _upgradeButton.interactable = canAfford;
                _upgradeCostText.text = $"升级（{upgradeCost}金币）";
                _upgradeCostText.color = canAfford ? Color.yellow : Color.red;
            }
        }
        
        private void UpdateSellButton()
        {
            if (_sellValueText == null) return;
            
            int sellValue = _currentTower.GetSellValue();
            _sellValueText.text = $"出售（{sellValue}金币）";
        }
        
        private void OnUpgradeClicked()
        {
            if (_currentTower != null)
            {
                _currentTower.Upgrade();
            }
        }
        
        private void OnSellClicked()
        {
            if (_currentTower != null)
            {
                _currentTower.Sell();
            }
        }
        
        private void OnTowerUpgraded(TowerDefense.Tower.Tower tower)
        {
            UpdateTowerInfo();
        }
        
        private void OnTowerSold(TowerDefense.Tower.Tower tower)
        {
            Hide();
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
            if (_panel != null && _panel.activeSelf && _currentTower != null)
            {
                UpdateUpgradeButton();
            }
        }
    }
}
