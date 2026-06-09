using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Data
{
    [CreateAssetMenu(fileName = "WaveData", menuName = "TowerDefense/WaveData")]
    public class WaveData : ScriptableObject
    {
        public int waveNumber;
        public float waveDelay = 5f;
        public float spawnInterval = 1f;
        public List<WaveEnemy> enemies = new List<WaveEnemy>();

        [Serializable]
        public struct WaveEnemy
        {
            public EnemyData enemyData;
            public int count;
            public float spawnDelay;
            public string spawnPoint;
        }

        public int TotalEnemies
        {
            get
            {
                int total = 0;
                foreach (var enemy in enemies)
                {
                    total += enemy.count;
                }
                return total;
            }
        }
    }
}
