using UnityEngine;
using UnityEngine.UI;
using TowerDefense.Data;

namespace TowerDefense.UI
{
    /// <summary>
    /// 塔防金币与家园金币兑换面板。
    /// 挂载到场景中的 GameObject 上，将 UI 元素在 Inspector 中接线即可。
    /// 兑换比率: 100 塔防金币 = 1 家园金币
    /// </summary>
    public class ExchangePanel : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private Text _tdGoldText;
        [SerializeField] private Text _homeGoldText;
        [SerializeField] private Text _rateText;

        [Header("Input")]
        [SerializeField] private InputField _amountInput;

        [Header("Buttons")]
        [SerializeField] private Button _toHomeButton;
        [SerializeField] private Button _toTdButton;
        [SerializeField] private Button _closeButton;

        private PlayerWallet _wallet;
        private Sttop5.Shared.Player.PlayerProfile _profile;

        private void Awake()
        {
            _wallet = PlayerWallet.Instance;
            _profile = Sttop5.Shared.Player.PlayerProfile.Instance;

            if (_toHomeButton != null)
                _toHomeButton.onClick.AddListener(OnConvertToHome);
            if (_toTdButton != null)
                _toTdButton.onClick.AddListener(OnConvertToTd);
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            if (_toHomeButton != null)
                _toHomeButton.onClick.RemoveListener(OnConvertToHome);
            if (_toTdButton != null)
                _toTdButton.onClick.RemoveListener(OnConvertToTd);
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Hide);

            UnsubscribeEvents();
        }

        private void OnEnable()
        {
            SubscribeEvents();
            UpdateDisplay();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        #region Event Subscription

        private void SubscribeEvents()
        {
            if (_wallet != null)
                _wallet.OnGoldChanged += OnTdGoldChanged;
            if (_profile != null)
                _profile.OnGoldChanged += OnHomeGoldChanged;
        }

        private void UnsubscribeEvents()
        {
            if (_wallet != null)
                _wallet.OnGoldChanged -= OnTdGoldChanged;
            if (_profile != null)
                _profile.OnGoldChanged -= OnHomeGoldChanged;
        }

        private void OnTdGoldChanged(int gold) => UpdateTdGoldDisplay(gold);
        private void OnHomeGoldChanged(int gold) => UpdateHomeGoldDisplay(gold);

        #endregion

        public void Show()
        {
            gameObject.SetActive(true);
            UpdateDisplay();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void UpdateDisplay()
        {
            UpdateTdGoldDisplay(_wallet != null ? _wallet.Gold : 0);
            UpdateHomeGoldDisplay(_profile != null ? _profile.Gold : 0);

            if (_rateText != null)
                _rateText.text = $"兑换比率: {PlayerWallet.EXCHANGE_RATE}:1";
        }

        private void UpdateTdGoldDisplay(int gold)
        {
            if (_tdGoldText != null)
                _tdGoldText.text = $"塔防金币: {gold}";
        }

        private void UpdateHomeGoldDisplay(int gold)
        {
            if (_homeGoldText != null)
                _homeGoldText.text = $"家园金币: {gold}";
        }

        #region Conversion

        /// <summary>塔防金币 → 家园金币</summary>
        public void OnConvertToHome()
        {
            if (_wallet == null || !_wallet.CanExchange) return;

            int amount = GetInputAmount();
            if (amount <= 0) return;

            // 确保是 EXCHANGE_RATE 的整数倍
            amount = (amount / PlayerWallet.EXCHANGE_RATE) * PlayerWallet.EXCHANGE_RATE;
            if (amount <= 0) amount = PlayerWallet.EXCHANGE_RATE;

            if (_wallet.ConvertGoldToHome(amount))
            {
                Debug.Log($"[ExchangePanel] 兑换成功: {amount} 塔防金币 → {amount / PlayerWallet.EXCHANGE_RATE} 家园金币");
            }
            else
            {
                Debug.Log("[ExchangePanel] 兑换失败: 塔防金币不足");
            }
        }

        /// <summary>家园金币 → 塔防金币</summary>
        public void OnConvertToTd()
        {
            if (_wallet == null || !_wallet.CanExchange) return;

            int amount = GetInputAmount();
            if (amount <= 0) return;

            if (_wallet.ConvertGoldFromHome(amount))
            {
                Debug.Log($"[ExchangePanel] 兑换成功: {amount} 家园金币 → {amount * PlayerWallet.EXCHANGE_RATE} 塔防金币");
            }
            else
            {
                Debug.Log("[ExchangePanel] 兑换失败: 家园金币不足");
            }
        }

        private int GetInputAmount()
        {
            if (_amountInput != null && int.TryParse(_amountInput.text, out int amount))
            {
                return amount;
            }
            return 0;
        }

        #endregion
    }
}
