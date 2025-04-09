using UnityEngine;
using Cinemachine;

public class PlayerShooting : MonoBehaviour
{
    [Header("Core Setup")]
    public Transform firePoint;
    public float bulletForce = 20f;
    public PlayerStateManager player;

    [Header("Cinemachine Cameras")]
    public CinemachineVirtualCamera gameplayVCam;
    public CinemachineVirtualCamera teleportAimVCam;

    [Header("Aiming & UI")]
    public GameObject teleportAimUI;    // UI shown only when aiming teleport
    public float aimRaycastRange = 100f;
    [Tooltip("Set this mask to exclude Player, Weapon, AND the layer your bullet prefabs are on!")]
    public LayerMask aimRaycastLayerMask = ~0; // CONFIGURE IN INSPECTOR!
    public ParticleSystem aimingSpotEffect; // Assign a Particle System prefab/instance here

    [Header("Mana Costs")]
    public int basicManaCostInitial = 5;
    public int freezeManaCostInitial = 10;
    public int teleportManaCost = 20;

    // --- Internal State Variables ---
    private bool _isCharging = false;
    private GameObject currentBullet;
    private float chargeStartTime;
    private bool isUsingTeleportView = false;
    private Camera mainCamera;
    private int teleportAimVCamOriginalPriority = 0;
    private ParticleSystem.EmissionModule aimingSpotEmission; // Cache emission module

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null) { /* ... Error Handling ... */ this.enabled = false; return; }

        if (player == null) { /* ... Get Player State Manager ... */ }
        if (player == null || player.bulletSpawner == null) { /* ... Error handling ... */ this.enabled = false; return; }

        if (gameplayVCam == null || teleportAimVCam == null) { /* ... Error handling ... */ this.enabled = false; return; }
        teleportAimVCamOriginalPriority = teleportAimVCam.Priority;
        if (teleportAimVCam.Priority >= gameplayVCam.Priority) { /* ... Fix priority ... */ }

        if (teleportAimUI != null) { teleportAimUI.SetActive(false); }
        else { Debug.LogWarning("Teleport Aim UI is not assigned."); }

        // --- Aiming Spot Particle Setup ---
        if (aimingSpotEffect != null)
        {
            aimingSpotEmission = aimingSpotEffect.emission; // Cache emission module
            aimingSpotEmission.enabled = false; // Start with the effect hidden/off
            // Optional: Ensure it doesn't play automatically on awake if it's an instance in scene
            // aimingSpotEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Debug.Log("Aiming Spot Effect initialized.");
        } else {
            Debug.LogWarning("Aiming Spot Effect is not assigned. This feature will be disabled.");
        }
        // --- End Particle Setup ---


        if (player.bulletSpawner != null) { player.bulletSpawner.playerShooting = this; }
        else { Debug.LogError("Could not link to BulletSpawnerState!"); }

        isUsingTeleportView = false;
        _isCharging = false;
        currentBullet = null;
        Debug.Log("PlayerShooting Start: Initialization Complete.");
    }

    // --- Update Method for Aiming Particle ---
    void Update()
    {
        // Continuously update the aiming spot particle effect position
        if (aimingSpotEffect != null && mainCamera != null)
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hit;

            // Perform raycast using the layer mask to find where player is aiming
            if (Physics.Raycast(ray, out hit, aimRaycastRange, aimRaycastLayerMask))
            {
                // Ray hit something, position the effect at the hit point
                aimingSpotEffect.transform.position = hit.point;
                // Optional: Align effect with the surface normal
                aimingSpotEffect.transform.rotation = Quaternion.LookRotation(hit.normal);

                // Ensure the particle emission is enabled
                if (!aimingSpotEmission.enabled) {
                    aimingSpotEmission.enabled = true;
                    // Use Play() only if you need to restart the effect visually, often just enabling emission is enough
                    // aimingSpotEffect.Play();
                }
            }
            else
            {
                // Ray missed, disable the particle emission
                if (aimingSpotEmission.enabled) {
                     aimingSpotEmission.enabled = false;
                     // Use Stop() only if you need particles to clear immediately
                     // aimingSpotEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                // Optionally move it far away or keep its last position while hidden
                // aimingSpotEffect.transform.position = ray.GetPoint(aimRaycastRange);
            }
        }
    }
    // --- End Update Method ---


    public void StartCharge()
    {
        // ... (StartCharge logic remains largely the same as the previous version) ...
        // It determines bullet type, checks mana, activates teleport view if needed,
        // instantiates the bullet, and calls StartCharging on the specific bullet component.

        // --- Pre-Charge Checks ---
        if (player == null || player.bulletSpawner == null || firePoint == null){ return; }
        if (_isCharging) { return; }
        // --- Determine Requirements ---
        int requiredMana = 0; BulletType currentType = player.bulletSpawner.CurrentBulletType; bool isTeleportSelected = currentType == BulletType.Type3;
        switch (currentType) { /* ... set requiredMana ... */ }
        // --- Check Resources ---
        if (player.currentMana < requiredMana) { return; }
        // --- Activate Aiming Mode (if needed) ---
        if (isTeleportSelected) { ActivateTeleportVCamView(); }
        // --- Instantiate Bullet ---
        currentBullet = Instantiate(player.bulletSpawner.bulletPrefab, firePoint.position, firePoint.rotation);
        if (currentBullet == null) { if(isTeleportSelected) DeactivateTeleportVCamView(); return; }
        // --- Prepare Bullet ---
        Rigidbody rb = currentBullet.GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = true; currentBullet.transform.parent = firePoint;
        // --- Start Charging State ---
        chargeStartTime = Time.time; _isCharging = true;
        // --- Handle Bullet-Specific Logic & Mana Use ---
        BasicBullet basic = currentBullet.GetComponent<BasicBullet>(); FreezeBullet freeze = currentBullet.GetComponent<FreezeBullet>(); TeleportBullet teleport = currentBullet.GetComponent<TeleportBullet>();
        bool chargeStartedSuccessfully = false;
        if (basic != null) { if (player.UseMana(requiredMana, player)) { basic.StartCharging(player); chargeStartedSuccessfully = true; } }
        else if (freeze != null) { if (player.UseMana(requiredMana, player)) { freeze.StartCharging(player); chargeStartedSuccessfully = true; } }
        else if (teleport != null) { if (player.UseMana(requiredMana, player)) { chargeStartedSuccessfully = true; } }
        else { /* ... Log Error ... */ chargeStartedSuccessfully = false; }
        // --- Handle Failed Charge Start ---
        if (!chargeStartedSuccessfully) { /* ... Cleanup ... */ } else { /* ... Log Success ... */ }
    }


    public void EndCharge()
    {
        // --- Initial Logs & Deactivation ---
        Debug.Log("<color=yellow>EndCharge CALLED.</color>");
        DeactivateTeleportVCamView(); // Deactivate teleport view and hide UI
        // ... (Log priorities) ...

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
        else if (freeze != null) { freeze.StopCharging(); } // Assume freeze StopCharging doesn't affect multiplier

        // --- Calculate Fire Direction (NOW FOR ALL BULLETS) ---
        Vector3 fireDirection;
        BulletType firedBulletType = player.bulletSpawner.CurrentBulletType; // Get the type that was charged

        Debug.Log($"Calculating Aim Direction for {firedBulletType} via Raycast...");
        if (mainCamera == null) {
             Debug.LogError("Cannot perform aim raycast: Main Camera missing! Using default firePoint direction.");
             fireDirection = firePoint.forward; // Fallback
        } else {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hit;
            Vector3 targetPoint;

            // Visualize Aim Ray
            Debug.DrawRay(ray.origin, ray.direction * aimRaycastRange, Color.yellow, 2.0f);

            // Perform Raycast WITH LAYER MASK (ensure Inspector setup is correct!)
            if (Physics.Raycast(ray, out hit, aimRaycastRange, aimRaycastLayerMask)) {
                targetPoint = hit.point;
                Debug.Log($"Aim Raycast Hit: {hit.collider.name} at {targetPoint}");
            } else {
                targetPoint = ray.GetPoint(aimRaycastRange);
                Debug.Log($"Aim Raycast Missed. Targeting point far away: {targetPoint}");
            }

            // Calculate direction FROM firePoint TO targetPoint (works for all bullets now)
            fireDirection = (targetPoint - firePoint.position).normalized;
            Debug.Log($"Calculated Fire Direction: {fireDirection}");

            // Visualize Fire Direction (shows in Scene view if Gizmos enabled)
            // Red for teleport, Green otherwise (can customize)
            Color debugColor = (firedBulletType == BulletType.Type3) ? Color.red : Color.green;
            Debug.DrawRay(firePoint.position, fireDirection * 10f, debugColor, 2.0f);
        }
        // --- End Calculate Fire Direction ---


        // --- Apply Launch Force ---
        if (rb != null) {
            float finalForce = bulletForce * chargeMultiplier; // Use multiplier (mostly for basic)
            rb.AddForce(fireDirection * finalForce, ForceMode.VelocityChange); // Use calculated fireDirection
            Debug.Log($"<color=white>Fired {firedBulletType} with force multiplier {chargeMultiplier} in direction {fireDirection}</color>");
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
        if (teleportAimUI != null) { teleportAimUI.SetActive(false); } // Hide UI first
        if (teleportAimVCam != null) {
            if (isUsingTeleportView) { Debug.Log("<color=cyan>Deactivating Teleport View.</color>"); }
            teleportAimVCam.Priority = teleportAimVCamOriginalPriority;
            isUsingTeleportView = false;
        }
    }

    public void CancelCharge()
    {
        // ... (CancelCharge logic remains the same - calls DeactivateTeleportVCamView) ...
        Debug.LogError("<color=red>!!! CancelCharge was explicitly called! !!!</color>");
        DeactivateTeleportVCamView();
        if (_isCharging) { if (currentBullet != null) { Destroy(currentBullet); currentBullet = null; } _isCharging = false; }
        else { /* Log */ }
    }

} // End of PlayerShooting class