// TeleportationProjectile.cs
using UnityEngine;

public class TeleportBulletState : BulletBaseState
{
    private PlayerStateManager _player;
    private bool _canTeleport = true;
    public override void EnterState(BulletStateManager bullet)
    {
        Debug.Log("I'm shooting Regular bullets!");
    }

    public override void UpdateState(BulletStateManager bullet)
    {
        // What are we doing in this state


    }
    public override void Start()
    {
        base.Start(); // Handles lifetime
        speed = 30f;  // Teleportation bullets can be faster.

        // Find the player and store a reference.  Important for teleportation.
        _player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerStateManager>();
        if (_player == null)
        {
            Debug.LogError("Player not found.  Make sure your player has the 'Player' tag.");
        }
    }
    public override void Update()
    {
        base.Update();
    }

    protected override void OnHit(GameObject other)
    {
        _canTeleport = false; // Prevent teleportation after hitting something.
        Destroy(gameObject); // Destroy on impact.
    }

    // Changed to public, called by PlayerStateManager
    public void Teleport()
    {
        if (_canTeleport && _player != null)
        {
            _player.transform.position = transform.position; // Teleport the player.
            Destroy(gameObject); // Destroy the projectile.
        }
    }
}