using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Sttop5.Shared.Core;

namespace Sttop5.Shared.Save
{
    /// <summary>
    /// 统一存档系统，管理跨所有模块的存档数据。
    /// 支持模块化存档：每个游戏模块独立管理自己的存档数据。
    /// </summary>
    public class SaveSystem : Core.Singleton<SaveSystem>
    {
        private const string SAVE_FILE = "gamesave.json";
        private const string SAVE_FOLDER = "Save";
        private const int CURRENT_VERSION = 1;

        private GameSaveData _currentSave;
        private string SavePath => Path.Combine(Application.persistentDataPath, SAVE_FOLDER, SAVE_FILE);

        public GameSaveData CurrentSave => _currentSave;

        protected override void Awake()
        {
            base.Awake();
            LoadGame();
        }

        #region 基础存档操作

        /// <summary>
        /// 保存游戏到磁盘。
        /// </summary>
        public void SaveGame()
        {
            try
            {
                string directory = Path.GetDirectoryName(SavePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _currentSave.version = CURRENT_VERSION;
                _currentSave.player.lastLoginTimeTicks = DateTime.Now.Ticks;

                string json = JsonUtility.ToJson(_currentSave, true);
                File.WriteAllText(SavePath, json);

                Debug.Log("[SaveSystem] 游戏已保存");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 保存失败: {e.Message}");
            }
        }

        /// <summary>
        /// 从磁盘加载游戏。
        /// </summary>
        public void LoadGame()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    _currentSave = JsonUtility.FromJson<GameSaveData>(json);
                    Debug.Log("[SaveSystem] 存档已加载");
                }
                else
                {
                    _currentSave = new GameSaveData();
                    Debug.Log("[SaveSystem] 无存档文件，创建新存档");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 加载失败: {e.Message}");
                _currentSave = new GameSaveData();
            }
        }

        /// <summary>
        /// 删除存档文件。
        /// </summary>
        public void DeleteSave()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    File.Delete(SavePath);
                }
                _currentSave = new GameSaveData();
                Debug.Log("[SaveSystem] 存档已删除");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 删除存档失败: {e.Message}");
            }
        }

        #endregion

        #region 模块化存档

        /// <summary>
        /// 保存指定模块的存档数据。
        /// </summary>
        public void SaveModuleData(string moduleId, string jsonData, int dataVersion)
        {
            if (string.IsNullOrEmpty(moduleId)) return;

            var entry = _currentSave.moduleData.Find(m => m.moduleId == moduleId);
            if (entry == null)
            {
                entry = new GameSaveData.ModuleSaveEntry
                {
                    moduleId = moduleId
                };
                _currentSave.moduleData.Add(entry);
            }

            entry.dataJson = jsonData;
            entry.dataVersion = dataVersion;
            SaveGame();
        }

        /// <summary>
        /// 保存模块存档数据（通过 IModuleSaveData 接口）。
        /// </summary>
        public void SaveModuleData(IModuleSaveData moduleData)
        {
            SaveModuleData(moduleData.ModuleId, moduleData.Serialize(), moduleData.DataVersion);
        }

        /// <summary>
        /// 加载指定模块的存档数据。
        /// </summary>
        public string LoadModuleData(string moduleId)
        {
            var entry = _currentSave.moduleData.Find(m => m.moduleId == moduleId);
            return entry?.dataJson;
        }

        /// <summary>
        /// 加载模块存档数据并反序列化。
        /// </summary>
        public T LoadModuleData<T>() where T : IModuleSaveData, new()
        {
            var data = new T();
            var json = LoadModuleData(data.ModuleId);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    data.Deserialize(json);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveSystem] 模块存档反序列化失败: {data.ModuleId} - {e.Message}");
                }
            }
            return data;
        }

        /// <summary>
        /// 删除指定模块的存档数据。
        /// </summary>
        public void DeleteModuleData(string moduleId)
        {
            _currentSave.moduleData.RemoveAll(m => m.moduleId == moduleId);
            SaveGame();
        }

        /// <summary>
        /// 保存所有已注册模块的存档数据。
        /// </summary>
        public void SaveAllModules()
        {
            var moduleManager = Core.ModuleManager.Instance;
            if (moduleManager == null) return;

            var allData = moduleManager.CollectAllModuleSaveData();
            foreach (var kvp in allData)
            {
                var entry = _currentSave.moduleData.Find(m => m.moduleId == kvp.Key);
                if (entry == null)
                {
                    entry = new GameSaveData.ModuleSaveEntry { moduleId = kvp.Key };
                    _currentSave.moduleData.Add(entry);
                }
                entry.dataJson = kvp.Value;
            }

            SaveGame();
        }

        #endregion

        #region 设置

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

        #endregion

        #region 自动保存

        private float _autoSaveInterval = 60f;
        private float _autoSaveTimer;

        private void Update()
        {
            _autoSaveTimer += Time.unscaledDeltaTime;
            if (_autoSaveTimer >= _autoSaveInterval)
            {
                _autoSaveTimer = 0f;
                SaveGame();
            }
        }

        #endregion

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) SaveGame();
        }

        private void OnApplicationQuit()
        {
            SaveGame();
        }
    }
}
