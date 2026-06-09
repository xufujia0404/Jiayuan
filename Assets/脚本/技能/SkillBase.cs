using System.Collections.Generic;
using UnityEngine;
using TowerDefense.Core;
using TowerDefense.Enemy;

namespace TowerDefense.Skill
{
    public enum SkillTargetType
    {
        Self,
        Enemy,
        Area,
        Direction
    }

    public abstract class SkillBase : ScriptableObject
    {
        [Header("Skill Info")]
        public string skillName;
        public string description;
        public Sprite icon;
        public int maxLevel = 5;

        [Header("Cooldown & Cost")]
        public float cooldown = 10f;
        public int manaCost = 0;
        public int goldCost = 0;

        [Header("Targeting")]
        public SkillTargetType targetType = SkillTargetType.Area;
        public float range = 5f;
        public float radius = 2f;

        [Header("Effects")]
        public GameObject effectPrefab;
        public AudioClip soundEffect;

        protected int _currentLevel = 1;

        public int CurrentLevel => _currentLevel;

        public virtual bool CanUse(GameObject user)
        {
            if (goldCost > 0 && !GameManager.Instance.HasEnoughGold(goldCost))
            {
                return false;
            }
            return true;
        }

        public virtual void Use(GameObject user, Vector3 targetPosition)
        {
            if (!CanUse(user)) return;

            if (goldCost > 0)
            {
                GameManager.Instance.SpendGold(goldCost);
            }

            Execute(user, targetPosition);
            PlayEffects(targetPosition);

            EventBus.Publish(new SkillUsedEvent { SkillName = skillName });
        }

        protected abstract void Execute(GameObject user, Vector3 targetPosition);

        protected virtual void PlayEffects(Vector3 position)
        {
            if (effectPrefab != null)
            {
                Instantiate(effectPrefab, position, Quaternion.identity);
            }

            if (soundEffect != null)
            {
                AudioSource.PlayClipAtPoint(soundEffect, position);
            }
        }

        public virtual void Upgrade()
        {
            if (_currentLevel < maxLevel)
            {
                _currentLevel++;
            }
        }

        protected List<Enemy.Enemy> GetEnemiesInRadius(Vector3 center, float radius)
        {
            EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
            if (spawner == null) return new List<Enemy.Enemy>();

            return spawner.GetEnemiesInRange(center, radius);
        }
    }
}
