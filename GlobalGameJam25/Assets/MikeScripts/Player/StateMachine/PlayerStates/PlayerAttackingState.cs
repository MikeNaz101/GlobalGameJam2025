using UnityEngine;

public class PlayerAttackingState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("im Moving!");
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
    public void OnTriggerEnter(Collider other)
    {
        throw new System.NotImplementedException();
    }

    public void OnTriggerExit(Collider other)
    {
        throw new System.NotImplementedException();
    }

    public void OnTriggerStay(Collider other)
    {
        throw new System.NotImplementedException();
    }


}
