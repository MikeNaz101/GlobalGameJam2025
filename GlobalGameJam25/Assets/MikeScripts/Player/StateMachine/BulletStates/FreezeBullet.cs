using UnityEngine;

// Make sure you have assigned the Hit Effect Prefab in the Inspector!
public class FreezeBullet : MonoBehaviour
{
    [Header("Damage & Scaling")]
    public int baseDamage = 5;
    public int damageMultiplier = 3;
    public float maxSize = 3f; // Maximum scale the bullet can reach

    [Header("Freeze Effect")]
    public float freezeDurationMin = 1f; // Freeze duration at minimum size (scale 1)
    public float freezeDurationMax = 5f; // Freeze duration at maximum size

    [Header("Charging")]
    public float manaCostPerSecond = 5f;
    public float maxChargeTime = 3f; // Time it takes to reach maxSize

    [Header("Effects")] // Our fabulous effects section!
    public GameObject hitEffectPrefab; // ✨ Drag your particle system PREFAB here ✨

    // --- Private Variables ---
    private bool _isCharging = false;
    private bool _wasStarted = false; // To prevent StopCharging() before StartCharging()
    private float chargeStartTime;
    private PlayerStateManager _player; // Reference to the player script for mana checks
    private Transform bulletTransform; // Cache the transform for performance

    void Awake()
    {
        // Cache the transform component on Awake
        bulletTransform = transform;
    }

    // Called by an external script (like PlayerShooting) when charging begins
    public void StartCharging(PlayerStateManager playerRef)
    {
        if (playerRef == null) {
            Debug.LogError("FreezeBullet received null PlayerStateManager reference! Destroying bullet.");
            Destroy(gameObject); // Can't charge without a player reference
            return;
        }
        _player = playerRef;
        chargeStartTime = Time.time;
        _isCharging = true;
        _wasStarted = true; // Mark that charging has officially begun
        Debug.Log("FreezeBullet charging started.");
        // Ensure bullet starts at its base scale (important if reusing pooled objects)
        bulletTransform.localScale = Vector3.one;
    }

    // Called by an external script (like PlayerShooting) when charging ends (e.g., mouse release)
    public void StopCharging()
    {
        if (!_wasStarted) return; // Don't do anything if charging never started
        _isCharging = false; // Stop the charging state and mana drain/scaling
        Debug.Log("FreezeBullet charging stopped. Final scale will be used on hit.");
        // The final scale is determined by the last Update frame while _isCharging was true
    }

    // Update is called once per frame
    private void Update()
    {
        // Only perform charging logic if currently charging
        if (_isCharging)
        {
            float chargeTime = Time.time - chargeStartTime;

            // --- Mana Drain ---
            float manaToAttemptDeduct = manaCostPerSecond * Time.deltaTime;
            // Use CeilToInt to ensure even small fractions cost at least 1 mana if > 0
            int manaCostThisFrame = Mathf.CeilToInt(manaToAttemptDeduct);

            // Check if player reference exists AND if mana can be used
            if (manaCostThisFrame > 0 && (_player == null || !_player.UseMana(manaCostThisFrame)))
            {
                // If no player reference or out of mana, stop charging
                _isCharging = false;
                Debug.Log("Stopped charging FreezeBullet due to missing player reference or insufficient mana!");
            }
            else if (_player != null) // Only scale if mana was successfully deducted (or cost was 0)
            {
                // --- Scaling Logic ---
                // Clamp charge time to maxChargeTime
                float effectiveChargeTime = Mathf.Min(chargeTime, maxChargeTime);
                // Calculate charge progress (0.0 to 1.0)
                float chargePercentage = (maxChargeTime > 0) ? (effectiveChargeTime / maxChargeTime) : 1.0f; // Avoid division by zero
                // Lerp scale between 1 (base size) and maxSize based on charge percentage
                float newScale = Mathf.Lerp(1f, maxSize, chargePercentage);

                // Apply the new scale uniformly
                bulletTransform.localScale = Vector3.one * newScale;
            }
             else // Safety check: if player became null mid-charge somehow
             {
                 _isCharging = false;
             }
        }
    }

    // Called when this collider/rigidbody has begun touching another rigidbody/collider
    void OnCollisionEnter(Collision collision)
    {
        // Ignore collisions with the player who fired it
        if (collision.gameObject.CompareTag("Player")) // Make sure your player GameObject has the "Player" tag!
        {
            return;
        }

        // We need the exact point of contact to spawn the effect there.
        // collision.contacts contains information about all contact points.
        // We usually just need the first one for a simple impact.
        if (collision.contacts.Length > 0)
        {
            ContactPoint contact = collision.contacts[0];
            Vector3 contactPoint = contact.point;   // The world position of the impact
            Vector3 contactNormal = contact.normal; // The direction the surface is facing at the impact point

            // Call our central hit processing logic, passing necessary info
            OnHit(collision.gameObject, contactPoint, contactNormal);
        }
        else
        {
            // Fallback if no contact points are available (should be rare with non-trigger collisions)
            // Use the bullet's current position as an approximation
            OnHit(collision.gameObject, bulletTransform.position, -bulletTransform.forward); // Hit at bullet center, normal opposite to bullet travel
        }
    }

