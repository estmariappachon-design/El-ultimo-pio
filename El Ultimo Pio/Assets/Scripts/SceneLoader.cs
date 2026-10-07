using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void LoadGameplay()
    {
        SceneManager.LoadScene("Gameplay");
    }

    public void LoadLobby()
    {
        SceneManager.LoadScene("Lobby");
    }

    public void LoadResults()
    {
        SceneManager.LoadScene("Results");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}