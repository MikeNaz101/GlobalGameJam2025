using UnityEngine;
using System.Collections; // Might be needed if adding delays later

// Put this script on the GameObject that should act as the aura's center.
// It requires:
// 1. A ParticleSystem component (assign to 'auraParticles').
// 2. A SphereCollider component (set to 'Is Trigger').
// 3. A Rigidbody component (set to 'Is Kinematic').
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class HealingAura : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("The radius within which the player is affected.")]
    public float detectionRadius = 10f;
    [Tooltip("The tag identifying the player GameObject.")]
    public string playerTag = "Player";

    [Header("Effects")]
    [Tooltip("Assign the Particle System component for the aura visual.")]
    public ParticleSystem auraParticles;

    [Header("Restoration Rates (per second at closest range)")]
    [Tooltip("Max health restored per second when the player is very close.")]
    public float maxHealthPerSecond = 10f;
    [Tooltip("Max mana restored per second when the player is very close.")]
    public float maxManaPerSecond = 15f;
    [Tooltip("The distance below which the maximum restoration rate applies.")]
    public float minDistanceForMaxRate = 1f; // Prevents extreme rates right at the center

    [Header("Particle Alpha Control")]
    [Tooltip("Particle alpha when player is at the edge of the radius (0 = fully transparent).")]
    [Range(0f, 1f)]
    public float minAlpha = 0.1f;
    [Tooltip("Particle alpha when player is at the minimum distance (1 = fully opaque).")]
    [Range(0f, 1f)]
    public float maxAlpha = 0.8f;

    // Private runtime variables
    private Transform playerTransform;
    private PlayerStateManager playerStateManager;
    private bool isPlayerInRange = false;
    private SphereCollider sphereCollider;
    private ParticleSystem.MinMaxGradient originalStartColor; // To preserve original color values

    // Accumulators for smooth restoration
    private float healthAccumulator = 0f;
    private float manaAccumulator = 0f;

    void Awake()
    {
        // --- Essential Setup ---
        if (auraParticles == null)
        {
            Debug.LogError("HealingAura: Aura Particles system is not assigned!", this);
            this.enabled = false; // Disable script if particles aren't set
            return;
        }

        // Store original particle color settings (assumes a single start color for simplicity)
        // NOTE: If using gradients or complex color modes, alpha control might need adjustment.
        originalStartColor = auraParticles.main.startColor;
        // Ensure particles don't play automatically
        auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // Stop and clear any existing particles
        var mainModule = auraParticles.main;
        mainModule.playOnAwake = false;

        // --- Configure Collider ---
        sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = detectionRadius;

        // --- Configure Rigidbody ---
        // Rigidbody is needed for trigger events on static objects interacting with non-kinematic Rigidbody (like a player)
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if the entering object is the player
        if (!isPlayerInRange && other.CompareTag(playerTag))
        {
            // Try to get the PlayerStateManager
            PlayerStateManager psm = other.GetComponent<PlayerStateManager>();
            if (psm != null)
            {
                Debug.Log("Player entered Healing Aura radius.", this);
                playerTransform = other.transform;
                playerStateManager = psm;
                isPlayerInRange = true;
                healthAccumulator = 0f; // Reset accumulators on entry
                manaAccumulator = 0f;

                // Start playing the particles
                if (auraParticles != null)
                {
                    auraParticles.Play();
                }
            }
            else
            {
                Debug.LogWarning($"Object tagged '{playerTag}' entered radius but lacks PlayerStateManager component.", other);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Check if the exiting object is the player we are currently tracking
        if (isPlayerInRange && other.CompareTag(playerTag) && other.transform == playerTransform)
        {
            Debug.Log("Player exited Healing Aura radius.", this);
            isPlayerInRange = false;
            playerTransform = null;
            playerStateManager = null;

            // Stop the particles gracefully
            if (auraParticles != null)
            {
                auraParticles.Stop(); // Let existing particles fade out
            }
        }
    }

    void Update()
    {
        // Only run logic if the player is confirmed to be inside the radius
        if (!isPlayerInRange || playerTransform == null || playerStateManager == null)
        {
            return;
        }

        // --- Calculate Proximity ---
        float distance = Vector3.Distance(transform.position, playerTransform.position);

        // Calculate proximity factor: 1 = closest (at minDistanceForMaxRate), 0 = farthest (at detectionRadius)
        // Ensure denominator isn't zero if minDistance >= detectionRadius
        float range = Mathf.Max(0.01f, detectionRadius - minDistanceForMaxRate);
        float proximityFactor = 1.0f - Mathf.Clamp01((distance - minDistanceForMaxRate) / range);

        // --- Control Particle Alpha ---
        /*if (auraParticles != null)
        {
            float targetAlpha = Mathf.Lerp(minAlpha, maxAlpha, proximityFactor);
            var main = auraParticles.main;

            // Create a new gradient/color based on the original, but with the new alpha.
            // This simplified version assumes the original startColor was a single color.
            // More complex logic needed if originalStartColor used gradients.
            if (originalStartColor.mode == ParticleSystemGradientMode.Color)
            {
                Color newColor = originalStartColor.color;
                newColor.a = targetAlpha;
                main.startColor = new ParticleSystem.MinMaxGradient(newColor);
            }
            // TODO: Add handling for other ParticleSystemGradientModes if needed
            else
            {
                 // Fallback or warning if using gradients, as simple alpha blend won't work directly
                 // Debug.LogWarning("Healing Aura particle alpha control might not work correctly with gradient start colors.", this);
            }
        }*/

        // --- Apply Health & Mana Restoration ---
        // Calculate current rates based on proximity
        float currentHealthRate = Mathf.Lerp(0, maxHealthPerSecond, proximityFactor);
        float currentManaRate = Mathf.Lerp(0, maxManaPerSecond, proximityFactor);

        // Accumulate restoration potential over time
        healthAccumulator += currentHealthRate * Time.deltaTime;
        manaAccumulator += currentManaRate * Time.deltaTime;

        // Apply health restoration in whole numbers when accumulator reaches >= 1
        if (healthAccumulator >= 1.0f && playerStateManager.currentHealth < playerStateManager.maxHealth)
        {
            int healthToRestore = Mathf.FloorToInt(healthAccumulator);
            playerStateManager.currentHealth = Mathf.Min(playerStateManager.currentHealth + healthToRestore, playerStateManager.maxHealth);
            healthAccumulator -= healthToRestore; // Subtract the whole number part applied
             Debug.Log($"Restored {healthToRestore} health. Current: {playerStateManager.currentHealth}", this); // Optional debug
        }

        // Apply mana restoration in whole numbers when accumulator reaches >= 1
        if (manaAccumulator >= 1.0f && playerStateManager.currentMana < playerStateManager.maxMana)
        {
            int manaToRestore = Mathf.FloorToInt(manaAccumulator);
            playerStateManager.currentMana = Mathf.Min(playerStateManager.currentMana + manaToRestore, playerStateManager.maxMana);
            playerStateManager.UpdateShellCountVisuals(); // IMPORTANT: Update shells after mana changes!
            manaAccumulator -= manaToRestore; // Subtract the whole number part applied
             Debug.Log($"Restored {manaToRestore} mana. Current: {playerStateManager.currentMana}", this); // Optional debug
        }
    }

    // Draw a helpful gizmo in the Scene view to visualize the detection radius
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, minDistanceForMaxRate);
    }
}
