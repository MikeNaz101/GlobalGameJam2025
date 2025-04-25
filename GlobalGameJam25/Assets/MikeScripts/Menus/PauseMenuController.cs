using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
// using UnityEngine.Audio;

public class PauseMenuController : MonoBehaviour
{
    public GameObject pauseMenuUI;
    public Slider volumeSlider;
    public string mainMenuSceneName = "MainMenu";

    // Make a public static property to track pause state globally
    public static bool GameIsPaused { get; private set; } // Other scripts can read this

    // private bool isPaused = false; // We can now rely solely on the static variable

    void Start()
    {
        // Ensure game starts unpaused
        GameIsPaused = false; // Set static state
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (pauseMenuUI != null) {
            pauseMenuUI.SetActive(false);
        } else {
            Debug.LogError("Pause Menu UI not assigned in the Inspector!");
        }

        if (volumeSlider != null) {
            volumeSlider.value = AudioListener.volume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        } else {
             Debug.LogWarning("Volume Slider not assigned in the Inspector. Volume control disabled.");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameIsPaused) // Check the static state
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
        if (pauseMenuUI == null) return;

        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameIsPaused = true; // Set static state
        Debug.Log("Game Paused");
    }

    public void ResumeGame()
    {
        if (pauseMenuUI == null) return;

        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        GameIsPaused = false; // Set static state
        Debug.Log("Game Resumed");
    }

    public void RestartScene()
    {
        Time.timeScale = 1f;
        GameIsPaused = false; // Ensure state is correct before reload
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
        Debug.Log("Restarting Scene: " + currentScene.name);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
         GameIsPaused = false; // Ensure state is correct before reload
        SceneManager.LoadScene(mainMenuSceneName);
        Debug.Log("Loading Main Menu: " + mainMenuSceneName);
    }

    public void SetVolume(float volume)
    {
        AudioListener.volume = volume;
    }

     public void QuitGame()
     {
         Debug.Log("Quitting Game from Pause Menu...");
         GameIsPaused = false; // Reset state just in case
         Application.Quit();
         #if UNITY_EDITOR
         UnityEditor.EditorApplication.isPlaying = false;
         #endif
     }
}