using UnityEngine;
using Cinemachine;
using System.Collections;

public class PlayerShooting : MonoBehaviour
{
    [Header("Core Setup")]
    public Transform firePoint;
    public float bulletForce = 20f;     // Base force for launching bullets (at zero charge)
    public PlayerStateManager player;   // Reference to the player state manager

    [Header("Cinemachine Cameras")]
    public CinemachineVirtualCamera gameplayVCam;
    public CinemachineVirtualCamera teleportAimVCam;

    [Header("Aiming & UI")]
    public GameObject teleportAimUI;
    public float aimRaycastRange = 100f;
    [Tooltip("Set this mask to exclude Player, Weapon, AND the layer your bullet prefab is on!")]
    public LayerMask aimRaycastLayerMask = ~0;
    public ParticleSystem aimingSpotEffect;
    [Tooltip("Informational: How many units forward from the raycast hit point the target *would* be calculated (currently does not affect fire angle).")]
    public float aimForwardOffset = 1.0f;

    [Header("Charging Mechanics")]
    [Tooltip("Time in seconds to reach maximum charge.")]
    public float maxChargeTime = 2.0f;
    [Tooltip("Force multiplier applied at maximum charge (1.0 = base force).")]
    public float maxForceMultiplier = 2.5f;
    [Tooltip("Mana cost multiplier applied at maximum charge (1.0 = base cost).")]
    public float maxManaCostMultiplier = 3.0f;
    [Tooltip("Optional: Scale the bullet visual while charging.")]
    public bool scaleVisualWithCharge = true;
    [Tooltip("Maximum scale multiplier for the visual at full charge.")]
    public float maxVisualScaleMultiplier = 1.5f;


    // --- Base Mana Costs (Reference PlayerStateManager) ---
    // Add these INSIDE PlayerShooting.cs if they aren't already there
    [Header("Mana Costs")] // Or group them with Charging Mechanics
    [Tooltip("Base mana cost for a non-charged basic shot.")]
    public int basicManaCostInitial = 5;
    [Tooltip("Base mana cost for a non-charged freeze shot.")]
    public int freezeManaCostInitial = 10;
    [Tooltip("Base mana cost for a non-charged teleport shot.")]
    public int teleportManaCost = 20;

    // --- Internal State Variables ---
    private bool _isCharging = false;           // Is the player holding the fire button?
    private GameObject currentBulletVisual;     // Visual representation while charging
    private float chargeStartTime;              // Time when charging started
    private Vector3 initialBulletScale;         // Store initial scale for visual charging

    private bool isUsingTeleportView = false;
    private Camera mainCamera;
    private int teleportAimVCamOriginalPriority = 0;
    private ParticleSystem.EmissionModule aimingSpotEmission;
    public PlayerAnimationManager animationManager;

    void Start()
    {
        // --- Cache components and references ---
        mainCamera = Camera.main;
        if (mainCamera == null) { Debug.LogError("PlayerShooting Error: Main Camera not found!", this); this.enabled = false; return; }
        if (player == null) player = GetComponentInParent<PlayerStateManager>();
        if (player == null || player.bulletSpawner == null) { Debug.LogError("PlayerShooting Error: Missing PlayerStateManager or BulletSpawner reference!", this); this.enabled = false; return; }
        if (gameplayVCam == null || teleportAimVCam == null) { Debug.LogError("PlayerShooting Error: Gameplay or Teleport VCam not assigned!", this); this.enabled = false; return; }
        if (animationManager == null) animationManager = player.GetComponentInChildren<PlayerAnimationManager>(); // Try to find anim manager

        // --- Cinemachine & UI Setup ---
        teleportAimVCamOriginalPriority = teleportAimVCam.Priority;
        if (teleportAimVCam.Priority >= gameplayVCam.Priority) { teleportAimVCam.Priority = gameplayVCam.Priority - 1; teleportAimVCamOriginalPriority = teleportAimVCam.Priority; }
        if (teleportAimUI != null) { teleportAimUI.SetActive(false); }
        if (aimingSpotEffect != null) { aimingSpotEmission = aimingSpotEffect.emission; aimingSpotEmission.enabled = false; }

        // --- Link & Reset State ---
        if (player.bulletSpawner != null) { player.bulletSpawner.playerShooting = this; }
        isUsingTeleportView = false;
        _isCharging = false;
        currentBulletVisual = null;
    }


    void Update()
    {
        // --- Aiming Spot Particle Update (same as before) ---
        if (aimingSpotEffect != null && mainCamera != null)
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hit;
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

        // --- Charging Visual Update ---
        if (_isCharging && currentBulletVisual != null && scaleVisualWithCharge)
        {
            float chargeRatio = Mathf.Clamp01((Time.time - chargeStartTime) / maxChargeTime);
            float targetScaleMultiplier = Mathf.Lerp(1.0f, maxVisualScaleMultiplier, chargeRatio);
            currentBulletVisual.transform.localScale = initialBulletScale * targetScaleMultiplier;
            // You could also update particle emission rates or colors here based on chargeRatio
        }
    }


