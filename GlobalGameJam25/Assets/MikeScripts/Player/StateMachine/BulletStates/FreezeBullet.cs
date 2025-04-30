using UnityEngine;

// Implement the interface defined in PlayerShooting
public class FreezeBullet : MonoBehaviour, PlayerShooting.IBulletChargeReceiver
{
    [Header("Damage & Scaling")]
    [Tooltip("Base damage dealt by the bullet at minimum charge (scale=1).")]
    public int baseDamage = 5;
    [Tooltip("Additional damage added per unit of scale increase (from 1.0 up to maxSize).")]
    public int damageMultiplier = 3;
    [Tooltip("Maximum scale the bullet reaches at full charge (chargeRatio=1.0).")]
    public float maxSize = 3f;

    [Header("Freeze Effect")]
    [Tooltip("Freeze duration applied at minimum charge (scale=1).")]
    public float freezeDurationMin = 1f;
    [Tooltip("Freeze duration applied at maximum charge (scale=maxSize).")]
    public float freezeDurationMax = 5f;

    [Header("Lifetime & Effects")]
    [Tooltip("How many seconds the bullet lasts before expiring.")]
    public float lifetime = 7.0f; // Adjusted default lifetime slightly
    [Tooltip("Particle effect prefab spawned on impact. Assign in Inspector.")]
    public GameObject hitEffectPrefab; // ✨ Drag your particle system PREFAB here ✨

    // --- Private Variables ---
    private PlayerStateManager _player; // Reference set by PlayerShooting via OnFire
    private float finalScale = 1.0f;    // Stores the scale determined by chargeRatio
    private bool initialized = false;   // Tracks if OnFire has been called
    private bool hasHit = false;        // Prevents multiple hits/explosions
    private Transform bulletTransform;  // Cache transform

    void Awake()
    {
        // Cache the transform component
        bulletTransform = transform;
    }

    // Start() is minimal, initialization happens in OnFire
    void Start()
    {
        // Debug.Log("FreezeBullet GameObject instantiated.");
    }

    // --- IBulletChargeReceiver Implementation ---
    // This method is called by PlayerShooting exactly ONCE when the bullet is fired.
    public void OnFire(float chargeRatio, float forceMultiplier, PlayerStateManager playerRef)
    {
        if (initialized) return; // Prevent multiple initializations

        // 1. Store Player Reference
        _player = playerRef;
        if (_player == null)
        {
            Debug.LogError("FreezeBullet received null Player reference in OnFire! Multiplier will be 1.0.", this);
        }

        // 2. Calculate Final Scale based on chargeRatio
        // Lerp between scale 1.0 (chargeRatio=0) and maxSize (chargeRatio=1)
        finalScale = Mathf.Lerp(1.0f, maxSize, chargeRatio);
        bulletTransform.localScale = Vector3.one * finalScale; // Set the scale immediately

        // 3. Set Lifetime Timer
        Destroy(gameObject, lifetime);

        initialized = true; // Mark as initialized
        hasHit = false;     // Reset hit flag

        // Optional: Log received info
        Debug.Log($"FreezeBullet Initialized: ChargeRatio={chargeRatio:P1}, FinalScale={finalScale:F2}, ForceMult={forceMultiplier:F2}");
    }
    // --- End of Interface Implementation ---


    // Update is no longer needed for charging logic.
    // Could be used for visual effects that change over the bullet's lifetime.
    // void Update() { }


    // --- Collision Handling ---

    // Called when this collider/rigidbody has begun touching another rigidbody/collider
    void OnCollisionEnter(Collision collision)
    {
        // Don't process collision if not initialized or already hit something
        if (!initialized || hasHit) return;

        // Ignore collisions with the player who fired it
        if (collision.gameObject.CompareTag("Player"))
        {
            return;
        }

        // Ignore hitting other friendly projectiles
        if (collision.gameObject.CompareTag("PlayerProjectile")) // Assuming you tag player bullets
        {
            return;
        }

        // Get contact point information
        if (collision.contacts.Length > 0)
        {
            ContactPoint contact = collision.contacts[0];
            // Call the central hit processing logic
            OnHit(collision.gameObject, contact.point, contact.normal);
        }
        else
        {
            // Fallback if no contact points (rare)
            OnHit(collision.gameObject, bulletTransform.position, -bulletTransform.forward);
        }
    }

