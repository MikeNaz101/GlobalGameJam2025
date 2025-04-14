using UnityEngine;
using UnityEngine.AI; // If you want to use NavMesh for movement

public class SludgeEnemy : BaseEnemy
{
    public float patrolRadius = 10f;
    private Vector3 spawnPosition;
    private Vector3 patrolTarget;
    public float attackRange = 1.5f;
    public int attackDamage = 10;
    private float attackCooldown = 1f;
    private float attackTimer = 0f;

    protected override void Start()
    {
        base.Start();
        spawnPosition = transform.position;
        SetNewPatrolTarget();
    }

    protected override void Update()
    {
        base.Update();
        attackTimer += Time.deltaTime;
    }

    protected override void Patrol()
    {
        // Simple random movement within the patrol radius
        if (Vector3.Distance(transform.position, patrolTarget) < 0.5f)
        {
            SetNewPatrolTarget();
        }
        Vector3 direction = (patrolTarget - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;
        // Optional: Add rotation towards the movement direction
        transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
    }

    private void SetNewPatrolTarget()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += spawnPosition;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, NavMesh.AllAreas))
        {
            patrolTarget = hit.position;
        }
        else
        {
            patrolTarget = randomDirection; // Fallback if NavMesh isn't hit
        }
    }

    protected override void Chase()
    {
        Vector3 direction = (playerTransform.position - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
    }

    protected override void Attack()
    {
        if (attackTimer >= attackCooldown)
        {
            Debug.Log("Sludge attack!");
            // Implement your physical attack logic here (e.g., trigger an animation, apply damage to the player)
            if (Vector3.Distance(transform.position, playerTransform.position) <= attackRange)
            {
                PlayerStateManager player = playerTransform.GetComponent<PlayerStateManager>();
                if (player != null)
                {
                    player.TakeDamage(attackDamage);
                }
            }
            attackTimer = 0f;
        }
        // Optionally, you could have the sludge try to maintain its position within attack range
    }

    protected override void Flee()
    {
        Vector3 fleeDirection = (transform.position - playerTransform.position).normalized;
        transform.position += fleeDirection * moveSpeed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(new Vector3(fleeDirection.x, 0, fleeDirection.z));
    }

    protected override float GetAttackRange()
    {
        return attackRange;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") && currentState == EnemyState.Attacking && attackTimer >= attackCooldown)
        {
            PlayerStateManager player = collision.gameObject.GetComponent<PlayerStateManager>();
            if (player != null)
            {
                player.TakeDamage(attackDamage);
                attackTimer = 0f;
            }
        }
    }

    private new void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spawnPosition, patrolRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}