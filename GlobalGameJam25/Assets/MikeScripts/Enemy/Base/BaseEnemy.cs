using UnityEngine;
using UnityEngine.AI; // For NavMeshAgent

public enum EnemyState
{
    Patrolling,
    Chasing,
    Attacking,
    Fleeing,
    Dying
}

public abstract class BaseEnemy : MonoBehaviour
{
    public EnemyState currentState = EnemyState.Patrolling;
    public float detectionRadius = 5f;
    public LayerMask playerLayer;
    public int maxHealth = 100;
    public int currentHealth;
    public float moveSpeed = 2f;
    public Transform playerTransform;
    public GameManager gameManager; // Changed to private

    protected virtual void Start()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        currentHealth = maxHealth;
        gameManager = FindAnyObjectByType<GameManager>(); // Find GameManager
        if (gameManager == null)
        {
            Debug.LogError("GameManager not found!");
        }
    }

    protected virtual void Update()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        switch (currentState)
        {
            case EnemyState.Patrolling:
                Patrol();
                if (distanceToPlayer < detectionRadius)
                {
                    currentState = EnemyState.Chasing;
                }
                break;
            case EnemyState.Chasing:
                Chase();
                if (distanceToPlayer <= GetAttackRange())
                {
                    currentState = EnemyState.Attacking;
                }
                break;
            case EnemyState.Attacking:
                Attack();
                if (distanceToPlayer > GetAttackRange())
                {
                    currentState = EnemyState.Chasing;
                }
                break;
            case EnemyState.Fleeing:
                Flee();
                if (currentHealth >= maxHealth * 0.5f)
                {
                    currentState = EnemyState.Patrolling; // Or Chasing if player is still close
                }
                break;
            case EnemyState.Dying:
                Die();
                break;
        }
    }

    public virtual void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        if (currentHealth <= 0 && currentState != EnemyState.Dying)
        {
            currentState = EnemyState.Dying;
        }
        else if (currentHealth <= maxHealth * 0.1f && currentState != EnemyState.Fleeing)
        {
            currentState = EnemyState.Fleeing;
        }
    }

    protected abstract void Patrol();
    protected abstract void Chase();
    protected abstract void Attack();
    protected abstract void Flee();
    protected virtual void Die()
    {
        Debug.Log(gameObject.name + " has withered away!");
        gameManager?.EnemyDied(gameObject); // Call GameManager's method
        Destroy(gameObject);
    }

    protected abstract float GetAttackRange();

    public virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}