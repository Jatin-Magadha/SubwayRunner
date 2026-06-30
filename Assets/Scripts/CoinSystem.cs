using UnityEngine;

/// <summary>
/// Attach to each individual coin prefab (with a Trigger collider tagged "Coin").
/// Handles the visual pickup (disable + maybe pooling) and reports to ScoreManager.
/// </summary>
public class CoinCollector : MonoBehaviour
{
    public int coinValue = 1;
    public GameObject collectVFX; // optional sparkle effect prefab
    public AudioClip collectSFX;

    private static AudioSource sfxSource; // simple shared one-shot player

    /// <summary>
    /// Static entry point called from PlayerController.OnTriggerEnter
    /// so all collection logic lives in one place.
    /// </summary>
    public static void HandleCoinCollected(GameObject coinObject)
    {
        CoinCollector coin = coinObject.GetComponent<CoinCollector>();
        int value = coin != null ? coin.coinValue : 1;

        ScoreManager.Instance.AddCoins(value);

        if (coin != null)
        {
            if (coin.collectVFX != null)
                Instantiate(coin.collectVFX, coinObject.transform.position, Quaternion.identity);

            if (coin.collectSFX != null)
                PlaySFX(coin.collectSFX);
        }

        // Disable instead of Destroy if using object pooling for performance
        coinObject.SetActive(false);
    }

    private static void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null)
        {
            GameObject go = new GameObject("CoinSFXPlayer");
            sfxSource = go.AddComponent<AudioSource>();
        }
        sfxSource.PlayOneShot(clip);
    }
}

/// <summary>
/// Place on a Tile prefab to procedurally lay out a curved/straight row of coins
/// down one lane — the classic Subway-Surfers-style coin arc.
/// </summary>
public class CoinRowSpawner : MonoBehaviour
{
    public GameObject coinPrefab;
    public int coinsPerRow = 8;
    public float spacing = 1.5f;
    public float laneDistance = 2.5f;

    /// <summary>Call manually, or from Start(), with a chosen lane (-1, 0, 1).</summary>
    public void SpawnRow(int lane, Vector3 localStartPosition)
    {
        float xPos = lane * laneDistance;
        for (int i = 0; i < coinsPerRow; i++)
        {
            Vector3 pos = localStartPosition + new Vector3(xPos, 0f, i * spacing);
            GameObject coin = Instantiate(coinPrefab, transform);
            coin.transform.localPosition = pos;
        }
    }
}