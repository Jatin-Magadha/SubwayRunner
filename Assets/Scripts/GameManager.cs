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
    }

    public void ChangeGameState(GameState newState)
    {
        CurrentGameState = newState;

        MenuManager.Instance.UpdateMenu();
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
    }
}
