using UnityEngine;

// Implement the interface defined in PlayerShooting
public class TeleportBullet : MonoBehaviour, PlayerShooting.IBulletChargeReceiver
{
    [Header("Config")]
    [Tooltip("How high above the impact point the player appears.")]
    public float teleportVerticalOffset = 0.15f;
    [Tooltip("How long the bullet exists before disappearing if it hits nothing.")]
    public float lifetime = 10f; // Reduced lifetime from original example

    // --- Private Variables ---
    private PlayerStateManager _player; // Reference set by PlayerShooting via OnFire
    private bool _canTeleport = true;   // Flag to ensure teleport happens only once
    private bool initialized = false;   // Tracks if OnFire has been called

    // Start() is now minimal, initialization happens in OnFire
    void Start()
    {
        // Debug.Log("Teleport bullet GameObject instantiated.");
        // Player finding logic is removed from here.
    }

    // --- IBulletChargeReceiver Implementation ---
    // This method is called by PlayerShooting exactly ONCE when the bullet is fired.
    public void OnFire(float chargeRatio, float forceMultiplier, PlayerStateManager playerRef)
    {
        if (initialized) return; // Prevent multiple initializations

        Debug.Log($"TeleportBullet Initialized via OnFire. ChargeRatio: {chargeRatio:P0}");

        // 1. Store Player Reference
        _player = playerRef;
        if (_player == null)
        {
            Debug.LogError("TeleportBullet received null Player reference in OnFire! Teleport will fail.", this);
            // Destroy immediately if player ref is essential for any visual effect
            // Destroy(gameObject);
            // return;
        }

        // 2. Set Lifetime Timer
        Destroy(gameObject, lifetime);

        initialized = true; // Mark as initialized
        _canTeleport = true; // Ensure teleport flag is ready
    }
    // --- End of Interface Implementation ---


    // --- Collision/Trigger Handling ---

    // Using OnCollisionEnter (ensure bullet collider IsTrigger is FALSE)
    void OnCollisionEnter(Collision collision)
    {
        // Don't process collision if not initialized or already teleported
        if (!initialized || !_canTeleport) return;

        // Ignore hitting the player immediately after firing (or always)
        if (collision.gameObject.CompareTag("Player"))
        {
            return;
        }

        // Ignore hitting other bullets or specific enemy types if needed
        if (collision.gameObject.CompareTag("EnemyProjectile") || collision.gameObject.CompareTag("Enemy")) // Example tags
        {
             // Optionally destroy self without teleporting
             // Destroy(gameObject);
             return;
        }


        // Teleport on hitting anything else (or specific layers if needed)
        // Pass the first contact point for accurate teleport location
        OnHit(collision.gameObject, collision.contacts[0].point);
    }

    /*
    // --- OR --- Use OnTriggerEnter (ensure bullet collider IsTrigger is TRUE)
    void OnTriggerEnter(Collider other)
    {
        if (!initialized || !_canTeleport) return;
        if (other.CompareTag("Player")) return;
        if (other.CompareTag("EnemyProjectile") || other.CompareTag("Enemy")) return; // Example tags

        // Use other.ClosestPoint for approximate impact location with triggers
        Vector3 hitPoint = other.ClosestPoint(transform.position);
        OnHit(other.gameObject, hitPoint);
    }
    */


    // Central handler for impact logic
    protected void OnHit(GameObject hitObject, Vector3 hitPoint)
    {
        Debug.Log($"TeleportBullet hit '{hitObject.name}' at {hitPoint}. Attempting teleport.");

        // Attempt teleportation using the impact point
        Teleport(hitPoint);

        // Destroy the bullet AFTER attempting teleport
        // (Teleport might fail, but we still want the bullet gone)
        Destroy(gameObject);
    }


    // Teleports the player to the specified location
    public void Teleport(Vector3 targetPosition)
    {
        // Use the more detailed logs from the previous example for better diagnostics
        Debug.Log($"Teleport method called. Target: {targetPosition}. CanTeleport: {_canTeleport}, Player Null: {_player == null}, Initialized: {initialized}");

        // Check if teleport is allowed, player reference is valid, and initialized
        if (_canTeleport && initialized && _player != null)
        {
            // Calculate final position with offset
            Vector3 finalTeleportPosition = targetPosition + Vector3.up * teleportVerticalOffset;

            Debug.Log($"Attempting teleportation. Player Position BEFORE: {_player.transform.position}");

            // --- CharacterController Handling ---
            if (_player.controller != null)
            {
                Debug.Log("<color=green>Using CharacterController disable/enable method.</color>");
                _player.controller.enabled = false;   // Disable the controller
                _player.transform.position = finalTeleportPosition; // Set position *while* disabled
                _player.controller.enabled = true;    // Re-enable the controller

                // --- Animation Trigger ---
                _player.TriggerTeleportAnimation(); // Call the method on PlayerStateManager
            }
            // --- Fallback: Direct Transform Setting ---
            else
            {
                Debug.Log("<color=orange>Using direct transform.position setting (No CharacterController found via PlayerStateManager).</color>");
                _player.transform.position = finalTeleportPosition;
                 // Trigger animation even if controller is missing? Maybe.
                 _player.TriggerTeleportAnimation();
            }
            // --- End Teleport Method ---


            // Reset vertical velocity in PlayerStateManager to prevent falling through floor immediately
            if (_player.verticalVelocity != 0) {
                Debug.Log($"Resetting player vertical velocity from {_player.verticalVelocity} to 0.");
                _player.verticalVelocity = 0;
            }

            Debug.Log($"Player Position AFTER teleport attempt: {_player.transform.position}");

            _canTeleport = false; // Ensure teleportation only happens once per bullet
        }
        // --- Log reasons for failure ---
        // Corrected the variable name in the first else if condition below
        else if (!initialized) { // <<< CORRECTED from !_initialized
             Debug.LogWarning("Teleport failed: Bullet was not initialized via OnFire.");
        }
        else if (_player == null) {
            Debug.LogWarning("Teleport failed: Player reference missing (was null during OnFire or lost).");
        }
        else if (!_canTeleport) {
            Debug.LogWarning("Teleport failed: _canTeleport flag was already false (already teleported?).");
        }
    }
}