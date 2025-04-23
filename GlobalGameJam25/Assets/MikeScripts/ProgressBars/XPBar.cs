using UnityEngine;
using UnityEngine.UI; // Required for Slider
using TMPro; // Required if using TextMeshPro for level text, otherwise use UnityEngine.UI for standard Text

public class XPBar : MonoBehaviour
{
    [Tooltip("Assign the UI Slider component for the XP bar.")]
    public Slider xpSlider;

    [Tooltip("Assign the UI Text or TextMeshProUGUI component to display the player's level (Optional).")]
    public TextMeshProUGUI levelText; // Use 'public Text levelText;' if using standard UI Text

    // Call this initially and whenever the player levels up
    public void SetLevel(int level)
    {
        // Update the level display text if assigned
        if (levelText != null)
        {
            levelText.text = "Lvl: " + level.ToString();
        }

        // Reset the slider value to 0 when leveling up (optional, looks nice)
        // You might call SetCurrentXP(0) from the manager after this if using events,
        // or the continuous update will handle it.
        // if (xpSlider != null) xpSlider.value = 0;
    }

    // Call this initially and whenever the player levels up (or whenever xpToNextLevel changes)
    public void SetMaxXP(int xpToNextLevel)
    {
        if (xpSlider != null)
        {
            // Ensure max value is at least 1 to avoid slider errors
            xpSlider.maxValue = Mathf.Max(1, xpToNextLevel);
        }
    }

    // Call this whenever the player gains XP
    public void SetCurrentXP(int currentXP)
    {
        if (xpSlider != null)
        {
            // Clamp the value just in case currentXP somehow exceeds max momentarily
            xpSlider.value = Mathf.Clamp(currentXP, 0, xpSlider.maxValue);
        }
    }

    // Optional: A combined update function if preferred
    public void UpdateXPBar(int level, int currentXP, int xpToNextLevel)
    {
        SetLevel(level);
        SetMaxXP(xpToNextLevel);
        SetCurrentXP(currentXP);
    }
}