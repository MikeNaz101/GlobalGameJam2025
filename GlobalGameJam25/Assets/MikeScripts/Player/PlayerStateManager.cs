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

    [Header("Physics & Ground Check")]
    public float gravity = -9.81f;
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    [HideInInspector] public bool isGrounded;
    [HideInInspector] public float verticalVelocity = 0f;
    private bool hasJumped = false;
    private bool wasGroundedLastFrame = false;

    [Header("Shooting & Abilities")]
    public BulletSpawnerState bulletSpawner;
    public Transform firePoint;
    public Transform shellOrbitCenter;
    public List<GameObject> shellPrefabs;
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

    // Events for UI updates (XP bar should use these via ProgressBarManager or directly)
    public static event Action<int, int> OnXPChanged; // Sends current XP, XP to next level
    public static event Action<int> OnLevelChanged; // Sends new level
    public static event Action<float> OnMultiplierChanged; // Sends new multiplier
    // NOTE: If using Event-Based updates for Health/Mana/Stamina, declare static events here too

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>(); // Get the AudioSource

        if (controller == null) Debug.LogError("CharacterController not found on Player!");
        if (audioSource == null) Debug.LogError("AudioSource not found on Player!");
        if (bulletSpawner == null) Debug.LogError("BulletSpawnerState not assigned in the Inspector!");
        if (groundCheck == null) Debug.LogError("GroundCheck Transform not assigned in the Inspector!");
        if (shellOrbitCenter == null) Debug.LogError("ShellOrbitCenter Transform not assigned in the Inspector!");
    }

    void Start()
    {
        currentHealth = maxHealth;
        currentMana = maxMana;
        currentStamina = maxStamina;

        maxShells = (manaCost > 0) ? maxMana / manaCost : 0;
        currentShells = maxShells;

        UpdateShellVisuals();

        StartCoroutine(RecoverHealthOverTime());
        StartCoroutine(RecoverManaOverTime());
        StartCoroutine(RecoverStaminaOverTime()); // Stamina recovery coroutine

        CalculateXPForNextLevel();
        // Invoke initial UI updates via events for systems listening (like ProgressBarManager if using events)
        OnXPChanged?.Invoke(currentXP, xpToNextLevel);
        OnLevelChanged?.Invoke(currentLevel);
        OnMultiplierChanged?.Invoke(bulletEffectMultiplier);
        // Note: ProgressBarManager in Update mode will pick up initial values anyway.
        // If you have a separate StaminaBar script WITHOUT ProgressBarManager, call its init here.
        // SetupInitialUI(); // Call this if you need to initialize bars outside ProgressBarManager


        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
        wasGroundedLastFrame = isGrounded;

        SwitchState(idleState);
    }

    // --- Input System Handlers ---
    void OnMove(InputValue value) { movement = value.Get<Vector2>(); }
    void OnSprint(InputValue value) { if (value.isPressed) isSneaking = !isSneaking; }
    void OnRun(InputValue value) { isRunning = value.isPressed; }

    void OnFire(InputValue value)
    {
        if (PauseMenuController.GameIsPaused) return; // Assuming PauseMenuController exists
        PlayerShooting shooter = GetComponent<PlayerShooting>();
        if (shooter == null) return;
        if (value.isPressed) shooter.StartCharge();
        else shooter.EndCharge();
    }

    void OnSecondaryFire(InputValue value) { /* Implement if needed */ }
    void OnChangeWeaponVector2(InputValue scrollVal)
    {
        Vector2 scrollValue = scrollVal.Get<Vector2>();
        float scrollY = scrollValue.y;

        if (bulletSpawner == null) return;

        if (scrollY != 0)
        {
            int changeDirection = scrollY > 0 ? 1 : -1;
            bulletSpawner.ChangeBulletType(changeDirection);
            UpdateShellVisuals();
            Debug.Log("Changed bullet type via Vector2. Direction: " + changeDirection);
        }
    }

    void Update()
    {
        HandleGroundCheck();
        ApplyGravity();
        HandleMovement(); // This calls currentState.UpdateState()

        // Apply final calculated movement (horizontal from state + vertical from gravity/jump)
        Vector3 finalVelocity = currentHorizontalVelocity + (Vector3.up * verticalVelocity);
        controller.Move(finalVelocity * Time.deltaTime);

        UpdateShellPositions();
        wasGroundedLastFrame = isGrounded;
    }

    // --- Player Movement ---
    // Note: This method is primarily called BY states, not directly used for movement logic here.
    // States calculate direction/speed and set currentHorizontalVelocity instead.
    // Keep it if states need a direct Move call for specific scenarios.
    public void MovePlayer(Vector3 horizontalDirection, float speed)
    {
        horizontalDirection.y = 0;
        Vector3 finalMovement = (horizontalDirection.normalized * speed) + (Vector3.up * verticalVelocity);
        controller.Move(finalMovement * Time.deltaTime);
    }


    void HandleGroundCheck()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
    }

    void ApplyGravity()
    {
        bool justLanded = !wasGroundedLastFrame && isGrounded;

        if (justLanded)
        {
            verticalVelocity = -0.5f; // Stick slightly on landing
            hasJumped = false;
        }
        else if (isGrounded)
        {
            // Prevent accumulating negative velocity while grounded
            if (verticalVelocity < 0f) { verticalVelocity = 0f; }
            hasJumped = false;
        }
        else // Airborne
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    void HandleMovement()
    {
        // Delegate state logic (which includes setting currentHorizontalVelocity)
        currentState?.UpdateState(this);
    }

    // --- Jump Logic ---
    void OnJump()
    {
        Debug.Log("Jump Input Received");
        if (isGrounded) // Use our reliable ground check flag
        {
            Debug.Log("Performing Ground Jump");
            hasJumped = true;
            Jump(); // Calculate vertical velocity
            animationManager?.TriggerJump();

            // --- Play Jump Sound ---
            PlaySoundOneShot(jumpSound); // Play the jump grunt
            // ----------------------
        }
        else
        {
            Debug.Log("Jump Input while airborne (no action defined)");
        }
    }

    public void Jump()
    {
        // Calculate the upward velocity needed to reach the desired jump height
        verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
    }

    // --- Damage & Death ---
    public void TakeDamage(int damage)
    {
        if (currentHealth > 0 && currentState != hitState)
        {
            // --- Play Damage Sound ---
            PlaySoundOneShot(damageSound); // Play the damage grunt
            // -------------------------
            Debug.Log($"Attempting to call TriggerHit. animationManager is null? {animationManager == null}");
            animationManager?.TriggerHit();
            hitState.SetDamage(damage); // Pass damage amount to HitState if needed
            SwitchState(hitState);
            // Update Health value (HitState might reduce it, or do it here)
            currentHealth -= damage;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            // If using events for health bar: OnHealthChanged?.Invoke(currentHealth, maxHealth);
            if (currentHealth <= 0) { Die(); } // Check for death immediately after taking damage
        }
        // Removed else if (currentHealth <= 0) as it's handled above now.
    }


    public void Die()
    {
        Debug.Log("Player Died! Loading End Scene...");
        // Add death animation/effects trigger here if desired
        // animationManager?.TriggerDeath();
        // Stop player input/movement if needed
        this.enabled = false; // Disable this script
        controller.enabled = false; // Disable character controller
        // Load end scene after a short delay?
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
        if (currentStamina > 0) // Check if there's *any* stamina left
        {
            currentStamina -= (int)amount; // Deduct stamina
            currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina); // Ensure it doesn't go below 0
            // REMOVED: FindObjectOfType<StaminaBar>()?.SetStamina(currentStamina);
            // ProgressBarManager will handle the UI update.
            // If using events for stamina bar: OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            return true; // Indicate stamina was used (even if it hit 0 this frame)
        }
        return false; // Indicate no stamina was available to use
    }


    // --- Recovery Coroutines ---
    private IEnumerator RecoverHealthOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f); // How often to recover
            if (currentHealth < maxHealth && currentHealth > 0) // Don't recover if dead or full
            {
                currentHealth += healthRecoveryRate;
                currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
                // If using events for health bar: OnHealthChanged?.Invoke(currentHealth, maxHealth);
                // ProgressBarManager will handle the UI update.
            }
        }
    }
    private IEnumerator RecoverManaOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f); // How often to recover
            if (currentMana < maxMana)
            {
                currentMana += manaRecoveryRate;
                currentMana = Mathf.Clamp(currentMana, 0, maxMana);
                UpdateShellCountVisuals(); // Update shells as mana recovers
                // If using events for mana bar: OnManaChanged?.Invoke(currentMana, maxMana);
                // ProgressBarManager will handle the UI update.
            }
        }
    }
    private IEnumerator RecoverStaminaOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f); // Check every second
            // Recover ONLY if not running AND stamina is below max
            if (currentState != runState && currentStamina < maxStamina)
            {
                currentStamina += staminaRecoveryRate;
                currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
                 // REMOVED: FindObjectOfType<StaminaBar>()?.SetStamina(currentStamina);
                 // ProgressBarManager will handle the UI update.
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
        if (audioSource != null && audioSource.loop) // Only stop if it was looping
        {
             audioSource.Stop();
             audioSource.clip = null; // Clear the clip to prevent accidental replay
             audioSource.loop = false;
        }
    }


    // --- State Switching ---
    public void SwitchState(PlayerBaseState newState)
    {
        if (newState == null || newState == currentState) return;

        currentState?.ExitState(this);
        // Debug.Log($"Switching from {currentState?.GetType().Name ?? "None"} to {newState.GetType().Name}"); // Optional log
        currentState = newState;
        currentState.EnterState(this);
    }


    // --- Shell Visuals --- (Keep these methods as they are)
    public void UpdateShellCountVisuals()
    {
        if (manaCost <= 0) return;

        int targetShells = currentMana / manaCost;
        targetShells = Mathf.Clamp(targetShells, 0, maxShells);

        for (int i = 0; i < orbitingShells.Count; i++)
        {
            if (orbitingShells[i] != null)
            {
                Renderer rend = orbitingShells[i].GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.enabled = (i < targetShells);
                }
            }
        }
        currentShells = targetShells;
    }

    void SpawnSpiritBubbleShells()
    {
        if (bulletSpawner == null || shellPrefabs == null || shellPrefabs.Count == 0 || manaCost <= 0 || shellOrbitCenter == null)
        {
            Debug.LogError("Cannot spawn shells: Dependencies missing.");
            return;
        }
        int currentTypeIndex = (int)bulletSpawner.CurrentBulletType;
        if (currentTypeIndex < 0 || currentTypeIndex >= shellPrefabs.Count || shellPrefabs[currentTypeIndex] == null)
        {
             Debug.LogError($"Invalid shell prefab index {currentTypeIndex}. Check shellPrefabs list.");
             currentTypeIndex = 0;
             if (shellPrefabs.Count == 0 || shellPrefabs[0] == null) return;
        }
        GameObject shellPrefabToSpawn = shellPrefabs[currentTypeIndex];

        foreach (GameObject oldShell in orbitingShells) { if (oldShell != null) Destroy(oldShell); }
        orbitingShells.Clear();

        maxShells = maxMana / manaCost;
        currentShells = Mathf.Clamp(currentMana / manaCost, 0, maxShells);
        Vector3 centerPoint = shellOrbitCenter.position;

        for (int i = 0; i < maxShells; i++)
        {
             float angle = i * (360f / Mathf.Max(1, maxShells));
             float x = centerPoint.x + orbitRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
             float z = centerPoint.z + orbitRadius * Mathf.Sin(angle * Mathf.Deg2Rad);
             Vector3 spawnPosition = new Vector3(x, centerPoint.y, z);

             GameObject shell = Instantiate(shellPrefabToSpawn, spawnPosition, Quaternion.identity, shellOrbitCenter); // Parent to center
             orbitingShells.Add(shell);

             Renderer shellRenderer = shell.GetComponent<Renderer>();
             if (shellRenderer != null) { shellRenderer.enabled = (i < currentShells); }
        }
    }


    public void UpdateShellVisuals()
    {
        SpawnSpiritBubbleShells();
    }

    void UpdateShellPositions()
    {
        if (shellOrbitCenter == null) return;
        Vector3 orbitCenter = shellOrbitCenter.position; // Get position every frame
        // Use local rotation for orbiting the center point
        shellOrbitCenter.Rotate(Vector3.up, orbitSpeed * Time.deltaTime);
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
        xpToNextLevel = Mathf.Max(1, xpToNextLevel);
    }

    public void GainXP(int amount)
    {
        if (amount <= 0) return;
        currentXP += amount;
        Debug.Log($"Gained {amount} XP. Current XP: {currentXP}/{xpToNextLevel}");
        OnXPChanged?.Invoke(currentXP, xpToNextLevel); // Event for UI

        while (currentXP >= xpToNextLevel)
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        currentLevel++;
        int excessXP = currentXP - xpToNextLevel;
        currentXP = excessXP;

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
        UpdateShellCountVisuals();
        // If using events:
        // OnHealthChanged?.Invoke(currentHealth, maxHealth);
        // OnManaChanged?.Invoke(currentMana, maxMana);
        // OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    // --- NEW Method for Teleport Bullet to Call ---
    public void TriggerTeleportAnimation()
    {
        Debug.Log($"Attempting to call TriggerTeleported. animationManager is null? {animationManager == null}");
        animationManager?.TriggerTeleported();
    }

    // --- Optional: Debug Methods ---
    [ContextMenu("Add 50 XP")]
    void DebugAddXP() { GainXP(50); }

    [ContextMenu("Level Up Manually")]
    void DebugLevelUp() { GainXP(xpToNextLevel - currentXP); }

    // --- Optional: UI Initialization Helper (if NOT solely relying on ProgressBarManager) ---
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