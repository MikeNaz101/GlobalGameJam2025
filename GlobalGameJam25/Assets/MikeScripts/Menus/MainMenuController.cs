using UnityEngine;
using UnityEngine.SceneManagement; // Required for scene management

public class MainMenuController : MonoBehaviour
{
    public string gameSceneName = "GameScene"; // Set the exact name of your main game scene

    [Header("UI Panels")] // Optional: Adds a header in the Inspector for organization
    public GameObject mainMenuPanel;        // Assign the Panel containing your main menu buttons (Start, Credits, Quit)
    public GameObject creditsPanel;         // Assign your Credits Panel here

    void Start()
    {
        // Ensure main menu is visible and credits are hidden at start
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }
        else
        {
             Debug.LogError("Main Menu Panel not assigned in the Inspector!");
        }

        if (creditsPanel != null)
        {
            creditsPanel.SetActive(false);
        }
        else
        {
             Debug.LogError("Credits Panel not assigned in the Inspector!");
        }

        // Ensure cursor is visible and unlocked in the main menu
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // --- Public Methods for UI Buttons ---

    public void StartGame()
    {
        // Optional: Ensure main menu is visible before starting, just in case
        // if(mainMenuPanel != null) mainMenuPanel.SetActive(true);
        // if(creditsPanel != null) creditsPanel.SetActive(false);
        SceneManager.LoadScene(gameSceneName);
        Debug.Log("Starting Game: " + gameSceneName);
    }

    // This function now toggles BOTH the credits panel and the main menu panel
    public void ToggleCredits()
    {
        // Safety check: ensure both panels are assigned before proceeding
        if (creditsPanel == null || mainMenuPanel == null)
        {
            Debug.LogError("Cannot toggle panels: Either Credits Panel or Main Menu Panel is not assigned in the Inspector!");
            return;
        }

        // Determine the new state for the credits panel
        bool shouldShowCredits = !creditsPanel.activeSelf;

        // Set the credits panel to the new state
        creditsPanel.SetActive(shouldShowCredits);
        // Set the main menu panel to the OPPOSITE state
        mainMenuPanel.SetActive(!shouldShowCredits);

        // Update debug log
        if (shouldShowCredits)
        {
            Debug.Log("Showing Credits, Hiding Main Menu");
        }
        else
        {
            Debug.Log("Hiding Credits, Showing Main Menu");
        }
    }

    public void QuitGame()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}