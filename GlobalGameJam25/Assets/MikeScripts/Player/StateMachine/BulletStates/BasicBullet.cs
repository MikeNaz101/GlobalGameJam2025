using UnityEngine;

// Remember to assign the Hit Effect Prefab in the Inspector, darling!
public class BasicBullet : MonoBehaviour
{
    [Header("Damage & Scaling")]
    public int baseDamage = 10;
    public int damageMultiplier = 5;
    public float maxSize = 4f;

    [Header("Charging")]
    public float manaCostPerSecond = 2.5f;
    public float maxChargeTime = 2f;
    public float maxChargeMultiplier = 2f;

    [Header("Lifetime & Effects")] // Updated Header
    public float lifetime = 5.0f;       // Time in seconds before bullet explodes on its own
    public GameObject hitEffectPrefab; // Used for both impact and timeout explosion ✨

    // --- Private Variables ---
    private bool _isCharging = false;
    private bool _wasStarted = false;
    private float chargeStartTime;
    private PlayerStateManager _player;
    private Transform bulletTransform;
    private float expireTime;           // Time when the bullet should expire
    private bool hasExploded = false;   // Prevent double explosions

    void Awake()
    {
        bulletTransform = transform;
    }

    void Start() // Use Start for time-based initialization
    {
        // Calculate when the bullet should expire
        expireTime = Time.time + lifetime;
        // --- IMPORTANT: Ensure no Destroy(gameObject, time) calls are here ---
    }

    // Called by PlayerShooting when charging begins
    public void StartCharging(PlayerStateManager playerRef)
    {
        // ... (StartCharging logic remains the same - setting player, time, flags) ...
        if (playerRef == null) { /* ... Error handling ... */ Destroy(gameObject); return; }
         _player = playerRef;
         chargeStartTime = Time.time;
         _isCharging = true;
         _wasStarted = true;
         bulletTransform.localScale = Vector3.one; // Reset scale
         Debug.Log("BasicBullet charging started.");
    }

    // Called by PlayerShooting when charging ends
    public float StopCharging()
    {
        // ... (StopCharging logic remains the same - calculating multiplier) ...
        if (!_wasStarted) return 1f;
         _isCharging = false;
         Debug.Log("BasicBullet charging stopped.");
         float chargeDuration = Mathf.Clamp(Time.time - chargeStartTime, 0, maxChargeTime);
         float chargePercentage = (maxChargeTime > 0) ? (chargeDuration / maxChargeTime) : 1.0f;
         float chargeMultiplier = Mathf.Lerp(1f, maxChargeMultiplier, chargePercentage);
         return chargeMultiplier;
    }

    // Update is called once per frame
    private void Update()
    {
        // --- Charging Logic ---
        if (_isCharging)
        {
            // ... (Mana Drain and Scaling logic remains the same) ...
            float chargeTime = Time.time - chargeStartTime;
            float manaToAttemptDeduct = manaCostPerSecond * Time.deltaTime;
            int manaCostThisFrame = Mathf.CeilToInt(manaToAttemptDeduct);
            if (manaCostThisFrame > 0 && (_player == null || !_player.UseMana(manaCostThisFrame, _player))) {
                _isCharging = false; /* Log out of mana */
            } else if (_player != null) {
                 float effectiveChargeTime = Mathf.Min(chargeTime, maxChargeTime);
                 float chargePercentage = (maxChargeTime > 0) ? (effectiveChargeTime / maxChargeTime) : 1.0f;
                 float newScale = Mathf.Lerp(1f, maxSize, chargePercentage);
                 bulletTransform.localScale = Vector3.one * newScale;
            } else { _isCharging = false; /* Player became null? */ }
        }

        // --- Lifetime Expiry Check ---
        // Check if the bullet hasn't already exploded and its time is up
        if (!hasExploded && Time.time >= expireTime)
        {
            Debug.Log($"BasicBullet lifetime expired at {Time.time}s.");
            // Explode at the current position, maybe facing away from travel direction
            Explode(bulletTransform.position, -bulletTransform.forward); // Pass current position and a dummy normal
        }
    }

    // Using OnCollisionEnter for physics interactions
    void OnCollisionEnter(Collision collision)
    {
        // Prevent collision processing if already exploded (e.g., by lifetime)
        if (hasExploded) return;

        // Prevent collision with player right after firing
        if (collision.gameObject.CompareTag("Player"))
        {
            return;
        }

        // --- Get Hit Info and Call Explode ---
        if (collision.contacts.Length > 0)
        {
            ContactPoint contact = collision.contacts[0];
            // Call Explode with details from the collision
            Explode(contact.point, contact.normal, collision.gameObject); // Pass hit object too
        }
        else
        {
            // Fallback if no contact points (rare) - explode at bullet pos facing away
            Explode(bulletTransform.position, -bulletTransform.forward, collision.gameObject);
        }
    }

    // Central method to handle explosion logic (called by Collision or Lifetime Expiry)
    // Added optional hitObject parameter
    void Explode(Vector3 position, Vector3 normal, GameObject hitObject = null)
    {
        // Ensure this only runs once
        if (hasExploded) return;
        hasExploded = true;

        // Calculate final damage based on final scale (needs to happen here now)
        float currentScale = Mathf.Max(1f, bulletTransform.localScale.x); // Get scale just before explosion
        int damage = baseDamage + Mathf.FloorToInt((currentScale - 1f) * damageMultiplier);

        Debug.Log($"BasicBullet exploding. Hit: {(hitObject != null ? hitObject.name : "Lifetime Expired")}, Scale: {currentScale:F2}, Damage: {damage} at point {position}");

        // --- Instantiate and Scale the Hit Effect! ---
        if (hitEffectPrefab != null)
        {
            // Create the particle effect prefab instance at the explosion point
            GameObject effectInstance = Instantiate(hitEffectPrefab, position, Quaternion.LookRotation(normal));
            // Scale the particle effect instance based on the bullet's final scale
            effectInstance.transform.localScale = Vector3.one * currentScale;
            // Particle system should auto-destroy, add fallback if needed
        }
        else { Debug.LogWarning("BasicBullet 'Hit Effect Prefab' is not assigned!"); }

        // --- Apply Damage (Only if caused by collision, not timeout) ---
        if (hitObject != null)
        {
             EnemyBubble enemy = hitObject.GetComponent<EnemyBubble>();
             if (enemy != null)
             {
                 enemy.EnemyTakeDamage(damage);
             }

             // --- Handle Splash Damage ---
             float splashRadius = currentScale * 0.5f; // Example splash radius
             Collider[] hitColliders = Physics.OverlapSphere(position, splashRadius); // Use explosion position
             foreach (var hitCollider in hitColliders)
             {
                 if (hitCollider.gameObject == hitObject || hitCollider.CompareTag("Player")) continue;
                 EnemyBubble splashEnemy = hitCollider.GetComponent<EnemyBubble>();
                 if (splashEnemy != null)
                 {
                     int splashDamage = damage; // Or reduced splash: damage / 2;
                     splashEnemy.EnemyTakeDamage(splashDamage);
                 }
             }
        }
        // Note: No damage applied if explosion is due to lifetime expiry in this example

        // --- Destroy Bullet ---
        // Happens last, after effects are spawned and damage dealt
        Destroy(gameObject);
    }
}