using UnityEngine;
using System.Collections; // Needed for Coroutine

// Add this enum if you don't have it already
public enum DamageType { Basic, Freeze, Other } // Example types

// Ensure an AudioSource component exists on the GameObject
[RequireComponent(typeof(AudioSource))]
public abstract class BaseEnemy : MonoBehaviour
{
    [Header("Base Enemy Stats")]
    public int maxHealth = 50;
    public int currentHealth;
    public float moveSpeed = 3f;
    public float detectionRange = 20f;
    public float loseSightRange = 25f;
    public float fleeHealthPercentage = 0.3f;
    public float baseFreezeDuration = 5f;

    [Header("XP")]
    public int xpValue = 10;

    [Header("Effects & References")]
    public GameObject deathEffectPrefab;
    public GameObject freezeEffectPrefab;
    protected GameObject currentFreezeEffectInstance;
    public AreaCleansingManager myAreaManager;

    // ----- NEW AUDIO -----
    [Header("Audio")]
    [Tooltip("Main AudioSource for one-shot effects like attack, damage, death.")]
    protected AudioSource mainAudioSource; // Renamed from audioSource for clarity
    [Tooltip("Optional: Separate AudioSource for looping ambient sounds (like grunts). Configure it to Loop in the Inspector.")]
    public AudioSource ambientAudioSource;

    [Header("Sound Clips")]
    [Tooltip("Sound played when the enemy first spawns/becomes active.")]
    public AudioClip spawnSound;
    [Tooltip("Sound played when the enemy starts chasing the player.")]
    public AudioClip chaseSound;
    [Tooltip("Sound played when the enemy takes damage.")]
    public AudioClip takeDamageSound;
    [Tooltip("Sound played when the enemy dies.")]
    public AudioClip deathSound;
    [Tooltip("Sound played continuously while idle/patrolling. Assign to the Ambient Audio Source.")]
    public AudioClip ambientGruntSound;
    [Tooltip("How long to wait after playing the death sound before destroying the object.")]
    public float deathSoundDelay = 1.0f; // Default delay of 1 second for death sound

    // ----- END NEW AUDIO -----


    // State Machine
    public enum EnemyState { Idle, Patrol, Chase, Attack, Flee, Frozen, Dying }
    public EnemyState currentState = EnemyState.Idle;

    protected Transform playerTransform;
    protected bool _isFrozen = false;
    protected float freezeTimer = 0f;
    protected float fleeHealthThreshold;

    // --- Abstract Methods ---
    protected abstract void Patrol();
    protected abstract void Chase();
    protected abstract void Attack();
    protected abstract void Flee();
    protected abstract float GetAttackRange();
    protected abstract void UpdateHealthBar();

    // --- Virtual Methods ---
    protected virtual void Awake()
    {
        myAreaManager = FindFirstObjectByType<AreaCleansingManager>();
        currentHealth = maxHealth;
        fleeHealthThreshold = maxHealth * fleeHealthPercentage;

        // --- AUDIO: Get the main AudioSource ---
        mainAudioSource = GetComponent<AudioSource>();
        if (mainAudioSource == null)
        {
            Debug.LogError($"[{gameObject.name}] Critical Error: Missing required AudioSource component!");
        }
        // Configure main source for one-shots
        mainAudioSource.playOnAwake = false;
        mainAudioSource.loop = false;
        // ------------------------------------
    }

