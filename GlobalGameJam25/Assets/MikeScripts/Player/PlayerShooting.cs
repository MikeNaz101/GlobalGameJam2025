using UnityEngine;
using Cinemachine; // <-- Make sure this tangy line is here!

public class PlayerShooting : MonoBehaviour
{
    [Header("Core Setup")]
    public Transform firePoint;
    public float bulletForce = 20f;
    public PlayerStateManager player; // Should be assigned automatically if on same object, or assign manually

    [Header("Cinemachine Cameras")]
    public CinemachineVirtualCamera gameplayVCam; // Assign your NORMAL gameplay VCam
    public CinemachineVirtualCamera teleportAimVCam; // Assign your NEW Teleport Aim VCam

    // Store the original low priority of the aim cam
    private int teleportAimVCamOriginalPriority = 0;

    [Header("Mana Costs")]
    public int basicManaCostInitial = 5;
    public int freezeManaCostInitial = 5;
    public int teleportManaCost = 20;

    // Internal State
    private bool _isCharging = false;
    private GameObject currentBullet;
    private float chargeStartTime;
    private bool isUsingTeleportView = false; // Track if we switched VCam priority


    void Start()
    {
        // Null checks for PlayerStateManager and BulletSpawner
        if (player == null) {
            player = GetComponent<PlayerStateManager>(); // Try to get it from the same GameObject
             if (player == null) { Debug.LogError("PlayerShooting: Missing PlayerStateManager reference!"); this.enabled = false; return; }
        }
        if (player.bulletSpawner == null) { Debug.LogError("PlayerShooting: PlayerStateManager is missing its BulletSpawnerState reference!"); this.enabled = false; return; }


        // --- Cinemachine Setup ---
        if (gameplayVCam == null || teleportAimVCam == null) {
            Debug.LogError("PlayerShooting: Assign both Gameplay VCam and Teleport Aim VCam in the Inspector!");
            this.enabled = false; // Disable if cams aren't set
            return;
        }
        // Store the original priority so we can reset it correctly
        teleportAimVCamOriginalPriority = teleportAimVCam.Priority;
        // Ensure the aim cam starts with lower priority (just in case it was changed in editor)
        teleportAimVCam.Priority = teleportAimVCamOriginalPriority;
        Debug.Log($"PlayerShooting Start: TeleportAimVCam Original Priority stored as: {teleportAimVCamOriginalPriority}");
         // --- End Cinemachine Setup ---


        // Link to BulletSpawnerState for cancellation notification
         if (player.bulletSpawner != null) {
            player.bulletSpawner.playerShooting = this;
         } else {
             // This case should be caught by the earlier check, but added for extra safety
             Debug.LogError("PlayerShooting could not link to BulletSpawnerState!");
         }
    }

