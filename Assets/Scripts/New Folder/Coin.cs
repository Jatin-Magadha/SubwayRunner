using UnityEngine;

namespace SubwaySurferClone
{
    /// <summary>
    /// Put this on the coin prefab. Requires a trigger Collider on the same object.
    /// Rotates for visual flair, and returns itself to its pool when collected.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Coin : MonoBehaviour
    {
        public float spinSpeedDegPerSec = 180f;
        public int value = 1;

        private PoolMember _poolMember;

        private void Awake()
        {
            _poolMember = GetComponent<PoolMember>();
            GetComponent<Collider>().isTrigger = true;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, spinSpeedDegPerSec * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            CoinWallet wallet = other.GetComponentInParent<CoinWallet>();
            if (wallet != null) wallet.AddCoins(value);

            Collect();
        }

        private void Collect()
        {
            // Could trigger a particle/sound here before returning to pool.
            if (_poolMember != null)
                _poolMember.ReturnToPool();
            else
                gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Minimal coin counter. Put on the player object (or anywhere) and hook UI to OnCoinsChanged.
    /// </summary>
    public class CoinWallet : MonoBehaviour
    {
        public int TotalCoins { get; private set; }
        public System.Action<int> OnCoinsChanged;

        public void AddCoins(int amount)
        {
            TotalCoins += amount;
            OnCoinsChanged?.Invoke(TotalCoins);
        }
    }
}
