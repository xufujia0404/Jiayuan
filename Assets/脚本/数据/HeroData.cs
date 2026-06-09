using System;
using UnityEngine;

namespace TowerDefense.Data
{
    [CreateAssetMenu(fileName = "HeroData", menuName = "TowerDefense/HeroData")]
    public class HeroData : ScriptableObject
    {
        public string heroName;
        public string description;
        public Sprite portrait;
        public GameObject heroPrefab;
        public HeroStats baseStats;
        public HeroSkill[] skills;

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
            public Sprite icon;
            public float cooldown;
            public float manaCost;
            public GameObject effectPrefab;
            public float effectDuration;
            public float effectRadius;
            public float effectValue;
        }
    }
}
