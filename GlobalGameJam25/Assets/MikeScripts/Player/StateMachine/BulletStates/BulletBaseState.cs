using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public abstract class BulletBaseState
{
    public abstract void EnterState(BulletStateManager bullet);

    public abstract void UpdateState(BulletStateManager bullet);
    public virtual void ExitState(BulletStateManager bullet) { }
}

