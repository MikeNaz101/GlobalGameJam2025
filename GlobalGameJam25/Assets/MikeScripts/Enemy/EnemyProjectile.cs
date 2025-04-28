using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))] // Ensure it has a collider (set IsTrigger=true for OnTriggerEnter)
public class EnemyProjectile : MonoBehaviour
{
    [Header("Stats")]
    [Tooltip("Damage dealt to the player on impact.")]
    public int damageAmount = 5; // Default damage, can be set by the firing enemy
    [Tooltip("Speed the projectile travels.")]
    public float speed = 15f;
    [Tooltip("How many seconds the projectile lasts before being destroyed.")]
    public float lifetime = 5.0f;

    [Header("Effects")]
    [Tooltip("Particle effect prefab spawned on impact. Assign in Inspector.")]
    public GameObject hitEffectPrefab; // Assign particle effect here

    private Rigidbody rb;

    void Awake()
    {
        // Get the Rigidbody component
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody component missing from projectile!", this);
            enabled = false; // Disable script if no Rigidbody
            return;
        }

        // Ensure the collider is set to be a trigger if using OnTriggerEnter
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            // Optional: Log a warning if you intend to use OnTriggerEnter but IsTrigger is false
            // Debug.LogWarning($"Projectile '{gameObject.name}' collider is not set to IsTrigger. Using OnCollisionEnter instead?", this);
        } else if (col == null) {
             Debug.LogError("Collider component missing from projectile!", this);
             enabled = false;
        }
    }

    void Start()
    {
        // Set the projectile's velocity ONCE for consistent movement
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * speed;
        }

        // Automatically destroy the projectile after its lifetime expires
        Destroy(gameObject, lifetime);
    }

    // --- Collision Detection ---

    // Use OnTriggerEnter if your projectile's collider has "Is Trigger" checked.
    // Ensure the Player's Rigidbody/Collider setup allows for trigger events.
    void OnTriggerEnter(Collider other)
    {
        // Check if the object collided with has the "Player" tag
        if (other.CompareTag("Player"))
        {
            HandlePlayerHit(other.gameObject); // Pass the player GameObject
            HandleImpact(other.transform.position, transform.forward); // Spawn effect at player position
        }
        // Optional: Add checks for other tags (e.g., "Environment") to destroy projectile
        else if (!other.CompareTag("Enemy") && !other.isTrigger) // Avoid hitting other enemies or triggers
        {
             // Get the closest point on the collider that was hit for effect placement
             Vector3 impactPoint = other.ClosestPoint(transform.position);
             HandleImpact(impactPoint, transform.forward); // Spawn effect at impact point
        }
    }

    /*
    // --- OR --- Use OnCollisionEnter if your projectile is NOT a trigger.
    // Ensure the Player has a non-trigger Collider and potentially a Rigidbody.
    void OnCollisionEnter(Collision collision)
    {
        // Check if the object collided with has the "Player" tag
        if (collision.gameObject.CompareTag("Player"))
        {
            HandlePlayerHit(collision.gameObject);
            HandleImpact(collision.contacts[0].point, collision.contacts[0].normal); // Spawn effect at contact point
        }
        // Optional: Add checks for other tags
        else if (!collision.gameObject.CompareTag("Enemy")) // Avoid hitting other enemies
        {
             HandleImpact(collision.contacts[0].point, collision.contacts[0].normal); // Spawn effect at contact point
        }
    }
    */

    // --- Helper Methods ---

    void HandlePlayerHit(GameObject playerObject)
    {
        Debug.Log($"Projectile hit player: {playerObject.name}");
        // Try to get the PlayerStateManager component to apply damage
        PlayerStateManager player = playerObject.GetComponentInParent<PlayerStateManager>();
        if (player != null)
        {
            player.TakeDamage(damageAmount);
        }
        else
        {
            Debug.LogWarning($"Could not find PlayerStateManager on hit object '{playerObject.name}' or its parents.", this);
        }
    }

    void HandleImpact(Vector3 position, Vector3 direction)
    {
        // Instantiate hit effect if assigned
        if (hitEffectPrefab != null)
        {
            // Spawn effect slightly offset from the surface normal if needed, or just at position
            Instantiate(hitEffectPrefab, position, Quaternion.LookRotation(direction));
            // Ensure the particle effect prefab destroys itself after playing
        }

        // Destroy the projectile GameObject immediately after handling impact
        Destroy(gameObject);
    }
}
