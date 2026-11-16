using Cinemachine;
using UnityEngine;

namespace Ishana.CameraSystem
{
    /// <summary>
    /// Trauma-based camera shake as a Cinemachine extension. Every virtual camera with this
    /// extension shakes together, so blending between the gameplay and aim cameras stays smooth.
    /// ThirdPersonCameraController adds it to the player's virtual cameras automatically.
    ///
    /// Trauma (0..1) decays over time; shake strength is trauma squared, which keeps small hits
    /// subtle and big hits punchy.
    /// </summary>
    [AddComponentMenu("Ishana/Camera Shake (Cinemachine Extension)")]
    public class CameraShake : CinemachineExtension
    {
        [Tooltip("Max positional offset at full trauma (world units).")]
        public float maxOffset = 0.35f;
        [Tooltip("Max rotation at full trauma (degrees) for pitch / yaw / roll.")]
        public Vector3 maxAngles = new Vector3(3f, 3f, 5f);
        [Tooltip("Noise speed. Higher = more jittery.")]
        public float frequency = 22f;
        [Tooltip("Trauma lost per second.")]
        public float decayPerSecond = 1.6f;

        static float _trauma;
        static float _lastDecayTime;
        static float _decayPerSecond = 1.6f;

        float _seed;

        /// <summary>Global intensity multiplier, e.g. for an accessibility "reduce camera shake" option.</summary>
        public static float GlobalIntensity = 1f;

        public static void AddTrauma(float amount)
        {
            if (amount <= 0f) return;
            DecayTrauma();
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        static void DecayTrauma()
        {
            float now = Time.unscaledTime;
            float dt = now - _lastDecayTime;
            _lastDecayTime = now;
            if (dt <= 0f) return;
            _trauma = Mathf.Max(0f, _trauma - _decayPerSecond * dt);
        }

        protected override void Awake()
        {
            base.Awake();
            _seed = Random.Range(0f, 1000f);
            _decayPerSecond = decayPerSecond;
        }

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Noise || !Application.isPlaying) return;

            DecayTrauma();
            float shake = _trauma * _trauma * GlobalIntensity;
            if (shake <= 0.0001f) return;

            float t = Time.unscaledTime * frequency;
            Vector3 offset = new Vector3(
                Noise(t, 0f), Noise(t, 1f), Noise(t, 2f)) * (maxOffset * shake);
            Vector3 angles = new Vector3(
                Noise(t, 3f) * maxAngles.x, Noise(t, 4f) * maxAngles.y, Noise(t, 5f) * maxAngles.z) * shake;

            state.PositionCorrection += state.FinalOrientation * offset;
            state.OrientationCorrection = state.OrientationCorrection * Quaternion.Euler(angles);
        }

        float Noise(float t, float channel) => Mathf.PerlinNoise(_seed + channel * 17.3f, t) * 2f - 1f;
    }
}
