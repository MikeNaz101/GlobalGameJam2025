using UnityEngine;

// Implement the interface defined in PlayerShooting
public class BasicBullet : MonoBehaviour, PlayerShooting.IBulletChargeReceiver
{
    [Header("Damage & Scaling")]
    [Tooltip("Base damage dealt by the bullet at minimum charge (scale=1).")]
    public int baseDamage = 10;
    [Tooltip("Additional damage added per unit of scale increase (from 1.0 up to maxSize). E.g., scale 2 adds 'damageMultiplier' damage.")]
    public int damageMultiplier = 5;
    [Tooltip("Maximum scale the bullet reaches at full charge.")]
    public float maxSize = 4f; // This defines the scale at chargeRatio = 1.0

    // Removed charging-specific fields like manaCostPerSecond, maxChargeTime, maxChargeMultiplier
    // Those are now handled by PlayerShooting

    [Header("Lifetime & Effects")]
    [Tooltip("How many seconds the bullet lasts before expiring.")]
    public float lifetime = 5.0f;
    [Tooltip("Particle effect prefab spawned on impact or expiry.")]
    public GameObject hitEffectPrefab; // Assign in Inspector! ✨

    // --- Private Variables ---
    private PlayerStateManager _player; // Reference to player for LEVEL MULTIPLIER
    private Transform bulletTransform;
    private float finalScale = 1.0f;    // Stores the scale determined by chargeRatio
    private bool hasExploded = false;   // Prevents multiple explosions
    private bool initialized = false;   // Tracks if OnFire has been called

    void Awake()
    {
        bulletTransform = transform;
        // Don't set expireTime here, wait until OnFire confirms it's launched
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
            Debug.LogError("BasicBullet received null Player reference in OnFire!", this);
            // Optionally destroy immediately if player ref is essential
            // Destroy(gameObject);
            // return;
        }

        // 2. Calculate Final Scale based on chargeRatio
        // Lerp between scale 1.0 (chargeRatio=0) and maxSize (chargeRatio=1)
        finalScale = Mathf.Lerp(1.0f, maxSize, chargeRatio);
        bulletTransform.localScale = Vector3.one * finalScale; // Set the scale immediately

        // 3. Set Lifetime Timer
        Destroy(gameObject, lifetime); // Use Unity's built-in Destroy timer

        initialized = true; // Mark as initialized
        hasExploded = false; // Ensure explosion flag is reset

