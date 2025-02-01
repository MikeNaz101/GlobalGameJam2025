using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public EnemyBubble enemyBubble; // Reference to the EnemyBubble that created this projectile
    public Player player;           // Reference to the player

    private int damageAmount;
    private float speed = 10f;      // Speed of the projectile
    private Rigidbody rb;

    void Start()
    {
        if (enemyBubble != null)
        {
            damageAmount = enemyBubble.size; // Get size from the EnemyBubble instance
            Debug.Log("Projectile damage amount: " + damageAmount);
        }
        else
        {
            Debug.LogError("EnemyBubble reference is missing!");
        }

        // Get the Rigidbody component to move the projectile using physics
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody is missing on the projectile!");
        }
    }

    void Update()
    {
        // Move the projectile forward every frame
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * speed; //added linear velocity instead of velocity  // Apply velocity to move the projectile
            //rb.velocity = transform.forward * speed;  // Apply velocity to move the projectile
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) // Ensure the Player has the "Player" tag
        {
            Debug.Log("Projectile hit the player!");
            if (player != null)
            {
                player.TakeDamage(damageAmount); // Apply damage to the player
            }
            else
            {
                Debug.LogError("Player reference is missing!");
            }
            Destroy(gameObject); // Destroy the projectile on impact
        }
    }
}
