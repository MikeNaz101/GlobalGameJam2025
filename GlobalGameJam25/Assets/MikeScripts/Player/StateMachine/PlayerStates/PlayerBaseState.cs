using System;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class PlayerBaseState : BaseState<Key>
{
    protected PlayerBaseState(Key key) : base(key)
    {
    }

    internal abstract void EnterState(PlayerStateManager playerStateManager);

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
