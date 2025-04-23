using UnityEngine;
using System.Collections; // Needed for Coroutine

// Add this enum if you don't have it already
public enum DamageType { Basic, Freeze, Other } // Example types

public abstract class BaseEnemy : MonoBehaviour
{
    [Header("Base Enemy Stats")]
    public int maxHealth = 50;
    public int currentHealth;
    public float moveSpeed = 3f;
    public float detectionRange = 20f;
    public float loseSightRange = 25f;
    public float fleeHealthPercentage = 0.3f; // Flee when below 30% health
    [Tooltip("How long the enemy stays frozen by default.")]
    public float baseFreezeDuration = 5f;

    // ----- XP Value -----
    [Header("XP")]
    [Tooltip("How much XP this enemy grants when killed.")]
    public int xpValue = 10; // Default value, can be set per enemy type in Inspector
    // ----- END XP Value -----

    [Header("Effects & References")]
    public GameObject deathEffectPrefab; // Assign prefab for death particles/sound
    public GameObject freezeEffectPrefab; // Assign prefab for freeze visual effect
    [Tooltip("The object representing the visual freeze effect, instantiated when frozen.")]
    protected GameObject currentFreezeEffectInstance;
    // ----- ADD THIS LINE -----
    [Tooltip("Reference to the Area Manager this enemy belongs to. Assigned by Spawner.")]
    public AreaCleansingManager myAreaManager;
    // -------------------------


    // State Machine
    public enum EnemyState { Idle, Patrol, Chase, Attack, Flee, Frozen, Dying }
    public EnemyState currentState = EnemyState.Idle;

    protected Transform playerTransform;
    protected bool _isFrozen = false;
    protected float freezeTimer = 0f;
    protected float fleeHealthThreshold;

    // --- Abstract Methods (Must be implemented by derived classes) ---
    protected abstract void Patrol();
    protected abstract void Chase();
    protected abstract void Attack();
    protected abstract void Flee();
    protected abstract float GetAttackRange(); // Derived classes define their specific attack range
    protected abstract void UpdateHealthBar(); // Derived classes handle their specific health bar logic

