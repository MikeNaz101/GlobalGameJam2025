// --- SludgeEnemy Script (Using GetComponentInParent) ---
using UnityEngine;

// Requires BaseEnemy script and DamageType enum

public class SludgeEnemy : BaseEnemy
{
    [Header("Sludge Specific Stats")]
    public float patrolRadius = 10f;
    public float attackRange = 1.5f;
    public int attackDamage = 10;
    public float attackCooldown = 1.5f;
    public float stoppingDistanceBuffer = 0.2f;
    public float rotationSpeed = 180f;

    [Header("Grounding")]
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.5f;
    public float groundOffset = 0.1f;

    [Header("References")]
    public HealthBar healthBar;

    private Vector3 spawnPosition;
    private Vector3 currentTargetPosition;
    private float attackTimer = 0f;
    private bool hasTarget = false;

    protected override void Awake() { base.Awake(); }

    protected override void Start()
    {
        base.Start();
        spawnPosition = transform.position;
        SetNewPatrolTarget();
        if (healthBar != null) { healthBar.SetMaxStats(maxHealth); UpdateHealthBar(); }
    }

    protected override void Update()
    {
        if (!_isFrozen) attackTimer += Time.deltaTime;
        StickToGround();
        base.Update();
    }

    // --- State Implementations ---
    // Patrol, SetNewPatrolTarget, Chase remain the same as the previous version
    protected override void Patrol()
    {
        if (!hasTarget || Vector3.Distance(transform.position, currentTargetPosition) < 1.0f) SetNewPatrolTarget();
        if (hasTarget) { MoveTowards(currentTargetPosition); LookTowards(currentTargetPosition); }
    }
    private void SetNewPatrolTarget()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        Vector3 potentialTarget = spawnPosition + new Vector3(randomDirection.x, 0, randomDirection.z);
        RaycastHit hit;
        if (Physics.Raycast(potentialTarget + Vector3.up * 5f, Vector3.down, out hit, 10f, groundLayer)) {
            currentTargetPosition = hit.point; hasTarget = true;
        } else {
             currentTargetPosition = spawnPosition; hasTarget = true;
        }
    }
    protected override void Chase()
    {
        if (playerTransform == null) { hasTarget = false; return; }
        currentTargetPosition = playerTransform.position; hasTarget = true;
        if (Vector3.Distance(transform.position, currentTargetPosition) > GetAttackRange() - stoppingDistanceBuffer) MoveTowards(currentTargetPosition);
        LookTowards(currentTargetPosition);
    }

    // Attack method remains the same (still calls PerformMeleeAttack)
     protected override void Attack()
    {
         if (playerTransform == null) { hasTarget = false; return; }
         currentTargetPosition = playerTransform.position; hasTarget = true;
         LookTowards(currentTargetPosition); // Keep looking

        // --- DEBUG LOGS for Attack State (Optional - Keep or Remove) ---
        /*
        float currentDistance = Vector3.Distance(transform.position, currentTargetPosition);
        bool readyToAttack = attackTimer >= attackCooldown;
        bool withinRange = currentDistance <= GetAttackRange() * 1.1f;
        if (Time.frameCount % 20 == 0) {
             Debug.Log($"<color=#FFBF00>[{gameObject.name}]</color> In Attack State. Timer: {attackTimer:F2}/{attackCooldown:F2} (Ready? {readyToAttack}). Dist: {currentDistance:F2} / Range: {(GetAttackRange() * 1.1f):F2} (InRange? {withinRange}).");
        }
        */
        // --- END DEBUG LOGS ---

        // Check conditions to perform the attack
        if (attackTimer >= attackCooldown && Vector3.Distance(transform.position, currentTargetPosition) <= GetAttackRange() * 1.1f)
        {
             // Log call if keeping debug logs
             // Debug.Log($"<color=lime>[{gameObject.name}]</color> *** Attack conditions met! Calling PerformMeleeAttack... ***");
             PerformMeleeAttack();
             attackTimer = 0f;
        }
    }


     // --- PerformMeleeAttack NOW USES GetComponentInParent ---
     void PerformMeleeAttack()
     {
         // Optional Log: Debug.Log($"<color=green>[{gameObject.name}]</color> Inside PerformMeleeAttack. Trying GetComponentInParent on '{playerTransform?.name ?? "null"}'.");

         // --- CHANGE IS HERE: Use GetComponentInParent ---
         PlayerStateManager player = playerTransform?.GetComponent<PlayerStateManager>();
         // ---------------------------------------------

         if (player != null)
         {
             // Optional Log: Debug.Log($"<color=green>[{gameObject.name}]</color> Found PlayerStateManager via parent on '{player.name}'. Applying {attackDamage} damage.");
             player.TakeDamage(attackDamage);
         }
         else
         {
              // Error if still not found (means script isn't on 'body' or any of its parents)
             if(playerTransform == null) {
                  Debug.LogError($"<color=red>[{gameObject.name}]</color> ERROR in PerformMeleeAttack: playerTransform reference is NULL!");
             } else {
                 Debug.LogError($"<color=red>[{gameObject.name}]</color> ERROR in PerformMeleeAttack: Could not find PlayerStateManager component on '{playerTransform.name}' OR IN ANY OF ITS PARENTS! Check player prefab structure and script location.");
             }
         }
     }

    // Flee method remains the same
     protected override void Flee()
    {
        if (playerTransform == null) { hasTarget = false; return; }
        Vector3 fleeDirection = (transform.position - playerTransform.position).normalized;
        Vector3 potentialTarget = transform.position + fleeDirection * (patrolRadius * 0.75f);
        RaycastHit hit;
        if (Physics.Raycast(potentialTarget + Vector3.up * 5f, Vector3.down, out hit, 10f, groundLayer)) {
            currentTargetPosition = hit.point; hasTarget = true;
        } else {
            hasTarget = false;
        }
        if(hasTarget) { MoveTowards(currentTargetPosition); LookTowards(currentTargetPosition); }
    }

    // --- Helper Methods ---
    void MoveTowards(Vector3 target) { /* ... same as before ... */
        Vector3 direction = (target - transform.position).normalized;
        direction.y = 0;
        transform.position += direction * moveSpeed * Time.deltaTime;
     }
    void LookTowards(Vector3 target) { /* ... same as before ... */
        Vector3 direction = (target - transform.position).normalized;
        if (direction != Vector3.zero) {
            direction.y = 0;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
     }
    void StickToGround() { /* ... same as before ... */
         RaycastHit hit;
         Vector3 rayStart = transform.position + Vector3.up * 0.5f;
         if (Physics.Raycast(rayStart, Vector3.down, out hit, groundCheckDistance + 0.5f, groundLayer)) {
             Vector3 targetPosition = hit.point + Vector3.up * groundOffset;
             transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);
         }
     }

    // --- Overrides ---
    protected override float GetAttackRange() { return attackRange; }
    protected override void UpdateHealthBar() { healthBar?.SetHealth(currentHealth); }
    protected override void Die() { base.Die(); }

} // End of SludgeEnemy class