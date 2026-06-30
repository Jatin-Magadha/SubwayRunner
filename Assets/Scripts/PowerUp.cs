using UnityEngine;
using System.Collections;

/// <summary>
/// Base power-up behavior: magnet (auto-collect coins), score multiplier,
/// or temporary invincibility (pass through obstacles). Attach to pickup prefab.
/// </summary>
public class PowerUp : MonoBehaviour
{
    public enum PowerUpType { Magnet, ScoreMultiplier, Invincibility, JetpackHover }

    public PowerUpType type;
    public float duration = 8f;
    public float magnetRadius = 4f;
    public float scoreMultiplierAmount = 2f;

    private static Coroutine activeRoutine;

    public void Activate(PlayerController player)
    {
        switch (type)
        {
            case PowerUpType.Magnet:
                player.StartCoroutine(MagnetRoutine(player));
                break;
            case PowerUpType.ScoreMultiplier:
                player.StartCoroutine(ScoreMultiplierRoutine());
                break;
            case PowerUpType.Invincibility:
                player.StartCoroutine(InvincibilityRoutine(player));
                break;
            case PowerUpType.JetpackHover:
                // Custom hover logic could go here (lock player to fly mode)
                break;
        }
    }

    private IEnumerator MagnetRoutine(PlayerController player)
    {
        float timer = duration;
        while (timer > 0f)
        {
            Collider[] nearbyCoins = Physics.OverlapSphere(player.transform.position, magnetRadius);
            foreach (var col in nearbyCoins)
            {
                if (col.CompareTag("Coin"))
                {
                    col.transform.position = Vector3.MoveTowards(
                        col.transform.position, player.transform.position, Time.deltaTime * 15f);
                }
            }
            timer -= Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator ScoreMultiplierRoutine()
    {
        ScoreManager.Instance.distanceScoreMultiplier *= scoreMultiplierAmount;
        yield return new WaitForSeconds(duration);
        ScoreManager.Instance.distanceScoreMultiplier /= scoreMultiplierAmount;
    }

    private IEnumerator InvincibilityRoutine(PlayerController player)
    {
        // Simple approach: temporarily make player ignore the Obstacle layer
        int playerLayer = player.gameObject.layer;
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        Physics.IgnoreLayerCollision(playerLayer, obstacleLayer, true);

        yield return new WaitForSeconds(duration);

        Physics.IgnoreLayerCollision(playerLayer, obstacleLayer, false);
    }
}