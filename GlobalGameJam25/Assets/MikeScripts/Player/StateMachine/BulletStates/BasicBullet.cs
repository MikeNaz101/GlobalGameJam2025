using UnityEngine;

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

    [Header("Lifetime & Effects")]
    public float lifetime = 5.0f;
    public GameObject hitEffectPrefab; // Assign in Inspector! ✨

    // Private Variables
    private bool _isCharging = false;
    private bool _wasStarted = false;
    private float chargeStartTime;
    private PlayerStateManager _player;
    private Transform bulletTransform;
    private float expireTime;
    private bool hasExploded = false;

    void Awake()
    {
        bulletTransform = transform;
    }

    void Start()
    {
        expireTime = Time.time + lifetime;
    }

    // Called by PlayerShooting when charging begins
    public void StartCharging(PlayerStateManager playerRef)
    {
        if (playerRef == null) { Debug.LogError("Player reference is null in StartCharging!", this); Destroy(gameObject); return; }
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
        if (!_wasStarted) return 1f;
         _isCharging = false;
         Debug.Log("BasicBullet charging stopped.");
         float chargeDuration = Mathf.Clamp(Time.time - chargeStartTime, 0, maxChargeTime);
         float chargePercentage = (maxChargeTime > 0) ? (chargeDuration / maxChargeTime) : 1.0f;
         float chargeMultiplier = Mathf.Lerp(1f, maxChargeMultiplier, chargePercentage);
         return chargeMultiplier;
    }

    private void Update()
    {
        // Charging Logic
        if (_isCharging)
        {
            float chargeTime = Time.time - chargeStartTime;
            float manaToAttemptDeduct = manaCostPerSecond * Time.deltaTime;
            int manaCostThisFrame = Mathf.CeilToInt(manaToAttemptDeduct);
            if (manaCostThisFrame > 0 && (_player == null || !_player.UseMana(manaCostThisFrame))) {
                _isCharging = false; Debug.Log("BasicBullet stopped charging (out of mana or player lost).");
            } else if (_player != null) {
                 float effectiveChargeTime = Mathf.Min(chargeTime, maxChargeTime);
                 float chargePercentage = (maxChargeTime > 0) ? (effectiveChargeTime / maxChargeTime) : 1.0f;
                 float newScale = Mathf.Lerp(1f, maxSize, chargePercentage);
                 bulletTransform.localScale = Vector3.one * newScale;
            } else { _isCharging = false; Debug.Log("BasicBullet stopped charging (player became null)."); }
        }

        // Lifetime Expiry Check
        if (!hasExploded && Time.time >= expireTime)
        {
            Debug.Log($"BasicBullet lifetime expired at {Time.time}s.");
            Explode(bulletTransform.position, -bulletTransform.forward); // Explode without a hit object
        }
    }

    // Using OnCollisionEnter for physics interactions
    void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;

        // Optional: Ignore player for a very short time after firing if needed
        // if (collision.gameObject.CompareTag("Player") && Time.time < expireTime - (lifetime - 0.1f)) return;
        if (collision.gameObject.CompareTag("Player")) return; // Simple ignore

        if (collision.contacts.Length > 0)
        {
            ContactPoint contact = collision.contacts[0];
            Explode(contact.point, contact.normal, collision.gameObject);
        }
        else { Explode(bulletTransform.position, -bulletTransform.forward, collision.gameObject); }
    }

    // Central method to handle explosion logic
    void Explode(Vector3 position, Vector3 normal, GameObject hitObject = null)
    {
        if (hasExploded) return;
        hasExploded = true;

        float currentScale = Mathf.Max(1f, bulletTransform.localScale.x);
        int damage = baseDamage + Mathf.FloorToInt((currentScale - 1f) * damageMultiplier);

        Debug.Log($"BasicBullet exploding. Hit: {(hitObject != null ? hitObject.name : "Lifetime Expired")}, Scale: {currentScale:F2}, Damage: {damage} at point {position}");

        // Instantiate and Scale the Hit Effect
        if (hitEffectPrefab != null)
        {
            GameObject effectInstance = Instantiate(hitEffectPrefab, position, Quaternion.LookRotation(normal));
            effectInstance.transform.localScale = Vector3.one * currentScale;
        } else { Debug.LogWarning("BasicBullet 'Hit Effect Prefab' is not assigned!"); }

        // Apply Damage (Only if caused by collision)
        if (hitObject != null)
        {
            // --- Find BaseEnemy on hit object or its children ---
            BaseEnemy enemy = hitObject.GetComponentInChildren<BaseEnemy>(); // Use InChildren!
            if (enemy != null)
            {
                Debug.Log($"Applying {damage} Basic damage to {enemy.gameObject.name} (found via GetComponentInChildren on {hitObject.name})");
                enemy.TakeDamage(damage, DamageType.Basic);
            }
            else { Debug.Log($"{hitObject.name} was hit but doesn't have a BaseEnemy component on itself or children."); }
            // -----------------------------------------------------

            // Handle Splash Damage
            float splashRadius = currentScale * 0.5f;
            Collider[] hitColliders = Physics.OverlapSphere(position, splashRadius);
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.gameObject == hitObject || hitCollider.CompareTag("Player")) continue;

                // --- Find BaseEnemy on splashed object or its children ---
                BaseEnemy splashEnemy = hitCollider.GetComponentInChildren<BaseEnemy>(); // Use InChildren!
                if (splashEnemy != null)
                {
                    int splashDamage = damage; // Or reduced splash: damage / 2;
                    Debug.Log($"Applying {splashDamage} Basic splash damage to {splashEnemy.gameObject.name} (found via GetComponentInChildren on {hitCollider.gameObject.name})");
                    splashEnemy.TakeDamage(splashDamage, DamageType.Basic);
                }
                // --------------------------------------------------------
            }
        }

        // Destroy Bullet
        Destroy(gameObject);
    }
}