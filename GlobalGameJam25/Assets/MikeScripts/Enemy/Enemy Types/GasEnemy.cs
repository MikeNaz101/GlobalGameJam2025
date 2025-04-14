using UnityEngine;

public class GasEnemy : BaseEnemy
{
    public float patrolRadius = 15f;
    private Vector3 spawnPosition;
    private Vector3 patrolTarget;
    public float attackRange = 10f;
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float projectileSpeed = 10f;
    public float fireCooldown = 2f;
    private float fireTimer = 0f;
    private bool _isFrozen = false;
    private float _freezeEndTime = 0f;

    public HealthBar healthBar; // Make sure this is assigned in the Inspector

    protected override void Start()
    {
        base.Start();
        spawnPosition = transform.position;
        SetNewPatrolTarget();
        if (healthBar != null)
        {
            healthBar.SetMaxStats(maxHealth);
        }
        else
        {
            Debug.LogWarning("HealthBar not assigned to " + gameObject.name);
        }
    }

    protected override void Update()
    {
        if (_isFrozen)
        {
            if (Time.time >= _freezeEndTime)
            {
                _isFrozen = false;
                GetComponent<Renderer>().material.color = Color.white;
            }
            else
            {
                return; // Frozen, so skip other updates
            }
        }
        base.Update();
        fireTimer += Time.deltaTime;
    }

    protected override void Patrol()
    {
        // Simple random movement within the patrol radius
        if (Vector3.Distance(transform.position, patrolTarget) < 0.5f)
        {
            SetNewPatrolTarget();
        }
        Vector3 direction = (patrolTarget - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;
        // Optional: Add floating movement or other unique patrol behavior
    }

    private void SetNewPatrolTarget()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += spawnPosition;
        // You might want to constrain the Y position for a floating enemy
        patrolTarget = new Vector3(randomDirection.x, spawnPosition.y + Mathf.Sin(Time.time * 0.5f) * 2f, randomDirection.z); // Example float
    }

    protected override void Chase()
    {
        Vector3 direction = (playerTransform.position - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
    }

    protected override void Attack()
    {
        // Rotate to face the player
        Vector3 direction = (playerTransform.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);

        if (fireTimer >= fireCooldown)
        {
            FireProjectile();
            fireTimer = 0f;
        }
    }

    protected override void Flee()
    {
        // Gas enemy doesn't flee, maybe it becomes invisible or moves erratically?
        // For now, let's just have it move away.
        Vector3 fleeDirection = (transform.position - playerTransform.position).normalized;
        transform.position += fleeDirection * moveSpeed * Time.deltaTime;
    }

    protected override float GetAttackRange()
    {
        return attackRange;
    }

    public void FireProjectile()
    {
        if (projectilePrefab != null && firePoint != null && playerTransform != null)
        {
            // Rotate fire point to face the player
            Vector3 directionToPlayer = (playerTransform.position - firePoint.position).normalized;
            firePoint.rotation = Quaternion.LookRotation(new Vector3(directionToPlayer.x, directionToPlayer.y, directionToPlayer.z));

            GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
            Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
            if (projectileRb != null)
            {
                projectileRb.linearVelocity = firePoint.forward * projectileSpeed;
            }
            EnemyProjectile projectileScript = projectile.GetComponent<EnemyProjectile>();
            if (projectileScript != null)
            {
                projectileScript.damageAmount = GetComponent<EnemyBubble>()?.size ?? 1; // Pass damage info
            }
        }
    }

    public void Freeze(float duration)
    {
        _isFrozen = true;
        _freezeEndTime = Time.time + duration;
        GetComponent<Renderer>().material.color = Color.cyan;
    }

    public override void TakeDamage(int damage)
    {
        base.TakeDamage(damage);
        healthBar?.SetHealth(currentHealth);
    }

    protected override void Die()
    {
        base.Die();
        // Add any gas-specific death effects here
    }

    private void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(spawnPosition, patrolRadius);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}