using UnityEngine;

public class PlayerStateManager : MonoBehaviour
{
    PlayerBaseState currentState;
   // public Player_Idle idle = new Player_Idle();
    public Player_Running running = new Player_Running();
    public Player_Walking walking= new Player_Walking();
    public Player_Flying flying= new Player_Flying();
    public Player_Hit hit = new Player_Hit();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //currentState = idle;

        currentState.EnterState(this);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
