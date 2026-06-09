using System.IO;
using UnityEngine;
using TowerDefense.Core;

namespace TowerDefense.Save
{
    public class SaveSystem : Singleton<SaveSystem>
    {
        private const string SAVE_FILE = "savedata.json";
        private const string SAVE_FOLDER = "Save";

        private GameSaveData _currentSave;
        private string SavePath => Path.Combine(Application.persistentDataPath, SAVE_FOLDER, SAVE_FILE);

        public GameSaveData CurrentSave => _currentSave;

        /// <summary>是否在模块模式下运行</summary>
        private bool _isModuleMode;

        protected override void Awake()
        {
            base.Awake();

            _isModuleMode = ModuleModeDetector.IsModuleMode;

            if (_isModuleMode)
            {
                // 模块模式下使用共享 SaveSystem 的模块存档
                LoadFromSharedSaveSystem();
            }
            else
            {
                LoadGame();
            }
        }

        /// <summary>
        /// 从共享 SaveSystem 加载塔防模块存档数据。
        /// </summary>
        private void LoadFromSharedSaveSystem()
        {
            var sharedSave = Sttop5.Shared.Save.SaveSystem.Instance;
            if (sharedSave != null)
            {
                string moduleData = sharedSave.LoadModuleData("towerdefense");
                if (!string.IsNullOrEmpty(moduleData))
                {
                    try
                    {
                        // 尝试从模块存档数据中提取塔防原生存档
                        var wrapper = JsonUtility.FromJson<TDModuleSaveWrapper>(moduleData);
                        if (wrapper != null && !string.IsNullOrEmpty(wrapper.tdSaveJson))
                        {
                            _currentSave = JsonUtility.FromJson<GameSaveData>(wrapper.tdSaveJson);
                        }
                        else
                        {
                            _currentSave = new GameSaveData();
                        }
                        Debug.Log("[SaveSystem] 从共享存档加载塔防模块数据");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[SaveSystem] 加载共享模块存档失败: {e.Message}");
                        _currentSave = new GameSaveData();
                    }
                }
                else
                {
                    _currentSave = new GameSaveData();
                    Debug.Log("[SaveSystem] 无塔防模块存档，创建新数据");
                }
            }
            else
            {
                LoadGame();
                Debug.LogWarning("[SaveSystem] 共享 SaveSystem 不可用，使用独立存档");
            }
        }

        /// <summary>
        /// 模块存档数据包装器，用于从共享 SaveSystem 的模块存档中提取塔防原生存档。
        /// </summary>
        [System.Serializable]
        private class TDModuleSaveWrapper
        {
            public string tdSaveJson;
        }

        public void SaveGame()
        {
            if (_isModuleMode)
            {
                SaveToSharedSaveSystem();
                return;
            }

            string directory = Path.GetDirectoryName(SavePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(_currentSave, true);
            File.WriteAllText(SavePath, json);

            Debug.Log($"[SaveSystem] Game saved to {SavePath}");
        }

        /// <summary>
        /// 保存到共享 SaveSystem 的模块存档。
        /// </summary>
        private void SaveToSharedSaveSystem()
        {
            var sharedSave = Sttop5.Shared.Save.SaveSystem.Instance;
            if (sharedSave != null)
            {
                string tdJson = _currentSave != null ? JsonUtility.ToJson(_currentSave) : "";
                sharedSave.SaveModuleData("towerdefense", tdJson, 1);
                Debug.Log("[SaveSystem] 塔防模块存档已保存到共享 SaveSystem");
            }
        }

        public void LoadGame()
        {
            if (File.Exists(SavePath))
            {
                string json = File.ReadAllText(SavePath);
                _currentSave = JsonUtility.FromJson<GameSaveData>(json);
                Debug.Log("[SaveSystem] Game loaded successfully");
            }
            else
            {
                _currentSave = new GameSaveData();
                Debug.Log("[SaveSystem] No save file found, creating new save data");
            }
        }

        public void DeleteSave()
        {
            if (_isModuleMode)
            {
                var sharedSave = Sttop5.Shared.Save.SaveSystem.Instance;
                if (sharedSave != null)
                {
                    sharedSave.DeleteModuleData("towerdefense");
                    _currentSave = new GameSaveData();
                    Debug.Log("[SaveSystem] Module save data deleted");
                }
                return;
            }

            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
                _currentSave = new GameSaveData();
                Debug.Log("[SaveSystem] Save file deleted");
            }
        }

