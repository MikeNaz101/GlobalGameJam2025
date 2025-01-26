using System;
using UnityEngine;

public class EnemyBubble : MonoBehaviour
{
    public int size = 20;
    public int maxHealth = 100;
    public int speed = 20;
    public int currentHealth;
    public ProgressBar healthBar;
    public GameObject projectilePrefab;
    public Transform firePoint;        // A child GameObject or position where the projectile spawns
    public float projectileSpeed = 10f;

    public int Size()
    {
        return this.size; // Reference its own size
    }
    void Update()
    {
        if (Time.time % 2 < 0.1f) // Fires every 2 seconds
        {
            FireProjectile();
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth); // Ensure health doesn't go below 0
        healthBar.SetHealth(currentHealth);
    }

    public void FireProjectile()
    {
        // Instantiate the projectile
        GameObject projectile = Instantiate(projectilePrefab, transform.position, Quaternion.identity);

        // Get the Rigidbody of the projectile
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Apply force to move the projectile
            rb.linearVelocity = firePoint.forward * projectileSpeed; // Forward direction
        }

        // Assign this EnemyBubble instance to the projectile
        EnemyProjectile projectileScript = projectile.GetComponent<EnemyProjectile>();
        if (projectileScript != null)
        {
            projectileScript.enemyBubble = this; // Pass itself as a reference
        }
    }
}
