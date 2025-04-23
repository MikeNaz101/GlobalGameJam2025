using UnityEngine;

// Make sure you have assigned the Hit Effect Prefab in the Inspector!
public class FreezeBullet : MonoBehaviour
{
    [Header("Damage & Scaling")]
    public int baseDamage = 5;
    public int damageMultiplier = 3; // Damage added per unit of scale increase
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
    private PlayerStateManager _player; // Reference to the player script for mana checks AND LEVEL MULTIPLIER
    private Transform bulletTransform; // Cache the transform for performance

    void Awake()
    {
        // Cache the transform component on Awake
        bulletTransform = transform;
    }

    // Called by an external script (like PlayerShooting) when charging begins
    public void StartCharging(PlayerStateManager playerRef)
    {
        if (playerRef == null)
        {
            Debug.LogError("FreezeBullet received null PlayerStateManager reference! Destroying bullet.");
            Destroy(gameObject); // Can't charge without a player reference
            return;
        }
        _player = playerRef; // Store player reference
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
            OnHit(collision.gameObject, bulletTransform.position, -bulletTransform.forward); // Hit at bullet center, normal opposite to bullet travel
        }
    }

    // Central logic for handling what happens when the bullet hits *anything* (except the player)
    private void OnHit(GameObject hitObject, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Ensure scale is at least 1
        float currentScale = Mathf.Max(1f, bulletTransform.localScale.x);

        // --- Calculate Final Damage & Freeze Duration based on final scale ---
        // Damage
        int calculatedDamage = baseDamage + Mathf.FloorToInt((currentScale - 1f) * damageMultiplier);

        // Duration
        float scaleChargeRatio = (maxSize > 1f) ? Mathf.Clamp01((currentScale - 1f) / (maxSize - 1f)) : 0f;
        float calculatedDuration = Mathf.Lerp(freezeDurationMin, freezeDurationMax, scaleChargeRatio);

        // ----- APPLY PLAYER LEVEL MULTIPLIER -----
        float playerMultiplier = 1.0f; // Default
        if (_player != null)
        {
            playerMultiplier = _player.bulletEffectMultiplier;
        }
        else { Debug.LogWarning("FreezeBullet: Player reference lost before hit!", this); }

        int finalDamage = Mathf.CeilToInt(calculatedDamage * playerMultiplier);
        float finalDuration = calculatedDuration * playerMultiplier; // Apply to duration too
        // ----- END APPLY MULTIPLIER -----


        Debug.Log($"FreezeBullet hit {hitObject.name}, Scale: {currentScale:F2}, BaseDmg: {calculatedDamage}, FinalDmg (x{playerMultiplier:F2}): {finalDamage}, BaseDur: {calculatedDuration:F1}, FinalDur: {finalDuration:F1}s at point {hitPoint}");

        // --- Instantiate and Scale the Hit Effect! ---
        if (hitEffectPrefab != null)
        {
            GameObject effectInstance = Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
            effectInstance.transform.localScale = Vector3.one * currentScale; // Scale effect by bullet size
            // Manage effect lifetime
        }
        else
        {
            Debug.LogWarning("FreezeBullet 'Hit Effect Prefab' is not assigned!", this);
        }

        // --- Apply Direct Hit Effects (Damage & Freeze) ---
        EnemyBubble enemyBubble = hitObject.GetComponent<EnemyBubble>(); // Assuming direct hit on bubble?
        BaseEnemy baseEnemy = hitObject.GetComponentInChildren<BaseEnemy>(); // More general check

        if (baseEnemy != null) // Prioritize BaseEnemy component
        {
            baseEnemy.TakeDamage(finalDamage, DamageType.Freeze); // Use finalDamage
            baseEnemy.Freeze(finalDuration); // Use finalDuration
        }
        else if (enemyBubble != null) // Fallback for older EnemyBubble structure if still used
        {
             // enemyBubble.EnemyTakeDamage(finalDamage); // Assuming method exists
             // enemyBubble.Freeze(finalDuration); // Assuming method exists
             Debug.LogWarning($"Hit {hitObject.name} with EnemyBubble but NO BaseEnemy. Applying effects via EnemyBubble.", this);
        }


        // --- Handle Splash Damage & Freeze in an Area ---
        float splashRadius = currentScale * 0.6f; // Example radius
        Collider[] hitColliders = Physics.OverlapSphere(bulletTransform.position, splashRadius);

        foreach (var hitCollider in hitColliders)
        {
            // Skip the object that was directly hit and the player
            if (hitCollider.gameObject == hitObject || hitCollider.CompareTag("Player")) continue;

            BaseEnemy splashEnemy = hitCollider.GetComponentInChildren<BaseEnemy>();
            if (splashEnemy != null)
            {
                // Apply multiplier to splash effects too (can be reduced)
                int splashDamage = finalDamage;           // Example: Or Mathf.CeilToInt(finalDamage * 0.5f);
                float splashFreezeDuration = finalDuration; // Example: Or finalDuration * 0.75f;

                Debug.Log($"Applying splash freeze ({splashFreezeDuration:F1}s) and damage ({splashDamage}) to {splashEnemy.name}");
                splashEnemy.TakeDamage(splashDamage, DamageType.Freeze);
                splashEnemy.Freeze(splashFreezeDuration);
            }
             // Add fallback for EnemyBubble if needed
        }

        // --- Destroy the Bullet GameObject ---
        Destroy(gameObject);
    }
}