    // Called by PlayerStateManager (or input system) when the Fire action STARTS (button press)
    public void StartCharge()
    {
        if (player == null || player.bulletSpawner == null || firePoint == null) return;
        if (_isCharging) { Debug.LogWarning("StartCharge: Already charging."); return; }

        BulletType currentType = player.bulletSpawner.CurrentBulletType;
        bool isTeleportSelected = currentType == BulletType.Type3; // Assuming Type3 is Teleport

        // --- Check if player CAN fire *any* bullet (Optional - check base cost) ---
        // int baseManaCost = GetBaseManaCost(currentType);
        // if (player.currentMana < baseManaCost) {
        //     Debug.Log($"StartCharge: Not enough mana for even a basic shot ({currentType}). Need {baseManaCost}, Have {player.currentMana}");
        //     // Optionally play "no mana" sound
        //     return; // Prevent starting charge if cannot fire basic shot
        // }
        // Decided against checking mana here - check on release instead.

        // --- Instantiate Bullet Visual ---
        if (player.bulletSpawner.bulletPrefab == null) { Debug.LogError($"StartCharge Error: Bullet Prefab for {currentType} is null!"); return; }
        currentBulletVisual = Instantiate(player.bulletSpawner.bulletPrefab, firePoint.position, firePoint.rotation);
        if (currentBulletVisual == null) { Debug.LogError($"StartCharge Error: Failed to Instantiate bullet prefab for {currentType}"); return; }

        // --- Store initial scale for visual charging ---
        initialBulletScale = currentBulletVisual.transform.localScale;

        // --- Make Visual Non-functional ---
        Rigidbody rb = currentBulletVisual.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        Collider col = currentBulletVisual.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        // Disable bullet scripts if necessary (optional)
        // ...

        currentBulletVisual.transform.parent = firePoint;

        // --- Activate Aiming Mode (if needed) ---
        if (isTeleportSelected) { ActivateTeleportVCamView(); }

        // --- Start Charging State & Sound ---
        _isCharging = true;
        chargeStartTime = Time.time;
        player.PlayLoopingSound(player.chargeLoopSound); // Start the loop

        Debug.Log($"<color=yellow>Started charging {currentType}.</color>");
    }


    // Called by PlayerStateManager (or input system) when the Fire action ENDS (button release)
    public void EndCharge()
    {
        // --- Stop Aiming/Charging Effects ---
        DeactivateTeleportVCamView();
        player.StopLoopingSound(); // Stop the charging sound loop

        // --- Validate State ---
        if (!_isCharging || currentBulletVisual == null)
        {
            // This can happen if StartCharge failed or if EndCharge gets called erroneously
            _isCharging = false; // Ensure flag is reset
            if(currentBulletVisual != null) { Destroy(currentBulletVisual); currentBulletVisual = null;} // Cleanup stray visual
            return;
        }

        // --- Calculate Charge Level & Multipliers ---
        float chargeDuration = Time.time - chargeStartTime;
        // Clamp01 ensures value is between 0 (no charge) and 1 (full charge)
        float chargeRatio = Mathf.Clamp01(chargeDuration / maxChargeTime);

        float actualForceMultiplier = Mathf.Lerp(1.0f, maxForceMultiplier, chargeRatio);
        float actualManaCostMultiplier = Mathf.Lerp(1.0f, maxManaCostMultiplier, chargeRatio);

        // --- Get Base Mana Cost and Calculate Final Cost ---
        BulletType currentType = player.bulletSpawner.CurrentBulletType;
        int baseManaCost = GetBaseManaCost(currentType);
        int finalManaCost = Mathf.RoundToInt(baseManaCost * actualManaCostMultiplier);

        Debug.Log($"Charge Ended: Duration={chargeDuration:F2}s, Ratio={chargeRatio:P0}, ForceMult={actualForceMultiplier:F2}, ManaCost={finalManaCost}");
        
        Debug.Log($"Checking Mana: Current Mana = {player.currentMana}, Base Cost = {baseManaCost}, Charge Ratio = {chargeRatio:P0}, Cost Multiplier = {actualManaCostMultiplier:F2}, Final Cost = {finalManaCost}");
        // --- Check Mana & Fire ---
        if (player.UseMana(finalManaCost)) // UseMana checks if currentMana >= finalManaCost
        {
            Debug.Log("<color=lime>Firing Branch Entered (UseMana returned TRUE)</color>");
            // SUCCESS: Enough mana for the charged shot
            Debug.Log($"<color=cyan>Firing {currentType}! Mana Cost: {finalManaCost}</color>");

            // --- Prepare Visual for Firing ---
            currentBulletVisual.transform.parent = null; // Unparent
            currentBulletVisual.transform.localScale = initialBulletScale * Mathf.Lerp(1.0f, maxVisualScaleMultiplier, chargeRatio); // Set final scale just in case

            Rigidbody rb = currentBulletVisual.GetComponent<Rigidbody>();
            Collider col = currentBulletVisual.GetComponent<Collider>();
            // Re-enable components if they were disabled in StartCharge
            // ...

            if (rb != null) rb.isKinematic = false; // Enable physics
            if (col != null) col.enabled = true; // Enable collisions


            // --- Animation & Sound ---
            animationManager?.TriggerShoot();
            player.PlaySoundOneShot(player.shootSound); // Play the shooting sound

            // --- Calculate Fire Direction (same as before) ---
            Vector3 fireDirection = CalculateFireDirection();

            // --- Apply Launch Force (with multiplier) ---
            var bulletScript = currentBulletVisual.GetComponent<IBulletChargeReceiver>(); // Get the script using the interface
            if (bulletScript != null)
            {
                // Pass chargeRatio, forceMultiplier, AND the player reference
                bulletScript.OnFire(chargeRatio, actualForceMultiplier, player);
            }

            // Apply Launch Force (code from before)
            if (rb != null)
            {
                rb.AddForce(fireDirection * bulletForce * actualForceMultiplier, ForceMode.VelocityChange);
            }

        }
        else
        {
            // FAILURE: Not enough mana for the calculated cost
            Debug.Log($"<color=red>Fire Failed! Not enough mana for {currentType}. Need {finalManaCost}, Have {player.currentMana}</color>");
            // Play failure sound/effect (optional)
            // player.PlaySoundOneShot(player.chargeFailSound);

            // Destroy the unused visual
            Destroy(currentBulletVisual);
        }

        // --- Reset State ---
        _isCharging = false;
        currentBulletVisual = null;
        chargeStartTime = 0f;
    }