        public void SaveLevelProgress(int levelIndex, int stars, int score, float time)
        {
            if (levelIndex < 0) return;

            while (_currentSave.levels.Count <= levelIndex)
            {
                _currentSave.levels.Add(new GameSaveData.LevelSaveData
                {
                    levelIndex = _currentSave.levels.Count,
                    isUnlocked = false,
                    starsEarned = 0,
                    highScore = 0,
                    bestTime = float.MaxValue
                });
            }

            var levelData = _currentSave.levels[levelIndex];
            levelData.isUnlocked = true;
            levelData.starsEarned = Mathf.Max(levelData.starsEarned, stars);
            levelData.highScore = Mathf.Max(levelData.highScore, score);
            levelData.bestTime = Mathf.Min(levelData.bestTime, time);

            _currentSave.levels[levelIndex] = levelData;

            if (levelIndex + 1 < _currentSave.levels.Count)
            {
                var nextLevel = _currentSave.levels[levelIndex + 1];
                nextLevel.isUnlocked = true;
                _currentSave.levels[levelIndex + 1] = nextLevel;
            }

            SaveGame();
        }

        public GameSaveData.LevelSaveData GetLevelData(int levelIndex)
        {
            if (levelIndex >= 0 && levelIndex < _currentSave.levels.Count)
            {
                return _currentSave.levels[levelIndex];
            }
            return new GameSaveData.LevelSaveData { levelIndex = levelIndex, isUnlocked = levelIndex == 0 };
        }

        public void SaveHeroProgress(string heroName, int level, float experience)
        {
            var heroData = _currentSave.heroes.Find(h => h.heroName == heroName);
            if (heroData == null)
            {
                heroData = new GameSaveData.HeroSaveData { heroName = heroName, isUnlocked = true };
                _currentSave.heroes.Add(heroData);
            }

            heroData.level = level;
            heroData.experience = experience;

            SaveGame();
        }

        public GameSaveData.HeroSaveData GetHeroData(string heroName)
        {
            return _currentSave.heroes.Find(h => h.heroName == heroName);
        }

        public void UpdatePlayerStats(int gold, int kills, int waves, float playTime)
        {
            _currentSave.player.totalGold += gold;
            _currentSave.player.totalKills += kills;
            _currentSave.player.totalWavesCompleted += waves;
            _currentSave.player.totalPlayTime += playTime;
            _currentSave.player.totalGamesPlayed++;

            SaveGame();
        }

        public void SaveSettings(float musicVolume, float sfxVolume, bool fullscreen, int quality)
        {
            _currentSave.settings.musicVolume = musicVolume;
            _currentSave.settings.sfxVolume = sfxVolume;
            _currentSave.settings.isFullscreen = fullscreen;
            _currentSave.settings.qualityLevel = quality;

            SaveGame();
        }

        public void LoadSettings()
        {
            AudioListener.volume = _currentSave.settings.sfxVolume;
            Screen.fullScreen = _currentSave.settings.isFullscreen;
            QualitySettings.SetQualityLevel(_currentSave.settings.qualityLevel);
        }

        public int CalculateTotalStars()
        {
            int total = 0;
            foreach (var level in _currentSave.levels)
            {
                total += level.starsEarned;
            }
            _currentSave.totalStars = total;
            return total;
        }
    }
}
