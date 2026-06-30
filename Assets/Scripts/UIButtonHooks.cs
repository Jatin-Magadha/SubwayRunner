using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple bridge between UI Buttons and GameManager calls.
/// Attach to a UI Canvas object and wire buttons' OnClick() in the Inspector
/// to these public methods (e.g. PlayButton -> OnPlayPressed).
/// </summary>
public class UIButtonHooks : MonoBehaviour
{
    public void OnPlayPressed()
    {
        GameManager.Instance.StartGame();
    }

    public void OnRestartPressed()
    {
        GameManager.Instance.RestartGame();
    }

    public void OnMainMenuPressed()
    {
        GameManager.Instance.QuitToMenu();
    }

    public void OnQuitPressed()
    {
        Application.Quit();
    }
}