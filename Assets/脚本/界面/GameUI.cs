using UnityEngine;
using UnityEngine.UI;
using TowerDefense.Core;

namespace TowerDefense.UI
{
    public class GameUI : MonoBehaviour
    {
        [Header("Top Bar")]
        [SerializeField] private Image _goldIcon;
        [SerializeField] private Text _goldText;
        [SerializeField] private Image _lifeIcon;
        [SerializeField] private Text _lifeText;
        [SerializeField] private Image _waveIcon;
        [SerializeField] private Text _waveText;

        [Header("Wave Info")]
        [SerializeField] private GameObject _waveInfoPanel;
        [SerializeField] private Text _waveTimerText;
        [SerializeField] private Text _enemyCountText;
        [SerializeField] private Button _startWaveButton;

        [Header("Tower Selection")]
        [SerializeField] private GameObject _towerSelectPanel;
        [SerializeField] private Transform _towerButtonContainer;
        [SerializeField] private GameObject _towerButtonPrefab;

        [Header("Tower Info")]
        [SerializeField] private GameObject _towerInfoPanel;
        [SerializeField] private Text _towerNameText;
        [SerializeField] private Text _towerLevelText;
        [SerializeField] private Text _towerDamageText;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Text _upgradeCostText;
        [SerializeField] private Button _sellButton;
        [SerializeField] private Text _sellValueText;

        [Header("Game Controls")]
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _speedButton;
        [SerializeField] private GameObject _pauseMenu;
        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private GameObject _victoryPanel;

        [Header("Settings")]
        [SerializeField] private Slider _volumeSlider;
        [SerializeField] private Toggle _soundToggle;

        private Tower.TowerSlot _selectedSlot;
        private Tower.Tower _selectedTower;

        private void Start()
        {
            InitializeUI();
            SubscribeToEvents();
        }

        private void InitializeUI()
        {
            if (_pauseMenu != null) _pauseMenu.SetActive(false);
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
            if (_victoryPanel != null) _victoryPanel.SetActive(false);
            if (_towerInfoPanel != null) _towerInfoPanel.SetActive(false);
            if (_towerSelectPanel != null) _towerSelectPanel.SetActive(false);

            UpdateGoldDisplay(GameManager.Instance.CurrentGold);
            UpdateLifeDisplay(GameManager.Instance.CurrentLife);
            UpdateWaveDisplay(GameManager.Instance.CurrentWave);

            if (_startWaveButton != null)
            {
                _startWaveButton.onClick.AddListener(OnStartWaveClicked);
            }
            if (_pauseButton != null)
            {
                _pauseButton.onClick.AddListener(OnPauseClicked);
            }
            if (_speedButton != null)
            {
                _speedButton.onClick.AddListener(OnSpeedClicked);
            }
            if (_upgradeButton != null)
            {
                _upgradeButton.onClick.AddListener(OnUpgradeClicked);
            }
            if (_sellButton != null)
            {
                _sellButton.onClick.AddListener(OnSellClicked);
            }
        }

        private void SubscribeToEvents()
        {
            GameManager.Instance.OnGoldChanged += UpdateGoldDisplay;
            GameManager.Instance.OnLifeChanged += UpdateLifeDisplay;
            GameManager.Instance.OnWaveChanged += UpdateWaveDisplay;
            GameManager.Instance.OnStateChanged += OnGameStateChanged;

            EventBus.Subscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Subscribe<LifeChangedEvent>(OnLifeChanged);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGoldChanged -= UpdateGoldDisplay;
                GameManager.Instance.OnLifeChanged -= UpdateLifeDisplay;
                GameManager.Instance.OnWaveChanged -= UpdateWaveDisplay;
                GameManager.Instance.OnStateChanged -= OnGameStateChanged;
            }

            EventBus.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Unsubscribe<LifeChangedEvent>(OnLifeChanged);
        }

        #region Display Updates

        private void UpdateGoldDisplay(int gold)
        {
            if (_goldText != null)
            {
                _goldText.text = gold.ToString();
            }
            
            if (_goldIcon != null)
            {
                _goldIcon.enabled = true;
            }
        }

        private void UpdateLifeDisplay(int life)
        {
            if (_lifeText != null)
            {
                _lifeText.text = life.ToString();
            }
            
            if (_lifeIcon != null)
            {
                _lifeIcon.enabled = true;
            }
        }

        private void UpdateWaveDisplay(int wave)
        {
            if (_waveText != null)
            {
                _waveText.text = $"{wave}/{GameManager.Instance.TotalWaves}";
            }
            
            if (_waveIcon != null)
            {
                _waveIcon.enabled = true;
            }
        }

        private void OnGoldChanged(GoldChangedEvent e)
        {
            UpdateGoldDisplay(e.CurrentGold);
        }

        private void OnLifeChanged(LifeChangedEvent e)
        {
            UpdateLifeDisplay(e.CurrentLife);
        }

