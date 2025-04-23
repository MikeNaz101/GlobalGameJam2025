using UnityEngine;

public class ProgressBarManager : MonoBehaviour
{
    public HealthBar healthBar; // Reference to the Health progress bar
    public ManaBar manaBar;   // Reference to the Mana progress bar
    public StaminaBar staminaBar; // Reference to the Stamina progress bar
    public XPBar xpBar;       // <<< ADDED: Reference to the XP progress bar

    public PlayerStateManager player; // Reference to the PlayerCharacter class

    void Start()
    {
        if (player == null)
        {
            Debug.LogError("PlayerStateManager reference not set on ProgressBarManager!", this);
            this.enabled = false; // Disable component if player missing
            return;
        }

        // --- Initialize Health, Mana, Stamina ---
        if (healthBar != null) healthBar.SetMaxStats(player.maxHealth);
        else Debug.LogWarning("HealthBar reference not set.", this);

        if (manaBar != null) manaBar.SetMaxStats(player.maxMana);
        else Debug.LogWarning("ManaBar reference not set.", this);

        if (staminaBar != null) staminaBar.SetMaxStats(player.maxStamina);
        else Debug.LogWarning("StaminaBar reference not set.", this);

        // --- ADDED: Initialize XP Bar ---
        if (xpBar != null)
        {
            // Use the combined update or individual setters
            xpBar.UpdateXPBar(player.currentLevel, player.currentXP, player.xpToNextLevel);
            // Or:
            // xpBar.SetLevel(player.currentLevel);
            // xpBar.SetMaxXP(player.xpToNextLevel);
            // xpBar.SetCurrentXP(player.currentXP);
        }
        else Debug.LogWarning("XpBar reference not set.", this);
        // -------------------------------

        // Update the progress bars to reflect the current stats immediately
        UpdateProgressBars();
    }

    void Update()
    {
        // Avoid errors if player reference was lost somehow
        if (player == null) return;

        // Continuously update the progress bars
        UpdateProgressBars();
    }

    void UpdateProgressBars()
    {
        // Update each progress bar based on the player's current stats
        if (healthBar != null) healthBar.SetHealth(player.currentHealth);
        if (manaBar != null) manaBar.SetMana(player.currentMana);
        if (staminaBar != null) staminaBar.SetStamina(player.currentStamina);

        // --- ADDED: Update XP Bar ---
        if (xpBar != null)
        {
            // Continuously update all aspects of the XP bar
            // Note: Setting MaxXP every frame is slightly inefficient but matches the pattern.
            // Consider using events from PlayerStateManager for better performance if stats don't change constantly.
            xpBar.UpdateXPBar(player.currentLevel, player.currentXP, player.xpToNextLevel);
        }
        // --------------------------
    }

    // --- Optional: Event-Based Update (More Performant) ---
    /*
    void OnEnable()
    {
        // Subscribe to player events when this manager becomes active
        PlayerStateManager.OnXPChanged += HandleXPChange;
        PlayerStateManager.OnLevelChanged += HandleLevelChange;
        // Add subscriptions for Health/Mana/Stamina if PlayerStateManager has events for them
    }

    void OnDisable()
    {
        // Unsubscribe when inactive/destroyed to prevent errors
        PlayerStateManager.OnXPChanged -= HandleXPChange;
        PlayerStateManager.OnLevelChanged -= HandleLevelChange;
    }

    void HandleXPChange(int currentXP, int xpToNext)
    {
        if (xpBar != null)
        {
            // Only update necessary parts when XP changes
            xpBar.SetMaxXP(xpToNext); // Max might change on level up, update just in case
            xpBar.SetCurrentXP(currentXP);
        }
    }

    void HandleLevelChange(int newLevel)
    {
        if (xpBar != null)
        {
            xpBar.SetLevel(newLevel);
            // We expect OnXPChanged to fire immediately after level up to update slider values
        }
    }
    // You would remove the Update() method or remove the XP update lines from UpdateProgressBars()
    // if using the event-based approach. You'd also need to call the Handle methods once in Start
    // or OnEnable AFTER subscribing to initialize the UI.
    */
    // -------------------------------------------------------
}