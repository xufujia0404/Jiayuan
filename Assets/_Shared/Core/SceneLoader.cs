using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sttop5.Shared.Core
{
    /// <summary>
    /// 场景加载管理器，处理 Additive 场景加载/卸载。
    /// 所有场景切换通过此类进行，支持加载进度回调。
    /// </summary>
    public class SceneLoader : Singleton<SceneLoader>
    {
        private readonly HashSet<string> _loadedScenes = new HashSet<string>();
        private bool _isLoading = false;

        public bool IsLoading => _isLoading;
        public IReadOnlyCollection<string> LoadedScenes => _loadedScenes;

        /// <summary>
        /// 以 Additive 模式加载场景。
        /// </summary>
        public void LoadSceneAdditive(string sceneName, Action onLoaded = null)
        {
            if (_isLoading)
            {
                Debug.LogWarning($"[SceneLoader] 正在加载中，忽略请求: {sceneName}");
                return;
            }

            if (_loadedScenes.Contains(sceneName))
            {
                Debug.LogWarning($"[SceneLoader] 场景已加载: {sceneName}");
                onLoaded?.Invoke();
                return;
            }

            StartCoroutine(LoadSceneAdditiveCoroutine(sceneName, onLoaded));
        }

        /// <summary>
        /// 卸载已加载的 Additive 场景。
        /// </summary>
        public void UnloadScene(string sceneName, Action onUnloaded = null)
        {
            if (!_loadedScenes.Contains(sceneName))
            {
                Debug.LogWarning($"[SceneLoader] 场景未加载，无法卸载: {sceneName}");
                onUnloaded?.Invoke();
                return;
            }

            StartCoroutine(UnloadSceneCoroutine(sceneName, onUnloaded));
        }

        /// <summary>
        /// 卸载所有通过 SceneLoader 加载的场景（保留根场景）。
        /// </summary>
        public void UnloadAllScenes(Action onAllUnloaded = null)
        {
            if (_loadedScenes.Count == 0)
            {
                onAllUnloaded?.Invoke();
                return;
            }

            StartCoroutine(UnloadAllScenesCoroutine(onAllUnloaded));
        }

        /// <summary>
        /// 检查指定场景是否已加载。
        /// </summary>
        public bool IsSceneLoaded(string sceneName)
        {
            return _loadedScenes.Contains(sceneName);
        }

        private IEnumerator LoadSceneAdditiveCoroutine(string sceneName, Action onLoaded)
        {
            _isLoading = true;

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[SceneLoader] 无法加载场景: {sceneName}");
                _isLoading = false;
                yield break;
            }

            while (!op.isDone)
            {
                yield return null;
            }

            _loadedScenes.Add(sceneName);

            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.IsValid())
            {
                SceneManager.SetActiveScene(loadedScene);
            }

            _isLoading = false;
            Debug.Log($"[SceneLoader] 场景已加载: {sceneName}");
            onLoaded?.Invoke();
        }

        private IEnumerator UnloadSceneCoroutine(string sceneName, Action onUnloaded)
        {
            _isLoading = true;

            var op = SceneManager.UnloadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[SceneLoader] 无法卸载场景: {sceneName}");
                _isLoading = false;
                yield break;
            }

            while (!op.isDone)
            {
                yield return null;
            }

            _loadedScenes.Remove(sceneName);
            _isLoading = false;
            Debug.Log($"[SceneLoader] 场景已卸载: {sceneName}");
            onUnloaded?.Invoke();
        }

        private IEnumerator UnloadAllScenesCoroutine(Action onAllUnloaded)
        {
            var scenesToUnload = new List<string>(_loadedScenes);
            foreach (var sceneName in scenesToUnload)
            {
                yield return UnloadSceneCoroutine(sceneName, null);
            }
            onAllUnloaded?.Invoke();
        }
    }
}
