using UnityEngine;

// Remember to assign the Hit Effect Prefab in the Inspector, darling!
public class BasicBullet : MonoBehaviour
{
    [Header("Damage & Scaling")]
    public int baseDamage = 10;
    public int damageMultiplier = 5; // Extra damage per unit of scale increase
    public float maxSize = 4f;       // Max scale multiplier

    [Header("Charging")]
    public float manaCostPerSecond = 2.5f; // Mana drained while charging
    public float maxChargeTime = 2f;       // Time to reach max charge/size
    public float maxChargeMultiplier = 2f; // Max force multiplier returned by StopCharging

    [Header("Effects")] // Our little stage for visual flair!
    public GameObject hitEffectPrefab; // ✨ Drag your particle system PREFAB here ✨

    // --- Private Variables ---
    private bool _isCharging = false;
    private bool _wasStarted = false; // To prevent issues if StopCharging is called without Start
    private float chargeStartTime;
    private PlayerStateManager _player; // Reference set by PlayerShooting
    private Transform bulletTransform; // Cache the transform

    void Awake() // Use Awake for component references
    {
        bulletTransform = transform;
    }

    // Called by PlayerShooting when charging begins
    public void StartCharging(PlayerStateManager playerRef)
    {
        if (playerRef == null) {
            Debug.LogError("BasicBullet received null PlayerStateManager reference! Destroying bullet.");
            Destroy(gameObject);
            return;
        }
        _player = playerRef;
        chargeStartTime = Time.time;
        _isCharging = true;
        _wasStarted = true;
        Debug.Log("BasicBullet charging started.");
        // Ensure bullet starts at base scale
        bulletTransform.localScale = Vector3.one;
    }

    // Called by PlayerShooting when charging ends (mouse release)
    // Returns a multiplier for the firing force based on charge time
    public float StopCharging()
    {
        if (!_wasStarted) return 1f; // Return default multiplier if never started

        _isCharging = false; // Stop internal charging state
        Debug.Log("BasicBullet charging stopped.");

        // Calculate final multiplier based on duration
        float chargeDuration = Mathf.Clamp(Time.time - chargeStartTime, 0, maxChargeTime);
        float chargePercentage = (maxChargeTime > 0) ? (chargeDuration / maxChargeTime) : 1.0f; // Ratio 0 to 1, avoid div by zero
        float chargeMultiplier = Mathf.Lerp(1f, maxChargeMultiplier, chargePercentage); // Lerp multiplier

        return chargeMultiplier;
    }

    private void Update()
    {
        // If charging, increase the bullet size and deduct mana over time
        if (_isCharging)
        {
            float chargeTime = Time.time - chargeStartTime;

            // --- Mana Drain ---
            float manaToAttemptDeduct = manaCostPerSecond * Time.deltaTime;
            int manaCostThisFrame = Mathf.CeilToInt(manaToAttemptDeduct);

            // Check mana and attempt deduction *before* scaling
            if (manaCostThisFrame > 0 && (_player == null || !_player.UseMana(manaCostThisFrame, _player)))
            {
                _isCharging = false;
                Debug.Log("Ran out of mana while charging BasicBullet or player ref missing!");
            }
            else if (_player != null) // Only scale if mana was okay
            {
                // --- Scaling ---
                float effectiveChargeTime = Mathf.Min(chargeTime, maxChargeTime);
                float chargePercentage = (maxChargeTime > 0) ? (effectiveChargeTime / maxChargeTime) : 1.0f; // Avoid div by zero
                float newScale = Mathf.Lerp(1f, maxSize, chargePercentage); // Lerp scale from 1 to maxSize

                bulletTransform.localScale = Vector3.one * newScale;
            }
             else { // Player ref somehow became null mid-charge
                 _isCharging = false;
             }
        }
    }

    // Using OnCollisionEnter for physics interactions
    void OnCollisionEnter(Collision collision)
    {
        // Prevent collision with player right after firing
        if (collision.gameObject.CompareTag("Player"))
        {
            return;
        }

        // --- Get Hit Info and Call OnHit ---
        if (collision.contacts.Length > 0)
        {
            ContactPoint contact = collision.contacts[0];
            Vector3 contactPoint = contact.point;   // Exact world position of impact
            Vector3 contactNormal = contact.normal; // Surface direction at impact

            // ✨ Pass the delicious details to OnHit ✨
            OnHit(collision.gameObject, contactPoint, contactNormal);
        }
        else
        {
            // Fallback if no contact points (rare)
            OnHit(collision.gameObject, bulletTransform.position, -bulletTransform.forward);
        }
    }

    // Central logic for handling bullet impact
    // ✨ Updated signature to accept hitPoint and hitNormal ✨
    private void OnHit(GameObject hitObject, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Calculate final damage based on final scale
        float currentScale = Mathf.Max(1f, bulletTransform.localScale.x);
        int damage = baseDamage + Mathf.FloorToInt((currentScale - 1f) * damageMultiplier);

        Debug.Log($"BasicBullet hit {hitObject.name}, Scale: {currentScale:F2}, Damage: {damage} at point {hitPoint}");

        // --- ✨ Instantiate and Scale the Hit Effect! ✨ ---
        if (hitEffectPrefab != null)
        {
            // Create the particle effect prefab instance at the impact point
            GameObject effectInstance = Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitNormal));

            // ✨ Scale the particle effect instance based on the bullet's final scale ✨
            effectInstance.transform.localScale = Vector3.one * currentScale;

            // Best practice: Particle system prefab should destroy itself via Stop Action.
            // Add fallback destruction logic if needed (see FreezeBullet example).
        }
        else
        {
            Debug.LogWarning("BasicBullet 'Hit Effect Prefab' is not assigned!", this);
        }


        // --- Apply Damage to Target ---
        EnemyBubble enemy = hitObject.GetComponent<EnemyBubble>();
        if (enemy != null)
        {
            enemy.EnemyTakeDamage(damage);
        }

        // --- Handle Splash Damage ---
        float splashRadius = currentScale * 0.5f; // Example: Splash radius scales too
        Collider[] hitColliders = Physics.OverlapSphere(bulletTransform.position, splashRadius);

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.gameObject == hitObject || hitCollider.CompareTag("Player")) continue;

            EnemyBubble splashEnemy = hitCollider.GetComponent<EnemyBubble>();
            if (splashEnemy != null)
            {
                int splashDamage = damage; // Maybe reduce splash? damage / 2;
                Debug.Log($"Applying splash damage {splashDamage} to {splashEnemy.name}");
                splashEnemy.EnemyTakeDamage(splashDamage);
            }
        }

        // --- Destroy Bullet ---
        // Happens last, after effects are spawned and damage dealt
        Destroy(gameObject);
    }
}