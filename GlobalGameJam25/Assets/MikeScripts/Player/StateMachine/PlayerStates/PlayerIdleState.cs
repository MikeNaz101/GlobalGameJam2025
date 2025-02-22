using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerIdleState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("is'm Idling!");
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // What are we doing in this state
        if (player.movement.magnitude > 0.1)
        {
            if(player.isSneaking)
            {
                player.SwitchState(player.sneakState);
            }
            else
            {
                player.SwitchState(player.walkState);
            }
        }


        // When 

    }
}
