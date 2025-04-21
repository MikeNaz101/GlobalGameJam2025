using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpawnAreaSettings
{
    [Header("Spawn Area Settings")]
    public Transform spawnAreaCenter;
    public float spawnAreaRadius = 15f;
    public int numSludgeToSpawn = 5;
    public int numGasToSpawn = 5;
    [Tooltip("Drag the AreaCleansingManager GameObject for this area here.")]
    public AreaCleansingManager areaManager;
    [HideInInspector] public bool sludgeSpawned = false;
    [HideInInspector] public bool gasSpawned = false;
    [HideInInspector] public bool spawnSludgeFirst = false; // Determined externally
}

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    public GameObject sludgePrefab;
    public GameObject gasPrefab;

    [Header("Spawn Areas")]
    public List<SpawnAreaSettings> spawnAreas = new List<SpawnAreaSettings>();

    private bool firstAreaTypeSet = false;

    // Call this early (e.g., from GameManager) to decide initial enemy distribution
    public void SetAreaEnemyTypes(List<bool> spawnSludgeFirstInAreas)
    {
        if (spawnSludgeFirstInAreas.Count != spawnAreas.Count)
        {
            Debug.LogError("EnemySpawner: The number of initial area types does not match the number of spawn areas!");
            return;
        }

        for (int i = 0; i < spawnAreas.Count; i++)
        {
            spawnAreas[i].spawnSludgeFirst = spawnSludgeFirstInAreas[i];
            Debug.Log($"EnemySpawner: Area {i + 1} will spawn {(spawnAreas[i].spawnSludgeFirst ? "Sludge" : "Gas")} first.");
        }
        firstAreaTypeSet = true;
    }

    // Call this to trigger spawning for a specific area index (0-based)
    public void SpawnEnemiesForArea(int areaIndex)
    {
        if (!firstAreaTypeSet)
        {
            Debug.LogError("EnemySpawner: Initial area enemy types not set! Call SetAreaEnemyTypes first.");
            return;
        }

        if (areaIndex < 0 || areaIndex >= spawnAreas.Count)
        {
            Debug.LogError($"EnemySpawner: Invalid area index provided: {areaIndex}.");
            return;
        }

        SpawnAreaSettings currentArea = spawnAreas[areaIndex];

        if (currentArea.areaManager == null)
        {
            Debug.LogError($"EnemySpawner: Area Manager is not assigned for Area {areaIndex + 1} in the Inspector!", this);
            return;
        }

        if (currentArea.spawnSludgeFirst && !currentArea.sludgeSpawned)
        {
            Debug.Log($"EnemySpawner: Spawning Sludge in Area {areaIndex + 1}.");
            SpawnSludgeEnemies(currentArea.spawnAreaCenter.position, currentArea.spawnAreaRadius, currentArea.numSludgeToSpawn, currentArea.areaManager);
            currentArea.sludgeSpawned = true;
        }
        else if (!currentArea.spawnSludgeFirst && !currentArea.gasSpawned)
        {
            Debug.Log($"EnemySpawner: Spawning Gas in Area {areaIndex + 1}.");
            SpawnGasEnemies(currentArea.spawnAreaCenter.position, currentArea.spawnAreaRadius, currentArea.numGasToSpawn, currentArea.areaManager);
            currentArea.gasSpawned = true;
        }
        else
        {
            Debug.Log($"EnemySpawner: Area {areaIndex + 1} ({ (currentArea.spawnSludgeFirst ? "Sludge" : "Gas") }) already spawned.");
        }
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

            BaseEnemy enemyScript = enemyGO.GetComponentInChildren<BaseEnemy>();
            if (enemyScript != null)
            {
                enemyScript.myAreaManager = managerToAssign;
                Debug.Log($"Found BaseEnemy script on child object '{enemyScript.gameObject.name}' of instance '{enemyGO.name}'. Assigning manager '{managerToAssign.gameObject.name}'.");
            }
            else
            {
                Debug.LogError($"Spawned Sludge enemy '{enemyGO.name}' is missing BaseEnemy script on itself AND all children!", enemyGO);
            }

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
            Quaternion desiredRotation = Quaternion.Euler(180f, 0f, 90f);
            GameObject enemyGO = Instantiate(gasPrefab, randomPosition, desiredRotation);

            BaseEnemy enemyScript = enemyGO.GetComponentInChildren<BaseEnemy>();
            if (enemyScript != null)
            {
                enemyScript.myAreaManager = managerToAssign;
                Debug.Log($"Found BaseEnemy script on child object '{enemyScript.gameObject.name}' of instance '{enemyGO.name}'. Assigning manager '{managerToAssign.gameObject.name}'.");
            }
            else
            {
                Debug.LogError($"Spawned Gas enemy '{enemyGO.name}' is missing BaseEnemy script on itself AND all children!", enemyGO);
            }

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
        for (int attempt = 0; attempt < 5; attempt++)
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