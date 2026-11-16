using UnityEngine;
using UnityEngine.Events; // Use UnityEvent
using System; // Needed for Enum.GetValues

// Ensure this enum matches the order expected by PlayerStateManager's shellPrefabs list if using index-based access
// Also used by PlayerShooting to identify the teleport bullet
public enum BulletType
{
    Type1, // Index 0 (Basic Bullet)
    Type2, // Index 1 (Freeze Bullet)
    Type3, // Index 2 (Teleport Bullet)
    Type4  // Index 3 (Earth Bullet: regrows burnt plants)
}

public class BulletSpawnerState : MonoBehaviour
{
    [Header("Bullet Prefabs")]
    public GameObject basicBullet; // Assign Basic Bullet Prefab in Inspector (Index 0)
    public GameObject freezeBullet; // Assign Freeze Bullet Prefab in Inspector (Index 1)
    public GameObject teleportBullet; // Assign Teleport Bullet Prefab in Inspector (Index 2)
    [Tooltip("Optional. If empty, a green copy of the basic bullet is built at runtime.")]
    public GameObject earthBullet; // Earth Bullet Prefab (Index 3)

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
    public void ChangeBulletType(int changeAmount, int maxAllowedIndex, bool earthUnlocked = false)
    {
        // The shots the player can cycle through: basic, then freeze/teleport up to the unlocked index,
        // then earth if it has been unlocked (it is unlocked separately from the others).
        int earthIndex = (int)BulletType.Type4;
        int actualMaxIndex = Mathf.Clamp(maxAllowedIndex, 0, earthIndex - 1);
        var allowed = new System.Collections.Generic.List<int>();
        for (int i = 0; i <= actualMaxIndex; i++) allowed.Add(i);
        if (earthUnlocked) allowed.Add(earthIndex);

        // If only one shot is allowed, we cannot switch
        if (allowed.Count <= 1) return;

        BulletType previousType = CurrentBulletType; // Store previous type
        int position = allowed.IndexOf((int)CurrentBulletType);
        if (position < 0) position = 0;

        int step = changeAmount > 0 ? 1 : -1;
        int nextIndexInUnlockedRange = allowed[((position + step) % allowed.Count + allowed.Count) % allowed.Count];

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
            case BulletType.Type4:
                if (earthBullet == null) earthBullet = EarthBullet.CreateTemplate(basicBullet, transform);
                bulletPrefab = earthBullet;
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
               case BulletType.Type4: return EarthBullet.EarthColor;
               default: return Color.white;
          }
     }

     // Optional: Helper to get total count if needed by PlayerStateManager validation
     public int GetTotalBulletTypes()
     {
         return Enum.GetValues(typeof(BulletType)).Length;
     }
}