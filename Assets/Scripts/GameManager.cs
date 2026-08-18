using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Menu,
    InProgress,
    Paused,
    GameOver
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState CurrentGameState { get; private set; }

    public event EventHandler onMenuClicked;
    public event EventHandler onGameStarted;

    public float startingSpeed = 10.0f;
    public float maxSpeed = 30.0f;

    [SerializeField] private AudioSource audioSource;

    [Header("Time")]
    public float timeToReachMaxSpeed = 300f; // 5 minutes

    private float gameStartTime;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        //StartGame();
        ChangeGameState(GameState.Menu);
    }

    public void StartGame()
    {
        onGameStarted?.Invoke(this, EventArgs.Empty);

        ScoreManager.Instance.StartGame();

        ChangeGameState(GameState.InProgress);

        gameStartTime = Time.time;
    }

    public void ChangeGameState(GameState newState)
    {
        CurrentGameState = newState;

        if (MenuManager.Instance != null)
            MenuManager.Instance.UpdateMenu();

        switch (CurrentGameState)
        {
            case GameState.GameOver:
                ScoreManager.Instance.UpdateGameOverData();
                break;
        }
    }

    public void RestartGame()
    {
        //SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void MainMenu()
    {
        ChangeGameState(GameState.Menu);
        //SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        onMenuClicked?.Invoke(this, EventArgs.Empty);

        Time.timeScale = 1.0f;
    }

    public void PauseGame()
    {
        Time.timeScale = 0.0f;

        ChangeGameState(GameState.Paused);
    }

    public void ResumeGame()
    {
        Time.timeScale = 1.0f;

        ChangeGameState(GameState.InProgress);
    }

    public float GetCurrentSpeed()
    {
        float elapsed = Time.time - gameStartTime;

        // Normalize from 0 to 1
        float t = Mathf.Clamp01(elapsed / timeToReachMaxSpeed);

        // Interpolate between min and max speed
        return Mathf.Lerp(startingSpeed, maxSpeed, t);
    }

    public void PlayAudio(AudioClip clip)
    {
        audioSource.PlayOneShot(clip);
    }
}
