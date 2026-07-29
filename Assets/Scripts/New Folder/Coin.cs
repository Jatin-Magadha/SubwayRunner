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

        private bool moveTowardsPlayer = false;
        private Vector3 playerPosition;
        [SerializeField] private float moveSpeed = 0.2f;


        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, spinSpeedDegPerSec * Time.deltaTime, Space.World);

            if (moveTowardsPlayer)
            {
                transform.position = Vector3.MoveTowards(transform.position, playerPosition, moveSpeed * Time.deltaTime);
            }
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

        public void EnableMagnetAbility(Vector3 pos)
        {
            playerPosition = pos;
            moveTowardsPlayer = true;
        }
    }
}
