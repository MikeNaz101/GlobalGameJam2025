using UnityEngine;

public class ProgressBarManager : MonoBehaviour
{
    public HealthBar healthBar; // Reference to the Health progress bar
    public ManaBar manaBar;  // Reference to the Mana progress bar
    public StaminaBar staminaBar; // Reference to the Stamina progress bar

    public PlayerManager player; // Reference to the PlayerCharacter class

    void Start()
    {
        // Initialize the progress bars with player's max stats
        healthBar.SetMaxStats(player.maxHealth);
        manaBar.SetMaxStats(player.maxMana);
        staminaBar.SetMaxStats(player.maxStamina);

        // Update the progress bars to reflect the current stats
        UpdateProgressBars();
    }

    void Update()
    {
        // Continuously update the progress bars (or trigger this in events for optimization)
        UpdateProgressBars();
    }

    void UpdateProgressBars()
    {
        // Update each progress bar based on the player's current stats
        healthBar.SetHealth(player.currentHealth);
        manaBar.SetMana(player.currentMana);
        staminaBar.SetStamina(player.currentStamina);
    }
}