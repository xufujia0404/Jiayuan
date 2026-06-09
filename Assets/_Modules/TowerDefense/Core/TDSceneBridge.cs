using UnityEngine;
using UnityEngine.SceneManagement;
using Sttop5.Shared.Core;

namespace TowerDefense.Module
{
    /// <summary>
    /// 塔防场景桥接器，处理从家园进入塔防和从塔防返回家园的场景切换。
    /// 挂载在 TDGameScene 中。
    /// </summary>
    public class TDSceneBridge : MonoBehaviour
    {
        [Header("返回家园配置")]
        [SerializeField] private string _homeSceneName = "HomeScene";

        /// <summary>
        /// 从塔防返回家园。由 UI 按钮调用。
        /// </summary>
        public void ReturnToHome()
        {
            Debug.Log("[TDSceneBridge] 返回家园...");

            // 保存塔防数据
            SaveCurrentState();

            // 停用模块（会清理状态并卸载场景）
            ModuleManager.Instance.DeactivateCurrentModule();
        }

        /// <summary>
        /// 重新开始当前塔防关卡。
        /// </summary>
        public void RestartCurrentLevel()
        {
            Debug.Log("[TDSceneBridge] 重新开始关卡");

            var gameManager = Core.GameManager.Instance;
            if (gameManager != null)
            {
                Time.timeScale = 1f;
                gameManager.RestartGame();
            }
        }

        /// <summary>
        /// 进入塔防模块（从家园调用）。
        /// </summary>
        public static void EnterTowerDefense()
        {
            Debug.Log("[TDSceneBridge] 进入塔防模块");
            ModuleManager.Instance.ActivateModule("towerdefense");
        }

        private void SaveCurrentState()
        {
            var module = TowerDefenseGameModule.Instance;
            if (module != null)
            {
                string saveData = module.GetModuleSaveData();
                Sttop5.Shared.Save.SaveSystem.Instance?.SaveModuleData("towerdefense", saveData, 1);
            }
        }
    }
}
