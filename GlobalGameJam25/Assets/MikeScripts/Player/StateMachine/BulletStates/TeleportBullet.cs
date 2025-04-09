using UnityEngine;

public class TeleportBullet : MonoBehaviour
{
    private PlayerStateManager _player;
    private bool _canTeleport = true; // Flag to ensure teleport happens only once
    private Transform bulletTransform;
    // private float speed; // This wasn't used, removed

    void Start()
    {
        Debug.Log("Teleport bullet fired!");
        bulletTransform = transform; // Cache transform

        // Find Player - Make sure Player GameObject has the "Player" tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
             _player = playerObj.GetComponent<PlayerStateManager>();
             if (_player == null) {
                  Debug.LogError("TeleportBullet found Player object, but it lacks PlayerStateManager component!");
             }
        } else {
             Debug.LogError("TeleportBullet could not find GameObject with tag 'Player'.");
        }

        // Optional: Set a lifetime for the bullet in case it never hits anything
         Destroy(gameObject, 30f); // Destroy after 10 seconds if nothing hit
    }

    // Using OnCollisionEnter
    void OnCollisionEnter(Collision collision)
    {
         // Don't teleport if hitting the player immediately after firing
         if (collision.gameObject.CompareTag("Player"))
         {
             // Could potentially ignore collision for a brief moment after firing
             return;
         }

         // Teleport on hitting anything else (or specific layers if needed)
        OnHit(collision.gameObject, collision.contacts[0].point); // Pass hit point

         // Optional: Impact effect
    }

     // Using OnTriggerEnter if collider is trigger
     /*
     void OnTriggerEnter(Collider other) {
          if (other.CompareTag("Player")) return;
          // Use other.ClosestPoint(transform.position) or collider bounds center as teleport point
          Vector3 hitPoint = other.ClosestPoint(transform.position);
          OnHit(other.gameObject, hitPoint);
     }
     */


    // Renamed parameter for clarity and added hitPoint
    protected void OnHit(GameObject hitObject, Vector3 hitPoint)
    {
        // Attempt teleportation
        Teleport(hitPoint);

        // Destroy the bullet regardless of teleport success after the attempt
        Destroy(gameObject);
    }

    // Teleports the player to the specified location
    public void Teleport(Vector3 targetPosition)
    {
         // Use the more detailed logs from the previous example for better diagnostics
         Debug.Log($"Teleport method called. Target: {targetPosition}. CanTeleport: {_canTeleport}, Player Null: {_player == null}, Controller Null: {(_player != null ? (_player.controller == null).ToString() : "N/A")}");

        // Check if teleport is allowed and player reference is valid
        if (_canTeleport && _player != null)
        {
            // Increased offset slightly, adjust as needed
            Vector3 finalTeleportPosition = targetPosition + Vector3.up * 0.15f;

            Debug.Log($"Attempting teleportation. Player Position BEFORE: {_player.transform.position}");

            // --- CharacterController Handling ---
            // Check if the PlayerStateManager has a valid controller reference
            if (_player.controller != null)
            {
                Debug.Log("<color=green>Using CharacterController disable/enable method via PlayerStateManager.</color>");

                // --- UNCOMMENT THESE THREE LINES ---
                _player.controller.enabled = false;   // Disable the controller
                _player.transform.position = finalTeleportPosition; // Set position *while* disabled
                _player.controller.enabled = true;    // Re-enable the controller
                // --- ---

                // --- COMMENT OUT OR DELETE THIS LINE (as it conflicts) ---
                // _player.transform.position = targetPosition + Vector3.up * 0.1f;
                // --- ---

            }
            // --- Fallback: Direct Transform Setting ---
            else // This runs if _player.controller is null
            {
                 Debug.Log("<color=orange>Using direct transform.position setting (No CharacterController found via PlayerStateManager).</color>");
                // Fallback if no character controller reference found on PlayerStateManager
                _player.transform.position = finalTeleportPosition;
            }
            // --- End Teleport Method ---


            // Reset vertical velocity in PlayerStateManager (if applicable)
             if (_player.verticalVelocity != 0) {
                 Debug.Log($"Resetting player vertical velocity from {_player.verticalVelocity} to 0.");
                 _player.verticalVelocity = 0;
             }


            Debug.Log($"Player Position AFTER teleport attempt: {_player.transform.position}");

            _canTeleport = false; // Ensure teleportation only happens once per bullet
        }
        else if (_player == null) {
            Debug.LogWarning("Teleport failed: Player reference missing.");
        }
         else if (!_canTeleport) {
             Debug.LogWarning("Teleport failed: _canTeleport flag was already false.");
         }
    }
}