        // Optional: Log received info
        Debug.Log($"BasicBullet Initialized: ChargeRatio={chargeRatio:P1}, FinalScale={finalScale:F2}, ForceMult={forceMultiplier:F2}");
    }
    // --- End of Interface Implementation ---


    // Update is now ONLY for checking if initialization failed (e.g., PlayerShooting didn't call OnFire)
    // Or potentially for homing logic etc., but NOT for charging.
    void Update()
    {
        // Optional: safety check if OnFire was never called after a short delay
        // Consider removing this if it's not necessary
        // if (!initialized && Time.time > 0.5f) // Example check after 0.5 seconds
        // {
        //     Debug.LogWarning("BasicBullet Update: Never Initialized via OnFire. Destroying.", this);
        //     Destroy(gameObject);
        // }

        // Lifetime expiry is handled by Destroy(gameObject, lifetime) in OnFire now.
    }


    // Using OnCollisionEnter for physics interactions
    void OnCollisionEnter(Collision collision)
    {
        // Don't do anything if already exploded or not properly initialized
        if (hasExploded || !initialized) return;

        // Simple ignore player collision
        if (collision.gameObject.CompareTag("Player")) return;

        // Get contact point for accurate effect placement
        if (collision.contacts.Length > 0)
        {
            ContactPoint contact = collision.contacts[0];
            Explode(contact.point, contact.normal, collision.gameObject);
        }
        else // Fallback if contact point isn't available for some reason
        {
            Explode(bulletTransform.position, -bulletTransform.forward, collision.gameObject);
        }
    }

    // Central method to handle explosion logic
    void Explode(Vector3 position, Vector3 normal, GameObject hitObject = null)
    {
        if (hasExploded) return; // Should be redundant due to checks in collision/lifetime, but safe
        hasExploded = true;

        // --- Damage Calculation ---
        // Base damage + bonus damage from scaling (finalScale is set in OnFire)
        // Note: finalScale is already clamped between 1.0 and maxSize via Lerp
        int calculatedDamage = baseDamage + Mathf.FloorToInt((finalScale - 1.0f) * damageMultiplier);

        // --- Apply Player Level Multiplier ---
        float playerMultiplier = 1.0f; // Default multiplier
        if (_player != null)
        {
            playerMultiplier = _player.bulletEffectMultiplier;
        }
        else { Debug.LogWarning("BasicBullet: Player reference was null during explosion!", this); }

        int finalDamage = Mathf.Max(1, Mathf.CeilToInt(calculatedDamage * playerMultiplier)); // Ensure damage is at least 1
        // ------------------------------------

        Debug.Log($"BasicBullet exploding. Hit: {(hitObject != null ? hitObject.name : "Lifetime Expired")}, Scale: {finalScale:F2}, BaseDmg: {calculatedDamage}, FinalDmg (x{playerMultiplier:F2}): {finalDamage} at point {position}");

        // --- Instantiate and Scale Hit Effect ---
        if (hitEffectPrefab != null)
        {
            GameObject effectInstance = Instantiate(hitEffectPrefab, position, Quaternion.LookRotation(normal));
            // Scale the effect based on the bullet's final scale
            effectInstance.transform.localScale = Vector3.one * finalScale;
            // Ensure the effect has a ParticleSystem component with "Stop Action" set to "Destroy"
            // or add a self-destruct script to the effect prefab.
        }
        else { Debug.LogWarning("BasicBullet 'Hit Effect Prefab' is not assigned!"); }

        // --- Apply Damage (Only if it hit something) ---
        if (hitObject != null)
        {
            // --- Apply Direct Damage ---
            BaseEnemy enemy = hitObject.GetComponentInChildren<BaseEnemy>(); // Use InChildren!
            if (enemy != null)
            {
                Debug.Log($"Applying {finalDamage} Basic damage to {enemy.gameObject.name} (found via GetComponentInChildren on {hitObject.name})");
                enemy.TakeDamage(finalDamage, DamageType.Basic); // Use finalDamage
            }
            else // If it's not an enemy, check if it's a breakable boulder
            {
                BreakableBoulder boulder = hitObject.GetComponent<BreakableBoulder>(); // Check on the hit object itself
                if (boulder != null)
                {
                    Debug.Log($"Applying {finalDamage} damage to BreakableBoulder: {boulder.gameObject.name}");
                    boulder.TakeDamage(finalDamage); // Call the boulder's damage method
                }
                // You could add more 'else if' checks here for other damageable types
                else
                {
                    Debug.Log($"{hitObject.name} was hit but doesn't have a BaseEnemy or BreakableBoulder component.");
                }
            }

            // --- Apply Splash Damage ---
            // Example radius scales with bullet size
            float splashRadius = finalScale * 0.75f; // Adjust multiplier as needed
            Collider[] hitColliders = Physics.OverlapSphere(position, splashRadius);
            foreach (var hitCollider in hitColliders)
            {
                // Don't splash self or the object directly hit, or the player
                if (hitCollider.gameObject == hitObject || hitCollider.gameObject == gameObject || hitCollider.CompareTag("Player")) continue;

                BaseEnemy splashEnemy = hitCollider.GetComponentInChildren<BaseEnemy>(); // Use InChildren!
                if (splashEnemy != null)
                {
                    // Apply multiplier to splash damage too (can be reduced if desired)
                    // Example: Splash damage is half of direct damage
                    int splashDamage = Mathf.Max(1, Mathf.CeilToInt(finalDamage * 0.5f));
                    Debug.Log($"Applying {splashDamage} Basic splash damage to {splashEnemy.gameObject.name} (found via GetComponentInChildren on {hitCollider.gameObject.name})");
                    splashEnemy.TakeDamage(splashDamage, DamageType.Basic);
                }
            }
        }

        // Destroy Bullet GameObject immediately after explosion logic
        // (Destroy(gameObject, lifetime) in OnFire handles expiry)
        Destroy(gameObject);
    }
}