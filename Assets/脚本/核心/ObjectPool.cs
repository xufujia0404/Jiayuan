using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Core
{
    public class ObjectPool : Singleton<ObjectPool>
    {
        private Dictionary<string, Queue<GameObject>> _pools = new Dictionary<string, Queue<GameObject>>();
        private Dictionary<GameObject, string> _objectTags = new Dictionary<GameObject, string>();

        [SerializeField] private Transform _poolContainer;

        protected override void Awake()
        {
            base.Awake();
            if (_poolContainer == null)
            {
                GameObject container = new GameObject("PoolContainer");
                _poolContainer = container.transform;
                DontDestroyOnLoad(container);
            }
        }

        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            string tag = prefab.name;

            if (_pools.TryGetValue(tag, out Queue<GameObject> pool) && pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                obj.transform.position = position;
                obj.transform.rotation = rotation;
                obj.SetActive(true);
                return obj;
            }

            GameObject newObj = Instantiate(prefab, position, rotation);
            newObj.name = prefab.name;
            _objectTags[newObj] = tag;
            return newObj;
        }

        public GameObject Get(GameObject prefab, Vector3 position)
        {
            return Get(prefab, position, Quaternion.identity);
        }

        public GameObject Get(GameObject prefab)
        {
            return Get(prefab, Vector3.zero, Quaternion.identity);
        }

        public T Get<T>(GameObject prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            GameObject obj = Get(prefab, position, rotation);
            return obj.GetComponent<T>();
        }

        public void Return(GameObject obj)
        {
            if (obj == null) return;

            string tag;
            if (!_objectTags.TryGetValue(obj, out tag))
            {
                tag = obj.name;
                _objectTags[obj] = tag;
            }

            if (!_pools.ContainsKey(tag))
            {
                _pools[tag] = new Queue<GameObject>();
            }

            obj.SetActive(false);
            obj.transform.SetParent(_poolContainer);
            _pools[tag].Enqueue(obj);
        }

        public void Preload(GameObject prefab, int count)
        {
            string tag = prefab.name;

            if (!_pools.ContainsKey(tag))
            {
                _pools[tag] = new Queue<GameObject>();
            }

            for (int i = 0; i < count; i++)
            {
                GameObject obj = Instantiate(prefab, _poolContainer);
                obj.name = prefab.name;
                obj.SetActive(false);
                _objectTags[obj] = tag;
                _pools[tag].Enqueue(obj);
            }
        }

        public void ClearPool(string tag)
        {
            if (_pools.TryGetValue(tag, out Queue<GameObject> pool))
            {
                while (pool.Count > 0)
                {
                    GameObject obj = pool.Dequeue();
                    if (obj != null)
                    {
                        Destroy(obj);
                    }
                }
                _pools.Remove(tag);
            }
        }

        public void ClearAllPools()
        {
            foreach (var pool in _pools.Values)
            {
                while (pool.Count > 0)
                {
                    GameObject obj = pool.Dequeue();
                    if (obj != null)
                    {
                        Destroy(obj);
                    }
                }
            }
            _pools.Clear();
            _objectTags.Clear();
        }
    }
}
