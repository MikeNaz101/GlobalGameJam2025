using UnityEngine;

public class TestGetComponent : MonoBehaviour
{
    [Tooltip("Drag the Sludge instance FROM THE HIERARCHY onto here in Inspector AFTER manually placing it in the scene for testing.")]
    public GameObject sludgeInstanceToTest; // Assign manually placed instance here

    void Start()
    {
        if (sludgeInstanceToTest == null)
        {
            Debug.LogError("TEST SCRIPT: Sludge instance not assigned in Inspector!");
            return;
        }

        Debug.Log($"TEST SCRIPT: Testing GetComponentInChildren on {sludgeInstanceToTest.name}...");

        // --- Use GetComponentInChildren ---
        BaseEnemy foundBase = sludgeInstanceToTest.GetComponentInChildren<BaseEnemy>();
        SludgeEnemy foundSludge = sludgeInstanceToTest.GetComponentInChildren<SludgeEnemy>();
        // --------------------------------

        Debug.Log($"TEST SCRIPT: Found BaseEnemy component (InChildren)? {(foundBase != null ? "YES" : "NO")}");
        Debug.Log($"TEST SCRIPT: Found SludgeEnemy component (InChildren)? {(foundSludge != null ? "YES" : "NO")}");

        if (foundBase == null) {
            Debug.LogError("TEST SCRIPT: GetComponentInChildren<BaseEnemy> FAILED on the manually placed instance! Check prefab hierarchy and script attachment.");
        } else {
            Debug.Log($"TEST SCRIPT: Found BaseEnemy script on object: {foundBase.gameObject.name}");
        }

        if (foundSludge == null) {
            Debug.LogError("TEST SCRIPT: GetComponentInChildren<SludgeEnemy> FAILED on the manually placed instance! Check prefab hierarchy and script attachment.");
        } else {
            Debug.Log($"TEST SCRIPT: Found SludgeEnemy script on object: {foundSludge.gameObject.name}");
        }
    }
}