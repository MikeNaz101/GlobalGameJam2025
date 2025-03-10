using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

public class PlayerStateManager : MonoBehaviour
{
    // Input System fields
    private PlayerInput playerInput; // Reference to the PlayerInput component
    private InputAction rightClickAction;

    public int maxHealth = 100;
    public int currentHealth;
    public int healthRecoveryRate = 2; // Health points per second
    public int maxMana = 100;
    public int manaCost = 10;
    public int currentMana;
    public int manaRecoveryRate = 5; // Mana points per second
    public int maxStamina = 100;
    public int currentStamina;
    public int staminaRecoveryRate = 10; // Stamina points per second
    public int maxSpeed = 15;
    public int currentSpeed = 10;
    private Vector3 position;
    //public ProgressBar healthBar;
    public GameObject projectilePrefab;  // projectile prefab here
    public GameObject shellPrefab;       // Visual-only projectile (dummy)
    public GameObject rWingPrefab;       // Right Wing
    public GameObject lWingPrefab;       // Left Wing
    public Transform firePoint;  // Point from which the projectile will fire
    public Transform lWingPoint;  // Point from which the Left Wing will spawn
    public Transform rWingPoint;  // Point from which the Right Wing will spawn

    public float bulletTypeChangeCooldown = 0.1f; // Minimum time between bullet type changes
    private float lastBulletTypeChangeTime = -1000f;
    public float orbitRadius = 2f;       // Distance from player
    public float orbitSpeed = 50f;       // Speed of rotation
    public int maxShells;            // Max floating shells
    public int currentShells;
    public List<GameObject> orbitingShells = new List<GameObject>();
    public BulletStateManager bulletManager;
    public PlayerBaseState currentState;
    //public float default_speed = 15;
    public bool isSneaking = false;
    [HideInInspector]
    public PlayerIdleState idleState = new PlayerIdleState();
    [HideInInspector]
    public PlayerWalkingState walkState = new PlayerWalkingState();
    [HideInInspector]
    public PlayerSneakState sneakState = new PlayerSneakState();
    [HideInInspector]
    //public PlayerAttackingState attackState = new PlayerAttackingState();
    //[HideInInspector]
    public PlayerHitState hitState = new PlayerHitState();
    [HideInInspector]
    public PlayerDeathState deathState = new PlayerDeathState();
    [HideInInspector]
    public PlayerFlyingState flyingState = new PlayerFlyingState();
    public Vector2 movement;
    [HideInInspector]
    public CharacterController controller;
    private bool canDoubleJump = false;
    public Transform groundCheck; // Assign this in the inspector
    public float groundCheckRadius = 0.2f; // Radius of the ground check
    public LayerMask groundLayer; // Set this to the layer(s) you consider as ground
    public bool isGrounded; // To track if the player is on the ground
    private bool hasJumped = false; // Track if the player has jumped
    public float jumpForce = 20f; // Adjustable jump force
    public float verticalVelocity = 0f; // Tracks falling speed
    public float gravity = -4.81f;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isGrounded = true;
        currentHealth = maxHealth;
        currentMana = maxMana;
        currentSpeed = maxSpeed;
        currentStamina = maxStamina;
        position = transform.position;
        maxShells = maxMana / manaCost;
        currentShells = maxShells;
        SpawnSpiritBubbleShells();
        // Subscribe to the BulletTypeChanged event
        if (bulletManager != null)
        {
            bulletManager.OnBulletTypeChanged.AddListener(UpdateShellColors);
            // Initial color update
            UpdateShellColors();
        }
        else
        {
            Debug.LogError("BulletStateManager.Instance is null!  Make sure the BulletStateManager is in the scene and initializes before PlayerStateManager.");
        }
        Debug.Log("Your currentHealth starts as: " + currentHealth);

        // Start recovery for all stats
        StartCoroutine(RecoverHealthOverTime());
        StartCoroutine(RecoverManaOverTime());
        StartCoroutine(RecoverStaminaOverTime());
        controller = GetComponent<CharacterController>();
        currentState = walkState;

