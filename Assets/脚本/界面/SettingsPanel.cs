using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _settingsPanel;
        
        [Header("Buttons")]
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private Button _quitButton;
        
        [Header("Settings")]
        [SerializeField] private Toggle _bgMusicToggle;
        [SerializeField] private Toggle _soundEffectsToggle;
        [SerializeField] private Slider _volumeSlider;
        [SerializeField] private Dropdown _languageDropdown;
        
        private bool _isPanelOpen = false;
        private bool _wasPausedBefore = false;
        
        private void Awake()
        {
            // 初始隐藏设置面板
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(false);
            }
            
            // 绑定按钮事件
            if (_settingsButton != null)
            {
                _settingsButton.onClick.AddListener(ToggleSettingsPanel);
            }
            
            if (_restartButton != null)
            {
                _restartButton.onClick.AddListener(OnRestartClicked);
            }
            
            if (_mainMenuButton != null)
            {
                _mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            }
            
            if (_quitButton != null)
            {
                _quitButton.onClick.AddListener(OnQuitClicked);
            }
            
            // 绑定设置选项事件
            if (_bgMusicToggle != null)
            {
                _bgMusicToggle.onValueChanged.AddListener(OnBgMusicChanged);
            }
            
            if (_soundEffectsToggle != null)
            {
                _soundEffectsToggle.onValueChanged.AddListener(OnSoundEffectsChanged);
            }
            
            if (_volumeSlider != null)
            {
                _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }
            
            if (_languageDropdown != null)
            {
                _languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
            }
            
            // 加载保存的设置
            LoadSettings();
        }
        
        private void LoadSettings()
        {
            // 从 PlayerPrefs 加载设置
            if (_bgMusicToggle != null)
            {
                _bgMusicToggle.isOn = PlayerPrefs.GetInt("BgMusicEnabled", 1) == 1;
            }
            
            if (_soundEffectsToggle != null)
            {
                _soundEffectsToggle.isOn = PlayerPrefs.GetInt("SoundEffectsEnabled", 1) == 1;
            }
            
            if (_volumeSlider != null)
            {
                _volumeSlider.value = PlayerPrefs.GetFloat("Volume", 0.7f);
            }
            
            if (_languageDropdown != null)
            {
                _languageDropdown.value = PlayerPrefs.GetInt("Language", 0);
            }
        }
        
        private void SaveSettings()
        {
            // 保存设置到 PlayerPrefs
            if (_bgMusicToggle != null)
            {
                PlayerPrefs.SetInt("BgMusicEnabled", _bgMusicToggle.isOn ? 1 : 0);
            }
            
            if (_soundEffectsToggle != null)
            {
                PlayerPrefs.SetInt("SoundEffectsEnabled", _soundEffectsToggle.isOn ? 1 : 0);
            }
            
            if (_volumeSlider != null)
            {
                PlayerPrefs.SetFloat("Volume", _volumeSlider.value);
            }
            
            if (_languageDropdown != null)
            {
                PlayerPrefs.SetInt("Language", _languageDropdown.value);
            }
            
            PlayerPrefs.Save();
        }
        
        public void ToggleSettingsPanel()
        {
            _isPanelOpen = !_isPanelOpen;
            
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(_isPanelOpen);
            }
            
            // 打开时暂停游戏，关闭时恢复
            if (_isPanelOpen)
            {
                _wasPausedBefore = Core.GameManager.Instance.CurrentState == Core.GameState.Paused;
                
                if (!_wasPausedBefore)
                {
                    Core.GameManager.Instance.PauseGame();
                    Debug.Log("设置界面打开，游戏已暂停");
                }
            }
            else
            {
                if (!_wasPausedBefore)
                {
                    Core.GameManager.Instance.ResumeGame();
                    Debug.Log("设置界面关闭，游戏已恢复");
                }
                
                // 保存设置
                SaveSettings();
            }
        }
        
        private void OnBgMusicChanged(bool enabled)
        {
            Debug.Log($"背景音乐: {(enabled ? "开启" : "关闭")}");
            // 这里可以添加实际的音乐控制逻辑
            // AudioManager.Instance.SetBgMusicEnabled(enabled);
        }
        
        private void OnSoundEffectsChanged(bool enabled)
        {
            Debug.Log($"音效: {(enabled ? "开启" : "关闭")}");
            // 这里可以添加实际的音效控制逻辑
            // AudioManager.Instance.SetSoundEffectsEnabled(enabled);
        }
        
        private void OnVolumeChanged(float value)
        {
            Debug.Log($"音量: {value * 100:F0}%");
            // 这里可以添加实际的音量控制逻辑
            // AudioManager.Instance.SetVolume(value);
        }
        
        private void OnLanguageChanged(int index)
        {
            string language = _languageDropdown.options[index].text;
            Debug.Log($"语言切换为: {language}");
            // 这里可以添加实际的语言切换逻辑
        }
        
        private void OnRestartClicked()
        {
            Debug.Log("重新开始游戏");
            Core.GameManager.Instance.RestartGame();
        }
        
        private void OnMainMenuClicked()
        {
            Debug.Log("返回主菜单");
            // 恢复游戏状态
            if (Core.GameManager.Instance.CurrentState == Core.GameState.Paused)
            {
                Core.GameManager.Instance.ResumeGame();
            }
            
            // 加载主菜单场景
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
        
        private void OnQuitClicked()
        {
            Debug.Log("退出游戏");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        
        private void OnDestroy()
        {
            // 清理事件绑定
            if (_settingsButton != null)
            {
                _settingsButton.onClick.RemoveListener(ToggleSettingsPanel);
            }
            
            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(OnRestartClicked);
            }
            
            if (_mainMenuButton != null)
            {
                _mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
            }
            
            if (_quitButton != null)
            {
                _quitButton.onClick.RemoveListener(OnQuitClicked);
            }
        }
    }
}
