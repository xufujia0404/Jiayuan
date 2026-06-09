using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sttop5.Shared.Core
{
    /// <summary>
    /// 模块管理器，负责注册、激活、停用游戏模块。
    /// 管理从家园进入小游戏的完整生命周期。
    /// </summary>
    public class ModuleManager : Singleton<ModuleManager>
    {
        private readonly Dictionary<string, GameModule> _modules = new Dictionary<string, GameModule>();
        private GameModule _activeModule;

        /// <summary>当前激活的模块</summary>
        public GameModule ActiveModule => _activeModule;

        /// <summary>当前激活的模块ID，无模块时为 "home"</summary>
        public string ActiveModuleId => _activeModule != null ? _activeModule.ModuleId : "home";

        /// <summary>模块切换开始回调</summary>
        public event Action<string, string> OnModuleSwitching;

        /// <summary>模块切换完成回调</summary>
        public event Action<string> OnModuleSwitched;

        /// <summary>
        /// 注册一个游戏模块。
        /// </summary>
        public void RegisterModule(GameModule module)
        {
            if (module == null || string.IsNullOrEmpty(module.ModuleId))
            {
                Debug.LogError("[ModuleManager] 无法注册空模块或无ID的模块");
                return;
            }

            if (_modules.ContainsKey(module.ModuleId))
            {
                Debug.LogWarning($"[ModuleManager] 模块已注册，将被替换: {module.ModuleId}");
            }

            _modules[module.ModuleId] = module;
            Debug.Log($"[ModuleManager] 模块已注册: {module.DisplayName} ({module.ModuleId})");
        }

        /// <summary>
        /// 注销一个游戏模块。
        /// </summary>
        public void UnregisterModule(string moduleId)
        {
            if (_modules.ContainsKey(moduleId))
            {
                if (_activeModule != null && _activeModule.ModuleId == moduleId)
                {
                    DeactivateCurrentModule();
                }
                _modules.Remove(moduleId);
                Debug.Log($"[ModuleManager] 模块已注销: {moduleId}");
            }
        }

        /// <summary>
        /// 模块场景名到模块ID的映射，用于场景加载后自动注册。
        /// </summary>
        private readonly Dictionary<string, string> _sceneToModuleId = new Dictionary<string, string>();

        /// <summary>
        /// 激活指定模块，通过场景加载。
        /// 如果模块尚未注册，先加载场景，让场景中的 GameModule 自行注册后再激活。
        /// </summary>
        public void ActivateModule(string moduleId)
        {
            if (!_modules.TryGetValue(moduleId, out GameModule module))
            {
                // 模块尚未注册，可能场景还未加载
                // 尝试通过已知的场景名加载场景，让模块自行注册
                string sceneName = GetKnownSceneName(moduleId);
                if (!string.IsNullOrEmpty(sceneName))
                {
                    Debug.Log($"[ModuleManager] 模块未注册，先加载场景: {sceneName}");
                    ActivateModuleByScene(moduleId, sceneName);
                    return;
                }

                Debug.LogError($"[ModuleManager] 未找到模块: {moduleId}");
                return;
            }

            if (_activeModule != null && _activeModule.ModuleId == moduleId)
            {
                Debug.LogWarning($"[ModuleManager] 模块已激活: {moduleId}");
                return;
            }

            string fromModule = ActiveModuleId;
            OnModuleSwitching?.Invoke(fromModule, moduleId);

            // 停用当前模块
            if (_activeModule != null)
            {
                _activeModule.Deactivate();
            }

            // 加载模块场景
            if (!string.IsNullOrEmpty(module.SceneName))
            {
                SceneLoader.Instance.LoadSceneAdditive(module.SceneName, () =>
                {
                    module.Activate();
                    _activeModule = module;
                    OnModuleSwitched?.Invoke(moduleId);

                    GlobalEventBus.Publish(new ModuleSwitchEvent
                    {
                        FromModule = fromModule,
                        ToModule = moduleId
                    });
                });
            }
            else
            {
                module.Activate();
                _activeModule = module;
                OnModuleSwitched?.Invoke(moduleId);
            }
        }

        /// <summary>
        /// 通过场景加载激活模块（模块尚未注册时使用）。
        /// 加载场景后等待模块自行注册，然后激活。
        /// </summary>
        private void ActivateModuleByScene(string moduleId, string sceneName)
        {
            string fromModule = ActiveModuleId;
            OnModuleSwitching?.Invoke(fromModule, moduleId);

            // 停用当前模块
            if (_activeModule != null)
            {
                _activeModule.Deactivate();
            }

            SceneLoader.Instance.LoadSceneAdditive(sceneName, () =>
            {
                // 场景加载后，模块应该已经自行注册
                if (_modules.TryGetValue(moduleId, out GameModule module))
                {
                    module.Activate();
                    _activeModule = module;
                    OnModuleSwitched?.Invoke(moduleId);

                    GlobalEventBus.Publish(new ModuleSwitchEvent
                    {
                        FromModule = fromModule,
                        ToModule = moduleId
                    });
                }
                else
                {
                    Debug.LogError($"[ModuleManager] 场景 {sceneName} 加载后未找到模块: {moduleId}");
                }
            });
        }

        /// <summary>
        /// 注册模块的场景映射，用于在模块未注册时通过场景名加载。
        /// </summary>
        public void RegisterModuleScene(string moduleId, string sceneName)
        {
            _sceneToModuleId[sceneName] = moduleId;
        }

        /// <summary>
        /// 根据模块ID获取已知的场景名。
        /// </summary>
        private string GetKnownSceneName(string moduleId)
        {
            foreach (var kvp in _sceneToModuleId)
            {
                if (kvp.Value == moduleId)
                    return kvp.Key;
            }

            // 硬编码已知的模块场景映射
            switch (moduleId)
            {
                case "towerdefense": return "TDGameScene";
                case "match3": return "Match3Scene";
                default: return null;
            }
        }

        /// <summary>
        /// 停用当前模块并返回家园。
        /// </summary>
        public void DeactivateCurrentModule()
        {
            if (_activeModule == null)
            {
                Debug.LogWarning("[ModuleManager] 当前无激活模块");
                return;
            }

            string moduleId = _activeModule.ModuleId;
            string sceneName = _activeModule.SceneName;

            _activeModule.Deactivate();

            if (!string.IsNullOrEmpty(sceneName))
            {
                SceneLoader.Instance.UnloadScene(sceneName);
            }

            _activeModule = null;
            Debug.Log($"[ModuleManager] 模块已停用，返回家园: {moduleId}");
        }

        /// <summary>
        /// 获取已注册的模块。
        /// </summary>
        public GameModule GetModule(string moduleId)
        {
            _modules.TryGetValue(moduleId, out GameModule module);
            return module;
        }

        /// <summary>
        /// 获取所有已注册模块。
        /// </summary>
        public IReadOnlyDictionary<string, GameModule> GetAllModules()
        {
            return _modules;
        }

        /// <summary>
        /// 保存所有模块的存档数据。
        /// </summary>
        public Dictionary<string, string> CollectAllModuleSaveData()
        {
            var result = new Dictionary<string, string>();
            foreach (var kvp in _modules)
            {
                try
                {
                    string data = kvp.Value.GetModuleSaveData();
                    if (!string.IsNullOrEmpty(data))
                    {
                        result[kvp.Key] = data;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ModuleManager] 获取模块存档失败: {kvp.Key} - {e.Message}");
                }
            }
            return result;
        }
    }
}
