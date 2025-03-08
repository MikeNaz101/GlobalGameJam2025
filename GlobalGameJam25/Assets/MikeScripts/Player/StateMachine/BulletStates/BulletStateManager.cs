using UnityEngine;

public class BulletStateManager : MonoBehaviour
{
    public BasicBulletState basicBullet = new BasicBulletState();
    [HideInInspector]
    public FreezeBulletState freezeBullet = new FreezeBulletState();
    [HideInInspector]
    public TeleportBulletState teleportBullet = new TeleportBulletState();
    [HideInInspector]
    public PlayerAttackingState attackState = new PlayerAttackingState();
    [HideInInspector]
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
