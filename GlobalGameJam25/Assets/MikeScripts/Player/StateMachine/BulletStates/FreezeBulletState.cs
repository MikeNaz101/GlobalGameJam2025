using UnityEngine;
using UnityEngine.InputSystem;

public class FreezeBulletState : BulletBaseState
{
    public override void EnterState(BulletStateManager bullet)
    {
        Debug.Log("I'm shooting Regular bullets!");
    }

    public override void UpdateState(BulletStateManager bullet)
    {
        // What are we doing in this state
        

    }
}
