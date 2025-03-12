using UnityEngine;

public class FreezeBulletState : BulletBaseState
{
    public int baseDamage = 5;
    public int damageMultiplier = 5;
    public float freezeDurationMin = 5f;
    public float freezeDurationMax = 10f;
    public float chargeRate = 0.5f;
    public float manaCostPerSecond = 5f;
    public float maxSize = 4;

    public float maxChargeTime = 3f;
    public float maxFreezeDuration = 5f;
    private bool _isCharging = false;
    private float chargeStartTime;
    private PlayerStateManager _player;
    private Transform transform;
    private float speed;

    public override void EnterState(BulletStateManager bullet)
    {
        Debug.Log("I'm shooting Freeze bullets!");
        _player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerStateManager>();
        if (_player == null)
        {
            Debug.LogError("Player not found.");
        }
        transform = bullet.bulletPrefab.transform;
    }

    public override void UpdateState(BulletStateManager bullet)
    {
        if (_isCharging)
        {
            float chargeTime = Time.time - chargeStartTime;
            float newScale = Mathf.Min(1 + chargeTime * chargeRate, maxSize);
            transform.localScale = Vector3.one * newScale;
            int manaToDeduct = Mathf.FloorToInt(chargeTime * manaCostPerSecond);
            _player.UseMana(manaToDeduct, _player);
            chargeStartTime = Time.time;
        }
    }

    public void StartCharging()
    {
        chargeStartTime = Time.time;
        _isCharging = true;
    }

    public float StopCharging()
    {
        float chargeDuration = Time.time - chargeStartTime;
        float chargePercentage = Mathf.Clamp01(chargeDuration / maxChargeTime);
        float freezeDuration = chargePercentage * maxFreezeDuration;
        _isCharging = false;
        return freezeDuration;
    }

    protected void OnHit(GameObject other)
    {
        int damage = baseDamage + ((int)transform.localScale.x - 1) * damageMultiplier;

        EnemyBubble enemy = other.GetComponent<EnemyBubble>();
        if (enemy != null)
        {
            float freezeDuration = Random.Range(freezeDurationMin, freezeDurationMax);
            enemy.Freeze(freezeDuration);
            enemy.EnemyTakeDamage(damage);
        }

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, transform.localScale.x / 2);
        foreach (var hitCollider in hitColliders)
        {
            EnemyBubble splashEnemy = hitCollider.GetComponent<EnemyBubble>();
            if (splashEnemy != null && splashEnemy != enemy)
            {
                float freezeDuration = Random.Range(freezeDurationMin, freezeDurationMax);
                splashEnemy.Freeze(freezeDuration);
                splashEnemy.EnemyTakeDamage(damage);
            }
        }

        Object.Destroy(transform.gameObject);
    }

    public override void ExitState(BulletStateManager bullet)
    {
        _isCharging = false;
    }
}