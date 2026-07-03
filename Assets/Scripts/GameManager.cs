using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { MainMenu, Playing, Paused, GameOver }
    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    [Header("References")]
    public PlayerController player;
    public TileSpawner      tileSpawner;
    public ScoreManager     scoreManager;
    public ChaserController chaser;

    [Header("UI")]
    public GameObject mainMenuPanel;
    public GameObject gameplayHUD;
    public GameObject gameOverPanel;

    [Header("Game Settings")]
    public float baseSpeed              = 8f;
    public float speedIncreasePerSecond = 0.15f;
    public float maxSpeed               = 28f;

    [Tooltip("Seconds after game start before the chaser becomes active. " +
             "Gives player time to get comfortable before pressure begins.")]
    public float chaserActivationDelay = 4f;

    public float CurrentSpeed { get; private set; }
    private float gameTime;
    private float chaserActivationTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start() => ShowMainMenu();

    private void Update()
    {
        if (CurrentState != GameState.Playing) return;

        gameTime += Time.deltaTime;
        CurrentSpeed = Mathf.Min(baseSpeed + gameTime * speedIncreasePerSecond, maxSpeed);

        // Activate the chaser after the delay
        if (chaser != null && !chaser.enabled)
        {
            chaserActivationTimer -= Time.deltaTime;
            if (chaserActivationTimer <= 0f)
                chaser.ActivateChaser();
        }
    }

    public void ShowMainMenu()
    {
        CurrentState = GameState.MainMenu;
        Time.timeScale = 1f;
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
        if (gameplayHUD)   gameplayHUD.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);
    }

    public void StartGame()
    {
        CurrentState  = GameState.Playing;
        gameTime      = 0f;
        CurrentSpeed  = baseSpeed;
        chaserActivationTimer = chaserActivationDelay;

        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (gameplayHUD)   gameplayHUD.SetActive(true);
        if (gameOverPanel) gameOverPanel.SetActive(false);

        scoreManager.ResetScore();
        tileSpawner.ResetSpawner();
        player.ResetPlayer();

        if (chaser != null) chaser.ResetChaser();

        Time.timeScale = 1f;
    }

    public void TriggerGameOver()
    {
        if (CurrentState != GameState.Playing) return;

        CurrentState = GameState.GameOver;
        scoreManager.FinalizeRun();

        if (chaser != null) chaser.DeactivateChaser();

        if (gameplayHUD)   gameplayHUD.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(true);

        Invoke(nameof(StopGameTime), 2.0f);
    }

    private void StopGameTime()
    {
        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        ShowMainMenu();
    }
}