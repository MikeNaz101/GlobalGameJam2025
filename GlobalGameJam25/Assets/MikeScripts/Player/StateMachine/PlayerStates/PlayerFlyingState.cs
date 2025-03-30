using UnityEngine;

public class PlayerFlyingState : PlayerBaseState
{
    // Wing object references
    private GameObject rWingInstance;
    private GameObject lWingInstance;

    // Consider moving speeds to PlayerStateManager if consistent access is needed elsewhere
    // public float flySpeed = 15f; // Now defined in PlayerStateManager
    // public float flapStrength = 10f; // Now defined in PlayerStateManager

    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("Entering Flying State");

        // --- Enable/Instantiate Wings ---
        // Option A: Instantiate new wings
        if (player.rWingPrefab != null && player.rWingPoint != null)
            rWingInstance = GameObject.Instantiate(player.rWingPrefab, player.rWingPoint.position, player.rWingPoint.rotation, player.rWingPoint);
        if (player.lWingPrefab != null && player.lWingPoint != null)
            lWingInstance = GameObject.Instantiate(player.lWingPrefab, player.lWingPoint.position, player.lWingPoint.rotation, player.lWingPoint);

        // Option B: Enable existing wings (if they are permanent child objects)
        // player.rWingPoint.gameObject.SetActive(true);
        // player.lWingPoint.gameObject.SetActive(true);

        // --- Initial Flight Settings ---
        // Give a small upward boost when entering flight?
        player.verticalVelocity = player.flapStrength / 2f; // Example: half flap strength boost
        player.isGrounded = false; // Ensure player is marked as airborne

        // Optional: Set player orientation (e.g., slightly pitched forward?)
        // Be careful if this fights camera controls.
        // player.transform.rotation = Quaternion.Euler(10f, player.transform.eulerAngles.y, 0);
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // --- Input Handling for Flying ---

        // 1. Flapping (Vertical Boost)
        // Check the flag set by PlayerStateManager's OnJump handler
        if (player.jumpInputPressedThisFrame)
        {
            player.verticalVelocity = player.flapStrength; // Apply flap strength as vertical velocity
            FlapWingsAnimation(); // Trigger visual/sound feedback
        }

        // 2. Directional Movement (Horizontal Plane relative to player)
        // Get movement input from PlayerStateManager
        Vector3 moveInputDirection = (player.transform.right * player.movement.x) + (player.transform.forward * player.movement.y);

        // --- Call MovePlayer ---
        // Pass the calculated horizontal direction and fly speed.
        // PlayerStateManager handles combining this with verticalVelocity and gravity.
        player.MovePlayer(moveInputDirection, player.flySpeed);

        // --- Landing Check ---
        // Use the consistent CheckGrounded method if possible
        if (player.isGrounded) // Or player.CheckGrounded()
        {
            Debug.Log("Landed from flying.");
            // Transition back to a grounded state
            if (player.movement.magnitude > 0.1f) {
                 player.SwitchState(player.walkState); // Land into walking if moving
            } else {
                 player.SwitchState(player.idleState); // Land into idle if stopped
            }
        }
    }

    private void FlapWingsAnimation()
    {
        // Add logic for wing animation (Animator trigger, etc.) or sound effect
        Debug.Log("Flap!");
        // Example: rWingInstance?.GetComponent<Animator>()?.SetTrigger("Flap");
        // Example: lWingInstance?.GetComponent<Animator>()?.SetTrigger("Flap");
    }

    public override void ExitState(PlayerStateManager player)
    {
        Debug.Log("Exiting Flying State");

        // --- Disable/Destroy Wings ---
        // Option A: Destroy instantiated wings
        if (rWingInstance != null) GameObject.Destroy(rWingInstance);
        if (lWingInstance != null) GameObject.Destroy(lWingInstance);

        // Option B: Disable existing wings
        // player.rWingPoint.gameObject.SetActive(false);
        // player.lWingPoint.gameObject.SetActive(false);

        // Reset orientation if it was changed on entry?
        // player.transform.rotation = Quaternion.Euler(0, player.transform.eulerAngles.y, 0);
    }
}