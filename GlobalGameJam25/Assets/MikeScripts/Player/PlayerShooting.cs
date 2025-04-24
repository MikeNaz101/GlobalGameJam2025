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

    // ----- Aim Offset (Informational Only Now) -----
    // This value no longer affects the bullet trajectory angle with the current setup,
    // but could be used for other calculations if needed (e.g., max range effects).
    [Tooltip("Informational: How many units forward from the raycast hit point the target *would* be calculated (currently does not affect fire angle).")]
    public float aimForwardOffset = 1.0f;
    // -----------------------------------------------


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
        if (mainCamera == null) { Debug.LogError("PlayerShooting Error: Main Camera not found!", this); this.enabled = false; return; }
        Debug.Log($"PlayerShooting Start: Main Camera '{mainCamera.name}' cached successfully.");

        // --- Get Player State Manager ---
        if (player == null) player = GetComponentInParent<PlayerStateManager>(); // Try to find if not assigned
        if (player == null || player.bulletSpawner == null) { Debug.LogError("PlayerShooting Error: Missing PlayerStateManager or BulletSpawner reference!", this); this.enabled = false; return; }
        Debug.Log("PlayerShooting Start: PlayerStateManager and BulletSpawner references seem valid.");

        // --- Cinemachine Setup ---
        if (gameplayVCam == null || teleportAimVCam == null) { Debug.LogError("PlayerShooting Error: Gameplay or Teleport VCam not assigned!", this); this.enabled = false; return; }
        teleportAimVCamOriginalPriority = teleportAimVCam.Priority;
        if (teleportAimVCam.Priority >= gameplayVCam.Priority) { Debug.LogWarning("TeleportAim VCam priority might be too high initially. Ensure it's lower than Gameplay VCam.", this); teleportAimVCam.Priority = gameplayVCam.Priority - 1; teleportAimVCamOriginalPriority = teleportAimVCam.Priority; }
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
        // This still uses the direct raycast hit point, which is usually desired for the visual indicator.
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
            case BulletType.Type1: requiredMana = basicManaCostInitial; break;
            case BulletType.Type2: requiredMana = freezeManaCostInitial; break;
            case BulletType.Type3: requiredMana = teleportManaCost; break;
            default: Debug.LogError($"StartCharge Error: Unhandled BulletType: {currentType}"); return;
        }

        // --- Check Resources ---
        if (player.currentMana < requiredMana)
        {
            Debug.Log($"StartCharge: Not enough mana for {currentType}. Need {requiredMana}, Have {player.currentMana}");
            return;
        }

        // --- Activate Aiming Mode (if needed) ---
        if (isTeleportSelected) { ActivateTeleportVCamView(); }

        // --- Instantiate Bullet ---
        if (player.bulletSpawner.bulletPrefab == null)
        {
             Debug.LogError($"StartCharge Error: Bullet Prefab for {currentType} is null in BulletSpawner!");
             if(isTeleportSelected) DeactivateTeleportVCamView();
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
        if (rb != null) rb.isKinematic = true;
        currentBullet.transform.parent = firePoint;

        // --- Start Charging State ---
        chargeStartTime = Time.time;
        _isCharging = true;

        // --- Handle Bullet-Specific Logic & Mana Use ---
        BasicBullet basic = currentBullet.GetComponent<BasicBullet>();
        FreezeBullet freeze = currentBullet.GetComponent<FreezeBullet>();
        TeleportBullet teleport = currentBullet.GetComponent<TeleportBullet>();
        bool chargeStartedSuccessfully = false;

        // Mana is deducted, then specific bullet logic starts
        if (player.UseMana(requiredMana))
        {
             if (basic != null) { basic.StartCharging(player); chargeStartedSuccessfully = true; }
             else if (freeze != null) { freeze.StartCharging(player); chargeStartedSuccessfully = true; }
             else if (teleport != null) { /* Teleport might not need StartCharging */ chargeStartedSuccessfully = true; }
             else { Debug.LogError($"StartCharge Error: Instantiated bullet '{currentBullet.name}' has no recognized bullet script!"); chargeStartedSuccessfully = false; } // Ensure flag is false if no script found
        }
        else
        {
            chargeStartedSuccessfully = false; // Mana check failed
        }


        // --- Handle Failed Charge Start ---
        if (!chargeStartedSuccessfully)
        {
            _isCharging = false;
            if(isTeleportSelected) DeactivateTeleportVCamView();
            // Mana was either not deducted (UseMana failed) or was deducted but script missing.
            // Decide if mana should be refunded if script was missing.
            if (currentBullet != null) Destroy(currentBullet); // Destroy if instantiated
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
        DeactivateTeleportVCamView(); // Deactivate view and hide UI

        // --- Check Charging State ---
        if (!_isCharging || currentBullet == null) { _isCharging = false; currentBullet = null; return; }

        _isCharging = false;

        // --- Prepare Bullet for Firing ---
        currentBullet.transform.parent = null;
        Rigidbody rb = currentBullet.GetComponent<Rigidbody>();
        if (rb == null) { Debug.LogError("Fired bullet is missing Rigidbody!", currentBullet); Destroy(currentBullet); currentBullet = null; return; }
        rb.isKinematic = false;

        // --- Handle Bullet-Specific Stop Logic ---
        BasicBullet basic = currentBullet.GetComponent<BasicBullet>(); FreezeBullet freeze = currentBullet.GetComponent<FreezeBullet>();
        float chargeMultiplier = 1f; // Force multiplier from charging
        if (basic != null) { chargeMultiplier = basic.StopCharging(); }
        else if (freeze != null) { freeze.StopCharging(); }


        // ===== Calculate Fire Direction (Towards OFFSET Target Point) =====
        Vector3 fireDirection;
        BulletType firedBulletType = player.bulletSpawner.CurrentBulletType;

        if (mainCamera == null) {
             Debug.LogError("Cannot aim raycast: Main Camera missing!");
             fireDirection = firePoint.forward; // Fallback
        } else {
            // Get the ray representing the camera's center view
            Ray aimRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hitInfo;
            Vector3 initialTargetPoint; // Where the crosshair visually hits

            // Visualize Aim Ray (where the player THINKS they are aiming)
            Debug.DrawRay(aimRay.origin, aimRay.direction * aimRaycastRange, Color.yellow, 3.0f);

            // --- Perform Raycast WITH LAYER MASK to find the initial target ---
            if (Physics.Raycast(aimRay, out hitInfo, aimRaycastRange, aimRaycastLayerMask)) {
                initialTargetPoint = hitInfo.point;
            } else {
                initialTargetPoint = aimRay.GetPoint(aimRaycastRange); // Point far away if no hit
            }

            // --- Calculate the OFFSET target point ---
            // Push the target point further along the CAMERA'S aim direction
            Vector3 offsetTargetPoint = initialTargetPoint + aimRay.direction * aimForwardOffset;
            // ------------------------------------------

            // --- Calculate final fire direction FROM firePoint TO the OFFSET target point ---
            fireDirection = (offsetTargetPoint - firePoint.position).normalized;
            // ----------------------------------------------------------------------------

            // --- Debug Visualizations ---
            // Yellow Ray: Camera aim line
            // Debug.DrawRay(aimRay.origin, aimRay.direction * aimRaycastRange, Color.yellow, 3.0f); // Already drawn above

            // Magenta Line: From Fire Point to the actual OFFSET point the bullet is aimed towards
            Debug.DrawLine(firePoint.position, offsetTargetPoint, Color.magenta, 3.0f);

            // Cyan Ray: The actual path the bullet will take (along the magenta line's direction)
            Debug.DrawRay(firePoint.position, fireDirection * 20f, Color.cyan, 3.0f);
        }
        // ===== End Calculate Fire Direction =====


        // --- Apply Launch Force ---
        if (rb != null) {
            float finalForce = bulletForce * chargeMultiplier;
            // Apply force ALONG the calculated fireDirection (towards offset target)
            rb.AddForce(fireDirection * finalForce, ForceMode.VelocityChange);
            Debug.Log($"<color=white>Fired {firedBulletType} TOWARDS offset target point with force mult {chargeMultiplier}. Dir: {fireDirection}</color>");
        }
        // --- End Firing Logic ---

        // --- Cleanup ---
        currentBullet = null;
    }


    // --- VCam & UI Helper Methods ---
    private void ActivateTeleportVCamView()
    {
        if (teleportAimVCam != null && gameplayVCam != null && !isUsingTeleportView) {
            int newPriority = gameplayVCam.Priority + 1;
            teleportAimVCam.Priority = newPriority;
            isUsingTeleportView = true;
            if (teleportAimUI != null) { teleportAimUI.SetActive(true); }
        }
    }

    private void DeactivateTeleportVCamView()
    {
        if (teleportAimUI != null) { if(teleportAimUI.activeSelf) teleportAimUI.SetActive(false); }
        if (teleportAimVCam != null && isUsingTeleportView) { // Check isUsingTeleportView
            teleportAimVCam.Priority = teleportAimVCamOriginalPriority;
            isUsingTeleportView = false; // Set flag *after* changing priority
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

    // Helper for debug drawing sphere (optional - can be removed if not needed)
    void DebugDrawSphere(Vector3 center, float radius, Color color, float duration)
    {
        int segments = 10;
        float angleStep = 360f / segments;
        Vector3 lastPointXY = center + new Vector3(Mathf.Cos(0) * radius, Mathf.Sin(0) * radius, 0);
        Vector3 lastPointXZ = center + new Vector3(Mathf.Cos(0) * radius, 0, Mathf.Sin(0) * radius);
        Vector3 lastPointYZ = center + new Vector3(0, Mathf.Cos(0) * radius, Mathf.Sin(0) * radius);

        for (int i = 1; i <= segments; i++)
        {
            float rad = Mathf.Deg2Rad * (i * angleStep);
            Vector3 currentPointXY = center + new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0);
            Vector3 currentPointXZ = center + new Vector3(Mathf.Cos(rad) * radius, 0, Mathf.Sin(rad) * radius);
            Vector3 currentPointYZ = center + new Vector3(0, Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
            Debug.DrawLine(lastPointXY, currentPointXY, color, duration);
            Debug.DrawLine(lastPointXZ, currentPointXZ, color, duration);
            Debug.DrawLine(lastPointYZ, currentPointYZ, color, duration);
            lastPointXY = currentPointXY;
            lastPointXZ = currentPointXZ;
            lastPointYZ = currentPointYZ;
        }
    }

} // End of PlayerShooting class