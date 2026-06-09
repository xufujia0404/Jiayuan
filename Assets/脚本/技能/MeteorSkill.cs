using UnityEngine;
using TowerDefense.Enemy;

namespace TowerDefense.Skill
{
    [CreateAssetMenu(fileName = "MeteorSkill", menuName = "TowerDefense/Skills/Meteor")]
    public class MeteorSkill : SkillBase
    {
        [Header("Meteor Settings")]
        public float baseDamage = 100f;
        public float damagePerLevel = 20f;
        public float stunDuration = 1f;
        public int meteorCount = 3;

        protected override void Execute(GameObject user, Vector3 targetPosition)
        {
            float totalDamage = baseDamage + (damagePerLevel * (_currentLevel - 1));

            var enemies = GetEnemiesInRadius(targetPosition, radius);

            foreach (var enemy in enemies)
            {
                if (enemy != null && !enemy.IsDead)
                {
                    // 在第25行修复
                   enemy.TakeDamage(Mathf.RoundToInt(totalDamage), DamageType.Explosion);

                    if (stunDuration > 0)
                    {
                        enemy.ApplyStun(stunDuration);
                    }
                }
            }
        }
    }
}
