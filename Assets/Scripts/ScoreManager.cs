using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    private int totalCoins;
    private int currentCoins;

    private int currentScore = 0;

    [SerializeField] private TextMeshProUGUI inGameScoreUI;
    [SerializeField] private TextMeshProUGUI inGameCoinUI;

    [SerializeField] private TextMeshProUGUI gameOverScoreUI;
    [SerializeField] private TextMeshProUGUI gameOverCoinUI;

    private bool isMultiplierActivated = true;

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


    private void UpdateScore()
    {
        if (GameManager.Instance.CurrentGameState != GameState.InProgress)
        {
            CancelInvoke(nameof(UpdateScore));
        }

        if (isMultiplierActivated)
        {
            currentScore += 30;
        }
        else
        {
            currentScore += 10;
        }


        inGameScoreUI.text = currentScore.ToString();
    }

    public void StartGame()
    {
        ResetData();

        InvokeRepeating(nameof(UpdateScore), 1.0f, 1.0f);
    }

    public void AddCoin()
    {
        currentCoins++;

        inGameCoinUI.text = currentCoins.ToString();
    }

    public void ResetData()
    {
        currentCoins = 0;
        currentScore = 0;

        inGameCoinUI.text = currentCoins.ToString();
        inGameScoreUI.text = currentScore.ToString();

        isMultiplierActivated = false;
    }

    public void UpdateGameOverData()
    {
        NetworkDataManager.Instance.SendScoreToLeaderboard(currentScore);

        gameOverCoinUI.text = currentCoins.ToString();
        gameOverScoreUI.text = currentScore.ToString();
    }

    public void ActivateMultiplier()
    {
        isMultiplierActivated = true;

        CancelInvoke(nameof(DeactivateMultiplier));
        Invoke(nameof(DeactivateMultiplier), 30.0f); 
    }

    private void DeactivateMultiplier()
    {
        isMultiplierActivated = false;
    }
}
