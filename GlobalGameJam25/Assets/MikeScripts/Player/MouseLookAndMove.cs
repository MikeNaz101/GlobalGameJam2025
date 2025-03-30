using UnityEngine;

// Consider renaming this script to MouseLook or CameraController
public class MouseLookAndMove : MonoBehaviour
{
    [Tooltip("Sensitivity for mouse movement.")]
    public float mouseSensitivity = 100f;

    [Tooltip("Assign the main Player Body Transform (the parent object with PlayerStateManager).")]
    public Transform playerBody; // Reference to the player body (parent object)

    private float xRotation = 0f; // Stores the vertical rotation angle for the camera

    // No longer need reference to PlayerStateManager or CharacterController here
    // public PlayerStateManager player;
    // public CharacterController player;

    void Start()
    {
        // Lock and hide the cursor for a typical FPS feel
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerBody == null)
        {
            Debug.LogError("CRITICAL: Player Body Transform must be assigned in the Inspector for MouseLook script!", this.gameObject);
            // You might want to disable the script if the reference is missing
            // this.enabled = false;
        }

        // Assuming this script is on the Camera object, which should be a child of the Player Body.
        // If not, the rotation logic might need adjustment.
    }

    void Update()
    {
        // --- Mouse Look Logic ---

        // Get Mouse Input (using old Input Manager GetAxis)
        // Note: Your PlayerStateManager uses the new Input System. For consistency,
        // you might want to update this script to read mouse delta from an Input Action as well.
        // However, this will work if your project still uses the old Input Manager or mixes them.
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // --- Vertical Rotation (Pitch) ---
        // Rotate the Camera up/down based on mouseY.
        // This rotation is applied to the GameObject this script is attached to (presumably the Camera).
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Prevent looking straight up/down and flipping over
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f); // Apply rotation locally to the camera

        // --- Horizontal Rotation (Yaw) ---
        // Rotate the entire player body left/right based on mouseX.
        // This rotation is applied to the parent 'playerBody' transform.
        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * mouseX);
        }

        // --- MOVEMENT, JUMP, and DASH logic have been REMOVED from this script ---
        // Why? Because these actions are now handled by the PlayerStateManager and its
        // state machine (PlayerIdleState, PlayerWalkingState, PlayerRunningState,
        // PlayerFlyingState, PlayerHitState) based on Input System Actions
        // like OnMove, OnJump, OnRun, OnSprint defined in PlayerStateManager.
    }

    // --- FixedUpdate REMOVED ---
    // Movement is handled in PlayerStateManager's Update via CharacterController.Move, called by the current state.

    // --- Dash REMOVED ---
    // Dashing can be re-implemented later as an ability within the PlayerStateManager/State system if desired.
}