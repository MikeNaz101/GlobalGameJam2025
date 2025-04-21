using UnityEngine;
using Cinemachine; // Make sure you have Cinemachine installed and this line is present

public class PlayerShooting : MonoBehaviour
{
    [Header("Core Setup")]
    public Transform firePoint;         // Where the bullet visually spawns from
    public float bulletForce = 20f;     // Base force for launching bullets
    public PlayerStateManager player;   // Reference to the player state manager (ensure this is assigned)

    [Header("Cinemachine Cameras")]
    public CinemachineVirtualCamera gameplayVCam;     // Assign your NORMAL gameplay VCam here
    public CinemachineVirtualCamera teleportAimVCam;  // Assign your Teleport Aim VCam here

    [Header("Aiming & UI")]
    public GameObject teleportAimUI;    // Assign your aiming UI GameObject (Image, Panel, etc.)
    public float aimRaycastRange = 100f;// How far the aiming raycast checks
    // --- IMPORTANT: Configure this LayerMask in the Inspector! ---
    [Tooltip("Set this mask to exclude Player, Weapon, AND the layer your bullet prefab is on!")]
    public LayerMask aimRaycastLayerMask = ~0; // Default to 'Everything', MUST BE CONFIGURED IN INSPECTOR!
    // --- ---
    public ParticleSystem aimingSpotEffect; // Assign a Particle System prefab/instance here


    [Header("Mana Costs")]
    public int basicManaCostInitial = 5;
    public int freezeManaCostInitial = 10;
    public int teleportManaCost = 20;

    // --- Internal State Variables ---
    private bool _isCharging = false;           // Is the player holding the fire button?
    private GameObject currentBullet;           // Reference to the bullet being charged/held
    private float chargeStartTime;              // Time when charging started (for potential future use)
    private bool isUsingTeleportView = false;   // Tracks if the teleport VCam/UI should be active
    private Camera mainCamera;                  // Cached reference to the main rendering camera
    private int teleportAimVCamOriginalPriority = 0; // Stores the teleport VCam's default low priority
    private ParticleSystem.EmissionModule aimingSpotEmission; // Cache emission module for aiming effect


    // Called once when the script instance is first enabled
    void Start()
    {
        // --- Cache Main Camera ---
        mainCamera = Camera.main;
        if (mainCamera == null) { /* ... Error Handling ... */ this.enabled = false; return; }
        Debug.Log($"PlayerShooting Start: Main Camera '{mainCamera.name}' cached successfully.");

        // --- Get Player State Manager ---
        if (player == null) { /* ... (Try GetComponent / GetComponentInParent) ... */ }
        if (player == null || player.bulletSpawner == null) { /* ... Error handling ... */ this.enabled = false; return; }
        Debug.Log("PlayerShooting Start: PlayerStateManager and BulletSpawner references seem valid.");

        // --- Cinemachine Setup ---
        if (gameplayVCam == null || teleportAimVCam == null) { /* ... Error handling ... */ this.enabled = false; return; }
        teleportAimVCamOriginalPriority = teleportAimVCam.Priority;
        if (teleportAimVCam.Priority >= gameplayVCam.Priority) { /* ... (Warn and fix priority) ... */ }
        else { Debug.Log($"PlayerShooting Start: VCam Priorities OK. TeleportAim Original: {teleportAimVCamOriginalPriority}, Gameplay: {gameplayVCam.Priority}"); }

        // --- UI Setup ---
        if (teleportAimUI != null) { teleportAimUI.SetActive(false); }
        else { Debug.LogWarning("PlayerShooting Start: Teleport Aim UI is not assigned."); }

        // --- Aiming Spot Particle Setup ---
        if (aimingSpotEffect != null) {
            aimingSpotEmission = aimingSpotEffect.emission; aimingSpotEmission.enabled = false;
            Debug.Log("Aiming Spot Effect initialized.");
        } else { Debug.LogWarning("Aiming Spot Effect is not assigned."); }

        // --- Link to Bullet Spawner ---
         if (player.bulletSpawner != null) { player.bulletSpawner.playerShooting = this; }
         else { Debug.LogError("PlayerShooting Start Error: Could not link to BulletSpawnerState!"); }

        // --- ADDED DEBUG LOG FOR LAYERMASK VALUE ---
        Debug.Log($"PlayerShooting Start: Aim Raycast LayerMask VALUE = {aimRaycastLayerMask.value}. Ensure layers to IGNORE are NOT included in this value's bitmask.");
        // ---

         // --- Reset Flags ---
         isUsingTeleportView = false;
         _isCharging = false;
         currentBullet = null;
         Debug.Log("PlayerShooting Start: Initialization Complete.");
    }


