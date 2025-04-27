using UnityEngine;
// using UnityEngine.InputSystem; // Might not be needed here if StateManager handles input values

public class PlayerIdleState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("Entering Idle State");
        // Maybe ensure player velocity is low here?
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // Set horizontal velocity to zero when Idle
        player.currentHorizontalVelocity = Vector3.zero;
        // Apply gravity only (MovePlayer(0) effectively does this if it includes verticalVelocity)
        // Or rely on PlayerStateManager.Update to handle gravity while idle

        // Check for movement input to transition
        if (player.movement.magnitude > 0.1f)
        {
            // Prioritize Run > Sneak > Walk
            if (player.isRunning && !player.isSneaking) // Run if holding run AND NOT sneaking
            {
                player.SwitchState(player.runState);
            }
            else if (player.isSneaking) // Sneak if sneak is toggled (even if holding run maybe?)
            {
                player.SwitchState(player.sneakState);
            }
            else // Walk otherwise
            {
                player.SwitchState(player.walkState);
            }
        }
         // --- Add Jump Check ---
         // Assuming OnJump sets a flag or directly calls Jump() which might switch state
         // if (Input.GetButtonDown("Jump") && player.isGrounded) { // Example check
         //    player.Jump(); // Jump might implicitly or explicitly switch state later
         // }

         // --- Add Falling Check ---
         // if (!player.isGrounded && player.verticalVelocity < -0.1f) { // Small threshold
              // If we add a FallingState:
              // player.SwitchState(player.fallingState);
         // }
    }

     public override void ExitState(PlayerStateManager player) { } // Optional Exit logic
}