    // --- Helper Methods ---

    // Corrected GetBaseManaCost inside PlayerShooting.cs

    private int GetBaseManaCost(BulletType type)
    {
        switch (type)
        {
            // Read directly from this script's fields
            case BulletType.Type1: return this.basicManaCostInitial; // Or just basicManaCostInitial
            case BulletType.Type2: return this.freezeManaCostInitial; // Or just freezeManaCostInitial
            case BulletType.Type3: return this.teleportManaCost;     // Or just teleportManaCost
            default:
                Debug.LogError($"GetBaseManaCost Error: Unhandled BulletType: {type}");
                return 9999; // Return high cost on error
        }
    }

    private Vector3 CalculateFireDirection()
    {
        if (mainCamera == null) {
             Debug.LogError("Cannot calculate fire direction: Main Camera missing!");
             return firePoint.forward; // Fallback
        }

        Ray aimRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hitInfo;
        Vector3 initialTargetPoint;

        if (Physics.Raycast(aimRay, out hitInfo, aimRaycastRange, aimRaycastLayerMask)) {
            initialTargetPoint = hitInfo.point;
        } else {
            initialTargetPoint = aimRay.GetPoint(aimRaycastRange);
        }

        // Aim towards the point slightly offset *from the camera's perspective*
        Vector3 offsetTargetPoint = initialTargetPoint + aimRay.direction * aimForwardOffset;

        // Calculate direction from the actual fire point to that offset target
        Vector3 fireDirection = (offsetTargetPoint - firePoint.position).normalized;

        // Debug Visualizations (Optional)
        // Debug.DrawRay(aimRay.origin, aimRay.direction * aimRaycastRange, Color.yellow, 1.0f);
        // Debug.DrawLine(firePoint.position, offsetTargetPoint, Color.magenta, 1.0f);
        // Debug.DrawRay(firePoint.position, fireDirection * 20f, Color.cyan, 1.0f);

        return fireDirection;
    }


    // VCam activation/deactivation methods remain the same
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
        if (teleportAimUI != null && teleportAimUI.activeSelf) { teleportAimUI.SetActive(false); }
        if (teleportAimVCam != null && isUsingTeleportView) {
            teleportAimVCam.Priority = teleportAimVCamOriginalPriority;
            isUsingTeleportView = false;
        }
    }

    // Optional: Explicit Cancel function if player state changes during charge
     public void CancelCharge()
     {
        if (_isCharging)
        {
            Debug.Log("<color=orange>Charge Explicitly Cancelled.</color>");
            player.StopLoopingSound();
            DeactivateTeleportVCamView();
            if (currentBulletVisual != null)
            {
                Destroy(currentBulletVisual);
            }
            _isCharging = false;
            currentBulletVisual = null;
            chargeStartTime = 0f;
        }
     }
     
     public interface IBulletChargeReceiver
     {
         // Add PlayerStateManager playerRef parameter
         void OnFire(float chargeRatio, float forceMultiplier, PlayerStateManager playerRef);
     }


} // End of PlayerShooting class