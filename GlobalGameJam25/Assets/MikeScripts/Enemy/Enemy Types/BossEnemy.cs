using UnityEngine;
using System.Collections;

// Requires BaseEnemy, potentially HealthBar, PlayerStateManager, EnemyProjectile
[RequireComponent(typeof(AudioSource))] // Already required by BaseEnemy, but good practice
public class BossEnemy : BaseEnemy
{
    [Header("Boss Specific Stats")]
    [Tooltip("Initial movement speed. Will increase with phases.")]
    public float initialMoveSpeed = 1.0f; // Start very slow
    [Tooltip("Initial rotation speed (degrees/sec). Will increase with phases.")]
    public float initialRotationSpeed = 45f; // Start very slow rotation
    [Tooltip("Initial damage for melee attacks. Will increase with phases.")]
    public int initialMeleeDamage = 25; // Start strong
    [Tooltip("Initial damage for projectile attacks. Will increase with phases.")]
    public int initialProjectileDamage = 20; // Start strong
    [Tooltip("Range for switching to melee attack.")]
    public float meleeAttackRange = 3.0f;
    [Tooltip("Maximum range for projectile attacks (should be <= detectionRange).")]
    public float rangedAttackRange = 15.0f; // Use this instead of BaseEnemy's detectionRange for attacking
    [Tooltip("Cooldown between any attacks (melee or ranged).")]
    public float attackCooldown = 3.0f;
    [Tooltip("Field of view angle in degrees.")]
    [Range(1f, 360f)]
    public float fieldOfViewAngle = 90f;
    [Tooltip("How high above the pivot point to perform visibility checks from.")]
    public float eyeHeightOffset = 1.0f;

    [Header("Phase Change Settings")]
    [Tooltip("How many hits the boss can take.")]
    public int bossMaxHealth = 6; // Overrides BaseEnemy's maxHealth logic internally
    [Tooltip("Sound played when changing phases.")]
    public AudioClip phaseChangeSound;
    [Tooltip("Duration of the pause during phase change.")]
    public float phaseChangePauseDuration = 2.0f;
    [Tooltip("Particle effect to play during phase change (optional).")]
    public ParticleSystem phaseChangeEffect; // Assign in Inspector if you have one

    [Header("Gas Attack Components (Required for Ranged)")]
    [Tooltip("The projectile prefab to instantiate when attacking.")]
    public GameObject projectilePrefab;
    [Tooltip("The point from which projectiles are fired.")]
    public Transform firePoint;
    [Tooltip("The speed of the fired projectiles.")]
    public float projectileSpeed = 10f;

    [Header("Sludge Attack Components (Required for Melee)")]
    [Tooltip("Particle system instance played during melee attack.")]
    public ParticleSystem meleeAttackEffectInstance;
    [Tooltip("Optional: Point where the melee attack particle effect originates.")]
    public Transform meleeAttackEffectOrigin;
    [Tooltip("Sound played for melee attack.")]
    public AudioClip meleeAttackSound;
    [Tooltip("Sound played for ranged attack.")]
    public AudioClip rangedAttackSound; // Separate from melee

    [Header("Activation")]
    [Tooltip("Assign the BoxCollider trigger that activates the boss fight.")]
    public Collider activationTriggerZone; // Assign in Inspector

    [Header("References")]
    [Tooltip("Reference to the enemy's health bar UI.")]
    public HealthBar healthBar; // Assign if using a health bar

    // --- Internal State ---
    private float currentMoveSpeed;
    private float currentRotationSpeed;
    private int currentMeleeDamage;
    private int currentProjectileDamage;
    private float attackTimer = 0f;
    private int currentPhase = 1;
    private bool isActivated = false;
    private bool isChangingPhase = false;
    private int hitsTaken = 0; // Track hits specifically for phase changes

    // Layer mask for visibility checks (ignores triggers, potentially other enemies)
    private LayerMask visibilityLayerMask;

    // --- Overrides & New Methods ---

