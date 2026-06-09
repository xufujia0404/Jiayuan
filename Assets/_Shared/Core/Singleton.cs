using UnityEngine;

namespace Sttop5.Shared.Core
{
    /// <summary>
    /// 泛型单例基类，所有需要全局唯一实例的管理器继承此类。
    /// 自动处理 DontDestroyOnLoad 和重复实例销毁。
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<T>();

                        if (_instance == null)
                        {
                            GameObject go = new GameObject($"[Singleton] {typeof(T).Name}");
                            _instance = go.AddComponent<T>();
                            DontDestroyOnLoad(go);
                        }
                    }
                    return _instance;
                }
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _applicationIsQuitting = true;
            }
        }

        /// <summary>
        /// 重置单例状态，仅用于测试或场景完全重载时。
        /// </summary>
        public static void ResetInstance()
        {
            _applicationIsQuitting = false;
            _instance = null;
        }
    }
}
