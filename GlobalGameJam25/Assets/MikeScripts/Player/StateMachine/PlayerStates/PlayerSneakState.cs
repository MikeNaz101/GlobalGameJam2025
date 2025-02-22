using UnityEngine;

public class PlayerSneakState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("I is Sneaking!");
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // What are we doing in this state
        player.MovePlayer(player.default_speed/2);

        if (player.movement.magnitude < 0.1)
        {
            player.SwitchState(player.idleState);
        }
        else if(!player.isSneaking)
        {
            player.SwitchState(player.walkState);
        }
    }
}
