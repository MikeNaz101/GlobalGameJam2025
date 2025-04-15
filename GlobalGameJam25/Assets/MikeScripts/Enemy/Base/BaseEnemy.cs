// --- Updated BaseEnemy (Showing the TakeDamage modification) ---
using UnityEngine;
using UnityEngine.AI; // For NavMeshAgent

public enum DamageType
{
    Basic,
    Freeze,
    Other // For any other damage source
}
public enum EnemyState
{
    Patrolling,
    Chasing,
    Attacking,
    Fleeing,
    Frozen, // Added Frozen state
    Dying
}

// Assuming DamageType enum is defined elsewhere or above

public abstract class BaseEnemy : MonoBehaviour
{
    public EnemyState currentState = EnemyState.Patrolling;
    public float detectionRadius = 5f;
    public LayerMask playerLayer;
    public int maxHealth = 100;
    public int currentHealth;
    public float moveSpeed = 2f;
    public Transform playerTransform;
    public GameManager gameManager; // Keep reference if needed

    // Freeze related variables
    protected bool _isFrozen = false;
    protected float _freezeEndTime = 0f;
    protected EnemyState _stateBeforeFreeze; // To remember state

    // Optional: For visual feedback consistency
    protected Renderer _renderer;
    protected Color _originalColor;

    protected virtual void Awake() // Changed Start to Awake for component caching
    {
        currentHealth = maxHealth;
        _renderer = GetComponent<Renderer>(); // Cache renderer
        if (_renderer != null)
        {
            _originalColor = _renderer.material.color; // Store original color
        }
        else
        {
            Debug.LogWarning($"Renderer component not found on {gameObject.name}. Freeze visual feedback might not work.");
        }

        // Find player later in Start or ensure player exists before accessing
    }

    protected virtual void Start()
    {
        // It's often safer to find player here in case player spawns later
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }
        if (playerTransform == null)
        {
            Debug.LogError($"Player not found for {gameObject.name}! Enemy AI might not function correctly.");
            // Optionally disable the script if player is crucial: this.enabled = false;
        }

