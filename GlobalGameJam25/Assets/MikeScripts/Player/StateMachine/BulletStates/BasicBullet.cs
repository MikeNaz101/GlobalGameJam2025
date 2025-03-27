using UnityEngine;

public class BasicBullet : MonoBehaviour
{
    public int baseDamage = 10;
    public int damageMultiplier = 5;
    public int maxSize = 4;
    public float chargeRate = 0.5f;
    public float manaCostPerSecond = 2.5f;

    private bool _isCharging = false;
    private float chargeStartTime;
    public float maxChargeTime = 2f;
    public float maxChargeMultiplier = 2f;
    private PlayerStateManager _player;
    private EnemyBubble enemy;
    private Transform transform;
    private float speed;

    public override void EnterState(BulletStateManager bullet)
    {
        Debug.Log("I'm shooting Regular bullets!");
        _player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerStateManager>();
        if (_player == null)
        {
            Debug.LogError("Player not found.");
        }
        transform = bullet.bulletPrefab.transform; //added transform.
    }

    public override void UpdateState(BulletStateManager bullet)
    {
        if (_isCharging)
        {
            float chargeTime = Time.time - chargeStartTime;
            float newScale = Mathf.Min(1f + chargeTime * chargeRate, maxSize);
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
        float chargeMultiplier = 1f + (chargePercentage * (maxChargeMultiplier - 1f));
        _isCharging = false;
        return chargeMultiplier;
    }

    private void Fire()
    {
        speed = 20f;
    }

    protected void OnHit(GameObject other)
    {
        int damage = baseDamage + ((int)(transform.localScale.x) - 1) * damageMultiplier;

        enemy = other.GetComponent<EnemyBubble>();
        if (enemy != null)
        {
            enemy.EnemyTakeDamage(damage);
        }

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, transform.localScale.x / 2);
        foreach (var hitCollider in hitColliders)
        {
            EnemyBubble splashEnemy = hitCollider.GetComponent<EnemyBubble>();
            if (splashEnemy != null && splashEnemy != enemy)
            {
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