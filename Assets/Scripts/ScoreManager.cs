using System.Timers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    private int totalCoins;
    private int currentCoins;

    private float currentScore = 0;

    [SerializeField] private TextMeshProUGUI inGameScoreUI;
    [SerializeField] private TextMeshProUGUI inGameCoinUI;

    [SerializeField] private TextMeshProUGUI gameOverScoreUI;
    [SerializeField] private TextMeshProUGUI gameOverCoinUI;

    [SerializeField] private TextMeshProUGUI pauseScoreUI;
    [SerializeField] private TextMeshProUGUI pauseCoinUI;

    [SerializeField] private GameObject multiplierAbilityIcon;
    [SerializeField] private Slider multiplierAbilitySlider;

    private bool isMultiplierActivated = true;
    private float multiplierTimer = 30.0f;
    private float currentTimerLeft = 0;

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
        UpdateScore();
    }

    private void UpdateScore()
    {
        if (GameManager.Instance.CurrentGameState != GameState.InProgress)
        {
            //CancelInvoke(nameof(UpdateScore));

            return;
        }

        if (isMultiplierActivated)
        {
            currentScore += 30 * Time.deltaTime;

            currentTimerLeft -= Time.deltaTime;

            multiplierAbilitySlider.value = currentTimerLeft / multiplierTimer;

            if (currentTimerLeft <= 0)
            {
                DeactivateMultiplier();
            }
        }
        else
        {
            currentScore += 10 * Time.deltaTime;
        }

        inGameScoreUI.text = Mathf.FloorToInt(currentScore).ToString();
        pauseScoreUI.text = Mathf.FloorToInt(currentScore).ToString();

    }

    public void StartGame()
    {
        ResetData();

        //InvokeRepeating(nameof(UpdateScore), 1.0f, 1.0f);
    }

    public void AddCoin()
    {
        currentCoins++;

        inGameCoinUI.text = currentCoins.ToString();
        pauseScoreUI.text = currentCoins.ToString();
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
        NetworkDataManager.Instance.SendScoreToLeaderboard(Mathf.FloorToInt(currentScore));

        gameOverCoinUI.text = currentCoins.ToString();
        gameOverScoreUI.text = Mathf.FloorToInt(currentScore).ToString();
    }

    public void ActivateMultiplier()
    {
        isMultiplierActivated = true;

        //CancelInvoke(nameof(DeactivateMultiplier));
        //Invoke(nameof(DeactivateMultiplier), 30.0f);

        currentTimerLeft = multiplierTimer;
        multiplierAbilityIcon.SetActive(true);

    }

    private void DeactivateMultiplier()
    {
        isMultiplierActivated = false;
        multiplierAbilityIcon.SetActive(false);
    }
}