    // --- Virtual Methods (Can be optionally overridden by derived classes) ---
    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        fleeHealthThreshold = maxHealth * fleeHealthPercentage;
    }

    protected virtual void Start()
    {
        // Find player once at the start (can be improved with a GameManager or event system)
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
        }
        else
        {
            Debug.LogWarning($"Enemy '{gameObject.name}' could not find GameObject with tag 'Player'.", this);
        }
        UpdateHealthBar(); // Initial health bar update

        // --- Optional: Add a check/warning if the Area Manager isn't assigned ---
        // Note: This check runs in Start. Spawner assigns it AFTER Instantiate but before Start sometimes.
        // A warning here might be premature if the spawner assigns it correctly.
        // if (myAreaManager == null)
        // {
        //     Debug.LogWarning($"[{gameObject.name}] does not have an AreaCleansingManager assigned in Start. Ensure it's set via Spawner.", this);
        // }
        // ----------------------------------------------------------------------
    }

    protected virtual void Update()
    {
        if (currentState == EnemyState.Dying) return; // Don't do anything if dying

        HandleFreezeTimer();

        if (_isFrozen)
        {
            // If currently frozen, don't run other state logic
            if (currentState != EnemyState.Frozen) TransitionToState(EnemyState.Frozen);
            return;
        }
        else if (currentState == EnemyState.Frozen)
        {
            // If NOT frozen anymore but state is Frozen, transition back (e.g., to Idle)
            TransitionToState(EnemyState.Idle);
        }

        // --- State Machine Logic ---
        float distanceToPlayer = GetDistanceToPlayer();

        switch (currentState)
        {
            case EnemyState.Idle:
                // Look for player
                if (CanSeePlayer(distanceToPlayer)) TransitionToState(EnemyState.Chase);
                else Patrol(); // Or just stand still if Idle shouldn't patrol
                break;

            case EnemyState.Patrol:
                Patrol();
                // Check for player
                if (CanSeePlayer(distanceToPlayer)) TransitionToState(EnemyState.Chase);
                break;

            case EnemyState.Chase:
                // Check flee condition first
                if (ShouldFlee()) TransitionToState(EnemyState.Flee);
                // Check attack range
                else if (distanceToPlayer <= GetAttackRange()) TransitionToState(EnemyState.Attack);
                // Check if player is lost
                else if (!CanSeePlayer(distanceToPlayer, loseSightRange)) TransitionToState(EnemyState.Patrol); // Go back to patrol if lost
                else Chase(); // Continue chasing
                break;

            case EnemyState.Attack:
                // Check flee condition first
                if (ShouldFlee()) TransitionToState(EnemyState.Flee);
                // Check if player moved out of range
                else if (distanceToPlayer > GetAttackRange() * 1.1f) TransitionToState(EnemyState.Chase); // Give a little buffer (1.1f)
                // Check if player is lost (e.g., behind cover)
                else if (!CanSeePlayer(distanceToPlayer, loseSightRange)) TransitionToState(EnemyState.Patrol);
                else Attack(); // Continue attacking
                break;

            case EnemyState.Flee:
                // If health recovered or player is far away, stop fleeing
                if (!ShouldFlee() || distanceToPlayer > loseSightRange * 1.5f) TransitionToState(EnemyState.Patrol);
                else Flee();
                break;

            case EnemyState.Frozen:
                // Logic is handled by HandleFreezeTimer and the start of Update
                break;
        }
    }

    protected virtual void TransitionToState(EnemyState newState)
    {
        if (currentState == newState || currentState == EnemyState.Dying) return;

        currentState = newState;
    }


    // --- Damage & Effects ---

    public virtual void TakeDamage(int damage, DamageType type = DamageType.Other)
    {
        if (currentState == EnemyState.Dying || damage <= 0) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log($"{gameObject.name} took {damage} ({type}) damage. Health: {currentHealth}/{maxHealth}");
        UpdateHealthBar();

        if (currentHealth <= 0)
        {
            Die();
        }
        else if (ShouldFlee() && currentState != EnemyState.Flee && currentState != EnemyState.Frozen)
        {
            TransitionToState(EnemyState.Flee);
        }
        else if (currentState == EnemyState.Idle || currentState == EnemyState.Patrol)
        {
             TransitionToState(EnemyState.Chase);
        }
    }

    protected virtual void Die()
    {
        if (currentState == EnemyState.Dying) return; // Prevent multiple calls

        TransitionToState(EnemyState.Dying);
        Debug.Log($"{gameObject.name} has died.");

        // ----- Grant XP Directly OR Spawn Orb -----
        // Decide which method you prefer:

        // METHOD 1: Grant XP Directly (Simpler, use this if you removed the orb script)
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            PlayerStateManager playerState = playerObject.GetComponent<PlayerStateManager>();
            if (playerState != null)
            {
                playerState.GainXP(xpValue); // Grant XP directly
            }
            else { Debug.LogError($"Enemy '{gameObject.name}': Could not find PlayerStateManager on player object!", playerObject); }
        }
        else { Debug.LogWarning($"Enemy '{gameObject.name}': Could not find Player object to grant XP."); }

        // METHOD 2: Spawn XP Orb (Use this if you have the XpOrbDelay script on the prefab)
        // Comment out Method 1 above if using this.
        // if (deathEffectPrefab != null) // Assuming deathEffectPrefab IS the XP Orb
        // {
        //     Instantiate(deathEffectPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        // }
        // else { Debug.LogWarning($"[{gameObject.name}] No deathEffectPrefab (XP Orb) assigned in Inspector."); }
        // ----- END XP GRANT -----


        // --- Notify the Area Manager! ---
        if (myAreaManager != null)
        {
            myAreaManager.RegisterMonsterKill();
        }
        else
        {
            // Warning if an enemy dies without being part of an area cleansing process
            Debug.LogWarning($"[{gameObject.name}] died but was not assigned to an AreaCleansingManager by its Spawner. Progression in its area won't be tracked.", this);
        }
        // --- End Notify Area Manager ---


        // Optional: Disable components instead of destroying immediately for effects
        Collider col = GetComponent<Collider>(); if (col != null) col.enabled = false;
        Rigidbody rb = GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = true; // Stop physics

        // Destroy the GameObject after a short delay (allows effects to play)
        Destroy(gameObject, 0.1f); // Adjust delay as needed
    }

    public virtual void Freeze(float duration)
    {
        if (_isFrozen || currentState == EnemyState.Dying) return; // Don't freeze if already frozen or dying

        _isFrozen = true;
        freezeTimer = duration; // Set the timer
        TransitionToState(EnemyState.Frozen); // Ensure state reflects frozen status

        Debug.Log($"{gameObject.name} frozen for {duration} seconds.");

        // Instantiate freeze visual effect if assigned
        if (freezeEffectPrefab != null && currentFreezeEffectInstance == null)
        {
            currentFreezeEffectInstance = Instantiate(freezeEffectPrefab, transform.position, transform.rotation, transform); // Parent to enemy
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if(rb != null) rb.isKinematic = true;
    }

    protected virtual void Unfreeze()
    {
        if (!_isFrozen) return; // Only unfreeze if actually frozen

        _isFrozen = false;
        freezeTimer = 0f;
        // State will transition out of Frozen in the next Update() call

        Debug.Log($"{gameObject.name} un-frozen.");

        if (currentFreezeEffectInstance != null)
        {
            Destroy(currentFreezeEffectInstance);
            currentFreezeEffectInstance = null;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if(rb != null) rb.isKinematic = false;
    }

    protected virtual void HandleFreezeTimer()
    {
        if (_isFrozen && freezeTimer > 0)
        {
            freezeTimer -= Time.deltaTime;
            if (freezeTimer <= 0)
            {
                Unfreeze();
            }
        }
    }

    // --- Helper Methods ---

    protected float GetDistanceToPlayer()
    {
        if (playerTransform == null) return float.MaxValue; // Player not found or destroyed
        return Vector3.Distance(transform.position, playerTransform.position);
    }

    protected bool CanSeePlayer(float currentDistance, float rangeOverride = -1f)
    {
        if (playerTransform == null) return false;
        float checkRange = (rangeOverride > 0) ? rangeOverride : detectionRange;
        if (currentDistance > checkRange) return false; // Too far away
        return true; // Simple distance check for now
    }

    protected bool ShouldFlee()
    {
        return currentHealth <= fleeHealthThreshold && currentHealth > 0;
    }
}