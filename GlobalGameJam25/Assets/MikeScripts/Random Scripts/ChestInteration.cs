using UnityEngine;
using System.Collections; // Required for Coroutines

public class ChestInteraction : MonoBehaviour
{
    [Header("Chest Settings")]
    [Tooltip("The Transform of the chest lid object that will rotate.")]
    public Transform lidTransform;

    [Tooltip("The axis around which the lid will rotate.")]
    public Vector3 rotationAxis = Vector3.right; 

    [Tooltip("The angle in degrees the lid should open.")]
    public float openAngle = 90.0f;

    [Tooltip("How long the opening animation should take in seconds.")]
    public float openDuration = 1.0f;

    [Tooltip("Delay in seconds before the chest disappears after opening.")]
    public float destroyDelay = 2.0f;

    [Header("Interaction")]
    [Tooltip("The key the player needs to press to open the chest.")]
    public KeyCode interactionKey = KeyCode.E;

    [Header("Effects & Rewards")]
    [Tooltip("The AudioSource component to play the sound from. If empty, will try to find one on this GameObject.")]
    public AudioSource audioSource;

    [Tooltip("The sound effect to play when the chest opens.")]
    public AudioClip openSound;

    [Tooltip("The Particle System to activate when the chest opens.")]
    public ParticleSystem particles;

    [Tooltip("The amount of XP to grant the player.")]
    public int xpAmount = 50;

    // --- Private Variables ---
    private bool playerInRange = false;
    private bool isOpen = false;
    private PlayerStateManager playerStateManager; // Reference to the player's state manager script

    void Awake()
    {
        // Attempt to get AudioSource if not assigned
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // Basic validation
        if (lidTransform == null)
        {
            Debug.LogError("ChestInteraction: Lid Transform is not assigned!", this);
        }
        if (particles == null)
        {
            Debug.LogWarning("ChestInteraction: Particle System is not assigned.", this);
        }
         if (audioSource == null && openSound != null)
        {
            Debug.LogWarning("ChestInteraction: Open Sound is assigned, but no AudioSource found or assigned. Adding one.", this);
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void Update()
    {
        // Check if the player is in range, the chest isn't already open, and the interaction key is pressed
        // CHANGED: Check against playerStateManager reference
        if (playerInRange && !isOpen && playerStateManager != null && Input.GetKeyDown(interactionKey))
        {
            OpenChest();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if the object entering the trigger is the player and the chest is not open
        if (!isOpen && other.CompareTag("Player"))
        {
            // CHANGED: Try to get the PlayerStateManager component
            playerStateManager = other.GetComponent<PlayerStateManager>();
            if (playerStateManager != null)
            {
                playerInRange = true;
                 // Optional: Display UI prompt here (e.g., "Press E to open")
                Debug.Log("Player entered chest range. Press E to open.");
            }
            else
            {
                 // Warning if the component is missing on the tagged player object
                 Debug.LogWarning("ChestInteraction: Object tagged 'Player' entered range, but no PlayerStateManager script found.", other.gameObject);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Check if the object leaving the trigger is the player
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            playerStateManager = null; // Clear the reference when player leaves
            // Optional: Hide UI prompt here
             Debug.Log("Player exited chest range.");
        }
    }

    void OpenChest()
    {
        if (isOpen) return; // Don't open if already open

        isOpen = true;
        playerInRange = false; // Prevent re-interaction after opening starts
        Debug.Log("Opening Chest!");

        // 1. Start Lid Rotation Animation (using a Coroutine for smoothness)
        if (lidTransform != null)
        {
            StartCoroutine(RotateLid());
        }

        // 2. Play Sound
        if (audioSource != null && openSound != null)
        {
            audioSource.PlayOneShot(openSound);
        }

        // 3. Activate Particles
        if (particles != null)
        {
            particles.Play();
        }

        // 4. Add XP to Player
        // CHANGED: Check playerStateManager reference and call GainXP method
        if (playerStateManager != null)
        {
            playerStateManager.GainXP(xpAmount); // Use GainXP method
            Debug.Log($"Player gained {xpAmount} XP.");
        }
        else
        {
             Debug.LogWarning("ChestInteraction: Could not give XP because PlayerStateManager reference was lost or not found.", this);
        }

        // 5. Schedule Chest Destruction
        Destroy(gameObject, destroyDelay);
    }

    IEnumerator RotateLid()
    {
        Quaternion startRotation = lidTransform.localRotation;
        // Calculate target rotation by rotating start rotation by openAngle around the rotationAxis
        Quaternion targetRotation = startRotation * Quaternion.Euler(rotationAxis * openAngle);
        float elapsedTime = 0f;

        while (elapsedTime < openDuration)
        {
            // Smoothly interpolate between start and target rotation
            lidTransform.localRotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime / openDuration);
            elapsedTime += Time.deltaTime;
            yield return null; // Wait for the next frame
        }

        // Ensure the final rotation is exactly the target rotation
        lidTransform.localRotation = targetRotation;
        Debug.Log("Chest lid finished opening.");
    }
}