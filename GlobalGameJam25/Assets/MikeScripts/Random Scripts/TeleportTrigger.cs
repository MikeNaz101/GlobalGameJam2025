using UnityEngine;
using System.Collections; // Required for Coroutine

public class TeleportTrigger : MonoBehaviour
{
    [Header("Teleport Settings")]
    [Tooltip("The point from which the player will be pushed away.")]
    public Transform centerPoint; // Assign an empty GameObject's transform here

    [Tooltip("How far away from the center point (relative to entry) the player is pushed horizontally.")]
    public float pushDistance = 10.0f;

    [Tooltip("How high the player is teleported.")]
    public float pushHeight = 10.0f;

    [Header("Player Effects")]
    [Tooltip("The grunt sound to play when teleported.")]
    public AudioClip gruntSound;

    [Tooltip("The name of the trigger parameter in the Player's Animator Controller for the falling animation.")]
    public string fallingAnimationTrigger = "teleported"; // IMPORTANT: Change if your trigger name is different

    private Collider triggerCollider; // Reference to this object's collider

    void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            Debug.LogError("TeleportTrigger requires a Collider component on the same GameObject.", this);
            enabled = false; // Disable script if no collider
            return;
        }
        if (!triggerCollider.isTrigger)
        {
            Debug.LogWarning("TeleportTrigger: The attached Collider is not set to 'Is Trigger'. It might not function as expected.", this);
        }

        if (centerPoint == null)
        {
            Debug.LogError("TeleportTrigger requires the 'Center Point' Transform to be assigned.", this);
            enabled = false; // Disable script if center point is missing
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if the object entering is the Player
        if (other.CompareTag("Player"))
        {
            // Try to get necessary components from the Player object
            PlayerStateManager playerManager = other.GetComponent<PlayerStateManager>();
            CharacterController playerController = other.GetComponent<CharacterController>();
            PlayerAnimationManager playerAnimationManager = other.GetComponent<PlayerAnimationManager>(); // Get the animation manager

            // Check if all required components were found
            if (playerManager != null && playerController != null && playerAnimationManager != null && centerPoint != null)
            {
                Debug.Log("Player entered teleport trigger. Initiating teleport...");
                // Use a coroutine to handle the teleport and controller disable/enable
                StartCoroutine(TeleportPlayer(other.transform, playerController, playerManager, playerAnimationManager));
            }
            else
            {
                Debug.LogError($"TeleportTrigger: Player entered, but couldn't find all required components! " +
                               $"PlayerManager Found: {playerManager != null}, " +
                               $"Controller Found: {playerController != null}, " +
                               $"AnimationManager Found: {playerAnimationManager != null}, " +
                               $"CenterPoint Assigned: {centerPoint != null}", other.gameObject);
            }
        }
    }

    IEnumerator TeleportPlayer(Transform playerTransform, CharacterController playerController, PlayerStateManager playerManager, PlayerAnimationManager playerAnimationManager)
    {
        // --- Calculate Teleport Destination ---

        // 1. Get the direction vector pointing FROM the center point TO the player's current position
        Vector3 directionFromCenter = playerTransform.position - centerPoint.position;

        // 2. Ignore the vertical difference for the horizontal push direction
        directionFromCenter.y = 0;

        // 3. Normalize the direction vector. Handle case where player is exactly at center horizontally.
        Vector3 pushDirection;
        if (directionFromCenter.sqrMagnitude < 0.01f) // If player is very close to the center
        {
             Debug.LogWarning("Player entered teleport trigger very close to center point. Pushing forward.", this);
             pushDirection = playerTransform.forward; // Push in player's forward direction as a fallback
        }
        else
        {
             pushDirection = directionFromCenter.normalized;
        }


        // 4. Calculate the target position
        Vector3 horizontalPush = pushDirection * pushDistance;
        Vector3 verticalPush = Vector3.up * pushHeight;
        // Target is current position + horizontal push + vertical push
        Vector3 targetPosition = playerTransform.position + horizontalPush + verticalPush;

        Debug.Log($"Teleporting player from {playerTransform.position} to {targetPosition}");

        // --- Execute Effects and Teleport ---

        // 1. Play Grunt Sound (using the Player's AudioSource via PlayerStateManager)
        if (gruntSound != null && playerManager.audioSource != null) // Use the AudioSource reference from PlayerStateManager
        {
            playerManager.PlaySoundOneShot(gruntSound); // Call the helper method on PlayerStateManager
        }
        else if(gruntSound == null)
        {
             Debug.LogWarning("TeleportTrigger: Grunt Sound is not assigned.", this);
        }
        else if(playerManager.audioSource == null)
        {
             Debug.LogWarning("TeleportTrigger: PlayerStateManager does not have an assigned AudioSource.", playerManager);
        }


        // 2. Play Falling Animation
        // IMPORTANT: Assumes PlayerAnimationManager has a method matching the string name.
        // A direct method call like 'playerAnimationManager.TriggerFall()' might be safer if available.
        if (!string.IsNullOrEmpty(fallingAnimationTrigger))
        {
             // Option 1: Using Animator directly if PlayerAnimationManager exposes it
             // if (playerAnimationManager.animator != null) // Assuming PlayerAnimationManager has 'public Animator animator;'
             // {
             //     playerAnimationManager.animator.SetTrigger(fallingAnimationTrigger);
             // }
             // Option 2: Calling a dedicated method on PlayerAnimationManager (Preferred)
              try
              {
                  // Example if you have a method like this in PlayerAnimationManager:
                  // public void TriggerFallingDown() { animator.SetTrigger("TriggerFallingDown"); }
                  playerAnimationManager.SendMessage(fallingAnimationTrigger, SendMessageOptions.DontRequireReceiver); // Less safe, uses string name
                  // Replace SendMessage with a direct call if possible:
                  // playerAnimationManager.TriggerFallingDownMethod(); // <-- Change to your actual method name
              }
              catch (System.Exception e)
              {
                 Debug.LogError($"TeleportTrigger: Failed to trigger animation '{fallingAnimationTrigger}'. Does the method/trigger exist? Error: {e.Message}", playerAnimationManager);
              }
        }
        else
        {
             Debug.LogWarning("TeleportTrigger: Falling Animation Trigger name is empty.", this);
        }


        // 3. Disable CharacterController (NECESSARY for direct transform manipulation)
        playerController.enabled = false;

        // 4. Set the player's position
        playerTransform.position = targetPosition;

        // 5. Wait a very short moment (e.g., end of frame) BEFORE re-enabling the controller.
        // This helps ensure the physics engine registers the position change properly.
        yield return new WaitForEndOfFrame(); // Or yield return null;

        // 6. Re-enable CharacterController
        playerController.enabled = true;

        Debug.Log("Player teleport complete. Controller re-enabled.");

        // Optional: Disable this trigger temporarily to prevent immediate re-triggering?
        // triggerCollider.enabled = false;
        // StartCoroutine(ReEnableTriggerAfterDelay(1.0f)); // Example delay
    }

    // Optional: Coroutine to re-enable trigger after a delay
    // IEnumerator ReEnableTriggerAfterDelay(float delay)
    // {
    //     yield return new WaitForSeconds(delay);
    //     if (triggerCollider != null) triggerCollider.enabled = true;
    // }
}
