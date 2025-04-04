using UnityEngine;
using UnityEngine.InputSystem; // Using new Input System
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Required for aiming reticle Image

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
    public int maxStamina = 100;
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
    public float gravity = -9.81f;
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

    // --- NEW: Aiming Settings ---
    [Header("Aiming Settings")]
    public Camera mainCamera; // Assign your main scene camera
    public Transform firstPersonCamPosition; // Assign an empty GameObject where the FP camera should be
    public float aimSensitivity = 1.5f; // Mouse sensitivity for aiming
    public Image aimReticle; // Assign your UI Image for the reticle (optional)
    public float maxAimDistance = 100f; // How far the aiming raycast checks (for visual feedback)

    [Header("Visuals (Optional)")]
    public GameObject rWingPrefab; // Assign Wing Prefab
    public GameObject lWingPrefab; // Assign Wing Prefab
    public Transform rWingPoint; // Assign Wing spawn/attach point
    public Transform lWingPoint; // Assign Wing spawn/attach point

    // --- Components ---
    [HideInInspector] public CharacterController controller;
    [HideInInspector] public PlayerShooting playerShooting; // Reference to PlayerShooting script

    // --- Input Flags / State ---
    [HideInInspector] public Vector2 movement; // Input value from OnMove
    [HideInInspector] public Vector2 lookInput; // Store look input for aiming
    [HideInInspector] public bool isSneaking = false; // Toggled by OnSprint
    [HideInInspector] public bool isRunning = false; // Held true by OnRun
    [HideInInspector] public bool jumpInputPressedThisFrame = false; // Flag set by OnJump for one frame

    // --- Aiming State Variables ---
    private bool isAiming = false;
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private Transform originalCameraParent;
    private float cameraPitch = 0f;
    private bool aimingInputHeld = false; // Track aim button state from Input System


    // --- State Machine ---
    [HideInInspector] public PlayerBaseState currentState;
    [HideInInspector] public PlayerIdleState idleState = new PlayerIdleState();
    [HideInInspector] public PlayerWalkingState walkState = new PlayerWalkingState();
    [HideInInspector] public PlayerSneakState sneakState = new PlayerSneakState();
    [HideInInspector] public PlayerRunningState runState = new PlayerRunningState();
    [HideInInspector] public PlayerHitState hitState = new PlayerHitState();
    [HideInInspector] public PlayerFlyingState flyingState = new PlayerFlyingState();
    // TODO: Add a FallingState for better airborne control when not flying
    // [HideInInspector] public PlayerFallingState fallingState = new PlayerFallingState();


    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerShooting = GetComponent<PlayerShooting>(); // Get PlayerShooting component

        if (controller == null) Debug.LogError("CharacterController not found on Player!");
        if (playerShooting == null) Debug.LogError("PlayerShooting component not found on Player!");
        if (bulletSpawner == null) Debug.LogError("BulletSpawnerState not assigned in the Inspector!");
        if (groundCheck == null) Debug.LogError("GroundCheck Transform not assigned in the Inspector!");
        if (firstPersonCamPosition == null) Debug.LogError("First Person Camera Position Transform not assigned!");

        // Attempt to find main camera if not assigned
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) Debug.LogError("Main Camera could not be found!");
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

        // Ensure PlayerShooting has reference to this manager
        if (playerShooting != null && playerShooting.player == null)
        {
            playerShooting.player = this;
        }

        // Hide reticle initially
        if (aimReticle != null) aimReticle.enabled = false;

        // Start in Idle state
        SwitchState(idleState);
    }

    // --- Input System Handlers ---
    // Called by PlayerInput component events
    void OnMove(InputValue value) { movement = value.Get<Vector2>(); }
    void OnLook(InputValue value) { lookInput = value.Get<Vector2>(); } // Store mouse/stick input
    void OnSprint(InputValue value) { if(value.isPressed) isSneaking = !isSneaking; } // Toggle Sneak on press
    void OnRun(InputValue value) { isRunning = value.isPressed; } // Hold Run
    void OnJump(InputValue value) {
        if (value.isPressed) {
            jumpInputPressedThisFrame = true; // Set flag for states (like Flying flap)
            Jump(); // Handle ground jump / flight entry logic
        }
    }

    // --- MODIFIED OnFire --- Handles BOTH Aimed Teleport Fire AND Normal Charge Fire ---
    void OnFire(InputValue value)
    {
        if (playerShooting == null) return;

        bool isPressed = value.isPressed;
        // Ensure bulletSpawner is valid before checking type
        bool isTeleportSelected = (bulletSpawner != null && bulletSpawner.CurrentBulletType == BulletType.Type3);

        if (isPressed) // Button Down
        {
            if (isAiming && isTeleportSelected) // *** CASE 1: Aiming with Teleport ***
            {
                // Call the immediate fire function in PlayerShooting, passing camera direction
                playerShooting.FireTeleportImmediate(mainCamera.transform.forward);
                StopAiming(); // Stop aiming right after firing
            }
            else if (!isAiming) // *** CASE 2: Normal Firing (Not aiming) ***
            {
                // Start the regular charge mechanic
                playerShooting.StartCharge();
            }
            // If isAiming is true but teleport is NOT selected, do nothing on primary fire press.
        }
        else // Button Release
        {
            // Only call EndCharge if we are NOT aiming the teleport bullet
            // This prevents EndCharge from interfering with the immediate fire done on press.
            // It also correctly handles releasing the button for normal charged shots.
            if (!isAiming || !isTeleportSelected)
            {
                 playerShooting.EndCharge();
            }
        }
    }

    // --- NEW/MODIFIED OnSecondaryFire --- Handles Aiming ---
    void OnSecondaryFire(InputValue value)
    {
        aimingInputHeld = value.isPressed; // Store the raw input state

        // Ensure bulletSpawner is valid before checking type
        bool isTeleportSelected = (bulletSpawner != null && bulletSpawner.CurrentBulletType == BulletType.Type3);

        // Start Aiming ONLY if Teleport is selected AND the input is pressed
        if (aimingInputHeld && isTeleportSelected && !isAiming)
        {
            StartAiming();
        }
        // Stop Aiming if input is released OR if bullet type changes while aiming
        // Check isAiming flag first to avoid unnecessary calls to StopAiming
        else if (isAiming && (!aimingInputHeld || !isTeleportSelected))
        {
            StopAiming();
        }
    }

    // Modified OnChangeWeaponVector2 to handle stopping aim if changing away from Teleport
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

            // If aiming and weapon changes away from Teleport, stop aiming
            if (isAiming && bulletSpawner.CurrentBulletType != BulletType.Type3)
            {
                StopAiming();
            }
            Debug.Log("Changed bullet type via Vector2 method. Direction: " + changeDirection);
        }
    }
    /* Original OnChangeWeapon commented out
    public void OnChangeWeapon(InputValue value) {
         if (bulletSpawner == null) return;
         float scroll = value.Get<Vector2>().y;
         if (scroll != 0) {
             int change = scroll > 0 ? 1 : -1;
             bulletSpawner.ChangeBulletType(change);
             UpdateShellVisuals();
             // Add the StopAiming check here too if using this method
             if (isAiming && bulletSpawner.CurrentBulletType != BulletType.Type3) { StopAiming(); }
         }
     }*/

    // --- Core Update Loop ---
    void Update()
    {
        isGrounded = CheckGrounded();

        if (!isGrounded) {
            verticalVelocity += gravity * Time.deltaTime;
        } else if (verticalVelocity < 0) {
            verticalVelocity = -2f; // Small downward force to keep grounded
        }

        if (isGrounded) {
            hasJumped = false;
        }

        // --- Handle Aiming Look ---
        if (isAiming)
        {
            AimLook(); // Apply mouse look if aiming
            UpdateAimTarget(); // Optional visual feedback update
        }

        // --- State Update ---
        // Only run state update if NOT aiming, or allow aiming state to override/coexist?
        // Current approach lets state update run, but AimLook overrides rotation when active.
        currentState?.UpdateState(this);

        // --- Reset Per-Frame Input Flags ---
        // Do this AFTER the state update so the state could read the flag
        jumpInputPressedThisFrame = false;

        // --- Other Updates ---
        UpdateShellPositions(); // Update orbiting shell visuals
    }

     bool CheckGrounded() {
          if (groundCheck == null) return false;
          return Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
     }

    // --- Player Movement ---
    // Called by states to apply movement based on their logic and speed.
    public void MovePlayer(Vector3 horizontalDirection, float speed)
    {
        // If aiming, horizontal input might be zero if you want aiming to lock movement,
        // or you could allow strafing using the 'movement' input vector.
        // Current implementation lets states control movement direction/speed.

        horizontalDirection.y = 0; // Ensure movement is purely horizontal based on input direction
        Vector3 finalMovement = (horizontalDirection.normalized * speed) + (Vector3.up * verticalVelocity);
        controller.Move(finalMovement * Time.deltaTime);

        // Optional: Add player model rotation here based on horizontalDirection, but ONLY if not aiming
        // if (!isAiming && horizontalDirection.sqrMagnitude > 0.01f) {
        //    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(horizontalDirection), Time.deltaTime * 10f);
        // }
    }

    // --- Jump Logic ---
    // Handles ground jump and entering flight state on double jump.
    public void Jump()
    {
        if (isGrounded) {
            verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity); // Calculate impulse for jump height
            isGrounded = false;
            hasJumped = true; // Mark first jump
             Debug.Log("Ground Jump!");
            // TODO: Optionally switch to a FallingState immediately after jumping
            // SwitchState(fallingState);
        }
        // Double Jump -> Enter Flying State
        // Allow if airborne, haven't double-jumped yet, and NOT already flying
        else if (!hasJumped && currentState != flyingState) {
            hasJumped = true; // Consume the double jump
            SwitchState(flyingState);
            Debug.Log("Double Jump -> Entering Flying State!");
        }
        // Note: If already in flyingState, jumpInputPressedThisFrame handles flapping within the state's Update
    }

    // --- Damage & Death ---
    public void TakeDamage(int damage)
    {
        if (currentHealth > 0 && currentState != hitState) // Prevent re-hitting during hit stun
        {
            // If aiming when hit, stop aiming first
            if (isAiming) StopAiming();

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

        // If aiming when dying, stop aiming to restore cursor etc.
        if (isAiming) StopAiming();

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
    public bool UseMana(int mCost, PlayerStateManager player) // Assuming self-reference isn't needed here
    {
        if (currentMana >= mCost) {
            currentMana -= mCost;
            currentMana = Mathf.Clamp(currentMana, 0, maxMana);
            UpdateShellCountVisuals(); // Update shell visuals based on new mana
            return true;
        } else {
            Debug.Log($"Failed to use {mCost} mana. Needed {mCost}, Have: {currentMana}");
            return false; // Not enough mana
        }
    }

    // Updates existing shells' visibility based on current mana count
    public void UpdateShellCountVisuals() {
         if (manaCost <= 0) return; // Avoid division by zero

         int targetShells = currentMana / manaCost;
         targetShells = Mathf.Clamp(targetShells, 0, maxShells);

         // Enable/Disable shells based on target count
         for(int i = 0; i < orbitingShells.Count; i++) {
              if (orbitingShells[i] != null) {
                   Renderer rend = orbitingShells[i].GetComponent<Renderer>();
                   if (rend != null) {
                        // Only enable shells up to the target count
                        rend.enabled = (i < targetShells);
                   }
              }
         }
         currentShells = targetShells; // Update the count
    }

    // Spawns the maximum potential number of shells for the current weapon type
    // Called by UpdateShellVisuals
    void SpawnSpiritBubbleShells() {
         if (bulletSpawner == null || shellPrefabs == null || shellPrefabs.Count == 0 || manaCost <= 0) {
             Debug.LogError("Cannot spawn shells: Dependencies missing or manaCost is zero.");
             return;
         }
         int currentTypeIndex = (int)bulletSpawner.CurrentBulletType;
          if(currentTypeIndex < 0 || currentTypeIndex >= shellPrefabs.Count || shellPrefabs[currentTypeIndex] == null) {
              Debug.LogError($"Invalid shell prefab index {currentTypeIndex}. Check shellPrefabs list and ensure it matches BulletType enum indices.");
              // Attempt fallback, but safest might be to just return if invalid index
              return;
          }
         GameObject shellPrefabToSpawn = shellPrefabs[currentTypeIndex];

         // Clear existing shells before spawning new ones
         foreach (GameObject oldShell in orbitingShells) { if (oldShell != null) Destroy(oldShell); }
         orbitingShells.Clear();

         maxShells = maxMana / manaCost; // Recalculate based on current potential max mana
         currentShells = Mathf.Clamp(currentMana / manaCost, 0, maxShells); // Recalculate based on current mana

         // Spawn max potential shells for this type
         for (int i = 0; i < maxShells; i++) {
             // Calculate position using the same logic as UpdateShellPositions
             Vector3 orbitCenter = firePoint != null ? firePoint.position : transform.position + Vector3.up;
             float angle = (i * 360f / Mathf.Max(1, maxShells)); // Initial angle offset per shell
             float x = orbitCenter.x + orbitRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
             float z = orbitCenter.z + orbitRadius * Mathf.Sin(angle * Mathf.Deg2Rad);
             float y = orbitCenter.y;
             Vector3 spawnPosition = new Vector3(x, y, z);

             GameObject shell = Instantiate(shellPrefabToSpawn, spawnPosition, Quaternion.identity, transform); // Parent to player
             orbitingShells.Add(shell);

             // Enable based on current mana count immediately after spawning
             Renderer shellRenderer = shell.GetComponent<Renderer>();
             if(shellRenderer != null) { shellRenderer.enabled = (i < currentShells); }
         }
    }

     // Public wrapper to handle respawning shells when weapon type changes
     public void UpdateShellVisuals() { SpawnSpiritBubbleShells(); }

    // Updates the positions of currently active orbiting shells
    void UpdateShellPositions() {
        Vector3 orbitCenter = firePoint != null ? firePoint.position : transform.position + Vector3.up;
        // Use currentShells to potentially optimize if many shells are inactive?
        // Or just iterate through all potential slots in orbitingShells list.
        for (int i = 0; i < orbitingShells.Count; i++) {
            if (orbitingShells[i] != null) { // Check if shell exists
                 // Only update position if renderer is enabled? Might save tiny performance.
                 // Renderer rend = orbitingShells[i].GetComponent<Renderer>();
                 // if (rend != null && rend.enabled) {
                     float angle = Time.time * orbitSpeed + (i * 360f / Mathf.Max(1, maxShells)); // Avoid division by zero
                     float x = orbitCenter.x + orbitRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
                     float z = orbitCenter.z + orbitRadius * Mathf.Sin(angle * Mathf.Deg2Rad);
                     float y = orbitCenter.y; // Or add oscillation: + Mathf.Sin(Time.time * orbitSpeed * 0.5f + i) * 0.1f;
                     orbitingShells[i].transform.position = new Vector3(x, y, z);
                     // Optional: Make shells look towards center or player
                     // orbitingShells[i].transform.LookAt(orbitCenter);
                 // }
            }
        }
    }


    // --- State Switching ---
    public void SwitchState(PlayerBaseState newState)
    {
        if (newState == null || newState == currentState) return; // Ignore invalid or same state switch

        // If switching state while aiming, stop aiming first? (e.g., if hit)
        // Handled within TakeDamage for HitState. Could force StopAiming here if desired for all state changes.
        // if(isAiming) StopAiming(); // Uncomment to force stop aiming on ANY state change

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
                // Update health bar UI?
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
             // You might need more sophisticated checks based on current state's properties
             if (currentState != runState && currentStamina < maxStamina) { // Example check
                 currentStamina += staminaRecoveryRate;
                 currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
                 // Update stamina bar UI?
             }
         }
    }

    // --- NEW Aiming Functions ---

    void StartAiming()
    {
        // Prevent aiming if already doing a normal charge
        if (playerShooting != null && playerShooting._isCharging) // Check if playerShooting exists
        {
            Debug.Log("Cannot aim while charging another shot.");
            return;
        }
        // Prevent aiming in certain states? (e.g., HitState, maybe Flying?)
        if (currentState == hitState) return; // Example: Can't aim when hit

        isAiming = true;
        if (aimReticle != null) aimReticle.enabled = true;

        // Store original camera state only if mainCamera is valid
        if (mainCamera != null)
        {
            originalCameraParent = mainCamera.transform.parent;
            originalCameraPosition = mainCamera.transform.localPosition;
            originalCameraRotation = mainCamera.transform.localRotation;

            // Move camera to first-person position if valid
            if (firstPersonCamPosition != null)
            {
                mainCamera.transform.parent = firstPersonCamPosition;
                mainCamera.transform.localPosition = Vector3.zero;
                mainCamera.transform.localRotation = Quaternion.identity;
                cameraPitch = 0f; // Reset pitch based on current FP anchor rotation

                // Optional: Adjust initial pitch slightly based on the FP anchor's world rotation if needed
                // cameraPitch = firstPersonCamPosition.eulerAngles.x; // Might need careful adjustment if used
            } else {
                 Debug.LogError("First Person Cam Position is not assigned, cannot move camera!");
                 // Should we abort aiming here?
                 isAiming = false;
                 if (aimReticle != null) aimReticle.enabled = false;
                 return;
            }

        } else {
            Debug.LogError("Main Camera reference is missing, cannot modify camera for aiming!");
            // Abort aiming if no camera reference
            isAiming = false;
            if (aimReticle != null) aimReticle.enabled = false;
            return;
        }


        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Optional: Notify current state that aiming has started?
        // currentState?.OnAimStart(this);
        Debug.Log("Started Aiming");
    }

    void StopAiming()
    {
        // Only stop if actually aiming
        if (!isAiming) return;

        isAiming = false;
        if (aimReticle != null) aimReticle.enabled = false;

        // Restore original camera state only if mainCamera is valid
        // And check parentage in case something else moved the camera during aiming
        if (mainCamera != null && mainCamera.transform.parent == firstPersonCamPosition)
        {
            mainCamera.transform.parent = originalCameraParent;
            mainCamera.transform.localPosition = originalCameraPosition;
            mainCamera.transform.localRotation = originalCameraRotation;
        }
        // If mainCamera was null or parent changed, we might not be able to restore, but still reset cursor etc.

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Optional: Notify current state that aiming has stopped?
        // currentState?.OnAimEnd(this);
        Debug.Log("Stopped Aiming");
    }

    void AimLook()
    {
        // Get look input (should be delta values from Input System Action)
        // Multiply by sensitivity and time for frame-rate independence
        // Adjust the final multiplier (e.g., 10f) to get desired sensitivity feel
        float mouseX = lookInput.x * Time.deltaTime * aimSensitivity * 10f;
        float mouseY = lookInput.y * Time.deltaTime * aimSensitivity * 10f;

        // Rotate the player body left/right based on horizontal look input
        // Using Space.World ensures rotation is around the global Y axis
        transform.Rotate(Vector3.up * mouseX, Space.World);

        // Rotate the camera up/down (pitch) based on vertical look input
        cameraPitch -= mouseY; // Subtract because typical mouse Y is inverted for camera pitch
        cameraPitch = Mathf.Clamp(cameraPitch, -85f, 85f); // Clamp vertical look range

        // Apply pitch directly to the camera's local rotation relative to the FP anchor
        if (mainCamera != null)
        {
            mainCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }
    }

    // Optional function for visual feedback (reticle color, marker)
    void UpdateAimTarget()
    {
        // No need to run if no reticle assigned or not aiming
        if (aimReticle == null || !isAiming || mainCamera == null) return;

        RaycastHit hit;
        // Raycast from camera's current position and forward direction
        if (Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out hit, maxAimDistance))
        {
            // Example: Change reticle color if hitting something within range
            aimReticle.color = Color.green;
            // Optional: Place a 3D marker GameObject at hit.point?
            // if (aimMarkerInstance != null) { aimMarkerInstance.transform.position = hit.point; aimMarkerInstance.SetActive(true); }
        }
        else
        {
            // Example: Change reticle color if aiming at sky or beyond max distance
            aimReticle.color = Color.white;
            // Optional: Hide 3D marker
            // if (aimMarkerInstance != null) { aimMarkerInstance.SetActive(false); }
        }
    }


    // --- Gizmos ---
    void OnDrawGizmosSelected() {
        // Draw ground check sphere in editor
        if (groundCheck != null) {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        // Draw aiming raycast direction (only in editor when aiming?)
        // Gizmos are drawn even when game not running, 'isAiming' won't work directly here.
        // Could draw from firstPersonCamPosition if it exists.
        if (mainCamera != null && firstPersonCamPosition != null)
        {
             // Visualize potential aim direction from the FP anchor
             Gizmos.color = Color.blue;
             Gizmos.DrawRay(firstPersonCamPosition.position, firstPersonCamPosition.forward * 5f);
        }
    }
}