    // Central logic for handling what happens when the bullet hits *anything* valid
    private void OnHit(GameObject hitObject, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (hasHit) return; // Double-check to prevent multiple calls
        hasHit = true;      // Mark as hit

        // --- Calculate Final Damage & Freeze Duration based on finalScale (set in OnFire) ---
        // Damage
        int calculatedDamage = baseDamage + Mathf.FloorToInt((finalScale - 1.0f) * damageMultiplier);
        // Duration - Calculate scale progress (0 to 1) based on current scale vs max scale
        float scaleProgressRatio = (maxSize > 1.0f) ? Mathf.Clamp01((finalScale - 1.0f) / (maxSize - 1.0f)) : 0f;
        float calculatedDuration = Mathf.Lerp(freezeDurationMin, freezeDurationMax, scaleProgressRatio);

        // --- Apply Player Level Multiplier ---
        float playerMultiplier = 1.0f; // Default
        if (_player != null)
        {
            playerMultiplier = _player.bulletEffectMultiplier;
        }
        else { Debug.LogWarning("FreezeBullet: Player reference was null during OnHit!", this); }

        int finalDamage = Mathf.Max(1, Mathf.CeilToInt(calculatedDamage * playerMultiplier)); // Ensure at least 1 damage
        float finalDuration = calculatedDuration * playerMultiplier; // Apply multiplier to duration too
        // ------------------------------------

        Debug.Log($"FreezeBullet hit {hitObject.name}, Scale: {finalScale:F2}, BaseDmg: {calculatedDamage}, FinalDmg (x{playerMultiplier:F2}): {finalDamage}, BaseDur: {calculatedDuration:F1}, FinalDur: {finalDuration:F1}s at point {hitPoint}");

        // --- Instantiate and Scale the Hit Effect ---
        if (hitEffectPrefab != null)
        {
            GameObject effectInstance = Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
            effectInstance.transform.localScale = Vector3.one * finalScale; // Scale effect by bullet size
            // Ensure the effect prefab destroys itself after playing
        }
        else
        {
            Debug.LogWarning("FreezeBullet 'Hit Effect Prefab' is not assigned!", this);
        }

        // --- Apply Direct Hit Effects (Damage & Freeze) ---
        // Use GetComponentInChildren for flexibility if collider is not on the main enemy object
        BaseEnemy baseEnemy = hitObject.GetComponentInChildren<BaseEnemy>();
        if (baseEnemy != null)
        {
            Debug.Log($"Applying direct freeze ({finalDuration:F1}s) and damage ({finalDamage}) to {baseEnemy.gameObject.name}");
            baseEnemy.TakeDamage(finalDamage, DamageType.Freeze); // Pass DamageType
            baseEnemy.Freeze(finalDuration);
        }

        // --- Handle Splash Damage & Freeze in an Area ---
        float splashRadius = finalScale * 0.75f; // Example: Splash radius scales with bullet size
        Collider[] hitColliders = Physics.OverlapSphere(hitPoint, splashRadius); // Use hitPoint as center

        foreach (var hitCollider in hitColliders)
        {
            // Skip the object that was directly hit, the player, and the bullet itself
            if (hitCollider.gameObject == hitObject || hitCollider.gameObject == gameObject || hitCollider.CompareTag("Player")) continue;

            BaseEnemy splashEnemy = hitCollider.GetComponentInChildren<BaseEnemy>();
            if (splashEnemy != null)
            {
                // Apply multiplier to splash effects too (can be reduced if desired)
                // Example: Splash damage/duration is 75% of direct hit
                int splashDamage = Mathf.Max(1, Mathf.CeilToInt(finalDamage * 0.75f));
                float splashFreezeDuration = finalDuration * 0.75f;

                Debug.Log($"Applying splash freeze ({splashFreezeDuration:F1}s) and damage ({splashDamage}) to {splashEnemy.gameObject.name}");
                splashEnemy.TakeDamage(splashDamage, DamageType.Freeze); // Pass DamageType
                splashEnemy.Freeze(splashFreezeDuration);
            }
        }

        // --- Destroy the Bullet GameObject ---
        // Destroy immediately after processing the hit
        Destroy(gameObject);
    }
}