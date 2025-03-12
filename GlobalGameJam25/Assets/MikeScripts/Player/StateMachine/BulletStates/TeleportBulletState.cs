using UnityEngine;

public class TeleportBulletState : BulletBaseState
{
    private PlayerStateManager _player;
    private bool _canTeleport = true;
    private Transform transform;
    private float speed;

    public override void EnterState(BulletStateManager bullet)
    {
        Debug.Log("I'm shooting Teleport bullets!");
        _player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerStateManager>();
        if (_player == null)
        {
            Debug.LogError("Player not found.");
        }
        transform = bullet.bulletPrefab.transform;
        speed = 30f;
    }

    public override void UpdateState(BulletStateManager bullet)
    {
        // No update logic needed in this state
    }

    protected void OnHit(GameObject other)
    {
        _canTeleport = false;
        Object.Destroy(transform.gameObject);
    }

    public void Teleport()
    {
        if (_canTeleport && _player != null)
        {
            _player.transform.position = transform.position;
            Object.Destroy(transform.gameObject);
        }
    }

    public override void ExitState(BulletStateManager bullet)
    {
        // No exit logic needed in this state
    }
}