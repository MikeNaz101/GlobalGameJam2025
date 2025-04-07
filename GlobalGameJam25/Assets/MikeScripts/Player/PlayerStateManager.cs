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
    [HideInInspector] public bool isGrounded;
    [HideInInspector] public float verticalVelocity = 0f;
    private bool hasJumped = false; // Used for double jump / flight entry

    [Header("Shooting & Abilities")]
    public BulletSpawnerState bulletSpawner; // Assign in Inspector
    public Transform firePoint; // Assign in Inspector
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
    // Called by PlayerInput component events
    void OnMove(InputValue value) { movement = value.Get<Vector2>(); }
    void OnSprint(InputValue value) { if(value.isPressed) isSneaking = !isSneaking; } // Toggle Sneak on press
    void OnRun(InputValue value) { isRunning = value.isPressed; } // Hold Run
    void OnJump() {
        Debug.Log("Jumped!");
        if (controller.isGrounded) {
            Debug.Log("Really Jumped!");
            hasJumped = true;
            Jump(); // Handle ground jump
        }
        else {
            Debug.Log("Just kidding...");
        }

        //if (value.isPressed) {
            //jumpInputPressedThisFrame = true; // Set flag for states (like Flying flap)
            // / flight entry logic
        //}
    }
    //void OnFire(InputValue value) { GetComponent<PlayerShooting>()?.StartCharge(); } // Simplified - assumes press
    //void OnFireRelease(InputValue value) { GetComponent<PlayerShooting>()?.EndCharge(); } // Need separate release action
    // Or Modify OnFire to handle both press/release:
    
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
        Debug.Log("OnChangeWeaponVector2 called with: " + scrollValue); // Test if this works

        // Get the vertical scroll component
        float scrollY = scrollValue.y;

        // Now, put the logic from your original OnChangeWeapon here,
        // using scrollY instead of value.Get<Vector2>().y
        if (bulletSpawner == null) return;

        if (scrollY != 0)
        {
            int changeDirection = scrollY > 0 ? 1 : -1;
            bulletSpawner.ChangeBulletType(changeDirection);
            UpdateShellVisuals();
            Debug.Log("Changed bullet type via Vector2 method. Direction: " + changeDirection);
        }
    }
    /*public void OnChangeWeapon(InputValue value) {
         if (bulletSpawner == null) return;
         float scroll = value.Get<Vector2>().y;
         if (scroll != 0) {
             int change = scroll > 0 ? 1 : -1;
             bulletSpawner.ChangeBulletType(change);
             UpdateShellVisuals();
         }
     }*/

    // --- Core Update Loop ---
    void Update()
    {
        // Perform ground check
        //isGrounded = CheckGrounded();

        if (controller.isGrounded && verticalVelocity < 0) {
            verticalVelocity = -2f; // A small negative value helps stick to the ground
            // Maybe reset hasJumped here if needed for double jump logic later
            // hasJumped = false;
        } else {
            // Apply gravity when not grounded
            verticalVelocity += gravity * Time.deltaTime; // Use += because gravity is negative
        }

        // Reset double jump flag only when grounded
        /*if (isGrounded) {
            hasJumped = false;
        }*/

        // --- State Update ---
        // The current state's UpdateState is responsible for movement logic and transitions
        currentState?.UpdateState(this);

        // --- Reset Per-Frame Input Flags ---
        // Do this AFTER the state update so the state could read the flag
        jumpInputPressedThisFrame = false;

        // --- Other Updates ---
        UpdateShellPositions(); // Update orbiting shell visuals
    }

     /*bool CheckGrounded() {
          if (groundCheck == null) return false;
          return Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
     }*/

    // --- Player Movement ---
    // Called by states to apply movement based on their logic and speed.
    public void MovePlayer(Vector3 horizontalDirection, float speed)
    {
        horizontalDirection.y = 0; // Ensure movement is purely horizontal based on input direction
        Vector3 finalMovement = (horizontalDirection.normalized * speed) + (Vector3.up * verticalVelocity);
        controller.Move(finalMovement * Time.deltaTime);

        // Optional: Add player model rotation here based on horizontalDirection
        // if (horizontalDirection.sqrMagnitude > 0.01f) {
        //    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(horizontalDirection), Time.deltaTime * 10f);
        // }
    }

    // --- Jump Logic ---
    // Handles ground jump and entering flight state on double jump.
    public void Jump()
    {
        if (controller.isGrounded) {
            hasJumped = true;
            verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity); // Calculate impulse for jump height
        }
        /*if (isGrounded) {
            verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity); // Calculate impulse for jump height
            isGrounded = false;
            hasJumped = true; // Mark first jump
             Debug.Log("Ground Jump!");
            // TODO: Optionally switch to a FallingState immediately after jumping
            // SwitchState(fallingState);
        }*/
        // Double Jump -> Enter Flying State
        // Allow if airborne, haven't double-jumped yet, and NOT already flying
        /*else if (!hasJumped && currentState != flyingState) {
            hasJumped = true; // Consume the double jump
            SwitchState(flyingState);
            Debug.Log("Double Jump -> Entering Flying State!");
        }*/
        // Note: If already in flyingState, jumpInputPressedThisFrame handles flapping within the state's Update
    }

    // --- Damage & Death ---
    public void TakeDamage(int damage)
    {
        if (currentHealth > 0 && currentState != hitState) // Prevent re-hitting during hit stun
        {
            hitState.SetDamage(damage); // Pass damage amount to the state
            SwitchState(hitState);
        }
         else if (currentHealth <= 0) {
             // Already dead, do nothing? Or ensure Die() is called?
         }
    }

    public void Die()
    {
        // This method is called when health reaches 0 (e.g., from HitState)
        Debug.Log("Player Died! Loading End Scene...");
        // Add any death effects, disable controls, play sound, etc.
        // controller.enabled = false; // Example: Disable movement
        // Load the end scene (ensure it's in Build Settings)
        // Consider adding a delay before loading scene: StartCoroutine(LoadEndSceneAfterDelay(2.0f));
        SceneManager.LoadScene("EndScene");
    }
    /* // Example Delay Coroutine
    private IEnumerator LoadEndSceneAfterDelay(float delay) {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene("EndScene");
    } */


    // --- Mana & Shells ---
    public bool UseMana(int mCost, PlayerStateManager player) // Assuming self-reference isn't needed
    {
        if (currentMana >= mCost) {
            currentMana -= mCost;
            currentMana = Mathf.Clamp(currentMana, 0, maxMana);
            UpdateShellCountVisuals(); // Update shell visuals based on new mana
            return true;
        } else {
            return false; // Not enough mana
        }
    }

    public void UpdateShellCountVisuals() {
         if (manaCost <= 0) return; // Avoid division by zero

         int targetShells = currentMana / manaCost;
         targetShells = Mathf.Clamp(targetShells, 0, maxShells);

         // Enable/Disable shells based on target count
         for(int i = 0; i < orbitingShells.Count; i++) {
              if (orbitingShells[i] != null) {
                   Renderer rend = orbitingShells[i].GetComponent<Renderer>();
                   if (rend != null) {
                        rend.enabled = (i < targetShells);
                   }
              }
         }
         currentShells = targetShells; // Update the count
    }

    void SpawnSpiritBubbleShells() {
         if (bulletSpawner == null || shellPrefabs == null || shellPrefabs.Count == 0 || manaCost <= 0) {
             Debug.LogError("Cannot spawn shells: Dependencies missing or manaCost is zero.");
             return;
         }
         int currentTypeIndex = (int)bulletSpawner.CurrentBulletType;
          if(currentTypeIndex < 0 || currentTypeIndex >= shellPrefabs.Count || shellPrefabs[currentTypeIndex] == null) {
              Debug.LogError($"Invalid shell prefab index {currentTypeIndex}. Check shellPrefabs list.");
              currentTypeIndex = 0; // Fallback to first? Check if valid.
              if(shellPrefabs.Count == 0 || shellPrefabs[0] == null) return; // No valid fallback
          }
         GameObject shellPrefabToSpawn = shellPrefabs[currentTypeIndex];

         // Clear existing
         foreach (GameObject oldShell in orbitingShells) { if (oldShell != null) Destroy(oldShell); }
         orbitingShells.Clear();

         maxShells = maxMana / manaCost;
         currentShells = Mathf.Clamp(currentMana / manaCost, 0, maxShells);

         // Spawn max potential shells
         for (int i = 0; i < maxShells; i++) {
             float angle = i * (360f / maxShells);
             Vector3 centerPoint = transform.position + Vector3.up * 1.0f;
             float x = centerPoint.x + orbitRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
             float z = centerPoint.z + orbitRadius * Mathf.Sin(angle * Mathf.Deg2Rad);
             Vector3 spawnPosition = new Vector3(x, centerPoint.y, z);

             GameObject shell = Instantiate(shellPrefabToSpawn, spawnPosition, Quaternion.identity, transform); // Parent to player
             orbitingShells.Add(shell);

             // Enable based on current mana
             Renderer shellRenderer = shell.GetComponent<Renderer>();
             if(shellRenderer != null) { shellRenderer.enabled = (i < currentShells); }
         }
    }

     public void UpdateShellVisuals() { SpawnSpiritBubbleShells(); } // Called on type change

    void UpdateShellPositions() {
        Vector3 orbitCenter = firePoint != null ? firePoint.position : transform.position + Vector3.up;
        for (int i = 0; i < orbitingShells.Count; i++) {
            if (orbitingShells[i] != null) {
                 float angle = Time.time * orbitSpeed + (i * 360f / Mathf.Max(1, maxShells)); // Avoid division by zero
                 float x = orbitCenter.x + orbitRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
                 float z = orbitCenter.z + orbitRadius * Mathf.Sin(angle * Mathf.Deg2Rad);
                 float y = orbitCenter.y; // Or add oscillation
                 orbitingShells[i].transform.position = new Vector3(x, y, z);
                 // Optional: orbitingShells[i].transform.LookAt(orbitCenter);
            }
        }
    }


    // --- State Switching ---
    public void SwitchState(PlayerBaseState newState)
    {
        if (newState == null || newState == currentState) return; // Ignore invalid or same state switch

        currentState?.ExitState(this); // Safely call Exit on current state
        // Debug.Log($"Switching from {currentState?.GetType().Name ?? "None"} to {newState.GetType().Name}"); // Optional debug
        currentState = newState;
        currentState.EnterState(this);
    }

    // --- Recovery Coroutines ---
    private IEnumerator RecoverHealthOverTime() {
        while (true) {
            yield return new WaitForSeconds(5f);
            if (currentHealth < maxHealth && currentHealth > 0) { // Don't recover if dead
                currentHealth += healthRecoveryRate;
                currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
                // Update health bar?
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
             yield return new WaitForSeconds(1f); // Recovery rate
             // Only recover if not running or performing other stamina-draining actions
             if (currentState != runState && currentStamina < maxStamina) {
                 currentStamina += staminaRecoveryRate;
                 currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
                 // Update stamina bar?
             }
         }
     }

    // --- Gizmos ---
    void OnDrawGizmosSelected() {
        // Draw ground check sphere in editor
        if (groundCheck != null) {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}