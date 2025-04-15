using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class GameManager : MonoBehaviour
{
    public EnemySpawner enemySpawner;

    public TextMeshProUGUI infoTextUI;
    public GameObject infoPanelUI;

    public List<string> triggeredEvents = new List<string>();
    public List<string> triggeredEventsOrder = new List<string>();
    public List<GameObject> activeEnemies = new List<GameObject>();

    public static GameManager Instance { get; private set; }

    private bool firstSpawnTriggered = false;
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
        }

        if (enemySpawner == null)
        {
            enemySpawner = FindAnyObjectByType<EnemySpawner>();
            if (enemySpawner == null)
            {
                Debug.LogError("EnemySpawner not found in the scene!");
            }
        }

        tutorialManager = FindAnyObjectByType<TutorialManager>();
        if (tutorialManager == null)
        {
            Debug.LogError("TutorialManager not found in the scene!");
        }
    }

    public void RegisterTriggeredEvent(string eventName)
    {
        if (!triggeredEvents.Contains(eventName))
        {
            triggeredEvents.Add(eventName);
        }
        triggeredEventsOrder.Add(eventName);
        Debug.Log("Event Triggered: " + eventName);
        ProcessTriggeredEvents();
    }

    private void ProcessTriggeredEvents()
    {
        if (triggeredEventsOrder.Count > 0)
        {
            string latestEventName = triggeredEventsOrder[triggeredEventsOrder.Count - 1]; // Get the last event

            if (int.TryParse(latestEventName, out int areaNumber))
            {
                if (!firstSpawnTriggered)
                {
                    enemySpawner?.SetFirstArea(areaNumber);
                    enemySpawner?.SpawnEnemiesForArea(areaNumber);
                    firstSpawnTriggered = true;
                }
                else
                {
                    enemySpawner?.SpawnEnemiesForArea(areaNumber);
                }
            }
            else if (latestEventName.StartsWith("Tutorial"))
            {
                string tutorialID = latestEventName.Substring(8);
                tutorialManager?.ShowTutorial(tutorialID);
            }
            else
            {
                switch (latestEventName)
                {
                    case "Info_Pickup_Item":
                        ShowInfoMessage("Press E to interact with items.");
                        break;
                }
            }
        }

        // You can still have logic based on the order of these events (if needed)
        if (triggeredEventsOrder.Count >= 2)
        {
            if (triggeredEventsOrder[0] == "1" && triggeredEventsOrder[1] == "2")
            {
                Debug.Log("First triggered area was 1, then 2.");
            }
        }
    }

    private void SpawnEnemies(GameObject enemyPrefab, Vector3 center, float radius, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 randomPosition = enemySpawner.GetRandomPointInCircle(center, radius);
            GameObject newEnemy = Instantiate(enemyPrefab, randomPosition, Quaternion.identity);

            BaseEnemy enemyComponent = newEnemy.GetComponent<BaseEnemy>();
            if (enemyComponent != null)
            {
                enemyComponent.gameManager = this;
                activeEnemies.Add(newEnemy);
            }
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
            Debug.LogError("Info UI elements not assigned!");
        }
    }

    public void EnemyDied(GameObject deadEnemy)
    {
        Debug.Log("GameManager: Enemy Died: " + deadEnemy.name);
        activeEnemies.Remove(deadEnemy);
    }

    private void SpawnEnemiesForArea(int areaNumber)
    {
        enemySpawner?.SpawnEnemiesForArea(areaNumber);
    }
}