    protected override void Awake()
    {
        // Don't call base.Awake() as we manage health differently
        myAreaManager = FindFirstObjectByType<AreaCleansingManager>();

        // --- AUDIO: Get the main AudioSource ---
        mainAudioSource = GetComponent<AudioSource>();
        if (mainAudioSource == null)
        {
            Debug.LogError($"[{gameObject.name}] Critical Error: Missing required AudioSource component!");
        }
        mainAudioSource.playOnAwake = false;
        mainAudioSource.loop = false;
        // ------------------------------------

        // Set initial stats based on Inspector values
        currentHealth = bossMaxHealth; // Use boss health
        maxHealth = bossMaxHealth; // Sync max health for potential base class usage (like flee check if not overridden)
        currentMoveSpeed = initialMoveSpeed;
        currentRotationSpeed = initialRotationSpeed;
        currentMeleeDamage = initialMeleeDamage;
        currentProjectileDamage = initialProjectileDamage;

        currentPhase = 1;
        isActivated = false;
        isChangingPhase = false;
        hitsTaken = 0;

        // Disable the activation trigger collider once assigned to prevent re-triggering issues
        // if (activationTriggerZone != null)
        // {
        //     activationTriggerZone.enabled = true; // Ensure it's enabled initially
        // }
        // else
        // {
        //     Debug.LogError($"[{gameObject.name}] Boss Activation Trigger Zone not assigned in Inspector!", this);
        //     // Optionally activate immediately if no trigger is set for testing
        //     // isActivated = true;
        // }

        // Setup layer mask for visibility check - Exclude IgnoreRaycast layer by default
        // You might want to customize this further
        visibilityLayerMask = ~LayerMask.GetMask("Ignore Raycast", "Trigger"); // Ignore self, triggers

        // Disable attack effects initially
        if (meleeAttackEffectInstance != null && meleeAttackEffectInstance.main.playOnAwake)
        {
             var main = meleeAttackEffectInstance.main; main.playOnAwake = false;
        }
        if (phaseChangeEffect != null && phaseChangeEffect.main.playOnAwake)
        {
             var main = phaseChangeEffect.main; main.playOnAwake = false;
        }
    }

    protected override void Start()
    {
        // Call base.Start() AFTER setting playerTransform if needed by base class logic
        // base.Start(); // Be careful what this initializes

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) playerTransform = playerObject.transform;
        else Debug.LogWarning($"Enemy '{gameObject.name}' could not find GameObject with tag 'Player'.", this);

        UpdateHealthBar(); // Update health bar with boss health

        // --- AUDIO: Start Ambient Grunt Loop (If configured) ---
        if (ambientAudioSource != null && ambientGruntSound != null)
        {
            ambientAudioSource.clip = ambientGruntSound;
            ambientAudioSource.loop = true;
            ambientAudioSource.playOnAwake = false;
            // Don't play ambient sound until activated? Or play softly?
            // ambientAudioSource.Play();
        }

