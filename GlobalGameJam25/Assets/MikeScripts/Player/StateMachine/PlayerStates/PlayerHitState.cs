using UnityEngine;

public class PlayerHitState : PlayerBaseState
{
    private float hitDuration = 0.5f; // Time spent in hit state
    private float hitTimer = 0f;
    private bool knockbackApplied = false;
    
    private int damageTaken; // Store the damage value

    public void SetDamage(int damage)
    {
        damageTaken = damage; // Store damage value to apply when entering state
    }

    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("Player got hit!");

        // Apply damage
        Debug.Log("Your currentHealth before calculating damage is: " + player.currentHealth);
        player.currentHealth -= damageTaken;
        player.currentHealth = Mathf.Clamp(player.currentHealth, 0, player.maxHealth);
        Debug.Log("Player Took " + damageTaken + " damage! Your health is now: " + player.currentHealth);

        // Optionally trigger a "hit" animation
        // player.animator.SetTrigger("Hit");

        // Apply knockback if necessary
        if (!knockbackApplied)
        {
            ApplyKnockback(player);
            knockbackApplied = true;
        }

        // Check if the player is dead
        if (player.currentHealth <= 0)
        {
            player.SwitchState(player.deathState); // Transition to death state (if you have one)
        }
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // Keep the player in this state for `hitDuration` seconds
        hitTimer += Time.deltaTime;
        if (hitTimer >= hitDuration)
        {
            player.SwitchState(player.idleState); // Return to idle or another appropriate state
        }
    }

    private void ApplyKnockback(PlayerStateManager player)
    {
        Vector3 knockbackDirection = -player.transform.forward; // Example: Knock back in the opposite direction
        player.controller.Move(knockbackDirection * 2f); // Adjust force as needed
    }
}
