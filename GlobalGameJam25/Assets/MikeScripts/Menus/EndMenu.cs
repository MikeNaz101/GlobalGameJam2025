using UnityEngine;
using UnityEngine.SceneManagement; // Required for scene management
using UnityEngine.UI; // Optional: If you want to reference buttons directly

public class EndSceneMenu : MonoBehaviour
{
    [Header("Scene Configuration")]
    [Tooltip("The name of your main menu or start scene. Must match the scene name in Build Settings.")]
    public string startSceneName = "StartMenu"; // CHANGE "MainMenu" to your actual start scene name

    [Tooltip("The name of the scene to load when 'Restart' is clicked (e.g., the first game level). Must match the scene name in Build Settings.")]
    public string restartSceneName = "VinnySceneMikeRevised";

    public void LoadRestartScene() // Renamed for clarity
    {

        Debug.Log($"Loading Restart Scene: {restartSceneName}");

        // Load the scene using the name provided in the Inspector
        SceneManager.LoadScene(restartSceneName);
    }
    public void GoToStartScene()
    {

        Debug.Log($"Loading Start Scene: {startSceneName}");

        // Load the scene using the name provided in the Inspector
        SceneManager.LoadScene(startSceneName);
    }
    void Start()
    {
        // Ensure cursor is visible and unlocked
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}