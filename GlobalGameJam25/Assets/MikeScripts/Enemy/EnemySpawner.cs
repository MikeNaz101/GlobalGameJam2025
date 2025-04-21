using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    public GameObject sludgePrefab;
    public GameObject gasPrefab;

    [Header("Spawn Area 1 Settings")]
    public Transform spawnArea1Center;
    public float spawnArea1Radius = 15f;
    public int numSludgeToSpawnArea1 = 5;
    public int numGasToSpawnArea1 = 5;
    [Tooltip("Drag the AreaCleansingManager GameObject for Area 1 here.")]
    public AreaCleansingManager area1Manager;

    [Header("Spawn Area 2 Settings")]
    public Transform spawnArea2Center;
    public float spawnArea2Radius = 15f;
    public int numSludgeToSpawnArea2 = 5;
    public int numGasToSpawnArea2 = 5;
    [Tooltip("Drag the AreaCleansingManager GameObject for Area 2 here.")]
    public AreaCleansingManager area2Manager;

    // Internal Logic Variables
    private bool area1IsSludgeFirst = false;
    private bool firstAreaTypeSet = false;
    private bool area1SludgeSpawned = false;
    private bool area1GasSpawned = false;
    private bool area2SludgeSpawned = false;
    private bool area2GasSpawned = false;

    // Call this early (e.g., from GameManager) to decide initial enemy distribution
    public void SetArea1EnemyType(bool spawnSludgeFirstInArea1)
    {
        if (!firstAreaTypeSet)
        {
            area1IsSludgeFirst = spawnSludgeFirstInArea1;
            firstAreaTypeSet = true;
            Debug.Log($"EnemySpawner: Area 1 will spawn {(spawnSludgeFirstInArea1 ? "Sludge" : "Gas")} first.");
        }
    }

    // Call this to trigger spawning for a specific area number (1 or 2)
    public void SpawnEnemiesForArea(int areaNumber)
    {
        if (!firstAreaTypeSet)
        {
            Debug.LogError("EnemySpawner: Initial area enemy type not set! Call SetArea1EnemyType first.");
            return;
        }
        if (area1Manager == null) {
            Debug.LogError("EnemySpawner: Area 1 Manager is not assigned in the Inspector!", this);
            return;
        }
         if (area2Manager == null) {
            Debug.LogError("EnemySpawner: Area 2 Manager is not assigned in the Inspector!", this);
            return;
        }

        if (areaNumber == 1)
        {
            if (area1IsSludgeFirst && !area1SludgeSpawned)
            {
                Debug.Log("EnemySpawner: Spawning Sludge in Area 1.");
                SpawnSludgeEnemies(spawnArea1Center.position, spawnArea1Radius, numSludgeToSpawnArea1, area1Manager);
                area1SludgeSpawned = true;
            }
            else if (!area1IsSludgeFirst && !area1GasSpawned)
            {
                Debug.Log("EnemySpawner: Spawning Gas in Area 1.");
                SpawnGasEnemies(spawnArea1Center.position, spawnArea1Radius, numGasToSpawnArea1, area1Manager);
                area1GasSpawned = true;
            }
            else { Debug.Log($"EnemySpawner: Area 1 ({ (area1IsSludgeFirst ? "Sludge" : "Gas") }) already spawned."); }
        }
        else if (areaNumber == 2)
        {
            if (!area1IsSludgeFirst && !area2SludgeSpawned)
            {
                 Debug.Log("EnemySpawner: Spawning Sludge in Area 2.");
                SpawnSludgeEnemies(spawnArea2Center.position, spawnArea2Radius, numSludgeToSpawnArea2, area2Manager);
                area2SludgeSpawned = true;
            }
            else if (area1IsSludgeFirst && !area2GasSpawned)
            {
                Debug.Log("EnemySpawner: Spawning Gas in Area 2.");
                SpawnGasEnemies(spawnArea2Center.position, spawnArea2Radius, numGasToSpawnArea2, area2Manager);
                area2GasSpawned = true;
            }
             else { Debug.Log($"EnemySpawner: Area 2 ({ (!area1IsSludgeFirst ? "Sludge" : "Gas") }) already spawned."); }
        }
        else { Debug.LogError($"EnemySpawner: Invalid area number provided: {areaNumber}. Use 1 or 2."); }
    }

    // Spawns Sludge enemies and assigns the correct Area Manager
    private void SpawnSludgeEnemies(Vector3 center, float radius, int count, AreaCleansingManager managerToAssign)
    {
        if (sludgePrefab == null) { Debug.LogError("Sludge Prefab not assigned to spawner!", this); return; }
        if (managerToAssign == null) { Debug.LogError("Attempted to spawn Sludge enemies but managerToAssign was null!", this); return; }

        Debug.Log($"Attempting to spawn {count} Sludge enemies for manager: {managerToAssign.gameObject.name}");
        for (int i = 0; i < count; i++)
        {
            Vector3 randomPosition = GetRandomPointInCircle(center, radius);
            GameObject enemyGO = Instantiate(sludgePrefab, randomPosition, Quaternion.identity);

            // --- Assign the Manager using GetComponentInChildren ---
            BaseEnemy enemyScript = enemyGO.GetComponentInChildren<BaseEnemy>(); // Use InChildren!
            if (enemyScript != null)
            {
                enemyScript.myAreaManager = managerToAssign;
                 // Log the specific object where the script was found
                 Debug.Log($"Found BaseEnemy script on child object '{enemyScript.gameObject.name}' of instance '{enemyGO.name}'. Assigning manager '{managerToAssign.gameObject.name}'.");
            }
            else
            {
                Debug.LogError($"Spawned Sludge enemy '{enemyGO.name}' is missing BaseEnemy script on itself AND all children!", enemyGO);
            }
            // -------------------------------------------------------

            // Register with GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterSpawnedEnemy(enemyGO);
            }
            else { Debug.LogWarning("GameManager instance not found when trying to register spawned enemy.", enemyGO); }
        }
    }

    // Spawns Gas enemies and assigns the correct Area Manager
    private void SpawnGasEnemies(Vector3 center, float radius, int count, AreaCleansingManager managerToAssign)
    {
         if (gasPrefab == null) { Debug.LogError("Gas Prefab not assigned to spawner!", this); return; }
         if (managerToAssign == null) { Debug.LogError("Attempted to spawn Gas enemies but managerToAssign was null!", this); return; }

        Debug.Log($"Attempting to spawn {count} Gas enemies for manager: {managerToAssign.gameObject.name}");
        for (int i = 0; i < count; i++)
        {
            Vector3 randomPosition = GetRandomPointInCircle(center, radius);
            // Define the desired rotation (90 degrees on X, 0 on Y, 0 on Z)
            Quaternion desiredRotation = Quaternion.Euler(180f, 0f, 90f);

            // Instantiate with the desired rotation
            GameObject enemyGO = Instantiate(sludgePrefab, randomPosition, desiredRotation);

            // --- Assign the Manager using GetComponentInChildren ---
            BaseEnemy enemyScript = enemyGO.GetComponentInChildren<BaseEnemy>(); // Use InChildren!
            if (enemyScript != null)
            {
                enemyScript.myAreaManager = managerToAssign;
                // Log the specific object where the script was found
                Debug.Log($"Found BaseEnemy script on child object '{enemyScript.gameObject.name}' of instance '{enemyGO.name}'. Assigning manager '{managerToAssign.gameObject.name}'.");
            }
            else
            {
                  Debug.LogError($"Spawned Gas enemy '{enemyGO.name}' is missing BaseEnemy script on itself AND all children!", enemyGO);
             }
             // -------------------------------------------------------

             // Register with GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterSpawnedEnemy(enemyGO);
            }
             else { Debug.LogWarning("GameManager instance not found when trying to register spawned enemy.", enemyGO); }
        }
    }

    // Utility Method to find a point on the ground within the radius
    public Vector3 GetRandomPointInCircle(Vector3 center, float radius)
    {
        for(int attempt = 0; attempt < 5; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2);
            float u = Random.Range(0f, 1f) + Random.Range(0f, 1f);
            float r = radius * (u > 1 ? 2 - u : u);
            Vector3 potentialPos = new Vector3(center.x + r * Mathf.Cos(angle), center.y + 10f, center.z + r * Mathf.Sin(angle));

            RaycastHit hit;
            if (Physics.Raycast(potentialPos, Vector3.down, out hit, 20f))
            {
                return hit.point + Vector3.up * 0.5f;
            }
        }
        Debug.LogWarning("Could not find suitable ground point for enemy spawn near " + center + ", using center point plus offset.");
        return center + Vector3.up * 0.5f;
    }
}