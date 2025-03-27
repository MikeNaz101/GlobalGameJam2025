using UnityEngine;
using UnityEngine.Events; // Add this for UnityEvent

public enum BulletType
{
    Type1,
    Type2,
    Type3
}

public class BulletSpawnerState : MonoBehaviour
{
    
    public GameObject basicBullet; [HideInInspector]
    
    public GameObject freezeBullet; [HideInInspector]
    
    public GameObject teleportBullet; [HideInInspector]

    //public static BulletStateManager Instance { get; private set; }

    public BulletType CurrentBulletType { get; private set; }
    public GameObject bulletPrefab;
    public Color type1Color = Color.red;
    public Color type2Color = Color.blue;
    public Color type3Color = Color.green;

    public UnityEvent OnBulletTypeChanged; // Use UnityEvent directly

    private void Awake()
    {
        CurrentBulletType = BulletType.Type1;
        //basicBullet = new BasicBulletState(); // Instantiate here
        //freezeBullet = new FreezeBulletState(); // Instantiate here
        //teleportBullet = new TeleportBulletState(); // Instantiate here
    }

    public void ChangeBulletType(int changeAmount)
    {
        int currentTypeIndex = (int)CurrentBulletType;
        int numTypes = System.Enum.GetValues(typeof(BulletType)).Length;
        currentTypeIndex = (currentTypeIndex + changeAmount) % numTypes;
        if (currentTypeIndex < 0)
        {
            currentTypeIndex += numTypes;
        }
        CurrentBulletType = (BulletType)currentTypeIndex;

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
                return Color.white;
        }
    }
}