using Ishana.Elements;
using UnityEngine;

/// <summary>
/// Earth magic. While charging it looks like the other shots (a green orb in the hand), but on release
/// it fires an instant beam at whatever the crosshair is on:
///
/// - Burnt or scorched plants (hit directly, or around where the beam lands) regrow.
/// - A healthy plant that is able to grow (has a GrowablePlant component) shoots up and sprouts
///   branch platforms to climb.
/// - Bare ground rises into a solid mound that can be stood on, then crumbles away after a few seconds.
///   Standing on the spot when it rises carries you up with it. A charged beam raises a bigger mound.
///
/// If no Earth Bullet prefab is assigned on the BulletSpawnerState, a green copy of the basic bullet
/// is built at runtime, so the shot works without any prefab setup.
/// </summary>
public class EarthBullet : MonoBehaviour, PlayerShooting.IBulletChargeReceiver
{
    public static readonly Color EarthColor = new Color(0.4f, 0.85f, 0.3f);

    [Header("Beam")]
    [Tooltip("How far the beam reaches, in player heights.")]
    public float rangeInPlayerHeights = 40f;
    [Tooltip("Seconds the beam stays visible.")]
    public float beamTime = 0.3f;
    [Tooltip("Beam width as a fraction of the player's height.")]
    public float beamWidth = 0.12f;

    [Header("Healing")]
    [Tooltip("Burnt plants within this many player heights of where the beam lands are regrown (uncharged / fully charged).")]
    public Vector2 healRadius = new Vector2(2f, 6f);

    [Header("Raised Ground")]
    [Tooltip("Mound height as a multiple of the player's jump height (uncharged / fully charged).")]
    public Vector2 moundHeight = new Vector2(0.6f, 1.4f);
    [Tooltip("Mound radius in player heights (uncharged / fully charged).")]
    public Vector2 moundRadius = new Vector2(1.2f, 2.2f);
    [Tooltip("Seconds the mound stands before it starts to crumble.")]
    public float moundLifetime = 5f;
    [Tooltip("Steepest ground (degrees) a mound can be raised from.")]
    public float maxGroundSlope = 50f;

    [Tooltip("Optional particle effect prefab spawned where the beam lands.")]
    public GameObject hitEffectPrefab;

    static readonly RaycastHit[] _hits = new RaycastHit[32];

    bool _fired;
    LineRenderer _line;
    float _fadeStart;

    public void OnFire(float chargeRatio, float forceMultiplier, PlayerStateManager playerRef)
    {
        if (_fired) return;
        _fired = true;
        float charge = Mathf.Clamp01(chargeRatio);

        // The orb itself goes no further: stop it and hide it, the beam takes over
        var body = GetComponent<Rigidbody>();
        if (body != null) body.isKinematic = true;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        foreach (Light l in GetComponentsInChildren<Light>()) l.enabled = false;

        // Sizes follow the player, so this works whatever scale the world is built at
        float playerHeight = 2f, jump = 2f;
        Transform playerRoot = null;
        if (playerRef != null)
        {
            playerRoot = playerRef.transform;
            var cc = playerRef.GetComponent<CharacterController>();
            if (cc != null) playerHeight = Mathf.Max(0.5f, cc.height * playerRef.transform.lossyScale.y);
            jump = Mathf.Clamp(playerRef.jumpForce, playerHeight * 0.5f, playerHeight * 6f);
        }
        float range = rangeInPlayerHeights * playerHeight;

        // Aim: where the crosshair points, then a straight line from the hand to that spot
        Vector3 origin = transform.position;
        Vector3 target = origin + transform.forward * range;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Ray aim = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            // Ignore anything between the camera and the player
            float skip = Mathf.Max(0f, Vector3.Dot(origin - aim.origin, aim.direction));
            target = Cast(aim, range + skip, skip, playerRoot, out RaycastHit aimHit) ? aimHit.point : aim.GetPoint(range + skip);
        }

        Vector3 direction = (target - origin).sqrMagnitude > 0.0001f ? (target - origin).normalized : transform.forward;
        bool hitSomething = Cast(new Ray(origin, direction), range, 0f, playerRoot, out RaycastHit hit);
        Vector3 end = hitSomething ? hit.point : origin + direction * range;

        DrawBeam(origin, end, beamWidth * playerHeight);
        if (hitSomething) Land(hit, charge, playerHeight, jump, playerRef);

