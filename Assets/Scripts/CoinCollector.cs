using UnityEngine;

/// <summary>
/// Attach to each individual coin prefab (Trigger collider, tagged "Coin").
/// Handles the visual pickup and reports to ScoreManager.
/// </summary>
public class CoinCollector : MonoBehaviour
{
    public int coinValue = 1;
    public GameObject collectVFX;   // optional sparkle effect prefab
    public AudioClip collectSFX;

    private static AudioSource sfxSource;

    void Update()
    {
        transform.Rotate(0, 250 * Time.deltaTime, 0f);
    }

    /// <summary>
    /// Static entry point called from PlayerController.OnTriggerEnter.
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