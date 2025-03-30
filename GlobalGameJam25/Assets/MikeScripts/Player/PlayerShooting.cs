using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    public Transform firePoint;
    public float bulletForce = 20f;
    public PlayerStateManager player; // Reference assigned in Inspector or via GetComponent

    // --- Initial Mana Costs (Adjust as needed) ---
    // Set to 0 if mana cost is purely handled by bullet's charge-over-time
    public int basicManaCostInitial = 5;
    public int freezeManaCostInitial = 5;
    public int teleportManaCost = 20; // Teleport cost is usually upfront

    private bool _isCharging = false;
    private GameObject currentBullet;
    private float chargeStartTime;

    void Start()
    {
        // Get PlayerStateManager if not assigned in Inspector
        if (player == null)
        {
            player = GetComponent<PlayerStateManager>();
            if (player == null)
            {
                Debug.LogError("PlayerShooting cannot find PlayerStateManager!");
                this.enabled = false; // Disable script if player reference is missing
            }
        }
    }

    // Update is no longer needed here for input if using PlayerInput component and events (OnFire, OnChangeWeapon) handled in PlayerStateManager
    /*
    void Update()
    {
        // Handle bullet type switching via mouse scroll wheel (Moved to PlayerStateManager.OnChangeWeapon)
        // float scrollWheelInput = Input.GetAxis("Mouse ScrollWheel");
        // if (scrollWheelInput != 0 && player.bulletSpawner != null)
        // {
        //     int changeDirection = scrollWheelInput > 0 ? 1 : -1;
        //     player.bulletSpawner.ChangeBulletType(changeDirection);
        //     player.UpdateShellVisuals(); // Trigger shell update
        // }

        // Handle left click (Moved to PlayerStateManager.OnFire)
        // if (Input.GetMouseButtonDown(0))
        // {
        //     StartCharge();
        // }
        // if (Input.GetMouseButtonUp(0))
        // {
        //     EndCharge();
        // }
    }
    */

    // This function is now called by PlayerStateManager's OnFire event
    public void StartCharge()
    {
        // Ensure references are valid
        if (player == null || player.bulletSpawner == null || firePoint == null)
        {
            Debug.LogError("PlayerShooting is missing required references (Player, BulletSpawner, or FirePoint).");
            return;
        }
        // Prevent starting a new charge if already charging
        if (_isCharging) return;

        // Determine required mana for the current bullet type
        int requiredMana = 0;
        switch (player.bulletSpawner.CurrentBulletType)
        {
            case BulletType.Type1: requiredMana = basicManaCostInitial; break;
            case BulletType.Type2: requiredMana = freezeManaCostInitial; break;
            case BulletType.Type3: requiredMana = teleportManaCost; break;
        }

        // Check if player has enough mana BEFORE instantiating
        // Use the boolean return value from the modified UseMana function
        // Note: We check mana here, but deduct it *after* potentially telling the bullet to charge.
        if (player.currentMana < requiredMana)
        {
            Debug.Log("Not enough mana to start firing!");
            // Optional: Play 'failed' sound effect
            return;
        }

        // --- Instantiate the bullet ---
        currentBullet = Instantiate(player.bulletSpawner.bulletPrefab, firePoint.position, firePoint.rotation);
        if (currentBullet == null)
        {
            Debug.LogError("Failed to instantiate bullet prefab!");
            return;
        }

        // --- Prepare bullet for charging ---
        Rigidbody rb = currentBullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; // Keep it stationary during charge
        }
        currentBullet.transform.parent = firePoint; // Attach to fire point visually

        // --- Start Charging Logic ---
        chargeStartTime = Time.time;
        _isCharging = true;

        // Get specific bullet components
        BasicBullet basic = currentBullet.GetComponent<BasicBullet>();
        FreezeBullet freeze = currentBullet.GetComponent<FreezeBullet>();
        TeleportBullet teleport = currentBullet.GetComponent<TeleportBullet>();

        bool chargeStartedSuccessfully = false;

        // Tell the bullet to start charging (if applicable) and deduct initial cost
        if (basic != null)
        {
             // Pass player reference; bullet handles mana drain over time
            if (player.UseMana(requiredMana, player)) // Deduct initial cost
            {
                basic.StartCharging(player);
                chargeStartedSuccessfully = true;
            }
        }
        else if (freeze != null)
        {
             // Pass player reference; bullet handles mana drain over time
            if (player.UseMana(requiredMana, player)) // Deduct initial cost
            {
                freeze.StartCharging(player);
                 chargeStartedSuccessfully = true;
            }
        }
        else if (teleport != null)
        {
            // Teleport cost is only upfront. No charging method called.
            if (player.UseMana(requiredMana, player)) // Deduct cost
            {
                 chargeStartedSuccessfully = true; // Mark as successful to proceed to EndCharge later
                 // No _isCharging needed for teleport unless you add a visual indicator?
                 // For simplicity, let's keep _isCharging = true so EndCharge fires it.
            }
        }
        else
        {
            Debug.LogError("Instantiated bullet prefab has no recognized bullet script (Basic, Freeze, Teleport)!");
            chargeStartedSuccessfully = false; // Failed
        }


        // Cleanup if mana check failed *after* instantiation or bullet script missing
        if (!chargeStartedSuccessfully) {
             _isCharging = false;
             Destroy(currentBullet);
             currentBullet = null;
             // Don't refund mana here, UseMana handles the check/deduction attempt.
             Debug.Log("Failed to start charge (likely insufficient mana for initial cost or missing script).");
        } else {
             Debug.Log($"Started charging {player.bulletSpawner.CurrentBulletType}");
             // Optionally hide one shell visual immediately upon starting charge
             // player.UseMana(0, player); // Hacky way to trigger shell update? Better to integrate shell hiding directly.
             // Consider: If using shells strictly as ammo count, hide one here.
        }
    }


    // This function is now called by PlayerStateManager's OnFire event (when button is released)
    public void EndCharge()
    {
        // Only proceed if we were actually charging and have a bullet instance
        if (!_isCharging || currentBullet == null)
        {
            _isCharging = false; // Ensure flag is reset
            return;
        }

        _isCharging = false; // Stop charging state

        // --- Detach and Enable Physics ---
        currentBullet.transform.parent = null; // Unparent from fire point
        Rigidbody rb = currentBullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; // Allow physics to take over
        }
        else
        {
            Debug.LogError("Charged bullet has no Rigidbody!", currentBullet);
            // Decide how to handle this - destroy bullet? Log error?
        }


        // --- Stop Bullet Charging & Get Modifiers ---
        BasicBullet basic = currentBullet.GetComponent<BasicBullet>();
        FreezeBullet freeze = currentBullet.GetComponent<FreezeBullet>();
        // TeleportBullet teleport = currentBullet.GetComponent<TeleportBullet>(); // Not needed here unless StopCharging does something

        float chargeMultiplier = 1f; // Default force multiplier

        if (basic != null)
        {
            chargeMultiplier = basic.StopCharging(); // Get multiplier from basic bullet
        }
        else if (freeze != null)
        {
            freeze.StopCharging(); // Tell freeze bullet to finalize state (duration calculated OnHit)
        }
        // No special action needed for TeleportBullet on EndCharge itself


        // --- Apply Launch Force ---
        if (rb != null)
        {
            float finalForce = bulletForce * chargeMultiplier; // Apply multiplier only if applicable (e.g., basic bullet)
            rb.AddForce(firePoint.forward * finalForce, ForceMode.VelocityChange);
             Debug.Log($"Fired {player.bulletSpawner.CurrentBulletType} with force multiplier {chargeMultiplier}");
        }

        // --- Cleanup ---
        currentBullet = null; // Release reference, bullet is now independent
    }
}