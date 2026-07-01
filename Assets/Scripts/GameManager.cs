using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game state controller. Singleton.
/// Handles Start / Playing / GameOver states and ties together
/// ScoreManager, Player, and Spawner.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { MainMenu, Playing, Paused, GameOver }
    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    [Header("References")]
    public PlayerController player;
    public TileSpawner tileSpawner;
    public ScoreManager scoreManager;

    [Header("UI (assign in Inspector)")]
    public GameObject mainMenuPanel;
    public GameObject gameplayHUD;
    public GameObject gameOverPanel;

    [Header("Game Settings")]
    public float baseSpeed = 8f;
    public float speedIncreasePerSecond = 0.15f;
    public float maxSpeed = 28f;

    public float CurrentSpeed { get; private set; }
    private float gameTime;

    private void Awake()
    {
        // Singleton pattern - persists across scene reloads if needed
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        ShowMainMenu();
    }

    private void Update()
    {
        if (CurrentState == GameState.Playing)
        {
            gameTime += Time.deltaTime;
            CurrentSpeed = Mathf.Min(baseSpeed + gameTime * speedIncreasePerSecond, maxSpeed);
        }
    }

    public void ShowMainMenu()
    {
        CurrentState = GameState.MainMenu;
        Time.timeScale = 1f;
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
        if (gameplayHUD) gameplayHUD.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);
    }

    /// <summary>Call this from the "Play" / "Tap to Start" button.</summary>
    public void StartGame()
    {
        CurrentState = GameState.Playing;
        gameTime = 0f;
        CurrentSpeed = baseSpeed;

        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (gameplayHUD) gameplayHUD.SetActive(true);
        if (gameOverPanel) gameOverPanel.SetActive(false);

        scoreManager.ResetScore();
        tileSpawner.ResetSpawner();
        player.ResetPlayer();

        Time.timeScale = 1f;
    }

    /// <summary>Called by PlayerController when it collides with an obstacle.</summary>
    public void TriggerGameOver()
    {
        if (CurrentState != GameState.Playing) return; // avoid double-trigger

        CurrentState = GameState.GameOver;
        scoreManager.FinalizeRun();

        if (gameplayHUD) gameplayHUD.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(true);

        // Optional: slow-mo death effect instead of hard stop
        Invoke(nameof(StopTime), 2.0f);
    }

    private void StopTime()
    {
        Time.timeScale = 0f;
        
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        // Simplest approach: reload the scene fresh
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        ShowMainMenu();
    }
}