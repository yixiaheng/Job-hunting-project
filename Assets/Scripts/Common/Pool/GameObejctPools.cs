using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Common.Pool
{
    public sealed class GameObjectPool
    {
        private readonly Transform parent;
        private readonly List<GameObject> activeObjects = new();
        private readonly ObjectPool<GameObject> pool;

        public GameObjectPool(
            GameObject prefab,
            Transform parent,
            int defaultCapacity = 8,
            int maxSize = 64,
            Action<GameObject> onGet = null,
            Action<GameObject> onRelease = null)
        {
            this.parent = parent;

            pool = new ObjectPool<GameObject>(
                CreateObject,
                item =>
                {
                    item.transform.SetParent(this.parent, false);
                    item.SetActive(true);
                    onGet?.Invoke(item);
                },
                item =>
                {
                    onRelease?.Invoke(item);
                    item.SetActive(false);
                },
                item => UnityEngine.Object.Destroy(item),
                collectionCheck: true,
                defaultCapacity,
                maxSize
            );

            GameObject CreateObject()
            {
                return UnityEngine.Object.Instantiate(prefab, this.parent, false);
            }
        }

        public GameObject Get()
        {
            GameObject item = pool.Get();
            activeObjects.Add(item);
            return item;
        }

        public T Get<T>() where T : Component
        {
            return Get().GetComponent<T>();
        }

        public void Release(GameObject item)
        {
            if (!activeObjects.Remove(item))
            {
                return;
            }

            pool.Release(item);
        }

        public void Release(Component item)
        {
            Release(item.gameObject);
        }

        public void ReleaseAll()
        {
            for (int i = activeObjects.Count - 1; i >= 0; i--)
            {
                pool.Release(activeObjects[i]);
            }

            activeObjects.Clear();
        }

        public void Dispose()
        {
            ReleaseAll();
            pool.Clear();
        }
    }
}
