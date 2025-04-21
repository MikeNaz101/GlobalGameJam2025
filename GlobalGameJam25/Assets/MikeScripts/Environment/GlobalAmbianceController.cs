using UnityEngine;

// Manages global scene settings like fog and skybox tint, blending based on the active area.
public class GlobalAmbianceController : MonoBehaviour
{
    [Header("Default/Neutral Settings")]
    [Tooltip("Fog density when player is not in any special area.")]
    public float defaultFogDensity = 0.01f;
    [Tooltip("Fog color when player is not in any special area.")]
    public Color defaultFogColor = Color.gray;
    [ColorUsage(false, true)] // <-- Added ColorUsage attribute
    [Tooltip("Skybox tint color when player is not in any special area.")]
    public Color defaultSkyboxTint = Color.white; // <-- NEW

    [Header("Transition Speed")]
    [Tooltip("How quickly the ambiance blends to the new target settings.")]
    public float transitionSpeed = 2.0f;

    // --- Internal State ---
    private AreaCleansingManager currentActiveArea = null;
    // Fog Targets
    private float targetFogDensity;
    private Color targetFogColor;
    // Skybox Targets
    private Color targetSkyboxTint; // <-- NEW
    private Material currentSkyboxMat; // <-- NEW Reference to the actual skybox material
    private bool skyboxHasTintProperty = false; // <-- NEW Flag

    void Start()
    {
        // Initialize Fog
        targetFogDensity = defaultFogDensity;
        targetFogColor = defaultFogColor;
        RenderSettings.fogDensity = targetFogDensity;
        RenderSettings.fogColor = targetFogColor;
        RenderSettings.fog = (targetFogDensity > 0.001f);

        // Initialize Skybox --- NEW ---
        currentSkyboxMat = RenderSettings.skybox; // Get the currently assigned skybox material
        targetSkyboxTint = defaultSkyboxTint; // Start with default tint
        if (currentSkyboxMat != null)
        {
            // Check if the material HAS the _Tint property before trying to use it
            if (currentSkyboxMat.HasProperty("_Tint"))
            {
                skyboxHasTintProperty = true;
                // Optional: Initialize targetSkyboxTint from the material's current tint if preferred
                // targetSkyboxTint = currentSkyboxMat.GetColor("_Tint");
                currentSkyboxMat.SetColor("_Tint", targetSkyboxTint); // Set initial tint
                 Debug.Log("GlobalAmbianceController: Found Skybox with _Tint property.");
            } else {
                 Debug.LogWarning("GlobalAmbianceController: Assigned Skybox material does not have a '_Tint' property. Skybox tint blending will be disabled.", this);
            }
        } else {
             Debug.LogWarning("GlobalAmbianceController: No Skybox material assigned in Render Settings (Edit -> Project Settings -> Graphics -> Skybox Material). Skybox tint blending will be disabled.", this);
        }
        // --- END NEW ---
    }

    void Update()
    {
        // Smoothly Lerp Fog
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, Time.deltaTime * transitionSpeed);
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFogColor, Time.deltaTime * transitionSpeed);
        RenderSettings.fog = (RenderSettings.fogDensity > 0.001f || targetFogDensity > 0.001f);

        // Smoothly Lerp Skybox Tint --- NEW ---
        if (currentSkyboxMat != null && skyboxHasTintProperty) // Only if we have a valid material and property
        {
            Color currentTint = currentSkyboxMat.GetColor("_Tint");
            Color newTint = Color.Lerp(currentTint, targetSkyboxTint, Time.deltaTime * transitionSpeed);
            currentSkyboxMat.SetColor("_Tint", newTint);
        }
        // --- END NEW ---
    }

    // Called by AreaCleansingManager when player ENTERS its trigger
    public void SetActiveArea(AreaCleansingManager newAreaManager)
    {
        currentActiveArea = newAreaManager; // Store reference regardless of settings
        if (currentActiveArea != null)
        {
             Debug.Log($"Global Ambiance: Player entered '{currentActiveArea.gameObject.name}'. Updating targets.");
             UpdateAllTargetsFromManager(currentActiveArea); // Update all targets initially
        }
         else // Should not happen if called correctly, but safety check
        {
             RevertToDefaults();
        }
    }

    // Called by AreaCleansingManager when its state changes WHILE player is inside
     public void UpdateActiveAreaTargets(AreaCleansingManager reportingManager)
    {
        // Only update if the reporting manager is the currently active one
        if (reportingManager == currentActiveArea && currentActiveArea != null)
        {
            Debug.Log($"Global Ambiance: Updating targets from active area {currentActiveArea.gameObject.name}");
             UpdateAllTargetsFromManager(currentActiveArea); // Update all targets
        }
    }

    // Called by AreaCleansingManager when player EXITS its trigger
    public void ClearActiveArea(AreaCleansingManager exitingManager)
    {
        // Only revert to default if the EXITIING manager is the one currently active
        if (currentActiveArea == exitingManager)
        {
            Debug.Log($"Global Ambiance: Player left '{exitingManager?.gameObject.name ?? "an area"}'. Reverting to default settings.");
            currentActiveArea = null;
            RevertToDefaults(); // Revert all targets to default
        }
    }

    // --- Helper Methods ---

    // Updates all target variables based on the manager's settings
    private void UpdateAllTargetsFromManager(AreaCleansingManager manager)
    {
         // Update Fog Targets
         if (manager.affectsGlobalFog) {
             manager.GetTargetFogSettings(out targetFogDensity, out targetFogColor);
         } else {
             targetFogDensity = defaultFogDensity;
             targetFogColor = defaultFogColor;
         }

         // Update Skybox Targets --- NEW ---
         if (manager.affectsSkyboxTint && skyboxHasTintProperty) { // Check property exists too
             manager.GetTargetSkyboxSettings(out targetSkyboxTint);
         } else {
             targetSkyboxTint = defaultSkyboxTint;
         }
         // --- END NEW ---
    }

     // Reverts all target variables to their default values
    private void RevertToDefaults()
    {
         targetFogDensity = defaultFogDensity;
         targetFogColor = defaultFogColor;
         targetSkyboxTint = defaultSkyboxTint; // <-- NEW
    }
}