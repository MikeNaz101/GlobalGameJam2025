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

    private const string JUMP_SPEED_PARAM = "JumpSpeed"; // Playback speed of the jump states
    private const string JUMP_TAG = "Jump";              // Tag on the Jumping and RunningJump states
    private static readonly int JumpingState = Animator.StringToHash("Jumping");
    private static readonly int RunningJumpState = Animator.StringToHash("RunningJump");

    [Header("Jump Animation")]
    [Tooltip("Use the running-jump animation when moving at least this fraction of the way from walk speed to run speed.")]
    [Range(0f, 1f)] public float runningJumpSpeedThreshold = 0.5f;
    [Tooltip("Blend time into a jump animation (seconds).")]
    public float jumpBlendTime = 0.08f;
    [Tooltip("Standing jump clip: when the feet leave the ground and when they land, as a fraction of the clip (0-1).")]
    public Vector2 standingJumpAirWindow = new Vector2(0.06f, 0.68f);
    [Tooltip("Running jump clip: when the feet leave the ground and when they land, as a fraction of the clip (0-1).")]
    public Vector2 runningJumpAirWindow = new Vector2(0f, 0.8f);
    [Tooltip("Play the falling pose after being in the air this long without jumping (walking off a ledge).")]
    public float fallAnimationDelay = 0.3f;

    [Header("Jump Root Motion Fix")]
    [Tooltip("The jump clips move the whole skeleton (up, and forward for the running jump). The game already moves the " +
             "character, so that movement is removed while a jump animation plays. Turn off to see the raw clips.")]
    public bool removeJumpClipMovement = true;
    [Tooltip("Name of the skeleton's root object that the clips move.")]
    public string skeletonRootName = "metarig";

    private Transform skeletonRoot;
    private Vector3 skeletonBaseline;     // Where the skeleton root sits during normal movement
    private float jumpLockWeight;         // 0 = raw animation, 1 = jump clip movement removed
    private float airborneTime;
    private bool jumpAnimationActive;     // A jump/fall animation was started and hasn't landed yet
    private Vector2 activeAirWindow;
    private float activeAirSpeed = 1f;

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

        skeletonRoot = FindDeep(transform, skeletonRootName);
        if (skeletonRoot != null) skeletonBaseline = skeletonRoot.localPosition;
    }

    private static Transform FindDeep(Transform parent, string childName)
    {
        if (string.IsNullOrEmpty(childName)) return null;
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform found = FindDeep(child, childName);
            if (found != null) return found;
        }
        return null;
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

        // --- Grounded Check (from the player's own ground logic, so it matches the jump physics) ---
        bool airborne = playerStateManager.IsAirborne;
        animator.SetBool(IS_GROUNDED_PARAM, !airborne);
        UpdateJumpAnimation(airborne);

        // --- Movement ---
        // Check if there's significant movement input
        bool isMoving = playerStateManager.movement.magnitude > 0.1f; // Use a small threshold
        // Running means actually sprinting, not just holding the run key (e.g. out of stamina = walking)
        bool isRunning = playerStateManager.IsSprinting && isMoving;
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
        if (animator == null) return;

        // Pick the clip from how fast the player is actually moving
        float speed = playerStateManager.currentHorizontalVelocity.magnitude;
        float threshold = Mathf.Lerp(playerStateManager.walkSpeed, playerStateManager.runSpeed, runningJumpSpeedThreshold);
        bool running = speed >= threshold;

        StartJumpAnimation(running ? RunningJumpState : JumpingState,
                           running ? runningJumpAirWindow : standingJumpAirWindow,
                           startAtTakeoff: true);
    }

    // The jump is driven from code instead of the "jump" trigger, so the animation always starts on the
    // frame the character leaves the ground and is timed to how long the jump really lasts.
    private void StartJumpAnimation(int stateHash, Vector2 airWindow, bool startAtTakeoff)
    {
        activeAirWindow = airWindow;
        jumpAnimationActive = true;
        animator.ResetTrigger(JUMP_TRIGGER);
        animator.SetBool(IS_GROUNDED_PARAM, false);

        if (startAtTakeoff)
        {
            // Stretch or squeeze the airborne part of the clip to fit the real air time.
            // The clip length isn't known until the state is playing, so start at speed 1 and correct in Update.
            activeAirSpeed = -1f;
            animator.SetFloat(JUMP_SPEED_PARAM, 1f);
            animator.CrossFadeInFixedTime(stateHash, jumpBlendTime, 0, airWindow.x); // Offset in seconds; the clips are about 1s long
        }
        else
        {
            // Falling without a jump: go straight to the "coming down" part of the clip and hold there
            activeAirSpeed = 0f;
            animator.SetFloat(JUMP_SPEED_PARAM, 0f);
            animator.CrossFade(stateHash, 0.2f, 0, Mathf.Max(airWindow.x, airWindow.y - 0.1f));
        }
    }

    private void UpdateJumpAnimation(bool airborne)
    {
        airborneTime = airborne ? airborneTime + Time.deltaTime : 0f;

        // Is a jump-tagged state playing (or being blended into)?
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        bool inTransition = animator.IsInTransition(0);
        AnimatorStateInfo next = inTransition ? animator.GetNextAnimatorStateInfo(0) : current;
        bool nextIsJump = inTransition && next.IsTag(JUMP_TAG);
        bool currentIsJump = current.IsTag(JUMP_TAG) && !(inTransition && !nextIsJump); // Not while blending out
        bool jumpStatePlaying = nextIsJump || currentIsJump;
        AnimatorStateInfo jumpInfo = nextIsJump ? next : current;

        // Walked off a ledge: show the falling pose
        if (airborne && !jumpAnimationActive && !jumpStatePlaying && airborneTime >= fallAnimationDelay
            && playerStateManager.verticalVelocity < 0f)
        {
            StartJumpAnimation(JumpingState, standingJumpAirWindow, startAtTakeoff: false);
            return;
        }

        if (!jumpStatePlaying)
        {
            // Landed and the controller has moved on (or the jump was interrupted by a hit/attack)
            if (!airborne) jumpAnimationActive = false;
            return;
        }

        float progress = jumpInfo.normalizedTime;
        float clipLength = jumpInfo.length; // Seconds at the current playback speed
        float speed;

        if (airborne)
        {
            if (activeAirSpeed < 0f)
            {
                // First frame in the state: work out the speed that makes the airborne part last the whole jump
                float currentSpeed = Mathf.Max(0.01f, animator.GetFloat(JUMP_SPEED_PARAM));
                float fullLength = clipLength * currentSpeed; // Length at speed 1
                float airSeconds = fullLength * Mathf.Max(0.05f, activeAirWindow.y - activeAirWindow.x);
                activeAirSpeed = Mathf.Clamp(airSeconds / Mathf.Max(0.1f, playerStateManager.PredictedJumpAirTime), 0.3f, 3f);
            }

            // Still in the air when the clip reaches its landing: hold the coming-down pose until we land
            float holdPoint = activeAirWindow.y - 0.06f;
            speed = progress >= holdPoint ? 0f : Mathf.Max(activeAirSpeed, 0.3f);
        }
        else
        {
            // On the ground: hurry to the landing part if we landed early, then play the landing normally
            speed = progress < activeAirWindow.y - 0.02f ? 3f : 1f;
        }

        animator.SetFloat(JUMP_SPEED_PARAM, speed);
    }

    // Runs after the Animator has posed the skeleton for this frame
    void LateUpdate()
    {
        if (skeletonRoot == null || animator == null) return;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        bool inTransition = animator.IsInTransition(0);
        bool jumpPlaying = current.IsTag(JUMP_TAG) || (inTransition && animator.GetNextAnimatorStateInfo(0).IsTag(JUMP_TAG));

        Vector3 animated = skeletonRoot.localPosition;
        bool wantLock = removeJumpClipMovement && jumpPlaying;
        jumpLockWeight = Mathf.MoveTowards(jumpLockWeight, wantLock ? 1f : 0f, Time.deltaTime * (wantLock ? 14f : 6f));

        if (!jumpPlaying && jumpLockWeight <= 0f)
        {
            // Normal movement: remember where the skeleton sits so jumps can stay anchored there
            skeletonBaseline = Vector3.Lerp(skeletonBaseline, animated, Time.deltaTime * 6f);
            return;
        }

        if (jumpLockWeight <= 0f) return;

        // Keep the clip's crouch (below the baseline) but not its rise or its forward/sideways travel:
        // the CharacterController already moves the player through the air.
        Vector3 locked = new Vector3(skeletonBaseline.x, Mathf.Min(animated.y, skeletonBaseline.y), skeletonBaseline.z);
        skeletonRoot.localPosition = Vector3.Lerp(animated, locked, jumpLockWeight);
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