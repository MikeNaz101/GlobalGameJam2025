using UnityEngine;

public class PlayerWalkingState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("Entering Walking State");
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // --- Calculate Movement Direction ---
        // Get the desired direction based on input (player.movement)
        // relative to the player's current orientation (transform.right/forward)
        Vector3 moveDirection = (player.transform.right * player.movement.x) + (player.transform.forward * player.movement.y);

        // --- Call MovePlayer with Direction and Speed ---
        // Now pass BOTH the direction and the specific speed for walking
        player.MovePlayer(moveDirection, player.walkSpeed);

        // --- Check for transitions ---
        if (player.movement.magnitude < 0.1f && player.isGrounded) // Transition to Idle if stopping
        {
            player.SwitchState(player.idleState);
        }
        // Prioritize Run > Sneak
        else if (player.isRunning && !player.isSneaking) // Transition to Run if holding Run and not sneaking
        {
            player.SwitchState(player.runState);
        }
        else if (player.isSneaking) // Transition to Sneak if sneak is toggled
        {
            player.SwitchState(player.sneakState);
        }

        // TODO: Add Jump Check based on input flag/event
        // TODO: Add Falling Check based on !player.isGrounded
    }

     public override void ExitState(PlayerStateManager player) { }
}