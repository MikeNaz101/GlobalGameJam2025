using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
[RequireComponent(typeof(Collider))] // Ensure there's a Collider for trigger detection
public class XpOrbDelay : MonoBehaviour
{
    [Tooltip("How long (in seconds) after spawning before the orb starts being affected by external forces (like the player's attractor).")]
    public float enableForceFieldInteractionDelay = 0.5f;

    [Tooltip("How much XP this orb grants. Should match the enemy's xpValue or be set appropriately on the prefab.")]
    public int xpAmount = 10; // Set this on the prefab, or find a way to pass it from the enemy

    [Tooltip("Effect to play when collected (optional).")]
    public GameObject collectionEffectPrefab;

    [Tooltip("Sound to play when collected (optional).")]
    public AudioClip collectionSound;


    private ParticleSystem ps;
    private Collider col;
    private bool forcesEnabled = false;
    private bool collected = false; // Prevent multiple collections

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        col = GetComponent<Collider>();

        if (col == null)
        {
            Debug.LogError($"XP Orb '{gameObject.name}' is missing a Collider component!", this);
            enabled = false; // Disable script if no collider
            return;
        }

        // --- IMPORTANT: Set Collider to be a Trigger ---
        col.isTrigger = true;
        // ----------------------------------------------

        // Disable external forces initially if using Particle System Force Fields
        var forces = ps.externalForces;
        if (forces.enabled) // Only disable if it was enabled on the prefab
        {
            forces.enabled = false;
        }
    }

    void Start()
    {
        // Schedule forces to be enabled after the delay if needed
        if (enableForceFieldInteractionDelay > 0)
        {
            Invoke(nameof(EnableForces), enableForceFieldInteractionDelay);
        }
        else
        {
            EnableForces(); // Enable immediately if delay is zero or negative
        }

        // Optional: Automatically destroy the orb after some time if not collected
        // Destroy(gameObject, 15f); // e.g., Destroy after 15 seconds
    }

    void EnableForces()
    {
        if (ps != null && !forcesEnabled) // Check if ps exists and not already enabled
        {
            // Only re-enable if it was originally enabled
            // This requires checking prefab settings or assuming it should be enabled
            var forces = ps.externalForces;
            forces.enabled = true; // Assume we want forces enabled after delay
            forcesEnabled = true;
            // Optional: Debug.Log("XP Orb forces enabled - Homing active!");
        }
    }

    // --- Trigger Detection for Player Collection ---
    void OnTriggerEnter(Collider other)
    {
        if (collected) return; // Already collected, do nothing

        // Check if the object that entered the trigger has the "Player" tag
        if (other.CompareTag("Player"))
        {
            // Try to get the PlayerStateManager component from the player object
            PlayerStateManager playerState = other.GetComponent<PlayerStateManager>();
            // You might need GetComponentInParent if the collider is on a child object of the player
            // PlayerStateManager playerState = other.GetComponentInParent<PlayerStateManager>();

            if (playerState != null)
            {
                collected = true; // Mark as collected

                // Grant the XP to the player
                playerState.GainXP(xpAmount);
                Debug.Log($"Player collected XP Orb worth {xpAmount} XP.");

                // --- Optional: Play collection effects ---
                if (collectionEffectPrefab != null)
                {
                    Instantiate(collectionEffectPrefab, transform.position, Quaternion.identity);
                }
                if (collectionSound != null)
                {
                    // Play sound at the orb's position (requires an AudioSource)
                    // Or use a global sound manager: SoundManager.PlaySound(collectionSound);
                    AudioSource.PlayClipAtPoint(collectionSound, transform.position);
                }
                // -----------------------------------------

                // Destroy the XP orb GameObject
                Destroy(gameObject);
            }
            else
            {
                Debug.LogWarning($"XP Orb collided with Player tagged object '{other.name}', but couldn't find PlayerStateManager component.", other);
            }
        }
    }

    // Note: OnParticleCollision is used if the Particle System's "Collision" module is set to kill particles
    // AND you want the *particle itself* hitting the player to grant XP.
    // OnTriggerEnter is generally better for a collectible *object* like an orb prefab instance.
    // If you uncomment OnParticleCollision, ensure your Particle System Collision module is set up correctly.
    /*
    void OnParticleCollision(GameObject other)
    {
        if (collected) return; // Check collection status

        if (other.CompareTag("Player"))
        {
            PlayerStateManager playerState = other.GetComponent<PlayerStateManager>();
            // PlayerStateManager playerState = other.GetComponentInParent<PlayerStateManager>(); // Alternative

            if (playerState != null)
            {
                // Grant XP - Note: This might trigger multiple times if multiple particles hit!
                // It's often better to have ONE orb object grant the XP via OnTriggerEnter.
                playerState.GainXP(1); // Grant fixed XP or need a way to know particle's value
                Debug.Log("XP Particle hit Player");

                // You might not want to destroy the *entire orb object* here,
                // just handle the particle hit according to PS settings.
                // collected = true; // Mark collected if ONE particle hit should grant all XP and destroy orb
                // Destroy(gameObject);
            }
        }
    }
    */
}