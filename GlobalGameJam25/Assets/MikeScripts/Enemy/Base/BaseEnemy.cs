using UnityEngine;

public enum EnemyState { Patrolling, Chasing, Attacking, Fleeing, Frozen, Dying }
public enum DamageType { Basic, Freeze, Other } // Assuming DamageType is defined

public abstract class BaseEnemy : MonoBehaviour
{
    public EnemyState currentState = EnemyState.Patrolling;
    [Header("Detection & Stats")]
    public float detectionRadius = 10f;
    public LayerMask playerLayer;
    public int maxHealth = 100;
    public int currentHealth;
    public float moveSpeed = 2f;

    [Header("References")]
    [Tooltip("Leave empty to find by tag 'Player' at runtime.")]
    public Transform playerTransform;
    public GameManager gameManager;

    [Header("Loot & Effects")] // Section added for XP Orb
    [Tooltip("The particle effect prefab to spawn when the enemy dies (represents XP). Assign your XP Orb Prefab here.")]
    public GameObject xpEffectPrefab; // <-- Assign your XpOrbEffect prefab in the Inspector
    [Tooltip("Amount of XP this enemy grants (used by Player on collection). Value could potentially be passed if needed.")]
    public int xpValue = 10; // Example value - Currently collected by Player script

    // Freeze related variables
    protected bool _isFrozen = false;
    protected float _freezeEndTime = 0f;
    protected EnemyState _stateBeforeFreeze;
    protected Renderer _renderer;
    protected Color _originalColor;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        _renderer = GetComponent<Renderer>();
        if (_renderer != null) _originalColor = _renderer.material.color;
    }

    protected virtual void Start()
    {
        // Find Player Reference via Tag
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
            else Debug.LogError($"<color=red>[{gameObject.name}]</color> FAILED TO FIND PLAYER OBJECT TAGGED 'Player' IN START!");
        }
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
    }

    protected virtual void Update()
    {
        if (_isFrozen) {
            if (Time.time >= _freezeEndTime) Unfreeze();
            else return;
        }
        if (playerTransform == null && currentState != EnemyState.Dying) { return; } // Basic check if player lost
        if (playerTransform != null) HandleStateMachine();
    }

    protected virtual void HandleStateMachine()
    {
        // Simplified state machine logic from previous version
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        switch (currentState) {
            case EnemyState.Patrolling:
                Patrol();
                if (distanceToPlayer < detectionRadius) ChangeState(EnemyState.Chasing);
                break;
            case EnemyState.Chasing:
                Chase();
                if (distanceToPlayer <= GetAttackRange()) ChangeState(EnemyState.Attacking);
                else if (distanceToPlayer > detectionRadius * 1.2f) ChangeState(EnemyState.Patrolling);
                break;
            case EnemyState.Attacking:
                Attack();
                if (distanceToPlayer > GetAttackRange()) ChangeState(EnemyState.Chasing);
                break;
            case EnemyState.Fleeing:
                Flee();
                if (distanceToPlayer > detectionRadius * 1.5f) ChangeState(EnemyState.Patrolling);
                break;
            case EnemyState.Dying: break;
            case EnemyState.Frozen: break;
        }
    }

    protected virtual void ChangeState(EnemyState newState)
    {
        if (currentState == EnemyState.Dying || currentState == newState || _isFrozen) return;
        currentState = newState;
    }

    public virtual void TakeDamage(int damage, DamageType type = DamageType.Other)
    {
        if (currentState == EnemyState.Dying) return;
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
        UpdateHealthBar();
        if (currentHealth <= 0) {
            if(currentState != EnemyState.Dying) { ChangeState(EnemyState.Dying); Die(); }
        } else if (currentHealth <= maxHealth * 0.25f && currentState != EnemyState.Fleeing && currentState != EnemyState.Frozen) {
            ChangeState(EnemyState.Fleeing);
        }
    }

    public virtual void Freeze(float baseDuration)
    {
        if (_isFrozen || currentState == EnemyState.Dying) return;
        _isFrozen = true;
        _freezeEndTime = Time.time + Random.Range(Mathf.Max(1f, baseDuration * 0.8f), baseDuration * 1.2f);
        _stateBeforeFreeze = currentState;
        currentState = EnemyState.Frozen;
        if (_renderer != null) _renderer.material.color = Color.cyan;
    }

     protected virtual void Unfreeze()
     {
         _isFrozen = false;
         EnemyState stateToRevertTo = _stateBeforeFreeze;
         if (_stateBeforeFreeze == EnemyState.Fleeing && currentHealth > maxHealth * 0.25f) stateToRevertTo = EnemyState.Patrolling;
         if(currentState == EnemyState.Frozen) currentState = stateToRevertTo;
         if (_renderer != null) _renderer.material.color = _originalColor;
     }

    // --- Die Method Modified ---
    protected virtual void Die() {
        if (!enabled) return; // Prevent multi-calls if already dying/disabled
        Debug.Log($"{gameObject.name} withered away!");

        // --- Spawn XP Effect ---
        if (xpEffectPrefab != null && playerTransform != null) // Check player still exists
        {
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f; // Spawn slightly above pivot
            Instantiate(xpEffectPrefab, spawnPos, Quaternion.identity);
            // The XpOrbDelay script on the prefab handles delayed homing activation.
            // The Player's XpCollector script handles receiving the XP via triggers.
        } else if (xpEffectPrefab == null) {
            Debug.LogWarning($"[{gameObject.name}] No xpEffectPrefab assigned in Inspector.");
        }
        // --- End Spawn XP Effect ---

        this.enabled = false; // Disable script immediately
        gameManager?.EnemyDied(gameObject); // Notify GM
        Destroy(gameObject, 0.1f); // Destroy shortly after
    }

    // --- Abstract methods & Other Virtuals ---
    protected abstract void Patrol();
    protected abstract void Chase();
    protected abstract void Attack();
    protected abstract void Flee();
    protected abstract float GetAttackRange();
    protected virtual void UpdateHealthBar() { /* Implement in child or leave empty */ }

    // OnDrawGizmosSelected remains the same
    public virtual void OnDrawGizmosSelected() {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectionRadius);
        float attackRange = GetAttackRange();
        if(currentState != EnemyState.Patrolling && attackRange > 0) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange); }
        if (playerTransform != null) { Gizmos.color = Color.magenta; Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, playerTransform.position + Vector3.up * 0.1f); }
    }
}