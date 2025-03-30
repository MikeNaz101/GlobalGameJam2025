using UnityEngine;

public class PlayerHitState : PlayerBaseState
{
    private float hitDuration = 0.5f; // Time player is stunned/in hit state
    private float hitTimer = 0f;
    private bool knockbackApplied = false; // Ensures knockback happens once per entry

    private int damageTaken; // Store the damage value passed via SetDamage

    // Called by PlayerStateManager.TakeDamage before switching to this state
    public void SetDamage(int damage)
    {
        damageTaken = damage;
    }

    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("Entering Hit State!");
        hitTimer = 0f; // Reset timer on entry
        knockbackApplied = false; // Reset knockback flag

        // --- Apply Damage ---
        if (player.currentHealth > 0) // Only apply if not already dead
        {
            // Debug.Log("Health before hit state damage: " + player.currentHealth); // Optional debug
            player.currentHealth -= damageTaken;
            player.currentHealth = Mathf.Clamp(player.currentHealth, 0, player.maxHealth);
            Debug.Log($"Player Took {damageTaken} damage! Health now: {player.currentHealth}");
            // Update Health Bar if applicable
            // player.healthBar?.SetHealth(player.currentHealth);
        }

        // --- Check for Death ---
        if (player.currentHealth <= 0)
        {
            // PlayerStateManager.Die() handles scene change etc.
            // No need to switch state further if Die() changes scene immediately.
            // If Die() plays animation first, then switching to a DeadState *within* Die() might be useful.
            // For now, assume Die() handles everything.
            // player.SwitchState(player.deathState); // We removed deathState
            // player.Die(); // Call Die directly if TakeDamage didn't already
            // Let's assume PlayerStateManager.TakeDamage calls SwitchState, and THIS state checks health again
            // If health is 0 here, we perhaps just don't apply knockback and let the Die() process happen.
            if (!knockbackApplied) // Don't knockback if dead
            {
                 ApplyKnockback(player);
                 knockbackApplied = true;
            }
            // PlayerStateManager's Die() method should handle the rest.
             return; // Exit EnterState if dead
        }

        // --- Apply Knockback (if alive) ---
        // TODO: Get knockback direction/force from the damage source for better feel.
        if (!knockbackApplied)
        {
            ApplyKnockback(player);
            knockbackApplied = true;
        }

        // Trigger hit animation (optional)
        // player.GetComponentInChildren<Animator>()?.SetTrigger("Hit");
    }

    public override void UpdateState(PlayerStateManager player)
    {
        // --- State Duration ---
        hitTimer += Time.deltaTime;
        if (hitTimer >= hitDuration)
        {
            // --- Transition Out ---
            // TODO: Could transition back to previous state, or to FallingState if knocked airborne.
            // For simplicity, transitioning back to Idle.
            player.SwitchState(player.idleState);
        }
        // Note: Player input is effectively ignored while in this state's Update.
        // Gravity is still applied by PlayerStateManager.Update.
    }

    private void ApplyKnockback(PlayerStateManager player)
    {
         // Simple example: Knock directly backwards from where player is facing.
         // A better approach involves getting direction FROM the damage source.
        Vector3 knockbackDirection = -player.transform.forward; // Simple backwards direction
        float knockbackForce = 2f; // Adjust strength as needed

        Debug.Log("Applying knockback");

        // Using controller.Move for knockback can be basic.
        // It might fight gravity or other movement. Consider applying an impulse
        // via player.verticalVelocity and a temporary horizontal velocity if needed.
        // For now, using simple Move:
        player.controller.Move(knockbackDirection * knockbackForce * Time.deltaTime); // Apply over one frame

         // Example alternative: Apply vertical/horizontal impulse
         // player.verticalVelocity += 2.0f; // Pop up slightly
         // player.horizontalVelocity = knockbackDirection * 5.0f; // Apply horizontal speed (needs horizontalVelocity field + handling in PlayerStateManager)
    }

     public override void ExitState(PlayerStateManager player)
     {
         // Reset anything specific to the hit state if needed
         // Debug.Log("Exiting Hit State");
     }
}