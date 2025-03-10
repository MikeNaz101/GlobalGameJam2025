using UnityEngine;

public enum BulletType
{
    Type1,
    Type2,
    Type3
}
public class BulletStateManager : MonoBehaviour
{
    [HideInInspector]
    public BasicBulletState basicBullet = new BasicBulletState();
    [HideInInspector]
    public FreezeBulletState freezeBullet = new FreezeBulletState();
    [HideInInspector]
    public TeleportBulletState teleportBullet = new TeleportBulletState();
    [HideInInspector]
    public PlayerAttackingState attackState = new PlayerAttackingState();
    [HideInInspector]
    public static BulletStateManager Instance { get; private set; } // Singleton pattern

    public BulletType CurrentBulletType { get; private set; }
    public GameObject bulletPrefab;
    public Color type1Color = Color.red;
    public Color type2Color = Color.blue;
    public Color type3Color = Color.green;


    private void Awake()
    {
        // Singleton pattern setup
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Optional: Keeps the manager alive between scenes
        }
        else
        {
            Destroy(gameObject);
        }

        CurrentBulletType = BulletType.Type1; // Set a default bullet type
    }

    public void ChangeBulletType(int changeAmount)
    {
        // Cycle through the bullet types.  The modulo operator (%) handles wrapping around.
        int currentTypeIndex = (int)CurrentBulletType;
        int numTypes = System.Enum.GetValues(typeof(BulletType)).Length;
        currentTypeIndex = (currentTypeIndex + changeAmount) % numTypes;
        if (currentTypeIndex < 0)
        {
            currentTypeIndex += numTypes;  //handle the negative numbers from the scroll wheel
        }
        CurrentBulletType = (BulletType)currentTypeIndex;

        // Debug.Log("Current Bullet Type: " + CurrentBulletType); //for testing

        // Notify other systems (e.g., IndicatorManager) about the change.
        // We'll use a UnityEvent for this.  More robust than direct calls.
        OnBulletTypeChanged?.Invoke();
    }
    public Color GetCurrentBulletColor()
    {
        switch (CurrentBulletType)
        {
            case BulletType.Type1:
                return type1Color;
            case BulletType.Type2:
                return type2Color;
            case BulletType.Type3:
                return type3Color;
            default:
                return Color.white; // Should never happen, but good practice
        }
    }

    // UnityEvent for notifying other scripts when the bullet type changes.
    public UnityEngine.Events.UnityEvent OnBulletTypeChanged;
}