    void Update()
    {
        // --- Aiming Spot Particle Update ---
        if (aimingSpotEffect != null && mainCamera != null)
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hit;
            // Use the configured LayerMask here too!
            if (Physics.Raycast(ray, out hit, aimRaycastRange, aimRaycastLayerMask))
            {
                aimingSpotEffect.transform.position = hit.point;
                aimingSpotEffect.transform.rotation = Quaternion.LookRotation(hit.normal);
                if (!aimingSpotEmission.enabled) { aimingSpotEmission.enabled = true; }
            }
            else
            {
                if (aimingSpotEmission.enabled) { aimingSpotEmission.enabled = false; }
            }
        }
    }


    // Called by PlayerStateManager (or input system) when the Fire action starts
    public void StartCharge()
    {
        // --- Pre-Charge Checks ---
        if (player == null || player.bulletSpawner == null || firePoint == null){ Debug.LogError("StartCharge Error: Missing Player, Spawner, or FirePoint refs."); return; }
        if (_isCharging) { Debug.LogWarning("StartCharge: Already charging."); return; }

        // --- Determine Requirements ---
        int requiredMana = 0;
        BulletType currentType = player.bulletSpawner.CurrentBulletType;
        bool isTeleportSelected = currentType == BulletType.Type3; // Assuming Type3 is Teleport

        switch (currentType)
        {
            case BulletType.Type1: // Basic
                requiredMana = basicManaCostInitial;
                break;
            case BulletType.Type2: // Freeze
                requiredMana = freezeManaCostInitial;
                break;
            case BulletType.Type3: // Teleport
                requiredMana = teleportManaCost;
                break;
            default:
                Debug.LogError($"StartCharge Error: Unhandled BulletType: {currentType}");
                return;
        }

        // --- Check Resources ---
        if (player.currentMana < requiredMana)
        {
            Debug.Log($"StartCharge: Not enough mana for {currentType}. Need {requiredMana}, Have {player.currentMana}");
            // Optionally provide player feedback here (sound, UI)
            return;
        }

        // --- Activate Aiming Mode (if needed) ---
        if (isTeleportSelected)
        {
            ActivateTeleportVCamView();
        }

        // --- Instantiate Bullet ---
        // Ensure prefab is valid before instantiating
        if (player.bulletSpawner.bulletPrefab == null)
        {
             Debug.LogError($"StartCharge Error: Bullet Prefab for {currentType} is null in BulletSpawner!");
             if(isTeleportSelected) DeactivateTeleportVCamView(); // Clean up VCam if activated
             return;
        }
        currentBullet = Instantiate(player.bulletSpawner.bulletPrefab, firePoint.position, firePoint.rotation);
        if (currentBullet == null)
        {
             Debug.LogError($"StartCharge Error: Failed to Instantiate bullet prefab for {currentType}");
             if(isTeleportSelected) DeactivateTeleportVCamView();
             return;
        }

        // --- Prepare Bullet ---
        Rigidbody rb = currentBullet.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true; // Make it kinematic initially
        currentBullet.transform.parent = firePoint; // Attach to fire point during charge

        // --- Start Charging State ---
        chargeStartTime = Time.time;
        _isCharging = true;

        // --- Handle Bullet-Specific Logic & Mana Use ---
        BasicBullet basic = currentBullet.GetComponent<BasicBullet>();
        FreezeBullet freeze = currentBullet.GetComponent<FreezeBullet>();
        TeleportBullet teleport = currentBullet.GetComponent<TeleportBullet>();
        bool chargeStartedSuccessfully = false;

        // --- CORRECTED UseMana CALLS ---
        if (basic != null)
        {
            // Call UseMana with ONLY the mana cost
            if (player.UseMana(requiredMana))
            {
                basic.StartCharging(player); // Pass player ref to bullet script if needed
                chargeStartedSuccessfully = true;
            }
        }
        else if (freeze != null)
        {
            // Call UseMana with ONLY the mana cost
            if (player.UseMana(requiredMana))
            {
                freeze.StartCharging(player); // Pass player ref to bullet script if needed
                chargeStartedSuccessfully = true;
            }
        }
        else if (teleport != null)
        {
            // Call UseMana with ONLY the mana cost
            // Teleport might not need a StartCharging method, but still consumes mana on start
            if (player.UseMana(requiredMana))
            {
                chargeStartedSuccessfully = true;
            }
        }
        else
        {
            Debug.LogError($"StartCharge Error: Instantiated bullet '{currentBullet.name}' has no recognized bullet script (Basic, Freeze, Teleport)!");
            chargeStartedSuccessfully = false; // Ensure failure state
        }
        // --- END OF CORRECTIONS ---


        // --- Handle Failed Charge Start (e.g., mana check failed inside UseMana) ---
        if (!chargeStartedSuccessfully)
        {
            _isCharging = false; // Reset charging flag
            if(isTeleportSelected) DeactivateTeleportVCamView(); // Clean up VCam if activated
            Destroy(currentBullet); // Destroy the unused bullet
            currentBullet = null;
            Debug.Log($"StartCharge: Failed to start charge for {currentType} (Likely due to UseMana returning false or missing script).");
        }
        else
        {
            Debug.Log($"<color=lime>Started charging {currentType}. Mana deducted: {requiredMana}</color>");
        }
    }

    // Called by PlayerStateManager (or input system) when the Fire action is released/canceled
    public void EndCharge()
    {
        // --- Initial Logs & Deactivation ---
        Debug.Log("<color=yellow>EndCharge CALLED.</color>");
        DeactivateTeleportVCamView(); // Deactivate view and hide UI
        // ... (Log priorities for debugging) ...

        // --- Check Charging State ---
        if (!_isCharging || currentBullet == null) { _isCharging = false; currentBullet = null; return; }

        Debug.Log("<color=green>EndCharge: Proceeding with firing logic...</color>");
        _isCharging = false;

        // --- Prepare Bullet for Firing ---
        currentBullet.transform.parent = null;
        Rigidbody rb = currentBullet.GetComponent<Rigidbody>();
        if (rb == null) { /* ... handle error ... */ Destroy(currentBullet); currentBullet = null; return; }
        rb.isKinematic = false;

        // --- Handle Bullet-Specific Stop Logic ---
        BasicBullet basic = currentBullet.GetComponent<BasicBullet>(); FreezeBullet freeze = currentBullet.GetComponent<FreezeBullet>();
        float chargeMultiplier = 1f;
        if (basic != null) { chargeMultiplier = basic.StopCharging(); }
        else if (freeze != null) { freeze.StopCharging(); }

        // --- Calculate Fire Direction (Raycast for ALL Types) ---
        Vector3 fireDirection;
        BulletType firedBulletType = player.bulletSpawner.CurrentBulletType;

        Debug.Log($"Calculating Aim Direction for {firedBulletType} via Raycast...");
        if (mainCamera == null) {
             Debug.LogError("Cannot aim raycast: Main Camera missing!");
             fireDirection = firePoint.forward; // Fallback
        } else {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hit;
            Vector3 targetPoint;

            // Visualize Aim Ray
            Debug.DrawRay(ray.origin, ray.direction * aimRaycastRange, Color.yellow, 2.0f);

            // --- Perform Raycast WITH LAYER MASK ---
            // **** ENSURE 'aimRaycastLayerMask' IS CONFIGURED IN INSPECTOR ****
            if (Physics.Raycast(ray, out hit, aimRaycastRange, aimRaycastLayerMask)) { // USE THE MASK!
                targetPoint = hit.point;
                Debug.Log($"Aim Raycast Hit: {hit.collider.name} (Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}) at {targetPoint}, Dist: {hit.distance}");
            } else {
                targetPoint = ray.GetPoint(aimRaycastRange);
                Debug.Log($"Aim Raycast Missed (Range: {aimRaycastRange}m). Targeting point far away.");
            }

            // Calculate direction FROM firePoint TO targetPoint
            fireDirection = (targetPoint - firePoint.position).normalized;
            Debug.Log($"Calculated Fire Direction: {fireDirection}");

            // Visualize Fire Direction
            Color debugColor = (firedBulletType == BulletType.Type3) ? Color.red : Color.green;
            Debug.DrawRay(firePoint.position, fireDirection * 10f, debugColor, 2.0f);
        }
        // --- End Calculate Fire Direction ---


        // --- Apply Launch Force ---
        if (rb != null) {
            float finalForce = bulletForce * chargeMultiplier;
            rb.AddForce(fireDirection * finalForce, ForceMode.VelocityChange); // Use calculated fireDirection
            Debug.Log($"<color=white>Fired {firedBulletType} with force mult {chargeMultiplier} in dir {fireDirection}</color>");
        }
        // --- End Firing Logic ---

        // --- Cleanup ---
        currentBullet = null;
    }


    // --- VCam & UI Helper Methods --- (No changes needed here)
    private void ActivateTeleportVCamView()
    {
        if (teleportAimVCam != null && gameplayVCam != null && !isUsingTeleportView) {
            int newPriority = gameplayVCam.Priority + 1;
            teleportAimVCam.Priority = newPriority;
            isUsingTeleportView = true;
            if (teleportAimUI != null) { teleportAimUI.SetActive(true); }
            Debug.Log("<color=lime>Activated Teleport View & UI.</color>");
        }
    }

    private void DeactivateTeleportVCamView()
    {
        if (teleportAimUI != null) { if(teleportAimUI.activeSelf) teleportAimUI.SetActive(false); }
        if (teleportAimVCam != null) {
            if (isUsingTeleportView) { Debug.Log("<color=cyan>Deactivating Teleport View.</color>"); }
            teleportAimVCam.Priority = teleportAimVCamOriginalPriority;
            isUsingTeleportView = false;
        }
    }

     public void CancelCharge()
    {
        Debug.LogError("<color=red>!!! CancelCharge was explicitly called! !!!</color>");
        DeactivateTeleportVCamView(); // Handles UI and Camera reset
        if (_isCharging) {
             if (currentBullet != null) { Destroy(currentBullet); currentBullet = null; }
             _isCharging = false;
        } else { Debug.Log("<color=red>CancelCharge: Was not charging.</color>"); }
    }

} // End of PlayerShooting class