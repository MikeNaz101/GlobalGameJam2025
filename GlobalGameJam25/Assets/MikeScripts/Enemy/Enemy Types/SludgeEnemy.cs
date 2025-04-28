using UnityEngine;

// Requires the updated BaseEnemy script which handles most audio and state logic.
public class SludgeEnemy : BaseEnemy
{
    [Header("Sludge Specific Stats")]
    public float patrolRadius = 10f;
    public float attackRange = 1.5f;
    public int attackDamage = 10;
    public float attackCooldown = 1.5f;
    public float stoppingDistanceBuffer = 0.2f; // Buffer to prevent slight overshooting stopping attack
    public float rotationSpeed = 180f; // Degrees per second

    // ----- NEW AUDIO -----
    [Header("Sludge Specific Audio")]
    [Tooltip("Sound played when the Sludge Enemy attacks (melee). Assign in Inspector.")]
    public AudioClip attackSound;
    // ----- END NEW AUDIO -----

    [Header("Grounding")]
    public LayerMask groundLayer; // Which layers are considered ground
    public float groundCheckDistance = 0.5f; // How far down to check for ground
    public float groundOffset = 0.1f; // How far above the ground to float

    [Header("References")]
    public HealthBar healthBar; // Reference to the enemy's health bar UI

    // Private variables
    private Vector3 spawnPosition;
    private Vector3 currentTargetPosition; // Where the enemy is currently trying to move
    private float attackTimer = 0f; // Timer to track cooldown between attacks
    private bool hasTarget = false; // Tracks if a valid patrol or flee target point was found

    // --- Unity Methods ---

    // Awake is called when the script instance is being loaded.
    // We inherit Awake behavior from BaseEnemy (getting AudioSource, setting health).
    protected override void Awake()
    {
        base.Awake(); // Call the BaseEnemy Awake method
    }

    // Start is called before the first frame update.
    // We inherit Start behavior from BaseEnemy (finding player, playing spawn sound, starting ambient sound).
    protected override void Start()
    {
        base.Start(); // Call the BaseEnemy Start method
        spawnPosition = transform.position; // Store the initial position
        SetNewPatrolTarget(); // Find an initial patrol point
        if (healthBar != null)
        {
            healthBar.SetMaxStats(maxHealth); // Initialize health bar visuals
            UpdateHealthBar(); // Set initial health value on the bar
        }
    }

    // Update is called once per frame.
    // We inherit Update behavior from BaseEnemy (state machine, freeze timer, ambient sound checks).
    protected override void Update()
    {
        // Only increment attack timer if not frozen
        if (!_isFrozen)
        {
            attackTimer += Time.deltaTime;
        }

        StickToGround(); // Keep the enemy snapped to the ground layer

        base.Update(); // IMPORTANT: Call the BaseEnemy Update method to run the state machine
    }

    // --- State Implementations (Overrides from BaseEnemy) ---

    protected override void Patrol()
    {
        // If we don't have a target OR we've reached the current target, find a new one.
        if (!hasTarget || Vector3.Distance(transform.position, currentTargetPosition) < 1.0f)
        {
            SetNewPatrolTarget();
        }

        // If we successfully found a target, move towards it and look at it.
        if (hasTarget)
        {
            MoveTowards(currentTargetPosition);
            LookTowards(currentTargetPosition);
        }
    }

    // Finds a new random point within the patrol radius around the spawn point.
    private void SetNewPatrolTarget()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        Vector3 potentialTarget = spawnPosition + new Vector3(randomDirection.x, 0, randomDirection.z); // Keep it on the same plane initially

