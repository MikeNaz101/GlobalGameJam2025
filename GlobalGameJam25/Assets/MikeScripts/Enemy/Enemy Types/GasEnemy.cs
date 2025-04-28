using UnityEngine;

// Requires the updated BaseEnemy script.
// May also require an EnemyBubble script if that component is used.
// //[RequireComponent(typeof(EnemyBubble))]
public class GasEnemy : BaseEnemy
{
    [Header("Gas Specific Stats")]
    [Tooltip("How far the enemy patrols from its spawn point.")]
    public float patrolRadius = 15f;
    [Tooltip("Range at which the enemy starts attacking.")]
    public float attackRange = 10f;
    [Tooltip("The projectile prefab to instantiate when attacking.")]
    public GameObject projectilePrefab;
    [Tooltip("The point from which projectiles are fired.")]
    public Transform firePoint;
    [Tooltip("The speed of the fired projectiles.")]
    public float projectileSpeed = 10f;
    [Tooltip("Time in seconds between consecutive attacks.")]
    public float fireCooldown = 2f;
    [Tooltip("Damage reduction factor against 'Basic' damage type when not frozen (0.1 = 90% reduction, 1 = no reduction).")]
    [Range(0.1f, 1f)]
    public float basicDamageReductionFactor = 0.5f; // Takes 50% damage from Basic type

    // ----- NEW AUDIO -----
    [Header("Gas Specific Audio")]
    [Tooltip("Sound played when the Gas Enemy attacks (fires projectile). Assign in Inspector.")]
    public AudioClip attackSound;
    // ----- END NEW AUDIO -----

    [Header("References")]
    [Tooltip("Reference to the enemy's health bar UI.")]
    public HealthBar healthBar;
    // Reference to the EnemyBubble component if used for size/damage scaling
    private EnemyBubble enemyBubble;

    // Private variables
    private Vector3 spawnPosition; // Where the enemy originally spawned
    private Vector3 patrolTarget; // Current destination during patrol state
    private float fireTimer = 0f; // Timer to track attack cooldown

    // --- Unity Methods ---

    // Awake is called when the script instance is being loaded.
    // We inherit Awake behavior from BaseEnemy (getting AudioSource, setting health).
    protected override void Awake()
    {
        base.Awake(); // Call the BaseEnemy Awake method first
        // Try to get the EnemyBubble component if it exists on this GameObject
        enemyBubble = GetComponent<EnemyBubble>();
        // Note: EnemyBubble script itself is not provided, assuming it exists if uncommented/used.
    }

    // Start is called before the first frame update.
    // We inherit Start behavior from BaseEnemy (finding player, playing spawn sound, starting ambient sound).
    protected override void Start()
    {
        base.Start(); // Call the BaseEnemy Start method
        spawnPosition = transform.position; // Store spawn position for patrol and wobble
        SetNewPatrolTarget(); // Find an initial patrol point

        // Initialize health bar if assigned
        if (healthBar != null)
        {
            healthBar.SetMaxStats(maxHealth);
            UpdateHealthBar(); // Set initial health value
        }

        // Error checks for required references
        if (firePoint == null)
        {
            Debug.LogError($"[{gameObject.name}] Fire Point reference is not assigned in the Inspector!", this);
        }
        if (projectilePrefab == null)
        {
            Debug.LogError($"[{gameObject.name}] Projectile Prefab is not assigned in the Inspector!", this);
        }
    }

    // Update is called once per frame.
    // We inherit Update behavior from BaseEnemy (state machine, freeze timer, ambient sound checks).
    protected override void Update()
    {
        // Base Update MUST be called to handle state transitions, freezing, etc.
        base.Update();

        // Perform gas-specific actions if not frozen and player exists
        if (!_isFrozen && playerTransform != null)
        {
            // Increment fire cooldown timer regardless of state (reset in Attack)
            fireTimer += Time.deltaTime;
            // Apply visual wobble effect
            Wobble();
        }
    }

    // --- Gas Specific Behaviors ---

