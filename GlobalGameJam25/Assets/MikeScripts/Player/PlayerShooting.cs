using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    [Header("References")]
    public Transform firePoint; // Assign in Inspector: Where bullets spawn
    public PlayerStateManager player; // Reference assigned in Inspector or via GetComponent in Start/Awake

    [Header("Shooting Settings")]
    public float bulletForce = 20f; // Base force applied to bullets

    // --- Mana Costs (Can be adjusted per bullet type) ---
    [Header("Mana Costs")]
    public int basicManaCostInitial = 5;  // Initial cost to start charging BasicBullet (if applicable)
    public int freezeManaCostInitial = 5; // Initial cost to start charging FreezeBullet (if applicable)
    public int teleportManaCost = 20; // Cost for firing the TeleportBullet (both aimed and non-aimed)

    // --- State Variables ---
    // Made public so PlayerStateManager can check if a charge is in progress
    // Alternatively, use a public property: public bool IsCharging => _isCharging;
    [HideInInspector] public bool _isCharging = false;

    private GameObject currentBullet;   // Holds the bullet instance during charging
    private float chargeStartTime;      // Time when charging started (for potential charge effects)


    void Start()
    {
        // Get PlayerStateManager if not assigned in Inspector
        if (player == null)
        {
            player = GetComponent<PlayerStateManager>();
            // If still null (maybe it's on a parent object?), try GetComponentInParent
            if (player == null) player = GetComponentInParent<PlayerStateManager>();

            if (player == null)
            {
                Debug.LogError("PlayerShooting cannot find PlayerStateManager component! Ensure it's on this GameObject or a parent, or assign it in the Inspector.");
                this.enabled = false; // Disable script if player reference is missing
                return;
            }
        }
        // Optional: Ensure PlayerStateManager also has a reference back to this script
        // if (player.playerShooting == null) player.playerShooting = this;
    }


    // --- NEW: Handles Immediate Firing for Aimed Teleport Shots ---
    // Called directly by PlayerStateManager when Fire1 is pressed while aiming Teleport
    public void FireTeleportImmediate(Vector3 aimDirection)
    {
        // 1. --- Reference Checks ---
        // Ensure references needed for firing are valid
        if (player == null || player.bulletSpawner == null || firePoint == null)
        {
            Debug.LogError("PlayerShooting.FireTeleportImmediate: Missing required references (Player, BulletSpawner, or FirePoint). Cannot fire.");
            return;
        }

        // 2. --- Bullet Type Check (Safety) ---
        // Double-check the correct bullet type is selected
        if (player.bulletSpawner.CurrentBulletType != BulletType.Type3)
        {
             Debug.LogWarning("FireTeleportImmediate called but Teleport bullet is not the currently selected type.");
             return; // Don't fire if the wrong type is somehow selected
        }
        GameObject prefabToSpawn = player.bulletSpawner.bulletPrefab; // Get current prefab from spawner
        if (prefabToSpawn == null)
        {
            Debug.LogError("FireTeleportImmediate: Teleport bullet prefab is null in BulletSpawnerState!");
            return;
        }

        // 3. --- Mana Check & Deduction ---
        // Use the player's UseMana function to check and spend mana
        if (!player.UseMana(teleportManaCost, player)) // Pass cost and player reference (if needed by UseMana)
        {
            Debug.Log("Not enough mana for immediate teleport shot!");
            // Optional: Play 'failed' sound effect
            return; // Stop execution if not enough mana
        }

        // 4. --- Instantiate & Fire ---
        Debug.Log("Firing Teleport Bullet (Immediate Aim Mode)");

        // Instantiate the bullet at the fire point, rotated towards the aim direction
        GameObject bullet = Instantiate(prefabToSpawn, firePoint.position, Quaternion.LookRotation(aimDirection));

        // Apply force immediately
        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; // Ensure physics is enabled
            // Apply force using the aim direction provided by PlayerStateManager
            rb.AddForce(aimDirection * bulletForce, ForceMode.VelocityChange);
        }
        else
        {
            Debug.LogError("Teleport bullet prefab is missing Rigidbody! Cannot apply force.", prefabToSpawn);
            // If the bullet moves via its own script, that script needs to handle its movement.
        }
        // No charging, no parenting needed. The bullet is fired instantly.
    }


    // --- MODIFIED: Starts the Charging Process for NON-Teleport Bullets ---
    // Called by PlayerStateManager when Fire1 is pressed and NOT aiming Teleport
    public void StartCharge()
    {
        // 1. --- Reference Checks ---
        if (player == null || player.bulletSpawner == null || firePoint == null)
        {
            Debug.LogError("PlayerShooting.StartCharge: Missing required references. Cannot start charge.");
            return;
        }

        // 2. --- Prevent Charge for Teleport ---
        // Teleport bullet uses immediate fire (aimed) or fires on EndCharge (non-aimed). It doesn't "charge".
        if (player.bulletSpawner.CurrentBulletType == BulletType.Type3)
        {
             // This case might happen if player presses Fire1 without aiming.
             // We still instantiate here but don't call any "charging" logic on the bullet itself.
             // EndCharge will handle the actual firing on release.
             Debug.Log("StartCharge called for Teleport (non-aimed). Instantiating, will fire on release.");
             // Proceed to instantiate, but skip bullet-specific charging calls later.
        }
        else if (_isCharging)
        {
            // Prevent starting a new charge if already charging another bullet
            return;
        }


        // 3. --- Determine Mana Cost & Check ---
        int requiredMana = 0;
        bool isTeleportForCostCheck = (player.bulletSpawner.CurrentBulletType == BulletType.Type3);
        switch (player.bulletSpawner.CurrentBulletType)
        {
            case BulletType.Type1: requiredMana = basicManaCostInitial; break;
            case BulletType.Type2: requiredMana = freezeManaCostInitial; break;
            case BulletType.Type3: requiredMana = teleportManaCost; break; // Use teleport cost here too
        }

        // Check mana BEFORE instantiating (unless it's teleport, which checks/deducts later or in immediate fire)
        // Note: For charge bullets, we might only deduct on successful StartCharging call below.
        // For simplicity, let's check here. Teleport mana check is redundant if it happened in ImmediateFire,
        // but required if firing non-aimed via EndCharge.
        if (player.currentMana < requiredMana)
        {
            Debug.Log($"Not enough mana to start firing {player.bulletSpawner.CurrentBulletType}! Need {requiredMana}");
            // Optional: Play 'failed' sound effect
            return; // Stop if not enough mana
        }


        // 4. --- Instantiate the Bullet ---
        GameObject prefabToSpawn = player.bulletSpawner.bulletPrefab;
        if (prefabToSpawn == null)
        {
             Debug.LogError($"Bullet prefab for type {player.bulletSpawner.CurrentBulletType} is null!");
             return;
        }
        currentBullet = Instantiate(prefabToSpawn, firePoint.position, firePoint.rotation);
        if (currentBullet == null)
        {
            Debug.LogError("Failed to instantiate bullet prefab!");
            return;
        }


        // 5. --- Prepare Bullet for Charging / Holding ---
        Rigidbody rb = currentBullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; // Keep it stationary at the fire point during charge/hold
        }
        currentBullet.transform.parent = firePoint; // Attach to fire point visually


        // 6. --- Start Charging State / Logic ---
        chargeStartTime = Time.time;
        _isCharging = true; // Set charging flag

        // 7. --- Call Bullet-Specific StartCharging (If Applicable) & Deduct Initial Cost ---
        // This part is only for bullets that actually have a charging mechanic (e.g., Basic, Freeze)
        BasicBullet basic = currentBullet.GetComponent<BasicBullet>();
        FreezeBullet freeze = currentBullet.GetComponent<FreezeBullet>();
        // We don't need TeleportBullet component here for charging logic.

        bool chargeStartedSuccessfully = false; // Flag to track if mana was deducted etc.

        if (isTeleportForCostCheck)
        {
            // Teleport doesn't "charge", but we mark as successful so EndCharge can fire it.
            // Mana was already checked above. We deduct it here for the non-aimed case.
            if(player.UseMana(requiredMana, player)) {
                 chargeStartedSuccessfully = true;
            } else {
                 // This shouldn't happen due to check above, but safety first.
                 Debug.LogError("Failed to use mana for Teleport in StartCharge after check passed?");
                 chargeStartedSuccessfully = false;
            }

        }
        else if (basic != null)
        {
            // Pass player reference; bullet handles mana drain over time if needed
            if (player.UseMana(requiredMana, player)) // Deduct initial cost NOW
            {
                basic.StartCharging(player); // Tell the basic bullet it's charging
                chargeStartedSuccessfully = true;
            } else { chargeStartedSuccessfully = false; }
        }
        else if (freeze != null)
        {
            // Pass player reference; bullet handles mana drain over time if needed
            if (player.UseMana(requiredMana, player)) // Deduct initial cost NOW
            {
                freeze.StartCharging(player); // Tell the freeze bullet it's charging
                 chargeStartedSuccessfully = true;
            } else { chargeStartedSuccessfully = false; }
        }
        else if (!isTeleportForCostCheck) // If it's not Basic, Freeze, or Teleport
        {
            Debug.LogWarning($"Instantiated bullet prefab {currentBullet.name} has no recognized charging script (Basic, Freeze)!");
            // Assume it's a simple fire-on-release bullet, deduct cost if possible
             if (player.UseMana(requiredMana, player)) {
                 chargeStartedSuccessfully = true;
             } else { chargeStartedSuccessfully = false; }
        }


        // 8. --- Cleanup if Charge Failed ---
        // If mana deduction failed or no valid script found (and wasn't teleport)
        if (!chargeStartedSuccessfully) {
             _isCharging = false; // Reset flag
             Destroy(currentBullet); // Destroy the useless bullet instance
             currentBullet = null;
             Debug.Log("Failed to start charge (likely insufficient mana for initial cost).");
             // Do NOT refund mana here, UseMana handles the check/deduction attempt.
        } else {
             Debug.Log($"Started holding/charging {player.bulletSpawner.CurrentBulletType}");
             // Optional: Update shell visuals immediately?
             // player.UpdateShellCountVisuals(); // May already be handled by UseMana
        }
    }


    // --- MODIFIED: Ends the Charge and Fires NON-Teleport Bullets, OR Fires Held Teleport Bullet ---
    // Called by PlayerStateManager on Fire1 release (if not aiming teleport)
    public void EndCharge()
    {
        // 1. --- Check if Charging ---
        // Only proceed if we were actually charging and have a valid bullet instance
        if (!_isCharging || currentBullet == null)
        {
            _isCharging = false; // Ensure flag is reset just in case
            return; // Nothing to end/fire
        }

        // --- Get Bullet Components ---
        // We need these before resetting _isCharging flag if we check TeleportBullet first
        BasicBullet basic = currentBullet.GetComponent<BasicBullet>();
        FreezeBullet freeze = currentBullet.GetComponent<FreezeBullet>();
        TeleportBullet teleport = currentBullet.GetComponent<TeleportBullet>();


        // 2. --- Handle Held Teleport Bullet Case ---
        // If the bullet we are holding is a Teleport bullet, fire it simply.
        // This happens if Fire1 was pressed/released without aiming.
        if (teleport != null) {
              Debug.Log("EndCharge firing held Teleport bullet (non-aimed).");
               _isCharging = false; // Stop charging state

               currentBullet.transform.parent = null; // Unparent
               Rigidbody rb = currentBullet.GetComponent<Rigidbody>();
               if (rb != null) {
                   rb.isKinematic = false; // Enable physics
                   // Fire straight forward from the fire point
                   rb.AddForce(firePoint.forward * bulletForce, ForceMode.VelocityChange);
               } else { Debug.LogError("Held Teleport bullet missing Rigidbody!"); }

               currentBullet = null; // Release reference
               return; // IMPORTANT: Exit EndCharge early for teleport
        }


        // --- Logic for NON-Teleport Bullets (Basic, Freeze, Other) ---

        // 3. --- Stop Charging State ---
        _isCharging = false;

        // 4. --- Detach and Enable Physics ---
        currentBullet.transform.parent = null; // Unparent from fire point
        Rigidbody _rb = currentBullet.GetComponent<Rigidbody>();
        if (_rb != null)
        {
            _rb.isKinematic = false; // Allow physics to take over
        }
        else
        {
            Debug.LogError($"Charged bullet {currentBullet.name} has no Rigidbody!", currentBullet);
            // Bullet might move via script, but force application below will fail.
        }


        // 5. --- Stop Bullet Charging & Get Modifiers ---
        float chargeMultiplier = 1f; // Default force multiplier (for basic bullets)

        if (basic != null)
        {
            // Call StopCharging on the bullet script, which might return a multiplier
            chargeMultiplier = basic.StopCharging();
        }
        else if (freeze != null)
        {
            // Freeze bullet might not return a multiplier, but needs StopCharging called
            // to finalize its state (e.g., calculate freeze duration based on charge time).
            freeze.StopCharging();
            // chargeMultiplier remains 1f for freeze bullet force.
        }
        // No special StopCharging call needed for other simple bullet types without specific scripts.


        // 6. --- Apply Launch Force ---
        if (_rb != null)
        {
            // Apply multiplier only if applicable (e.g., basic bullet modifies it)
            float finalForce = bulletForce * chargeMultiplier;
            // Apply force straight forward from the fire point's orientation at time of release
            _rb.AddForce(firePoint.forward * finalForce, ForceMode.VelocityChange);
            Debug.Log($"Fired {currentBullet.name} with force multiplier {chargeMultiplier} (Force: {finalForce})");
        }

        // 7. --- Cleanup ---
        currentBullet = null; // Release reference, bullet is now independent
    }
}