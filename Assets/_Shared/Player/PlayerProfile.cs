using System;
using UnityEngine;
using Sttop5.Shared.Core;
using Sttop5.Shared.Save;

namespace Sttop5.Shared.Player
{
    /// <summary>
    /// 统一玩家档案，管理跨所有模块共享的玩家数据。
    /// 包含货币（金币、钻石）、等级、经验值。
    /// </summary>
    public class PlayerProfile : Singleton<PlayerProfile>
    {
        [Header("初始值")]
        [SerializeField] private int _initialGold = 1000;
        [SerializeField] private int _initialDiamond = 100;
        [SerializeField] private int _initialStamina = 60;

        [Header("等级配置")]
        [SerializeField] private int _maxLevel = 99;
        [SerializeField] private int _baseExp = 100;
        [SerializeField] private int _expIncrement = 50;

        private int _gold;
        private int _diamond;
        private int _stamina;
        private int _level = 1;
        private int _currentExp;

        #region 属性

        public int Gold => _gold;
        public int Diamond => _diamond;
        public int Stamina => _stamina;
        public int Level => _level;
        public int CurrentExp => _currentExp;
        public int MaxLevel => _maxLevel;
        public int ExpToNextLevel => GetExpRequiredForLevel(_level);
        public float ExpProgress => _level >= _maxLevel ? 1f : (float)_currentExp / ExpToNextLevel;
        public bool IsMaxLevel => _level >= _maxLevel;

        #endregion

        #region 事件

        public event Action<int> OnGoldChanged;
        public event Action<int> OnDiamondChanged;
        public event Action<int> OnStaminaChanged;
        public event Action<int> OnLevelChanged;
        public event Action<int, int> OnExpChanged;

        #endregion

        protected override void Awake()
        {
            base.Awake();
            _gold = _initialGold;
            _diamond = _initialDiamond;
            _stamina = _initialStamina;
        }

        private void Start()
        {
            LoadFromSave();
        }

        #region 金币

        public bool HasEnoughGold(int amount) => _gold >= amount;

        public void AddGold(int amount, string source = "")
        {
            if (amount <= 0) return;
            _gold += amount;
            OnGoldChanged?.Invoke(_gold);
            GlobalEventBus.Publish(new PlayerCurrencyChangedEvent
            {
                Source = source,
                GoldDelta = amount,
                DiamondDelta = 0
            });
        }

        public bool SpendGold(int amount, string source = "")
        {
            if (!HasEnoughGold(amount)) return false;
            _gold -= amount;
            OnGoldChanged?.Invoke(_gold);
            GlobalEventBus.Publish(new PlayerCurrencyChangedEvent
            {
                Source = source,
                GoldDelta = -amount,
                DiamondDelta = 0
            });
            return true;
        }

        #endregion

        #region 钻石

        public bool HasEnoughDiamond(int amount) => _diamond >= amount;

        public void AddDiamond(int amount, string source = "")
        {
            if (amount <= 0) return;
            _diamond += amount;
            OnDiamondChanged?.Invoke(_diamond);
            GlobalEventBus.Publish(new PlayerCurrencyChangedEvent
            {
                Source = source,
                GoldDelta = 0,
                DiamondDelta = amount
            });
        }

        public bool SpendDiamond(int amount, string source = "")
        {
            if (!HasEnoughDiamond(amount)) return false;
            _diamond -= amount;
            OnDiamondChanged?.Invoke(_diamond);
            GlobalEventBus.Publish(new PlayerCurrencyChangedEvent
            {
                Source = source,
                GoldDelta = 0,
                DiamondDelta = -amount
            });
            return true;
        }

        #endregion

        #region 体力

        public bool HasEnoughStamina(int amount) => _stamina >= amount;

        public void AddStamina(int amount)
        {
            if (amount <= 0) return;
            _stamina += amount;
            OnStaminaChanged?.Invoke(_stamina);
        }

        public bool SpendStamina(int amount)
        {
            if (!HasEnoughStamina(amount)) return false;
            _stamina -= amount;
            OnStaminaChanged?.Invoke(_stamina);
            return true;
        }

        #endregion

        #region 等级与经验

        public int GetExpRequiredForLevel(int level)
        {
            if (level >= _maxLevel) return int.MaxValue;
            return _baseExp + (level - 1) * _expIncrement;
        }

        public void AddExp(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return;

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
            GlobalEventBus.Publish(new PlayerLevelChangedEvent
            {
                OldLevel = oldLevel,
                NewLevel = _level,
                CurrentExp = _currentExp,
                ExpToNext = ExpToNextLevel
            });

            if (_level != oldLevel)
            {
                OnLevelChanged?.Invoke(_level);
                Debug.Log($"[PlayerProfile] 升级! Lv.{oldLevel} → Lv.{_level}");
            }
        }

        #endregion

        #region 存档

        public void LoadFromSave()
        {
            var saveSystem = SaveSystem.Instance;
            if (saveSystem == null || saveSystem.CurrentSave == null) return;

            var data = saveSystem.CurrentSave.player;
            _gold = data.gold > 0 ? data.gold : _initialGold;
            _diamond = data.diamond > 0 ? data.diamond : _initialDiamond;
            _stamina = data.stamina > 0 ? data.stamina : _initialStamina;
            _level = Mathf.Clamp(data.playerLevel, 1, _maxLevel);
            _currentExp = Mathf.Clamp(data.playerExp, 0, ExpToNextLevel);

            Debug.Log($"[PlayerProfile] 从存档加载: Lv.{_level}, Gold={_gold}, Diamond={_diamond}");
        }

        public void PersistToSave()
        {
            var saveSystem = SaveSystem.Instance;
            if (saveSystem == null || saveSystem.CurrentSave == null) return;

            saveSystem.CurrentSave.player.gold = _gold;
            saveSystem.CurrentSave.player.diamond = _diamond;
            saveSystem.CurrentSave.player.stamina = _stamina;
            saveSystem.CurrentSave.player.playerLevel = _level;
            saveSystem.CurrentSave.player.playerExp = _currentExp;
            saveSystem.SaveGame();
        }

        #endregion

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
    }
}
