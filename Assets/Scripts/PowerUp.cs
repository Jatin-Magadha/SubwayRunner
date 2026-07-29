using UnityEngine;

public enum PowerUpType
{
    Multiplier,
    Magnet
}

public class PowerUp : MonoBehaviour
{
    [SerializeField] private PowerUpType powerUpType;
    [SerializeField] private float duration = 15.0f;
    [SerializeField] private float magnetRadius = 50.0f;
    [SerializeField] private float moveSpeed = 0.2f;

    private Vector3 playerPosition;

    private void Start()
    {
        int chance = Random.Range(0, 10);

        if (chance < 3)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, 180f * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (powerUpType == PowerUpType.Multiplier)
            {
                ScoreManager.Instance.ActivateMultiplier();

                Destroy(gameObject);
            }

            if (powerUpType == PowerUpType.Magnet)
            {
                playerPosition = other.transform.position;

                PlayerController playerController = other.GetComponentInParent<PlayerController>();

                if (playerController)
                {
                    playerController.EnableMagnetAbility();

                    Destroy(gameObject);
                }

                //EnableMagnet();
            }
        }
    }

    private void EnableMagnet()
    {
        float timer = duration;
        while (timer > 0f)
        {
            Collider[] nearbyCoins = Physics.OverlapSphere(playerPosition, magnetRadius);
            foreach (var col in nearbyCoins)
            {
                if (col.CompareTag("Coin"))
                {
                    col.transform.position = Vector3.MoveTowards(
                        col.transform.position, playerPosition, moveSpeed * Time.deltaTime);
                }
            }
            timer -= Time.deltaTime;
        }

        Destroy(gameObject, timer);
    }
}
