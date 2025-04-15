// --- GasEnemy Script ---
using UnityEngine;

// Requires BaseEnemy script and DamageType enum

[RequireComponent(typeof(EnemyBubble))] // Good practice if it relies on EnemyBubble
public class GasEnemy : BaseEnemy
{
    [Header("Gas Specific Stats")]
    public float patrolRadius = 15f;
    public float attackRange = 10f;
    public GameObject projectilePrefab;
    public Transform firePoint; // Assign in inspector
    public float projectileSpeed = 10f;
    public float fireCooldown = 2f;
    [Range(0.1f, 1f)] // Make damage reduction configurable
    public float basicDamageReductionFactor = 0.5f; // Takes 50% damage from Basic when not frozen

    [Header("References")]
    public HealthBar healthBar; // Assign in the Inspector

    // Internal state
    private Vector3 spawnPosition;
    private Vector3 patrolTarget;
    private float fireTimer = 0f;
    private EnemyBubble enemyBubble; // Cache reference to EnemyBubble

    protected override void Awake() // Use Awake for getting components
    {
        base.Awake(); // Call base Awake first
        enemyBubble = GetComponent<EnemyBubble>(); // Get EnemyBubble component
        if (enemyBubble == null)
        {
             Debug.LogError($"GasEnemy {gameObject.name} is missing the EnemyBubble component!");
        }
    }

    protected override void Start()
    {
        base.Start(); // Call base Start
        spawnPosition = transform.position;
        SetNewPatrolTarget();

        if (healthBar != null)
        {
            healthBar.SetMaxStats(maxHealth);
            UpdateHealthBar(); // Set initial health
        }
        else
        {
            Debug.LogWarning($"HealthBar not assigned to {gameObject.name}");
        }

         // Ensure firePoint is assigned
         if (firePoint == null) {
             Debug.LogError($"Fire Point not assigned on {gameObject.name}! GasEnemy cannot attack.");
             // Find it by name/tag as fallback?
             // firePoint = transform.Find("FirePoint"); // Example
         }
         if (projectilePrefab == null)
         {
             Debug.LogError($"Projectile Prefab not assigned on {gameObject.name}! GasEnemy cannot attack.");
         }
    }

    protected override void Update()
    {
        base.Update(); // This handles the freeze check and state machine call

        // Update fire timer ONLY if not frozen and player exists
        if (!_isFrozen && playerTransform != null)
        {
             fireTimer += Time.deltaTime;
             // Add floating movement here regardless of state (if desired)
             Wobble();
        }
    }

    void Wobble()
    {
        // Simple up/down bobbing effect
         transform.position = new Vector3(transform.position.x,
                                         spawnPosition.y + Mathf.Sin(Time.time * 1.5f) * 0.3f, // Adjust speed/amplitude
                                         transform.position.z);
    }

    // --- State Implementations ---

    protected override void Patrol()
    {
        // Move towards patrol target
        if (Vector3.Distance(transform.position, patrolTarget) < 1.0f) // Increased tolerance
        {
            SetNewPatrolTarget();
        }
        MoveTowards(patrolTarget);
        // Look ahead slightly (optional)
        // Vector3 lookDir = (patrolTarget - transform.position).normalized;
        // if (lookDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookDir);
    }

    private void SetNewPatrolTarget()
    {
        // Find a random point within the patrol sphere, keeping original Y level
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        patrolTarget = spawnPosition + new Vector3(randomDirection.x, 0, randomDirection.z); // Stay on same Y plane initially
        // Add NavMesh sampling here if using NavMesh for gas movement (less common)
    }

    protected override void Chase()
    {
         if (playerTransform == null) return;
        MoveTowards(playerTransform.position);
        LookAtPlayer();
    }

    protected override void Attack()
    {
        if (playerTransform == null) return;

        LookAtPlayer();

        // Attack logic (fire projectile)
        if (fireTimer >= fireCooldown && projectilePrefab != null && firePoint != null)
        {
            FireProjectile();
            fireTimer = 0f; // Reset timer AFTER firing
        }
        // Optional: Maybe drift slightly while attacking?
    }

    protected override void Flee()
    {
        if (playerTransform == null) return;

        // Move directly away from the player
        Vector3 fleeDirection = (transform.position - playerTransform.position).normalized;
        Vector3 fleeTarget = transform.position + fleeDirection * 5f; // Target a point away
        MoveTowards(fleeTarget);
        // Optionally look away from player
        // if (fleeDirection != Vector3.zero) transform.rotation = Quaternion.LookRotation(fleeDirection);
    }

