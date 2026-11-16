using System.Collections.Generic;
using Ishana.CameraSystem;
using UnityEngine;

namespace Ishana.Combat
{
    /// <summary>
    /// Central "game feel" service: hit-stop, camera shake, enemy hit flashes and damage numbers.
    ///
    /// Gameplay code only calls the static Report/Shake methods. The service creates itself the first
    /// time it is needed, so no scene setup is required. To tune values, add this component to any
    /// GameObject in the scene (the scene copy is used instead of the auto-created one).
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatFeedback : MonoBehaviour
    {
        [Header("Hit-Stop (brief freeze on impact)")]
        [Tooltip("Freeze length when an enemy is hit (real seconds).")]
        public float hitStopOnHit = 0.035f;
        [Tooltip("Freeze length when an enemy dies.")]
        public float hitStopOnKill = 0.09f;
        [Tooltip("Freeze length when the player is hit.")]
        public float hitStopOnPlayerHit = 0.06f;
        [Tooltip("Time scale used during hit-stop (0 = full freeze).")]
        [Range(0f, 1f)] public float hitStopTimeScale = 0.05f;

        [Header("Camera Shake (trauma 0..1)")]
        public float shakeOnHit = 0.15f;
        public float shakeOnKill = 0.35f;
        public float shakeOnBossHit = 0.45f;
        public float shakeOnPlayerHit = 0.5f;
        [Tooltip("Shake added when a shot is released, scaled by charge.")]
        public float shakeOnFullChargeShot = 0.3f;

        [Header("Hit Flash")]
        public Color hitFlashColor = Color.white;
        public float hitFlashDuration = 0.1f;

        [Header("Damage Numbers")]
        public bool showDamageNumbers = true;
        public Color damageNumberColor = new Color(1f, 0.92f, 0.55f);
        public Color freezeDamageNumberColor = new Color(0.6f, 0.9f, 1f);
        public Color killDamageNumberColor = new Color(1f, 0.55f, 0.35f);
        public float damageNumberSize = 4f;

        [Header("Full-Charge Cue")]
        [Tooltip("Optional sound played once when a shot reaches full charge.")]
        public AudioClip chargeReadyClip;
        [Range(0f, 1f)] public float chargeReadyVolume = 0.6f;

        static CombatFeedback _instance;
        float _hitStopEndRealtime;
        bool _hitStopActive;
        readonly List<DamageNumber> _numberPool = new List<DamageNumber>();

        public static CombatFeedback Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<CombatFeedback>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[CombatFeedback]");
                        _instance = go.AddComponent<CombatFeedback>();
                    }
                }
                return _instance;
            }
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_hitStopActive && !PauseMenuController.GameIsPaused) Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ public API

        public static void ReportEnemyHit(BaseEnemy enemy, int damage, DamageType type, bool killed, bool isBoss = false)
        {
            if (enemy == null) return;
            var fx = Instance;

            fx.HitStop(killed ? fx.hitStopOnKill : fx.hitStopOnHit);
            float trauma = killed ? fx.shakeOnKill : fx.shakeOnHit;
            if (isBoss) trauma = Mathf.Max(trauma, fx.shakeOnBossHit);
            CameraShake.AddTrauma(trauma);

            var flash = enemy.GetComponent<HitFlash>();
            if (flash == null) flash = enemy.gameObject.AddComponent<HitFlash>();
            flash.Flash(fx.hitFlashColor, fx.hitFlashDuration);

            if (fx.showDamageNumbers)
            {
                Color c = killed ? fx.killDamageNumberColor
                        : type == DamageType.Freeze ? fx.freezeDamageNumberColor
                        : fx.damageNumberColor;
                fx.SpawnDamageNumber(GetTopOf(enemy.gameObject), damage, c, killed ? 1.4f : 1f);
            }
        }

        public static void ReportPlayerHit(PlayerStateManager player, int damage)
        {
            var fx = Instance;
            fx.HitStop(fx.hitStopOnPlayerHit);
            CameraShake.AddTrauma(fx.shakeOnPlayerHit);
        }

        /// <summary>Called when a charged shot is released. chargeRatio is 0..1.</summary>
        public static void ShotFired(float chargeRatio)
        {
            var fx = Instance;
            CameraShake.AddTrauma(fx.shakeOnFullChargeShot * chargeRatio * chargeRatio);
        }

        /// <summary>Called once when a shot reaches full charge.</summary>
        public static void ChargeReady(Vector3 position)
        {
            var fx = Instance;
            CameraShake.AddTrauma(0.08f);
            if (fx.chargeReadyClip != null) AudioSource.PlayClipAtPoint(fx.chargeReadyClip, position, fx.chargeReadyVolume);
        }

        /// <summary>Add camera shake (0..1).</summary>
        public static void Shake(float trauma) => CameraShake.AddTrauma(trauma);

        // ------------------------------------------------------------------ hit-stop

        public void HitStop(float realSeconds)
        {
            if (realSeconds <= 0f || PauseMenuController.GameIsPaused) return;
            _hitStopEndRealtime = Mathf.Max(_hitStopEndRealtime, Time.unscaledTime + realSeconds);
            if (!_hitStopActive)
            {
                _hitStopActive = true;
                Time.timeScale = hitStopTimeScale;
            }
        }

        void Update()
        {
            if (!_hitStopActive) return;

            if (PauseMenuController.GameIsPaused)
            {
                // The pause menu owns timeScale now; just drop our hit-stop.
                _hitStopActive = false;
                return;
            }

            if (Time.unscaledTime >= _hitStopEndRealtime)
            {
                _hitStopActive = false;
                Time.timeScale = 1f;
            }
        }

        // ------------------------------------------------------------------ damage numbers

        void SpawnDamageNumber(Vector3 position, int amount, Color color, float scale)
        {
            DamageNumber number = null;
            for (int i = 0; i < _numberPool.Count; i++)
            {
                if (_numberPool[i] != null && !_numberPool[i].gameObject.activeSelf) { number = _numberPool[i]; break; }
            }
            if (number == null)
            {
                number = DamageNumber.Create(transform);
                _numberPool.Add(number);
            }

            Vector3 jitter = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0f, 0.3f), Random.Range(-0.3f, 0.3f));
            number.Show(position + jitter, amount.ToString(), color, damageNumberSize * scale);
        }

        static Vector3 GetTopOf(GameObject go)
        {
            var col = go.GetComponentInChildren<Collider>();
            if (col != null && col.enabled) return new Vector3(col.bounds.center.x, col.bounds.max.y + 0.3f, col.bounds.center.z);
            var rend = go.GetComponentInChildren<Renderer>();
            if (rend != null) return new Vector3(rend.bounds.center.x, rend.bounds.max.y + 0.3f, rend.bounds.center.z);
            return go.transform.position + Vector3.up * 2f;
        }
    }
}
