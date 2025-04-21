using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class PlayerStateManager : MonoBehaviour
{
    [Header("Core Stats")]
    public int maxHealth = 100;
    public int currentHealth;
    public int healthRecoveryRate = 2;
    public int maxMana = 100;
    public int manaCost = 10; // Base cost per shell / For shell count calc
    public int currentMana;
    public int manaRecoveryRate = 5;
    public int maxStamina = 100; // Add stamina cost/recovery if using run state stamina
    public int currentStamina;
    public int staminaRecoveryRate = 10;

    [Header("Movement Speeds")]
    public float walkSpeed = 10f;
    public float runSpeed = 20f;
    public float sneakSpeed = 5f;
    public float flySpeed = 15f;
    public float jumpForce = 20f; // Ground jump initial strength
    public float flapStrength = 10f; // Flying flap upward strength

    [Header("Physics & Ground Check")]
    public float gravity = -9.81f; // Standard gravity
    public Transform groundCheck; // Assign in Inspector
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer; // Assign in Inspector
    [HideInInspector] public bool isGrounded; // Note: CheckGrounded() is commented out, controller.isGrounded is used
    [HideInInspector] public float verticalVelocity = 0f;
    private bool hasJumped = false; // Used for double jump / flight entry

    [Header("Shooting & Abilities")]
    public BulletSpawnerState bulletSpawner; // Assign in Inspector
    public Transform firePoint; // Assign in Inspector (Still useful for firing direction/origin)
    public Transform shellOrbitCenter; // Assign in Inspector (Center for shell spawning/orbiting)
    public List<GameObject> shellPrefabs; // Assign in Inspector (match BulletType order)
    public float orbitRadius = 2f;
    public float orbitSpeed = 50f;
    [HideInInspector] public int maxShells;
    [HideInInspector] public int currentShells;
    [HideInInspector] public List<GameObject> orbitingShells = new List<GameObject>();

    [Header("Visuals (Optional)")]
    public GameObject rWingPrefab; // Assign Wing Prefab
    public GameObject lWingPrefab; // Assign Wing Prefab
    public Transform rWingPoint; // Assign Wing spawn/attach point
    public Transform lWingPoint; // Assign Wing spawn/attach point

    // --- Components ---
    [HideInInspector] public CharacterController controller;

    // --- Input Flags / State ---
    [HideInInspector] public Vector2 movement; // Input value from OnMove
    [HideInInspector] public bool isSneaking = false; // Toggled by OnSprint
    [HideInInspector] public bool isRunning = false; // Held true by OnRun
    [HideInInspector] public bool jumpInputPressedThisFrame = false; // Flag set by OnJump for one frame

    // --- State Machine ---
    [HideInInspector] public PlayerBaseState currentState;
    [HideInInspector] public PlayerIdleState idleState = new PlayerIdleState();
    [HideInInspector] public PlayerWalkingState walkState = new PlayerWalkingState();
    [HideInInspector] public PlayerSneakState sneakState = new PlayerSneakState();
    [HideInInspector] public PlayerRunningState runState = new PlayerRunningState();
    [HideInInspector] public PlayerHitState hitState = new PlayerHitState();
    //[HideInInspector] public PlayerFlyingState flyingState = new PlayerFlyingState();
    // TODO: Add a FallingState for better airborne control when not flying
    // [HideInInspector] public PlayerFallingState fallingState = new PlayerFallingState();


    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null) Debug.LogError("CharacterController not found on Player!");
        if (bulletSpawner == null) Debug.LogError("BulletSpawnerState not assigned in the Inspector!");
        if (groundCheck == null) Debug.LogError("GroundCheck Transform not assigned in the Inspector!");
        if (shellOrbitCenter == null) Debug.LogError("ShellOrbitCenter Transform not assigned in the Inspector!"); // Added check
    }

    void Start()
    {
        currentHealth = maxHealth;
        currentMana = maxMana;
        currentStamina = maxStamina; // Initialize stamina

        maxShells = (manaCost > 0) ? maxMana / manaCost : 0; // Calculate max shells safely
        currentShells = maxShells;

        UpdateShellVisuals(); // Spawn initial shells

        // Start recovery coroutines
        StartCoroutine(RecoverHealthOverTime());
        StartCoroutine(RecoverManaOverTime());
        StartCoroutine(RecoverStaminaOverTime()); // Add stamina recovery if needed

        // Start in Idle state
        SwitchState(idleState);
    }

    // --- Input System Handlers ---
    void OnMove(InputValue value) { movement = value.Get<Vector2>(); }
    void OnSprint(InputValue value) { if(value.isPressed) isSneaking = !isSneaking; } // Toggle Sneak on press
    void OnRun(InputValue value) { isRunning = value.isPressed; } // Hold Run
    void OnJump() {
        Debug.Log("Jump Input Received"); // Simplified log
        if (controller.isGrounded) {
            Debug.Log("Performing Ground Jump");
            hasJumped = true;
            Jump(); // Handle ground jump
        } else {
            Debug.Log("Jump Input while airborne (no action defined)");
            // Add double jump / flight entry logic here if desired
        }
        // The jumpInputPressedThisFrame = true logic was commented out, keep as is unless needed for other states like flying
    }

    void OnFire(InputValue value) {
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

    // --- Core Update Loop ---
    void Update()
    {
        // Ground check using CharacterController.isGrounded
        isGrounded = controller.isGrounded; // Update internal isGrounded flag

        if (isGrounded && verticalVelocity < 0) {
            verticalVelocity = -2f; // Keep player grounded
            hasJumped = false; // Reset jump flag when grounded
        } else {
            // Apply gravity when not grounded
            verticalVelocity += gravity * Time.deltaTime;
        }

        // --- State Update ---
        currentState?.UpdateState(this);

        // --- Reset Per-Frame Input Flags ---
        jumpInputPressedThisFrame = false;

        // --- Other Updates ---
        UpdateShellPositions(); // Update orbiting shell visuals
    }

    // The Physics.CheckSphere ground check is commented out, using controller.isGrounded instead.
    /*bool CheckGrounded() {
          if (groundCheck == null) return false;
          return Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
     }*/

    // --- Player Movement ---
    public void MovePlayer(Vector3 horizontalDirection, float speed)
    {
        horizontalDirection.y = 0;
        Vector3 finalMovement = (horizontalDirection.normalized * speed) + (Vector3.up * verticalVelocity);
        controller.Move(finalMovement * Time.deltaTime);
    }

    // --- Jump Logic ---
    public void Jump()
    {
        // Assumes this is only called when controller.isGrounded is true (checked in OnJump)
        verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
        // Note: isGrounded will become false automatically after the Move call with positive verticalVelocity
    }

    // --- Damage & Death ---
    public void TakeDamage(int damage)
    {
        if (currentHealth > 0 && currentState != hitState)
        {
            hitState.SetDamage(damage);
            SwitchState(hitState);
        }
         else if (currentHealth <= 0) {
            // Already dead or dying, ensure Die is called or handled appropriately
            // Die(); // Could potentially call Die here, but HitState likely handles it
         }
    }

    public void Die()
    {
        Debug.Log("Player Died! Loading End Scene...");
        // Consider disabling controls, playing effects etc.
        // controller.enabled = false;
        SceneManager.LoadScene("EndScene"); // Ensure "EndScene" is in Build Settings
    }

    // --- Mana & Shells ---
    public bool UseMana(int mCost) // Removed redundant player parameter
    {
        if (currentMana >= mCost) {
            currentMana -= mCost;
            currentMana = Mathf.Clamp(currentMana, 0, maxMana);
            UpdateShellCountVisuals();
            return true;
        } else {
            return false;
        }
    }

    public void UpdateShellCountVisuals() {
         if (manaCost <= 0) return;

         int targetShells = currentMana / manaCost;
         targetShells = Mathf.Clamp(targetShells, 0, maxShells);

         for(int i = 0; i < orbitingShells.Count; i++) {
              if (orbitingShells[i] != null) {
                   Renderer rend = orbitingShells[i].GetComponent<Renderer>();
                   if (rend != null) {
                        rend.enabled = (i < targetShells);
                   }
              }
         }
         currentShells = targetShells;
    }

    // --- Modified Shell Spawning ---
    void SpawnSpiritBubbleShells() {
         // Check essential dependencies including the new shellOrbitCenter
         if (bulletSpawner == null || shellPrefabs == null || shellPrefabs.Count == 0 || manaCost <= 0 || shellOrbitCenter == null) {
             Debug.LogError("Cannot spawn shells: Dependencies missing (BulletSpawner, ShellPrefabs, ShellOrbitCenter), manaCost is zero, or ShellOrbitCenter is not assigned.");
             return;
         }
         int currentTypeIndex = (int)bulletSpawner.CurrentBulletType;
          if(currentTypeIndex < 0 || currentTypeIndex >= shellPrefabs.Count || shellPrefabs[currentTypeIndex] == null) {
              Debug.LogError($"Invalid shell prefab index {currentTypeIndex}. Check shellPrefabs list.");
              currentTypeIndex = 0; // Fallback
              if(shellPrefabs.Count == 0 || shellPrefabs[0] == null) return; // No valid fallback
          }
         GameObject shellPrefabToSpawn = shellPrefabs[currentTypeIndex];

         // Clear existing shells
         foreach (GameObject oldShell in orbitingShells) { if (oldShell != null) Destroy(oldShell); }
         orbitingShells.Clear();

         maxShells = maxMana / manaCost;
         currentShells = Mathf.Clamp(currentMana / manaCost, 0, maxShells);

         // Use the assigned Transform's position as the center point for spawning
         Vector3 centerPoint = shellOrbitCenter.position;

         // Spawn max potential shells around the centerPoint
         for (int i = 0; i < maxShells; i++) {
             float angle = i * (360f / Mathf.Max(1, maxShells)); // Avoid division by zero
             // Calculate orbital position relative to the centerPoint
             float x = centerPoint.x + orbitRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
             float z = centerPoint.z + orbitRadius * Mathf.Sin(angle * Mathf.Deg2Rad);
             Vector3 spawnPosition = new Vector3(x, centerPoint.y, z); // Use centerPoint's y

             // Instantiate and parent the shell
             GameObject shell = Instantiate(shellPrefabToSpawn, spawnPosition, Quaternion.identity, transform); // Consider parenting to shellOrbitCenter? Or keep on player?
             orbitingShells.Add(shell);

             // Enable renderer based on current available mana/shells
             Renderer shellRenderer = shell.GetComponent<Renderer>();
             if(shellRenderer != null) { shellRenderer.enabled = (i < currentShells); }
         }
    }

     public void UpdateShellVisuals() {
         SpawnSpiritBubbleShells(); // This is called on type change or initialization
     }

    // --- Modified Shell Orbiting ---
    void UpdateShellPositions() {
        // Ensure the orbit center is assigned
        if (shellOrbitCenter == null) {
            // Optional: Log warning only once or less frequently
            // Debug.LogWarning("ShellOrbitCenter is not assigned. Shells cannot update position.");
            return; // Stop updating if center is missing
        }

        // Use the assigned Transform's position as the orbit center
        Vector3 orbitCenter = shellOrbitCenter.position;

        // Update position for each active shell
        for (int i = 0; i < orbitingShells.Count; i++) {
            if (orbitingShells[i] != null) {
                 // Calculate angle based on time, speed, and index
                 float angle = Time.time * orbitSpeed + (i * 360f / Mathf.Max(1, maxShells)); // Avoid division by zero if maxShells is 0
                 // Calculate world position based on orbit parameters
                 float x = orbitCenter.x + orbitRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
                 float z = orbitCenter.z + orbitRadius * Mathf.Sin(angle * Mathf.Deg2Rad);
                 float y = orbitCenter.y; // Orbit horizontally at the center's height
                 // Apply the calculated position
                 orbitingShells[i].transform.position = new Vector3(x, y, z);
                 // Optional: Make shells face the orbit center
                 // orbitingShells[i].transform.LookAt(orbitCenter);
            }
        }
    }


    // --- State Switching ---
    public void SwitchState(PlayerBaseState newState)
    {
        if (newState == null || newState == currentState) return;

        currentState?.ExitState(this);
        // Debug.Log($"Switching from {currentState?.GetType().Name ?? "None"} to {newState.GetType().Name}"); // Optional state change logging
        currentState = newState;
        currentState.EnterState(this);
    }

    // --- Recovery Coroutines ---
    private IEnumerator RecoverHealthOverTime() {
        while (true) {
            yield return new WaitForSeconds(5f);
            if (currentHealth < maxHealth && currentHealth > 0) {
                currentHealth += healthRecoveryRate;
                currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
                // Update UI if needed
            }
        }
    }
    private IEnumerator RecoverManaOverTime() {
        while (true) {
            yield return new WaitForSeconds(1f);
            if (currentMana < maxMana) {
                currentMana += manaRecoveryRate;
                currentMana = Mathf.Clamp(currentMana, 0, maxMana);
                UpdateShellCountVisuals(); // Update shells as mana recovers
            }
        }
    }
    private IEnumerator RecoverStaminaOverTime() {
         while (true) {
             yield return new WaitForSeconds(1f);
             // Recover only if not performing stamina-draining actions (e.g., not running)
             if (currentState != runState && currentStamina < maxStamina) { // Example condition
                 currentStamina += staminaRecoveryRate;
                 currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
                 // Update UI if needed
             }
         }
     }

    // --- Gizmos ---
    void OnDrawGizmosSelected() {
        // Draw ground check sphere gizmo
        if (groundCheck != null) {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        // Draw orbit radius gizmo around the shellOrbitCenter
        if (shellOrbitCenter != null) {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(shellOrbitCenter.position, orbitRadius);
        }
    }
}