using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Data
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "TowerDefense/LevelData")]
    public class LevelData : ScriptableObject
    {
        public string levelName;
        public string sceneName;
        public Sprite previewImage;
        public int unlockCost;
        public bool isUnlocked;

        [Header("Tilemap Layout")]
        [Tooltip("用字符画地图: G=草地 P=路径 B=建塔位 D=装饰 S=起点 E=终点 .=空\n从上到下，第0行是最上面一行")]
        [TextArea(3, 30)]
        public string[] mapRows;

        [Header("Map Settings")]
        public Vector2 mapSize;
        public List<PathData> paths = new List<PathData>();
        public List<TowerSlotData> towerSlots = new List<TowerSlotData>();

        [Header("Wave Settings")]
        public int waveCount = 5;
        public List<WaveData> waves = new List<WaveData>();
        public float timeBetweenWaves = 10f;

        [Header("Rewards")]
        public int star1Requirement;
        public int star2Requirement;
        public int star3Requirement;
        public int goldReward;

        [Header("Initial Resources")]
        public int initialGold = 100;
        public int initialLife = 20;

        [Serializable]
        public struct PathData
        {
            public string pathName;
            public List<Vector3> waypoints;
        }

        [Serializable]
        public struct TowerSlotData
        {
            public Vector3 position;
            public SlotType slotType;
        }

        public enum SlotType
        {
            Normal,
            Premium,
            Restricted
        }

        public int MapWidth => mapRows?.Length > 0 ? mapRows[0].Length : 0;
        public int MapHeight => mapRows?.Length ?? 0;
    }
}