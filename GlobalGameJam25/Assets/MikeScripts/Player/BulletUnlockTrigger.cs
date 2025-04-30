using UnityEngine;

// Ensures the GameObject has a Collider component.
[RequireComponent(typeof(Collider))]
public class BulletUnlockTrigger : MonoBehaviour
{
    [Header("Unlock Settings")]
    [Tooltip("The index (BulletType enum value) this trigger unlocks. e.g., 1 for the second type (Freeze), 2 for the third (Teleport). Index 0 is default.")]
    [Min(1)] // Enforce unlocking index 1 or higher via triggers.
    public int bulletIndexToUnlock = 1;

    [Tooltip("Should the trigger deactivate itself after successfully unlocking? Prevents re-triggering.")]
    public bool disableAfterUnlock = true;

    [Header("Optional Feedback")]
    [Tooltip("Particle effect prefab to instantiate at the trigger's location on successful unlock.")]
    public GameObject unlockEffectPrefab; // Assign in Inspector (Optional)

    [Tooltip("Sound to play on successful unlock. Uses the Player's AudioSource for playback.")]
    public AudioClip unlockSound; // Assign in Inspector (Optional)

    private bool _hasBeenTriggered = false; // Internal flag to prevent repeated triggers if disabling

    void Start()
    {
        // Ensure the attached collider is actually set as a trigger.
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"Collider on '{gameObject.name}' (BulletUnlockTrigger) was not set to 'Is Trigger'. Forcing it to true.", this);
            col.isTrigger = true;
        }
    }

    // Called by Unity Physics when another Collider enters this trigger.
    void OnTriggerEnter(Collider other)
    {
        // If we want to disable after unlock, and it has already been triggered, do nothing.
        if (disableAfterUnlock && _hasBeenTriggered)
        {
            return;
        }

        // Check if the object that entered has the "Player" tag.
        // MAKE SURE YOUR PLAYER GAMEOBJECT IS TAGGED "Player" IN THE INSPECTOR.
        if (other.CompareTag("Player"))
        {
            // Try to get the PlayerStateManager component from the player GameObject.
            PlayerStateManager playerManager = other.GetComponent<PlayerStateManager>();

            // Check if we successfully found the component.
            if (playerManager != null)
            {
                // Attempt to unlock the specified bullet index on the player.
                // The UnlockBulletType method returns true if a new unlock level was actually reached.
                bool didUnlock = playerManager.UnlockBulletType(bulletIndexToUnlock);

                // Only play effects and potentially disable if a *new* unlock level was achieved.
                if (didUnlock)
                {
                    Debug.Log($"Player entered trigger '{gameObject.name}'. Successfully unlocked bullet index up to: {bulletIndexToUnlock}", this);

                    // Play optional visual and audio feedback.
                    PlayUnlockEffects(playerManager);

                    // If configured, disable the trigger to prevent it from firing again.
                    if (disableAfterUnlock)
                    {
                        _hasBeenTriggered = true; // Mark as triggered
                        // Disable the collider component so it can't be triggered again.
                        GetComponent<Collider>().enabled = false;
                        // Alternatively, you could deactivate the entire GameObject:
                        // gameObject.SetActive(false);
                        Debug.Log($"Trigger '{gameObject.name}' disabled after successful unlock.", this);
                    }
                }
                else
                {
                    // This logs if the player enters but the unlock index was already met or invalid.
                    // Debug.Log($"Player entered trigger '{gameObject.name}', but bullet index {bulletIndexToUnlock} was already unlocked or invalid. No change.", this);
                }
            }
            else
            {
                // This error means the object tagged "Player" doesn't have the PlayerStateManager script.
                Debug.LogError($"Object tagged 'Player' entered trigger '{gameObject.name}', but no PlayerStateManager component was found!", other.gameObject);
            }
        }
    }

    // Helper method to play optional feedback effects.
    void PlayUnlockEffects(PlayerStateManager player)
    {
        // Instantiate the particle effect prefab at the trigger's position if assigned.
        if (unlockEffectPrefab != null)
        {
            Instantiate(unlockEffectPrefab, transform.position, Quaternion.identity);
        }

        // Play the unlock sound using the player's AudioSource if assigned and available.
        if (unlockSound != null && player.audioSource != null)
        {
            // Use PlayOneShot on the player's source so it doesn't interrupt player sounds.
            player.audioSource.PlayOneShot(unlockSound);
        }
        else if (unlockSound != null)
        {
            // Log a warning if the sound is assigned but couldn't be played.
            Debug.LogWarning($"Unlock sound is assigned to '{gameObject.name}', but couldn't find a valid AudioSource on the player to play it.", this);
        }
    }
}