    // Called by PlayerStateManager when Fire input starts
    public void StartCharge()
    {
        // Prevent issues if required components aren't found during gameplay
        if (player == null || player.bulletSpawner == null || firePoint == null){
            Debug.LogError("PlayerShooting: Missing core references during StartCharge!");
            return;
        }
        if (_isCharging) {
             Debug.LogWarning("StartCharge called while already charging.");
             return; // Already charging
        }


        // Determine mana cost and check type
        int requiredMana = 0;
        bool isTeleportSelected = player.bulletSpawner.CurrentBulletType == BulletType.Type3;
        switch (player.bulletSpawner.CurrentBulletType)
        {
            case BulletType.Type1: requiredMana = basicManaCostInitial; break;
            case BulletType.Type2: requiredMana = freezeManaCostInitial; break;
            case BulletType.Type3: requiredMana = teleportManaCost; break;
        }

        // Check mana *before* doing anything else
        if (player.currentMana < requiredMana)
        {
            Debug.Log("Not enough mana to start firing!");
            // Maybe play a 'fail' sound effect here
            return;
        }

        // --- Activate Teleport VCam Logic ---
        if (isTeleportSelected)
        {
            ActivateTeleportVCamView(); // Attempt to switch view
        }
        // --- End VCam Logic ---

        // Instantiate bullet (rest of the logic is mostly the same)
        currentBullet = Instantiate(player.bulletSpawner.bulletPrefab, firePoint.position, firePoint.rotation);
        if (currentBullet == null)
        {
            Debug.LogError("Failed to instantiate bullet prefab!");
            // If instantiation fails, make sure to reset the camera view if it was activated
            if(isTeleportSelected) DeactivateTeleportVCamView();
            return;
        }

        // Prepare bullet visuals/physics
        Rigidbody rb = currentBullet.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        currentBullet.transform.parent = firePoint; // Attach visually during charge

        // Start charging state *after* checks and instantiation
        chargeStartTime = Time.time;
        _isCharging = true; // Set charging flag *before* mana use/bullet logic

        // Get bullet components and check mana Use for specific types
        BasicBullet basic = currentBullet.GetComponent<BasicBullet>();
        FreezeBullet freeze = currentBullet.GetComponent<FreezeBullet>();
        TeleportBullet teleport = currentBullet.GetComponent<TeleportBullet>();
        bool chargeStartedSuccessfully = false;

        // Use mana and initiate bullet-specific charging
        if (basic != null) { if (player.UseMana(requiredMana, player)) { basic.StartCharging(player); chargeStartedSuccessfully = true; } }
        else if (freeze != null) { if (player.UseMana(requiredMana, player)) { freeze.StartCharging(player); chargeStartedSuccessfully = true; } }
        else if (teleport != null) { if (player.UseMana(requiredMana, player)) { chargeStartedSuccessfully = true; } } // Teleport mana is upfront
        else { Debug.LogError("Instantiated bullet has no recognized bullet script!"); chargeStartedSuccessfully = false; }


        // Cleanup if mana failed *after* instantiation or script missing
        if (!chargeStartedSuccessfully)
        {
            _isCharging = false; // Reset charging flag
            // Ensure camera resets if activation was attempted
             if(isTeleportSelected) DeactivateTeleportVCamView();
            Destroy(currentBullet);
            currentBullet = null;
            Debug.Log("Failed to start charge (likely insufficient mana for chosen type or missing script).");
        }
        else
        {
            Debug.Log($"<color=lime>Successfully Started charging {player.bulletSpawner.CurrentBulletType}</color>");
        }
    }

    // Called by PlayerStateManager when Fire input is released/canceled
    public void EndCharge()
    {
        Debug.Log("<color=yellow>EndCharge CALLED.</color>"); // Check if EndCharge is even running

        // Always try to deactivate the teleport view when releasing the button
        Debug.Log("<color=lightblue>Calling DeactivateTeleportVCamView from EndCharge...</color>");
        DeactivateTeleportVCamView(); // Attempt to switch back

        // Log priorities AFTER attempting the reset
        if(gameplayVCam) Debug.Log($"Priorities after Deactivate attempt: Gameplay = {gameplayVCam.Priority}");
        if(teleportAimVCam) Debug.Log($"Priorities after Deactivate attempt: TeleportAim = {teleportAimVCam.Priority}");


        // Check if we were actually in a valid charging state before proceeding to fire
        if (!_isCharging || currentBullet == null)
        {
            Debug.Log("<color=grey>EndCharge: Was not charging or no current bullet. Resetting flags only.</color>");
             // Ensure flags are reset even if return early, DeactivateTeleportVCamView already handled camera
            _isCharging = false;
            currentBullet = null; // Ensure reference is cleared
            return; // Exit early if we weren't properly charging
        }

         Debug.Log("<color=green>EndCharge: Proceeding with firing logic...</color>");
        _isCharging = false; // Stop charging state

        // --- Bullet Firing Logic ---
        // Detach and Enable Physics
        currentBullet.transform.parent = null;
        Rigidbody rb = currentBullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
        } else {
            Debug.LogError("Charged bullet has no Rigidbody!", currentBullet);
            // Decide how to handle this - maybe destroy bullet?
        }

        // Stop Bullet Charging Components & Get Modifiers (if applicable)
        BasicBullet basic = currentBullet.GetComponent<BasicBullet>();
        FreezeBullet freeze = currentBullet.GetComponent<FreezeBullet>();

        float chargeMultiplier = 1f; // Default force multiplier
        if (basic != null) chargeMultiplier = basic.StopCharging();
        else if (freeze != null) freeze.StopCharging();
        // No special action needed for TeleportBullet on EndCharge itself