    // Central logic for handling what happens when the bullet hits *anything* (except the player)
    // Now accepts the precise hit location and surface normal
    private void OnHit(GameObject hitObject, Vector3 hitPoint, Vector3 hitNormal)
    {
        // --- Calculate Final Damage & Freeze Duration based on final scale ---
        // Ensure scale is at least 1, even if something weird happened
        float currentScale = Mathf.Max(1f, bulletTransform.localScale.x);
        // Calculate damage: base + bonus based on how much bigger than 1 it got
        int damage = baseDamage + Mathf.FloorToInt((currentScale - 1f) * damageMultiplier);

        // Calculate the charge ratio based on scale (0 = min size, 1 = max size)
        // Avoid division by zero if maxSize is set to 1 or less
        float scaleChargeRatio = (maxSize > 1f) ? Mathf.Clamp01((currentScale - 1f) / (maxSize - 1f)) : 0f;
        // Interpolate freeze duration based on the charge ratio
        float freezeDuration = Mathf.Lerp(freezeDurationMin, freezeDurationMax, scaleChargeRatio);

        Debug.Log($"FreezeBullet hit {hitObject.name}, Scale: {currentScale:F2}, Damage: {damage}, Freeze Duration: {freezeDuration:F1}s at point {hitPoint}");

        // --- ✨ Instantiate and Scale the Hit Effect! ✨ ---
        if (hitEffectPrefab != null)
        {
            // Create the particle effect prefab instance at the impact point
            // Quaternion.LookRotation(hitNormal) makes the effect face away from the surface it hit
            GameObject effectInstance = Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitNormal));

            // ✨ Scale the particle effect instance based on the bullet's final scale ✨
            effectInstance.transform.localScale = Vector3.one * currentScale;

            // Ensure the particle system destroys itself after playing.
            // Best practice: Configure this on the prefab itself (Main Module -> Stop Action -> Destroy)
            // Fallback: Add a script to the prefab, or call Destroy here with a delay:
            // ParticleSystem ps = effectInstance.GetComponent<ParticleSystem>();
            // if (ps != null) { Destroy(effectInstance, ps.main.duration + ps.main.startLifetime.constantMax); }
            // else { Destroy(effectInstance, 5f); } // Destroy after 5s if no particle system found
        }
        else
        {
            // Warn if the prefab wasn't assigned in the inspector
            Debug.LogWarning("FreezeBullet 'Hit Effect Prefab' is not assigned!", this);
        }

        // --- Apply Direct Hit Effects (Damage & Freeze) ---
        // Try to get the EnemyBubble component from the object directly hit
        EnemyBubble enemy = hitObject.GetComponent<EnemyBubble>();
        if (enemy != null)
        {
            enemy.EnemyTakeDamage(damage);
            enemy.Freeze(freezeDuration); // Assuming the EnemyBubble script has a Freeze method
        }

        // --- Handle Splash Damage & Freeze in an Area ---
        // Calculate splash radius based on the bullet's final scale
        float splashRadius = currentScale * 0.6f; // Adjust the multiplier (0.6f) as needed
        // Find all colliders within the splash radius around the bullet's final position
        // Note: Splash originates from bullet center, not necessarily the exact impact point.
        Collider[] hitColliders = Physics.OverlapSphere(bulletTransform.position, splashRadius);

        foreach (var hitCollider in hitColliders)
        {
            // Skip the object that was directly hit (already processed) and the player
            if (hitCollider.gameObject == hitObject || hitCollider.CompareTag("Player")) continue;

            // Try to get EnemyBubble component from objects within splash radius
            EnemyBubble splashEnemy = hitCollider.GetComponent<EnemyBubble>();
            if (splashEnemy != null)
            {
                // Apply splash effects (currently same as direct hit, could be reduced)
                int splashDamage = damage;             // Example: Maybe reduce splash damage? damage / 2;
                float splashFreezeDuration = freezeDuration; // Example: Maybe reduce splash freeze? freezeDuration * 0.75f;

                Debug.Log($"Applying splash freeze ({splashFreezeDuration:F1}s) and damage ({splashDamage}) to {splashEnemy.name}");
                splashEnemy.EnemyTakeDamage(splashDamage);
                splashEnemy.Freeze(splashFreezeDuration);
            }
        }

        // --- Destroy the Bullet GameObject ---
        // This happens last, after all effects and logic have been processed.
        Destroy(gameObject);
    }
}