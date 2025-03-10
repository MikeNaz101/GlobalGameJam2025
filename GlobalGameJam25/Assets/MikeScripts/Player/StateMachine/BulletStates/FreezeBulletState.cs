using Unity.VisualScripting;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.InputSystem;

public class FreezeBulletState : BulletBaseState
{
    public override void EnterState(BulletStateManager bullet)
    {
        Debug.Log("I'm shooting Regular bullets!");
    }

    public override void UpdateState(BulletStateManager bullet)
    {
        // What are we doing in this state
        

    }
    public int baseDamage = 5;
    public int damageMultiplier = 5;
    public float freezeDurationMin = 5f;
    public float freezeDurationMax = 10f;
    public float chargeRate = 0.5f;  // Scale increase per second.
    public float manaCostPerSecond = 5f;
    public float maxSize = 4;

    private bool _isCharging = false;
    private float _chargeStartTime;
    private PlayerStateManager _player;

    public override void Start()
    {
        base.Start(); // Call the base Start() for lifetime management
        _player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerStateManager>(); //find the player to handle the charging.
        if (_player == null)
        {
            Debug.LogError("Player not found.  Make sure your player has the 'Player' tag.");
        }
    }

    protected override void OnHit(GameObject other)
    {
        // Calculate damage based on size.
        int damage = baseDamage + ((int)transform.localScale.x - 1) * damageMultiplier;

        EnemyBubble enemy = other.GetComponent<EnemyBubble>();
        if (enemy != null)
        {
            float freezeDuration = Random.Range(freezeDurationMin, freezeDurationMax);
            enemy.Freeze(freezeDuration);
            enemy.EnemyTakeDamage(damage); //deal the damage that you are doing
        }
        // Splash damage logic (Area of Effect)
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, transform.localScale.x / 2);
        foreach (var hitCollider in hitColliders)
        {
            EnemyBubble splashEnemy = hitCollider.GetComponent<EnemyBubble>();
            if (splashEnemy != null && splashEnemy != enemy)  // Don't damage the primary target twice.
            {
                float freezeDuration = Random.Range(freezeDurationMin, freezeDurationMax);
                splashEnemy.Freeze(freezeDuration);
                splashEnemy.EnemyTakeDamage(damage);
            }
        }


        Destroy(gameObject); // Destroy the projectile.
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
            float newScale = Mathf.Min(1 + chargeTime * chargeRate, maxSize);
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
}