        #endregion

        #region Tower Selection

        public void ShowTowerSelectPanel(Tower.TowerSlot slot)
        {
            _selectedSlot = slot;
            if (_towerSelectPanel != null)
            {
                _towerSelectPanel.SetActive(true);
            }
        }

        public void HideTowerSelectPanel()
        {
            _selectedSlot = null;
            if (_towerSelectPanel != null)
            {
                _towerSelectPanel.SetActive(false);
            }
        }

        public void SelectTower(Data.TowerData towerData)
        {
            if (_selectedSlot != null)
            {
                _selectedSlot.PlaceTower(towerData);
                HideTowerSelectPanel();
            }
        }

        #endregion

        #region Tower Info

        public void ShowTowerInfo(Tower.Tower tower)
        {
            _selectedTower = tower;

            if (_towerInfoPanel != null)
            {
                _towerInfoPanel.SetActive(true);
            }

            UpdateTowerInfo();
        }

        public void HideTowerInfo()
        {
            _selectedTower = null;
            if (_towerInfoPanel != null)
            {
                _towerInfoPanel.SetActive(false);
            }
        }

        private void UpdateTowerInfo()
        {
            if (_selectedTower == null) return;

            if (_towerNameText != null)
            {
                _towerNameText.text = _selectedTower.Data.towerName;
            }
            if (_towerLevelText != null)
            {
                _towerLevelText.text = $"Level: {_selectedTower.CurrentLevel}";
            }
            if (_towerDamageText != null)
            {
                _towerDamageText.text = $"Damage: {_selectedTower.CurrentStats.damage}";
            }
            if (_upgradeCostText != null)
            {
                int cost = _selectedTower.GetUpgradeCost();
                _upgradeCostText.text = cost > 0 ? cost.ToString() : "MAX";
            }
            if (_sellValueText != null)
            {
                _sellValueText.text = _selectedTower.GetSellValue().ToString();
            }
            if (_upgradeButton != null)
            {
                _upgradeButton.interactable = !_selectedTower.IsMaxLevel;
            }
        }

        #endregion

        #region Button Handlers

        private void OnStartWaveClicked()
        {
            Wave.WaveManager waveManager = FindObjectOfType<Wave.WaveManager>();
            if (waveManager != null)
            {
                waveManager.ForceStartWave();
            }

            if (_startWaveButton != null)
            {
                _startWaveButton.gameObject.SetActive(false);
            }
        }

        private void OnPauseClicked()
        {
            GameManager.Instance.TogglePause();

            if (_pauseMenu != null)
            {
                _pauseMenu.SetActive(GameManager.Instance.CurrentState == GameState.Paused);
            }
        }

        private void OnSpeedClicked()
        {
            float currentSpeed = GameManager.Instance.GameSpeed;
            float newSpeed = currentSpeed >= 3f ? 1f : currentSpeed + 1f;

            if (_speedButton != null)
            {
                Text textComponent = _speedButton.GetComponentInChildren<Text>();
                if (textComponent != null)
                {
                    textComponent.text = $"{newSpeed}x";
                }
            }
        }

        private void OnUpgradeClicked()
        {
            if (_selectedTower != null)
            {
                _selectedTower.Upgrade();
                UpdateTowerInfo();
            }
        }

        private void OnSellClicked()
        {
            if (_selectedTower != null)
            {
                _selectedTower.Sell();
                HideTowerInfo();
            }
        }

        #endregion

        #region Game State

        private void OnGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Paused:
                    ShowPauseMenu();
                    break;
                case GameState.Playing:
                    HidePauseMenu();
                    break;
                case GameState.GameOver:
                    ShowGameOver();
                    break;
                case GameState.Victory:
                    ShowVictory();
                    break;
            }
        }

        private void ShowPauseMenu()
        {
            if (_pauseMenu != null)
            {
                _pauseMenu.SetActive(true);
            }
        }

        private void HidePauseMenu()
        {
            if (_pauseMenu != null)
            {
                _pauseMenu.SetActive(false);
            }
        }

        private void ShowGameOver()
        {
            if (_gameOverPanel != null)
            {
                _gameOverPanel.SetActive(true);
            }
        }

        private void ShowVictory()
        {
            if (_victoryPanel != null)
            {
                _victoryPanel.SetActive(true);
            }
        }

        #endregion

        #region Wave Timer

        public void UpdateWaveTimer(float time)
        {
            if (_waveTimerText != null)
            {
                if (time > 0)
                {
                    _waveTimerText.text = $"Next Wave: {Mathf.CeilToInt(time)}s";
                }
                else
                {
                    _waveTimerText.text = "Wave In Progress";
                }
            }
        }

        public void UpdateEnemyCount(int count)
        {
            if (_enemyCountText != null)
            {
                _enemyCountText.text = $"Enemies: {count}";
            }
        }

        #endregion
    }
}
