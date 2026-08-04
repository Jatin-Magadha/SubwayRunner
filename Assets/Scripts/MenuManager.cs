using UnityEngine;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }


    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject inGameMenu;
    [SerializeField] private GameObject gameOverMenu;
    [SerializeField] private GameObject pauseMenu;

    private void Start()
    {
        Instance = this;

        UpdateMenu();
    }

    public void UpdateMenu()
    {
        switch (GameManager.Instance.CurrentGameState)
        {
            case GameState.Menu:
                mainMenu.SetActive(true);
                inGameMenu.SetActive(false);
                gameOverMenu.SetActive(false);
                pauseMenu.SetActive(false);
                break;

            case GameState.InProgress:
                mainMenu.SetActive(false);
                inGameMenu.SetActive(true);
                gameOverMenu.SetActive(false);
                pauseMenu.SetActive(false);
                break;

            case GameState.Paused:
                mainMenu.SetActive(false);
                inGameMenu.SetActive(false);
                gameOverMenu.SetActive(false);
                pauseMenu.SetActive(true);
                break;

            case GameState.GameOver:
                Invoke(nameof(OpenGameOverAnimation), 1.0f);
                break;
        }
    }

    private void OpenGameOverAnimation()
    {
        mainMenu.SetActive(false);
        inGameMenu.SetActive(false);
        gameOverMenu.SetActive(true);
        pauseMenu.SetActive(false);
    }
}
