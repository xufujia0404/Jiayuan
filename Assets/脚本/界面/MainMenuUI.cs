using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TowerDefense.Core;
using TowerDefense.Data;

namespace TowerDefense.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject _mainPanel;
        [SerializeField] private GameObject _levelSelectPanel;
        [SerializeField] private GameObject _settingsPanel;

        [Header("Level Selection")]
        [SerializeField] private Transform _levelButtonContainer;
        [SerializeField] private GameObject _levelButtonPrefab;

        [Header("Settings")]
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _sfxSlider;
        [SerializeField] private Toggle _fullscreenToggle;

        [Header("Currency Displays")]
        [SerializeField] private Text _goldText;
        [SerializeField] private Text _diamondText;
        [SerializeField] private Text _staminaText;
        [SerializeField] private Text _playerLevelText;
        [SerializeField] private Slider _playerExpSlider;

        [Header("Exchange")]
        [SerializeField] private Button _exchangeButton;
        [SerializeField] private ExchangePanel _exchangePanel;

        [Header("Level Data")]
        [SerializeField] private LevelData[] _levels;

        private void Start()
        {
            ShowMainPanel();
            LoadSettings();
            UpdatePlayerLevelDisplay();

            if (_exchangeButton != null)
                _exchangeButton.onClick.AddListener(OnExchangeClicked);
        }

        private void OnEnable()
        {
            var wallet = PlayerWallet.Instance;
            if (wallet != null)
            {
                wallet.OnGoldChanged += UpdateGold;
                wallet.OnDiamondChanged += UpdateDiamond;
                wallet.OnStaminaChanged += UpdateStamina;
                UpdateGold(wallet.Gold);
                UpdateDiamond(wallet.Diamond);
                UpdateStamina(wallet.Stamina);
            }

            var levelSystem = PlayerLevelSystem.Instance;
            if (levelSystem != null)
            {
                levelSystem.OnLevelChanged += UpdateLevelText;
                levelSystem.OnExpChanged += UpdateExpSlider;
                UpdatePlayerLevelDisplay();
            }
        }

        private void OnDisable()
        {
            var wallet = PlayerWallet.Instance;
            if (wallet != null)
            {
                wallet.OnGoldChanged -= UpdateGold;
                wallet.OnDiamondChanged -= UpdateDiamond;
                wallet.OnStaminaChanged -= UpdateStamina;
            }

            var levelSystem = PlayerLevelSystem.Instance;
            if (levelSystem != null)
            {
                levelSystem.OnLevelChanged -= UpdateLevelText;
                levelSystem.OnExpChanged -= UpdateExpSlider;
            }
        }

        private void UpdateGold(int amount) { if (_goldText != null) _goldText.text = amount.ToString(); }
        private void UpdateDiamond(int amount) { if (_diamondText != null) _diamondText.text = amount.ToString(); }
        private void UpdateStamina(int amount) { if (_staminaText != null) _staminaText.text = amount.ToString(); }

        public void ShowMainPanel()
        {
            if (_mainPanel != null) _mainPanel.SetActive(true);
            if (_levelSelectPanel != null) _levelSelectPanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
        }

        public void ShowLevelSelect()
        {
            if (_mainPanel != null) _mainPanel.SetActive(false);
            if (_levelSelectPanel != null) _levelSelectPanel.SetActive(true);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);

            GenerateLevelButtons();
        }

        public void ShowSettings()
        {
            if (_mainPanel != null) _mainPanel.SetActive(false);
            if (_levelSelectPanel != null) _levelSelectPanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(true);
        }

        public void StartLevel(int levelIndex)
        {
            if (levelIndex >= 0 && levelIndex < _levels.Length)
            {
                SceneManager.LoadScene(_levels[levelIndex].sceneName);
            }
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnExchangeClicked()
        {
            if (_exchangePanel != null)
                _exchangePanel.Show();
        }

        private void GenerateLevelButtons()
        {
            if (_levelButtonContainer == null || _levelButtonPrefab == null) return;

            foreach (Transform child in _levelButtonContainer)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < _levels.Length; i++)
            {
                GameObject buttonObj = Instantiate(_levelButtonPrefab, _levelButtonContainer);
                LevelButton levelButton = buttonObj.GetComponent<LevelButton>();

                if (levelButton != null)
                {
                    levelButton.Setup(_levels[i], i, this);
                }
            }
        }

        #region Settings

        public void OnMusicVolumeChanged(float value)
        {
            PlayerPrefs.SetFloat("MusicVolume", value);
            PlayerPrefs.Save();
        }

        public void OnSFXVolumeChanged(float value)
        {
            PlayerPrefs.SetFloat("SFXVolume", value);
            PlayerPrefs.Save();
        }

        public void OnFullscreenToggled(bool isFullscreen)
        {
            Screen.fullScreen = isFullscreen;
            PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void LoadSettings()
        {
            if (_musicSlider != null)
            {
                _musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
            }
            if (_sfxSlider != null)
            {
                _sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
            }
            if (_fullscreenToggle != null)
            {
                _fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
            }
        }

        #endregion

        #region Player Level Display

        private void UpdatePlayerLevelDisplay()
        {
            var levelSystem = PlayerLevelSystem.Instance;
            if (levelSystem == null) return;

            UpdateLevelText(levelSystem.Level);
            UpdateExpSlider(levelSystem.CurrentExp, levelSystem.ExpToNextLevel);
        }

        private void UpdateLevelText(int level)
        {
            if (_playerLevelText != null)
                _playerLevelText.text = $"Lv.{level}";
        }

        private void UpdateExpSlider(int currentExp, int expToNext)
        {
            if (_playerExpSlider != null)
            {
                _playerExpSlider.maxValue = expToNext;
                _playerExpSlider.value = currentExp;
            }
        }

        #endregion
    }
}
