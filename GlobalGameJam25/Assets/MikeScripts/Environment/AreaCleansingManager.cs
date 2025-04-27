using UnityEngine;
using UnityEngine.Rendering; // Optional: For Post-Processing
using System;

// Manages a SINGLE grove and tells the Global Controller about ambiance needs.
[RequireComponent(typeof(Collider))]
public class AreaCleansingManager : MonoBehaviour
{
    // --- Event for Global Notification ---
    public static event Action<AreaCleansingManager> OnAreaCleared; // Event signature
    private bool _areaHasBeenCleared = false; // Prevent multiple invocations

    [Header("Pest Tracking")]
    [Tooltip("How many pesky critters start in THIS grove?")]
    public int totalMonstersInArea = 10;
    private int monstersKilled = 0;
    private float currentBlightPotency = 1.0f; // 1.0 = fully blighted, 0.0 = clean

    [Header("Local Environment Ingredients")]
    [Tooltip("A light source PRIMARILY affecting this local area.")]
    public Light areaLight;

    // --- UPDATED SECTION: Grove Ambiance Flavors ---
    [Header("Grove Ambiance Flavors (Fog)")]
    [Tooltip("Check this if this area should influence global fog when player is present.")]
    public bool affectsGlobalFog = true;
    [Tooltip("How thick the 'unripe' fog should be.")]
    public float sicklyFogDensity = 0.1f;
    [Tooltip("What color the 'unripe' fog should be.")]
    public Color sicklyFogColor = new Color(0.5f, 0.6f, 0.5f);
    [Tooltip("The fresh, clean fog density for this area.")]
    public float cleanFogDensity = 0.01f;
    [Tooltip("The color of the air when this area is peachy!")]
    public Color cleanFogColor = new Color(0.8f, 0.9f, 1.0f);

    [Header("Grove Ambiance Flavors (Skybox)")] // <-- NEW SKYBOX SECTION
    [Tooltip("Check this if this area should influence the skybox tint when player is present.")]
    public bool affectsSkyboxTint = true;      // <-- NEW
    [ColorUsage(false, true)] // Show HDR color picker, but not for emission
    [Tooltip("The tint color of the skybox when the area is blighted.")]
    public Color sicklySkyboxTint = Color.grey; // <-- NEW (Example: Greyish tint)
    [ColorUsage(false, true)]
    [Tooltip("The tint color of the skybox when the area is clean.")]
    public Color cleanSkyboxTint = Color.white;  // <-- NEW (Example: Normal white tint)

    [Header("Grove Ambiance Flavors (Local Light)")] // <-- Renamed Header
    [Tooltip("How dim the LOCAL light gets.")]
    public float sicklyLightIntensity = 0.5f;
    [Tooltip("The sour color of the LOCAL light.")]
    public Color sicklyLightColor = new Color(0.8f, 0.5f, 0.8f);
    [Tooltip("How bright the LOCAL light becomes!")]
    public float cleanLightIntensity = 1.0f;
    [Tooltip("The warm, inviting color of the cleansed LOCAL light.")]
    public Color cleanLightColor = Color.white;
    // --- END UPDATED SECTION ---


    // --- Internal State ---
    private bool isPlayerCurrentlyInArea = false;
    private GlobalAmbianceController globalAmbianceController;

    void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        else Debug.LogError("AreaCleansingManager needs a Collider component!", gameObject);

        globalAmbianceController = FindFirstObjectByType<GlobalAmbianceController>();
        if (globalAmbianceController == null) Debug.LogError("Couldn't find GlobalAmbianceController!", gameObject);

        UpdateBlightPotency();
        ApplyLocalAmbiance(currentBlightPotency);
    }

    // --- Player Enter/Exit ---
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"Player entered {gameObject.name}");
            isPlayerCurrentlyInArea = true;
            if (globalAmbianceController != null) // Removed affectsGlobalFog check here, let controller handle it
            {
                globalAmbianceController.SetActiveArea(this); // Controller checks affectsFog/affectsSkybox
            }
            ApplyLocalAmbiance(currentBlightPotency);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
             Debug.Log($"Player exited {gameObject.name}");
            isPlayerCurrentlyInArea = false;
            if (globalAmbianceController != null) // Removed affectsGlobalFog check here
            {
                globalAmbianceController.ClearActiveArea(this);
            }
        }
    }

    // --- Monster Tracking & Updates ---
    public void RegisterMonsterKill()
    {
        if (monstersKilled < totalMonstersInArea)
        {
            monstersKilled++;
            Debug.Log($"Squashed a pest in {gameObject.name}! {monstersKilled}/{totalMonstersInArea}");

            UpdateBlightPotency();
            ApplyLocalAmbiance(currentBlightPotency);

            // If the player is here, tell the global controller about the change
            if (isPlayerCurrentlyInArea && globalAmbianceController != null) // Removed affectsGlobalFog check
            {
                // Tell controller to update ALL its targets from this area
                globalAmbianceController.UpdateActiveAreaTargets(this);
            }
        }
        // --- Check if Area Just Got Cleared ---
        if (!_areaHasBeenCleared && monstersKilled >= totalMonstersInArea)
        {
            _areaHasBeenCleared = true; // Mark as cleared
            Debug.Log($"{gameObject.name} is perfectly ripe! Invoking OnAreaCleared event.");

            // --- Invoke the static event ---
            OnAreaCleared?.Invoke(this); // Notify subscribers (like PlayerAnimationManager)
            // -----------------------------
        }
    }

    void UpdateBlightPotency()
    {
        if (totalMonstersInArea <= 0) { currentBlightPotency = 0.0f; return; }
        float fractionKilled = (float)monstersKilled / totalMonstersInArea;
        if (fractionKilled >= 1.0f) currentBlightPotency = 0.0f;
        else if (fractionKilled >= 2.0f / 3.0f) currentBlightPotency = 0.25f;
        else if (fractionKilled >= 1.0f / 3.0f) currentBlightPotency = 0.5f;
        else currentBlightPotency = 1.0f;
    }

    // --- Applying Effects ---
    void ApplyLocalAmbiance(float intensity) // Only affects local things now
    {
        if (areaLight != null)
        {
            areaLight.intensity = Mathf.Lerp(cleanLightIntensity, sicklyLightIntensity, intensity);
            areaLight.color = Color.Lerp(cleanLightColor, sicklyLightColor, intensity);
        }
    }

    // --- Getters for Global Controller ---
    // Provides target fog settings based on current blight level
    public void GetTargetFogSettings(out float density, out Color color)
    {
        density = Mathf.Lerp(cleanFogDensity, sicklyFogDensity, currentBlightPotency);
        color = Color.Lerp(cleanFogColor, sicklyFogColor, currentBlightPotency);
    }

    // Provides target skybox tint based on current blight level <-- NEW
    public void GetTargetSkyboxSettings(out Color tint)
    {
        tint = Color.Lerp(cleanSkyboxTint, sicklySkyboxTint, currentBlightPotency);
    }
}