    // --- Helper Methods ---

    void MoveTowards(Vector3 target)
    {
        Vector3 direction = (target - transform.position).normalized;
        // Use simple transform move (suitable for flying/gas enemy)
        transform.position += direction * moveSpeed * Time.deltaTime;
    }

    void LookAtPlayer()
    {
        if (playerTransform == null) return;
        Vector3 direction = (playerTransform.position - transform.position).normalized;
        // Look rotation only on Y axis for typical flying enemies
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f); // Smooth rotation
    }


     void FireProjectile()
     {
         // Ensure firePoint looks towards player's current position just before firing
         Vector3 directionToPlayer = (playerTransform.position - firePoint.position).normalized;
         firePoint.rotation = Quaternion.LookRotation(directionToPlayer);

         GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
         Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
         if (projectileRb != null)
         {
             projectileRb.linearVelocity = firePoint.forward * projectileSpeed; // Use velocity for consistent speed
         }

         // Pass damage based on EnemyBubble size (if available)
         EnemyProjectile projectileScript = projectile.GetComponent<EnemyProjectile>();
         if (projectileScript != null)
         {
             // Use ?? (null-coalescing operator) for safety if enemyBubble might be null
             projectileScript.damageAmount = enemyBubble?.size ?? 1;
             // projectileScript.Setup(...); // Call any other setup methods on the projectile
         }
     }

    // --- Overrides ---

    public override void TakeDamage(int damage, DamageType type = DamageType.Other)
    {
        if (currentState == EnemyState.Dying) return;

        int damageToTake = damage;

        // --- Gas Enemy Special Damage Logic ---
        if (type == DamageType.Basic && !_isFrozen)
        {
            // If taking Basic damage AND NOT frozen, apply reduction
            damageToTake = Mathf.CeilToInt(damage * basicDamageReductionFactor); // Use CeilToInt to ensure at least 1 damage if factor < 1
             Debug.Log($"GasEnemy is gaseous, reducing Basic damage by {(1-basicDamageReductionFactor)*100}% to {damageToTake}");
        }
        else if (type == DamageType.Basic && _isFrozen)
        {
             Debug.Log($"GasEnemy is frozen (solid), taking full Basic damage: {damageToTake}");
        }
        // --- End Special Logic ---

        // Call the base TakeDamage AFTER adjusting the damage amount
        base.TakeDamage(damageToTake, type); // Pass the (potentially modified) damage and original type
    }

     public override void Freeze(float baseDuration = 5f) // Default duration if not specified by bullet
     {
        if (_isFrozen || currentState == EnemyState.Dying) return;
        base.Freeze(baseDuration); // Call base freeze logic (sets flags, state, timer, base visuals)
        // Gas specific: Become "Solid" - maybe change physics properties? (Rigidbody?)
         Debug.Log("GasEnemy turned solid!");
         // Example: Change layer? Change tag? Modify visual effect?
         // If you have different materials for Gas/Solid state:
         // if (_renderer != null && solidMaterial != null) _renderer.material = solidMaterial;
     }

     protected override void Unfreeze()
     {
        // Gas specific: Revert from "Solid"
        bool wasFrozen = _isFrozen; // Check before calling base.Unfreeze
        base.Unfreeze(); // Call base unfreeze logic (resets flags, state, timer, base visuals)

        if(wasFrozen) // Only do this if we were actually frozen
        {
            Debug.Log("GasEnemy turned gaseous again!");
            // Example: Revert physics changes?
            // If you changed materials:
            // if (_renderer != null && gasMaterial != null) _renderer.material = gasMaterial;
            // else if (_renderer != null) _renderer.material.color = _originalColor; // Fallback if only color was changed
        }
     }


    protected override float GetAttackRange()
    {
        return attackRange;
    }

    protected override void UpdateHealthBar()
    {
        healthBar?.SetHealth(currentHealth);
    }

    protected override void Die()
    {
         // Add specific gas death effects (e.g., dissipate particle effect)
         // Instantiate(gasDissipateEffect, transform.position, Quaternion.identity);
         base.Die(); // Call base Die for destruction and GameManager notification
    }

    // Override Gizmos to draw attack range
    private new void OnDrawGizmosSelected() // 'new' keyword needed to hide base method if signature matches
    {
        base.OnDrawGizmosSelected(); // Draw base gizmos (detection radius)

        // Draw Patrol Radius (optional, could be in base if common)
         Gizmos.color = Color.blue;
         Gizmos.DrawWireSphere(spawnPosition, patrolRadius); // Use spawnPosition as center

        // Draw Attack Radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}