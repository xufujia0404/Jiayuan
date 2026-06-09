using UnityEngine;
using TowerDefense.Core;

namespace TowerDefense.Data
{
    public class PlayerWallet : Singleton<PlayerWallet>
    {
        [Header("Initial Values")]
        [SerializeField] private int _initialGold = 1000;
        [SerializeField] private int _initialDiamond = 100;
        [SerializeField] private int _initialStamina = 60;

        /// <summary>塔防金币与家园金币的兑换比率：100 塔防金币 = 1 家园金币</summary>
        public const int EXCHANGE_RATE = 100;

        private int _gold;
        private int _diamond;
        private int _stamina;

        /// <summary>是否代理钻石/体力到共享 PlayerProfile（金币始终独立管理）</summary>
        private bool _isProxyMode;

        /// <summary>金币始终独立管理（不代理到 PlayerProfile）</summary>
        public int Gold => _gold;
        /// <summary>钻石在模块模式下代理到 PlayerProfile</summary>
        public int Diamond => _isProxyMode ? GetProxyDiamond() : _diamond;
        /// <summary>体力在模块模式下代理到 PlayerProfile</summary>
        public int Stamina => _isProxyMode ? GetProxyStamina() : _stamina;

        /// <summary>是否可以与家园金币兑换（需要 PlayerProfile 可用）</summary>
        public bool CanExchange => _profile != null;

        public event System.Action<int> OnGoldChanged;
        public event System.Action<int> OnDiamondChanged;
        public event System.Action<int> OnStaminaChanged;

        private Sttop5.Shared.Player.PlayerProfile _profile;

        private const string WALLET_SAVE_KEY = "towerdefense_wallet";

        [System.Serializable]
        private class WalletSaveData
        {
            public int gold;
        }

        protected override void Awake()
        {
            base.Awake();

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "TDGameScene" || ModuleModeDetector.IsModuleMode)
            {
                EnableProxyMode();
            }
            else
            {
                _gold = _initialGold;
                _diamond = _initialDiamond;
                _stamina = _initialStamina;
            }
        }

        /// <summary>
        /// 启用代理模式，将钻石/体力操作转发到共享 PlayerProfile，金币独立管理。
        /// </summary>
        public void EnableProxyMode()
        {
            _profile = Sttop5.Shared.Player.PlayerProfile.Instance;
            if (_profile != null)
            {
                _isProxyMode = true;
                // 钻石和体力代理到 PlayerProfile
                _diamond = _profile.Diamond;
                _stamina = _profile.Stamina;

                _profile.OnDiamondChanged += OnProxyDiamondChanged;
                _profile.OnStaminaChanged += OnProxyStaminaChanged;

                // 金币独立管理，从存档加载
                LoadWalletData();

                Debug.Log($"[PlayerWallet] 代理模式已启用，金币独立管理，当前金币: {_gold}");
            }
            else
            {
                _gold = _initialGold;
                _diamond = _initialDiamond;
                _stamina = _initialStamina;
                Debug.LogWarning("[PlayerWallet] PlayerProfile 不可用，使用独立模式");
            }
        }

        /// <summary>
        /// 禁用代理模式，断开与 PlayerProfile 的连接。
        /// </summary>
        public void DisableProxyMode()
        {
            if (_profile != null)
            {
                _profile.OnDiamondChanged -= OnProxyDiamondChanged;
                _profile.OnStaminaChanged -= OnProxyStaminaChanged;
            }
            _isProxyMode = false;
            _profile = null;
        }

        #region Proxy Helpers

        private int GetProxyDiamond() => _profile != null ? _profile.Diamond : _diamond;
        private int GetProxyStamina() => _profile != null ? _profile.Stamina : _stamina;

        private void OnProxyDiamondChanged(int diamond)
        {
            _diamond = diamond;
            OnDiamondChanged?.Invoke(diamond);
        }

        private void OnProxyStaminaChanged(int stamina)
        {
            _stamina = stamina;
            OnStaminaChanged?.Invoke(stamina);
        }

        #endregion

        #region Gold (独立管理)

