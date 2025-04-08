using UnityEngine;
using UnityEngine.Events; // Use UnityEvent

// Ensure this enum matches the order expected by PlayerStateManager's shellPrefabs list if using index-based access
// Also used by PlayerShooting to identify the teleport bullet
public enum BulletType
{
    Type1, // Example: Basic Bullet
    Type2, // Example: Freeze Bullet
    Type3  // Example: Teleport Bullet
}

public class BulletSpawnerState : MonoBehaviour
{
    [Header("Bullet Prefabs")]
    public GameObject basicBullet; // Assign Basic Bullet Prefab in Inspector
    public GameObject freezeBullet; // Assign Freeze Bullet Prefab in Inspector
    public GameObject teleportBullet; // Assign Teleport Bullet Prefab in Inspector

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

    // Called by PlayerInput's change weapon action (likely via PlayerStateManager)
    public void ChangeBulletType(int changeAmount)
    {
        int currentTypeIndex = (int)CurrentBulletType;
        int numTypes = System.Enum.GetValues(typeof(BulletType)).Length;
        BulletType previousType = CurrentBulletType; // Store previous type to check for teleport cancel

        // Calculate the new index with wrapping
        currentTypeIndex = (currentTypeIndex + changeAmount) % numTypes;
        // Handle negative wrapping if changeAmount is negative
        if (currentTypeIndex < 0) currentTypeIndex += numTypes;

        BulletType newType = (BulletType)currentTypeIndex;

        // Update the current bullet type only if it actually changed
        if (newType != previousType)
        {
            CurrentBulletType = newType;
            UpdateBulletPrefab();

            // Invoke the event to notify listeners (like PlayerStateManager for shells/UI)
            OnBulletTypeChanged?.Invoke(); // Safely invoke
            Debug.Log("Bullet type changed to: " + CurrentBulletType);

            // --- Add Camera Reset Logic ---
            // If the player was charging the *teleport* bullet and switched *away* from it
            if (previousType == BulletType.Type3 && newType != BulletType.Type3 && playerShooting != null)
            {
                // Tell PlayerShooting to cancel the charge (which includes resetting the VCam priority)
                 playerShooting.CancelCharge();
            }
            // --- End Camera Reset Logic ---
        }
    }

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
                Debug.LogError("Unhandled BulletType in UpdateBulletPrefab!");
                bulletPrefab = basicBullet; // Default fallback
                break;
        }

        // Safety check if the selected prefab is null
        if (bulletPrefab == null) {
             Debug.LogError($"Prefab for {CurrentBulletType} is not assigned in BulletSpawnerState Inspector!");
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
}