    protected virtual void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) playerTransform = playerObject.transform;
        else Debug.LogWarning($"Enemy '{gameObject.name}' could not find GameObject with tag 'Player'.", this);

        UpdateHealthBar();

        // --- AUDIO: Play Spawn Sound ---
        PlaySound(spawnSound, mainAudioSource); // Use main source for spawn alert
        // -----------------------------

        // --- AUDIO: Start Ambient Grunt Loop ---
        if (ambientAudioSource != null && ambientGruntSound != null)
        {
            ambientAudioSource.clip = ambientGruntSound;
            ambientAudioSource.loop = true; // Ensure looping is enabled
            ambientAudioSource.playOnAwake = false; // We control playback
            ambientAudioSource.Play();
        }
        else if (ambientGruntSound != null)
        {
             Debug.LogWarning($"[{gameObject.name}] Has an Ambient Grunt Sound assigned, but no Ambient Audio Source component/reference set in the Inspector.", this);
        }
        // -------------------------------------
    }

    protected virtual void Update()
    {
        if (currentState == EnemyState.Dying) return;

        HandleFreezeTimer();

        if (_isFrozen)
        {
            if (currentState != EnemyState.Frozen) TransitionToState(EnemyState.Frozen);
            // --- AUDIO: Stop ambient sound when frozen ---
            if (ambientAudioSource != null && ambientAudioSource.isPlaying)
            {
                ambientAudioSource.Pause(); // Pause instead of Stop to resume easily
            }
            // ------------------------------------------
            return;
        }
        else if (currentState == EnemyState.Frozen)
        {
             // --- AUDIO: Resume ambient sound when unfrozen ---
             if (ambientAudioSource != null && !ambientAudioSource.isPlaying && ambientGruntSound != null)
             {
                 ambientAudioSource.UnPause(); // Resume if paused
             }
             // ----------------------------------------------
            TransitionToState(EnemyState.Idle); // Transition back after handling freeze
        }

        // Resume ambient sound if it was stopped for some reason other than freezing (e.g. manually)
        if (ambientAudioSource != null && !ambientAudioSource.isPlaying && ambientGruntSound != null && !_isFrozen && currentState != EnemyState.Dying)
        {
            ambientAudioSource.Play();
        }


        // State Machine Logic
        float distanceToPlayer = GetDistanceToPlayer();
        switch (currentState)
        {
            case EnemyState.Idle:
                if (CanSeePlayer(distanceToPlayer)) TransitionToState(EnemyState.Chase);
                else Patrol();
                break;

            case EnemyState.Patrol:
                Patrol();
                if (CanSeePlayer(distanceToPlayer)) TransitionToState(EnemyState.Chase);
                break;

            case EnemyState.Chase:
                if (ShouldFlee()) TransitionToState(EnemyState.Flee);
                else if (distanceToPlayer <= GetAttackRange()) TransitionToState(EnemyState.Attack);
                else if (!CanSeePlayer(distanceToPlayer, loseSightRange)) TransitionToState(EnemyState.Patrol);
                else Chase();
                break;

            case EnemyState.Attack:
                if (ShouldFlee()) TransitionToState(EnemyState.Flee);
                else if (distanceToPlayer > GetAttackRange() * 1.1f) TransitionToState(EnemyState.Chase);
                else if (!CanSeePlayer(distanceToPlayer, loseSightRange)) TransitionToState(EnemyState.Patrol);
                else Attack(); // Attack logic (including sound) is in derived class methods
                break;

            case EnemyState.Flee:
                if (!ShouldFlee() || distanceToPlayer > loseSightRange * 1.5f) TransitionToState(EnemyState.Patrol);
                else Flee();
                break;

            case EnemyState.Frozen:
                // Logic handled above
                break;
        }
    }

    protected virtual void TransitionToState(EnemyState newState)
    {
        if (currentState == newState || currentState == EnemyState.Dying) return;

        // --- AUDIO: Play Chase Sound on entering Chase state ---
        if (newState == EnemyState.Chase && currentState != EnemyState.Chase) // Only play when entering
        {
            PlaySound(chaseSound, mainAudioSource);
        }
        // ----------------------------------------------------

        currentState = newState;

        // --- AUDIO: Manage Ambient Sound based on State ---
        // Optional: Stop ambient sound during attack/flee if desired
        /*
        if (ambientAudioSource != null)
        {
            if (newState == EnemyState.Attack || newState == EnemyState.Flee)
            {
                if (ambientAudioSource.isPlaying) ambientAudioSource.Pause();
            }
            else if (!ambientAudioSource.isPlaying && !_isFrozen) // Resume if not attacking/fleeing/frozen
            {
                 ambientAudioSource.UnPause(); // Or Play() if stopped completely
            }
        }
        */
        // --------------------------------------------------
    }

    public virtual void TakeDamage(int damage, DamageType type = DamageType.Other)
    {
        // Ignore damage if already dying or damage is non-positive
        if (currentState == EnemyState.Dying || damage <= 0) return;

        // --- AUDIO: Play Take Damage Sound ---
        // Play sound *before* checking for death, so you hear the hit that kills
        PlaySound(takeDamageSound, mainAudioSource);
        // -------------------------------------

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log($"{gameObject.name} took {damage} ({type}) damage. Health: {currentHealth}/{maxHealth}");
        UpdateHealthBar();

        if (currentHealth <= 0)
        {
            // Use StartCoroutine to handle the death sequence with delay
            StartCoroutine(DieCoroutine());
        }
        else if (ShouldFlee() && currentState != EnemyState.Flee && currentState != EnemyState.Frozen)
        {
            TransitionToState(EnemyState.Flee);
        }
        // Aggro if hit while idle/patrolling
        else if (currentState == EnemyState.Idle || currentState == EnemyState.Patrol)
        {
             TransitionToState(EnemyState.Chase);
        }
    }

    // --- MODIFIED: Die() is now a Coroutine ---
    protected virtual IEnumerator DieCoroutine()
    {
        if (currentState == EnemyState.Dying) yield break; // Prevent multiple calls

        TransitionToState(EnemyState.Dying);
        Debug.Log($"{gameObject.name} has died.");

        // --- AUDIO: Stop Ambient Sound ---
        if (ambientAudioSource != null && ambientAudioSource.isPlaying)
        {
            ambientAudioSource.Stop();
        }
        // -------------------------------

        // --- AUDIO: Play Death Sound ---
        PlaySound(deathSound, mainAudioSource);
        // -----------------------------

        // --- AUDIO: Wait for the sound/delay ---
        // Use either the clip's length or the specified delay
        float waitTime = deathSound != null ? Mathf.Max(deathSound.length, deathSoundDelay) : deathSoundDelay;
        yield return new WaitForSeconds(waitTime);
        // ---------------------------------------

        // Grant XP
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            PlayerStateManager playerState = playerObject.GetComponent<PlayerStateManager>();
            if (playerState != null) playerState.GainXP(xpValue);
            else Debug.LogError($"Enemy '{gameObject.name}': Could not find PlayerStateManager on player object!", playerObject);
        }
        else Debug.LogWarning($"Enemy '{gameObject.name}': Could not find Player object to grant XP.");

        // Notify Area Manager
        myAreaManager?.RegisterMonsterKill(); // Use null-conditional operator

        // Spawn Death Effects (if any) - Separate from XP Orb logic
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }

        // Disable components (optional, good practice before destroy)
        Collider col = GetComponent<Collider>(); if (col != null) col.enabled = false;
        Rigidbody rb = GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = true;
        // Disable any other relevant components like renderers if needed immediately

        // Destroy the GameObject
        Destroy(gameObject);
    }
    // --- END MODIFIED DieCoroutine ---


    public virtual void Freeze(float duration)
    {
        if (_isFrozen || currentState == EnemyState.Dying) return;

        _isFrozen = true;
        freezeTimer = duration;
        TransitionToState(EnemyState.Frozen);

        Debug.Log($"{gameObject.name} frozen for {duration} seconds.");

        if (freezeEffectPrefab != null && currentFreezeEffectInstance == null)
        {
            currentFreezeEffectInstance = Instantiate(freezeEffectPrefab, transform.position, transform.rotation, transform);
        }

        Rigidbody rb = GetComponent<Rigidbody>(); if(rb != null) rb.isKinematic = true;

        // --- AUDIO: Ensure ambient sound is paused on freeze (handled in Update now) ---
        // if (ambientAudioSource != null && ambientAudioSource.isPlaying) ambientAudioSource.Pause();
        // ------------------------------------------------------------------------------
    }

    protected virtual void Unfreeze()
    {
        if (!_isFrozen) return;

        _isFrozen = false;
        freezeTimer = 0f;
        // State transitions out of Frozen in Update

        Debug.Log($"{gameObject.name} un-frozen.");

        if (currentFreezeEffectInstance != null)
        {
            Destroy(currentFreezeEffectInstance);
            currentFreezeEffectInstance = null;
        }

        Rigidbody rb = GetComponent<Rigidbody>(); if(rb != null) rb.isKinematic = false;

        // --- AUDIO: Ensure ambient sound resumes (handled in Update now) ---
        // if (ambientAudioSource != null && !ambientAudioSource.isPlaying && ambientGruntSound != null) ambientAudioSource.UnPause();
        // ------------------------------------------------------------------
    }

    protected virtual void HandleFreezeTimer()
    {
        if (_isFrozen && freezeTimer > 0)
        {
            freezeTimer -= Time.deltaTime;
            if (freezeTimer <= 0)
            {
                Unfreeze();
            }
        }
    }

    // --- Helper Methods ---
    protected float GetDistanceToPlayer()
    {
        if (playerTransform == null) return float.MaxValue;
        return Vector3.Distance(transform.position, playerTransform.position);
    }

    protected bool CanSeePlayer(float currentDistance, float rangeOverride = -1f)
    {
        if (playerTransform == null) return false;
        float checkRange = (rangeOverride > 0) ? rangeOverride : detectionRange;
        return currentDistance <= checkRange; // Simple distance check
    }

    protected bool ShouldFlee()
    {
        return currentHealth <= fleeHealthThreshold && currentHealth > 0;
    }

    // --- NEW AUDIO HELPER ---
    /// <summary>
    /// Plays the specified audio clip on the given AudioSource if both are valid.
    /// </summary>
    /// <param name="clip">The AudioClip to play.</param>
    /// <param name="source">The AudioSource to play on.</param>
    protected void PlaySound(AudioClip clip, AudioSource source)
    {
        if (source != null && clip != null)
        {
            source.PlayOneShot(clip); // Use PlayOneShot for non-looping effects
        }
        // else
        // {
        //     if(clip != null) Debug.LogWarning($"[{gameObject.name}] Tried to play sound '{clip.name}' but AudioSource is missing or null.", this);
        // }
    }
    // --- END NEW AUDIO HELPER ---
}