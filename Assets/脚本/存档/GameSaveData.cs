using System;
using System.Collections.Generic;
using System.Linq;

namespace TowerDefense.Save
{
    [Serializable]
    public class GameSaveData
    {
        public int totalStars;
        public int currentLevel;
        public List<LevelSaveData> levels = new List<LevelSaveData>();
        public List<HeroSaveData> heroes = new List<HeroSaveData>();
        public SettingsSaveData settings = new SettingsSaveData();
        public PlayerSaveData player = new PlayerSaveData();
        public QuestSaveData quests = new QuestSaveData();

        [Serializable]
        public class LevelSaveData
        {
            public int levelIndex;
            public bool isUnlocked;
            public int starsEarned;
            public int highScore;
            public float bestTime;
        }

        [Serializable]
        public class HeroSaveData
        {
            public string heroName;
            public int level;
            public float experience;
            public bool isUnlocked;
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
        public class PlayerSaveData
        {
            public int totalGold;
            public int totalKills;
            public int totalWavesCompleted;
            public int totalGamesPlayed;
            public float totalPlayTime;
            public int playerLevel = 1;
            public int playerExp;
        }

        [Serializable]
        public class QuestItemSave
        {
            public int id;
            public int currentCount;
            public int status;
        }

        [Serializable]
        public class QuestSaveData
        {
            public List<QuestItemSave> items = new List<QuestItemSave>();
            public int dailyActivity;
            public string dailyResetDate;
        }
    }
}
