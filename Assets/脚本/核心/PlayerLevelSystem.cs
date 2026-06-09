using UnityEngine;
using TowerDefense.Core;
using TowerDefense.Save;

namespace TowerDefense.Data
{
    public class PlayerLevelSystem : Singleton<PlayerLevelSystem>
    {
        [Header("等级配置")]
        [SerializeField] private int _maxLevel = 99;
        [SerializeField] private int _baseExp = 100;
        [SerializeField] private int _expIncrement = 50;

        private int _level = 1;
        private int _currentExp = 0;

        /// <summary>是否代理到共享 PlayerProfile</summary>
        private bool _isProxyMode;

        private Sttop5.Shared.Player.PlayerProfile _profile;

        public int Level => _isProxyMode ? GetProxyLevel() : _level;
        public int CurrentExp => _isProxyMode ? GetProxyExp() : _currentExp;
        public int MaxLevel => _maxLevel;
        public int ExpToNextLevel => _isProxyMode ? GetProxyExpToNext() : GetExpRequiredForLevel(_level);
        public float ExpProgress => Level >= _maxLevel ? 1f : (float)CurrentExp / ExpToNextLevel;
        public bool IsMaxLevel => Level >= _maxLevel;

        public event System.Action<int> OnLevelChanged;
        public event System.Action<int, int> OnExpChanged;

        private bool _loadedFromSave = false;

        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {
            // 检测模块模式（通过场景名或全局标志判断）
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "TDGameScene" || ModuleModeDetector.IsModuleMode)
            {
                EnableProxyMode();
            }
            else
            {
                LoadFromSave();
            }
        }

        /// <summary>
        /// 启用代理模式，将等级/经验操作转发到共享 PlayerProfile。
        /// </summary>
        public void EnableProxyMode()
        {
            _profile = Sttop5.Shared.Player.PlayerProfile.Instance;
            if (_profile != null)
            {
                _isProxyMode = true;
                _level = _profile.Level;
                _currentExp = _profile.CurrentExp;

                _profile.OnLevelChanged += OnProxyLevelChanged;
                _profile.OnExpChanged += OnProxyExpChanged;

                Debug.Log($"[PlayerLevelSystem] 代理模式已启用: Lv.{_level}");
            }
            else
            {
                LoadFromSave();
                Debug.LogWarning("[PlayerLevelSystem] PlayerProfile 不可用，使用独立模式");
            }
        }

        /// <summary>
        /// 禁用代理模式。
        /// </summary>
        public void DisableProxyMode()
        {
            if (_profile != null)
            {
                _profile.OnLevelChanged -= OnProxyLevelChanged;
                _profile.OnExpChanged -= OnProxyExpChanged;
            }
            _isProxyMode = false;
            _profile = null;
        }

        #region Proxy Helpers

        private int GetProxyLevel() => _profile != null ? _profile.Level : _level;
        private int GetProxyExp() => _profile != null ? _profile.CurrentExp : _currentExp;
        private int GetProxyExpToNext() => _profile != null ? _profile.ExpToNextLevel : GetExpRequiredForLevel(_level);

        private void OnProxyLevelChanged(int level)
        {
            _level = level;
            OnLevelChanged?.Invoke(level);
        }

        private void OnProxyExpChanged(int currentExp, int expToNext)
        {
            _currentExp = currentExp;
            OnExpChanged?.Invoke(currentExp, expToNext);
            EventBus.Publish(new ExpChangedEvent { Level = _level, CurrentExp = currentExp, ExpToNext = expToNext });
        }

        #endregion

        public int GetExpRequiredForLevel(int level)
        {
            if (level >= _maxLevel) return int.MaxValue;
            return _baseExp + (level - 1) * _expIncrement;
        }

        public void AddExp(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return;

            if (_isProxyMode && _profile != null)
            {
                _profile.AddExp(amount);
                return;
            }

            if (!_loadedFromSave)
            {
                LoadFromSave();
            }

            int oldLevel = _level;
            _currentExp += amount;

            while (_currentExp >= ExpToNextLevel && !IsMaxLevel)
            {
                _currentExp -= ExpToNextLevel;
                _level++;
            }

            if (IsMaxLevel)
                _currentExp = 0;

            OnExpChanged?.Invoke(_currentExp, ExpToNextLevel);
            EventBus.Publish(new ExpChangedEvent { Level = _level, CurrentExp = _currentExp, ExpToNext = ExpToNextLevel });

            if (_level != oldLevel)
            {
                OnLevelChanged?.Invoke(_level);
                EventBus.Publish(new LevelUpEvent { NewLevel = _level, OldLevel = oldLevel });
                Debug.Log($"[PlayerLevelSystem] 升级! Lv.{oldLevel} → Lv.{_level}");
            }
        }

        public void SetLevelData(int level, int exp)
        {
            _level = Mathf.Clamp(level, 1, _maxLevel);
            _currentExp = IsMaxLevel ? 0 : Mathf.Clamp(exp, 0, ExpToNextLevel);
            OnLevelChanged?.Invoke(_level);
            OnExpChanged?.Invoke(_currentExp, ExpToNextLevel);
        }

        private void LoadFromSave()
        {
            var save = SaveSystem.Instance;
            if (save != null && save.CurrentSave != null)
            {
                var player = save.CurrentSave.player;
                SetLevelData(player.playerLevel, player.playerExp);
                _loadedFromSave = true;
                Debug.Log($"[PlayerLevelSystem] 从存档加载: Lv.{_level}, Exp={_currentExp}");
            }
            else
            {
                Debug.LogWarning("[PlayerLevelSystem] SaveSystem 未就绪，将在首次 AddExp 时重试加载");
            }
        }

        public void PersistToSave()
        {
            if (_isProxyMode)
            {
                // 代理模式下由 PlayerProfile 自己持久化
                _profile?.PersistToSave();
                return;
            }

            var save = SaveSystem.Instance;
            if (save != null && save.CurrentSave != null)
            {
                save.CurrentSave.player.playerLevel = _level;
                save.CurrentSave.player.playerExp = _currentExp;
                save.SaveGame();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) PersistToSave();
        }

        private void OnApplicationQuit()
        {
            PersistToSave();
        }

        private void OnDisable()
        {
            PersistToSave();
        }

        private new void OnDestroy()
        {
            DisableProxyMode();
        }
    }
}
