using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class GameManager : MonoBehaviour
{
    // Keep the reference to the spawner
    public EnemySpawner enemySpawner;

    public TextMeshProUGUI infoTextUI;
    public GameObject infoPanelUI;

    public List<string> triggeredEvents = new List<string>();
    public List<string> triggeredEventsOrder = new List<string>(); // Keep for sequence checks if needed
    public List<GameObject> activeEnemies = new List<GameObject>(); // We'll repopulate this

    public static GameManager Instance { get; private set; }

    // Removed: firstAreaTypeDecisionMade flag
    private TutorialManager tutorialManager;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple GameManagers found. Destroying the extra.");
            Destroy(gameObject);
            return; // Added return to prevent further execution on destroyed object
        }

        // Find Enemy Spawner if not assigned
        if (enemySpawner == null)
        {
            enemySpawner = FindAnyObjectByType<EnemySpawner>();
            if (enemySpawner == null)
            {
                Debug.LogError("EnemySpawner not found in the scene and not assigned!", this);
            }
        }

        // Find Tutorial Manager
        tutorialManager = FindAnyObjectByType<TutorialManager>();
        if (tutorialManager == null)
        {
            // This might not be an error if tutorials are optional
            // Debug.LogError("TutorialManager not found in the scene!", this);
            Debug.LogWarning("TutorialManager not found in the scene. Tutorial events will be ignored.", this);
        }
    }

    public void RegisterTriggeredEvent(string eventName)
    {
        // --- Simplified Event Registration ---
        if (!triggeredEvents.Contains(eventName))
        {
            triggeredEvents.Add(eventName);
        }
        // Keep track of order if needed for complex logic
        triggeredEventsOrder.Add(eventName);
        Debug.Log("GameManager: Event Triggered: " + eventName);
        // --- End Simplified ---

        ProcessSingleTriggeredEvent(eventName); // Process immediately
    }

    // Process the most recent event immediately
    private void ProcessSingleTriggeredEvent(string eventName)
    {
        if (int.TryParse(eventName, out int areaNumber)) // Check if it's an area number (expecting 1-based from trigger name)
        {
            if (enemySpawner == null)
            {
                Debug.LogError("Cannot process area trigger: EnemySpawner reference is missing!", this);
                return;
            }

            int areaIndex = areaNumber - 1; // Convert to 0-based index for the list

            // ----- REMOVED LOGIC BLOCK -----
            // The EnemySpawner no longer needs SetAreaEnemyTypes.
            // The spawning logic is entirely handled within EnemySpawner.SpawnEnemiesForArea.
            // Removed: if (!firstAreaTypeDecisionMade) { ... enemySpawner.SetAreaEnemyTypes(...) ... }
            // -------------------------------

            // --- Trigger Spawning for the specific area ---
            // This will now attempt to spawn ALL enemies for the area if not already spawned.
            Debug.Log($"GameManager: Requesting spawn for area index {areaIndex} (Trigger: {areaNumber}).");
            enemySpawner.SpawnEnemiesForArea(areaIndex);
            // --- End Trigger Spawning ---
        }
        else if (eventName.StartsWith("Tutorial")) // Check for Tutorial events
        {
            if (tutorialManager != null)
            {
                string tutorialID = eventName.Substring(8); // Get the part after "Tutorial"
                tutorialManager.ShowTutorial(tutorialID);
            }
            else
            {
                // Warning logged in Awake if manager is missing
            }
        }
        else // Handle other named events
        {
            switch (eventName)
            {
                case "Info_Pickup_Item":
                    ShowInfoMessage("Press E to interact with items.");
                    break;
                // Add other specific named events here
                default:
                    Debug.Log($"GameManager: Received unhandled named event: {eventName}");
                    break;
            }
        }

        // Optional: Logic based on sequence of events (kept from original)
        CheckEventSequence();
    }

    // Optional: Keep if you need complex logic based on trigger order
    private void CheckEventSequence()
    {
        // This logic remains unchanged, it just checks the order triggers were hit
        if (triggeredEventsOrder.Count >= 2)
        {
            if (triggeredEventsOrder[0] == "1" && triggeredEventsOrder[1] == "2")
            {
                Debug.Log("GameManager Sequence Check: First triggered area was 1, then 2.");
                // Add specific logic here if needed
            }
            // Add other sequence checks if necessary
        }
    }


    // Method for EnemySpawner to call to register enemies
    public void RegisterSpawnedEnemy(GameObject enemyGO)
    {
        if (enemyGO != null && !activeEnemies.Contains(enemyGO))
        {
            activeEnemies.Add(enemyGO);
            // Debug.Log($"GameManager: Registered spawned enemy {enemyGO.name}. Total active: {activeEnemies.Count}"); // Less verbose
        }
    }


    private void ShowInfoMessage(string message)
    {
        Debug.Log("Showing Info: " + message);
        if (infoTextUI != null && infoPanelUI != null)
        {
            infoTextUI.text = message;
            infoPanelUI.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Info UI elements not assigned in GameManager!", this);
        }
    }

    // Keep this: Called by BaseEnemy when it dies
    public void EnemyDied(GameObject deadEnemy)
    {
        // Debug.Log("GameManager: Enemy Died: " + deadEnemy.name); // Less verbose
        if (activeEnemies.Contains(deadEnemy))
        {
            activeEnemies.Remove(deadEnemy);
            // Debug.Log($"GameManager: Removed enemy. Total active remaining: {activeEnemies.Count}"); // Less verbose
        }
        // else // This warning might be noisy if enemies are destroyed for other reasons
        // {
        //     Debug.LogWarning($"GameManager: Tried to remove enemy {deadEnemy.name} but it wasn't in the active list.", deadEnemy);
        // }
        // Add any logic here needed when an enemy dies (e.g., check if area clear via AreaCleansingManager counts)
    }
}