        // Apply Launch Force
        if (rb != null)
        {
            float finalForce = bulletForce * chargeMultiplier;
            rb.AddForce(firePoint.forward * finalForce, ForceMode.VelocityChange);
            Debug.Log($"<color=white>Fired {player.bulletSpawner.CurrentBulletType} with force multiplier {chargeMultiplier}</color>");
        }
        // --- End Firing Logic ---

        // Cleanup
        currentBullet = null; // Release reference, bullet is now independent
    }

    // --- VCam Helper Methods ---

    private void ActivateTeleportVCamView()
    {
         Debug.Log($"<color=orange>ActivateTeleportVCamView: Checking conditions... teleportAimVCam is null? = {teleportAimVCam == null}, gameplayVCam is null? = {gameplayVCam == null}, isUsingTeleportView = {isUsingTeleportView}</color>");

        if (teleportAimVCam != null && gameplayVCam != null && !isUsingTeleportView)
        {
            int newPriority = gameplayVCam.Priority + 1; // Calculate the intended priority
            Debug.Log($"<color=orange>ACTIVATING TELEPORT VIEW: Setting TeleportAimVCam Priority from {teleportAimVCam.Priority} to {newPriority}</color>");
            teleportAimVCam.Priority = newPriority;
            isUsingTeleportView = true;
            Debug.Log("<color=lime>ActivateTeleportVCamView: Successfully set isUsingTeleportView = true.</color>"); // ADD THIS
        } else {
             // Log why it didn't activate
             if(isUsingTeleportView) Debug.LogWarning("<color=yellow>ActivateTeleportVCamView called, but view was already active?</color>");
             if(teleportAimVCam == null) Debug.LogError("<color=red>ActivateTeleportVCamView: teleportAimVCam reference is NULL</color>");
             if(gameplayVCam == null) Debug.LogError("<color=red>ActivateTeleportVCamView: gameplayVCam reference is NULL</color>");
        }
    }

    private void DeactivateTeleportVCamView()
    {
        // Log current state *before* the check
        Debug.Log($"<color=cyan>DeactivateTeleportVCamView: Checking conditions... teleportAimVCam is null? = {teleportAimVCam == null}, isUsingTeleportView = {isUsingTeleportView}</color>");

        if (teleportAimVCam != null && isUsingTeleportView)
        {
            // This is the expected path for successful deactivation
            Debug.Log($"<color=cyan>DEACTIVATING Teleport View: Resetting TeleportAimVCam Priority from {teleportAimVCam.Priority} to {teleportAimVCamOriginalPriority}</color>");
            teleportAimVCam.Priority = teleportAimVCamOriginalPriority;
            isUsingTeleportView = false; // Mark view as inactive *after* resetting priority
        }
        else if (teleportAimVCam != null && !isUsingTeleportView)
        {
            // This is the case the user reported - getting called when flag is already false
            Debug.LogWarning("<color=orange>DeactivateTeleportVCamView was called, but 'isUsingTeleportView' flag was already false. Ensuring priority is low anyway.</color>");
            // Ensure priority is still low just in case something weird happened
            teleportAimVCam.Priority = teleportAimVCamOriginalPriority;
        }
         else if (teleportAimVCam == null)
        {
             // Log error if the VCam reference is missing
             Debug.LogError("<color=red>DeactivateTeleportVCamView: teleportAimVCam reference is NULL!</color>");
        }
    }

    // Called externally (e.g., by BulletSpawnerState on weapon switch) to cancel charge & view
    public void CancelCharge()
    {
        Debug.LogError("<color=red>!!! CancelCharge was called! !!!</color>"); // <-- IMPORTANT LOG TO SEE IF THIS IS THE CULPRIT

        // Call deactivate first to ensure camera resets, regardless of charging state
        DeactivateTeleportVCamView();

        if (_isCharging) {
             Debug.Log("<color=red>CancelCharge: Was charging, destroying current bullet.</color>");
             if (currentBullet != null) {
                 Destroy(currentBullet);
                 currentBullet = null;
             }
             _isCharging = false; // Ensure charging stops
         } else {
              Debug.Log("<color=red>CancelCharge: Was not charging, only ensured camera reset.</color>");
         }
    }
}