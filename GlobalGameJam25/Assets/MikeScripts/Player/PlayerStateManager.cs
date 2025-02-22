using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerStateManager : MonoBehaviour
{
    public PlayerBaseState currentState;
    public float default_speed = 100;
    public bool isSneaking = false;
    [HideInInspector]
    public PlayerIdleState idleState = new PlayerIdleState();
    [HideInInspector]
    public PlayerWalkingState walkState = new PlayerWalkingState();
    [HideInInspector]
    public PlayerSneakState sneakState = new PlayerSneakState();
    [HideInInspector]
    public Vector2 movement;
    [HideInInspector]
    CharacterController controller;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        controller = GetComponent<CharacterController>();
        currentState = walkState;

        SwitchState(idleState);
    }

    // Update is called once per frame
    void Update()
    {
        currentState.UpdateState(this);
    }

    void OnMove(InputValue movVal)
    {
        movement = movVal.Get<Vector2>();
        print("I is move!");
    }

    void OnSprint()
    {
        isSneaking = !isSneaking;
    }
    public void MovePlayer(float speed)
    {
        float moveX = movement.x;
        float moveY = movement.y;

        Vector3 actual_movement = new Vector3(moveX, 0, moveY);
        controller.Move(actual_movement*speed*Time.deltaTime);
    }

    public void SwitchState(PlayerBaseState newState)
    {
        currentState = newState;
        currentState.EnterState(this);
    }
}
