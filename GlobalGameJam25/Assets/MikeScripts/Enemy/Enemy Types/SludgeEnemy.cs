// --- SludgeEnemy Script ---
using UnityEngine;
using UnityEngine.AI; // Include if using NavMeshAgent

// Requires BaseEnemy script and DamageType enum

[RequireComponent(typeof(NavMeshAgent))] // Good practice if Sludge MUST use NavMesh
public class SludgeEnemy : BaseEnemy
{
    [Header("Sludge Specific Stats")]
    public float patrolRadius = 10f;
    public float attackRange = 1.5f; // Close range melee
    public int attackDamage = 10;
    public float attackCooldown = 1.5f; // Slightly longer cooldown for melee

    [Header("References")]
    public HealthBar healthBar; // Assign in the Inspector (Optional)

    // Internal state
    private Vector3 spawnPosition;
    private Vector3 patrolTarget;
    private float attackTimer = 0f;
    private NavMeshAgent agent; // Cache NavMeshAgent

    protected override void Awake()
    {
        base.Awake(); // Call base Awake first
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
             Debug.LogError($"SludgeEnemy {gameObject.name} requires a NavMeshAgent component!");
             this.enabled = false; // Disable if no agent
             return;
        }
        // Configure agent defaults here if needed
        agent.speed = moveSpeed;
        agent.stoppingDistance = attackRange * 0.8f; // Stop slightly before exact attack range
    }

    protected override void Start()
    {
        base.Start(); // Call base Start
        spawnPosition = transform.position;
        // Ensure the enemy starts on the NavMesh
        if (agent.isOnNavMesh)
        {
            SetNewPatrolTarget();
        }
        else
        {
             Debug.LogError($"SludgeEnemy {gameObject.name} is not placed on a NavMesh!");
             // Try to warp to nearest valid NavMesh point?
             NavMeshHit hit;
             if (NavMesh.SamplePosition(transform.position, out hit, 2.0f, NavMesh.AllAreas)) {
                 agent.Warp(hit.position);
                 spawnPosition = hit.position; // Update spawn pos
                 SetNewPatrolTarget();
             } else {
                 Debug.LogError($"Could not find valid NavMesh position near {transform.position} for SludgeEnemy!");
                 this.enabled = false; // Disable if can't find valid spot
             }
        }

        // Initialize Health Bar
        if (healthBar != null)
        {
            healthBar.SetMaxStats(maxHealth);
            UpdateHealthBar();
        }
    }

    protected override void Update()
    {
        // --- Update Agent Speed (In case moveSpeed changes) ---
         if (agent != null && agent.speed != moveSpeed) {
             agent.speed = moveSpeed;
         }

        base.Update(); // Handles freeze check and calls HandleStateMachine if not frozen

        // Update attack timer ONLY if not frozen
        if (!_isFrozen)
        {
            attackTimer += Time.deltaTime;
        }
    }

    // --- State Implementations ---

    protected override void Patrol()
    {
        // Use NavMeshAgent to move towards patrol target
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            SetNewPatrolTarget();
        }
    }

    private void SetNewPatrolTarget()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += spawnPosition; // Use spawnPosition as origin

        NavMeshHit hit;
        // Find a random point on the NavMesh within the patrolRadius from spawnPosition
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius * 1.5f, NavMesh.AllAreas)) // Increase sample radius slightly
        {
            patrolTarget = hit.position;
            agent.SetDestination(patrolTarget);
        }
        else
        {
             // Could not find point, maybe try again next frame or log warning
             // For now, just stay put or go back to spawn
             agent.SetDestination(spawnPosition);
             Debug.LogWarning($"Could not find NavMesh point near {randomDirection} for {gameObject.name}");
        }
    }

    protected override void Chase()
    {
        if (playerTransform == null || !agent.enabled || !agent.isOnNavMesh) return;
        // Set destination to player's position
        agent.SetDestination(playerTransform.position);
        // Agent handles rotation automatically (updateRotation = true) unless specified otherwise
    }

    protected override void Attack()
    {
         if (playerTransform == null || !agent.enabled || !agent.isOnNavMesh) return;

        // Stop moving when in attack state (usually handled by agent.stoppingDistance)
         // agent.ResetPath(); // Or agent.isStopped = true; might be better if you want it to resume chase easily

        // Ensure looking at player
        LookAtPlayer();

        // Melee attack logic
        if (attackTimer >= attackCooldown)
        {
            // Check distance again just to be sure (NavMesh pathing isn't instant)
            if (Vector3.Distance(transform.position, playerTransform.position) <= attackRange * 1.1f) // Slight tolerance
            {
                PerformMeleeAttack();
                attackTimer = 0f; // Reset timer AFTER attacking
            }
        }
    }

     void PerformMeleeAttack()
     {
         Debug.Log($"{gameObject.name} performs melee attack!");
         // Optional: Play attack animation
         // animator.SetTrigger("Attack");

         // Apply damage to player
         PlayerStateManager player = playerTransform.GetComponent<PlayerStateManager>();
         if (player != null)
         {
             // IMPORTANT: Call TakeDamage with DamageType.Basic (or .Other if sludge isn't 'basic')
             player.TakeDamage(attackDamage/*, DamageType.Basic*/); // Assuming PlayerStateManager.TakeDamage doesn't need type
         }
     }

    protected override void Flee()
    {
         if (playerTransform == null || !agent.enabled || !agent.isOnNavMesh) return;

        // Find a point away from the player on the NavMesh
        Vector3 fleeDirection = (transform.position - playerTransform.position).normalized;
        Vector3 targetPos = transform.position + fleeDirection * (patrolRadius * 0.5f); // Flee a certain distance

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, patrolRadius * 0.5f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            // Can't find flee point, maybe just stop or patrol?
             agent.SetDestination(spawnPosition); // Go back to spawn as fallback
        }
    }

    // --- Helper Methods ---
    void LookAtPlayer()
    {
        if (playerTransform == null) return;
        Vector3 direction = (playerTransform.position - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
             // Agent might handle rotation, but manual can be smoother or override
             transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * agent.angularSpeed * 0.1f); // Sync roughly with agent rotation speed
        }
    }

    // --- Overrides ---

     public override void Freeze(float baseDuration = 4f) // Sludge might freeze for slightly less?
     {
         if (_isFrozen || currentState == EnemyState.Dying) return;

         // Stop NavMesh Agent BEFORE calling base.Freeze if base.Freeze might disable it
         if (agent != null && agent.enabled)
         {
             agent.isStopped = true;
             // agent.velocity = Vector3.zero; // Ensure it stops immediately
         }
         base.Freeze(baseDuration); // Call base freeze logic
     }

     protected override void Unfreeze()
     {
         base.Unfreeze(); // Call base unfreeze logic
         // Resume NavMesh Agent AFTER calling base.Unfreeze
         if (agent != null && agent.enabled)
         {
             agent.isStopped = false;
             // Re-evaluate destination based on current state
             if (currentState == EnemyState.Chasing && playerTransform != null) {
                 agent.SetDestination(playerTransform.position);
             } else if (currentState == EnemyState.Patrolling) {
                 // Might need to reset patrol target or let it continue
                 if (!agent.hasPath || agent.remainingDistance < 0.5f) SetNewPatrolTarget();
                 else agent.SetDestination(patrolTarget); // Resume patrolling
             }
             // Add other states if needed (Fleeing)
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
        // Add specific sludge death effects (e.g., puddle dissolve)
        // Instantiate(sludgeDissolveEffect, transform.position, Quaternion.identity);

        // Disable NavMeshAgent BEFORE destroying
        if (agent != null) { agent.enabled = false; }

        base.Die(); // Call base Die for destruction and GameManager notification
    }

    // Override Gizmos to draw attack range
    private new void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected(); // Draw base gizmos (detection radius)

        // Draw Patrol Radius
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spawnPosition, patrolRadius);

        // Draw Attack Radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}