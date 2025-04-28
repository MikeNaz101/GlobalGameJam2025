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

    [Header("Sludge Specific Audio")]
    [Tooltip("Sound played when the Sludge Enemy attacks (melee). Assign in Inspector.")]
    public AudioClip attackSound;

    // ----- NEW EFFECTS -----
    [Header("Sludge Specific Effects")]
    [Tooltip("Particle system instance played during melee attack. Assign the child Particle System GameObject here.")]
    public ParticleSystem attackEffectInstance; // Assign in Inspector
    [Tooltip("Optional: Point where the attack particle effect originates. If null, uses enemy's position/effect's current position.")]
    public Transform attackEffectOrigin; // Optional: Assign in Inspector
    // ----- END NEW EFFECTS -----

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

    protected override void Awake()
    {
        base.Awake();
        // Ensure the attack particle system doesn't play automatically
        if (attackEffectInstance != null && attackEffectInstance.main.playOnAwake)
        {
             Debug.LogWarning($"Particle System '{attackEffectInstance.name}' on SludgeEnemy '{gameObject.name}' has PlayOnAwake enabled. Disabling it.", this);
             var main = attackEffectInstance.main; // Need to modify struct copy
             main.playOnAwake = false;
        }
    }

    protected override void Start()
    {
        base.Start();
        spawnPosition = transform.position;
        SetNewPatrolTarget();
        if (healthBar != null)
        {
            healthBar.SetMaxStats(maxHealth);
            UpdateHealthBar();
        }
    }

    protected override void Update()
    {
        if (!_isFrozen)
        {
            attackTimer += Time.deltaTime;
        }
        StickToGround();
        base.Update();
    }

    // --- State Implementations (Overrides from BaseEnemy) ---

    protected override void Patrol()
    {
        if (!hasTarget || Vector3.Distance(transform.position, currentTargetPosition) < 1.0f)
        {
            SetNewPatrolTarget();
        }
        if (hasTarget)
        {
            MoveTowards(currentTargetPosition);
            LookTowards(currentTargetPosition);
        }
    }

    private void SetNewPatrolTarget()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        Vector3 potentialTarget = spawnPosition + new Vector3(randomDirection.x, 0, randomDirection.z);
        RaycastHit hit;
        if (Physics.Raycast(potentialTarget + Vector3.up * 5f, Vector3.down, out hit, 10f, groundLayer))
        {
            currentTargetPosition = hit.point;
            hasTarget = true;
        }
        else
        {
            currentTargetPosition = spawnPosition;
            hasTarget = true;
        }
    }

    protected override void Chase()
    {
        if (playerTransform == null) { hasTarget = false; return; }
        currentTargetPosition = playerTransform.position;
        hasTarget = true;
        if (Vector3.Distance(transform.position, currentTargetPosition) > GetAttackRange() - stoppingDistanceBuffer)
        {
            MoveTowards(currentTargetPosition);
        }
        LookTowards(currentTargetPosition);
    }

    protected override void Attack()
    {
         if (playerTransform == null) { hasTarget = false; return; }
         currentTargetPosition = playerTransform.position;
         hasTarget = true;
         LookTowards(currentTargetPosition);
        if (attackTimer >= attackCooldown && Vector3.Distance(transform.position, currentTargetPosition) <= GetAttackRange() * 1.1f)
        {
             PerformMeleeAttack();
             attackTimer = 0f;
        }
    }

     // Performs the actual melee attack logic
     void PerformMeleeAttack()
     {
         // --- Play Attack Sound ---
         PlaySound(attackSound, mainAudioSource);

         // --- Play Attack Particle Effect --- <<< ADDED EFFECT TRIGGER
         if (attackEffectInstance != null)
         {
             // Optional: Position the effect at the origin point before playing
             if (attackEffectOrigin != null)
             {
                 attackEffectInstance.transform.position = attackEffectOrigin.position;
                 // Optional: Match rotation too if the effect direction matters
                 // attackEffectInstance.transform.rotation = attackEffectOrigin.rotation;
             }
             // Play the particle system
             attackEffectInstance.Play();
         }
         // ------------------------------------- <<< END EFFECT TRIGGER

         // --- Deal Damage ---
         PlayerStateManager player = playerTransform?.GetComponentInParent<PlayerStateManager>();
         if (player != null)
         {
             player.TakeDamage(attackDamage);
         }
         else
         {
             if(playerTransform == null) { Debug.LogError($"[{gameObject.name}] ERROR: playerTransform reference is NULL!"); }
             else { Debug.LogError($"[{gameObject.name}] ERROR: Could not find PlayerStateManager on '{playerTransform.name}' or parents!", this); }
         }
     }

    protected override void Flee()
    {
        if (playerTransform == null) { hasTarget = false; return; }
        Vector3 fleeDirection = (transform.position - playerTransform.position).normalized;
        Vector3 potentialTarget = transform.position + fleeDirection * (patrolRadius * 0.75f);
        RaycastHit hit;
        if (Physics.Raycast(potentialTarget + Vector3.up * 5f, Vector3.down, out hit, 10f, groundLayer))
        {
            currentTargetPosition = hit.point;
            hasTarget = true;
        }
        else { hasTarget = false; }

        if(hasTarget)
        {
            MoveTowards(currentTargetPosition);
            LookTowards(currentTargetPosition);
        }
    }

    // --- Helper Methods ---

    void MoveTowards(Vector3 target)
    {
        Vector3 direction = (target - transform.position).normalized;
        direction.y = 0;
        transform.position += direction * moveSpeed * Time.deltaTime;
     }

    void LookTowards(Vector3 target)
    {
        Vector3 direction = (target - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            direction.y = 0;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
     }

    void StickToGround()
    {
         RaycastHit hit;
         Vector3 rayStart = transform.position + Vector3.up * 0.5f;
         if (Physics.Raycast(rayStart, Vector3.down, out hit, groundCheckDistance + 0.5f, groundLayer))
         {
             Vector3 targetPosition = hit.point + Vector3.up * groundOffset;
             transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);
         }
     }

    // --- Overrides ---

    protected override float GetAttackRange() { return attackRange; }
    protected override void UpdateHealthBar() { healthBar?.SetHealth(currentHealth); }

} // End of SludgeEnemy class