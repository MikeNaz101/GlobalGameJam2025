using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public string gameSceneName = "GameScene";

    [Header("Main Menu Elements")]
    public GameObject mainMenuElementsContainer; 

    [Header("Credits Elements")]
    public GameObject creditsScrollView;   
    public GameObject creditsBackButton;    


    void Start()
    {
        // Initial setup: Show main menu items, hide credits items
        mainMenuElementsContainer.SetActive(true);
        creditsScrollView.SetActive(false);
        creditsBackButton.SetActive(false);

        // Ensure cursor is visible and unlocked
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // --- Button Methods ---

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
        Debug.Log("Starting Game: " + gameSceneName);
    }

    // This function now toggles between showing main menu elements and credits elements
    public void ToggleCredits()
    {
        // Determine if credits are currently shown by checking the scroll view's state
        bool isCreditsCurrentlyActive = creditsScrollView.activeSelf;

        // Calculate the new state
        bool showCredits = !isCreditsCurrentlyActive;

        // Toggle main menu elements container (opposite of credits state)
        mainMenuElementsContainer.SetActive(!showCredits);

        // Toggle Credits Scroll View
        creditsScrollView.SetActive(showCredits);

        // Toggle Credits Back Button
        creditsBackButton.SetActive(showCredits);


        // Update debug log
        if (showCredits) {
            Debug.Log("Showing Credits View");
        } else {
            Debug.Log("Showing Main Menu View");
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