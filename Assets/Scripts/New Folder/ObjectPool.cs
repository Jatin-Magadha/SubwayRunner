using System.Collections.Generic;
using UnityEngine;

namespace SubwaySurferClone
{
    /// <summary>
    /// Generic GameObject pool. Avoids Instantiate/Destroy churn for tiles and coins,
    /// which matters a lot in an endless runner since you spawn constantly.
    /// </summary>
    public class ObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly Stack<GameObject> _inactive = new Stack<GameObject>();

        /// <summary>How many instances from this pool are currently active (i.e. "in the world").</summary>
        public int ActiveCount { get; private set; }

        public ObjectPool(GameObject prefab, Transform parent, int prewarmCount = 0)
        {
            _prefab = prefab;
            _parent = parent;

            for (int i = 0; i < prewarmCount; i++)
            {
                GameObject obj = CreateNew();
                obj.SetActive(false);
                _inactive.Push(obj);
            }
        }

        private GameObject CreateNew()
        {
            GameObject obj = Object.Instantiate(_prefab, _parent);
            PoolMember member = obj.GetComponent<PoolMember>();
            if (member == null) member = obj.AddComponent<PoolMember>();
            member.OwnerPool = this;
            return obj;
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject obj = _inactive.Count > 0 ? _inactive.Pop() : CreateNew();
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);
            ActiveCount++;
            return obj;
        }

        public void Release(GameObject obj)
        {
            if (!obj.activeSelf) return; // already released - avoid double-counting

            obj.SetActive(false);
            obj.transform.SetParent(_parent);
            _inactive.Push(obj);
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
        }
    }

    /// <summary>
    /// Tag component so a pooled instance knows which pool to return itself to
    /// (handy for coins that release themselves on collection).
    /// </summary>
    public class PoolMember : MonoBehaviour
    {
        public ObjectPool OwnerPool;

        public void ReturnToPool()
        {
            OwnerPool?.Release(gameObject);
        }
    }
}