using UnityEngine;

// Requires the updated BaseEnemy script.
public class GasEnemy : BaseEnemy
{
    [Header("Gas Specific Stats")]
    [Tooltip("How far the enemy patrols from its spawn point.")]
    public float patrolRadius = 15f;
    [Tooltip("Range at which the enemy starts attacking.")]
    public float attackRange = 10f;
    [Tooltip("Damage dealt by each projectile.")]
    public int projectileDamage = 5; // Damage value for the projectile
    [Tooltip("The projectile prefab to instantiate when attacking.")]
    public GameObject projectilePrefab; // Assign your new EnemyProjectile prefab
    [Tooltip("The point from which projectiles are fired.")]
    public Transform firePoint;
    [Tooltip("The speed of the fired projectiles.")]
    public float projectileSpeed = 10f;
    [Tooltip("Time in seconds between consecutive attacks.")]
    public float fireCooldown = 2f;
    [Tooltip("Damage reduction factor against 'Basic' damage type when not frozen (0.1 = 90% reduction, 1 = no reduction).")]
    [Range(0.1f, 1f)]
    public float basicDamageReductionFactor = 0.5f; // Takes 50% damage from Basic type
    [Tooltip("How fast the enemy rotates to face its target (degrees per second).")]
    public float rotationSpeed = 180f;
    [Tooltip("How fast the wobble effect oscillates.")]
    public float wobbleSpeed = 1.5f;
    [Tooltip("How high the wobble effect moves the enemy.")]
    public float wobbleAmplitude = 0.3f;


    [Header("Gas Specific Audio")]
    [Tooltip("Sound played when the Gas Enemy attacks (fires projectile). Assign in Inspector.")]
    public AudioClip attackSound;

    [Header("References")]
    [Tooltip("Reference to the enemy's health bar UI.")]
    public HealthBar healthBar;

    // Private variables
    private Vector3 spawnPosition; // Where the enemy originally spawned
    private Vector3 patrolTarget; // Current destination during patrol state
    private float fireTimer = 0f; // Timer to track attack cooldown
    private bool hasPatrolTarget = false; // Tracks if a valid patrol target point was found

    // --- Unity Methods ---