        // Raycast down to find the actual ground position for the target
        RaycastHit hit;
        // Start raycast from high above the potential target point
        if (Physics.Raycast(potentialTarget + Vector3.up * 5f, Vector3.down, out hit, 10f, groundLayer))
        {
            currentTargetPosition = hit.point; // Use the point where the raycast hit the ground
            hasTarget = true;
        }
        else
        {
            // Fallback if raycast doesn't hit (e.g., edge of map) - target spawn point or stay put
            currentTargetPosition = spawnPosition; // Or potentially set hasTarget = false;
            hasTarget = true; // Let's try targeting spawn point as fallback
            // Debug.LogWarning($"[{gameObject.name}] Could not find ground for patrol target. Targeting spawn point.");
        }
    }

    protected override void Chase()
    {
        if (playerTransform == null)
        {
            hasTarget = false; // No player to chase
            // Transition back to Patrol or Idle might happen in BaseEnemy's Update
            return;
        }

        currentTargetPosition = playerTransform.position; // Target is the player
        hasTarget = true;

        // Move towards the player only if outside the attack range (plus a small buffer)
        if (Vector3.Distance(transform.position, currentTargetPosition) > GetAttackRange() - stoppingDistanceBuffer)
        {
            MoveTowards(currentTargetPosition);
        }
        // Always look towards the player while chasing
        LookTowards(currentTargetPosition);
    }

    protected override void Attack()
    {
         if (playerTransform == null)
         {
             hasTarget = false; // No player to attack
             // State transition likely handled by BaseEnemy Update
             return;
         }

         currentTargetPosition = playerTransform.position; // Keep track of player position
         hasTarget = true;
         LookTowards(currentTargetPosition); // Keep looking at the player

        // Check if attack cooldown is ready AND if the player is within attack range (with a small buffer)
        // The BaseEnemy Update handles transitioning *out* of attack state if range/LOS is broken.
        if (attackTimer >= attackCooldown && Vector3.Distance(transform.position, currentTargetPosition) <= GetAttackRange() * 1.1f) // Use 1.1 buffer for consistency
        {
             PerformMeleeAttack(); // Execute the attack
             attackTimer = 0f; // Reset the attack cooldown timer
        }
         // If not ready to attack, we just keep looking (handled above) and wait for the timer/range.
    }

     // Performs the actual melee attack logic
     void PerformMeleeAttack()
     {
         // --- AUDIO: Play Attack Sound ---
         // Uses the PlaySound helper method inherited from BaseEnemy
         // and the mainAudioSource obtained in BaseEnemy's Awake.
         PlaySound(attackSound, mainAudioSource);
         // -----------------------------

         // Try to get the PlayerStateManager component from the player object (or its parent)
         // This assumes PlayerStateManager script is on the main player object or a parent.
         PlayerStateManager player = playerTransform?.GetComponentInParent<PlayerStateManager>(); // GetComponentInParent is safer if player structure varies

         if (player != null)
         {
             // Apply damage to the player
             // Debug.Log($"<color=green>[{gameObject.name}]</color> Attacking player '{player.name}'. Applying {attackDamage} damage.");
             player.TakeDamage(attackDamage);
         }
         else
         {
              // Error message if the player or the component cannot be found
             if(playerTransform == null) {
                  Debug.LogError($"<color=red>[{gameObject.name}]</color> ERROR in PerformMeleeAttack: playerTransform reference is NULL!");
             } else {
                 Debug.LogError($"<color=red>[{gameObject.name}]</color> ERROR in PerformMeleeAttack: Could not find PlayerStateManager component on '{playerTransform.name}' or its parents! Check player prefab structure.", this);
             }
         }
     }

    protected override void Flee()
    {
        if (playerTransform == null)
        {
            hasTarget = false; // No player to flee from
            return;
        }

        // Calculate direction away from the player
        Vector3 fleeDirection = (transform.position - playerTransform.position).normalized;
        // Calculate a potential target point in the flee direction
        Vector3 potentialTarget = transform.position + fleeDirection * (patrolRadius * 0.75f); // Flee within patrol bounds

        // Raycast to find a valid ground point to flee towards
        RaycastHit hit;
        if (Physics.Raycast(potentialTarget + Vector3.up * 5f, Vector3.down, out hit, 10f, groundLayer))
        {
            currentTargetPosition = hit.point;
            hasTarget = true;
        }
        else
        {
            // If no valid ground point found in flee direction, might stop or patrol instead
            // For simplicity, we set hasTarget to false; BaseEnemy logic might switch state.
            hasTarget = false;
            // Debug.LogWarning($"[{gameObject.name}] Could not find ground for flee target.");
        }

        // If a valid flee target was found, move towards it
        if(hasTarget)
        {
            MoveTowards(currentTargetPosition);
            // Optionally look where you are going when fleeing
            LookTowards(currentTargetPosition);
            // Or keep looking away from the player:
            // LookTowards(transform.position + fleeDirection);
        }
    }

    // --- Helper Methods ---

    // Moves the enemy towards a target position, ignoring vertical movement.
    void MoveTowards(Vector3 target)
    {
        Vector3 direction = (target - transform.position).normalized;
        direction.y = 0; // Keep movement horizontal
        transform.position += direction * moveSpeed * Time.deltaTime;
     }

    // Rotates the enemy to face a target position horizontally.
    void LookTowards(Vector3 target)
    {
        Vector3 direction = (target - transform.position).normalized;
        if (direction != Vector3.zero) // Avoid errors when target is at current position
        {
            direction.y = 0; // Look horizontally only
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            // Smoothly rotate towards the target rotation
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
     }

    // Keeps the enemy positioned correctly on the ground using a raycast.
    void StickToGround()
    {
         RaycastHit hit;
         // Start ray slightly above the enemy's pivot point
         Vector3 rayStart = transform.position + Vector3.up * 0.5f;
         // Raycast downwards to find the ground
         if (Physics.Raycast(rayStart, Vector3.down, out hit, groundCheckDistance + 0.5f, groundLayer)) // Check slightly further than groundCheckDistance
         {
             // Calculate the desired position slightly above the hit point
             Vector3 targetPosition = hit.point + Vector3.up * groundOffset;
             // Smoothly move the enemy to the target ground position
             // Lerp helps prevent jittering if the ground is uneven
             transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f); // Adjust lerp speed as needed
         }
         // Optional: Handle case where no ground is found (e.g., falling) - could disable movement or apply gravity.
     }

    // --- Overrides ---

    // Returns the specific attack range for the Sludge Enemy.
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
    // We don't need to override Die() here unless SludgeEnemy has *additional* death behavior
    // *before* the base DieCoroutine runs its course (which is uncommon).
    // protected override void Die() { base.Die(); } // No longer needed

} // End of SludgeEnemy class