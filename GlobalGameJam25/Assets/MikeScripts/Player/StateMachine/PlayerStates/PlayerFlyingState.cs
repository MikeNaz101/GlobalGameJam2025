using UnityEngine;

public class PlayerFlyingState : PlayerBaseState
{
    private bool isFlying = false;
    private GameObject rWing; // Reference to the Right wing prefab
    private GameObject lWing; // Reference to the Left wing prefab
    private float forwardSpeed = 10f; // Base forward speed
    private float speedIncrease = 5f;  // Speed increase when moving forward
    private const float gravityScale = 0.5f; // Half gravity when flying

    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("Entering Flying State!");
        
        // Instantiate wings and set the player to horizontal
        rWing = GameObject.Instantiate(player.rWingPrefab, player.rWingPoint);
        player.transform.rotation = Quaternion.Euler(0, player.transform.eulerAngles.y, 0); // Horizontal orientation
        lWing = GameObject.Instantiate(player.lWingPrefab, player.lWingPoint);
        player.transform.rotation = Quaternion.Euler(0, player.transform.eulerAngles.y, 0); // Horizontal orientation
        isFlying = true;

        // Adjust gravity
        player.gravityScale = gravityScale; // Ensure you have this property in your controller logic
        FlyPlayer(player);
    }

    public override void UpdateState(PlayerStateManager player)
    {
        FlyPlayer(player);

        // Check if the player has touched the ground to exit flying
        if (player.IsGrounded())
        {
            player.SwitchState(player.idleState); // Switch to idle or any other appropriate state
        }
    }

    private void FlyPlayer(PlayerStateManager player)
    {
        // Forward movement
        float forwardMovement = forwardSpeed * Time.deltaTime;
        player.controller.Move(player.transform.forward * forwardMovement); // Move the player forward

        // Handle upward lift with the jump
        if (Input.GetKeyDown(KeyCode.Space))
        {
            FlapWings(player); // Play wing flapping animation
            player.controller.Move(Vector3.up * player.jumpForce * Time.deltaTime); // Move upward
        }

        // Increase forward speed if moving forward
        if (Input.GetKey(KeyCode.W)) // Forward movement key
        {
            forwardSpeed += speedIncrease * Time.deltaTime;
        }
    }

    private void FlapWings(PlayerStateManager player)
    {
        // Play flapping animation or effects
        Debug.Log("Flapping Wings!");
        // Add animation logic here if applicable
    }

    public void ExitState(PlayerStateManager player)
    {
        // Clean up
        GameObject.Destroy(rWing); // Remove wings from the player
        GameObject.Destroy(lWing);
        player.gravityScale = 1f; // Reset gravity to normal
        isFlying = false;
    }
}
