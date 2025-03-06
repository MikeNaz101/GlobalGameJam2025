using UnityEngine;

public class PlayerFlyingState : PlayerBaseState
{
    private bool isFlying = false;
    private GameObject rWing; // Reference to the Right wing prefab
    private GameObject lWing; // Reference to the Left wing prefab
    private float forwardSpeed = 10f; // Base forward speed
    private float speedIncrease = 5f;  // Speed increase when moving forward
    //private const float gravity = .5f; // Half gravity when flying

    public override void EnterState(PlayerStateManager player)
    {
        // Instantiate wings and set the player to horizontal
        rWing = GameObject.Instantiate(player.rWingPrefab, player.rWingPoint);
        player.transform.rotation = Quaternion.Euler(0, player.transform.eulerAngles.y, 0); // Horizontal orientation
        lWing = GameObject.Instantiate(player.lWingPrefab, player.lWingPoint);
        player.transform.rotation = Quaternion.Euler(0, player.transform.eulerAngles.y, 0); // Horizontal orientation
        isFlying = true;
        Debug.Log("Entered Flying State");
        player.verticalVelocity = 0f; // Reset vertical velocity when entering flying state
        FlyPlayer(player);
    }

    public override void UpdateState(PlayerStateManager player)
    {
        FlyPlayer(player);

        // Check if player lands
        if (player.IsGrounded())
        {
            player.SwitchState(player.idleState); // Return to idle state when landing
        }
    }

    private void FlyPlayer(PlayerStateManager player)
    {
        // Apply gravity when airborne
        if (!player.IsGrounded())
        {
            player.verticalVelocity += player.gravity * Time.deltaTime;
        }
        else
        {
            player.verticalVelocity = 0f; // Reset velocity when landing
        }

        // Handle upward movement (flap wings)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            FlapWings(player); // Play wing flapping animation
            player.verticalVelocity = player.jumpForce; // Apply jump force
        }

        // Increase forward speed if moving forward
        if (Input.GetKey(KeyCode.W))
        {
            forwardSpeed += speedIncrease * Time.deltaTime;
        }

        // Apply movement
        Vector3 movement = (player.transform.forward * forwardSpeed) + (Vector3.up * player.verticalVelocity);
        player.controller.Move(movement * Time.deltaTime);
    }

    private void FlapWings(PlayerStateManager player)
    {
        // Add logic for wing animation if needed
        Debug.Log("Flapped wings!");
    }

    public override void ExitState(PlayerStateManager player)
    {
        // Clean up
        GameObject.Destroy(rWing); // Remove wings from the player
        GameObject.Destroy(lWing);
        //player.gravity = 1f; // Reset gravity to normal
        isFlying = false;
    }
}
