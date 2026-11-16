using System.Collections;
using Ishana.Elements;
using UnityEngine;

/// <summary>
/// A boulder that goes through stages instead of vanishing in one frame:
///
///   Intact  ->  Cracked  ->  Crumbling (shakes, sheds dust)  ->  breaks into chunks  ->  chunks fade away
///
/// By default a boulder only breaks from one strong hit (a charged shot), like before. A hit that is
/// close to strong enough cracks it, as a hint that more charge will do it. Turn on "Accumulate Damage"
/// for boulders that weaker shots can wear down over several hits.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BreakableBoulder : MonoBehaviour
{
    public enum Stage { Intact, Cracked, Crumbling, Broken }

    /// <summary>Fired when a boulder breaks (used by the save system).</summary>
    public static event System.Action<BreakableBoulder> OnBoulderBroken;

    [Header("Breaking Mechanics")]
    [Tooltip("The minimum amount of damage from a single hit required to break this boulder.")]
    public int damageThreshold = 50; // Set this value in the Unity Inspector
    [Tooltip("If on, weaker hits add up until the total reaches the damage threshold.")]
    public bool accumulateDamage = false;
    [Tooltip("A hit (or accumulated damage) of at least this fraction of the threshold cracks the boulder.")]
    [Range(0.1f, 1f)] public float crackFraction = 0.5f;

    [Header("Crumbling")]
    [Tooltip("Seconds the boulder shakes and sheds dust before it breaks apart.")]
    public float crumbleTime = 0.8f;
    [Tooltip("How many chunks it breaks into.")]
    public int chunkCount = 10;
    [Tooltip("Seconds the chunks lie around before they fade away.")]
    public float chunkLifetime = 4f;
    [Tooltip("How much darker the boulder gets when cracked (0 = no change).")]
    [Range(0f, 0.8f)] public float crackDarkening = 0.35f;

    [Header("Sound and Effects")]
    [Tooltip("The AudioClip to play when the boulder breaks.")]
    public AudioClip breakSound;
    [Tooltip("Optional: sound for a hit that cracks or chips the boulder.")]
    public AudioClip crackSound;
    [Tooltip("Optional: A particle effect prefab (like dust or debris) to instantiate when the boulder breaks.")]
    public GameObject breakEffectPrefab;

    public Stage CurrentStage { get; private set; } = Stage.Intact;
    public bool IsBroken => isBroken;

    // --- Private Variables ---
    private AudioSource audioSource;
    private bool isBroken = false; // Flag to prevent breaking multiple times
    private int damageTaken;
    private Renderer[] renderers;
    private Vector3 restPosition;
    private float shakeUntil, shakeStrength;
    private static MaterialPropertyBlock block;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake()
    {
        // Get the AudioSource component attached to this GameObject
        audioSource = GetComponent<AudioSource>();
        // Make sure the break sound doesn't play automatically when the game starts
        audioSource.playOnAwake = false;
        renderers = GetComponentsInChildren<Renderer>();
        restPosition = transform.localPosition;
    }

    /// <summary>
    /// Call this method from your bullet script (or any damage source)
    /// to apply damage to the boulder.
    /// </summary>
    /// <param name="damageAmount">The amount of damage dealt by the hit.</param>
    public void TakeDamage(int damageAmount)
    {
        if (isBroken || damageAmount <= 0) return;

        if (accumulateDamage) damageTaken += damageAmount;
        int effective = accumulateDamage ? damageTaken : damageAmount;

        if (effective >= damageThreshold)
        {
            // --- The Boulder Breaks! ---
            isBroken = true; // Set the flag immediately to prevent duplicate breaks
            Debug.Log($"Boulder hit with {damageAmount} damage (Threshold: {damageThreshold}). BREAKING!");
            StartCoroutine(CrumbleRoutine());
            return;
        }

        // Not enough to break it: chip it, and crack it if the hit was close
        float size = GetBounds().extents.magnitude;
        ElementFX.Dust(transform.position, Mathf.Max(0.3f, size * 0.25f));
        Shake(0.25f, size * 0.01f);
        if (crackSound != null) AudioSource.PlayClipAtPoint(crackSound, transform.position, audioSource.volume);

        if (CurrentStage == Stage.Intact && effective >= damageThreshold * crackFraction)
        {
            CurrentStage = Stage.Cracked;
            Darken(crackDarkening);
            Shake(0.4f, size * 0.02f);
        }
    }

    /// <summary>Break the boulder now, whatever its damage threshold (used by scripted events).</summary>
    public void BreakNow()
    {
        if (isBroken) return;
        isBroken = true;
        StartCoroutine(CrumbleRoutine());
    }

    IEnumerator CrumbleRoutine()
    {
        CurrentStage = Stage.Crumbling;
        Bounds bounds = GetBounds();
        float size = bounds.extents.magnitude;

        // Tell the save system straight away, in case the game is closed mid-crumble
        OnBoulderBroken?.Invoke(this);

        // Stage 1: crumble. Darken, shake and shed dust.
        Darken(Mathf.Max(crackDarkening, 0.45f));
        if (crackSound != null) AudioSource.PlayClipAtPoint(crackSound, transform.position, audioSource.volume);
        float end = Time.time + crumbleTime;
        float nextDust = 0f;
        while (Time.time < end)
        {
            float t = 1f - (end - Time.time) / Mathf.Max(0.01f, crumbleTime);
            Shake(0.1f, size * Mathf.Lerp(0.01f, 0.04f, t));
            if (Time.time >= nextDust)
            {
                nextDust = Time.time + 0.15f;
                Vector3 p = bounds.center + Vector3.Scale(Random.insideUnitSphere, bounds.extents);
                ElementFX.Dust(p, Mathf.Max(0.3f, size * 0.2f));
            }
            yield return null;
        }

        // Stage 2: break apart
        CurrentStage = Stage.Broken;
        if (breakSound != null)
            AudioSource.PlayClipAtPoint(breakSound, transform.position, audioSource.volume); // Plays even though the boulder is going away
        if (breakEffectPrefab != null)
            Instantiate(breakEffectPrefab, transform.position, transform.rotation);
        ElementFX.Dust(bounds.center, Mathf.Max(0.5f, size * 0.5f));
        Ishana.Combat.CombatFeedback.Shake(0.3f);

        Material material = null;
        foreach (Renderer r in renderers)
            if (r != null && r.sharedMaterial != null) { material = r.sharedMaterial; break; }

        // The player walks through the rubble instead of getting stuck on it
        Collider[] ignore = null;
        var player = FindFirstObjectByType<PlayerStateManager>();
        if (player != null) ignore = player.GetComponentsInChildren<Collider>();

        foreach (Renderer r in renderers) if (r != null) r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

        RockDebris.Spawn(bounds, material, Mathf.Max(0, chunkCount), chunkLifetime, ignore);

        // Stage 3: the chunks fade on their own; the boulder itself is done
        Destroy(gameObject);
    }

    Bounds GetBounds()
    {
        bool found = false;
        Bounds b = new Bounds(transform.position, Vector3.one);
        foreach (Renderer r in renderers)
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            if (!found) { b = r.bounds; found = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    void Darken(float amount)
    {
        if (amount <= 0f) return;
        if (block == null) block = new MaterialPropertyBlock();
        foreach (Renderer r in renderers)
        {
            if (r == null || r.sharedMaterial == null) continue;
            Material m = r.sharedMaterial;
            int prop = m.HasProperty(BaseColorId) ? BaseColorId : m.HasProperty(ColorId) ? ColorId : 0;
            if (prop == 0) continue;
            r.GetPropertyBlock(block);
            block.SetColor(prop, m.GetColor(prop) * (1f - amount));
            r.SetPropertyBlock(block);
        }
    }

    void Shake(float seconds, float strength)
    {
        shakeUntil = Mathf.Max(shakeUntil, Time.time + seconds);
        shakeStrength = Mathf.Max(strength, 0f);
    }

    void LateUpdate()
    {
        if (shakeUntil <= 0f) return;
        if (Time.time >= shakeUntil)
        {
            shakeUntil = 0f;
            transform.localPosition = restPosition;
            return;
        }
        // Shake in world units, whatever the boulder's parent scale is
        Vector3 offset = Random.insideUnitSphere * shakeStrength;
        transform.localPosition = restPosition +
            (transform.parent != null ? transform.parent.InverseTransformVector(offset) : offset);
    }
}
