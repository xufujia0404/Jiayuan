using UnityEngine;
using TowerDefense.Data;
using TowerDefense.Map;

namespace TowerDefense.Core
{
    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        GameOver,
        Victory
    }

    public class GameManager : Singleton<GameManager>
    {
        [Header("Level Config")]
        [SerializeField] private LevelData[] _levels;
        [SerializeField] private LevelBuilder _levelBuilder;

        [Header("Game Settings")]
        [SerializeField] private int _initialGold = 500;
        [SerializeField] private int _initialLife = 20;
        [SerializeField] private float _gameSpeed = 1f;

        [Header("Debug")]
        [SerializeField] private bool _debugMode = false;
        [Tooltip("直接从第 N 关开始（0=第一关, 1=第二关...）。优化调试用，上线前改回 0")]
        [SerializeField] private int _debugStartLevel = 0;

        private LevelData _currentLevelData;
        private int _currentLevelIndex = -1;

        private GameState _currentState = GameState.Menu;
        private int _currentGold;
        private int _currentLife;
        private int _currentWave = 0;
        private int _totalWaves = 0;
        private int _enemiesKilled = 0;
        private float _gameTime = 0f;

        /// <summary>是否以模块模式运行</summary>
        private bool _isModuleMode;

        public GameState CurrentState => _currentState;
        public int CurrentGold => _currentGold;
        public int CurrentLife => _currentLife;
        public int CurrentWave => _currentWave;
        public int TotalWaves => _totalWaves;
        public int EnemiesKilled => _enemiesKilled;
        public float GameTime => _gameTime;
        public float GameSpeed => _gameSpeed;
        public bool IsPlaying => _currentState == GameState.Playing;
        public bool IsModuleMode => _isModuleMode;

        public System.Action<GameState> OnStateChanged;
        public System.Action<int> OnGoldChanged;
        public System.Action<int> OnLifeChanged;
        public System.Action<int> OnWaveChanged;

        protected override void Awake()
        {
            // 检测模块模式：通过场景名或全局标志判断
            _isModuleMode = ModuleModeDetector.IsModuleMode;

            if (_isModuleMode)
            {
                // 模块模式下不使用 DontDestroyOnLoad
                // 单例实例存在但不跨场景持久化
                // 使用 base.Instance 静态属性检测重复实例
                var existingInstance = FindObjectOfType<GameManager>();
                if (existingInstance != null && existingInstance != this)
                {
                    Destroy(gameObject);
                    return;
                }
                // 不调用 base.Awake()，避免 DontDestroyOnLoad
            }
            else
            {
                base.Awake();
            }

            InitializeGame();
        }

        public LevelData CurrentLevelData => _currentLevelData;
        public LevelData[] Levels => _levels;
        public int CurrentLevelIndex => _currentLevelIndex;

        private void Start()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "GameScene" || sceneName == "TDGameScene")
            {
                int startLevel = Mathf.Clamp(_debugStartLevel, 0, _levels != null ? _levels.Length - 1 : 0);
                LoadLevel(startLevel);
            }
        }

        public void LoadLevel(int levelIndex)
        {
            if (_levels == null || levelIndex < 0 || levelIndex >= _levels.Length)
            {
                Debug.LogError($"Invalid level index: {levelIndex}");
                return;
            }

            _currentLevelIndex = levelIndex;
            _currentLevelData = _levels[levelIndex];
            _initialGold = _currentLevelData.initialGold;
            _initialLife = _currentLevelData.initialLife;
            InitializeGame();

            OnGoldChanged?.Invoke(_currentGold);
            EventBus.Publish(new GoldChangedEvent { CurrentGold = _currentGold, Change = 0 });
            OnLifeChanged?.Invoke(_currentLife);

            if (_levelBuilder != null)
            {
                _levelBuilder.BuildLevel(_currentLevelData);
            }

            StartGame(_currentLevelData.waves.Count > 0 ? _currentLevelData.waves.Count : _currentLevelData.waveCount);
        }

        private void InitializeGame()
        {
            _currentGold = _initialGold;
            _currentLife = _initialLife;
            _currentState = GameState.Menu;
            _currentWave = 0;
            _enemiesKilled = 0;
            _gameTime = 0f;

            if (Wave.WaveManager.Instance != null)
            {
                Wave.WaveManager.Instance.Reset();
            }
        }

        private void Update()
        {
            if (_currentState == GameState.Playing)
            {
                _gameTime += Time.deltaTime * _gameSpeed;
            }

            if (_debugMode)
            {
                HandleDebugInput();
            }
        }

        public void StartGame(int totalWaves)
        {
            _totalWaves = totalWaves;
            _currentState = GameState.Playing;
            _gameTime = 0f;
            Time.timeScale = _gameSpeed;
            OnStateChanged?.Invoke(_currentState);
            EventBus.Publish(new GameStartEvent());
        }

        public void PauseGame()
        {
            if (_currentState == GameState.Playing)
            {
                _currentState = GameState.Paused;
                Time.timeScale = 0f;
                OnStateChanged?.Invoke(_currentState);
                EventBus.Publish(new GamePauseEvent { IsPaused = true });
            }
        }

        public void ResumeGame()
        {
            if (_currentState == GameState.Paused)
            {
                _currentState = GameState.Playing;
                Time.timeScale = _gameSpeed;
                OnStateChanged?.Invoke(_currentState);
                EventBus.Publish(new GamePauseEvent { IsPaused = false });
            }
        }

        public void TogglePause()
        {
            if (_currentState == GameState.Playing)
            {
                PauseGame();
            }
            else if (_currentState == GameState.Paused)
            {
                ResumeGame();
            }
        }

        public void GameOver(bool isVictory)
        {
            _currentState = isVictory ? GameState.Victory : GameState.GameOver;
            Time.timeScale = 0f;

            // 持久化玩家等级数据
            Data.PlayerLevelSystem.Instance?.PersistToSave();

            OnStateChanged?.Invoke(_currentState);
            EventBus.Publish(new GameOverEvent { IsVictory = isVictory });

            if (isVictory)
            {
                Data.PlayerLevelSystem.Instance?.AddExp(50 + _currentWave * 10);

                // Add gold reward to PlayerWallet (TD-specific gold)
                var wallet = Data.PlayerWallet.Instance;
                if (wallet != null)
                {
                    int goldReward = _currentWave * 20;
                    wallet.AddGold(goldReward);
                }
            }
            Data.PlayerLevelSystem.Instance?.PersistToSave();

            // 模块模式下发布 MiniGameCompletedEvent
            if (_isModuleMode)
            {
                Sttop5.Shared.Core.GlobalEventBus.Publish(new Sttop5.Shared.Core.MiniGameCompletedEvent
                {
                    ModuleId = "towerdefense",
                    IsVictory = isVictory,
                    StarsEarned = isVictory ? CalculateStars() : 0,
                    GoldReward = isVictory ? _currentWave * 20 : 0,
                    ExpReward = isVictory ? 50 + _currentWave * 10 : 0
                });
            }
        }

        private int CalculateStars()
        {
            float lifeRatio = (float)_currentLife / _initialLife;
            if (lifeRatio >= 0.7f) return 3;
            if (lifeRatio >= 0.4f) return 2;
            return 1;
        }

        public void OnVictory()
        {
            GameOver(true);
        }

        public void RestartGame()
        {
            InitializeGame();
            Time.timeScale = 1f;

            if (_isModuleMode)
            {
                // 模块模式下重载 TDGameScene
                var sceneLoader = Sttop5.Shared.Core.SceneLoader.Instance;
                if (sceneLoader != null)
                {
                    sceneLoader.UnloadScene("TDGameScene", () =>
                    {
                        sceneLoader.LoadSceneAdditive("TDGameScene");
                    });
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                    );
                }
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                );
            }
        }

        /// <summary>
        /// 返回家园（模块模式下使用）。
        /// </summary>
        public void ReturnToHome()
        {
            if (_isModuleMode)
            {
                Time.timeScale = 1f;
                Sttop5.Shared.Core.ModuleManager.Instance.DeactivateCurrentModule();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
        }

        #region Gold Management

        public bool HasEnoughGold(int amount)
        {
            return _currentGold >= amount;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            _currentGold += amount;
            OnGoldChanged?.Invoke(_currentGold);
            EventBus.Publish(new GoldChangedEvent { CurrentGold = _currentGold, Change = amount });
        }

        public bool SpendGold(int amount)
        {
            if (!HasEnoughGold(amount)) return false;
            _currentGold -= amount;
            OnGoldChanged?.Invoke(_currentGold);
            EventBus.Publish(new GoldChangedEvent { CurrentGold = _currentGold, Change = -amount });
            return true;
        }

        #endregion

        #region Life Management

        public void TakeDamage(int damage)
        {
            _currentLife -= damage;
            if (_currentLife < 0) _currentLife = 0;
            OnLifeChanged?.Invoke(_currentLife);
            EventBus.Publish(new LifeChangedEvent { CurrentLife = _currentLife, Change = -damage });

            if (_currentLife <= 0)
            {
                GameOver(false);
            }
        }

        public void Heal(int amount)
        {
            _currentLife += amount;
            OnLifeChanged?.Invoke(_currentLife);
            EventBus.Publish(new LifeChangedEvent { CurrentLife = _currentLife, Change = amount });
        }

        #endregion

        #region Wave Management

        public void SetCurrentWave(int wave)
        {
            _currentWave = wave;
            OnWaveChanged?.Invoke(_currentWave);
        }

        public void IncrementWave()
        {
            _currentWave++;
            OnWaveChanged?.Invoke(_currentWave);
        }

        #endregion

        #region Statistics

        public void AddKill()
        {
            _enemiesKilled++;
            Data.PlayerLevelSystem.Instance?.AddExp(5);
        }

        #endregion

        #region Debug

        private void HandleDebugInput()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                AddGold(100);
                Debug.Log($"[Debug] Added 100 gold. Current: {_currentGold}");
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                _currentLife = _initialLife;
                OnLifeChanged?.Invoke(_currentLife);
                Debug.Log($"[Debug] Life restored to {_currentLife}");
            }
            if (Input.GetKeyDown(KeyCode.F3))
            {
                _gameSpeed = _gameSpeed >= 3f ? 1f : _gameSpeed + 0.5f;
                if (_currentState == GameState.Playing)
                {
                    Time.timeScale = _gameSpeed;
                }
                Debug.Log($"[Debug] Game speed: {_gameSpeed}x");
            }
        }

        #endregion
    }
}
