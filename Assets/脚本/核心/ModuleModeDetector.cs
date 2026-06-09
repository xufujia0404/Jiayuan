using UnityEngine;

namespace TowerDefense.Core
{
    /// <summary>
    /// 模块模式检测器，提供全局静态标志来判断塔防是否以模块模式运行。
    /// 由 TowerDefenseGameModule 在激活时设置。
    /// </summary>
    public static class ModuleModeDetector
    {
        /// <summary>
        /// 塔防是否以模块模式运行（通过家园传送门进入，而非独立启动）。
        /// </summary>
        public static bool IsModuleMode { get; set; } = false;

        /// <summary>
        /// 检测当前是否在模块模式场景中运行。
        /// </summary>
        public static bool DetectFromScene()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return sceneName == "TDGameScene";
        }
    }
}