    // Creates a gentle up-and-down floating motion.
    void Wobble()
    {
        // Calculates vertical offset using a sine wave based on time
        float yOffset = Mathf.Sin(Time.time * 1.5f) * 0.3f; // Adjust speed (1.5f) and amplitude (0.3f) as needed
        // Applies the offset relative to the initial spawn height
        transform.position = new Vector3(transform.position.x, spawnPosition.y + yOffset, transform.position.z);
    }

    // --- State Implementations (Overrides from BaseEnemy) ---

    protected override void Patrol()
    {
        // If close to the current patrol target, find a new one
        if (Vector3.Distance(transform.position, patrolTarget) < 1.0f)
        {
            SetNewPatrolTarget();
        }
        // Move towards the target and look in the direction of movement
        MoveTowards(patrolTarget);
        Vector3 moveDir = (patrolTarget - transform.position).normalized;
        if (moveDir != Vector3.zero) // Avoid looking down if target is directly below/above (shouldn't happen with patrol)
        {
            LookInDirection(moveDir);
        }
    }

    // Calculates a new random patrol target within the radius around the spawn point.
    private void SetNewPatrolTarget()
    {
        // Get a random point inside a sphere of patrolRadius
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        // Set the target based on the spawn position plus the random offset (keeping Y the same initially)
        patrolTarget = spawnPosition + new Vector3(randomDirection.x, 0, randomDirection.z);
        // Note: This doesn't guarantee the target is reachable or on navmesh. Consider NavMeshAgent for more robust patrolling.
    }

    protected override void Chase()
    {
        if (playerTransform == null) return; // Exit if player doesn't exist
        // Move towards the player's current position
        MoveTowards(playerTransform.position);
        // Look at the player (horizontally)
        LookAtPlayer();
    }

    // Attack state logic: Aim and fire if cooldown ready.
    protected override void Attack()
    {
        if (playerTransform == null) return; // Exit if player doesn't exist

        LookAtPlayer(); // Always keep aiming at the player while in attack state

        // Check if the fire cooldown timer is ready and necessary prefabs/references exist
        if (fireTimer >= fireCooldown && projectilePrefab != null && firePoint != null)
        {
            FireProjectile(); // Execute the firing sequence
            fireTimer = 0f; // Reset the cooldown timer
        }
        // If not ready to fire, just keep aiming (handled by LookAtPlayer above).
        // BaseEnemy Update handles transitioning out if player moves out of range/LOS.
    }

    // Flee state logic: Move away from the player.
    protected override void Flee()
    {
        if (playerTransform == null) return; // Exit if player doesn't exist

        // Calculate direction away from the player
        Vector3 fleeDirection = (transform.position - playerTransform.position).normalized;
        // Calculate a target point in the flee direction
        Vector3 fleeTarget = transform.position + fleeDirection * 5f; // Flee a fixed distance away for simplicity
        // Move towards the flee target point
        MoveTowards(fleeTarget);
        // Look in the direction of fleeing
        if (fleeDirection != Vector3.zero)
        {
            LookInDirection(fleeDirection);
        }
    }

    // --- Helper Methods ---

    // Moves the enemy towards a target position.
    void MoveTowards(Vector3 target)
    {
        // Calculate direction to the target
        Vector3 direction = (target - transform.position).normalized;
        // Move the enemy in that direction based on moveSpeed
        // Note: This implementation ignores obstacles. Consider using NavMeshAgent.Move for pathfinding.
        transform.position += direction * moveSpeed * Time.deltaTime;
    }

    // Rotates the enemy to look at the player (horizontally).
    void LookAtPlayer()
    {
        if (playerTransform == null) return;
        // Calculate direction to the player
        Vector3 direction = playerTransform.position - transform.position;
        // Look in that direction (handled by LookInDirection)
        LookInDirection(direction);
    }

