using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Data.Config
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "TowerDefense/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Tower Configurations")]
        public List<TowerConfig> towers = new List<TowerConfig>();

        [Header("Enemy Configurations")]
        public List<EnemyConfig> enemies = new List<EnemyConfig>();

        [Header("Wave Configurations")]
        public List<WaveConfig> waves = new List<WaveConfig>();

        [Header("Level Configurations")]
        public List<LevelConfig> levels = new List<LevelConfig>();

        [Header("Hero Configurations")]
        public List<HeroConfig> heroes = new List<HeroConfig>();

        [Serializable]
        public class TowerConfig
        {
            public string towerName;
            public TowerType towerType;
            public List<TowerLevel> levels = new List<TowerLevel>();

            [Serializable]
            public class TowerLevel
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
                public int maxSplashTargets;
            }
        }

        [Serializable]
        public class EnemyConfig
        {
            public string enemyName;
            public EnemyType enemyType;
            public EnemyStats stats;

            [Serializable]
            public struct EnemyStats
            {
                public float maxHealth;
                public float moveSpeed;
                public int goldReward;
                public int lifeDamage;
                public float physicalResistance;
                public float magicResistance;
                public float explosionResistance;
                public bool isFlying;
                public bool isArmored;
                public bool isMagicImmune;
                public bool canBeSlowed;
                public float slowImmunity;
                public bool isBoss;
                public float bossHealthMultiplier;
            }
        }

        [Serializable]
        public class WaveConfig
        {
            public int waveNumber;
            public float waveDelay;
            public float spawnInterval;
            public List<WaveEnemy> enemies = new List<WaveEnemy>();

            [Serializable]
            public struct WaveEnemy
            {
                public string enemyName;
                public int count;
                public float spawnDelay;
                public string spawnPoint;
            }
        }

        [Serializable]
        public class LevelConfig
        {
            public string levelName;
            public string sceneName;
            public int initialGold;
            public int initialLife;
            public float timeBetweenWaves;
            public List<int> waveIndices = new List<int>();
            public List<Vector3> towerSlots = new List<Vector3>();
            public List<PathConfig> paths = new List<PathConfig>();

            [Serializable]
            public struct PathConfig
            {
                public string pathName;
                public List<Vector3> waypoints;
            }
        }

        [Serializable]
        public class HeroConfig
        {
            public string heroName;
            public HeroStats stats;
            public List<HeroSkill> skills = new List<HeroSkill>();

            [Serializable]
            public struct HeroStats
            {
                public float maxHealth;
                public float attackDamage;
                public float attackRange;
                public float attackSpeed;
                public float moveSpeed;
                public float healthRegen;
                public float armor;
            }

            [Serializable]
            public struct HeroSkill
            {
                public string skillName;
                public string description;
                public float cooldown;
                public float manaCost;
                public float effectDuration;
                public float effectRadius;
                public float effectValue;
            }
        }
    }
}
