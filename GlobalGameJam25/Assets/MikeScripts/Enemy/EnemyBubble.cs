using System;
using UnityEngine;

public class EnemyBubble : MonoBehaviour
{
    private bool _isFrozen = false;
    private float _freezeEndTime = 0f;
    public float detectionRadius = 5f;  // Range to detect player
    public LayerMask playerLayer;  // Set this to a layer that only includes the player
    public int size = 20;
    public int maxHealth = 100;
    public int speed = 2;  // Slower speed to balance chasing behavior
    public int currentHealth;
    public HealthBar healthBar;
    public GameObject projectilePrefab;
    public Transform firePoint;        // A child GameObject or position where the projectile spawns
    public float projectileSpeed = 10f;
    public float detectionRange = 15f; // Distance at which enemy starts chasing
    public float attackRange = 10f;    // Distance at which enemy starts shooting
    public float fireCooldown = 2f;    // Cooldown between shots
    private float fireTimer = 0f;      // Timer to track fire cooldown
    public PlayerStateManager player;
    public Transform playerTransform;   // Reference to the player's transform

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerStateManager>();
        // Finds player in the scene using "Player" tag
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        Debug.Log("Player's position is:" + playerTransform.position);
        currentHealth = maxHealth;
        healthBar.SetMaxStats(maxHealth); // Initialize health bar
    }

    void Update()
    {
        if (player == null) return; // Exit if player not found
        if (_isFrozen)
        {
            if (Time.time >= _freezeEndTime)
            {
                _isFrozen = false;
                // Optionally, revert material/color change.
                GetComponent<Renderer>().material.color = Color.white; // Example: back to white.
            }
            else
            {
                GetComponent<Renderer>().material.color = Color.white; // Example: back to white.
                // Enemy is frozen - prevent movement, attacking, etc.  You'd likely disable AI scripts here.
                return; // Early return to prevent any other actions.
            }
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Chase player if within detection range
        if (distanceToPlayer < detectionRange)
        {
            ChasePlayer(distanceToPlayer);
        }

        // Fire projectiles if within attack range
        if (distanceToPlayer < attackRange)
        {
            fireTimer += Time.deltaTime;
            if (fireTimer >= fireCooldown)
            {
                FireProjectile();
                fireTimer = 0f; // Reset fire timer
            }
        }
    }

    void ChasePlayer(float distance)
    {
        // Rotate to face the player
        Vector3 direction = (playerTransform.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z)); // Keep upright
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);

        // Move towards the player
        if (distance > attackRange) // Stop moving when in attack range
        {
            transform.position += direction * speed * Time.deltaTime;
        }
    }

    public void FireProjectile()
    {
        // Rotate to face the player
        Vector3 direction = (playerTransform.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z)); // Keep upright
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        // Instantiate the projectile at the fire point
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        /* Get the Rigidbody of the projectile
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Aim the projectile at the player
            Vector3 shootDirection = (playerTransform.position - firePoint.position).normalized;
            rb.linearVelocity = shootDirection * projectileSpeed;
        }*/

        // Assign this EnemyBubble instance to the projectile
        EnemyProjectile projectileScript = projectile.GetComponent<EnemyProjectile>();
        if (projectileScript != null)
        {
            projectileScript.enemyBubble = this; // Pass itself as a reference
        }
    }

    public void Freeze(float duration)
    {
        _isFrozen = true;
        _freezeEndTime = Time.time + duration;
        // Optionally, change the enemy's material/color to indicate they are frozen.
        GetComponent<Renderer>().material.color = Color.cyan; // Example: turn cyan when frozen.
    }


    public void EnemyTakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth); // Ensure health doesn't go below 0
        healthBar.SetHealth(currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        player.currentMana = player.maxMana;
        Debug.Log(gameObject.name + " has been destroyed!");
        Destroy(gameObject); // Destroy the enemy GameObject
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}

