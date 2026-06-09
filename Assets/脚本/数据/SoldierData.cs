using UnityEngine;

namespace TowerDefense.Data
{
    [CreateAssetMenu(fileName = "SoldierData", menuName = "TowerDefense/SoldierData")]
    public class SoldierData : ScriptableObject
    {
        [Header("Basic Stats")]
        public string soldierName;
        public GameObject soldierPrefab;
        public Sprite icon;

        [Header("Combat Stats")]
        public int maxHealth = 30;
        public int damage = 5;
        public float attackSpeed = 1f;
        public float attackRange = 1.2f;
        public float moveSpeed = 3f;

        [Header("Behavior")]
        public float engageRange = 3f;
        public float returnSpeed = 4f;
        public float respawnTime = 5f;
    }
}
