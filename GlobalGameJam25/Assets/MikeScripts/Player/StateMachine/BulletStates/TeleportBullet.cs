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
         Destroy(gameObject, 10f); // Destroy after 10 seconds if nothing hit
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
        // Check if teleport is allowed and player reference is valid
        if (_canTeleport && _player != null)
        {
             Debug.Log($"Teleporting player to {targetPosition}");

             // Use CharacterController.Move for teleportation if player has one,
             // as directly setting transform.position can cause issues with controllers.
             if (_player.controller != null) {
                 // Disable controller temporarily to prevent conflicts when setting position?
                 // _player.controller.enabled = false;
                 // _player.transform.position = targetPosition;
                 // _player.controller.enabled = true;

                 // Alternatively, try moving over one frame (less reliable for instant teleport)
                 // Vector3 moveVector = targetPosition - _player.transform.position;
                 // _player.controller.Move(moveVector);

                 // Safest might be direct transform setting, IF you handle potential physics overlaps/issues.
                 // Add a small vertical offset to prevent falling through floor?
                  _player.transform.position = targetPosition + Vector3.up * 0.1f;
                   _player.verticalVelocity = 0; // Reset fall speed after teleport
             }
             else {
                 // Fallback if no character controller
                 _player.transform.position = targetPosition + Vector3.up * 0.1f; ;
             }


            _canTeleport = false; // Ensure teleportation only happens once per bullet
        }
         else if (_player == null) {
             Debug.LogWarning("Teleport failed: Player reference missing.");
         }
    }
}