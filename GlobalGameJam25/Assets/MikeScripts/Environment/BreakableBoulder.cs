using UnityEngine;

// Ensure the GameObject has an AudioSource component for playing sounds.
[RequireComponent(typeof(AudioSource))]
public class BreakableBoulder : MonoBehaviour
{
    [Header("Breaking Mechanics")]
    [Tooltip("The minimum amount of damage from a single hit required to break this boulder.")]
    public int damageThreshold = 50; // Set this value in the Unity Inspector

    [Tooltip("The AudioClip to play when the boulder breaks.")]
    public AudioClip breakSound;

    [Tooltip("Optional: A particle effect prefab (like dust or debris) to instantiate when the boulder breaks.")]
    public GameObject breakEffectPrefab;

    // --- Private Variables ---
    private AudioSource audioSource;
    private bool isBroken = false; // Flag to prevent breaking multiple times

    void Awake()
    {
        // Get the AudioSource component attached to this GameObject
        audioSource = GetComponent<AudioSource>();
        // Make sure the break sound doesn't play automatically when the game starts
        audioSource.playOnAwake = false;
    }

    /// <summary>
    /// Call this method from your bullet script (or any damage source)
    /// to apply damage to the boulder.
    /// </summary>
    /// <param name="damageAmount">The amount of damage dealt by the hit.</param>
    public void TakeDamage(int damageAmount)
    {
        // If the boulder is already broken, or the damage isn't enough, do nothing.
        if (isBroken || damageAmount < damageThreshold)
        {
            // Optional: You could play a 'clink' or 'hit' sound here for non-breaking impacts
            // if (damageAmount > 0) { /* Play hit sound */ }
            // Debug.Log($"Boulder took {damageAmount} damage. Threshold is {damageThreshold}. Not breaking.");
            return;
        }

        // --- The Boulder Breaks! ---
        isBroken = true; // Set the flag immediately to prevent duplicate breaks
        Debug.Log($"Boulder hit with {damageAmount} damage (Threshold: {damageThreshold}). BREAKING!");

        // 1. Play the Breaking Sound
        // Use PlayClipAtPoint: This static method plays a sound at a world position
        // even if the object playing it is destroyed immediately after.
        if (breakSound != null)
        {
            AudioSource.PlayClipAtPoint(breakSound, transform.position, audioSource.volume); // Use boulder's volume setting
        }
        else
        {
            Debug.LogWarning("BreakableBoulder: 'Break Sound' AudioClip is not assigned!", this);
        }

        // 2. Instantiate Break Effect (Optional)
        if (breakEffectPrefab != null)
        {
            Instantiate(breakEffectPrefab, transform.position, transform.rotation);
            // Ensure the prefab cleans itself up (e.g., Particle System 'Stop Action' set to Destroy)
        }

        // 3. Destroy the Boulder GameObject
        Destroy(gameObject);
    }
}