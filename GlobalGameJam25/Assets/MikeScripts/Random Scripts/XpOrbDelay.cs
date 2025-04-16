using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class XpOrbDelay : MonoBehaviour
{
    [Tooltip("How long (in seconds) after spawning before the orb starts being affected by external forces (like the player's attractor).")]
    public float enableForceFieldInteractionDelay = 0.5f;

    private ParticleSystem ps;
    private bool forcesEnabled = false;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        // Disable external forces initially
        var forces = ps.externalForces;
        forces.enabled = false;
    }

    void Start()
    {
        // Schedule forces to be enabled after the delay
        if (enableForceFieldInteractionDelay > 0)
        {
            Invoke(nameof(EnableForces), enableForceFieldInteractionDelay);
        }
        else
        {
            EnableForces(); // Enable immediately if delay is zero
        }
    }

    void EnableForces()
    {
        if (ps != null && !forcesEnabled) // Check if ps exists and not already enabled
        {
            var forces = ps.externalForces;
            forces.enabled = true;
            forcesEnabled = true;
            // Optional: Debug.Log("XP Orb forces enabled - Homing active!");
        }
    }

    // Optional: Add collision handling here if NOT using Triggers module
    // void OnParticleCollision(GameObject other) {
    //    if(other.CompareTag("Player")) { // Check tag if needed
    //        PlayerStateManager player = other.GetComponentInParent<PlayerStateManager>();
    //        player?.GrantXP(1); // Grant fixed XP or get from another component
    //        // Note: Particle system might kill the particle automatically based on Collision module settings
    //    }
    // }
}