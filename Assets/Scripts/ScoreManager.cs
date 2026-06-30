using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tracks live score (distance-based + coin bonus), total coins collected,
/// and persists high score / total coin bank between runs using PlayerPrefs.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Score Settings")]
    public float distanceScoreMultiplier = 1f; // score per meter traveled
    public int coinScoreValue = 5;             // bonus score per coin, separate from coin bank

    [Header("UI - Live HUD")]
    public TMP_Text scoreText;
    public TMP_Text coinText;

    [Header("UI - Game Over Panel")]
    public TMP_Text finalScoreText;
    public TMP_Text highScoreText;
    public TMP_Text coinsThisRunText;
    public TMP_Text totalCoinBankText;

    public int CurrentScore { get; private set; }
    public int CoinsThisRun { get; private set; }

    private Transform player;
    private float startZ;

    private const string HIGH_SCORE_KEY = "HighScore";
    private const string TOTAL_COINS_KEY = "TotalCoins";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Hook the player's transform so we can measure distance traveled.</summary>
    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }

    public void ResetScore()
    {
        CurrentScore = 0;
        CoinsThisRun = 0;
        startZ = player != null ? player.position.z : 0f;
        UpdateHUD();
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;
        if (player == null) return;

        float distanceTraveled = player.position.z - startZ;
        int distanceScore = Mathf.FloorToInt(distanceTraveled * distanceScoreMultiplier);

        // Total score = distance covered + coin bonus, recalculated live
        CurrentScore = distanceScore + (CoinsThisRun * coinScoreValue);
        UpdateHUD();
    }

    public void AddCoins(int amount)
    {
        CoinsThisRun += amount;
        UpdateHUD();
    }

    private void UpdateHUD()
    {
        if (scoreText) scoreText.text = CurrentScore.ToString("N0");
        if (coinText) coinText.text = CoinsThisRun.ToString();
    }

    /// <summary>Called by GameManager when the run ends. Saves high score + coin bank.</summary>
    public void FinalizeRun()
    {
        int highScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        bool isNewHighScore = CurrentScore > highScore;
        if (isNewHighScore)
        {
            highScore = CurrentScore;
            PlayerPrefs.SetInt(HIGH_SCORE_KEY, highScore);
        }

        int totalCoins = PlayerPrefs.GetInt(TOTAL_COINS_KEY, 0);
        totalCoins += CoinsThisRun;
        PlayerPrefs.SetInt(TOTAL_COINS_KEY, totalCoins);

        PlayerPrefs.Save();

        if (finalScoreText) finalScoreText.text = $"Score: {CurrentScore:N0}";
        if (highScoreText) highScoreText.text = isNewHighScore ? "NEW HIGH SCORE!" : $"Best: {highScore:N0}";
        if (coinsThisRunText) coinsThisRunText.text = $"Coins: {CoinsThisRun}";
        if (totalCoinBankText) totalCoinBankText.text = $"Total Coins: {totalCoins}";
    }

    public static int GetHighScore() => PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
    public static int GetTotalCoinBank() => PlayerPrefs.GetInt(TOTAL_COINS_KEY, 0);
}