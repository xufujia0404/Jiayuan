using System;
using UnityEngine;

namespace TowerDefense.Data
{
    [CreateAssetMenu(fileName = "TowerData", menuName = "TowerDefense/TowerData")]
    public class TowerData : ScriptableObject
    {
        public string towerName;
        public string description;
        public TowerType towerType;
        public GameObject towerPrefab;
        public GameObject projectilePrefab;
        public Sprite icon;
        public TowerStats[] levels;

        [Header("Barracks Settings (only used for Barracks type)")]
        public SoldierData soldierData;
        public int[] soldierCountPerLevel = { 3, 5, 7 };

        [Serializable]
        public struct TowerStats
        {
            public int level;
            public int cost;
            public int upgradeCost;
            public int sellValue;
            public float damage;
            public float attackRange;
            public float attackSpeed;
            public ProjectileType projectileType;
            public int projectileCount;
            public float splashRadius;
            public float slowAmount;
            public float slowDuration;
            public int maxTargets;
            public int pierceCount;
        }
    }

    public enum TowerType
    {
        Archer,
        Mage,
        Barracks,
        Artillery,
        Sword
    }

    public enum ProjectileType
    {
        Arrow,
        Magic,
        Cannonball,
        None
    }
}
