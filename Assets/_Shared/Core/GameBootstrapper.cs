using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sttop5.Shared.Core
{
    /// <summary>
    /// 游戏启动器，作为整个游戏的入口点。
    /// 负责初始化共享系统并加载家园场景。
    /// 挂载到 PlatformRoot 场景中的 GameObject 上。
    /// </summary>
    public class GameBootstrapper : Singleton<GameBootstrapper>
    {
        [Header("场景配置")]
        [SerializeField] private string _homeSceneName = "HomeScene";
        [SerializeField] private bool _autoLoadHome = true;

        [Header("调试")]
        [SerializeField] private bool _debugMode = false;

        private bool _isInitialized = false;

        public bool IsInitialized => _isInitialized;

        protected override void Awake()
        {
            base.Awake();
            InitializePlatform();
        }

        private void Start()
        {
            if (_autoLoadHome && _isInitialized)
            {
                LoadHomeScene();
            }
        }

        private void InitializePlatform()
        {
            if (_isInitialized) return;

            Debug.Log("[GameBootstrapper] 初始化平台...");

            // 确保核心管理器存在
            EnsureManager<SceneLoader>();
            EnsureManager<ModuleManager>();

            _isInitialized = true;
            Debug.Log("[GameBootstrapper] 平台初始化完成");
        }

        /// <summary>
        /// 加载家园场景。
        /// </summary>
        public void LoadHomeScene()
        {
            if (string.IsNullOrEmpty(_homeSceneName))
            {
                Debug.LogError("[GameBootstrapper] 家园场景名未配置");
                return;
            }

            Debug.Log($"[GameBootstrapper] 加载家园场景: {_homeSceneName}");
            SceneLoader.Instance.LoadSceneAdditive(_homeSceneName);
        }

        /// <summary>
        /// 返回家园（停用当前模块，加载家园场景）。
        /// </summary>
        public void ReturnToHome()
        {
            ModuleManager.Instance.DeactivateCurrentModule();

            if (!SceneLoader.Instance.IsSceneLoaded(_homeSceneName))
            {
                LoadHomeScene();
            }
        }

        private T EnsureManager<T>() where T : MonoBehaviour
        {
            if (FindObjectOfType<T>() == null)
            {
                GameObject go = new GameObject($"[Manager] {typeof(T).Name}");
                go.transform.SetParent(transform);
                return go.AddComponent<T>();
            }
            return FindObjectOfType<T>();
        }

        private void OnGUI()
        {
            if (!_debugMode) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"Active Module: {ModuleManager.Instance?.ActiveModuleId ?? "none"}");
            GUILayout.Label($"Loaded Scenes: {SceneLoader.Instance?.LoadedScenes?.Count ?? 0}");
            if (GUILayout.Button("Return to Home"))
            {
                ReturnToHome();
            }
            GUILayout.EndArea();
        }
    }
}
