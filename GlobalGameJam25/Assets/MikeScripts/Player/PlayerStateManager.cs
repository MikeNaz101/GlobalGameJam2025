using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System; // Needed for Action event

[RequireComponent(typeof(AudioSource))] // Ensure AudioSource is present
public class PlayerStateManager : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private PlayerAnimationManager animationManager; // Assign in Inspector or find in Awake

    [Header("Core Stats")]
    public int maxHealth = 100;
    public int currentHealth;
    public int healthRecoveryRate = 2;
    public int maxMana = 100;
    public int manaCost = 10; // Base cost per shell / For shell count calc
    public int currentMana;
    public int manaRecoveryRate = 5;
    public int maxStamina = 100;
    public int currentStamina;
    public int staminaRecoveryRate = 10;
    public float runStaminaCostPerSecond = 15f; // Stamina cost for running

    [Header("Movement Speeds")]
    public float walkSpeed = 10f;
    public float runSpeed = 20f;
    public float sneakSpeed = 5f;
    public float flySpeed = 15f;
    public float jumpForce = 20f; // Ground jump initial strength
    public float flapStrength = 10f; // Flying flap upward strength
    [HideInInspector] public Vector3 currentHorizontalVelocity = Vector3.zero;
    [HideInInspector] public Vector3 targetHorizontalVelocity = Vector3.zero; // States set this; Update() accelerates toward it

    [Header("Movement Feel")]
    [Tooltip("Move relative to where the camera is looking (standard 3rd-person controls). Turn off to restore the old body-relative movement.")]
    public bool cameraRelativeMovement = true;
    [Tooltip("How quickly the player reaches full speed on the ground (units/sec^2).")]
    public float groundAcceleration = 80f;
    [Tooltip("How quickly the player stops on the ground (units/sec^2).")]
    public float groundDeceleration = 100f;
    [Tooltip("Acceleration while airborne. Lower = less air control.")]
    public float airAcceleration = 30f;
    [Tooltip("How fast the body turns to face the movement or aim direction (degrees/sec).")]
    public float turnSpeed = 900f;
    [Tooltip("Grace period after walking off a ledge during which a jump still works (seconds).")]
    public float coyoteTime = 0.12f;
    [Tooltip("A jump pressed this long before landing still fires on landing (seconds).")]
    public float jumpBufferTime = 0.15f;
    [Tooltip("Extra gravity while falling, for a snappier, less floaty jump arc.")]
    public float fallGravityMultiplier = 1.7f;
    [Tooltip("Terminal falling speed (units/sec).")]
    public float maxFallSpeed = 45f;

    [Header("Physics & Ground Check")]
    public float gravity = -9.81f;
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    [HideInInspector] public bool isGrounded;
    [HideInInspector] public float verticalVelocity = 0f;
    private bool hasJumped = false;
    private bool wasGroundedLastFrame = false;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;
    private float runStaminaAccumulator = 0f;
    private Transform cameraTransform;
    private PlayerShooting playerShooting;

    [Header("Shooting & Abilities")]
    public BulletSpawnerState bulletSpawner; // Assign your BulletSpawnerState component here
    public Transform firePoint;
    public Transform shellOrbitCenter;
    public List<GameObject> shellPrefabs; // Should correspond to BulletType enum order (0=Basic, 1=Freeze, 2=Teleport)
    public float orbitRadius = 2f;
    public float orbitSpeed = 50f;
    [HideInInspector] public int maxShells;
    [HideInInspector] public int currentShells;
    [HideInInspector] public List<GameObject> orbitingShells = new List<GameObject>();

    [Header("Visuals (Optional)")]
    public GameObject rWingPrefab;
    public GameObject lWingPrefab;
    public Transform rWingPoint;
    public Transform lWingPoint;

    // --- Audio ---
    [Header("Audio")]
    public AudioClip damageSound;
    public AudioClip jumpSound;
    public AudioClip walkFootstepSound;
    public AudioClip runFootstepSound;
    public AudioClip shootSound;
    public AudioClip chargeLoopSound; // Looping sound for charging
    // public AudioClip unlockConfirmationSound; // Optional: Add an unlock sound effect reference here
    [HideInInspector] public AudioSource audioSource; // Reference to the AudioSource component

    // --- Components ---
    [HideInInspector] public CharacterController controller;

    // --- Input Flags / State ---
    [HideInInspector] public Vector2 movement;
    [HideInInspector] public bool isSneaking = false;
    [HideInInspector] public bool isRunning = false;
    [HideInInspector] public bool jumpInputPressedThisFrame = false;

    // --- State Machine ---
    [HideInInspector] public PlayerBaseState currentState;
    [HideInInspector] public PlayerIdleState idleState = new PlayerIdleState();
    [HideInInspector] public PlayerWalkingState walkState = new PlayerWalkingState();
    [HideInInspector] public PlayerSneakState sneakState = new PlayerSneakState();
    [HideInInspector] public PlayerRunningState runState = new PlayerRunningState();
    [HideInInspector] public PlayerHitState hitState = new PlayerHitState();
    // [HideInInspector] public PlayerFlyingState flyingState = new PlayerFlyingState();
    // [HideInInspector] public PlayerFallingState fallingState = new PlayerFallingState();

    // ----- XP & Leveling System -----
    [Header("Leveling System")]
    public int currentLevel = 1;
    public int currentXP = 0;
    public int xpToNextLevel = 100;
    public int xpBaseRequirement = 100;
    public float xpRequirementMultiplier = 1.5f;
    public float bulletEffectMultiplier = 1.0f;
    public float effectMultiplierIncreasePerLevel = 0.05f;

    // Events for UI updates
    public static event Action<int, int> OnXPChanged; // Sends current XP, XP to next level
    public static event Action<int> OnLevelChanged; // Sends new level
    public static event Action<float> OnMultiplierChanged; // Sends new multiplier
    // public static event Action<int> OnBulletUnlock; // Optional: Event for UI showing unlocked bullets

    // ----- Ability Unlocks -----
    [Header("Ability Unlocks")] // NEW HEADER
    [SerializeField] // Allows viewing/testing in Inspector, but still private logic-wise
    [Tooltip("The highest index (BulletType enum value) the player has unlocked. 0 = Basic only.")]
    private int maxUnlockedBulletIndex = 0; // Start with only index 0 (Basic) unlocked
    // Optional: Public getter if other systems need to know the unlock level
    public int MaxUnlockedBulletIndex => maxUnlockedBulletIndex;


    void Awake()
    {
        controller = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>(); // Get the AudioSource

        if (controller == null) Debug.LogError("CharacterController not found on Player!");
        if (audioSource == null) Debug.LogError("AudioSource not found on Player!");
        if (bulletSpawner == null) Debug.LogError("BulletSpawnerState not assigned in the Inspector!");
        if (groundCheck == null) Debug.LogError("GroundCheck Transform not assigned in the Inspector!");
        if (shellOrbitCenter == null) Debug.LogError("ShellOrbitCenter Transform not assigned in the Inspector!");
        if (shellPrefabs == null || shellPrefabs.Count == 0) Debug.LogWarning("Shell Prefabs list is empty or null in PlayerStateManager!");
        playerShooting = GetComponent<PlayerShooting>();
        if (playerShooting == null) playerShooting = GetComponentInChildren<PlayerShooting>();

        // Subscribe UpdateShellVisuals to the BulletSpawnerState's OnBulletTypeChanged event
        if (bulletSpawner != null)
        {
            bulletSpawner.OnBulletTypeChanged.AddListener(UpdateShellVisuals);
        }
        else
        {
            Debug.LogError("Cannot subscribe UpdateShellVisuals to BulletSpawnerState event: BulletSpawner is null!");
        }
    }

    void Start()
    {
        currentHealth = maxHealth;
        currentMana = maxMana;
        currentStamina = maxStamina;

        maxShells = (manaCost > 0) ? maxMana / manaCost : 0;
        currentShells = maxShells; // Start with full shells equivalent

        // Initial setup of shell visuals based on the starting bullet type (Type1/Index 0)
        UpdateShellVisuals(); // Call this AFTER setting initial mana/shells

        StartCoroutine(RecoverHealthOverTime());
        StartCoroutine(RecoverManaOverTime());
        StartCoroutine(RecoverStaminaOverTime()); // Stamina recovery coroutine

        CalculateXPForNextLevel();
        // Invoke initial UI updates via events
        OnXPChanged?.Invoke(currentXP, xpToNextLevel);
        OnLevelChanged?.Invoke(currentLevel);
        OnMultiplierChanged?.Invoke(bulletEffectMultiplier);
        // If using health/mana/stamina events, invoke them here too

        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
        wasGroundedLastFrame = isGrounded;

        SwitchState(idleState);
    }

    // Clean up listener on destroy
    void OnDestroy()
    {
        if (bulletSpawner != null)
        {
            bulletSpawner.OnBulletTypeChanged.RemoveListener(UpdateShellVisuals);
        }
    }

    // --- Input System Handlers ---
    void OnMove(InputValue value) { movement = value.Get<Vector2>(); }
    void OnSprint(InputValue value) { if (value.isPressed) isSneaking = !isSneaking; }
    void OnRun(InputValue value) { isRunning = value.isPressed; }

    void OnFire(InputValue value)
    {
        if (PauseMenuController.GameIsPaused) return; // Assuming PauseMenuController exists
        PlayerShooting shooter = playerShooting;
        if (shooter == null) return;
        if (value.isPressed) shooter.StartCharge();
        else shooter.EndCharge();
    }

    void OnSecondaryFire(InputValue value) { /* Implement if needed */ }

    // --- MODIFIED Input Handler for Weapon Change ---
    void OnChangeWeaponVector2(InputValue scrollVal)
    {
        // Check if switching is allowed at all based on unlocks
        if (maxUnlockedBulletIndex <= 0)
        {
            // Debug.Log("Bullet switching locked (only basic unlocked)."); // Optional debug
            return; // Exit if only the first bullet (index 0) is unlocked
        }

        Vector2 scrollValue = scrollVal.Get<Vector2>();
        float scrollY = scrollValue.y;

        if (bulletSpawner == null)
        {
            Debug.LogError("Bullet Spawner reference missing in PlayerStateManager!");
            return;
        }

        // Only process if there was actual scroll input
        if (scrollY != 0)
        {
            int changeDirection = scrollY > 0 ? 1 : -1; // Determine scroll direction

            // Call the modified ChangeBulletType method on the BulletSpawnerState,
            // passing the direction AND the maximum allowed index based on unlocks.
            bulletSpawner.ChangeBulletType(changeDirection, maxUnlockedBulletIndex);

            // Note: UpdateShellVisuals() is now called automatically via the event listener set up in Awake()
            // UpdateShellVisuals(); // No longer needed here if event listener is working

            // Debug.Log($"Attempted change bullet type via Vector2. Direction: {changeDirection}, Max Index Allowed: {maxUnlockedBulletIndex}");
        }
    }
    // --- END MODIFIED Input Handler ---

    void Update()
    {
        HandleGroundCheck();
        if (isGrounded) lastGroundedTime = Time.time;
        ApplyGravity();
        HandleMovement(); // This calls currentState.UpdateState(), which sets targetHorizontalVelocity
        TryConsumeBufferedJump();
        UpdateHorizontalVelocity();
        UpdateFacing();

        // Apply final calculated movement - the ONLY place the CharacterController is moved each frame
        Vector3 finalVelocity = currentHorizontalVelocity + (Vector3.up * verticalVelocity);
        controller.Move(finalVelocity * Time.deltaTime);

        UpdateShellPositions(); // Keep rotating existing shells
        wasGroundedLastFrame = isGrounded;
    }

    // --- Player Movement ---

    /// <summary>
    /// World-space direction the player wants to move in, from the Move input.
    /// Camera-relative when cameraRelativeMovement is on, body-relative otherwise.
    /// Magnitude is 0..1 so analog sticks can move slowly.
    /// </summary>
    public Vector3 GetDesiredMoveDirection()
    {
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        if (cameraRelativeMovement)
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(cameraTransform.up, Vector3.up);
                forward.Normalize();
                right = Vector3.Cross(Vector3.up, forward);
            }
        }

        Vector3 direction = (forward * movement.y) + (right * movement.x);
        direction.y = 0f;
        return Vector3.ClampMagnitude(direction, 1f);
    }

    /// <summary>Called by movement states: move in the input direction at the given speed.</summary>
    public void SetMoveSpeed(float speed)
    {
        targetHorizontalVelocity = GetDesiredMoveDirection() * speed;
    }

    /// <summary>Stop horizontal movement (with deceleration).</summary>
    public void StopMoving()
    {
        targetHorizontalVelocity = Vector3.zero;
    }

    // Kept for older states (e.g. flying). Sets the target velocity; Update() does the actual move.
    // (Previously this moved the controller directly, which double-applied gravity and stacked with
    // the stale walk velocity, making run/sneak faster than intended.)
    public void MovePlayer(Vector3 horizontalDirection, float speed)
    {
        horizontalDirection.y = 0;
        targetHorizontalVelocity = Vector3.ClampMagnitude(horizontalDirection, 1f) * speed;
    }

    /// <summary>Drains stamina while running. Returns false when the player is out of stamina.</summary>
    public bool DrainRunStamina()
    {
        runStaminaAccumulator += runStaminaCostPerSecond * Time.deltaTime;
        if (runStaminaAccumulator >= 1f)
        {
            int whole = Mathf.FloorToInt(runStaminaAccumulator);
            runStaminaAccumulator -= whole;
            UseStamina(whole);
        }
        return currentStamina > 0;
    }

    void UpdateHorizontalVelocity()
    {
        float rate;
        if (!isGrounded) rate = airAcceleration;
        else if (targetHorizontalVelocity.sqrMagnitude > currentHorizontalVelocity.sqrMagnitude) rate = groundAcceleration;
        else rate = groundDeceleration;

        currentHorizontalVelocity = Vector3.MoveTowards(currentHorizontalVelocity, targetHorizontalVelocity, rate * Time.deltaTime);
    }

    void UpdateFacing()
    {
        if (!cameraRelativeMovement) return; // Legacy mode: PlayerBodyRotator drives the body
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        Vector3 faceDirection = Vector3.zero;
        bool isAiming = playerShooting != null && playerShooting.IsCharging;

        if (isAiming && cameraTransform != null)
        {
            faceDirection = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up); // Face where we're aiming
        }
        else if (movement.sqrMagnitude > 0.01f && targetHorizontalVelocity.sqrMagnitude > 0.0001f)
        {
            faceDirection = targetHorizontalVelocity; // Face where we're going
        }

        if (faceDirection.sqrMagnitude < 0.0001f) return;
        Quaternion targetRotation = Quaternion.LookRotation(faceDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    void HandleGroundCheck()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore)
                     || (controller != null && controller.isGrounded);
    }

    void ApplyGravity()
    {
        bool justLanded = !wasGroundedLastFrame && isGrounded;

        if (isGrounded && verticalVelocity <= 0f)
        {
            // Heavy landings get a small camera bump
            if (justLanded && verticalVelocity < -15f)
                Ishana.Combat.CombatFeedback.Shake(Mathf.InverseLerp(-15f, -maxFallSpeed, verticalVelocity) * 0.4f + 0.1f);

            // Small constant push keeps the controller glued to slopes instead of skipping down them
            verticalVelocity = -2f;
            hasJumped = false;
        }
        else // Airborne (or moving upward from a jump)
        {
            float multiplier = verticalVelocity < 0f ? fallGravityMultiplier : 1f;
            verticalVelocity += gravity * multiplier * Time.deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, -maxFallSpeed);
        }
    }

    void HandleMovement()
    {
        currentState?.UpdateState(this);
    }

    // --- Jump Logic ---
    void OnJump()
    {
        if (PauseMenuController.GameIsPaused) return;
        // Buffer the press; TryConsumeBufferedJump() fires it as soon as it's allowed (coyote time + jump buffering)
        lastJumpPressedTime = Time.time;
    }

    void TryConsumeBufferedJump()
    {
        bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
        bool canJump = Time.time - lastGroundedTime <= coyoteTime && !hasJumped && currentState != hitState;
        if (!jumpBuffered || !canJump) return;

        lastJumpPressedTime = float.NegativeInfinity;
        lastGroundedTime = float.NegativeInfinity; // Prevent a second coyote jump
        hasJumped = true;
        Jump(); // Calculate vertical velocity
        animationManager?.TriggerJump();
        PlaySoundOneShot(jumpSound);
    }

    public void Jump()
    {
        verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
    }

    // --- Damage & Death ---
    public void TakeDamage(int damage)
    {
        if (currentHealth > 0 && currentState != hitState)
        {
            PlaySoundOneShot(damageSound);
            // Debug.Log($"Attempting to call TriggerHit. animationManager is null? {animationManager == null}");
            animationManager?.TriggerHit();
            hitState.SetDamage(damage); // Pass damage amount to HitState if needed
            StopMoving();
            SwitchState(hitState);
            Ishana.Combat.CombatFeedback.ReportPlayerHit(this, damage);

            currentHealth -= damage;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            // If using events for health bar: OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0) { Die(); }
        }
    }

    public void Die()
    {
        Debug.Log("Player Died! Loading End Scene...");
        // animationManager?.TriggerDeath();
        this.enabled = false;
        if(controller) controller.enabled = false;
        // Consider a delay before loading the scene
        SceneManager.LoadScene("EndScene"); // Ensure "EndScene" is in Build Settings
    }

    // --- Mana & Shells ---
    public bool UseMana(int mCost)
    {
        if (currentMana >= mCost)
        {
            currentMana -= mCost;
            currentMana = Mathf.Clamp(currentMana, 0, maxMana);
            UpdateShellCountVisuals(); // Update orbiting shells based on new mana
            // If using events for mana bar: OnManaChanged?.Invoke(currentMana, maxMana);
            return true;
        }
        else
        {
            return false; // Not enough mana
        }
    }

    // --- Stamina ---
    public bool UseStamina(float amount)
    {
        if (currentStamina > 0)
        {
            currentStamina -= (int)amount;
            currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
            // If using events for stamina bar: OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            return true;
        }
        return false;
    }


    // --- Recovery Coroutines ---
    private IEnumerator RecoverHealthOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);
            if (currentHealth < maxHealth && currentHealth > 0)
            {
                currentHealth += healthRecoveryRate;
                currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
                // If using events for health bar: OnHealthChanged?.Invoke(currentHealth, maxHealth);
            }
        }
    }
    private IEnumerator RecoverManaOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            if (currentMana < maxMana)
            {
                currentMana += manaRecoveryRate;
                currentMana = Mathf.Clamp(currentMana, 0, maxMana);
                UpdateShellCountVisuals(); // Update shells as mana recovers
                // If using events for mana bar: OnManaChanged?.Invoke(currentMana, maxMana);
            }
        }
    }
    private IEnumerator RecoverStaminaOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            // Recover ONLY if not running AND stamina is below max
            if (currentState != runState && currentStamina < maxStamina)
            {
                currentStamina += staminaRecoveryRate;
                currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
                // If using events for stamina bar: OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            }
        }
    }

    // --- Audio Helper Methods ---
    public void PlaySoundOneShot(AudioClip clip, float volumeScale = 1.0f)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, volumeScale);
        }
    }

    public void PlayLoopingSound(AudioClip clip, bool loop = true)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.clip = clip;
            audioSource.loop = loop;
            audioSource.Play();
        }
    }

    public void StopLoopingSound()
    {
        if (audioSource != null && audioSource.isPlaying && audioSource.loop) // Check isPlaying too
        {
             audioSource.Stop();
             audioSource.clip = null;
             audioSource.loop = false;
        }
    }


    // --- State Switching ---
    public void SwitchState(PlayerBaseState newState)
    {
        if (newState == null || newState == currentState) return;

        currentState?.ExitState(this);
        // Debug.Log($"Switching from {currentState?.GetType().Name ?? "None"} to {newState.GetType().Name}");
        currentState = newState;
        currentState.EnterState(this);
    }


    // --- Shell Visuals ---

    /// <summary>
    /// Updates the number of VISIBLE shells based on current mana and mana cost.
    /// Does not respawn or change shell type.
    /// </summary>
    public void UpdateShellCountVisuals()
    {
        if (manaCost <= 0 || orbitingShells == null) return;

        int targetShells = currentMana / manaCost;
        targetShells = Mathf.Clamp(targetShells, 0, maxShells); // Ensure it doesn't exceed max capacity

        for (int i = 0; i < orbitingShells.Count; i++)
        {
            if (orbitingShells[i] != null)
            {
                // Enable/disable the Renderer component for visibility
                Renderer rend = orbitingShells[i].GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.enabled = (i < targetShells);
                }
                else // Fallback for complex prefabs? Check children?
                {
                    // Could try SetActive, but enabling/disabling Renderer is usually preferred
                    // orbitingShells[i].SetActive(i < targetShells);
                }
            }
        }
        // Update the logical count
        currentShells = targetShells;
    }

    /// <summary>
    /// Destroys old shells and spawns new ones based on the CURRENTLY selected bullet type
    /// in the BulletSpawnerState. Usually called when the bullet type changes.
    /// </summary>
    void SpawnSpiritBubbleShells()
    {
        if (bulletSpawner == null || shellPrefabs == null || shellPrefabs.Count == 0 || manaCost <= 0 || shellOrbitCenter == null)
        {
            Debug.LogError("Cannot spawn shells: Dependencies missing or invalid configuration.", this);
            return;
        }

        // Get the index corresponding to the current bullet type enum
        int currentTypeIndex = (int)bulletSpawner.CurrentBulletType;

        // Validate the index against the shellPrefabs list
        if (currentTypeIndex < 0 || currentTypeIndex >= shellPrefabs.Count || shellPrefabs[currentTypeIndex] == null)
        {
             Debug.LogError($"Invalid shell prefab index {currentTypeIndex} derived from BulletType {bulletSpawner.CurrentBulletType}. Check shellPrefabs list order/assignment in PlayerStateManager Inspector. Make sure it matches the BulletType enum.", this);
             // Fallback to index 0 if possible
             if (shellPrefabs.Count > 0 && shellPrefabs[0] != null) {
                 currentTypeIndex = 0;
                 Debug.LogWarning("Falling back to shell prefab index 0.");
             } else {
                 return; // Cannot proceed if even index 0 is invalid
             }
        }

        // Get the correct prefab based on the validated index
        GameObject shellPrefabToSpawn = shellPrefabs[currentTypeIndex];

        // --- Cleanup Old Shells ---
        // Use a temporary list to avoid modifying the list while iterating if needed,
        // but destroying directly should be okay here.
        foreach (GameObject oldShell in orbitingShells)
        {
            if (oldShell != null)
            {
                Destroy(oldShell);
            }
        }
        orbitingShells.Clear(); // Clear the list

        // --- Calculate Shell Count ---
        // Recalculate max based on mana, in case manaCost changes dynamically (though unlikely)
        maxShells = (manaCost > 0) ? maxMana / manaCost : 0;
        // Determine current shells based on *current* mana
        currentShells = Mathf.Clamp(currentMana / manaCost, 0, maxShells);

        // --- Spawn New Shells ---
        Vector3 centerPoint = shellOrbitCenter.position; // Use the center's current position

        for (int i = 0; i < maxShells; i++) // Spawn the maximum possible number
        {
            // Calculate position around the orbit center
             float angle = i * (360f / Mathf.Max(1, maxShells)); // Avoid division by zero if maxShells is 0
             // Use shellOrbitCenter's local forward/right if you want orbit relative to player facing
             // Or use global axes like below for world-aligned orbit
             float x = orbitRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
             float z = orbitRadius * Mathf.Sin(angle * Mathf.Deg2Rad);
             // Position relative to the center point's current world position
             Vector3 spawnPosition = centerPoint + new Vector3(x, 0, z); // Assuming Y offset is handled by prefab/orbitCenter

            // Instantiate the new shell and parent it to the orbit center for rotation
             GameObject shell = Instantiate(shellPrefabToSpawn, spawnPosition, Quaternion.identity, shellOrbitCenter);
             orbitingShells.Add(shell); // Add to our tracking list

            // --- Set Initial Visibility ---
            // Enable/disable the renderer based on whether it's within the currentShells count
            Renderer shellRenderer = shell.GetComponent<Renderer>();
            if (shellRenderer != null)
            {
                shellRenderer.enabled = (i < currentShells);
            }
             else
            {
                 // If no renderer on root, maybe try enabling/disabling the GameObject itself?
                 // shell.SetActive(i < currentShells);
                 Debug.LogWarning($"Shell prefab '{shellPrefabToSpawn.name}' or its instance lacks a Renderer component on the root. Visibility control might not work as expected.", shell);
            }
        }
        Debug.Log($"Spawned {orbitingShells.Count} shells of type {bulletSpawner.CurrentBulletType}. {currentShells} should be visible.");
    }

    /// <summary>
    /// Public method to trigger the respawning of shell visuals.
    /// Called when bullet type changes (via event listener).
    /// </summary>
    public void UpdateShellVisuals()
    {
        // Debug.Log("UpdateShellVisuals called - respawning shells for current type.");
        SpawnSpiritBubbleShells();
    }

    void UpdateShellPositions()
    {
        // Rotate the parent object (shellOrbitCenter) to make the children orbit
        if (shellOrbitCenter != null && orbitingShells.Count > 0)
        {
            shellOrbitCenter.Rotate(Vector3.up, orbitSpeed * Time.deltaTime, Space.World); // Rotate around world Y
        }
    }


    // --- Gizmos ---
    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        if (shellOrbitCenter != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(shellOrbitCenter.position, orbitRadius);
        }
    }

    // ----- XP & Leveling Methods -----
    private void CalculateXPForNextLevel()
    {
        if (currentLevel <= 0) currentLevel = 1;
        xpToNextLevel = Mathf.FloorToInt(xpBaseRequirement * Mathf.Pow(xpRequirementMultiplier, currentLevel - 1));
        xpToNextLevel = Mathf.Max(1, xpToNextLevel); // Ensure it's at least 1
    }

    public void GainXP(int amount)
    {
        if (amount <= 0) return;
        currentXP += amount;
        // Debug.Log($"Gained {amount} XP. Current XP: {currentXP}/{xpToNextLevel}");
        OnXPChanged?.Invoke(currentXP, xpToNextLevel); // Event for UI

        while (currentXP >= xpToNextLevel && xpToNextLevel > 0) // Add check for xpToNextLevel > 0 to prevent infinite loop if calculation is wrong
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        int excessXP = currentXP - xpToNextLevel; // Calculate excess BEFORE incrementing level
        currentLevel++;
        currentXP = excessXP; // Assign excess XP

        bulletEffectMultiplier += effectMultiplierIncreasePerLevel;
        CalculateXPForNextLevel(); // Calculate for the *new* next level

        Debug.Log($"<color=lime>LEVEL UP! Reached Level {currentLevel}. Multiplier: {bulletEffectMultiplier:F2}. Next Level at {xpToNextLevel} XP.</color>");
        animationManager?.TriggerCelebrate();

        // Invoke events for UI update
        OnLevelChanged?.Invoke(currentLevel);
        OnMultiplierChanged?.Invoke(bulletEffectMultiplier);
        OnXPChanged?.Invoke(currentXP, xpToNextLevel); // Update XP bar with new target and current XP

        // Refill stats on level up
        currentHealth = maxHealth;
        currentMana = maxMana;
        currentStamina = maxStamina; // Refill stamina too
        UpdateShellCountVisuals(); // Update shell visibility based on refilled mana
        // If using events:
        // OnHealthChanged?.Invoke(currentHealth, maxHealth);
        // OnManaChanged?.Invoke(currentMana, maxMana);
        // OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    // ----- Save / Load Support -----

    /// <summary>Restores progression from a save file. Call after Start() has run.</summary>
    public void ApplySavedProgress(int level, int xp, float effectMultiplier, int unlockedBulletIndex)
    {
        currentLevel = Mathf.Max(1, level);
        bulletEffectMultiplier = Mathf.Max(1f, effectMultiplier);
        CalculateXPForNextLevel();
        currentXP = Mathf.Clamp(xp, 0, Mathf.Max(0, xpToNextLevel - 1));

        int totalBulletTypes = bulletSpawner != null ? bulletSpawner.GetTotalBulletTypes() : 0;
        int highestIndex = totalBulletTypes > 0 ? totalBulletTypes - 1 : unlockedBulletIndex;
        maxUnlockedBulletIndex = Mathf.Clamp(unlockedBulletIndex, 0, highestIndex);

        OnXPChanged?.Invoke(currentXP, xpToNextLevel);
        OnLevelChanged?.Invoke(currentLevel);
        OnMultiplierChanged?.Invoke(bulletEffectMultiplier);
    }

    /// <summary>Moves the player instantly (respawn / load). A CharacterController must be disabled to be moved.</summary>
    public void TeleportTo(Vector3 position, float yawDegrees)
    {
        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
        if (controller != null) controller.enabled = wasEnabled;

        currentHorizontalVelocity = Vector3.zero;
        targetHorizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
    }

    /// <summary>Refills health, mana and stamina (e.g. as a reward for cleansing a grove).</summary>
    public void RestoreAllStats()
    {
        if (currentHealth <= 0) return; // Don't revive a dead player
        currentHealth = maxHealth;
        currentMana = maxMana;
        currentStamina = maxStamina;
        UpdateShellCountVisuals();
    }

    // --- NEW Method for Teleport Bullet to Call ---
    public void TriggerTeleportAnimation()
    {
        // Debug.Log($"Attempting to call TriggerTeleported. animationManager is null? {animationManager == null}");
        animationManager?.TriggerTeleported();
    }

    // --- Optional: Debug Methods ---
    [ContextMenu("Add 50 XP")]
    void DebugAddXP() { GainXP(50); }

    [ContextMenu("Level Up Manually")]
    void DebugLevelUp() { if(xpToNextLevel > 0) GainXP(xpToNextLevel - currentXP); }

    // ----- NEW Unlock Method -----
    /// <summary>
    /// Unlocks bullet types up to the specified index. Called by unlock triggers.
    /// </summary>
    /// <param name="indexToUnlock">The index (BulletType enum value) of the bullet type to unlock.</param>
    /// <returns>True if a new unlock level was reached, false otherwise.</returns>
    public bool UnlockBulletType(int indexToUnlock)
    {
        // Basic validation: index must be positive (index 0 is default)
        if (indexToUnlock <= 0) {
            Debug.LogWarning($"Tried to unlock invalid or default index: {indexToUnlock}. Only indices > 0 can be unlocked via trigger.");
            return false;
        }

        // Optional: Validate against the actual number of bullet types defined
        int totalBulletTypes = bulletSpawner?.GetTotalBulletTypes() ?? 0; // Requires GetTotalBulletTypes() in BulletSpawnerState
        if (totalBulletTypes > 0 && indexToUnlock >= totalBulletTypes) {
             Debug.LogWarning($"Tried to unlock index {indexToUnlock}, but there are only {totalBulletTypes} bullet types defined (max index {totalBulletTypes - 1}).");
             return false;
        }

        // Check if this unlock is actually higher than the current level
        if (indexToUnlock > maxUnlockedBulletIndex)
        {
            maxUnlockedBulletIndex = indexToUnlock;
            Debug.Log($"<color=lime>ABILITY UNLOCKED! Player can now use bullet types up to index {maxUnlockedBulletIndex} (Type: {(BulletType)maxUnlockedBulletIndex}).</color>");

            // Optional: Play unlock sound
            // PlaySoundOneShot(unlockConfirmationSound);

            // Optional: Invoke event for UI or other systems
            // OnBulletUnlock?.Invoke(maxUnlockedBulletIndex);

            return true; // Report that an unlock happened
        }
        else
        {
             Debug.Log($"Player already has bullet index {indexToUnlock} unlocked (current max: {maxUnlockedBulletIndex}). No change.");
            return false; // Report that no new unlock occurred
        }
    }
    // --- END NEW Unlock Method ---


    // --- Optional: UI Initialization Helper ---
    /*
    void SetupInitialUI()
    {
        // Example for StaminaBar if used independently
        var staminaBar = FindObjectOfType<StaminaBar>();
        if (staminaBar != null)
        {
             staminaBar.SetMaxStats(maxStamina);
             staminaBar.SetStamina(currentStamina);
        }
        // Add similar setup for Health/Mana if needed
    }
    */

} // End of PlayerStateManager class