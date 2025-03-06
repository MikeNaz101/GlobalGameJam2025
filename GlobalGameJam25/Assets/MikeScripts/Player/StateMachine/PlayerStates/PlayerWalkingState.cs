using UnityEngine;

public class PlayerWalkingState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("im WALKING!!!!!!!!!");
        player.MovePlayer(player.default_speed);
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // What are we doing in this state
        player.MovePlayer(player.default_speed);

        if (player.movement.magnitude < 0.1)
        {
            player.SwitchState(player.idleState);
        }
        else if (player.isSneaking)
        {
            player.SwitchState(player.sneakState);
        }
    }
}