        // Ensure the boss script is enabled, but it won't do much until activated
        this.enabled = true;
    }

    // --- Main Update Loop ---
    protected override void Update()
    {
        // --- Pre-computation Checks ---
        // If not activated, dead, changing phase, or frozen, do nothing
        if (!isActivated || currentState == EnemyState.Dying || isChangingPhase || _isFrozen)
        {
            // If frozen, handle freeze timer (from BaseEnemy)
            if (_isFrozen) HandleFreezeTimer(); // Ensure freeze timer still counts down
            // Optionally stop sounds if paused/frozen
            if (_isFrozen && ambientAudioSource != null && ambientAudioSource.isPlaying) ambientAudioSource.Pause();
            return;
        }

        // If we were frozen but are not anymore, transition back
        if (currentState == EnemyState.Frozen && !_isFrozen)
        {
             if (ambientAudioSource != null && !ambientAudioSource.isPlaying && ambientGruntSound != null) ambientAudioSource.UnPause();
             TransitionToState(EnemyState.Idle); // Go back to idle/seeking after unfreeze
        }

        // Ensure ambient sound plays if active and not frozen/dying
        if (isActivated && ambientAudioSource != null && !ambientAudioSource.isPlaying && ambientGruntSound != null && !_isFrozen && currentState != EnemyState.Dying)
        {
            ambientAudioSource.Play();
        }

        // --- Core Logic ---
        attackTimer += Time.deltaTime; // Increment attack timer

        if (playerTransform == null) return; // Need player reference

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        bool playerVisible = CanSeePlayerFOV(distanceToPlayer);

        // --- State Logic ---
        if (playerVisible)
        {
            // Player is visible, decide whether to chase or attack
            if (distanceToPlayer <= meleeAttackRange)
            {
                // Close enough for melee
                TransitionToState(EnemyState.Attack);
                Attack(); // Call attack logic (will handle melee internally)
            }
            else if (distanceToPlayer <= rangedAttackRange)
            {
                // Within ranged attack range, but outside melee range
                TransitionToState(EnemyState.Attack);
                Attack(); // Call attack logic (will handle ranged internally)
            }
            else if (distanceToPlayer <= detectionRange)
            {
                // Visible but too far to attack, chase them
                TransitionToState(EnemyState.Chase);
                Chase();
            }
            else
            {
                 // Visible but outside even detection range (shouldn't happen if CanSeePlayerFOV checks range)
                 TransitionToState(EnemyState.Idle); // Or Patrol if you implement it
                 // Patrol();
            }
        }
        else
        {
            // Player is not visible (but boss is active) -> Rotate towards player
            TransitionToState(EnemyState.Idle); // Use Idle state for seeking/rotating
            RotateTowardsPlayer();
            // StopMovement(); // Ensure boss doesn't move while seeking
        }
    }

    // --- Activation Trigger ---
    void OnTriggerEnter(Collider other)
    {
        // Check if already activated or if the collider isn't the designated trigger
        if (isActivated || other != activationTriggerZone) return;

        if (other.CompareTag("Player"))
        {
            Debug.Log($"[{gameObject.name}] Player entered activation zone. Boss fight started!");
            isActivated = true;
            // Play spawn/activation sound
            PlaySound(spawnSound, mainAudioSource);
             // Start ambient sound if not already playing
            if (ambientAudioSource != null && !ambientAudioSource.isPlaying && ambientGruntSound != null)
            {
                ambientAudioSource.Play();
            }
            // Disable the trigger after activation
            activationTriggerZone.enabled = false;
            // Optionally transition to an initial state like Chase immediately
            TransitionToState(EnemyState.Idle); // Start in Idle (will rotate if needed) or Chase
        }
    }

    // --- Visibility Check ---
    bool CanSeePlayerFOV(float currentDistance)
    {
        if (playerTransform == null) return false;

        // 1. Distance Check (using overall detection range)
        if (currentDistance > detectionRange)
        {
            return false;
        }

        Vector3 eyePosition = transform.position + Vector3.up * eyeHeightOffset;
        Vector3 directionToPlayer = playerTransform.position - eyePosition;
        directionToPlayer.y = 0; // Check FOV mainly on the horizontal plane

        // 2. Angle Check
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        if (angle > fieldOfViewAngle / 2f)
        {
            return false; // Outside FOV cone
        }

        // 3. Line of Sight Check (Raycast)
        RaycastHit hit;
        // Use the actual direction (with Y) for the raycast
        Vector3 directionToPlayerActual = playerTransform.position - eyePosition;
        if (Physics.Raycast(eyePosition, directionToPlayerActual.normalized, out hit, detectionRange, visibilityLayerMask))
        {
            // Check if the ray hit the player
            if (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform) || playerTransform.IsChildOf(hit.transform)) // Check hit self, child, or parent (for complex player rigs)
            {
                // Debug.DrawRay(eyePosition, directionToPlayerActual.normalized * hit.distance, Color.green);
                return true; // Path is clear to the player
            }
            else
            {
                // Debug.DrawRay(eyePosition, directionToPlayerActual.normalized * hit.distance, Color.red);
                return false; // Something obstructs the view
            }
        }
        else
        {
             // Raycast didn't hit anything within detection range (should hit player if they are close)
             // This case might mean player is slightly outside collider range but within FOV check range. Treat as not visible or adjust logic.
             // Let's consider it visible if nothing is hit within range (implies open space)
             // Debug.DrawRay(eyePosition, directionToPlayerActual.normalized * detectionRange, Color.yellow);
             // Check distance again to be sure they are within range
             return currentDistance <= detectionRange;
        }
    }


    // --- State Implementations ---

    protected override void Patrol()
    {
        // Boss doesn't patrol in the traditional sense once activated.
        // If in Idle state and player not visible, RotateTowardsPlayer is called from Update.
        // If in Idle state and player IS visible, Update should transition to Chase/Attack.
        // So, Patrol can likely remain empty or just ensure boss is stationary.
        // StopMovement(); // Example
    }

    protected override void Chase()
    {
        if (playerTransform == null) return;
        MoveTowards(playerTransform.position);
        LookTowards(playerTransform.position);
    }

    protected override void Attack()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        LookTowards(playerTransform.position); // Always face player when attacking

        // Decide attack type based on current distance
        if (distanceToPlayer <= meleeAttackRange)
        {
            // --- Melee Attack Logic ---
            // Stop moving closer if right at melee range
            // StopMovement(); // Optional: Boss might stop moving to melee

            if (attackTimer >= attackCooldown)
            {
                PerformMeleeAttack();
                attackTimer = 0f; // Reset cooldown
            }
        }
        else if (distanceToPlayer <= rangedAttackRange)
        {
            // --- Ranged Attack Logic ---
            // Optional: Boss might stop moving to fire, or keep moving slowly
            // StopMovement();

            if (attackTimer >= attackCooldown && projectilePrefab != null && firePoint != null)
            {
                FireProjectile();
                attackTimer = 0f; // Reset cooldown
            }
        }
        // If player moved out of both ranges while in Attack state, base Update logic should handle transition back to Chase
        // However, our custom Update loop handles this directly.
    }

    // Boss doesn't flee
    protected bool ShouldFlee() { return false; }
    protected override void Flee() { /* Do nothing */ }

    // --- Boss Specific Actions ---

    void RotateTowardsPlayer()
    {
        if (playerTransform == null) return;
        LookTowards(playerTransform.position); // Use the standard LookTowards with currentRotationSpeed
    }

    void PerformMeleeAttack()
    {
        // --- Play Attack Sound ---
        PlaySound(meleeAttackSound, mainAudioSource);

        // --- Play Attack Particle Effect ---
        if (meleeAttackEffectInstance != null)
        {
            if (meleeAttackEffectOrigin != null)
            {
                meleeAttackEffectInstance.transform.position = meleeAttackEffectOrigin.position;
                // Optional: Match rotation too if the effect direction matters
                // attackEffectInstance.transform.rotation = attackEffectOrigin.rotation;
            }
            meleeAttackEffectInstance.Play();
        }

        // --- Deal Damage ---
        // Use OverlapSphere to find player in front - more reliable for melee than just distance
        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * (meleeAttackRange * 0.5f), meleeAttackRange * 0.7f); // Check sphere slightly in front
        foreach (var hit in hits)
        {
             if (hit.CompareTag("Player"))
             {
                 PlayerStateManager player = hit.GetComponentInParent<PlayerStateManager>();
                 if (player != null)
                 {
                     Debug.Log($"[{gameObject.name}] Dealing {currentMeleeDamage} melee damage to player.");
                     player.TakeDamage(currentMeleeDamage);
                     break; // Damage player once
                 }
             }
        }
    }

    void FireProjectile()
    {
        PlaySound(rangedAttackSound, mainAudioSource);

        if (firePoint == null || playerTransform == null || projectilePrefab == null) return;

        // Calculate direction (slightly leading target might be better, but simple aim for now)
        // Aiming logic from GasEnemy (with slight upward tilt)
        Vector3 directionToPlayerFlat = (playerTransform.position - firePoint.position);
        directionToPlayerFlat.y = 0;
        directionToPlayerFlat.Normalize();
        Quaternion upwardTilt = Quaternion.AngleAxis(-15.0f, firePoint.right); // Adjust angle as needed
        Vector3 finalLaunchDirection = upwardTilt * directionToPlayerFlat;
        Quaternion finalLaunchRotation = Quaternion.LookRotation(finalLaunchDirection);

        GameObject projectileGO = Instantiate(projectilePrefab, firePoint.position, finalLaunchRotation);
        EnemyProjectile projectileScript = projectileGO.GetComponent<EnemyProjectile>();

        if (projectileScript != null)
        {
            projectileScript.damageAmount = this.currentProjectileDamage;
            projectileScript.speed = this.projectileSpeed;
            Debug.Log($"[{gameObject.name}] Firing projectile with {currentProjectileDamage} damage.");
        }
        else
        {
            Debug.LogError($"Projectile prefab '{projectilePrefab.name}' is missing the EnemyProjectile script!", projectileGO);
        }
    }

    // --- Phase Change Handling ---

    IEnumerator PhaseChangeSequence(int nextPhase)
    {
        isChangingPhase = true;
        TransitionToState(EnemyState.Idle); // Go to idle during pause
        // StopMovement(); // Ensure movement stops

        Debug.Log($"[{gameObject.name}] Starting Phase {nextPhase} transition...");

        // Play sound and effect
        PlaySound(phaseChangeSound, mainAudioSource);
        if (phaseChangeEffect != null) phaseChangeEffect.Play();

        // Wait
        yield return new WaitForSeconds(phaseChangePauseDuration);

        // Apply stat changes
        currentPhase = nextPhase;
        if (currentPhase == 2)
        {
            currentMoveSpeed = initialMoveSpeed * 2.0f;
            currentRotationSpeed = initialRotationSpeed * 1.5f; // Slightly faster rotation too?
            Debug.Log($"[{gameObject.name}] Entered Phase 2! Speed: {currentMoveSpeed}, Rotation: {currentRotationSpeed}");
        }
        else if (currentPhase == 3)
        {
            currentMoveSpeed = initialMoveSpeed * 3.0f;
            currentRotationSpeed = initialRotationSpeed * 2.0f; // Even faster rotation?
            currentMeleeDamage = initialMeleeDamage * 2;
            currentProjectileDamage = initialProjectileDamage * 2;
            Debug.Log($"[{gameObject.name}] Entered Phase 3! Speed: {currentMoveSpeed}, Rotation: {currentRotationSpeed}, MeleeDmg: {currentMeleeDamage}, ProjDmg: {currentProjectileDamage}");
        }

        // Stop effect if it's looping
        if (phaseChangeEffect != null && phaseChangeEffect.main.loop) phaseChangeEffect.Stop();

        isChangingPhase = false;
        // No need to explicitly transition state, Update loop will take over
         Debug.Log($"[{gameObject.name}] Phase {nextPhase} transition complete.");
    }


    // --- Overrides of BaseEnemy Methods ---

    // Override TakeDamage to handle hits and phase changes
    public override void TakeDamage(int damage, DamageType type = DamageType.Other)
    {
        if (currentState == EnemyState.Dying || isChangingPhase) return;

        // Base TakeDamage plays sound, reduces health, updates bar (but we manage health here)
        PlaySound(takeDamageSound, mainAudioSource); // Play hit sound

        // Reduce health (using our boss health)
        currentHealth -= damage; // Assume 1 hit = 1 damage for phase triggers
        currentHealth = Mathf.Clamp(currentHealth, 0, bossMaxHealth);
        hitsTaken++; // Increment hit counter regardless of damage amount

        Debug.Log($"[{gameObject.name}] Took hit #{hitsTaken}. Health: {currentHealth}/{bossMaxHealth}");
        UpdateHealthBar();

        if (currentHealth <= 0)
        {
            // Use the base DieCoroutine for effects, XP, etc.
            StartCoroutine(DieCoroutine()); // Make sure DieCoroutine exists and works in BaseEnemy
        }
        else
        {
            // Check for phase transitions based on HITS TAKEN
            if (hitsTaken == 2 && currentPhase == 1) // Just took 2nd hit
            {
                StartCoroutine(PhaseChangeSequence(2));
            }
            else if (hitsTaken == 4 && currentPhase == 2) // Just took 4th hit
            {
                StartCoroutine(PhaseChangeSequence(3));
            }
            // No flee logic for the boss
            // No aggro logic needed as boss is likely always aggro once activated
        }
    }

    // Override DieCoroutine if specific boss death behavior is needed,
    // otherwise ensure BaseEnemy.DieCoroutine handles sounds/effects/XP.
    // protected override IEnumerator DieCoroutine() { ... }

    // Override Freeze/Unfreeze if needed (e.g., different visual effect)
    // public override void Freeze(float duration) { ... base.Freeze(duration); ... }
    // protected override void Unfreeze() { ... base.Unfreeze(); ... }

    // This is less relevant now as Update checks ranges directly
    protected override float GetAttackRange()
    {
        // Return the closer range, maybe? Or just don't rely on this.
        return meleeAttackRange;
    }

    protected override void UpdateHealthBar()
    {
        healthBar?.SetMaxStats(bossMaxHealth); // Ensure max is correct
        healthBar?.SetHealth(currentHealth);
    }

    // --- Helper Movement/Rotation --- (Use current speeds)

    void MoveTowards(Vector3 target)
    {
        if (_isFrozen || isChangingPhase) return;
        Vector3 direction = (target - transform.position).normalized;
        direction.y = 0; // Keep movement horizontal
        transform.position += direction * currentMoveSpeed * Time.deltaTime;
    }

    void LookTowards(Vector3 target)
    {
        if (_isFrozen || isChangingPhase) return;
        Vector3 direction = (target - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            direction.y = 0; // Look horizontally
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, currentRotationSpeed * Time.deltaTime);
        }
    }

     void StopMovement()
     {
         // If using Rigidbody, set velocity to zero
         Rigidbody rb = GetComponent<Rigidbody>();
         if (rb != null)
         {
             rb.linearVelocity = Vector3.zero;
             rb.angularVelocity = Vector3.zero;
         }
         // If using CharacterController, you might not need this or handle it differently
     }

    // Optional: Gizmos for visualizing ranges and FOV
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) // Only draw calculated speeds/damages during play
        {
             currentMoveSpeed = initialMoveSpeed;
             currentRotationSpeed = initialRotationSpeed;
             currentMeleeDamage = initialMeleeDamage;
             currentProjectileDamage = initialProjectileDamage;
        }


        // Melee Range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeAttackRange);

        // Ranged Attack Range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, rangedAttackRange);

        // Detection Range (from BaseEnemy)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Field of View
        Gizmos.color = Color.cyan;
        Vector3 fovLine1 = Quaternion.AngleAxis(fieldOfViewAngle / 2, transform.up) * transform.forward * detectionRange;
        Vector3 fovLine2 = Quaternion.AngleAxis(-fieldOfViewAngle / 2, transform.up) * transform.forward * detectionRange;
        Gizmos.DrawRay(transform.position + Vector3.up * eyeHeightOffset, fovLine1);
        Gizmos.DrawRay(transform.position + Vector3.up * eyeHeightOffset, fovLine2);
        // Draw arc (simplified)
        // Handles.color = new Color(0f, 1f, 1f, 0.1f); // Requires UnityEditor namespace, only works in Editor
        // Handles.DrawSolidArc(transform.position + Vector3.up * eyeHeightOffset, transform.up, fovLine2, fieldOfViewAngle, detectionRange);

        // Current Target (if player exists)
        if (playerTransform != null)
        {
             Gizmos.color = Color.magenta;
             Gizmos.DrawLine(transform.position + Vector3.up * eyeHeightOffset, playerTransform.position);
        }
    }
     // Need using UnityEditor; at the top for Handles, but that prevents building the game.
     // Gizmos are usually sufficient for runtime debugging visualization.
}

