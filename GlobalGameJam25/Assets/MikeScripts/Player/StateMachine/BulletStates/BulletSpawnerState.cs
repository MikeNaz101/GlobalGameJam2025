using UnityEngine;
using UnityEngine.Events; // Use UnityEvent
using System; // Needed for Enum.GetValues

// Ensure this enum matches the order expected by PlayerStateManager's shellPrefabs list if using index-based access
// Also used by PlayerShooting to identify the teleport bullet
public enum BulletType
{
    Type1, // Index 0 (Basic Bullet)
    Type2, // Index 1 (Freeze Bullet)
    Type3  // Index 2 (Teleport Bullet)
}

public class BulletSpawnerState : MonoBehaviour
{
    [Header("Bullet Prefabs")]
    public GameObject basicBullet; // Assign Basic Bullet Prefab in Inspector (Index 0)
    public GameObject freezeBullet; // Assign Freeze Bullet Prefab in Inspector (Index 1)
    public GameObject teleportBullet; // Assign Teleport Bullet Prefab in Inspector (Index 2)

    [Header("State")]
    // Readonly property to see current type
    public BulletType CurrentBulletType { get; private set; }
    // Public getter for the currently selected prefab
    public GameObject bulletPrefab { get; private set; }

    [Header("Events")]
    // Event fired when bullet type changes (e.g., for UI or PlayerStateManager shell updates)
    public UnityEvent OnBulletTypeChanged;

    // Add a reference to notify PlayerShooting if needed (for VCam priority reset on weapon change)
    [HideInInspector] // Hide from inspector, PlayerShooting sets this in its Start()
    public PlayerShooting playerShooting;

    private void Awake()
    {
        // Default to first bullet type on start
        CurrentBulletType = BulletType.Type1;
        UpdateBulletPrefab();

        // Initialize the event if it hasn't been (good practice)
        if (OnBulletTypeChanged == null)
            OnBulletTypeChanged = new UnityEvent();
    }

    // --- MODIFIED Method ---
    /// <summary>
    /// Changes the selected bullet type, wrapping around within the allowed unlocked range.
    /// </summary>
    /// <param name="changeAmount">1 for next, -1 for previous.</param>
    /// <param name="maxAllowedIndex">The highest index (enum value) the player is allowed to select.</param>
    public void ChangeBulletType(int changeAmount, int maxAllowedIndex) // Added maxAllowedIndex parameter
    {
        // Validate maxAllowedIndex against the actual number of defined bullet types
        int totalDefinedTypes = Enum.GetValues(typeof(BulletType)).Length;
        int actualMaxIndex = Mathf.Clamp(maxAllowedIndex, 0, totalDefinedTypes - 1);

        // If only index 0 is allowed, we cannot switch
        if (actualMaxIndex <= 0)
        {
            // Debug.Log("ChangeBulletType: Only one type (Index 0) is unlocked. No change.");
            return;
        }

        int currentTypeIndex = (int)CurrentBulletType;
        BulletType previousType = CurrentBulletType; // Store previous type

        // Calculate the number of types the player can currently cycle through
        int numUnlockedTypes = actualMaxIndex + 1;

        // Calculate the next index within the *unlocked range* using modulo arithmetic
        // Add numUnlockedTypes before modulo to handle negative changeAmount correctly
        int nextIndexInUnlockedRange = (currentTypeIndex + changeAmount % numUnlockedTypes + numUnlockedTypes) % numUnlockedTypes;

        // The result 'nextIndexInUnlockedRange' is guaranteed to be between 0 and actualMaxIndex (inclusive)
        // because it's the index within the subset of unlocked types.
        BulletType newType = (BulletType)nextIndexInUnlockedRange; // Cast the valid index back to the enum type

        // Update the current bullet type only if it actually changed
        if (newType != previousType)
        {
            CurrentBulletType = newType;
            UpdateBulletPrefab(); // Update the prefab reference

            // Invoke the event to notify listeners (like PlayerStateManager for shells/UI)
            OnBulletTypeChanged?.Invoke(); // Safely invoke
            Debug.Log($"Bullet type changed to: {CurrentBulletType} (Index: {(int)CurrentBulletType})");

            // --- Camera Reset Logic (No changes needed here) ---
            if (previousType == BulletType.Type3 && newType != BulletType.Type3 && playerShooting != null)
            {
                playerShooting.CancelCharge();
            }
            // --- End Camera Reset Logic ---
        }
    }
    // --- END MODIFIED Method ---

    // Updates the public bulletPrefab based on the CurrentBulletType
    private void UpdateBulletPrefab()
    {
        switch (CurrentBulletType)
        {
            case BulletType.Type1:
                bulletPrefab = basicBullet;
                break;
            case BulletType.Type2:
                bulletPrefab = freezeBullet;
                break;
            case BulletType.Type3:
                bulletPrefab = teleportBullet;
                break;
            default:
                // This case should ideally not be reachable if CurrentBulletType is always valid
                Debug.LogError($"Unhandled BulletType '{CurrentBulletType}' in UpdateBulletPrefab!");
                bulletPrefab = basicBullet; // Default fallback
                break;
        }

        // Safety check if the selected prefab is null after the switch
        if (bulletPrefab == null) {
             Debug.LogError($"Prefab for {CurrentBulletType} is not assigned in the BulletSpawnerState Inspector!", this);
             // Attempt to fallback to basic if possible, otherwise things might break further
             if (CurrentBulletType != BulletType.Type1 && basicBullet != null) {
                 Debug.LogWarning($"Falling back to Basic Bullet prefab because {CurrentBulletType} was null.");
                 bulletPrefab = basicBullet;
                 CurrentBulletType = BulletType.Type1; // Correct the state too
                 OnBulletTypeChanged?.Invoke(); // Notify of the fallback change
             }
        }
    }

     // Optional helper to get color (if needed externally)
     public Color GetCurrentBulletColor()
     {
          switch (CurrentBulletType)
          {
               case BulletType.Type1: return Color.red; // Example color
               case BulletType.Type2: return Color.blue; // Example color
               case BulletType.Type3: return Color.green; // Example color
               default: return Color.white;
          }
     }

     // Optional: Helper to get total count if needed by PlayerStateManager validation
     public int GetTotalBulletTypes()
     {
         return Enum.GetValues(typeof(BulletType)).Length;
     }
}