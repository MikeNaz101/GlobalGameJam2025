using UnityEngine;
using UnityEngine.InputSystem;

public class BasicBulletState : BulletBaseState
{
    public override void EnterState(BulletStateManager bullet)
    {
        Debug.Log("I'm shooting Regular bullets!");
    }

    public override void UpdateState(BulletStateManager bullet)
    {
        // What are we doing in this state
    }
    public int baseDamage = 10;
    public int damageMultiplier = 5;
    public int maxSize = 4;  // Maximum size for the charged shot
    public float chargeRate = 0.5f;  // Scale increase per second.
    public float manaCostPerSecond = 2.5f; // Additional mana cost per second of charging.

    private bool _isCharging = false;
    private float _chargeStartTime;
    private PlayerStateManager _player;
    private EnemyBubble enemy;


    public override void Start()
    {
        base.Start(); // Call the base Start() for lifetime management
        _player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerStateManager>(); //find the player to handle the charging.
        if (_player == null)
        {
            Debug.LogError("Player not found.  Make sure your player has the 'Player' tag.");
        }
    }

    public void StartCharging()
    {
        _isCharging = true;
        _chargeStartTime = Time.time;
        // Optionally, play a charging sound or visual effect here.
    }

    public void StopCharging()
    {
        _isCharging = false;
        // Optionally, stop charging sound or visual effect here.
        Fire();
    }
    public override void Update()
    {
        base.Update(); // Handles movement

        if (_isCharging)
        {
            float chargeTime = Time.time - _chargeStartTime;
            // Calculate scale based on time, clamped to maxSize.
            float newScale = Mathf.Min(1f + chargeTime * chargeRate, maxSize); // start a a scale of one.
            transform.localScale = Vector3.one * newScale;
            // Deduct mana continuously while charging.
            int manaToDeduct = Mathf.FloorToInt(chargeTime * manaCostPerSecond);
            _player.UseMana(manaToDeduct, _player);
            _chargeStartTime = Time.time;

        }
    }


    private void Fire()
    {
        // This is called when the button is released.  The bullet is already instantiated.
        speed = 20f; // Reset speed if it was modified during charging.

    }

    protected override void OnHit(GameObject other)
    {
        // Calculate damage based on size.
        int damage = baseDamage + ((int)(transform.localScale.x) - 1) * damageMultiplier;

        enemy = other.GetComponent<EnemyBubble>();
        if (enemy != null)
        {
            enemy.EnemyTakeDamage(damage); // Deal the damage
        }

        // Splash damage logic (Area of Effect)
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, transform.localScale.x / 2);
        foreach (var hitCollider in hitColliders)
        {
            EnemyBubble splashEnemy = hitCollider.GetComponent<EnemyBubble>();
            if (splashEnemy != null && splashEnemy != enemy)  // Don't damage the primary target twice.
            {
                splashEnemy.EnemyTakeDamage(damage);
            }
        }

        Destroy(gameObject); // Destroy the projectile on hit.
    }
}


