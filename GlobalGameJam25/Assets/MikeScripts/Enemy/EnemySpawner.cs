using System.Collections.Generic;
using UnityEngine;

// SpawnAreaSettings class goes here (as modified above)
[System.Serializable]
public class SpawnAreaSettings
{
    [Header("Spawn Area Settings")]
    public Transform spawnAreaCenter;
    public float spawnAreaRadius = 15f;
    public int numSludgeToSpawn = 5; // How many Sludge to spawn in this area
    public int numGasToSpawn = 5;    // How many Gas to spawn in this area
    [Tooltip("Drag the AreaCleansingManager GameObject for this area here.")]
    public AreaCleansingManager areaManager;

    // Flag to track if enemies have been spawned for this area already
    [HideInInspector] public bool enemiesHaveSpawned = false;
}


public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    public GameObject sludgePrefab;
    public GameObject gasPrefab;

    [Header("Spawn Areas")]
    public List<SpawnAreaSettings> spawnAreas = new List<SpawnAreaSettings>();

    // Removed: firstAreaTypeSet flag
    // Removed: SetAreaEnemyTypes method


    // Call this to trigger spawning for a specific area index (0-based)
    // Assumes this is called by your trigger mechanism when the player enters the area collider.
    public void SpawnEnemiesForArea(int areaIndex)
    {
        // Removed: Check for firstAreaTypeSet

        if (areaIndex < 0 || areaIndex >= spawnAreas.Count)
        {
            Debug.LogError($"EnemySpawner: Invalid area index provided: {areaIndex}.");
            return;
        }

        SpawnAreaSettings currentArea = spawnAreas[areaIndex];

        // Check if enemies have already been spawned for this area
        if (currentArea.enemiesHaveSpawned)
        {
            Debug.Log($"EnemySpawner: Enemies already spawned for Area {areaIndex + 1}.");
            return; // Do nothing further
        }

        // --- Essential Checks ---
        if (currentArea.areaManager == null)
        {
            Debug.LogError($"EnemySpawner: Area Manager is not assigned for Area {areaIndex + 1} in the Inspector!", this);
            return;
        }
        if (currentArea.spawnAreaCenter == null)
        {
             Debug.LogError($"EnemySpawner: Spawn Area Center Transform is not assigned for Area {areaIndex + 1}!", this);
             return;
        }
        // Optional checks for prefabs if they might be null
        if (currentArea.numSludgeToSpawn > 0 && sludgePrefab == null)
        {
             Debug.LogError("EnemySpawner: Trying to spawn Sludge but Sludge Prefab is not assigned!", this);
             return;
        }
         if (currentArea.numGasToSpawn > 0 && gasPrefab == null)
        {
             Debug.LogError("EnemySpawner: Trying to spawn Gas but Gas Prefab is not assigned!", this);
             return;
        }
        // --- End Checks ---


        Debug.Log($"EnemySpawner: Spawning ALL enemies for Area {areaIndex + 1}.");

        // Spawn Sludge enemies if count > 0
        if (currentArea.numSludgeToSpawn > 0)
        {
            SpawnSludgeEnemies(currentArea.spawnAreaCenter.position, currentArea.spawnAreaRadius, currentArea.numSludgeToSpawn, currentArea.areaManager);
        }

        // Spawn Gas enemies if count > 0
        if (currentArea.numGasToSpawn > 0)
        {
             SpawnGasEnemies(currentArea.spawnAreaCenter.position, currentArea.spawnAreaRadius, currentArea.numGasToSpawn, currentArea.areaManager);
        }

        // Mark this area as having spawned its enemies
        currentArea.enemiesHaveSpawned = true;
    }

    // Spawns Sludge enemies and assigns the correct Area Manager
    private void SpawnSludgeEnemies(Vector3 center, float radius, int count, AreaCleansingManager managerToAssign)
    {
        // Prefab and manager null checks moved to SpawnEnemiesForArea

        Debug.Log($"Attempting to spawn {count} Sludge enemies for manager: {managerToAssign.gameObject.name}");
        for (int i = 0; i < count; i++)
        {
            Vector3 randomPosition = GetRandomPointInCircle(center, radius);
            GameObject enemyGO = Instantiate(sludgePrefab, randomPosition, Quaternion.identity);

            BaseEnemy enemyScript = enemyGO.GetComponentInChildren<BaseEnemy>();
            if (enemyScript != null)
            {
                // Ensure the BaseEnemy script has the myAreaManager field added previously
                if (enemyScript.GetType().GetField("myAreaManager") != null)
                {
                    enemyScript.myAreaManager = managerToAssign;
                } else {
                     Debug.LogError($"Spawned Sludge enemy '{enemyGO.name}' BaseEnemy script is missing the 'myAreaManager' field!", enemyGO);
                }
                // Debug.Log($"Found BaseEnemy script on child object '{enemyScript.gameObject.name}' of instance '{enemyGO.name}'. Assigning manager '{managerToAssign.gameObject.name}'."); // Less verbose
            }
            else
            {
                Debug.LogError($"Spawned Sludge enemy '{enemyGO.name}' is missing BaseEnemy script on itself AND all children!", enemyGO);
            }

            // Register with GameManager if it exists
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterSpawnedEnemy(enemyGO);
            }
            // else { Debug.LogWarning("GameManager instance not found when trying to register spawned enemy.", enemyGO); } // Less verbose
        }
    }

    // Spawns Gas enemies and assigns the correct Area Manager
    private void SpawnGasEnemies(Vector3 center, float radius, int count, AreaCleansingManager managerToAssign)
    {
         // Prefab and manager null checks moved to SpawnEnemiesForArea

        Debug.Log($"Attempting to spawn {count} Gas enemies for manager: {managerToAssign.gameObject.name}");
        for (int i = 0; i < count; i++)
        {
            Vector3 randomPosition = GetRandomPointInCircle(center, radius);
            // Consider if Gas enemies need a specific rotation
            Quaternion desiredRotation = Quaternion.identity; // Or specific rotation if needed
            // Quaternion desiredRotation = Quaternion.Euler(180f, 0f, 90f); // Old rotation

            GameObject enemyGO = Instantiate(gasPrefab, randomPosition, desiredRotation);

            BaseEnemy enemyScript = enemyGO.GetComponentInChildren<BaseEnemy>();
             if (enemyScript != null)
            {
                // Ensure the BaseEnemy script has the myAreaManager field added previously
                if (enemyScript.GetType().GetField("myAreaManager") != null)
                {
                    enemyScript.myAreaManager = managerToAssign;
                } else {
                     Debug.LogError($"Spawned Gas enemy '{enemyGO.name}' BaseEnemy script is missing the 'myAreaManager' field!", enemyGO);
                }
                // Debug.Log($"Found BaseEnemy script on child object '{enemyScript.gameObject.name}' of instance '{enemyGO.name}'. Assigning manager '{managerToAssign.gameObject.name}'."); // Less verbose
            }
            else
            {
                Debug.LogError($"Spawned Gas enemy '{enemyGO.name}' is missing BaseEnemy script on itself AND all children!", enemyGO);
            }

            // Register with GameManager if it exists
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterSpawnedEnemy(enemyGO);
            }
            // else { Debug.LogWarning("GameManager instance not found when trying to register spawned enemy.", enemyGO); } // Less verbose
        }
    }

    // Utility Method to find a point on the ground within the radius
    public Vector3 GetRandomPointInCircle(Vector3 center, float radius)
    {
        // Increased attempts slightly
        for (int attempt = 0; attempt < 10; attempt++)
        {
            // Simple random point within circle bounds
            Vector2 randomCirclePoint = Random.insideUnitCircle * radius;
            Vector3 potentialPos = center + new Vector3(randomCirclePoint.x, 0, randomCirclePoint.y);

            // Raycast down from slightly above to find ground
            float raycastHeight = 15f; // Height to cast down from
            RaycastHit hit;
            if (Physics.Raycast(potentialPos + Vector3.up * raycastHeight, Vector3.down, out hit, raycastHeight * 2f)) // Increased ray length
            {
                // Return point slightly above the ground hit
                return hit.point + Vector3.up * 0.5f; // Adjust offset as needed
            }
        }
        Debug.LogWarning("Could not find suitable ground point for enemy spawn near " + center + " after 10 attempts, using center point plus offset.");
        return center + Vector3.up * 0.5f; // Fallback
    }
}