using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    // stores the instructions panel
    public GameObject instructionsPanel;

    // opens the game scene
    public void PlayGame()
    {
        SceneManager.LoadScene("GameScene");
    }

    // shows the instructions panel
    public void OpenInstructions()
    {
        instructionsPanel.SetActive(true);
    }

    // hides the instructions panel
    public void CloseInstructions()
    {
        instructionsPanel.SetActive(false);
    }

    // closes the game
    public void QuitGame()
    {
        Application.Quit();
    }
}