        public bool HasEnoughGold(int amount) => _gold >= amount;

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            _gold += amount;
            OnGoldChanged?.Invoke(_gold);
            EventBus.Publish(new GoldChangedEvent { CurrentGold = _gold, Change = amount });
        }

        public bool SpendGold(int amount)
        {
            if (!HasEnoughGold(amount)) return false;
            _gold -= amount;
            OnGoldChanged?.Invoke(_gold);
            EventBus.Publish(new GoldChangedEvent { CurrentGold = _gold, Change = -amount });
            return true;
        }

        /// <summary>
        /// 将塔防金币兑换为家园金币（100:1）。
        /// </summary>
        /// <param name="tdGoldAmount">要兑换的塔防金币数量，必须为 EXCHANGE_RATE 的倍数</param>
        /// <returns>是否兑换成功</returns>
        public bool ConvertGoldToHome(int tdGoldAmount)
        {
            if (!CanExchange) return false;
            if (tdGoldAmount <= 0 || tdGoldAmount % EXCHANGE_RATE != 0) return false;
            if (_gold < tdGoldAmount) return false;

            int homeGold = tdGoldAmount / EXCHANGE_RATE;
            _gold -= tdGoldAmount;
            _profile.AddGold(homeGold, "towerdefense_exchange");

            OnGoldChanged?.Invoke(_gold);
            EventBus.Publish(new GoldChangedEvent { CurrentGold = _gold, Change = -tdGoldAmount });
            Debug.Log($"[PlayerWallet] 兑换: {tdGoldAmount} 塔防金币 → {homeGold} 家园金币");
            return true;
        }

        /// <summary>
        /// 将家园金币兑换为塔防金币（1:100）。
        /// </summary>
        /// <param name="homeGoldAmount">要兑换的家园金币数量</param>
        /// <returns>是否兑换成功</returns>
        public bool ConvertGoldFromHome(int homeGoldAmount)
        {
            if (!CanExchange) return false;
            if (homeGoldAmount <= 0) return false;
            if (!_profile.HasEnoughGold(homeGoldAmount)) return false;

            if (!_profile.SpendGold(homeGoldAmount, "towerdefense_exchange")) return false;

            int tdGold = homeGoldAmount * EXCHANGE_RATE;
            _gold += tdGold;

            OnGoldChanged?.Invoke(_gold);
            EventBus.Publish(new GoldChangedEvent { CurrentGold = _gold, Change = tdGold });
            Debug.Log($"[PlayerWallet] 兑换: {homeGoldAmount} 家园金币 → {tdGold} 塔防金币");
            return true;
        }

        #endregion

        #region Diamond (代理/独立)

        public bool HasEnoughDiamond(int amount) => Diamond >= amount;

        public void AddDiamond(int amount)
        {
            if (amount <= 0) return;

            if (_isProxyMode && _profile != null)
            {
                _profile.AddDiamond(amount, "towerdefense");
                return;
            }

            _diamond += amount;
            OnDiamondChanged?.Invoke(_diamond);
        }

        public bool SpendDiamond(int amount)
        {
            if (!HasEnoughDiamond(amount)) return false;

            if (_isProxyMode && _profile != null)
            {
                return _profile.SpendDiamond(amount, "towerdefense");
            }

            _diamond -= amount;
            OnDiamondChanged?.Invoke(_diamond);
            return true;
        }

        #endregion

        #region Stamina (代理/独立)

        public bool HasEnoughStamina(int amount) => Stamina >= amount;

        public void AddStamina(int amount)
        {
            if (amount <= 0) return;

            if (_isProxyMode && _profile != null)
            {
                _profile.AddStamina(amount);
                return;
            }

            _stamina += amount;
            OnStaminaChanged?.Invoke(_stamina);
        }

        public bool SpendStamina(int amount)
        {
            if (Stamina < amount) return false;

            if (_isProxyMode && _profile != null)
            {
                return _profile.SpendStamina(amount);
            }

            _stamina -= amount;
            OnStaminaChanged?.Invoke(_stamina);
            return true;
        }

        #endregion

        #region Persistence

        private void SaveWalletData()
        {
            var sharedSave = Sttop5.Shared.Save.SaveSystem.Instance;
            if (sharedSave == null) return;

            var data = new WalletSaveData { gold = _gold };
            sharedSave.SaveModuleData(WALLET_SAVE_KEY, JsonUtility.ToJson(data), 1);
        }

        private void LoadWalletData()
        {
            var sharedSave = Sttop5.Shared.Save.SaveSystem.Instance;
            if (sharedSave == null)
            {
                _gold = _initialGold;
                return;
            }

            string json = sharedSave.LoadModuleData(WALLET_SAVE_KEY);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var data = JsonUtility.FromJson<WalletSaveData>(json);
                    if (data != null)
                    {
                        _gold = data.gold;
                        Debug.Log($"[PlayerWallet] 从存档加载金币: {_gold}");
                        return;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[PlayerWallet] 加载金币存档失败: {e.Message}");
                }
            }
            _gold = _initialGold;
            Debug.Log($"[PlayerWallet] 使用初始金币: {_gold}");
        }

        #endregion

        private new void OnDestroy()
        {
            SaveWalletData();
            DisableProxyMode();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) SaveWalletData();
        }

        private void OnApplicationQuit()
        {
            SaveWalletData();
        }
    }
}
