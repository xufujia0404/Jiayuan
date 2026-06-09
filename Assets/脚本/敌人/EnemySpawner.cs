using UnityEngine;
using System.Collections.Generic;
using TowerDefense.Data;
using TowerDefense.Utils;
using TowerDefense.Wave;

namespace TowerDefense.Enemy
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private PathCreator _pathCreator;
        [SerializeField] private Transform _spawnPoint;
        
        private void Awake()
        {
            if (_pathCreator == null)
            {
                _pathCreator = FindObjectOfType<PathCreator>();
                if (_pathCreator != null)
                    Debug.Log("EnemySpawner: 自动找到 PathCreator - " + _pathCreator.gameObject.name);
                else
                    Debug.LogError("EnemySpawner: 找不到 PathCreator！");
            }
        }
        
        /// <summary>获取当前场景中所有有效的 PathCreator（实时查找，不缓存）</summary>
        private List<PathCreator> GetValidPaths()
        {
            var paths = new List<PathCreator>();
            foreach (var pc in FindObjectsOfType<PathCreator>())
            {
                if (pc != null && pc.WaypointCount > 0)
                    paths.Add(pc);
            }
            return paths;
        }
        
        /// <summary>随机选一条有效路径</summary>
        private PathCreator PickRandomPath()
        {
            var paths = GetValidPaths();
            if (paths.Count == 0)
            {
                // fallback 到序列化引用
                if (_pathCreator != null && _pathCreator.WaypointCount > 0)
                    return _pathCreator;
                return null;
            }
            if (paths.Count == 1) return paths[0];
            return paths[Random.Range(0, paths.Count)];
        }
        
        private Queue<EnemyData> _spawnQueue = new Queue<EnemyData>();
        private float _spawnTimer = 0f;
        private float _spawnInterval = 1f;
        private bool _isSpawning = false;
        private bool _allSpawned = false; // 标记是否所有敌人都已出生
        
        public bool IsSpawning => _isSpawning;
        public static int CurrentWaveId { get; private set; }
        public static int TotalEnemiesInWave { get; private set; }
        
        // 双保险：本波 HashSet 防止重复统计
        private static HashSet<GameObject> _killedEnemiesThisWave = new HashSet<GameObject>();
        
        public static int RemainingEnemies 
        { 
            get 
            { 
                int remaining = TotalEnemiesInWave - _killedEnemiesThisWave.Count;
                return remaining >= 0 ? remaining : 0;  // 保证非负
            } 
        }
        
        private void Update()
        {
            if (_isSpawning && _spawnQueue.Count > 0)
            {
                _spawnTimer += Time.deltaTime;
                
                if (_spawnTimer >= _spawnInterval)
                {
                    _spawnTimer = 0f;
                    SpawnEnemy();
                }
            }
            
            // 所有敌人都已出生，且全部死亡/到达终点，才通知波次完成
            if (_allSpawned && RemainingEnemies <= 0)
            {
                _isSpawning = false;
                _allSpawned = false;
                WaveManager.Instance?.OnWaveComplete();
            }
        }
        
        public static void EnemyBorn(GameObject enemy)
        {
            // 敌人出生时注册波次ID
            enemy.GetComponent<Enemy>()?.SetBirthWaveId(CurrentWaveId);
        }
        
        public static void OnEnemyKilled(GameObject enemy, int enemyWaveId)
        {
            // ✅ 双保险: HashSet去重 + 波次ID相等才统计
            if (!_killedEnemiesThisWave.Contains(enemy) && enemyWaveId == CurrentWaveId)
            {
                _killedEnemiesThisWave.Add(enemy);
                Debug.Log($"✅ 波次{CurrentWaveId}敌人死亡: {_killedEnemiesThisWave.Count}/{TotalEnemiesInWave}, 剩余: {RemainingEnemies}");
            }
        }
        
        public void StartWave(WaveData waveData)
        {
            _spawnQueue.Clear();
            
            // 波次递进，给新的ID
            CurrentWaveId++;
            TotalEnemiesInWave = 0;
            
            // ✅ 新波次: 清空死亡集合，防止死循环统计
            _killedEnemiesThisWave.Clear();
            
            foreach (var enemyGroup in waveData.enemies)
            {
                for (int i = 0; i < enemyGroup.count; i++)
                {
                    _spawnQueue.Enqueue(enemyGroup.enemyData);
                    TotalEnemiesInWave++;
                }
            }
            
            Debug.Log($"🌊 波次{CurrentWaveId}开始: 准备出生 {TotalEnemiesInWave} 个敌人");
            
            _spawnInterval = waveData.spawnInterval;
            _isSpawning = true;
            _allSpawned = false;
            _spawnTimer = 0f;
            
            Debug.Log($"Wave started! Enemies to spawn: {_spawnQueue.Count}");
        }
        
        private void SpawnEnemy()
        {
            if (_spawnQueue.Count == 0)
            {
                _allSpawned = true;
                return;
            }
            
            EnemyData enemyData = _spawnQueue.Dequeue();
            
            // 队列空了，标记所有敌人都已出生
            if (_spawnQueue.Count == 0)
            {
                _allSpawned = true;
            }
            
            if (enemyData == null || enemyData.enemyPrefab == null)
            {
                SpawnEnemy();
                return;
            }
            
            // 实时获取有效路径，不再依赖缓存
            var path = PickRandomPath();
            if (path == null)
            {
                Debug.LogError("EnemySpawner: 没有有效的 PathCreator，敌人生成失败！");
                return;
            }

            Vector3 spawnPos = path.WaypointCount > 0 ? path.GetWaypoint(0) : transform.position;
            
            GameObject enemyObj = Instantiate(enemyData.enemyPrefab, spawnPos, Quaternion.identity);
            Enemy enemy = enemyObj.GetComponent<Enemy>();
            
            if (enemy != null)
            {
                enemy.SetBirthWaveId(CurrentWaveId);
                enemy.Initialize(enemyData);
                enemy.StartMovement(path);
            }
            
            Debug.Log($"Spawned enemy: {enemyData.enemyName}, 波次ID={CurrentWaveId}");
        }
        
        public void StopSpawning()
        {
            _isSpawning = false;
            _spawnQueue.Clear();
        }
        
        public Enemy GetClosestEnemy(Vector3 position, float maxDistance)
        {
            Enemy[] enemies = FindObjectsOfType<Enemy>();
            Enemy closestEnemy = null;
            float closestDistance = float.MaxValue;
            
            foreach (var enemy in enemies)
            {
                float distance = Vector3.Distance(position, enemy.transform.position);
                if (distance <= maxDistance && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = enemy;
                }
            }
            
            return closestEnemy;
        }
        
        public List<Enemy> GetEnemiesInRange(Vector3 position, float range)
        {
            List<Enemy> enemiesInRange = new List<Enemy>();
            Enemy[] enemies = FindObjectsOfType<Enemy>();
            
            foreach (var enemy in enemies)
            {
                float distance = Vector3.Distance(position, enemy.transform.position);
                if (distance <= range)
                {
                    enemiesInRange.Add(enemy);
                }
            }
            
            return enemiesInRange;
        }
        
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Vector3 spawnPos = _spawnPoint != null ? _spawnPoint.position : transform.position;
            Gizmos.DrawSphere(spawnPos, 0.3f);
        }
    }
}