        gameManager = FindObjectOfType<GameManager>(); // Find GameManager
        if (gameManager == null)
        {
            Debug.LogError("GameManager not found!");
        }
    }

    protected virtual void Update()
    {
        // --- Freeze Check ---
        if (_isFrozen)
        {
            if (Time.time >= _freezeEndTime)
            {
                Unfreeze();
            }
            else
            {
                // If frozen, do nothing else this frame
                return;
            }
        }
        // --- End Freeze Check ---


        // Ensure player exists before proceeding
        if (playerTransform == null) return;

        // State logic only runs if not frozen
        HandleStateMachine();
    }

    protected virtual void HandleStateMachine()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        switch (currentState)
        {
            case EnemyState.Patrolling:
                Patrol();
                // Transition Check: Detect Player -> Chase
                if (distanceToPlayer < detectionRadius)
                {
                    ChangeState(EnemyState.Chasing);
                }
                break;

            case EnemyState.Chasing:
                Chase();
                // Transition Check: In Attack Range -> Attack
                if (distanceToPlayer <= GetAttackRange())
                {
                    ChangeState(EnemyState.Attacking);
                }
                // Transition Check: Player escaped -> Patrol
                else if (distanceToPlayer > detectionRadius * 1.2f) // Add a buffer to prevent rapid switching
                {
                     ChangeState(EnemyState.Patrolling);
                }
                break;

            case EnemyState.Attacking:
                Attack();
                // Transition Check: Player out of range -> Chase
                if (distanceToPlayer > GetAttackRange())
                {
                    ChangeState(EnemyState.Chasing);
                }
                break;

            case EnemyState.Fleeing:
                Flee();
                // Transition Check: Healed or Player far away? -> Patrol/Chase
                // Example: Stop fleeing if player is beyond detection range
                 if (distanceToPlayer > detectionRadius * 1.5f)
                 {
                     ChangeState(EnemyState.Patrolling);
                 }
                 // Example: Stop fleeing if health recovers (needs a healing mechanic)
                 // if (currentHealth >= maxHealth * 0.5f) { ChangeState(EnemyState.Patrolling); }
                break;

            case EnemyState.Dying:
                // The Die method handles the logic and destruction
                break;

            case EnemyState.Frozen:
                 // Logic is handled by the _isFrozen flag check at the start of Update
                 // No state transitions *from* Frozen happen here; they happen in Unfreeze()
                 break;
        }
    }

     // Method to handle state changes (optional but good practice)
    protected virtual void ChangeState(EnemyState newState)
    {
        if (currentState == newState || currentState == EnemyState.Dying || _isFrozen) return; // Don't change if dying or frozen

        // Optional: Add ExitState logic here if needed for specific states
        // Debug.Log($"{gameObject.name} changing state from {currentState} to {newState}");
        currentState = newState;
        // Optional: Add EnterState logic here if needed
    }


    // Updated TakeDamage to include DamageType
    public virtual void TakeDamage(int damage, DamageType type = DamageType.Other)
    {
        if (currentState == EnemyState.Dying) return; // Can't take damage if already dying

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
        Debug.Log($"{gameObject.name} took {damage} damage ({type}), health: {currentHealth}/{maxHealth}");

        // Update Health Bar if it exists (moved here for central update)
        UpdateHealthBar();

        // --- State Transitions based on Health ---
        if (currentHealth <= 0)
        {
            ChangeState(EnemyState.Dying);
            Die(); // Call Die immediately when health hits 0
        }
        // Fleeing threshold (example: flee below 25% health)
        else if (currentHealth <= maxHealth * 0.25f && currentState != EnemyState.Fleeing)
        {
            ChangeState(EnemyState.Fleeing);
        }
        // If took damage while patrolling, might start chasing immediately
        else if (currentState == EnemyState.Patrolling && type != DamageType.Freeze) // Don't chase if just frozen
        {
             ChangeState(EnemyState.Chasing);
        }
    }

    // Public method to be called by the Freeze Bullet
    public virtual void Freeze(float baseDuration) // Duration can be passed by bullet or use defaults
    {
        if (_isFrozen || currentState == EnemyState.Dying) return; // Already frozen or dying

        _isFrozen = true;
        // Make duration slightly random around the base value
        float freezeDuration = Random.Range(Mathf.Max(1f, baseDuration - 1f), baseDuration + 1f); // Example variation
        _freezeEndTime = Time.time + freezeDuration;
        _stateBeforeFreeze = currentState; // Remember what we were doing
        currentState = EnemyState.Frozen; // Set the state explicitly

        Debug.Log($"{gameObject.name} frozen for {freezeDuration:F1} seconds.");

        // Visual Feedback
        if (_renderer != null)
        {
            _renderer.material.color = Color.cyan; // Change color to cyan
        }
        // Optional: Stop NavMeshAgent if using one
        // NavMeshAgent agent = GetComponent<NavMeshAgent>();
        // if (agent != null && agent.enabled) { agent.isStopped = true; }
    }

    // Method to handle unfreezing
     protected virtual void Unfreeze()
     {
         _isFrozen = false;
         if (currentState == EnemyState.Frozen) // Only revert state if we were actually in Frozen state
         {
             currentState = _stateBeforeFreeze; // Revert to the previous state
         }
         Debug.Log($"{gameObject.name} unfrozen, returning to state: {currentState}");

         // Restore Visuals
         if (_renderer != null)
         {
             _renderer.material.color = _originalColor; // Restore original color
         }
         // Optional: Resume NavMeshAgent
         // NavMeshAgent agent = GetComponent<NavMeshAgent>();
         // if (agent != null && agent.enabled) { agent.isStopped = false; }
     }

    // Abstract methods to be implemented by derived classes
    protected abstract void Patrol();
    protected abstract void Chase();
    protected abstract void Attack();
    protected abstract void Flee();
    protected abstract float GetAttackRange();

    // Optional: Abstract method for health bar update if needed by children
    protected virtual void UpdateHealthBar() { /* Base implementation can be empty */ }


    protected virtual void Die()
    {
        Debug.Log(gameObject.name + " has withered away!");
        // Stop all movement/AI immediately
        // Optional: Stop NavMeshAgent
         // NavMeshAgent agent = GetComponent<NavMeshAgent>();
         // if (agent != null) { agent.enabled = false; }
         this.enabled = false; // Disable script to stop Update calls

        gameManager?.EnemyDied(gameObject); // Notify GameManager

        // Optional: Play death animation/particle effect before destroying
        // Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject, 0.1f); // Destroy after a short delay
    }

    public virtual void OnDrawGizmosSelected()
    {
        // Detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Attack radius (drawn by derived classes typically)
    }
}