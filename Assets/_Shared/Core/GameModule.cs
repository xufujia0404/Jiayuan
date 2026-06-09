using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Sttop5.Shared.Core
{
    /// <summary>
    /// 游戏模块基类，所有小游戏模块（塔防、消消乐等）继承此类。
    /// 提供模块生命周期管理：加载、激活、停用、卸载。
    /// </summary>
    public abstract class GameModule : MonoBehaviour
    {
        [Header("模块配置")]
        [SerializeField] protected string _moduleId;
        [SerializeField] protected string _displayName;
        [SerializeField] protected string _sceneName;

        /// <summary>模块唯一标识（如 "towerdefense", "match3"）</summary>
        public string ModuleId => _moduleId;

        /// <summary>模块显示名称（如 "塔防", "消消乐"）</summary>
        public string DisplayName => _displayName;

        /// <summary>模块对应场景名</summary>
        public string SceneName => _sceneName;

        /// <summary>模块是否已激活</summary>
        public bool IsActive { get; private set; }

        /// <summary>模块专属事件总线</summary>
        public EventBus EventBus { get; private set; } = new EventBus();

        /// <summary>模块状态变化回调</summary>
        public event Action<GameModule, bool> OnModuleActiveChanged;

        /// <summary>
        /// 模块激活时调用，执行初始化逻辑。
        /// </summary>
        public virtual void Activate()
        {
            IsActive = true;
            OnModuleActiveChanged?.Invoke(this, true);
            Debug.Log($"[GameModule] 模块已激活: {_displayName} ({_moduleId})");
        }

        /// <summary>
        /// 模块停用时调用，执行清理逻辑。
        /// </summary>
        public virtual void Deactivate()
        {
            IsActive = false;
            EventBus.Clear();
            OnModuleActiveChanged?.Invoke(this, false);
            Debug.Log($"[GameModule] 模块已停用: {_displayName} ({_moduleId})");
        }

        /// <summary>
        /// 获取模块存档数据，由 SaveSystem 调用。
        /// </summary>
        public abstract string GetModuleSaveData();

        /// <summary>
        /// 从存档数据恢复模块状态。
        /// </summary>
        public abstract void RestoreModuleSaveData(string jsonData);
    }
}
