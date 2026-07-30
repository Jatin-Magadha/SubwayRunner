using System;
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
        GameManager.Instance.onGameStarted += GameManager_onGameStarted;
    }

    private void OnDestroy()
    {
        GameManager.Instance.onGameStarted -= GameManager_onGameStarted;
    }

    private void GameManager_onGameStarted(object sender, EventArgs e)
    {
        gameObject.SetActive(true);

        int chance = UnityEngine.Random.Range(0, 10);

        if (chance < 3)
        {
            //Destroy(gameObject);

            gameObject.SetActive(false);
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

                //Destroy(gameObject);
                gameObject.SetActive(false);
            }

            if (powerUpType == PowerUpType.Magnet)
            {
                playerPosition = other.transform.position;

                PlayerController playerController = other.GetComponentInParent<PlayerController>();

                if (playerController)
                {
                    playerController.EnableMagnetAbility();

                    //Destroy(gameObject);
                    gameObject.SetActive(false);
                }
            }
        }
    }

}