    protected override void Awake()
    {
        base.Awake(); // Call the BaseEnemy Awake method first
    }

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
        } else if (projectilePrefab.GetComponent<EnemyProjectile>() == null) {
             Debug.LogError($"[{gameObject.name}] Assigned Projectile Prefab is missing the 'EnemyProjectile' script!", this);
        }
    }

    protected override void Update()
    {
        // Increment fire cooldown timer if not frozen
        if (!_isFrozen)
        {
            fireTimer += Time.deltaTime;
        }

        // Apply visual wobble effect regardless of state (unless frozen?)
        // You might want to stop wobble when frozen: if(!_isFrozen) Wobble();
        Wobble();

        // Base Update MUST be called AFTER specific updates to handle state transitions, freezing, etc.
        base.Update();
    }

    // --- Gas Specific Behaviors ---

    // Creates a gentle up-and-down floating motion.
    void Wobble()
    {
        // Calculates vertical offset using a sine wave based on time
        float yOffset = Mathf.Sin(Time.time * wobbleSpeed) * wobbleAmplitude;
        // Applies the offset relative to the initial spawn height
        transform.position = new Vector3(transform.position.x, spawnPosition.y + yOffset, transform.position.z);
    }

    // --- State Implementations (Overrides from BaseEnemy) ---

    protected override void Patrol()
    {
        // If we don't have a target OR we've reached the current target, find a new one.
        if (!hasPatrolTarget || Vector3.Distance(transform.position, patrolTarget) < 1.0f)
        {
            SetNewPatrolTarget();
        }

        // If we successfully found a target, move towards it and look where going.
        if (hasPatrolTarget)
        {
            MoveTowards(patrolTarget);
            Vector3 moveDir = (patrolTarget - transform.position).normalized;
             LookInDirection(moveDir); // Look in direction of movement
        }
    }

    // Calculates a new random patrol target within the radius around the spawn point.
    private void SetNewPatrolTarget()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        // Set the target based on the spawn position plus the random offset (keeping Y the same initially)
        patrolTarget = spawnPosition + new Vector3(randomDirection.x, 0, randomDirection.z);
        hasPatrolTarget = true;
        // Note: This simple patrol doesn't check for obstacles or ground height.
        // Consider NavMeshAgent for more complex environments.
    }

    protected override void Chase()
    {
        if (playerTransform == null) return; // Exit if player doesn't exist

        // Check distance to player
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Move towards player only if outside attack range
        if (distanceToPlayer > GetAttackRange())
        {
            MoveTowards(playerTransform.position);
        }
        // If inside attack range, might stop or strafe (optional, for now just stop moving forward)

        // Always look at the player (horizontally)
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
        LookInDirection(fleeDirection);
    }

    // --- Helper Methods ---

    // Moves the enemy towards a target position.
    void MoveTowards(Vector3 target)
    {
        Vector3 direction = (target - transform.position).normalized;
        // Move the enemy in that direction based on moveSpeed
        // Note: This implementation ignores obstacles and verticality.
        transform.position += new Vector3(direction.x, 0, direction.z).normalized * moveSpeed * Time.deltaTime;
    }

    // Rotates the enemy to look at the player (horizontally).
    void LookAtPlayer()
    {
        if (playerTransform == null) return;
        Vector3 direction = playerTransform.position - transform.position;
        LookInDirection(direction);
    }

    // Rotates the enemy smoothly to look in a specific direction (horizontally).
    void LookInDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f) return; // Avoid zero direction warning

        // Create rotation to look in the direction, ignoring vertical component
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        // Smoothly interpolate towards the target rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
    }

    // Instantiates and fires the projectile.
    void FireProjectile()
    {
        // --- AUDIO: Play Attack Sound ---
        PlaySound(attackSound, mainAudioSource);
        // -----------------------------

        // Ensure firePoint and playerTransform are valid before proceeding
        if (firePoint == null || playerTransform == null)
        {
            Debug.LogError($"[{gameObject.name}] Cannot fire projectile: firePoint or playerTransform is null.", this);
            return;
        }

        // --- Calculate Aiming Direction with Upward Angle ---

        // 1. Calculate the base direction towards the player (horizontally flattened)
        Vector3 directionToPlayerFlat = (playerTransform.position - firePoint.position);
        directionToPlayerFlat.y = 0; // Ignore vertical difference for base aiming direction
        directionToPlayerFlat.Normalize(); // Ensure it's a unit vector

        // 2. Calculate the rotation needed to aim directly at the player (for reference, might not be needed for instantiation)
        // Quaternion directLookRotation = Quaternion.LookRotation(directionToPlayer); // Rotation aiming directly at player

        // 3. Calculate the desired upward rotation (40 degrees around the fire point's right axis)
        // We use -40 degrees because rotating around the right axis with a positive angle tilts down.
        Quaternion upwardTilt = Quaternion.AngleAxis(-15.0f, firePoint.right);

        // 4. Apply the upward tilt to the flattened direction vector
        Vector3 finalLaunchDirection = upwardTilt * directionToPlayerFlat;
        // Ensure the final direction is normalized if needed, though LookRotation handles magnitude
        // finalLaunchDirection.Normalize();

        // 5. Calculate the final rotation for the projectile instance
        // The projectile should face the direction it's actually going
        Quaternion finalLaunchRotation = Quaternion.LookRotation(finalLaunchDirection);

        // --- End Calculation ---


        // Instantiate the projectile prefab at the fire point's position using the calculated final rotation
        GameObject projectileGO = Instantiate(projectilePrefab, firePoint.position, finalLaunchRotation);

        // Get the EnemyProjectile script from the instantiated object
        EnemyProjectile projectileScript = projectileGO.GetComponent<EnemyProjectile>();
        if (projectileScript != null)
        {
            // Configure the projectile's properties
            projectileScript.damageAmount = this.projectileDamage; // Set damage from GasEnemy stats
            projectileScript.speed = this.projectileSpeed;     // Set speed from GasEnemy stats
            // Note: The projectile sets its own velocity in its Start method using its forward direction (which is now tilted up)
        }
        else
        {
             Debug.LogError($"Projectile prefab '{projectilePrefab.name}' is missing the EnemyProjectile script!", projectileGO);
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
            damageToTake = Mathf.CeilToInt(damage * basicDamageReductionFactor);
            // Debug.Log($"[{gameObject.name}] Basic damage reduced by {(1 - basicDamageReductionFactor):P0}. Taking {damageToTake} from {damage}.");
        }

        // Call the base TakeDamage AFTER calculating the specific damage amount.
        base.TakeDamage(damageToTake, type);
    }

    // Override Freeze/Unfreeze if GasEnemy needs specific visual changes (optional).
    // public override void Freeze(float baseDuration = 5f) { ... base.Freeze(baseDuration); }
    // protected override void Unfreeze() { ... base.Unfreeze(); }

    // Returns the specific attack range for the Gas Enemy.
    protected override float GetAttackRange()
    {
        return attackRange;
    }

    // Updates the health bar visuals.
    protected override void UpdateHealthBar()
    {
        healthBar?.SetHealth(currentHealth);
    }

} // End of GasEnemy class
