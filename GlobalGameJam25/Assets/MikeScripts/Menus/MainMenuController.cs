using UnityEngine;
using UnityEngine.SceneManagement; // Required for scene management

public class MainMenuController : MonoBehaviour
{
    public string gameSceneName = "GameScene"; // Set the exact name of your main game scene
    public GameObject creditsPanel;         // Assign your Credits Panel/CanvasGroup here

    void Start()
    {
        // Ensure credits panel is hidden at start if it exists
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(false);
        }

        // Ensure cursor is visible and unlocked in the main menu
        Time.timeScale = 1f; // Just in case it was paused
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // --- Public Methods for UI Buttons ---

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
        Debug.Log("Starting Game: " + gameSceneName);
    }

    public void ShowCredits()
    {
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(true);
            Debug.Log("Showing Credits");
        }
        else
        {
            Debug.LogError("Credits Panel not assigned in the Inspector!");
        }
    }

    public void HideCredits()
    {
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(false);
            Debug.Log("Hiding Credits");
        }
         else
        {
            Debug.LogError("Credits Panel not assigned in the Inspector!");
        }
    }

    public void QuitGame()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();

        // If running in the Unity Editor, stop playing
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}