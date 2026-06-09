using UnityEngine;
using UnityEngine.UI;
using TowerDefense.Data;

namespace TowerDefense.UI
{
    public class LevelButton : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image _previewImage;
        [SerializeField] private Text _levelNameText;
        [SerializeField] private GameObject[] _stars;
        [SerializeField] private GameObject _lockOverlay;
        [SerializeField] private Button _button;

        private LevelData _levelData;
        private int _levelIndex;
        private MainMenuUI _mainMenu;

        public void Setup(LevelData levelData, int index, MainMenuUI mainMenu)
        {
            _levelData = levelData;
            _levelIndex = index;
            _mainMenu = mainMenu;

            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_levelNameText != null)
            {
                _levelNameText.text = _levelData.levelName;
            }

            if (_previewImage != null && _levelData.previewImage != null)
            {
                _previewImage.sprite = _levelData.previewImage;
            }

            if (_lockOverlay != null)
            {
                _lockOverlay.SetActive(!_levelData.isUnlocked);
            }

            if (_button != null)
            {
                _button.interactable = _levelData.isUnlocked;
                _button.onClick.AddListener(OnClicked);
            }

            int starsEarned = GetStarsEarned();
            for (int i = 0; i < _stars.Length; i++)
            {
                if (_stars[i] != null)
                {
                    _stars[i].SetActive(i < starsEarned);
                }
            }
        }

        private int GetStarsEarned()
        {
            return PlayerPrefs.GetInt($"Level_{_levelIndex}_Stars", 0);
        }

        private void OnClicked()
        {
            if (_mainMenu != null && _levelData.isUnlocked)
            {
                _mainMenu.StartLevel(_levelIndex);
            }
        }
    }
}
