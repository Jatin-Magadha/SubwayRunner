using UnityEngine;

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
        StartGame();
    }

    public void StartGame()
    {
        ScoreManager.Instance.StartGame();

        ChangeGameState(GameState.InProgress);
    }

    public void ChangeGameState(GameState newState)
    {
        CurrentGameState = newState;
    }
}
