using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    private int totalCoins;
    private int currentCoins;

    private int currentScore = 0;

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

    private void Update()
    {
        if (GameManager.Instance.CurrentGameState != GameState.InProgress)
            return;
    }

    private void UpdateScore()
    {
        currentScore++;
    }

    public void StartGame()
    {
        ResetData();

        InvokeRepeating(nameof(UpdateScore), 1.0f, 1.0f);
    }

    public void AddCoin()
    {
        currentCoins++;
    }

    public void ResetData()
    {
        currentCoins = 0;
        currentScore = 0;
    }
}
