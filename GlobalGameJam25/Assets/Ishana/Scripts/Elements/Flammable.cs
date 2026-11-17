using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ishana.Elements
{
    public enum PlantState { Healthy = 0, Burning = 1, Scorched = 2, Burnt = 3 }

    /// <summary>
    /// A plant (tree, bush, grass, weeds) that can catch fire.
    ///
    ///   Healthy  --fire-->  Burning  --burns out-->  Burnt
    ///                          |  ice                  | earth
    ///                          v                       v
    ///                       Scorched  ---earth--->  Healthy (regrows)
    ///
    /// Fire spreads to other plants nearby. Ice puts a fire out and leaves the plant damp for a moment
    /// so it doesn't relight straight away. Earth magic regrows scorched and burnt plants.
    ///
    /// Works on any object: add it to a tree prefab and it finds the renderers itself. If the object has
    /// separate leaves, put them in "Foliage" so they wither as the plant burns.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ishana/Flammable Plant")]
    public class Flammable : ElementalObject
    {
        /// <summary>How many plants may burn at once. Protects the frame rate and stops the whole map going up.</summary>
        public static int MaxSimultaneousFires = 30;
        /// <summary>How many fires may carry a real light at once. Each light is costly when many plants are in view.</summary>
        public static int MaxFireLights = 4;
        static int _fireLights;
        public static int BurningCount { get; private set; }
        public static event Action<Flammable, PlantState> OnStateChanged;
        /// <summary>Fired each time a burning plant tries to spread, before it looks for neighbours.</summary>
        public static event Action<Flammable> OnSpreadTick;

        [Header("Burning")]
        [Tooltip("Seconds of burning before the plant is completely burnt.")]
        public float burnDuration = 14f;
        [Tooltip("Chance (0-1) that a weak fire shot lights it. Charged shots and neighbouring fires add to this.")]
        [Range(0f, 1f)] public float flammability = 0.8f;
        [Tooltip("Seconds it stays damp (can't catch fire) after ice puts it out.")]
        public float dampTime = 6f;

        [Header("Spreading")]
        [Tooltip("How far the fire can jump to another plant, beyond this plant's own size.")]
        public float spreadRadius = 3f;
        [Tooltip("Seconds between spread attempts.")]
        public float spreadInterval = 1.5f;
        [Tooltip("Chance (0-1) that each attempt lights a given neighbour.")]
        [Range(0f, 1f)] public float spreadChance = 0.25f;

        [Header("Regrowing")]
        [Tooltip("Seconds earth magic takes to regrow the plant.")]
        public float regrowTime = 3f;
        [Tooltip("Regrow by itself this many seconds after burning (0 = only earth magic heals it).")]
        public float autoRegrowAfter = 0f;

        [Header("Look")]
        [Tooltip("Leaves/needles. They wither as the plant burns and come back when it heals. Optional.")]
        public Transform[] foliage;
        [Tooltip("Small plants (grass, weeds) shrink to a stub instead of leaving a charred shape standing.")]
        public bool collapsesWhenBurnt = false;
        public Color charredColor = new Color(0.07f, 0.06f, 0.055f);
        [Tooltip("Size of the flames compared to the plant.")]
        public float flameScale = 1f;
        [Tooltip("Keep a thin trail of smoke for a few seconds after burning out. Off for grass and ground patches: many of them burn at once.")]
        public bool smoulders = true;

        public PlantState State { get; private set; } = PlantState.Healthy;
        /// <summary>0 = untouched, 1 = completely burnt.</summary>
        public float BurnAmount => _burn;
        public override int StateId => (int)State;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static MaterialPropertyBlock _block;

        struct Slot { public Renderer renderer; public int index; public Color color; public int property; }

        readonly List<Slot> _slots = new List<Slot>();
        Vector3[] _foliageScales;
        Vector3 _baseScale;
        bool _cached;
        float _burn;            // Shown damage, 0..1
        float _regrowFrom = -1f; // >= 0 while regrowing
        float _regrowTimer;
        float _dampUntil;
        float _nextSpread;
        float _burntAt;
        Transform _fxRoot;
        ParticleSystem _flames, _smoke;
        Light _light;

        protected override void Awake()
        {
            base.Awake();
            Cache();
        }

        void Cache()
        {
            if (_cached) return;
            _cached = true;
            _baseScale = transform.localScale;

            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;
                    int prop = m.HasProperty(BaseColorId) ? BaseColorId : m.HasProperty(ColorId) ? ColorId : 0;
                    if (prop == 0) continue;
                    _slots.Add(new Slot { renderer = r, index = i, color = m.GetColor(prop), property = prop });
                }
            }

            if (foliage != null)
            {
                _foliageScales = new Vector3[foliage.Length];
                for (int i = 0; i < foliage.Length; i++)
                    _foliageScales[i] = foliage[i] != null ? foliage[i].localScale : Vector3.one;
            }
        }

        void OnDisable()
        {
            if (State == PlantState.Burning) BurningCount = Mathf.Max(0, BurningCount - 1);
        }

        void OnEnable()
        {
            if (State == PlantState.Burning) BurningCount++;
        }

        /// <summary>Tell the plant which parts are leaves (for plants built in code).</summary>
        public void SetFoliage(params Transform[] parts)
        {
            Cache();
            foliage = parts;
            _foliageScales = new Vector3[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                _foliageScales[i] = parts[i] != null ? parts[i].localScale : Vector3.one;
        }

        /// <summary>Work out the size of the plant from what it looks like (for prefabs).</summary>
        public void AutoSize()
        {
            bool found = false;
            Bounds b = new Bounds(transform.position, Vector3.zero);
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!found) { b = r.bounds; found = true; } else b.Encapsulate(r.bounds);
            }
            if (!found) return;
            reachRadius = Mathf.Max(0.2f, Mathf.Max(b.extents.x, b.extents.z));
            centerHeight = Mathf.Max(0.1f, b.center.y - transform.position.y);
        }

        /// <summary>The leaves were resized from outside (a tree grew): remember the new size.</summary>
        public void RefreshFoliageSize()
        {
            if (foliage == null || _burn > 0.001f) return;
            SetFoliage(foliage);
        }

        /// <summary>Regrow every scorched or burnt plant near a point. Returns how many it started healing.</summary>
        public static int HealAround(Vector3 point, float radius)
        {
            int healed = 0;
            IReadOnlyList<ElementalObject> all = All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (i >= all.Count || !(all[i] is Flammable f) || !f.gameObject.activeInHierarchy) continue;
                if (f.State != PlantState.Scorched && f.State != PlantState.Burnt) continue;
                float reach = radius + f.reachRadius;
                Vector3 d = f.transform.position - point;
                d.y = Mathf.Max(0f, Mathf.Abs(d.y) - f.centerHeight * 2f);
                if (d.sqrMagnitude > reach * reach) continue;
                f.Heal();
                healed++;
            }
            return healed;
        }

        // ------------------------------------------------------------------ reactions

        public override void ApplyElement(Element element, float power, Vector3 point)
        {
            switch (element)
            {
                case Element.Fire:
                    if (UnityEngine.Random.value <= flammability + Mathf.Max(0f, power - 1f) * 0.25f) Ignite();
                    break;
                case Element.Ice:
                    Extinguish();
                    break;
                case Element.Earth:
                    Heal();
                    break;
            }
        }

        /// <summary>Set the plant on fire. Returns false if it can't burn right now.</summary>
        public bool Ignite()
        {
            if (!gameObject.activeInHierarchy) return false;
            if (State == PlantState.Burning || State == PlantState.Burnt) return false;
            if (Time.time < _dampUntil || BurningCount >= MaxSimultaneousFires) return false;

            _regrowFrom = -1f;
            enabled = true; // Wake up before the state changes, so the fire is only counted once
            SetState(PlantState.Burning);
            BurningCount++;
            _nextSpread = Time.time + spreadInterval * UnityEngine.Random.Range(0.6f, 1.2f);
            StartFire();
            return true;
        }

        /// <summary>Put the fire out (ice magic). The plant stays scorched until healed.</summary>
        public void Extinguish()
        {
            _dampUntil = Time.time + dampTime;
            if (State != PlantState.Burning) return;

            BurningCount = Mathf.Max(0, BurningCount - 1);
            StopFire(smolder: false);
            ElementFX.Steam(Center, Mathf.Max(0.5f, reachRadius));
            _burntAt = Time.time;
            SetState(_burn >= 0.98f ? PlantState.Burnt : PlantState.Scorched);
        }

        /// <summary>Regrow a scorched or burnt plant (earth magic).</summary>
        public void Heal()
        {
            if (State != PlantState.Scorched && State != PlantState.Burnt) return;
            if (_regrowFrom >= 0f) return; // Already regrowing

            StopFire(smolder: false);
            ElementFX.Heal(Center, Mathf.Max(0.5f, reachRadius));
            _regrowFrom = _burn;
            _regrowTimer = 0f;
            enabled = true;
        }

        public override void RestoreState(int stateId)
        {
            Cache();
            PlantState target = (PlantState)Mathf.Clamp(stateId, 0, 3);
            if (target == PlantState.Burning) target = PlantState.Scorched; // Fires don't survive being unloaded
            if (State == PlantState.Burning) { BurningCount = Mathf.Max(0, BurningCount - 1); StopFire(false); }

            State = target;
            _regrowFrom = -1f;
            _burn = target == PlantState.Burnt ? 1f : target == PlantState.Scorched ? Mathf.Max(_burn, 0.5f) : 0f;
            _burntAt = Time.time;
            ApplyLook();
        }

        void SetState(PlantState state)
        {
            if (State == state) return;
            State = state;
            OnStateChanged?.Invoke(this, state);
        }

        // ------------------------------------------------------------------ update (only runs while something is changing)

        void Update()
        {
            if (State == PlantState.Burning)
            {
                _burn = Mathf.Min(1f, _burn + Time.deltaTime / Mathf.Max(0.1f, burnDuration));
                ApplyLook();

                if (_light != null)
                    _light.intensity = _lightIntensity * (0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 9f, GetInstanceID() * 0.137f));

                if (Time.time >= _nextSpread)
                {
                    _nextSpread = Time.time + spreadInterval;
                    Spread();
                }

                if (_burn >= 1f)
                {
                    BurningCount = Mathf.Max(0, BurningCount - 1);
                    StopFire(smolder: smoulders && !collapsesWhenBurnt);
                    _burntAt = Time.time;
                    SetState(PlantState.Burnt);
                }
                return;
            }

            if (_regrowFrom >= 0f)
            {
                _regrowTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_regrowTimer / Mathf.Max(0.1f, regrowTime));
                _burn = Mathf.Lerp(_regrowFrom, 0f, Mathf.SmoothStep(0f, 1f, t));
                ApplyLook();
                if (t >= 1f)
                {
                    _regrowFrom = -1f;
                    _burn = 0f;
                    ApplyLook();
                    SetState(PlantState.Healthy);
                }
                return;
            }

            // Scorched or burnt: wait for the smoke to finish, then for auto-regrow (if any), otherwise sleep
            bool smoking = _smoke != null;
            if (smoking && Time.time > _burntAt + 4f) { StopFire(false); smoking = false; }

            if (autoRegrowAfter > 0f && State != PlantState.Healthy)
            {
                if (Time.time > _burntAt + autoRegrowAfter) Heal();
            }
            else if (!smoking)
            {
                enabled = false;
            }
        }

        void Spread()
        {
            OnSpreadTick?.Invoke(this);
            IReadOnlyList<ElementalObject> all = All;
            Vector3 here = transform.position;
            // Fire gets better at spreading as it takes hold
            float chance = spreadChance * Mathf.Lerp(0.5f, 1.2f, _burn);

            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (i >= all.Count) continue; // List can shrink while we light things
                if (!(all[i] is Flammable other) || other == this || !other.gameObject.activeInHierarchy) continue;
                if (other.State != PlantState.Healthy && other.State != PlantState.Scorched) continue;
                if (other.State == PlantState.Scorched && other._burn >= 0.98f) continue;

                float reach = reachRadius + spreadRadius + other.reachRadius;
                Vector3 d = other.transform.position - here;
                if (d.sqrMagnitude > reach * reach) continue;
                if (UnityEngine.Random.value <= chance * other.flammability) other.Ignite();
            }
        }

        // ------------------------------------------------------------------ visuals

        float _lightIntensity;

        void StartFire()
        {
            enabled = true;
            StopFire(false);

            float radius = Mathf.Max(0.3f, reachRadius * flameScale);
            float height = Mathf.Max(0.6f, centerHeight * 2f * flameScale);

            _fxRoot = new GameObject("Fire").transform;
            _fxRoot.SetParent(transform, false);
            // Counter the plant's own scale so the fire is sized in world units
            Vector3 ls = transform.lossyScale;
            _fxRoot.localScale = new Vector3(Inv(ls.x), Inv(ls.y), Inv(ls.z));

            // The more fires there are, the fewer particles each one gets: a wide fire still reads as a wide fire
            float thin = Mathf.Lerp(1f, 0.45f, Mathf.InverseLerp(4f, 24f, BurningCount));
            _flames = ElementFX.Flames(_fxRoot, radius, height, thin);
            _smoke = ElementFX.Smoke(_fxRoot, radius, height, 3f * thin);
            _flames.Play();
            _smoke.Play();

            // Lights are expensive: only bigger fires get one
            if (height > 2.5f && _fireLights < MaxFireLights)
            {
                _fireLights++;
                var lightGo = new GameObject("Fire Light");
                lightGo.transform.SetParent(_fxRoot, false);
                lightGo.transform.localPosition = Vector3.up * height * 0.5f;
                _light = lightGo.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.color = new Color(1f, 0.55f, 0.2f);
                _light.range = radius * 6f + height;
                _lightIntensity = 2.5f;
                _light.intensity = _lightIntensity;
                _light.shadows = LightShadows.None;
            }
        }

        static float Inv(float v) => Mathf.Abs(v) > 0.0001f ? 1f / v : 1f;

        void StopFire(bool smolder)
        {
            if (_fxRoot == null) return;

            if (smolder && _smoke != null)
            {
                // Keep a thin trail of smoke for a while
                if (_flames != null) _flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (_light != null) { Destroy(_light.gameObject); _fireLights = Mathf.Max(0, _fireLights - 1); }
                var emission = _smoke.emission;
                emission.rateOverTime = 1.5f;
                _flames = null;
                _light = null;
                return;
            }

            if (_light != null) _fireLights = Mathf.Max(0, _fireLights - 1);
            Destroy(_fxRoot.gameObject);
            _fxRoot = null;
            _flames = null;
            _smoke = null;
            _light = null;
        }

        void ApplyLook()
        {
            Cache();
            if (_block == null) _block = new MaterialPropertyBlock();

            // Colour: darken toward charcoal. A healthy plant uses its plain shared material (cheapest to draw).
            float tint = Mathf.SmoothStep(0f, 1f, _burn);
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot s = _slots[i];
                if (s.renderer == null) continue;
                if (_burn <= 0.001f) { s.renderer.SetPropertyBlock(null, s.index); continue; }

                s.renderer.GetPropertyBlock(_block, s.index);
                _block.SetColor(s.property, Color.Lerp(s.color, charredColor, tint));
                s.renderer.SetPropertyBlock(_block, s.index);
            }

            // Leaves wither in the second half of the burn
            if (foliage != null && _foliageScales != null)
            {
                float leaf = Mathf.Lerp(1f, 0.12f, Mathf.InverseLerp(0.35f, 1f, _burn));
                for (int i = 0; i < foliage.Length; i++)
                    if (foliage[i] != null) foliage[i].localScale = _foliageScales[i] * leaf;
            }

            // Grass and weeds burn down to a stub
            if (collapsesWhenBurnt)
            {
                float h = Mathf.Lerp(1f, 0.15f, Mathf.InverseLerp(0.2f, 1f, _burn));
                transform.localScale = new Vector3(_baseScale.x, _baseScale.y * h, _baseScale.z);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_light != null) _fireLights = Mathf.Max(0, _fireLights - 1);
            // Clear tints so shared renderers aren't left with stale blocks (matters for pooled objects)
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].renderer != null) _slots[i].renderer.SetPropertyBlock(null, _slots[i].index);
        }
    }
}
