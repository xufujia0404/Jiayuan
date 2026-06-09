using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TowerDefense.Core;

namespace TowerDefense.UI
{
    public class VictoryPanel : MonoBehaviour
    {
        [Header("Title")]
        [SerializeField] private Text _titleText;

        [Header("Stars")]
        [SerializeField] private Image[] _starImages = new Image[3];
        [SerializeField] private Sprite _starFilledSprite;
        [SerializeField] private Sprite _starEmptySprite;

        [Header("Stats")]
        [SerializeField] private Text _killCountText;
        [SerializeField] private Text _goldText;
        [SerializeField] private Text _waveText;

        [Header("Buttons")]
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _nextLevelButton;
        [SerializeField] private Button _mainMenuButton;

        private void Awake()
        {
            if (_restartButton != null)
                _restartButton.onClick.AddListener(OnRestartClicked);
            if (_nextLevelButton != null)
                _nextLevelButton.onClick.AddListener(OnNextLevelClicked);
            if (_mainMenuButton != null)
                _mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        private void OnDestroy()
        {
            if (_restartButton != null)
                _restartButton.onClick.RemoveListener(OnRestartClicked);
            if (_nextLevelButton != null)
                _nextLevelButton.onClick.RemoveListener(OnNextLevelClicked);
            if (_mainMenuButton != null)
                _mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
        }

        public void ShowVictory()
        {
            gameObject.SetActive(true);
            UpdateStats();
            UpdateStars();
        }

        public void HideVictory()
        {
            gameObject.SetActive(false);
        }

        private void UpdateStats()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (_killCountText != null)
                _killCountText.text = gm.EnemiesKilled.ToString();
            if (_goldText != null)
                _goldText.text = gm.CurrentGold.ToString();
            if (_waveText != null)
                _waveText.text = $"{gm.CurrentWave}/{gm.TotalWaves}";
        }

        private void UpdateStars()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            int starCount = CalculateStars(gm);
            for (int i = 0; i < _starImages.Length; i++)
            {
                if (_starImages[i] == null) continue;
                if (_starFilledSprite != null && _starEmptySprite != null)
                {
                    _starImages[i].sprite = i < starCount ? _starFilledSprite : _starEmptySprite;
                }
                else
                {
                    _starImages[i].color = i < starCount
                        ? new Color(1f, 0.85f, 0f, 1f)
                        : new Color(0.3f, 0.3f, 0.3f, 0.5f);
                }
            }
        }

        private int CalculateStars(GameManager gm)
        {
            float lifeRatio = (float)gm.CurrentLife / 20f;
            if (lifeRatio >= 0.7f) return 3;
            if (lifeRatio >= 0.4f) return 2;
            return 1;
        }

        private void OnRestartClicked()
        {
            HideVictory();
            GameManager.Instance.RestartGame();
        }

        private void OnNextLevelClicked()
        {
            HideVictory();
            int nextIndex = GameManager.Instance.CurrentLevelIndex + 1;
            var levels = GameManager.Instance.Levels;
            if (levels != null && nextIndex < levels.Length)
            {
                GameManager.Instance.LoadLevel(nextIndex);
            }
            else
            {
                // 没有下一关，回到主菜单
                GameManager.Instance.ReturnToHome();
            }
        }

        private void OnMainMenuClicked()
        {
            HideVictory();
            if (ModuleModeDetector.IsModuleMode)
            {
                // 从家园进入时，返回家园
                SceneManager.LoadScene("HomeScene");
            }
            else
            {
                SceneManager.LoadScene("MainMenu");
            }
        }
    }
}
