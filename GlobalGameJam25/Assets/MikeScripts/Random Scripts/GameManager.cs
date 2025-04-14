using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public GameObject sludgePrefab;
    public GameObject gasPrefab;

    public Transform spawnArea1Center;
    public float spawnArea1Radius = 15f;
    public int numSludgeToSpawn = 5;

    public Transform spawnArea2Center;
    public float spawnArea2Radius = 15f;
    public int numGasToSpawn = 5;

    public List<string> triggeredEvents = new List<string>();
    public List<string> triggeredEventsOrder = new List<string>();

    public static GameManager Instance { get; private set; }

    private bool area1SludgeSpawned = false;
    private bool area2GasSpawned = false;

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
    }

    public void RegisterTriggeredEvent(string eventName)
    {
        if (!triggeredEvents.Contains(eventName))
        {
            triggeredEvents.Add(eventName);
            triggeredEventsOrder.Add(eventName);
            Debug.Log("Event Triggered: " + eventName);
            ProcessTriggeredEvents();
        }
        else
        {
            triggeredEventsOrder.Add(eventName); // Still record the order if needed
        }
    }

    private void ProcessTriggeredEvents()
    {
        foreach (string eventName in triggeredEventsOrder)
        {
            switch (eventName)
            {
                case "Spawn_Sludge_Area1":
                    if (!area1SludgeSpawned && sludgePrefab != null && spawnArea1Center != null)
                    {
                        SpawnEnemies(sludgePrefab, spawnArea1Center.position, spawnArea1Radius, numSludgeToSpawn);
                        area1SludgeSpawned = true;
                        Debug.Log("GameManager: Spawning Sludge in Area 1.");
                    }
                    break;
                case "Spawn_Gas_Area2":
                    if (!area2GasSpawned && gasPrefab != null && spawnArea2Center != null)
                    {
                        SpawnEnemies(gasPrefab, spawnArea2Center.position, spawnArea2Radius, numGasToSpawn);
                        area2GasSpawned = true;
                        Debug.Log("GameManager: Spawning Gas in Area 2.");
                    }
                    break;
                case "Tutorial_Movement":
                    ShowTutorialMessage("Welcome! Use WASD to move.");
                    break;
                case "Info_Pickup_Item":
                    ShowInfoMessage("Press E to interact with items.");
                    break;
                // Add more cases for other events
            }
        }

        // You might also want logic that checks the *order* of these events
        if (triggeredEventsOrder.Count >= 2)
        {
            if (triggeredEventsOrder[0] == "Tutorial_Movement" && triggeredEventsOrder[1] == "Spawn_Sludge_Area1")
            {
                Debug.Log("GameManager: Movement tutorial followed by entering the first enemy area.");
                // Perform some other action based on the order
            }
        }

        // You might also want logic that checks for combinations of triggered events
        if (triggeredEvents.Contains("Tutorial_Movement") && triggeredEvents.Contains("Info_Pickup_Item"))
        {
            // Do something when both events have occurred
        }
    }

    private void SpawnEnemies(GameObject enemyPrefab, Vector3 center, float radius, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 randomPosition = GetRandomPointInCircle(center, radius);
            Instantiate(enemyPrefab, randomPosition, Quaternion.identity);
        }
    }

    Vector3 GetRandomPointInCircle(Vector3 center, float radius)
    {
        float angle = Random.Range(0f, Mathf.PI * 2);
        float u = Random.Range(0f, 1f) + Random.Range(0f, 1f);
        float r = radius * (u > 1 ? 2 - u : u);
        return new Vector3(center.x + r * Mathf.Cos(angle), center.y + 1f, center.z + r * Mathf.Sin(angle));
    }

    // Methods to show messages
    private void ShowTutorialMessage(string message)
    {
        Debug.Log("Showing Tutorial: " + message);
        // **YOUR UI CODE TO DISPLAY THE TUTORIAL MESSAGE HERE**
        // This might involve finding a UI element and updating its text.
    }

    private void ShowInfoMessage(string message)
    {
        Debug.Log("Showing Info: " + message);
        // **YOUR UI CODE TO DISPLAY THE INFO MESSAGE HERE**
        // This might involve finding a different UI element or handling a different UI flow.
    }
}