using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tracks live score (distance-based + coin bonus), total coins collected,
/// and persists high score / total coin bank between runs using PlayerPrefs.
///
/// Player reference is pulled directly from GameManager.Instance.player —
/// no manual SetPlayer() call needed.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Score Settings")]
    [Tooltip("Score awarded per meter traveled. 10 = visible, 50 = punchy.")]
    public float distanceScoreMultiplier = 10f;

    [Tooltip("Bonus score added per coin collected (on top of the coin bank).")]
    public int coinScoreValue = 5;

    [Header("UI - Live HUD")]
    public TMP_Text scoreText;
    public TMP_Text coinText;

    [Header("UI - Game Over Panel")]
    public TMP_Text finalScoreText;
    public TMP_Text highScoreText;
    public TMP_Text coinsThisRunText;
    public TMP_Text totalCoinBankText;

    // ── State ────────────────────────────────────────────────────────────────
    public int CurrentScore   { get; private set; }
    public int CoinsThisRun   { get; private set; }
    public int DistanceScore  { get; private set; }   // exposed for debug / UI

    private float startZ;
    private Transform playerTransform;   // resolved from GameManager, never set manually

    private const string HIGH_SCORE_KEY  = "HighScore";
    private const string TOTAL_COINS_KEY = "TotalCoins";

    // ────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Called by GameManager.StartGame(). Resolves the player ref from
    /// GameManager so nothing else needs to call SetPlayer().
    /// </summary>
    public void ResetScore()
    {
        // Always re-resolve the player reference here so it's never stale
        if (GameManager.Instance != null && GameManager.Instance.player != null)
            playerTransform = GameManager.Instance.player.transform;
        else
            playerTransform = FindObjectOfType<PlayerController>()?.transform;

        if (playerTransform == null)
            Debug.LogWarning("ScoreManager: could not find PlayerController in scene. " +
                             "Assign it to GameManager.player in the Inspector.");

        CurrentScore  = 0;
        CoinsThisRun  = 0;
        DistanceScore = 0;
        startZ        = playerTransform != null ? playerTransform.position.z : 0f;

        UpdateHUD();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;
        if (playerTransform == null) return;

        float distanceTraveled = playerTransform.position.z - startZ;
        DistanceScore = Mathf.FloorToInt(distanceTraveled * distanceScoreMultiplier);

        // Total score is always recalculated so it never drifts out of sync
        CurrentScore = DistanceScore + (CoinsThisRun * coinScoreValue);

        UpdateHUD();
    }

    public void AddCoins(int amount)
    {
        CoinsThisRun += amount;
        // Score recalculates next Update(), but refresh HUD immediately
        // so the coin counter doesn't lag a frame behind collection
        UpdateHUD();
    }

    private void UpdateHUD()
    {
        if (scoreText) scoreText.text = CurrentScore.ToString("N0");
        if (coinText)  coinText.text  = CoinsThisRun.ToString();
    }

    /// <summary>
    /// Called by GameManager when the run ends.
    /// Saves high score and adds coins to the persistent bank.
    /// </summary>
    public void FinalizeRun()
    {
        int savedHighScore   = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        bool isNewHighScore  = CurrentScore > savedHighScore;

        if (isNewHighScore)
        {
            savedHighScore = CurrentScore;
            PlayerPrefs.SetInt(HIGH_SCORE_KEY, savedHighScore);
        }

        int totalCoins = PlayerPrefs.GetInt(TOTAL_COINS_KEY, 0) + CoinsThisRun;
        PlayerPrefs.SetInt(TOTAL_COINS_KEY, totalCoins);
        PlayerPrefs.Save();

        // Populate the Game Over panel
        if (finalScoreText)    finalScoreText.text    = $"Score: {CurrentScore:N0}";
        if (highScoreText)     highScoreText.text      = isNewHighScore
                                                            ? "NEW HIGH SCORE!"
                                                            : $"Best: {savedHighScore:N0}";
        if (coinsThisRunText)  coinsThisRunText.text   = $"Coins: {CoinsThisRun}";
        if (totalCoinBankText) totalCoinBankText.text  = $"Total Coins: {totalCoins}";
    }

    // ── Static helpers for menus ─────────────────────────────────────────────
    public static int GetHighScore()      => PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
    public static int GetTotalCoinBank()  => PlayerPrefs.GetInt(TOTAL_COINS_KEY, 0);
}