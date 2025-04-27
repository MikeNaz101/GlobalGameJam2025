using UnityEngine;

public class PlayerWalkingState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("Entering Walking State");
    }

    public override void UpdateState(PlayerStateManager player)
    {
        if (player.movement.magnitude < 0.1f) {
            player.SwitchState(player.idleState);
            return; // Exit early after state switch
        }
        if (player.isRunning && !player.isSneaking) {
            player.SwitchState(player.runState);
            return;
        }
        // --- Calculate Character Body-Relative Movement ---

        // 1. Get Input
        Vector2 input = player.movement; // e.g., (0, 1) for forward, (1, 0) for right

        // 2. Get Character's Local Directions
        // Get the forward/right vectors directly from the player's transform
        // (which is being rotated by PlayerBodyRotator)
        Vector3 bodyForward = player.transform.forward; // Player's current forward direction (Blue Axis)
        Vector3 bodyRight = player.transform.right;   // Player's current right direction (Red Axis)

        // Note: For standard CharacterController setups that don't tilt the main object,
        // these vectors are usually already horizontal (Y=0). If your main player object *can* tilt,
        // and you want purely horizontal ground movement, you might flatten these:
        // bodyForward.y = 0; bodyRight.y = 0; bodyForward.Normalize(); bodyRight.Normalize();
        // However, start without flattening first.

        // 3. Calculate World-Space Move Direction based on Body Axes and Input
        // Combine local directions scaled by input
        Vector3 desiredMoveDirection = (bodyForward * input.y) + (bodyRight * input.x);

        // 4. Set Horizontal Velocity for the State Manager
        // No need to normalize desiredMoveDirection if input magnitude handles partial movement (like gamepad stick)
        player.currentHorizontalVelocity = desiredMoveDirection * player.walkSpeed;

        // 5. Rotation is handled entirely by PlayerBodyRotator based on Mouse X.
        // The movement direction automatically follows the body's rotation.
        // No specific rotation logic is needed here.
        
    }


     public override void ExitState(PlayerStateManager player) { }
}