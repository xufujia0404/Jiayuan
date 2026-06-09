using System;
using UnityEngine;

namespace TowerDefense.Data
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "TowerDefense/EnemyData")]
    public class EnemyData : ScriptableObject
    {
        public string enemyName;
        public EnemyType enemyType;
        public GameObject enemyPrefab;
        public EnemyStats stats;

        [Serializable]
        public struct EnemyStats
        {
            [Header("Basic Stats")]
            public int maxHealth;
            public float moveSpeed;
            public int goldReward;
            public int lifeDamage;
            [Tooltip("Damage dealt to soldiers when blocked (Kingdom Rush mechanic)")]
            public int attackDamage;

            [Header("Resistances")]
            public float physicalResistance;
            public float magicResistance;
            public float explosionResistance;

            [Header("Special Properties")]
            public bool isFlying;
            public bool isArmored;
            public bool isMagicImmune;
            public bool canBeSlowed;
            public float slowImmunity;

            [Header("Boss Properties")]
            public bool isBoss;
            public float bossHealthMultiplier;
            public string bossName;
        }
    }

    public enum EnemyType
    {
        Goblin,
        Orc,
        Troll,
        Demon,
        Spider,
        Flying,
        Armored,
        Fast,
        Boss,
        MiniBoss
    }
}