        Destroy(gameObject, beamTime + 0.05f);
    }

    void Land(RaycastHit hit, float charge, float playerHeight, float jump, PlayerStateManager player)
    {
        Vector3 point = hit.point;
        if (hitEffectPrefab != null) Instantiate(hitEffectPrefab, point, Quaternion.identity);
        ElementFX.Heal(point, playerHeight * 0.5f);

        float heal = Mathf.Lerp(healRadius.x, healRadius.y, charge) * playerHeight;

        // --- A plant: heal it, or make it grow
        Flammable plant = hit.collider.GetComponentInParent<Flammable>();
        if (plant != null)
        {
            if (plant.State == PlantState.Scorched || plant.State == PlantState.Burnt)
            {
                plant.Heal();
                Flammable.HealAround(point, heal);
            }
            else if (plant.State == PlantState.Healthy)
            {
                GrowablePlant growable = plant.GetComponent<GrowablePlant>();
                if (growable != null) growable.Grow();
                Flammable.HealAround(point, heal);
            }
            return;
        }

        // --- Something that isn't ground (enemy, boulder, moving object): nothing more happens
        if (hit.collider.attachedRigidbody != null
            || hit.collider.GetComponentInParent<BaseEnemy>() != null
            || hit.collider.GetComponentInParent<BreakableBoulder>() != null
            || hit.collider.name == "Branch Platform") return;

        // --- Ground: burnt plants here regrow. If there was nothing to heal, the ground itself rises.
        if (Flammable.HealAround(point, heal) > 0) return;
        if (Vector3.Angle(hit.normal, Vector3.up) > maxGroundSlope) return;

        float height = Mathf.Lerp(moundHeight.x, moundHeight.y, charge) * jump;
        float radius = Mathf.Max(Mathf.Lerp(moundRadius.x, moundRadius.y, charge) * playerHeight, height * 0.45f);
        EarthMound.Raise(point, radius, height, moundLifetime, player);
    }

    /// <summary>Nearest solid hit along a ray, ignoring the player, this orb and triggers.</summary>
    bool Cast(Ray ray, float range, float minDistance, Transform ignoreRoot, out RaycastHit best)
    {
        best = default;
        float bestDistance = float.MaxValue;
        int count = Physics.RaycastNonAlloc(ray, _hits, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = _hits[i];
            if (h.distance < minDistance || h.distance >= bestDistance) continue;
            Transform t = h.collider.transform;
            if (t.IsChildOf(transform) || (ignoreRoot != null && t.IsChildOf(ignoreRoot))) continue;
            if (h.collider.CompareTag("Player")) continue;
            best = h;
            bestDistance = h.distance;
        }
        return bestDistance < float.MaxValue;
    }

    void DrawBeam(Vector3 from, Vector3 to, float width)
    {
        _line = gameObject.AddComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.positionCount = 2;
        _line.SetPosition(0, from);
        _line.SetPosition(1, to);
        _line.sharedMaterial = ElementFX.ParticleMaterial;
        _line.textureMode = LineTextureMode.Stretch;
        _line.numCapVertices = 4;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
        _line.startWidth = width;
        _line.endWidth = width * 1.6f;
        _line.startColor = _line.endColor = EarthColor;
        _fadeStart = Time.time;
    }

    void Update()
    {
        if (_line == null) return;
        float t = Mathf.Clamp01((Time.time - _fadeStart) / Mathf.Max(0.01f, beamTime));
        Color c = Color.Lerp(new Color(0.9f, 1f, 0.7f, 1f), new Color(EarthColor.r, EarthColor.g, EarthColor.b, 0f), t);
        _line.startColor = _line.endColor = c;
        _line.widthMultiplier = 1f - t * 0.6f;
    }

    /// <summary>
    /// Build an (inactive) earth orb to copy from, based on another bullet prefab.
    /// </summary>
    public static GameObject CreateTemplate(GameObject basedOn, Transform parent)
    {
        if (basedOn == null) return null;

        GameObject template = Instantiate(basedOn, parent);
        template.name = "EarthBullet (runtime template)";
        template.SetActive(false);

        // Swap the bullet behaviour for the earth one
        foreach (MonoBehaviour behaviour in template.GetComponents<MonoBehaviour>())
            if (behaviour is PlayerShooting.IBulletChargeReceiver) DestroyImmediate(behaviour);
        template.AddComponent<EarthBullet>();

        // Recolour it green
        foreach (Renderer r in template.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            Material m = r.material; // Own copy, so the basic bullet keeps its colour
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", EarthColor);
            if (m.HasProperty("_Color")) m.SetColor("_Color", EarthColor);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", EarthColor * 1.5f);
        }
        foreach (ParticleSystem ps in template.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = EarthColor;
        }
        foreach (Light l in template.GetComponentsInChildren<Light>(true)) l.color = EarthColor;
        foreach (TrailRenderer t in template.GetComponentsInChildren<TrailRenderer>(true))
        {
            t.startColor = EarthColor;
            t.endColor = new Color(EarthColor.r, EarthColor.g, EarthColor.b, 0f);
        }

        return template;
    }
}
