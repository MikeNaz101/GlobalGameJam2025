using UnityEngine;
using System.Collections.Generic; // Add this line for List

public class EnemySpawner1 : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float spawnRange = 20f;
    public float spawnHeight = 10f;
    public float spawnInterval = 5f;
    public Transform player;

    private List<GameObject> activeEnemies = new List<GameObject>(); // List to track active enemies
    private int maxEnemies = 10; // Maximum number of enemies

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player != null)
        {
            InvokeRepeating("SpawnEnemy", 0f, spawnInterval);
        }
        else
        {
            Debug.LogError("Player not found!");
        }
    }

    public void SpawnEnemy()
    {
        if (activeEnemies.Count < maxEnemies)
        {
            Vector3 randomPosition = player.position + new Vector3(
                Random.Range(-spawnRange, spawnRange),
                spawnHeight,
                Random.Range(-spawnRange, spawnRange)
            );

            GameObject enemy = Instantiate(enemyPrefab, randomPosition, Quaternion.identity);
            activeEnemies.Add(enemy); // Add the spawned enemy to the list

            // You can add additional setup for the enemy here if needed
        }
    }

    // Call this function when an enemy dies
    public void EnemyDied(GameObject deadEnemy)
    {
        activeEnemies.Remove(deadEnemy); // Remove the dead enemy from the list
    }
}