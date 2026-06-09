using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sttop5.Shared.Save
{
    /// <summary>
    /// 存档数据结构，支持模块化存档。
    /// 每个游戏模块将自己的数据序列化为 JSON 字符串存入 moduleData 字典。
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        public int version = 1;
        public PlayerSaveData player = new PlayerSaveData();
        public SettingsSaveData settings = new SettingsSaveData();
        public string lastActiveModuleId = "home";

        /// <summary>模块存档数据列表</summary>
        public List<ModuleSaveEntry> moduleData = new List<ModuleSaveEntry>();

        [Serializable]
        public class PlayerSaveData
        {
            public int gold;
            public int diamond;
            public int stamina;
            public int playerLevel = 1;
            public int playerExp;
            public int totalGamesPlayed;
            public float totalPlayTime;
            public long lastLoginTimeTicks;
        }

        [Serializable]
        public class SettingsSaveData
        {
            public float musicVolume = 1f;
            public float sfxVolume = 1f;
            public bool isFullscreen = true;
            public int qualityLevel = 2;
            public string language = "zh";
        }

        [Serializable]
        public class ModuleSaveEntry
        {
            public string moduleId;
            public string dataJson;
            public int dataVersion;
        }
    }

    /// <summary>
    /// 模块存档接口，每个游戏模块实现此接口以提供存档支持。
    /// </summary>
    public interface IModuleSaveData
    {
        string ModuleId { get; }
        int DataVersion { get; }
        string Serialize();
        void Deserialize(string json);
    }
}
