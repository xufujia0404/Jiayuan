using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TowerDefense.Data;
using TowerDefense.Enemy;
using TowerDefense.Core;

namespace TowerDefense.Wave
{
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }
        
        [Header("Settings")]
        [SerializeField] private EnemySpawner _enemySpawner;
        [SerializeField] private float _waveDelay = 5f;
        [SerializeField] private int _totalWaves = 5;
        [SerializeField] private int _baseEnemyCount = 5;
        [SerializeField] private int _enemyIncreasePerWave = 2;
        
        [Header("Wave Data (Optional)")]
        [Tooltip("如果设置了这个数组，将使用这里的配置而不是动态生成")]
        [SerializeField] private WaveData[] _waveDatas;
        
        [Header("Enemy Data")]
        [Tooltip("所有可用的敌人数据（飞机、骷髅等）")]
        [SerializeField] private EnemyData[] _enemyDatas;
        
        private EnemyData _defaultEnemyData;
        
        [Header("UI")]
        [SerializeField] private Text _waveText;
        [SerializeField] private Text _enemyCountText;
        [SerializeField] private Text _waveAnnounceText;
        [SerializeField] private float _announceDuration = 2f;
        
        private int _currentWaveIndex = 0;
        private float _waveTimer = 0f;
        private bool _isWaveActive = false;
        private bool _isWaveTransition = false;
        
        public int CurrentWave => _currentWaveIndex;
        public int TotalWaves => _totalWaves;
        public bool IsWaveActive => _isWaveActive;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            LoadEnemyData();
            UpdateWaveUI();
            
            if (_waveAnnounceText != null)
            {
                _waveAnnounceText.gameObject.SetActive(false);
            }
            
            EventBus.Subscribe<GameStartEvent>(OnGameStart);
        }
        
        private void LoadEnemyData()
        {
            // 如果在 Inspector 中设置了敌人数据数组，直接使用
            if (_enemyDatas != null && _enemyDatas.Length > 0)
            {
                Debug.Log($"已加载 {_enemyDatas.Length} 个敌人数据");
                foreach (var enemy in _enemyDatas)
                {
                    Debug.Log($"  - {enemy.enemyName}");
                }
                // 使用第一个作为默认
                _defaultEnemyData = _enemyDatas[0];
                return;
            }
            
            // 否则尝试从 Resources 加载
            _defaultEnemyData = Resources.Load<EnemyData>("Data/EnemyData");
            if (_defaultEnemyData == null)
            {
                Debug.LogError("Failed to load EnemyData from Resources/Data/EnemyData");
            }
        }
        
        private void OnDestroy()
        {
            EventBus.Unsubscribe<GameStartEvent>(OnGameStart);
        }
        
        private void OnGameStart(GameStartEvent e)
        {
            ForceStartWave();
        }
        
        private void Update()
        {
            if (_isWaveTransition && !_isWaveActive)
            {
                _waveTimer += Time.deltaTime;
                
                float remainingTime = _waveDelay - _waveTimer;
                
                if (remainingTime > 0)
                {
                    UpdateWaveCountdown((int)Mathf.CeilToInt(remainingTime));
                }
                
                if (remainingTime % 1f < 0.05f)
                {
                    Debug.Log($"倒计时更新: timer={_waveTimer:F1}/{_waveDelay}, 剩余={remainingTime:F1}s, transition={_isWaveTransition}, active={_isWaveActive}");
                }
                
                if (_waveTimer >= _waveDelay)
                {
                    Debug.Log($"✅ 倒计时结束, 准备开始下一波!");
                    _waveTimer = 0f;
                    HideWaveCountdown();
                    StartNextWave();
                }
            }
            
            UpdateEnemyCountDisplay();
        }
        
        private void UpdateWaveCountdown(int seconds)
        {
            if (_waveAnnounceText != null)
            {
                int nextWave = _currentWaveIndex + 1;
                _waveAnnounceText.text = $"第 {nextWave} 波 {seconds} 秒后出现！";
                _waveAnnounceText.gameObject.SetActive(true);
            }
        }
        
        private void HideWaveCountdown()
        {
            if (_waveAnnounceText != null)
            {
                _waveAnnounceText.gameObject.SetActive(false);
            }
        }
        
        private void UpdateEnemyCountDisplay()
        {
            if (_enemyCountText != null)
            {
                // 用 EnemySpawner 追踪的波次剩余敌人（出生总数 - 死亡数）
                _enemyCountText.text = $"剩余敌人:{TowerDefense.Enemy.EnemySpawner.RemainingEnemies}";
            }
        }
        
        public void StartNextWave()
        {
            if (_currentWaveIndex >= _totalWaves)
            {
                Debug.Log("All waves completed!");
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OnVictory();
                }
                return;
            }
            
            // 先增加波次索引
            _currentWaveIndex++;
            
            WaveData waveData = GetWaveData();
            if (waveData == null)
            {
                Debug.LogError("Failed to get wave data!");
                return;
            }
            
            _enemySpawner.StartWave(waveData);
            
            _isWaveActive = true;
            _isWaveTransition = false;
            
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetCurrentWave(_currentWaveIndex);
            }
            
            EventBus.Publish(new WaveStartEvent { WaveIndex = _currentWaveIndex });
            
            UpdateWaveUI();
            StartCoroutine(ShowWaveAnnouncement(_currentWaveIndex));
            Debug.Log($"Starting wave {_currentWaveIndex}");
        }
        
        private IEnumerator ShowWaveAnnouncement(int waveNumber)
        {
            if (_waveAnnounceText != null)
            {
                _waveAnnounceText.text = $"第 {waveNumber} 波开始出怪！";
                _waveAnnounceText.gameObject.SetActive(true);
                
                Debug.Log($"📢 {_waveAnnounceText.text}");
                
                yield return new WaitForSeconds(_announceDuration);
                
                _waveAnnounceText.gameObject.SetActive(false);
            }
        }
        
        private WaveData GetWaveData()
        {
            // 如果用户设置了 WaveData 数组，使用它
            if (_waveDatas != null && _waveDatas.Length > 0)
            {
                int index = _currentWaveIndex - 1;
                if (index >= 0 && index < _waveDatas.Length)
                {
                    Debug.Log($"使用配置的 WaveData: 波次 {_currentWaveIndex}");
                    return _waveDatas[index];
                }
            }
            
            // 否则动态生成
            if (_defaultEnemyData == null && (_enemyDatas == null || _enemyDatas.Length == 0))
            {
                Debug.LogError("No EnemyData available! Cannot create wave data.");
                return null;
            }
            
            Debug.Log($"动态生成 WaveData: 波次 {_currentWaveIndex}");
            return CreateWaveData();
        }
        
        private WaveData CreateWaveData()
        {
            WaveData waveData = ScriptableObject.CreateInstance<WaveData>();
            waveData.waveNumber = _currentWaveIndex;
            waveData.spawnInterval = Mathf.Max(0.5f, 1.5f - (_currentWaveIndex - 1) * 0.1f);
            waveData.enemies = new List<WaveData.WaveEnemy>();
            
            int totalEnemies = _baseEnemyCount + (_currentWaveIndex - 1) * _enemyIncreasePerWave;
            
            // 使用多个敌人数据
            if (_enemyDatas != null && _enemyDatas.Length > 0)
            {
                // 随机选择敌人
                for (int i = 0; i < _enemyDatas.Length; i++)
                {
                    int enemyCount = totalEnemies / _enemyDatas.Length;
                    if (i == 0)
                    {
                        enemyCount += totalEnemies % _enemyDatas.Length;
                    }
                    
                    if (enemyCount > 0)
                    {
                        waveData.enemies.Add(new WaveData.WaveEnemy
                        {
                            enemyData = _enemyDatas[i],
                            count = enemyCount,
                            spawnDelay = 0,
                            spawnPoint = "MainPath"
                        });
                        
                        Debug.Log($"波次 {_currentWaveIndex} 添加 {enemyCount} 个 {_enemyDatas[i].enemyName}");
                    }
                }
            }
            else
            {
                // 只有一个敌人数据时使用旧方法
                waveData.enemies.Add(new WaveData.WaveEnemy
                {
                    enemyData = _defaultEnemyData,
                    count = totalEnemies,
                    spawnDelay = 0,
                    spawnPoint = "MainPath"
                });
            }
            
            return waveData;
        }
        
        public void OnWaveComplete()
        {
            _isWaveActive = false;
            
            Debug.Log($"波次 {_currentWaveIndex}/{_totalWaves} 完成!");
            EventBus.Publish(new WaveEndEvent { WaveIndex = _currentWaveIndex });
            
            if (_currentWaveIndex >= _totalWaves)
            {
                Debug.Log($"全部波次完成!");
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OnVictory();
                }
            }
            else
            {
                _isWaveTransition = true;
                _waveTimer = 0f;
            }
            
            UpdateWaveUI();
        }
        
        public void StartWaveManually(int waveIndex)
        {
            if (waveIndex >= 0 && waveIndex < _totalWaves)
            {
                _currentWaveIndex = waveIndex;
                StartNextWave();
            }
        }
        
        public void SkipToNextWave()
        {
            if (!_isWaveActive && _currentWaveIndex < _totalWaves)
            {
                StartNextWave();
            }
        }
        
        public void ForceStartWave()
        {
            if (!_isWaveActive)
            {
                _isWaveTransition = false;
                _waveTimer = 0f;
                StartNextWave();
            }
        }
        
        private void UpdateWaveUI()
        {
            if (_waveText != null)
            {
                _waveText.text = $"波次:{_currentWaveIndex}/{_totalWaves}";
            }
            
            UpdateEnemyCountDisplay();
        }
        
        public void Reset()
        {
            _currentWaveIndex = 0;
            _waveTimer = 0f;
            _isWaveActive = false;
            _isWaveTransition = false;
            UpdateWaveUI();
        }
    }
}