        SwitchState(idleState);
    }
    private IEnumerator RecoverHealthOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f); // Wait 1 second between recovery
            currentHealth += healthRecoveryRate;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            Debug.Log("current Health"+ currentHealth.ToString());
        }
    }

    private IEnumerator RecoverManaOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f); // Wait 1 second between recovery
            currentMana += manaRecoveryRate;
            currentMana = Mathf.Clamp(currentMana, 0, maxMana);
            if (currentShells < (maxMana/manaCost) && currentShells < (currentMana/manaCost))
            {
                currentShells++;
                //this where spawn of bubble problem lives
                orbitingShells[currentShells - 1].GetComponent<MeshRenderer>().enabled = true;
            }
        }
    }

    private IEnumerator RecoverStaminaOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f); // Wait 1 second between recovery
            currentStamina += staminaRecoveryRate;
        }
    }

    // --- INPUT SYSTEM EVENT HANDLERS ---

    private void OnRightClick(InputAction.CallbackContext context)
    {
        // Find all active teleportation projectiles.
        TeleportBulletState[] projectiles = FindObjectsOfType<TeleportBulletState>();

        foreach (TeleportBulletState proj in projectiles)
        {
            proj.Teleport(); // Call Teleport() on *each* one.
        }
    }

    // IMPORTANT: Enable and disable the action to avoid errors.
    private void OnEnable()
    {
        if (rightClickAction != null)
            rightClickAction.Enable();
    }

    private void OnDisable()
    {
        if (rightClickAction != null)
            rightClickAction.Disable();
    }


    // Update is called once per frame
    void Update()
    {
        // Ground check using Sphere Cast
        isGrounded = controller.isGrounded;
        // Check for jump input

        if (Input.GetKeyDown(KeyCode.Space))
        {
            print("IsGrounded = "+ controller.isGrounded);
            if (isGrounded)
            {
                // First jump
                Jump();
            }
            else if (!hasJumped)
            {
                // Second jump (double jump) - enter flying state
                hasJumped = true;
                SwitchState(flyingState);
            }
        }
        isGrounded = controller.isGrounded;

        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; // Slight push to keep grounded state stable
        }

        // Apply gravity when airborne
        verticalVelocity += gravity * Time.deltaTime;
        /*
        // Move the character
        // Convert movement from 2D (input) to 3D
        Vector3 moveDirection = new Vector3(movement.x, 0, movement.y);

        // Move the character with gravity
        Vector3 finalMovement = (moveDirection * currentSpeed) + new Vector3(0, verticalVelocity, 0);
        controller.Move(finalMovement * Time.deltaTime);
        */
        currentState.UpdateState(this);
        UpdateShellPositions();
    }

    void OnMove(InputValue movVal)
    {
        movement = movVal.Get<Vector2>();
        print("I is move!");
    }

    void OnSprint()
    {
        isSneaking = !isSneaking;
    }
    public void MovePlayer(float speed)
    {
        float moveX = movement.x;
        float moveY = movement.y;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 moveDirection = (transform.right * moveX) + (transform.forward * moveY);
        Vector3 finalMovement = (moveDirection * currentSpeed) + new Vector3(0, verticalVelocity, 0);
        //Vector3 actual_movement = new Vector3(moveX, 0, moveY);
        controller.Move(finalMovement.normalized * speed*Time.deltaTime);
    }
    public void Jump()
    {
        if (isGrounded) // Ensure the player is on the ground before jumping
        {
            hasJumped = false; // Reset double jump tracking
        }

        // Apply upward force for jumping
        Vector3 jumpVelocity = new Vector3(0, jumpForce, 0);
        controller.Move(jumpVelocity * Time.deltaTime);

        Debug.Log("Player Jumped!");
    }

    public void UseMana(int mCost, PlayerStateManager player)
    {
        player.currentMana -= mCost;
        player.currentMana = Mathf.Clamp(player.currentMana, 0, player.maxMana);
    }
    public void TakeDamage(int damage)
    {
        // Check if the player is alive
        if (currentHealth > 0) 
        {
            // Pass damage to the hit state and switch to it
            hitState.SetDamage(damage); 
            SwitchState(hitState);
        }
    }


    // Makes the spirit bubbles orbit around player.
    void SpawnSpiritBubbleShells()
    {
        for (int i = 0; i < maxShells; i++)
        {
            float angle = i * (360f / maxShells); // Distribute shells evenly
            float x = transform.position.x + orbitRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
            float z = transform.position.z + orbitRadius * Mathf.Sin(angle * Mathf.Deg2Rad);

            Vector3 spawnPosition = new Vector3(x, transform.position.y, z);
            GameObject shell = Instantiate(shellPrefab, spawnPosition, Quaternion.identity);
            shell.transform.parent = transform; // Make sure shells follow the player
            orbitingShells.Add(shell);
        }
    }
    void UpdateShellPositions()
    {
        for (int i = 0; i < orbitingShells.Count; i++)
        {
            if (orbitingShells[i] != null)
            {
                float angle = Time.time * orbitSpeed + (i * 360f / maxShells);
                float orbitHeight = Time.time * orbitSpeed + (i * 360f / maxShells);
                float x = firePoint.position.x + orbitRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
                float z = firePoint.position.z + orbitRadius * Mathf.Sin(angle * Mathf.Deg2Rad);
                orbitingShells[i].transform.position = new Vector3(x, transform.position.y, z);
            }
        }
    }
    // This method is called whenever the BulletStateManager's OnBulletTypeChanged event is fired.
    void UpdateShellColors()
    {
        if (BulletStateManager.Instance == null) return; // Safety check

        Color newColor = BulletStateManager.Instance.GetCurrentBulletColor();
        foreach (GameObject shell in orbitingShells)
        {
            if (shell != null)
            {
                shell.GetComponent<ShellColorController>().SetColor(newColor);
            }
        }
    }

    public void SwitchState(PlayerBaseState newState)
    {
        currentState?.ExitState(this); // Calls ExitState only if it exists
        currentState = newState;
        currentState.EnterState(this);
    }

    private void OnDestroy()
    {
        // Unsubscribe from the event when this object is destroyed to prevent memory leaks.
        if (BulletStateManager.Instance != null)
        {
            BulletStateManager.Instance.OnBulletTypeChanged.RemoveListener(UpdateShellColors);
        }
    }
}
