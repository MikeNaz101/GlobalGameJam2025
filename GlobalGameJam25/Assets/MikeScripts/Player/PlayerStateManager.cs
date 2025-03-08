using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

public class PlayerStateManager : MonoBehaviour
{
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
    public int maxSpeed = 10;
    public int currentSpeed = 5;
    private Vector3 position;
    //public ProgressBar healthBar;
    public GameObject projectilePrefab;  // projectile prefab here
    public GameObject shellPrefab;       // Visual-only projectile (dummy)
    public GameObject rWingPrefab;       // Right Wing
    public GameObject lWingPrefab;       // Left Wing
    public Transform firePoint;  // Point from which the projectile will fire
    public Transform lWingPoint;  // Point from which the Left Wing will spawn
    public Transform rWingPoint;  // Point from which the Right Wing will spawn

    public float orbitRadius = 2f;       // Distance from player
    public float orbitSpeed = 50f;       // Speed of rotation
    public int maxShells;            // Max floating shells
    public int currentShells;
    public List<GameObject> orbitingShells = new List<GameObject>();
    public PlayerBaseState currentState;
    public float default_speed = 100;
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
    private bool isGrounded; // To track if the player is on the ground
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

        // Move the character
        // Convert movement from 2D (input) to 3D
        Vector3 moveDirection = new Vector3(movement.x, 0, movement.y);

        // Move the character with gravity
        Vector3 finalMovement = (moveDirection * currentSpeed) + new Vector3(0, verticalVelocity, 0);
        controller.Move(finalMovement * Time.deltaTime);
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

        Vector3 moveDirection = (transform.right * moveX) + (transform.forward * moveY);
        //Vector3 actual_movement = new Vector3(moveX, 0, moveY);
        controller.Move(moveDirection.normalized * speed*Time.deltaTime);
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
    void OnAttack()
    {
        if (Input.GetMouseButtonDown((int)MouseButton.LeftMouse) && currentMana > manaCost)
        {
            if (currentShells > 0)
            {
                orbitingShells[currentShells - 1].GetComponent<MeshRenderer>().enabled = false;
                currentShells -= 1;
                FireProjectile(this);
                UseMana(manaCost, this);
            }
        }
    }
    void FireProjectile(PlayerStateManager player)
    {
        // Instantiate the projectile at the firePoint
        if (player.projectilePrefab != null && player.firePoint != null)
        {
            GameObject projectile = GameObject.Instantiate(player.projectilePrefab, player.firePoint.position, player.firePoint.rotation);
        }
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

    public void SwitchState(PlayerBaseState newState)
    {
        currentState?.ExitState(this); // Calls ExitState only if it exists
        currentState = newState;
        currentState.EnterState(this);
    }
}
