using System.Collections.Generic;
using System;
using UnityEngine;

public abstract class StateManager<EState> : MonoBehaviour where EState : Enum
{
    protected Dictionary<EState, BaseState<EState>> States = new Dictionary<EState,BaseState<EState>>();

    protected BaseState<EState> CurrentState;
    protected bool IsTransitioningState = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CurrentState.EnterState();
    }

    // Update is called once per frame
    void Update()
    {
        EState nextStateKey = CurrentState.GetNextState();
        if (!IsTransitioningState && nextStateKey.Equals(CurrentState.StateKey))
        {
            CurrentState.UpateState();
        }
        else if (!IsTransitioningState)
        {
            TransitionToState(nextStateKey);
        }
    }

    public void TransitionToState(EState stateKey)
    {
        IsTransitioningState = true;
        CurrentState.ExitState();
        CurrentState = States[stateKey];
        CurrentState.EnterState();
        IsTransitioningState = false;
    }

    public abstract EState GetNextState();
    void OnTriggerEnter(Collider other)
    {
        CurrentState.OnTriggerEnter(other);
    }
    void OnTriggerStay(Collider other)
    {
        CurrentState.OnTriggerStay(other);
    }
    void OnTriggerExit(Collider other)
    {
        CurrentState.OnTriggerExit(other);
    }
}
