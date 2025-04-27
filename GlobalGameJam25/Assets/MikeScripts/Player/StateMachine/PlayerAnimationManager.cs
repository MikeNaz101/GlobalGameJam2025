using UnityEngine;

[RequireComponent(typeof(Animator))] // Ensures an Animator component exists
public class PlayerAnimationManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the PlayerStateManager script.")]
    [SerializeField] private PlayerStateManager playerStateManager;
    [SerializeField] private CharacterController characterController; // Add reference to CharacterController
    private Animator animator;

    // Animator Parameter Names (Use const for safety against typos)
    private const string IS_WALKING_PARAM = "isWalking";
    private const string IS_RUNNING_PARAM = "isRunning";
    private const string IS_DEAD_PARAM = "isDead";
    private const string IS_GROUNDED_PARAM = "isGrounded"; // Added for landing/falling transitions
    private const string HIT_TRIGGER = "hit";
    private const string CELEBRATE_TRIGGER = "celebrate";
    private const string JUMP_TRIGGER = "jump";
    private const string SHOOT_TRIGGER = "shoot";
    private const string TELEPORTED_TRIGGER = "teleported";

    // Optional: Smoothing for movement parameters
    private float smoothSpeed = 0f; // Used to smooth horizontal speed changes
    private const string SPEED_PARAM = "Speed"; // Optional float parameter for blend trees

    void Awake()
    {
        characterController = GetComponent<CharacterController>(); // Get the CharacterController
        animator = GetComponent<Animator>();

        // Attempt to find PlayerStateManager if not assigned in Inspector
        if (playerStateManager == null)
        {
            playerStateManager = GetComponentInParent<PlayerStateManager>();
        }

        if (playerStateManager == null)
        {
            Debug.LogError("PlayerAnimationManager: PlayerStateManager not found!", this);
            this.enabled = false; // Disable script if state manager is missing
            return;
        }
    }

     void OnEnable()
     {
        // Subscribe to the Area Cleared event (if it exists - requires modification in AreaCleansingManager)
        AreaCleansingManager.OnAreaCleared += HandleAreaCleared;
     }

     void OnDisable()
     {
        // Unsubscribe when the object is disabled or destroyed
        AreaCleansingManager.OnAreaCleared -= HandleAreaCleared;
     }

    void Update()
    {
        if (playerStateManager == null || animator == null) return; // Exit if essential components are missing

        // --- Death Check (Overrides everything else) ---
        bool isDead = playerStateManager.currentHealth <= 0;
        animator.SetBool(IS_DEAD_PARAM, isDead);
        if (isDead)
        {
            // If dead, potentially reset other movement flags to avoid conflicting animations
            animator.SetBool(IS_WALKING_PARAM, false);
            animator.SetBool(IS_RUNNING_PARAM, false);
            animator.SetFloat(SPEED_PARAM, 0f); // Reset speed if using blend tree
            return; // Stop further animation updates if dead
        }

        // --- Grounded Check (Directly from CharacterController) ---
        animator.SetBool(IS_GROUNDED_PARAM, characterController.isGrounded);

        // --- Movement ---
        // Check if there's significant movement input
        bool isMoving = playerStateManager.movement.magnitude > 0.1f; // Use a small threshold
        bool isRunning = playerStateManager.isRunning && isMoving; // Check run flag AND movement
        bool isWalking = isMoving && !isRunning; // Walking if moving but not running

        animator.SetBool(IS_WALKING_PARAM, isWalking);
        animator.SetBool(IS_RUNNING_PARAM, isRunning);

        // --- Optional: Speed Parameter for Blend Trees ---
        // Calculate target speed based on state for smoother transitions in blend trees
        float targetSpeed = 0f;
        if (isRunning) targetSpeed = 1.0f; // Normalized speed for running
        else if (isWalking) targetSpeed = 0.5f; // Normalized speed for walking
        // Add other states like sneaking if needed
        // else if (playerStateManager.isSneaking && isMoving) targetSpeed = 0.2f;

        // Smoothly interpolate the speed parameter
        smoothSpeed = Mathf.Lerp(smoothSpeed, targetSpeed, Time.deltaTime * 10f); // Adjust smoothing factor (10f) as needed
        animator.SetFloat(SPEED_PARAM, smoothSpeed);

    }

    // --- Public Methods to Trigger Animations ---

    public void TriggerJump()
    {
        if (animator != null)
        {
            Debug.Log("Animation Manager: Triggering Jump Animation");
            animator.SetTrigger(JUMP_TRIGGER);
        }
    }

    public void TriggerShoot()
    {
        if (animator != null)
        {
             Debug.Log("Animation Manager: Triggering Shoot Animation");
            animator.SetTrigger(SHOOT_TRIGGER);
        }
    }

    public void TriggerHit()
    {
        if (animator != null && playerStateManager.currentHealth > 0) // Don't trigger hit if already dead
        {
             Debug.Log("Animation Manager: Triggering Hit Animation");
            animator.SetTrigger(HIT_TRIGGER);
        }
    }

    public void TriggerTeleported()
    {
        if (animator != null)
        {
             Debug.Log("Animation Manager: Triggering Teleported (Landing) Animation");
            animator.SetTrigger(TELEPORTED_TRIGGER);
        }
    }

    public void TriggerCelebrate()
    {
        if (animator != null && playerStateManager.currentHealth > 0) // Don't celebrate if dead
        {
             Debug.Log("Animation Manager: Triggering Celebrate Animation");
            animator.SetTrigger(CELEBRATE_TRIGGER);
        }
    }

     // --- Event Handlers ---

     private void HandleAreaCleared(AreaCleansingManager clearedArea) // Method signature matches the event
     {
         // You could add checks here if only specific areas trigger celebration,
         // but for now, any cleared area triggers it.
         Debug.Log($"Area {clearedArea.gameObject.name} cleared, triggering celebration.");
         TriggerCelebrate();
     }
}