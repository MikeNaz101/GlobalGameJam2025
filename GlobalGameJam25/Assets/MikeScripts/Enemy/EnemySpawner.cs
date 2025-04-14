using UnityEngine;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    public GameObject sludgePrefab;
    public GameObject gasPrefab;
    public Transform spawnArea1Center;
    public int numSludgeToSpawn = 5;

    public Transform spawnArea2Center;
    public int numGasToSpawn = 5;

    private bool area1Entered = false;
    private bool area2Entered = false;
    private bool spawningComplete = false;
    private List<GameObject> activeEnemies = new List<GameObject>();

    void Start()
    {
        // Ensure the center GameObjects have SphereColliders set to 'Is Trigger'
        if (spawnArea1Center != null && spawnArea1Center.GetComponent<SphereCollider>() == null)
        {
            Debug.LogError("Spawn Area 1 Center does not have a SphereCollider!");
            enabled = false;
        }
        if (spawnArea1Center != null && !spawnArea1Center.GetComponent<SphereCollider>().isTrigger)
        {
            Debug.LogError("SphereCollider on Spawn Area 1 Center is not set to 'Is Trigger'!");
            enabled = false;
        }

        if (spawnArea2Center != null && spawnArea2Center.GetComponent<SphereCollider>() == null)
        {
            Debug.LogError("Spawn Area 2 Center does not have a SphereCollider!");
            enabled = false;
        }
        if (spawnArea2Center != null && !spawnArea2Center.GetComponent<SphereCollider>().isTrigger)
        {
            Debug.LogError("SphereCollider on Spawn Area 2 Center is not set to 'Is Trigger'!");
            enabled = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (spawningComplete) return;

        if (other.CompareTag("Player"))
        {
            if (other.transform == spawnArea1Center && !area1Entered)
            {
                area1Entered = true;
                SpawnSludgeEnemies(spawnArea1Center.position, spawnArea1Center.GetComponent<SphereCollider>().radius);
                Debug.Log("Player entered Spawn Area 1 first. Spawning Sludge enemies here.");
            }
            else if (other.transform == spawnArea2Center && !area2Entered)
            {
                area2Entered = true;
                if (!area1Entered)
                {
                    SpawnSludgeEnemies(spawnArea2Center.position, spawnArea2Center.GetComponent<SphereCollider>().radius);
                    Debug.Log("Player entered Spawn Area 2 first. Spawning Sludge enemies here.");
                }
                else
                {
                    SpawnGasEnemies(spawnArea2Center.position, spawnArea2Center.GetComponent<SphereCollider>().radius);
                    Debug.Log("Player entered Spawn Area 2 second. Spawning Gas enemies here.");
                }
            }
        }
    }

    void SpawnSludgeEnemies(Vector3 center, float radius)
    {
        for (int i = 0; i < numSludgeToSpawn; i++)
        {
            Vector3 randomPosition = GetRandomPointInCircle(center, radius);
            GameObject enemy = Instantiate(sludgePrefab, randomPosition, Quaternion.identity);
            activeEnemies.Add(enemy);
        }
    }

    void SpawnGasEnemies(Vector3 center, float radius)
    {
        for (int i = 0; i < numGasToSpawn; i++)
        {
            Vector3 randomPosition = GetRandomPointInCircle(center, radius);
            GameObject enemy = Instantiate(gasPrefab, randomPosition, Quaternion.identity);
            activeEnemies.Add(enemy);
        }
    }

    Vector3 GetRandomPointInCircle(Vector3 center, float radius)
    {
        float angle = Random.Range(0f, Mathf.PI * 2);
        float u = Random.Range(0f, 1f) + Random.Range(0f, 1f);
        float r = radius * (u > 1 ? 2 - u : u);
        return new Vector3(center.x + r * Mathf.Cos(angle), center.y + 1f, center.z + r * Mathf.Sin(angle));
    }

    public void EnemyDied(GameObject deadEnemy)
    {
        activeEnemies.Remove(deadEnemy);
    }

    private void OnDrawGizmosSelected()
    {
        // No need to draw Gizmos here as the SphereColliders visualize the areas
    }
}