using UnityEngine;
using UnityEngine.SceneManagement; // Required for scene management
using UnityEngine.UI;           // Required for Slider
// using UnityEngine.Audio;      // Optional: Add if using Audio Mixers

public class PauseMenuController : MonoBehaviour
{
    public GameObject pauseMenuUI;        // Assign your main Pause Menu Panel/CanvasGroup here in the Inspector
    public Slider volumeSlider;           // Assign your volume Slider UI element here
    public string mainMenuSceneName = "MainMenu"; // Set the exact name of your main menu scene

    private bool isPaused = false;

    void Start()
    {
        // Ensure the pause menu is hidden at the start
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }
        else
        {
            Debug.LogError("Pause Menu UI not assigned in the Inspector!");
        }

        // Initialize the slider's value to the current game volume
        if (volumeSlider != null)
        {
            volumeSlider.value = AudioListener.volume;
            // Add a listener to call SetVolume when the slider value changes
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
        else
        {
             Debug.LogWarning("Volume Slider not assigned in the Inspector. Volume control disabled.");
        }

        // Ensure game starts unpaused (in case we came from a paused state)
        Time.timeScale = 1f;
        // Ensure cursor is locked and hidden for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Check for the pause button press (Enter key)
        if (Input.GetKeyDown(KeyCode.Return)) // Or KeyCode.Escape
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    void PauseGame()
    {
        if (pauseMenuUI == null) return; // Safety check

        pauseMenuUI.SetActive(true);    // Show the pause menu
        Time.timeScale = 0f;            // Freeze game time
        Cursor.lockState = CursorLockMode.None; // Unlock the cursor
        Cursor.visible = true;          // Make the cursor visible
        isPaused = true;
        Debug.Log("Game Paused");
    }

    // --- Public Methods for UI Buttons ---

    public void ResumeGame()
    {
        if (pauseMenuUI == null) return; // Safety check

        pauseMenuUI.SetActive(false);   // Hide the pause menu
        Time.timeScale = 1f;            // Resume game time
        Cursor.lockState = CursorLockMode.Locked; // Lock the cursor
        Cursor.visible = false;         // Hide the cursor
        isPaused = false;
        Debug.Log("Game Resumed");
    }

    public void RestartScene()
    {
        // IMPORTANT: Unpause time before loading the scene
        Time.timeScale = 1f;
        // Get the current scene's build index and reload it
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
        Debug.Log("Restarting Scene: " + currentScene.name);
    }

    public void LoadMainMenu()
    {
        // IMPORTANT: Unpause time before loading the scene
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
        Debug.Log("Loading Main Menu: " + mainMenuSceneName);
    }

    // --- Public Method for Volume Slider ---

    public void SetVolume(float volume)
    {
        AudioListener.volume = volume; // Set the global volume
        // If using AudioMixers, you would set a parameter here instead:
        // audioMixer.SetFloat("MasterVolume", Mathf.Log10(volume) * 20); // Example for decibels
        // Debug.Log("Volume set to: " + volume);
    }

     // Optional: Add a Quit Game button directly to the pause menu
     public void QuitGame()
     {
         Debug.Log("Quitting Game from Pause Menu...");
         Application.Quit();

         // If running in the Unity Editor, stop playing
         #if UNITY_EDITOR
         UnityEditor.EditorApplication.isPlaying = false;
         #endif
     }
}