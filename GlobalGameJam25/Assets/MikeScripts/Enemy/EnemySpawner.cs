using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject sludgePrefab;
    public GameObject gasPrefab;
    public Transform spawnArea1Center;
    public float spawnArea1Radius = 15f;
    public int numSludgeToSpawn = 5;
    public Transform spawnArea2Center;
    public float spawnArea2Radius = 15f;
    public int numGasToSpawn = 5;

    private bool firstAreaIsSludge = false;
    private bool firstAreaSet = false;
    private bool sludgeSpawned = false; // Track if sludge has spawned
    private bool gasSpawned = false;   // Track if gas has spawned

    public void SetFirstArea(int areaNumber)
    {
        if (!firstAreaSet)
        {
            firstAreaIsSludge = (areaNumber == 1);
            firstAreaSet = true;
            Debug.Log("EnemySpawner: First area set to " + areaNumber + ". Sludge will spawn there.");
        }
    }

    public void SpawnEnemiesForArea(int areaNumber)
    {
        if (!firstAreaSet)
        {
            Debug.LogError("EnemySpawner: First area not set! Cannot spawn enemies yet.");
            return;
        }

        if ((firstAreaIsSludge && areaNumber == 1) && !sludgeSpawned)
        {
            SpawnSludgeEnemies(spawnArea1Center.position, spawnArea1Radius, numSludgeToSpawn);
            sludgeSpawned = true;
            Debug.Log("EnemySpawner: Spawning Sludge in Area " + areaNumber);
        }
        else if ((!firstAreaIsSludge && areaNumber == 2) && !sludgeSpawned)
        {
            SpawnSludgeEnemies(spawnArea2Center.position, spawnArea2Radius, numSludgeToSpawn);
            sludgeSpawned = true;
            Debug.Log("EnemySpawner: Spawning Sludge in Area " + areaNumber);
        }
        else if ((firstAreaIsSludge && areaNumber == 2) && !gasSpawned)
        {
            SpawnGasEnemies(spawnArea2Center.position, spawnArea2Radius, numGasToSpawn);
            gasSpawned = true;
            Debug.Log("EnemySpawner: Spawning Gas in Area " + areaNumber);
        }
        else if ((!firstAreaIsSludge && areaNumber == 1) && !gasSpawned)
        {
            SpawnGasEnemies(spawnArea1Center.position, spawnArea1Radius, numGasToSpawn);
            gasSpawned = true;
            Debug.Log("EnemySpawner: Spawning Gas in Area " + areaNumber);
        }
        else
        {
            Debug.Log("EnemySpawner: Spawning already complete or invalid area.");
        }
    }

    private void SpawnSludgeEnemies(Vector3 center, float radius, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 randomPosition = GetRandomPointInCircle(center, radius);
            Instantiate(sludgePrefab, randomPosition, Quaternion.identity);
        }
    }

    private void SpawnGasEnemies(Vector3 center, float radius, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 randomPosition = GetRandomPointInCircle(center, radius);
            Instantiate(gasPrefab, randomPosition, Quaternion.identity);
        }
    }

    public Vector3 GetRandomPointInCircle(Vector3 center, float radius)
    {
        float angle = Random.Range(0f, Mathf.PI * 2);
        float u = Random.Range(0f, 1f) + Random.Range(0f, 1f);
        float r = radius * (u > 1 ? 2 - u : u);
        return new Vector3(center.x + r * Mathf.Cos(angle), center.y + 1f, center.z + r * Mathf.Sin(angle));
    }
}