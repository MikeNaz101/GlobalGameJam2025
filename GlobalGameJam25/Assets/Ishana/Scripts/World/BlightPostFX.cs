using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ishana.World
{
    /// <summary>
    /// Drives two URP post-processing looks from the grove cleansing system:
    ///
    /// - Blighted: desaturated, sickly green, crushed, vignetted and grainy.
    /// - Cleansed: warm, saturated, with soft bloom.
    ///
    /// Inside a grove, the look follows that grove's blight (it heals as you kill its monsters).
    /// Outside, it follows the average blight of the whole forest, so the world visibly recovers as
    /// you progress. Clearing a grove fires a short bloom "bloom burst".
    ///
    /// Created automatically by IshanaBootstrap. Add it to a scene object yourself to tune the looks.
    /// Both looks are built in code, so no Volume Profile assets are needed.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ishana/Blight Post FX")]
    public class BlightPostFX : MonoBehaviour
    {
        [Header("Blend")]
        [Tooltip("How fast the look follows the blight level (per second).")]
        public float blendSpeed = 0.8f;
        [Tooltip("Volume priority. Higher than your scene's global volume so these looks win.")]
        public float volumePriority = 50f;
        [Tooltip("Force post-processing on for the main camera (it was off in the player prefab).")]
        public bool enableCameraPostProcessing = true;

        [Header("Blighted Look")]
        public float blightSaturation = -45f;
        public float blightContrast = 18f;
        public float blightExposure = -0.35f;
        public Color blightColorFilter = new Color(0.82f, 0.95f, 0.78f);
        public float blightTemperature = -12f;
        public float blightTint = 22f;
        [Range(0f, 1f)] public float blightVignette = 0.38f;
        public Color blightVignetteColor = new Color(0.08f, 0.12f, 0.05f);
        [Range(0f, 1f)] public float blightGrain = 0.35f;
        [Range(0f, 1f)] public float blightChromaticAberration = 0.15f;

        [Header("Cleansed Look")]
        public float cleanSaturation = 18f;
        public float cleanContrast = 8f;
        public float cleanTemperature = 10f;
        public float cleanBloomIntensity = 0.9f;
        public float cleanBloomThreshold = 0.95f;
        public Color cleanBloomTint = new Color(1f, 0.93f, 0.78f);
        [Range(0f, 1f)] public float cleanVignette = 0.18f;

        [Header("Grove Cleared Burst")]
        public float clearedBurstBloom = 6f;
        public float clearedBurstDuration = 2.5f;

        Volume _blightVolume, _cleanVolume;
        Bloom _cleanBloom;
        GlobalAmbianceController _ambiance;
        AreaCleansingManager[] _areas;
        float _blight = -1f;
        float _burstStart = -100f;

        void Awake()
        {
            _blightVolume = CreateVolume("Blight Volume", BuildBlightProfile());
            _cleanVolume = CreateVolume("Cleansed Volume", BuildCleanProfile(out _cleanBloom));
        }

        void OnEnable() => AreaCleansingManager.OnAreaCleared += HandleAreaCleared;
        void OnDisable() => AreaCleansingManager.OnAreaCleared -= HandleAreaCleared;

        void Start()
        {
            _ambiance = FindFirstObjectByType<GlobalAmbianceController>();
            _areas = FindObjectsByType<AreaCleansingManager>(FindObjectsSortMode.None);

            if (enableCameraPostProcessing) EnablePostOnMainCamera();
        }

        void Update()
        {
            if (enableCameraPostProcessing && Time.frameCount % 30 == 0) EnablePostOnMainCamera();

            float target = GetTargetBlight();
            _blight = _blight < 0f ? target : Mathf.MoveTowards(_blight, target, blendSpeed * Time.deltaTime);

            // Blighted look fades in with blight, cleansed look fades in as it heals (smooth crossfade)
            float b = Mathf.SmoothStep(0f, 1f, _blight);
            _blightVolume.weight = b;
            _cleanVolume.weight = 1f - b;

            // Grove-cleared bloom burst
            float burstT = (Time.time - _burstStart) / Mathf.Max(0.01f, clearedBurstDuration);
            float burst = burstT >= 0f && burstT < 1f ? Mathf.Sin(burstT * Mathf.PI) * (1f - burstT * 0.5f) : 0f;
            _cleanBloom.intensity.value = cleanBloomIntensity + clearedBurstBloom * burst;
            if (burst > 0f) _cleanVolume.weight = Mathf.Max(_cleanVolume.weight, burst);
        }

        float GetTargetBlight()
        {
            if (_ambiance != null && _ambiance.ActiveArea != null) return _ambiance.ActiveArea.BlightPotency;

            if (_areas == null || _areas.Length == 0) return 0.5f;
            float sum = 0f; int count = 0;
            foreach (var area in _areas)
            {
                if (area == null) continue;
                sum += area.BlightPotency; count++;
            }
            return count > 0 ? sum / count : 0.5f;
        }

        void HandleAreaCleared(AreaCleansingManager area)
        {
            _burstStart = Time.time;
            Combat.CombatFeedback.Shake(0.25f);
        }

        static void EnablePostOnMainCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null && !data.renderPostProcessing) data.renderPostProcessing = true;
        }

        Volume CreateVolume(string name, VolumeProfile profile)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = volumePriority;
            volume.weight = 0f;
            volume.sharedProfile = profile;
            return volume;
        }

        VolumeProfile BuildBlightProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Blight (runtime)";

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(blightSaturation);
            color.contrast.Override(blightContrast);
            color.postExposure.Override(blightExposure);
            color.colorFilter.Override(blightColorFilter);

            var white = profile.Add<WhiteBalance>(true);
            white.temperature.Override(blightTemperature);
            white.tint.Override(blightTint);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(blightVignette);
            vignette.color.Override(blightVignetteColor);
            vignette.smoothness.Override(0.5f);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(blightGrain);

            var chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.Override(blightChromaticAberration);

            return profile;
        }

        VolumeProfile BuildCleanProfile(out Bloom bloom)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Cleansed (runtime)";

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(cleanSaturation);
            color.contrast.Override(cleanContrast);

            var white = profile.Add<WhiteBalance>(true);
            white.temperature.Override(cleanTemperature);

            bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(cleanBloomIntensity);
            bloom.threshold.Override(cleanBloomThreshold);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(cleanBloomTint);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(cleanVignette);
            vignette.smoothness.Override(0.45f);

            return profile;
        }

        void OnDestroy()
        {
            if (_blightVolume != null && _blightVolume.sharedProfile != null) Destroy(_blightVolume.sharedProfile);
            if (_cleanVolume != null && _cleanVolume.sharedProfile != null) Destroy(_cleanVolume.sharedProfile);
        }
    }
}
