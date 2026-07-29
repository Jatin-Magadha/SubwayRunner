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

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, spinSpeedDegPerSec * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            Collect();
        }

        private void Collect()
        {
            ScoreManager.Instance.AddCoin();

            Destroy(gameObject);
        }
    }
}
