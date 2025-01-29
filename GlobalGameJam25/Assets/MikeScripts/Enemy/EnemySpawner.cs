using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;  // Reference to the enemy prefab
    public float spawnRange = 20f;  // Range around the player where enemies can spawn
    public float spawnHeight = 10f; // Height from which the enemy will spawn (above the player)
    public float spawnInterval = 5f; // Time interval between enemy spawns
    public Transform player;       // Reference to the player's transform

    void Start()
    {
        // Find the player object (assuming the player has a "Player" tag)
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // Start the spawn loop
        if (player != null)
        {
            InvokeRepeating("SpawnEnemy", 0f, spawnInterval); // Spawn enemies at regular intervals
        }
        else
        {
            Debug.LogError("Player not found!");
        }
    }

    void SpawnEnemy()
    {

        // Generate a random position within the spawn range
        Vector3 randomPosition = player.position + new Vector3(
            Random.Range(-spawnRange, spawnRange),
            spawnHeight,  // Spawn the enemy high in the air
            Random.Range(-spawnRange, spawnRange)
        );

        // Instantiate the enemy prefab at the calculated position
        GameObject enemy = Instantiate(enemyPrefab, randomPosition, Quaternion.identity);

        // You can also add additional setup for the enemy here if needed
        // For example, setting the enemy's target to the player or initializing other properties
    }
}
