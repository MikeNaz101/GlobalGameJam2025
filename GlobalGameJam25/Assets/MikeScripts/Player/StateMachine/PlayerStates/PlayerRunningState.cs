using UnityEngine;

public class PlayerRunningState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("Entering Running State");
        // Optional: Start consuming stamina here if you have a stamina system
        // player.StartCoroutine(player.ConsumeStamina(player.runStaminaCost));
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // --- Movement ---
        // Calculate movement direction based on input
        Vector3 moveDirection = (player.transform.right * player.movement.x) + (player.transform.forward * player.movement.y);
        // Call MovePlayer with the specific runSpeed
        player.MovePlayer(moveDirection, player.runSpeed);

        // Optional: Continue consuming stamina while in this state
        // if (player.currentStamina <= 0) { player.SwitchState(player.walkState); } // Example: Force walk if out of stamina

        // --- Transitions ---
        // 1. Stop Moving -> Idle
        if (player.movement.magnitude < 0.1f)
        {
            player.SwitchState(player.idleState);
        }
        // 2. Sneak Toggled On -> Sneak (Sneak overrides Run?)
        else if (player.isSneaking)
        {
            player.SwitchState(player.sneakState);
        }
        // 3. Run Key Released -> Walk
        else if (!player.isRunning)
        {
            player.SwitchState(player.walkState);
        }

        // --- Actions ---
        // Check for Jump Input (potentially a different jump height/distance while running?)
        // if (player.jumpInputPressedThisFrame && player.isGrounded) { player.Jump(); }

        // --- Ground Check ---
        // Check for Falling (if player runs off an edge)
        // if (!player.isGrounded && player.verticalVelocity < -0.1f) {
             // Switch to a Falling state if you implement one
             // player.SwitchState(player.fallingState);
        // }
    }

    public override void ExitState(PlayerStateManager player)
    {
        // Optional: Stop consuming stamina when exiting the run state
        // player.StopCoroutine("ConsumeStamina"); // Or use a Coroutine reference
        // Debug.Log("Exiting Running State");
    }
}