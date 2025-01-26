using UnityEngine;

public class PlayerStateMachine : StateManager<PlayerStateMachine.PlayerState>
{
    public enum PlayerState
    {
        Idle,
        Walk,
        Run,
        Fly,
        Attack,
        Hit,
        Dead
    }
    
    public override PlayerState GetNextState()
    {
        throw new System.NotImplementedException();
    }
}

