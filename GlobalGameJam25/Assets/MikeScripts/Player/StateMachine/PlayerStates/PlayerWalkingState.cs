using UnityEngine;

public class PlayerWalkingState : PlayerBaseState
{
    private float footstepTimer = 0f;
    private float timeBetweenFootsteps = 0.5f; // Adjust based on walk speed and preference

    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("Entering Walking State");
        footstepTimer = 0f; // Reset timer on entering state
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // --- Transitions ---
        if (player.movement.magnitude < 0.1f)
        {
            player.SwitchState(player.idleState);
            return;
        }
        // Transition to Run: Check shift key AND if player HAS STAMINA
        if (player.CanStartSprint)
        {
            player.SwitchState(player.runState);
            return;
        }
        if (player.isSneaking) // Check for sneak transition
        {
            player.SwitchState(player.sneakState);
            return;
        }

        // --- Movement (camera-relative; PlayerStateManager handles acceleration and facing) ---
        player.SetMoveSpeed(player.walkSpeed);

        // --- Footstep Sound ---
        footstepTimer -= Time.deltaTime;
        if (footstepTimer <= 0f)
        {
            player.PlaySoundOneShot(player.walkFootstepSound); // Play walk sound
            footstepTimer = timeBetweenFootsteps; // Reset timer
        }
        // --------------------
    }

    public override void ExitState(PlayerStateManager player)
    {
        // Optional: Reset horizontal velocity if needed, though the next state will likely set it.
        // player.currentHorizontalVelocity = Vector3.zero;
    }
}