using UnityEngine;
using TowerDefense.Core;

namespace TowerDefense.Skill
{
    [CreateAssetMenu(fileName = "HealSkill", menuName = "TowerDefense/Skills/Heal")]
    public class HealSkill : SkillBase
    {
        [Header("Heal Settings")]
        public int healAmount = 5;
        public int healPerLevel = 1;

        protected override void Execute(GameObject user, Vector3 targetPosition)
        {
            int totalHeal = healAmount + (healPerLevel * (_currentLevel - 1));
            GameManager.Instance.Heal(totalHeal);
        }
    }
}
