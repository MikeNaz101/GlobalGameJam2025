using UnityEngine;
using UnityEngine.Rendering; // Optional: For Post-Processing
using System;
using System.Collections; // Needed for Coroutines

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

    [Header("Grove Ambiance Flavors (Skybox)")]
    [Tooltip("Check this if this area should influence the skybox tint when player is present.")]
    public bool affectsSkyboxTint = true;
    [ColorUsage(false, true)]
    [Tooltip("The tint color of the skybox when the area is blighted.")]
    public Color sicklySkyboxTint = Color.grey;
    [ColorUsage(false, true)]
    [Tooltip("The tint color of the skybox when the area is clean.")]
    public Color cleanSkyboxTint = Color.white;

    [Header("Grove Ambiance Flavors (Local Light)")]
    [Tooltip("How dim the LOCAL light gets.")]
    public float sicklyLightIntensity = 0.5f;
    [Tooltip("The sour color of the LOCAL light.")]
    public Color sicklyLightColor = new Color(0.8f, 0.5f, 0.8f);
    [Tooltip("How bright the LOCAL light becomes!")]
    public float cleanLightIntensity = 1.0f;
    [Tooltip("The warm, inviting color of the cleansed LOCAL light.")]
    public Color cleanLightColor = Color.white;

    // --- NEW: Background Music Control ---
    [Header("Background Music Control")]
    [Tooltip("Assign the AudioSource playing the background music here.")]
    public AudioSource backgroundMusicSource; // Assign this in the Inspector
    [Tooltip("The target volume for the BGM when the player is INSIDE this area (e.g., 0.2).")]
    [Range(0.0f, 1.0f)]
    public float quietVolume = 0.2f;
    [Tooltip("How many seconds the fade in/out should take.")]
    public float musicFadeDuration = 1.5f;
    private float originalMusicVolume; // To store the BGM volume before entering
    private Coroutine currentFadeCoroutine = null; // To manage the active fade
    // --- END NEW SECTION ---

    // --- Internal State ---
    private bool isPlayerCurrentlyInArea = false;
    private GlobalAmbianceController globalAmbianceController;

    void Awake() // Use Awake for initialization that doesn't depend on others
    {
        if (backgroundMusicSource != null)
        {
            originalMusicVolume = backgroundMusicSource.volume; // Store the initial volume
        }
        else
        {
            Debug.LogWarning($"Background Music Source not assigned on {gameObject.name}. Music fading will not work for this area.", gameObject);
        }
    }

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
            if (globalAmbianceController != null)
            {
                globalAmbianceController.SetActiveArea(this);
            }
            ApplyLocalAmbiance(currentBlightPotency);

            // --- NEW: Start Music Fade Down ---
            if (backgroundMusicSource != null)
            {
                StartMusicFade(quietVolume, musicFadeDuration);
            }
            // --- END NEW SECTION ---
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"Player exited {gameObject.name}");
            isPlayerCurrentlyInArea = false;
            if (globalAmbianceController != null)
            {
                globalAmbianceController.ClearActiveArea(this);
            }

            // --- NEW: Start Music Fade Up ---
            if (backgroundMusicSource != null)
            {
                StartMusicFade(originalMusicVolume, musicFadeDuration);
            }
            // --- END NEW SECTION ---
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

            if (isPlayerCurrentlyInArea && globalAmbianceController != null)
            {
                globalAmbianceController.UpdateActiveAreaTargets(this);
            }
        }
        if (!_areaHasBeenCleared && monstersKilled >= totalMonstersInArea)
        {
            _areaHasBeenCleared = true;
            Debug.Log($"{gameObject.name} is perfectly ripe! Invoking OnAreaCleared event.");
            OnAreaCleared?.Invoke(this);
        }
    }

    void UpdateBlightPotency()
    {
        if (totalMonstersInArea <= 0) { currentBlightPotency = 0.0f; return; }
        float fractionKilled = (float)monstersKilled / totalMonstersInArea;
        // Simplified lerp for potency (you might want smoother steps later)
        currentBlightPotency = 1.0f - fractionKilled; // Linear decrease
        currentBlightPotency = Mathf.Clamp01(currentBlightPotency); // Ensure it stays between 0 and 1
    }

    // --- Applying Effects ---
    void ApplyLocalAmbiance(float intensity)
    {
        if (areaLight != null)
        {
            areaLight.intensity = Mathf.Lerp(cleanLightIntensity, sicklyLightIntensity, intensity);
            areaLight.color = Color.Lerp(cleanLightColor, sicklyLightColor, intensity);
        }
    }

    // --- Getters for Global Controller ---
    public void GetTargetFogSettings(out float density, out Color color)
    {
        density = Mathf.Lerp(cleanFogDensity, sicklyFogDensity, currentBlightPotency);
        color = Color.Lerp(cleanFogColor, sicklyFogColor, currentBlightPotency);
    }

    public void GetTargetSkyboxSettings(out Color tint)
    {
        tint = Color.Lerp(cleanSkyboxTint, sicklySkyboxTint, currentBlightPotency);
    }

    // --- NEW: Music Fading Logic ---
    private void StartMusicFade(float targetVolume, float duration)
    {
        // Stop any previously running fade coroutine on this script instance
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }
        // Start the new fade and store its reference
        currentFadeCoroutine = StartCoroutine(FadeAudio(targetVolume, duration));
    }

    private IEnumerator FadeAudio(float targetVolume, float duration)
    {
        if (backgroundMusicSource == null) yield break; // Exit if no source assigned

        float startVolume = backgroundMusicSource.volume;
        float startTime = Time.unscaledTime; // Use unscaled time if you want fade unaffected by Time.timeScale

        while (Time.unscaledTime < startTime + duration)
        {
            float elapsed = Time.unscaledTime - startTime;
            float progress = Mathf.Clamp01(elapsed / duration); // Value between 0 and 1

            // You can add easing here if desired (e.g., SmoothStep)
            // float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, progress);
            // backgroundMusicSource.volume = Mathf.Lerp(startVolume, targetVolume, easedProgress);

            backgroundMusicSource.volume = Mathf.Lerp(startVolume, targetVolume, progress);

            yield return null; // Wait for the next frame
        }

        // Ensure the volume is exactly the target volume at the end
        backgroundMusicSource.volume = targetVolume;
        currentFadeCoroutine = null; // Mark coroutine as finished
    }
    // --- END NEW SECTION ---
}