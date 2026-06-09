using UnityEngine;
using UnityEngine.UI;
using TowerDefense.Core;

namespace TowerDefense.Module
{
    /// <summary>
    /// 塔防返回家园按钮，仅在模块模式下显示。
    /// 挂载在 TDGameScene 的 Canvas 中。
    /// </summary>
    public class TDReturnButton : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button _returnButton;
        [SerializeField] private GameObject _returnButtonObject;

        [Header("场景桥接")]
        [SerializeField] private TDSceneBridge _sceneBridge;

        private void Start()
        {
            // 仅在模块模式下显示返回按钮
            bool isModuleMode = ModuleModeDetector.IsModuleMode;

            if (_returnButtonObject != null)
            {
                _returnButtonObject.SetActive(isModuleMode);
            }

            if (_returnButton != null && isModuleMode)
            {
                _returnButton.onClick.AddListener(OnReturnClicked);
            }
        }

        private void OnDestroy()
        {
            if (_returnButton != null)
            {
                _returnButton.onClick.RemoveListener(OnReturnClicked);
            }
        }

        private void OnReturnClicked()
        {
            if (_sceneBridge != null)
            {
                _sceneBridge.ReturnToHome();
            }
            else
            {
                Sttop5.Shared.Core.ModuleManager.Instance.DeactivateCurrentModule();
            }
        }
    }
}
