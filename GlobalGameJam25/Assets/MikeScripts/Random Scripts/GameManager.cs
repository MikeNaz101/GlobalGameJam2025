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
    public List<string> triggeredEventsOrder = new List<string>();
    public List<GameObject> activeEnemies = new List<GameObject>(); // We'll repopulate this

    public static GameManager Instance { get; private set; }

    private bool firstAreaTypeDecisionMade = false; // Renamed for clarity
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
            Debug.LogError("TutorialManager not found in the scene!", this);
        }
    }

    public void RegisterTriggeredEvent(string eventName)
    {
        // --- Simplified Event Registration ---
        if (!triggeredEvents.Contains(eventName))
        {
            triggeredEvents.Add(eventName);
        }
        // Keep track of order if needed for complex logic, otherwise might not be necessary
        triggeredEventsOrder.Add(eventName);
        Debug.Log("GameManager: Event Triggered: " + eventName);
        // --- End Simplified ---

        ProcessSingleTriggeredEvent(eventName); // Process immediately
    }

    // Process the most recent event immediately
    private void ProcessSingleTriggeredEvent(string eventName)
    {
        if (int.TryParse(eventName, out int areaNumber)) // Check if it's an area number (1-based now)
        {
            if (enemySpawner == null)
            {
                Debug.LogError("Cannot process area trigger: EnemySpawner reference is missing!", this);
                return;
            }

            int areaIndex = areaNumber - 1; // Convert to 0-based index for the list

            // --- Set Initial Enemy Type Distribution (Only Once) ---
            if (!firstAreaTypeDecisionMade)
            {
                // Example Logic: Determine initial spawn types based on the order areas are triggered.
                // You might want more sophisticated logic here based on your game design.
                List<bool> initialSpawnTypes = new List<bool>();
                for (int i = 0; i < enemySpawner.spawnAreas.Count; i++)
                {
                    // Example: First triggered area gets Sludge, others get Gas initially.
                    initialSpawnTypes.Add(i == areaIndex);
                }
                enemySpawner.SetAreaEnemyTypes(initialSpawnTypes);
                firstAreaTypeDecisionMade = true;
                Debug.Log($"GameManager: First area triggered was {areaNumber}. Setting initial enemy distribution.");
            }
            // --- End Set Initial Type ---

            // --- Trigger Spawning for the specific area ---
            Debug.Log($"GameManager: Requesting spawn for area {areaNumber}.");
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
                Debug.LogWarning("Cannot show tutorial, TutorialManager reference is missing!", this);
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


    // --- REMOVED OLD SpawnEnemies METHOD ---
    // The EnemySpawner script now handles instantiation and Area Manager assignment.

    // --- NEW Method for EnemySpawner to call ---
    // This allows GameManager to keep track of active enemies if needed.
    public void RegisterSpawnedEnemy(GameObject enemyGO)
    {
        if (enemyGO != null && !activeEnemies.Contains(enemyGO))
        {
            activeEnemies.Add(enemyGO);
            Debug.Log($"GameManager: Registered spawned enemy {enemyGO.name}. Total active: {activeEnemies.Count}");

            // Optional: Explicitly assign gameManager reference here if needed,
            // though BaseEnemy's Start() method should find it anyway.
            // BaseEnemy enemyScript = enemyGO.GetComponent<BaseEnemy>();
            // if (enemyScript != null && enemyScript.gameManager == null)
            // {
            //     enemyScript.gameManager = this;
            // }
        }
    }
    // --- End New Method ---


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
            Debug.LogError("Info UI elements not assigned in GameManager!", this);
        }
    }

    // Keep this: Called by BaseEnemy when it dies
    public void EnemyDied(GameObject deadEnemy)
    {
        Debug.Log("GameManager: Enemy Died: " + deadEnemy.name);
        if (activeEnemies.Contains(deadEnemy))
        {
            activeEnemies.Remove(deadEnemy);
            Debug.Log($"GameManager: Removed enemy. Total active remaining: {activeEnemies.Count}");
        }
        else
        {
            Debug.LogWarning($"GameManager: Tried to remove enemy {deadEnemy.name} but it wasn't in the active list.", deadEnemy);
        }
        // Add any logic here needed when an enemy dies (e.g., check if area clear)
    }

    // --- REMOVED OLD SpawnEnemiesForArea METHOD ---
    // Calling enemySpawner.SpawnEnemiesForArea directly is done in ProcessSingleTriggeredEvent now.
}