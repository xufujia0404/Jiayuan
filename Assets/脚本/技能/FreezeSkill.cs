using UnityEngine;
using TowerDefense.Enemy;

namespace TowerDefense.Skill
{
    [CreateAssetMenu(fileName = "FreezeSkill", menuName = "TowerDefense/Skills/Freeze")]
    public class FreezeSkill : SkillBase
    {
        [Header("Freeze Settings")]
        public float slowAmount = 0.5f;
        public float baseDuration = 3f;
        public float durationPerLevel = 0.5f;

        protected override void Execute(GameObject user, Vector3 targetPosition)
        {
            float duration = baseDuration + (durationPerLevel * (_currentLevel - 1));

            var enemies = GetEnemiesInRadius(targetPosition, radius);

            foreach (var enemy in enemies)
            {
                if (enemy != null && !enemy.IsDead)
                {
                    enemy.ApplySlow(slowAmount, duration);
                }
            }
        }
    }
}
