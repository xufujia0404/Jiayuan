using UnityEngine;
using Sttop5.Shared.Core;
using Sttop5.Shared.Save;

namespace TowerDefense.Module
{
    /// <summary>
    /// 塔防游戏模块，继承共享 GameModule 基类。
    /// 作为塔防玩法在 HomeBase 架构中的入口。
    /// </summary>
    public class TowerDefenseGameModule : GameModule
    {
        [Header("模块配置")]
        [SerializeField] private string _moduleIdInternal = "towerdefense";
        [SerializeField] private string _displayNameInternal = "塔防";
        [SerializeField] private string _sceneNameInternal = "TDGameScene";

        /// <summary>当前模块实例</summary>
        public static TowerDefenseGameModule Instance { get; private set; }

        private void Reset()
        {
            _moduleId = _moduleIdInternal;
            _displayName = _displayNameInternal;
            _sceneName = _sceneNameInternal;
        }

        private void Awake()
        {
            if (string.IsNullOrEmpty(_moduleId)) _moduleId = _moduleIdInternal;
            if (string.IsNullOrEmpty(_displayName)) _displayName = _displayNameInternal;
            if (string.IsNullOrEmpty(_sceneName)) _sceneName = _sceneNameInternal;

            Instance = this;
        }

        public override void Activate()
        {
            // 设置全局模块模式标志
            Core.ModuleModeDetector.IsModuleMode = true;

            base.Activate();

            // 注册到模块管理器
            ModuleManager.Instance.RegisterModule(this);

            Debug.Log("[TowerDefenseGameModule] 塔防模块已激活（模块模式）");
        }

        public override void Deactivate()
        {
            CleanupTowerDefenseState();

            // 清除全局模块模式标志
            Core.ModuleModeDetector.IsModuleMode = false;

            Instance = null;
            base.Deactivate();

            Debug.Log("[TowerDefenseGameModule] 塔防模块已停用");
        }

        /// <summary>
        /// 清理塔防运行状态，确保回到家园时无残留。
        /// </summary>
        private void CleanupTowerDefenseState()
        {
            // 重置时间缩放
            Time.timeScale = 1f;

            // 清理塔防 EventBus
            Core.EventBus.Clear();

            // 停止所有协程
            StopAllCoroutines();
        }

        #region 存档

        public override string GetModuleSaveData()
        {
            var adapter = new TDSaveAdapter();
            return adapter.CollectSaveData();
        }

        public override void RestoreModuleSaveData(string jsonData)
        {
            var adapter = new TDSaveAdapter();
            adapter.RestoreSaveData(jsonData);
        }

        #endregion
    }
}
