using System;
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

        [SerializeField] private AudioClip coinCollectionClip;

        private Vector3 initialPos;


        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Start()
        {
            GameManager.Instance.onGameStarted += GameManager_onGameStarted;

            initialPos = transform.localPosition;
        }

        private void OnDestroy()
        {
            GameManager.Instance.onGameStarted -= GameManager_onGameStarted;
        }

        private void GameManager_onGameStarted(object sender, EventArgs e)
        {
            gameObject.SetActive(true);

            moveTowardsPlayer = false;
            transform.localPosition = initialPos;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, spinSpeedDegPerSec * Time.deltaTime, Space.World);

            if (moveTowardsPlayer)
            {
                transform.position = Vector3.MoveTowards(transform.position, playerPosition, moveSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, playerPosition) < 1.0f)
                {
                    Collect();
                }

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

            GameManager.Instance.PlayAudio(coinCollectionClip);

            moveTowardsPlayer = false;

            gameObject.SetActive(false);
        }

        public void EnableMagnetAbility(Vector3 pos)
        {
            playerPosition = pos;
            moveTowardsPlayer = true;
        }
    }
}
