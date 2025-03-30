using UnityEngine;

public class PlayerSneakState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("Entering Sneaking State");
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // --- Calculate Movement Direction ---
        // Get the desired direction based on input (player.movement)
        // relative to the player's current orientation (transform.right/forward)
        Vector3 moveDirection = (player.transform.right * player.movement.x) + (player.transform.forward * player.movement.y);

        // --- Call MovePlayer with Direction and Speed ---
        // Pass BOTH the direction and the specific speed for sneaking
        player.MovePlayer(moveDirection, player.sneakSpeed);

        // --- Check for transitions ---
        if (player.movement.magnitude < 0.1f) // Transition to Idle if stopping
        {
            player.SwitchState(player.idleState);
        }
        else if (!player.isSneaking) // If sneak is toggled off...
        {
            // Check if Run is held, otherwise go to Walk
            if (player.isRunning)
            {
                player.SwitchState(player.runState);
            }
            else
            {
                player.SwitchState(player.walkState);
            }
        }

        // TODO: Add Jump Check based on input flag/event (maybe a quieter jump?)
        // TODO: Add Falling Check based on !player.isGrounded
    }

     public override void ExitState(PlayerStateManager player) { }
}