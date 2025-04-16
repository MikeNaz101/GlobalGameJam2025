using UnityEngine;

[RequireComponent(typeof(EnemyBubble))]
public class GasEnemy : BaseEnemy
{
    [Header("Gas Specific Stats")]
    public float patrolRadius = 15f;
    public float attackRange = 10f;
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float projectileSpeed = 10f;
    public float fireCooldown = 2f;
    [Range(0.1f, 1f)]
    public float basicDamageReductionFactor = 0.5f;

    [Header("References")]
    public HealthBar healthBar;

    private Vector3 spawnPosition;
    private Vector3 patrolTarget;
    private float fireTimer = 0f;
    private EnemyBubble enemyBubble;

    protected override void Awake() { base.Awake(); enemyBubble = GetComponent<EnemyBubble>(); }
    protected override void Start() {
        base.Start(); spawnPosition = transform.position; SetNewPatrolTarget();
        if (healthBar != null) { healthBar.SetMaxStats(maxHealth); UpdateHealthBar(); }
        if (firePoint == null) Debug.LogError($"Fire Point not assigned on {gameObject.name}!");
        if (projectilePrefab == null) Debug.LogError($"Projectile Prefab not assigned on {gameObject.name}!");
    }
    protected override void Update() {
        base.Update();
        if (!_isFrozen && playerTransform != null) { fireTimer += Time.deltaTime; Wobble(); }
    }
    void Wobble() { transform.position = new Vector3(transform.position.x, spawnPosition.y + Mathf.Sin(Time.time * 1.5f) * 0.3f, transform.position.z); }
    protected override void Patrol() {
        if (Vector3.Distance(transform.position, patrolTarget) < 1.0f) SetNewPatrolTarget();
        MoveTowards(patrolTarget);
        Vector3 moveDir = (patrolTarget - transform.position).normalized; if (moveDir != Vector3.zero) LookInDirection(moveDir);
    }
    private void SetNewPatrolTarget() { Vector3 randomDirection = Random.insideUnitSphere * patrolRadius; patrolTarget = spawnPosition + new Vector3(randomDirection.x, 0, randomDirection.z); }
    protected override void Chase() { if (playerTransform == null) return; MoveTowards(playerTransform.position); LookAtPlayer(); }
    protected override void Attack() {
        if (playerTransform == null) return; LookAtPlayer();
        if (fireTimer >= fireCooldown && projectilePrefab != null && firePoint != null) { FireProjectile(); fireTimer = 0f; }
    }
    protected override void Flee() { if (playerTransform == null) return; Vector3 fleeDirection = (transform.position - playerTransform.position).normalized; Vector3 fleeTarget = transform.position + fleeDirection * 5f; MoveTowards(fleeTarget); if (fleeDirection != Vector3.zero) LookInDirection(fleeDirection); }
    void MoveTowards(Vector3 target) { Vector3 direction = (target - transform.position).normalized; transform.position += direction * moveSpeed * Time.deltaTime; }
    void LookAtPlayer() { if (playerTransform == null) return; Vector3 direction = playerTransform.position - transform.position; LookInDirection(direction); }
    void LookInDirection(Vector3 direction) { if (direction == Vector3.zero) return; Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z)); transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f); }
    void FireProjectile() { Vector3 directionToPlayer = (playerTransform.position - firePoint.position).normalized; firePoint.rotation = Quaternion.LookRotation(directionToPlayer); GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation); Rigidbody projectileRb = projectile.GetComponent<Rigidbody>(); if (projectileRb != null) projectileRb.linearVelocity = firePoint.forward * projectileSpeed; EnemyProjectile projectileScript = projectile.GetComponent<EnemyProjectile>(); if (projectileScript != null) projectileScript.damageAmount = enemyBubble?.size ?? 1; }
    public override void TakeDamage(int damage, DamageType type = DamageType.Other) { if (currentState == EnemyState.Dying) return; int damageToTake = damage; if (type == DamageType.Basic && !_isFrozen) damageToTake = Mathf.CeilToInt(damage * basicDamageReductionFactor); base.TakeDamage(damageToTake, type); }
    public override void Freeze(float baseDuration = 5f) { if (_isFrozen || currentState == EnemyState.Dying) return; base.Freeze(baseDuration); }
    protected override void Unfreeze() { bool wasFrozen = _isFrozen; base.Unfreeze(); }
    protected override float GetAttackRange() { return attackRange; }
    protected override void UpdateHealthBar() { healthBar?.SetHealth(currentHealth); }
    protected override void Die() { base.Die(); } // Base Die handles XP orb spawning
}