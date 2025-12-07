using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public void OnStartGame()
    {
        SceneManager.LoadScene("gameplay_scene");
    }

    public void OnStartSandbox()
    {
        SceneManager.LoadScene("sandbox");
    }
    public void OnQuitGame()
    {
        Application.Quit();
    }
}
