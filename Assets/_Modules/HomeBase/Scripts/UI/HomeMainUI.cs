using UnityEngine;
using UnityEngine.UI;
using Sttop5.Shared.Core;
using Sttop5.Shared.Player;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 家园主界面 UI，显示资源栏、建筑菜单和操作按钮。
    /// </summary>
    public class HomeMainUI : MonoBehaviour
    {
        [Header("资源栏")]
        [SerializeField] private Text _goldText;
        [SerializeField] private Text _diamondText;
        [SerializeField] private Text _woodText;
        [SerializeField] private Text _stoneText;
        [SerializeField] private Text _foodText;
        [SerializeField] private Text _levelText;

        [Header("按钮 - 顶部")]
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _backpackButton;
        [SerializeField] private Button _shopButton;
        [SerializeField] private Button _friendButton;

        [Header("按钮 - 左侧")]
        [SerializeField] private Button _collectAllButton;
        [SerializeField] private Button _buildMenuButton;
        [SerializeField] private Button _taskButton;

        [Header("建筑菜单")]
        [SerializeField] private GameObject _buildMenuPanel;
        [SerializeField] private Transform _buildButtonContainer;
        [SerializeField] private BuildMenuUI _buildMenuUI;

        [Header("面板")]
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _taskPanel;

        [Header("设置面板按钮")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _quitButton;

        [Header("建造模式提示")]
        [SerializeField] private GameObject _buildModeHint;
        [SerializeField] private BuildModeController _buildModeController;

        [Header("底部信息栏")]
        [SerializeField] private Text _bottomInfoText;

        [Header("组件引用")]
        [SerializeField] private HomeManager _homeManager;
        [SerializeField] private BuildingManager _buildingManager;
        [SerializeField] private ResourceManager _resourceManager;

        private void Start()
        {
            SetupButtons();
            SubscribeEvents();
            RefreshUI();

            // 面板默认隐藏
            if (_buildModeHint != null)
                _buildModeHint.SetActive(false);
            if (_settingsPanel != null)
                _settingsPanel.SetActive(false);
            if (_taskPanel != null)
                _taskPanel.SetActive(false);

            // 监听建造模式状态变化
            if (_buildModeController != null)
                _buildModeController.OnStateChanged += OnBuildStateChanged;
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();

            if (_buildModeController != null)
                _buildModeController.OnStateChanged -= OnBuildStateChanged;
        }

        private void SetupButtons()
        {
            // 左侧按钮
            if (_collectAllButton != null)
                _collectAllButton.onClick.AddListener(OnCollectAllClicked);

            if (_buildMenuButton != null)
                _buildMenuButton.onClick.AddListener(ToggleBuildMenu);

            if (_taskButton != null)
                _taskButton.onClick.AddListener(ToggleTaskPanel);

            // 顶部按钮
            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(ToggleSettingsPanel);

            if (_backpackButton != null)
                _backpackButton.onClick.AddListener(() => ShowTips("背包功能即将开放"));

            if (_shopButton != null)
                _shopButton.onClick.AddListener(() => ShowTips("商店功能即将开放"));

            if (_friendButton != null)
                _friendButton.onClick.AddListener(() => ShowTips("好友功能即将开放"));

            // 设置面板按钮
            if (_resumeButton != null)
                _resumeButton.onClick.AddListener(CloseSettingsPanel);

            if (_restartButton != null)
                _restartButton.onClick.AddListener(OnRestartClicked);

            if (_quitButton != null)
                _quitButton.onClick.AddListener(OnQuitClicked);

            if (_buildMenuPanel != null)
                _buildMenuPanel.SetActive(false);
        }

        #region 事件订阅

        private void SubscribeEvents()
        {
            var profile = PlayerProfile.Instance;
            if (profile != null)
            {
                profile.OnGoldChanged += OnGoldChanged;
                profile.OnDiamondChanged += OnDiamondChanged;
                profile.OnLevelChanged += OnLevelChanged;
            }

            if (_resourceManager != null)
            {
                _resourceManager.OnLocalResourcesChanged += OnLocalResourcesChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            var profile = PlayerProfile.Instance;
            if (profile != null)
            {
                profile.OnGoldChanged -= OnGoldChanged;
                profile.OnDiamondChanged -= OnDiamondChanged;
                profile.OnLevelChanged -= OnLevelChanged;
            }

            if (_resourceManager != null)
            {
                _resourceManager.OnLocalResourcesChanged -= OnLocalResourcesChanged;
            }
        }

        #endregion

        #region UI 更新

        private void RefreshUI()
        {
            var profile = PlayerProfile.Instance;
            if (profile != null)
            {
                SetText(_goldText, profile.Gold.ToString());
                SetText(_diamondText, profile.Diamond.ToString());
                SetText(_levelText, $"Lv.{profile.Level}");
            }

            if (_resourceManager != null)
            {
                SetText(_woodText, _resourceManager.Wood.ToString());
                SetText(_stoneText, _resourceManager.Stone.ToString());
                SetText(_foodText, _resourceManager.Food.ToString());
            }
        }

        private void OnGoldChanged(int gold)
        {
            SetText(_goldText, gold.ToString());
        }

        private void OnDiamondChanged(int diamond)
        {
            SetText(_diamondText, diamond.ToString());
        }

        private void OnLevelChanged(int level)
        {
            SetText(_levelText, $"Lv.{level}");
        }

        private void OnLocalResourcesChanged(int wood, int stone, int food)
        {
            SetText(_woodText, wood.ToString());
            SetText(_stoneText, stone.ToString());
            SetText(_foodText, food.ToString());
        }

        private void SetText(Text text, string value)
        {
            if (text != null) text.text = value;
        }

        #endregion

        #region 按钮回调

        private void OnCollectAllClicked()
        {
            _homeManager?.OnCollectAllClicked();
        }

        private void ToggleBuildMenu()
        {
            if (_buildMenuUI != null)
            {
                if (_buildMenuPanel != null)
                {
                    bool show = !_buildMenuPanel.activeSelf;
                    _buildMenuPanel.SetActive(show);
                    if (show) _buildMenuUI.Open();
                    else _buildMenuUI.Close();
                }
            }
            else if (_buildMenuPanel != null)
            {
                _buildMenuPanel.SetActive(!_buildMenuPanel.activeSelf);
            }
        }

        private void OnBuildStateChanged(BuildModeController.BuildState state)
        {
            if (_buildModeHint != null)
                _buildModeHint.SetActive(state == BuildModeController.BuildState.Placing);
        }

        #region 面板切换

        private void ToggleSettingsPanel()
        {
            if (_settingsPanel == null) return;
            bool show = !_settingsPanel.activeSelf;
            _settingsPanel.SetActive(show);
            if (show)
                Time.timeScale = 0f;
            else
                Time.timeScale = 1f;
        }

        private void CloseSettingsPanel()
        {
            if (_settingsPanel != null)
                _settingsPanel.SetActive(false);
            Time.timeScale = 1f;
        }

        private void ToggleTaskPanel()
        {
            if (_taskPanel == null) return;
            _taskPanel.SetActive(!_taskPanel.activeSelf);
        }

        private void OnRestartClicked()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ShowTips(string msg)
        {
            Debug.Log($"[HomeMainUI] {msg}");
            if (_bottomInfoText != null)
            {
                _bottomInfoText.text = msg;
                CancelInvoke(nameof(ClearTips));
                Invoke(nameof(ClearTips), 2f);
            }
        }

        private void ClearTips()
        {
            if (_bottomInfoText != null)
                _bottomInfoText.text = "";
        }

        #endregion

        /// <summary>
        /// 返回按钮回调（返回家园或退出）。
        /// </summary>
        public void OnBackClicked()
        {
            var moduleManager = ModuleManager.Instance;
            if (moduleManager != null && moduleManager.ActiveModuleId != "home")
            {
                moduleManager.DeactivateCurrentModule();
            }
        }

        #endregion
    }
}