    // Rotates the enemy smoothly to look in a specific direction (horizontally).
    void LookInDirection(Vector3 direction)
    {
        if (direction == Vector3.zero) return; // Avoid looking down/up if direction is zero
        // Create rotation to look in the direction, ignoring vertical component
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        // Smoothly interpolate towards the target rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f); // Adjust rotation speed (5f) as needed
    }

    // Instantiates and fires the projectile.
    void FireProjectile()
    {
        // --- AUDIO: Play Attack Sound ---
        // Uses the PlaySound helper from BaseEnemy and the assigned attackSound clip.
        PlaySound(attackSound, mainAudioSource);
        // -----------------------------

        // Ensure firePoint and playerTransform are valid before proceeding
        if (firePoint == null || playerTransform == null)
        {
            Debug.LogError($"[{gameObject.name}] Cannot fire projectile: firePoint or playerTransform is null.", this);
            return;
        }

        // Calculate direction towards the player from the fire point
        Vector3 directionToPlayer = (playerTransform.position - firePoint.position).normalized;
        // Point the firePoint directly at the player for accurate projectile spawn rotation
        firePoint.rotation = Quaternion.LookRotation(directionToPlayer);

        // Instantiate the projectile prefab at the fire point's position and rotation
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        // Get the Rigidbody component of the projectile
        Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
        if (projectileRb != null)
        {
            // Set the projectile's velocity to move it forward
            projectileRb.linearVelocity = firePoint.forward * projectileSpeed; // Use velocity for consistent speed
            // Alternatively use AddForce: projectileRb.AddForce(firePoint.forward * projectileSpeed, ForceMode.VelocityChange);
        }

        // Get the EnemyProjectile script (assuming it exists) to set damage
        EnemyProjectile projectileScript = projectile.GetComponent<EnemyProjectile>();
        if (projectileScript != null)
        {
            // Set projectile damage, potentially based on EnemyBubble size if available
            projectileScript.damageAmount = enemyBubble?.size ?? 1; // Use bubble size if available, otherwise default to 1
        }
    }

    // --- Overrides from BaseEnemy ---

    // Applies damage, considering the basic damage reduction factor if applicable.
    public override void TakeDamage(int damage, DamageType type = DamageType.Other)
    {
        // Base TakeDamage handles the check for Dying state and plays the sound.
        if (currentState == EnemyState.Dying) return;

        int damageToTake = damage;
        // Apply damage reduction only for 'Basic' damage and only if not frozen
        if (type == DamageType.Basic && !_isFrozen)
        {
            damageToTake = Mathf.CeilToInt(damage * basicDamageReductionFactor); // Use CeilToInt to ensure at least 1 damage if factor is low but > 0
        }

        // Call the base TakeDamage AFTER calculating the specific damage amount.
        // Base method handles health reduction, state changes (Flee/Die), and health bar update.
        base.TakeDamage(damageToTake, type);
    }

    // Override Freeze if GasEnemy has specific behavior when frozen (optional).
    public override void Freeze(float baseDuration = 5f)
    {
        if (_isFrozen || currentState == EnemyState.Dying) return;
        // Potentially add gas-specific visual changes on freeze here
        base.Freeze(baseDuration); // Call base freeze logic (sets state, timer, stops movement)
    }

    // Override Unfreeze if GasEnemy has specific behavior when unfreezing (optional).
    // protected override void Unfreeze()
    // {
    //     // Potentially revert gas-specific visual changes here
    //     base.Unfreeze(); // Call base unfreeze logic
    // }

    // Returns the specific attack range for the Gas Enemy.
    protected override float GetAttackRange()
    {
        return attackRange;
    }

    // Updates the health bar visuals.
    protected override void UpdateHealthBar()
    {
        // Use null-conditional operator ?. for safety if healthBar is not assigned
        healthBar?.SetHealth(currentHealth);
    }

    // Die method is now handled by DieCoroutine in BaseEnemy, triggered from TakeDamage.
    // No need for an override here unless GasEnemy needs specific cleanup *before* the death sound/delay.
    // protected override void Die() { base.Die(); } // Not needed

} // End of GasEnemy class