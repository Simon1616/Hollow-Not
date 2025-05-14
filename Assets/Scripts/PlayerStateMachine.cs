using UnityEngine;
using System.Collections.Generic;

// Base class for all player states
public abstract class PlayerBaseState
{
    protected PlayerStateMachine stateMachine;

    public PlayerBaseState(PlayerStateMachine stateMachine)
    {
        this.stateMachine = stateMachine;
    }

    public abstract void Enter();
    public abstract void Tick(float deltaTime);
    public abstract void Exit();
}

// The main state machine component
public class PlayerStateMachine : MonoBehaviour
{
    private bool wasGroundedLastFrame = true;

    // --- Coyote time (grounded grace period) --- 
    private float jumpGroundedGraceTimer = 0f; // Initialize to 0 instead of 3
    private const float jumpGroundedGraceDuration = 0.10f; // 0.1 seconds of grace after jumping
    [field: SerializeField] public float MoveSpeed { get; private set; } = 5f;
    [field: SerializeField] public float AirControlSpeed { get; private set; } = 3f; // Separate air control speed
    [field: SerializeField] public float WallJumpForce { get; private set; } = 7.5f;
    [field: SerializeField] public int MaxJumps { get; private set; } = 1;
    public int JumpsRemaining { get; set; }

    [Header("Collider Settings")]
    [SerializeField] public CapsuleCollider2D playerCollider; // Changed to public
    [SerializeField] private Vector2 standingColliderSize = new Vector2(1f, 2f); // Example
    [SerializeField] private Vector2 standingColliderOffset = new Vector2(0f, 0f); // Example
    [SerializeField] private Vector2 crouchingColliderSize = new Vector2(1f, 1f); // Example
    [SerializeField] private Vector2 crouchingColliderOffset = new Vector2(0f, -0.5f); // Example
    [SerializeField] private float standUpCheckDistance = 0.1f; // Distance above collider to check
    [SerializeField] public LayerMask groundLayer; // Changed to public

    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private float groundCheckOffset = 0.1f; // Offset from feet for ground check

    [Header("Wall Check Settings")]
    [SerializeField] private float wallCheckDistance = 0.04f;
    [SerializeField] private float wallCheckOffset = 0.1f; // Offset from center for wall check

    [Header("Movement Deceleration")]
    [SerializeField] private float groundedDecelerationX = 20f;
    [SerializeField] private float airborneDecelerationX = 10f;
    [SerializeField] private float groundedDecelerationY = 30f;
    [SerializeField] private float airborneDecelerationY = 15f;
    [SerializeField] private float airborneDecelerationYDown = 10f; // Lower deceleration when falling

    [Header("Max Speeds")]
    [SerializeField] private float maxHorizontalSpeed = 10f;
    [SerializeField] private float maxVerticalSpeed = 15f;
    [Tooltip("The maximum falling speed. Higher values = faster falls, but may cause collision issues.")]
    [SerializeField] public float maxFallSpeed = 17f; // Made public to be accessible from FallState

    [Header("Jump Settings")]
    [SerializeField] private float jumpCooldown = 0.67f; // 2/3 of a second
    [field: SerializeField] public float JumpForce { get; private set; } = 10f; // Single jump force
    [field: SerializeField] public float WallJumpAngle { get; private set; } = 30f; // Wall jump angle in degrees (was 45 degrees)
    private float lastJumpTime = 0f;

    [Header("Dash Settings")]
    [field: SerializeField] public float DashSpeed { get; private set; } = 20f;
    [field: SerializeField] public float DashDuration { get; private set; } = 0.2f;
    [field: SerializeField] public float DashCooldown { get; private set; } = 0.45f;
    [field: SerializeField] public bool AllowAirDash { get; private set; } = true;
    [field: SerializeField] public int MaxAirDashes { get; private set; } = 1;
    private float lastDashTime = 0f;
    private int airDashesRemaining = 0;
    private bool isDashing = false;
    private float dashEndTime = 0f;

    // States
    public PlayerIdleState IdleState { get; private set; }
    public WalkState WalkState { get; private set; }
    public RunState RunState { get; private set; }
    public JumpState JumpState { get; private set; }
    public SlideState SlideState { get; private set; }
    public WallClingState WallClingState { get; private set; }
    public ShootState ShootState { get; private set; }
    public FallState FallState { get; private set; }
    public PlayerDashState DashState { get; private set; }

    // Component References (Example)
    public Rigidbody2D RB { get; private set; }
    public Animator Animator { get; private set; }
    // Add InputReader reference if using one

    // State transition event
    public delegate void StateChangedEvent(PlayerBaseState fromState, PlayerBaseState toState);
    public event StateChangedEvent OnStateChanged;

    // State duration tracking
    private float stateEnterTime;
    public float GetStateDuration()
    {
        return Time.time - stateEnterTime;
    }

    // InputReader abstraction (now a separate class)
    public InputReader InputReader { get; private set; } // Public property for states to access

    private float lastDirectionChangeTime = 0f;
    private const float DIRECTION_CHANGE_DELAY = 0.1f; // 1/10 of a second delay

    public bool IsFacingRight { get; private set; } = true;
    public PlayerBaseState CurrentState { get; private set; }

    private PlayerBaseState currentState;

    // State registry for extensibility
    private Dictionary<string, PlayerBaseState> stateRegistry = new Dictionary<string, PlayerBaseState>();

    private bool warnedAboutGroundCheck = false;

    // Cached HashSets for checking animator parameters to avoid repeated lookups
    private HashSet<string> existingAnimatorParameters;
    private HashSet<string> nonExistingAnimatorParameters;
    
    // Safely set an animator parameter (float) if it exists
    public void SafeSetAnimatorFloat(string paramName, float value)
    {
        if (Animator == null) return;
        
        // Initialize sets if needed
        if (existingAnimatorParameters == null)
            existingAnimatorParameters = new HashSet<string>();
            
        if (nonExistingAnimatorParameters == null)
            nonExistingAnimatorParameters = new HashSet<string>();
            
        // If we already know this parameter exists, set it
        if (existingAnimatorParameters.Contains(paramName))
        {
            Animator.SetFloat(paramName, value);
            return;
        }
        
        // If we already know this parameter doesn't exist, skip
        if (nonExistingAnimatorParameters.Contains(paramName))
            return;
            
        // First time checking this parameter
        bool paramExists = false;
        foreach (AnimatorControllerParameter param in Animator.parameters)
        {
            if (param.name == paramName)
            {
                paramExists = true;
                break;
            }
        }
        
        if (paramExists)
        {
            existingAnimatorParameters.Add(paramName);
            Animator.SetFloat(paramName, value);
        }
        else
        {
            nonExistingAnimatorParameters.Add(paramName);
            
            // Only log the warning once
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"Animator parameter '{paramName}' not found. This warning will only appear once.");
            #endif
        }
    }
    
    // Safely set an animator parameter (bool) if it exists
    public void SafeSetAnimatorBool(string paramName, bool value)
    {
        if (Animator == null) return;
        
        // Initialize sets if needed
        if (existingAnimatorParameters == null)
            existingAnimatorParameters = new HashSet<string>();
            
        if (nonExistingAnimatorParameters == null)
            nonExistingAnimatorParameters = new HashSet<string>();
            
        // If we already know this parameter exists, set it
        if (existingAnimatorParameters.Contains(paramName))
        {
            Animator.SetBool(paramName, value);
            return;
        }
        
        // If we already know this parameter doesn't exist, skip
        if (nonExistingAnimatorParameters.Contains(paramName))
            return;
            
        // First time checking this parameter
        bool paramExists = false;
        foreach (AnimatorControllerParameter param in Animator.parameters)
        {
            if (param.name == paramName)
            {
                paramExists = true;
                break;
            }
        }
        
        if (paramExists)
        {
            existingAnimatorParameters.Add(paramName);
            Animator.SetBool(paramName, value);
        }
        else
        {
            nonExistingAnimatorParameters.Add(paramName);
            
            // Only log the warning once
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"Animator parameter '{paramName}' not found. This warning will only appear once.");
            #endif
        }
    }
    
    // Safely set an animator parameter (trigger) if it exists
    public void SafeSetAnimatorTrigger(string paramName)
    {
        if (Animator == null) return;
        
        // Initialize sets if needed
        if (existingAnimatorParameters == null)
            existingAnimatorParameters = new HashSet<string>();
            
        if (nonExistingAnimatorParameters == null)
            nonExistingAnimatorParameters = new HashSet<string>();
            
        // If we already know this parameter exists, set it
        if (existingAnimatorParameters.Contains(paramName))
        {
            Animator.SetTrigger(paramName);
            return;
        }
        
        // If we already know this parameter doesn't exist, skip
        if (nonExistingAnimatorParameters.Contains(paramName))
            return;
            
        // First time checking this parameter
        bool paramExists = false;
        foreach (AnimatorControllerParameter param in Animator.parameters)
        {
            if (param.name == paramName)
            {
                paramExists = true;
                break;
            }
        }
        
        if (paramExists)
        {
            existingAnimatorParameters.Add(paramName);
            Animator.SetTrigger(paramName);
        }
        else
        {
            nonExistingAnimatorParameters.Add(paramName);
            
            // Only log the warning once
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"Animator parameter '{paramName}' not found. This warning will only appear once.");
            #endif
        }
    }

    // Safely play an animation if it exists
    public void SafePlayAnimation(string animationName)
    {
        if (Animator == null) return;
        
        // We'll attempt to play the animation but catch any errors
        // This avoids the console spam from missing animations
        try
        {
            Animator.Play(animationName);
        }
        catch (System.Exception)
        {
            // Only log the first time an animation is not found
            if (nonExistingAnimatorParameters == null)
                nonExistingAnimatorParameters = new HashSet<string>();
                
            if (!nonExistingAnimatorParameters.Contains(animationName))
            {
                nonExistingAnimatorParameters.Add(animationName);
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"Animation clip '{animationName}' not found. This warning will only appear once.");
                #endif
            }
        }
    }

    private void Awake()
    {
        // Get Components - do this first
        RB = GetComponent<Rigidbody2D>();
        if (RB == null)
        {
            Debug.LogError("Rigidbody2D component not found on the GameObject!", this);
            RB = gameObject.AddComponent<Rigidbody2D>();
            Debug.Log("Added a Rigidbody2D component automatically.");
        }
        
        // Initialize input reader before state creation
        InputReader = new InputReader();
        if (InputReader == null)
        {
            Debug.LogError("Failed to initialize InputReader!", this);
        }
        
        Animator = GetComponentInChildren<Animator>();
        if (Animator == null)
        {
            Debug.LogWarning("Animator component not found in children. Animations will not play.", this);
        }
        
        // Check for necessary colliders/check points
        if (playerCollider == null)
        {
            playerCollider = GetComponent<CapsuleCollider2D>();
            if (playerCollider == null)
            {
                playerCollider = gameObject.AddComponent<CapsuleCollider2D>();
                Debug.LogWarning("No CapsuleCollider2D found, adding one automatically.", this);
            }
            
            // Store initial size/offset
            if (standingColliderSize == Vector2.zero) standingColliderSize = playerCollider.size;
            if (standingColliderOffset == Vector2.zero) playerCollider.offset = Vector2.zero;
        }
        
        // Find ground check point if not assigned
        if (groundCheckPoint == null)
        {
            Transform checkPoint = transform.Find("GroundCheck");
            if (checkPoint != null)
            {
                groundCheckPoint = checkPoint;
                Debug.Log("Found and assigned GroundCheck automatically.");
            }
            else
            {
                // Create a ground check point if it doesn't exist
                GameObject groundCheck = new GameObject("GroundCheck");
                groundCheck.transform.parent = transform;
                groundCheck.transform.localPosition = new Vector3(0, -playerCollider.bounds.extents.y, 0);
                groundCheckPoint = groundCheck.transform;
                Debug.Log("Created and assigned GroundCheck automatically.");
            }
        }
        
        // Initialize state machine
        InitializeStateMachine();
    }
    
    private void InitializeStateMachine()
    {
        try
        {
            // Create state registry
            stateRegistry = new Dictionary<string, PlayerBaseState>();
            
            // Initialize all states
            IdleState = new PlayerIdleState(this);
            WalkState = new WalkState(this);
            RunState = new RunState(this);
            JumpState = new JumpState(this);
            FallState = new FallState(this);
            WallClingState = new WallClingState(this);
            ShootState = new ShootState(this);
            SlideState = new SlideState(this);
            DashState = new PlayerDashState(this);
            
            // Check if all states were initialized successfully
            if (IdleState == null || WalkState == null || RunState == null || JumpState == null || 
                FallState == null || WallClingState == null || ShootState == null || SlideState == null || 
                DashState == null)
            {
                Debug.LogError("One or more states failed to initialize!");
                return;
            }
            
            // Register states
            stateRegistry[nameof(PlayerIdleState)] = IdleState;
            stateRegistry[nameof(WalkState)] = WalkState;
            stateRegistry[nameof(RunState)] = RunState;
            stateRegistry[nameof(JumpState)] = JumpState;
            stateRegistry[nameof(FallState)] = FallState;
            stateRegistry[nameof(WallClingState)] = WallClingState;
            stateRegistry[nameof(ShootState)] = ShootState;
            stateRegistry[nameof(SlideState)] = SlideState;
            stateRegistry[nameof(PlayerDashState)] = DashState;
            
            // Set default values
            currentState = IdleState;
            CurrentState = IdleState;
            JumpsRemaining = MaxJumps;
            airDashesRemaining = MaxAirDashes;
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("State machine initialized successfully!");
            #endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize state machine: {e.Message}");
        }
    }

    private void Start()
    {
        // Make sure currentState is initialized and entered
        if (currentState != null)
        {
            currentState.Enter();
        }
        else if (IdleState != null)
        {
            // Set the initial state if not already set
            SwitchState(IdleState); // Start in Idle state
        }
        else
        {
            Debug.LogError("Cannot initialize player state. Both currentState and IdleState are null!", this);
        }
        
        // Initialize jump and dash counts
        JumpsRemaining = MaxJumps;
        airDashesRemaining = MaxAirDashes;
        
        // Configure physics settings to prevent falling through colliders
        if (RB != null)
        {
            // Increase collision detection for fast-moving objects
            RB.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    private void FixedUpdate()
    {
        // Set up enhanced collision detection for high speeds
        if (RB != null)
        {
            // Always use continuous collision detection for the player
            RB.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            
            // Increase physics iterations for fast falls to prevent tunneling
            float fallSpeed = Mathf.Abs(RB.linearVelocity.y);
            if (fallSpeed > 10f)
            {
                // Scale physics iterations based on fall speed
                int iterations = Mathf.Clamp(Mathf.FloorToInt(fallSpeed / 5f) + 8, 8, 16);
                Physics2D.velocityIterations = iterations;
                Physics2D.positionIterations = iterations;
                
                // Emergency raycast for extreme speeds
                if (fallSpeed > maxFallSpeed * 0.88f) // Set threshold to 88% of max fall speed
                {
                    // Get position for ground check
                    Vector2 checkPosition = transform.position;
                    if (groundCheckPoint != null)
                    {
                        checkPosition = groundCheckPoint.position;
                    }
                    
                    // Cast multiple rays in a pattern calibrated for precise detection
                    float rayLength = 0.4f; // Slightly shorter than before
                    float spread = 0.12f; // Reduced spread for more precision
                    
                    RaycastHit2D[] hits = new RaycastHit2D[3];
                    hits[0] = Physics2D.Raycast(checkPosition + new Vector2(-spread, 0), Vector2.down, rayLength, groundLayer);
                    hits[1] = Physics2D.Raycast(checkPosition, Vector2.down, rayLength, groundLayer);
                    hits[2] = Physics2D.Raycast(checkPosition + new Vector2(spread, 0), Vector2.down, rayLength, groundLayer);
                    
                    // Visualize the emergency raycasts only in editor
                    #if UNITY_EDITOR
                    Debug.DrawRay(checkPosition + new Vector2(-spread, 0), Vector2.down * rayLength, hits[0].collider != null ? Color.yellow : Color.red, 0.05f);
                    Debug.DrawRay(checkPosition, Vector2.down * rayLength, hits[1].collider != null ? Color.yellow : Color.red, 0.05f);
                    Debug.DrawRay(checkPosition + new Vector2(spread, 0), Vector2.down * rayLength, hits[2].collider != null ? Color.yellow : Color.red, 0.05f);
                    #endif
                    
                    // Find the closest hit point
                    float closestDist = float.MaxValue;
                    Vector2 hitPoint = Vector2.zero;
                    bool hitDetected = false;
                    
                    foreach (var hit in hits)
                    {
                        if (hit.collider != null && hit.distance < closestDist)
                        {
                            closestDist = hit.distance;
                            hitPoint = hit.point;
                            hitDetected = true;
                        }
                    }
                    
                    // Precise correction only when necessary
                    if (hitDetected && closestDist < 0.25f) // Less aggressive trigger distance
                    {
                        // Calculate exact height the player should be at
                        float yOffset = 0.01f; // Minimal offset - just enough to prevent interpenetration
                        float targetY = hitPoint.y + yOffset;
                        
                        // Add the player's collider height only if we have a collider
                        if (playerCollider != null)
                        {
                            targetY += playerCollider.bounds.extents.y;
                        }
                        
                        // Only actually move the player if we're very close to collision
                        if (closestDist < 0.1f || fallSpeed > 20f)
                        {
                            // Move the player precisely to the calculated position
                            transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
                            
                            // Reduce velocity but maintain some of the momentum
                            float dampedVelocity = -Mathf.Min(5f, fallSpeed * 0.3f);
                            RB.linearVelocity = new Vector2(RB.linearVelocity.x, dampedVelocity);
                            
                            #if UNITY_EDITOR || DEVELOPMENT_BUILD
                            // Only log in editor or development builds to avoid console spam
                            if (Time.frameCount % 10 == 0) // Reduce frequency of logs
                            {
                                Debug.Log($"[PRECISE] Adjusted position during fall. Speed: {fallSpeed}, Distance: {closestDist}");
                            }
                            #endif
                        }
                    }
                }
            }
            else
            {
                // Reset to default iterations for normal movement
                Physics2D.velocityIterations = 8;
                Physics2D.positionIterations = 3;
            }
        }
    }

    private void Update()
    {
        // Skip all processing if game is paused
        if (Time.timeScale <= 0.001f)
            return;
            
        // Update coyote time timer
        if (jumpGroundedGraceTimer > 0f)
            jumpGroundedGraceTimer -= Time.deltaTime;

        // Skip processing if critical components aren't initialized
        if (RB == null)
        {
            Debug.LogWarning("RB is null in Update - trying to get component");
            RB = GetComponent<Rigidbody2D>();
            if (RB == null) return;
        }

        // Track grounded state for jump and dash reset logic
        bool isGroundedNow = IsGrounded();
        if (!wasGroundedLastFrame && isGroundedNow)
        {
            // Landed this frame, reset jumps and air dashes
            JumpsRemaining = MaxJumps;
            ResetAirDash();
        }
        wasGroundedLastFrame = isGroundedNow;

        // Check for dash end
        if (isDashing && Time.time >= dashEndTime)
        {
            EndDash();
        }

        // Process dash input - only if we have InputReader and DashState
        if (InputReader != null && DashState != null && InputReader.IsDashPressed() && CanDash())
        {
            float direction = GetMovementInput().x;
            if (direction != 0)
            {
                StartDash(direction);
                return;
            }
        }

        // Update current state
        if (currentState != null)
        {
            try
            {
                currentState.Tick(Time.deltaTime);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerStateMachine] Error during Tick of state {currentState.GetType().Name}: {e.Message}");
                
                // If Tick fails, try falling back to IdleState
                if (currentState != IdleState && IdleState != null)
                {
                    Debug.Log("[PlayerStateMachine] Falling back to IdleState after Tick error");
                    SwitchState(IdleState);
                }
            }
        }
        else if (IdleState != null)
        {
            // If current state is null, initialize with idle state
            Debug.LogWarning("Current state was null. Initializing with Idle state.");
            SwitchState(IdleState);
        }
    }

    public void SwitchState(PlayerBaseState newState)
    {
        if (newState == null)
        {
            Debug.LogError("[PlayerStateMachine] Attempted to switch to a null state!");
            return;
        }

        PlayerBaseState oldState = currentState;
        
        // Exit current state if it exists
        if (currentState != null)
        {
            try
            {
                currentState.Exit();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerStateMachine] Error during Exit of state {currentState.GetType().Name}: {e.Message}");
            }
        }

        // Switch to new state
        currentState = newState;
        CurrentState = newState; // Set the public CurrentState property
        
        try
        {
            currentState.Enter();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerStateMachine] Error during Enter of state {currentState.GetType().Name}: {e.Message}");
            
            // Fallback to IdleState if available
            if (newState != IdleState && IdleState != null)
            {
                Debug.Log("[PlayerStateMachine] Falling back to IdleState after error");
                currentState = IdleState;
                CurrentState = IdleState;
                try
                {
                    currentState.Enter();
                }
                catch
                {
                    Debug.LogError("[PlayerStateMachine] Failed to enter fallback IdleState!");
                }
            }
        }
        
        // Track state transition time
        stateEnterTime = Time.time;
        
        // Only trigger event if both states are valid and there's a subscriber
        if (oldState != null && OnStateChanged != null)
        {
            try
            {
                OnStateChanged(oldState, currentState);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerStateMachine] Error in state transition event handler: {e.Message}");
            }
        }
    }

    public Vector2 GetMovementInput()
    {
        Vector2 input = InputReader.GetMovementInput();
        
        // Update facing direction based on input
        if (input.x != 0)
        {
            IsFacingRight = input.x > 0;
            transform.localScale = new Vector3(IsFacingRight ? 1 : -1, 1, 1);
        }
        
        return input;
    }

    public bool IsRunPressed()
    {
        // Delegate to the InputReader instance
        return InputReader.IsRunPressed();
    }

    // For extensibility: get state by name
    public PlayerBaseState GetState(string stateName)
    {
        if (stateRegistry.TryGetValue(stateName, out var state))
            return state;
        return null;
    }
    // Robust ground check using OverlapCircle with additional safety checks for high speeds
    public bool IsGrounded()
    {
        // During coyote time after jump, always return false
        if (jumpGroundedGraceTimer > 0f)
            return false;

        if (groundCheckPoint == null)
        {
            Debug.LogWarning("Ground Check Point not assigned in the Inspector! Add a child GameObject with this name or assign it in the Inspector.", this);
            return false;
        }

        // Standard ground check using OverlapCircle with moderate radius
        Vector2 checkPosition = groundCheckPoint.position + new Vector3(0, groundCheckOffset, 0);
        bool grounded = Physics2D.OverlapCircle(checkPosition, groundCheckRadius * 1.25f, groundLayer);
        
        // Use a raycast check for precision with high-speed falls
        if (!grounded && RB != null && RB.linearVelocity.y < 0)
        {
            // Calculate appropriate ray length based on velocity
            float fallSpeed = Mathf.Abs(RB.linearVelocity.y);
            float fallSpeedFactor = fallSpeed / 10f; // Scale factor based on fall speed
            float rayLength = groundCheckRadius * 2.5f * Mathf.Max(1f, fallSpeedFactor);
            
            // Use multiple raycasts for better coverage but with narrower spread
            float spread = 0.08f; // Reduced from 0.1f
            Vector2 leftCheck = checkPosition + new Vector2(-spread, 0);
            Vector2 centerCheck = checkPosition;
            Vector2 rightCheck = checkPosition + new Vector2(spread, 0);
            
            // Cast three rays for coverage
            RaycastHit2D hitLeft = Physics2D.Raycast(leftCheck, Vector2.down, rayLength, groundLayer);
            RaycastHit2D hitCenter = Physics2D.Raycast(centerCheck, Vector2.down, rayLength, groundLayer);
            RaycastHit2D hitRight = Physics2D.Raycast(rightCheck, Vector2.down, rayLength, groundLayer);
            
            // Visualize the raycasts in the editor only in debug mode
            #if UNITY_EDITOR
            Debug.DrawRay(leftCheck, Vector2.down * rayLength, hitLeft.collider != null ? Color.green : Color.red, 0.1f);
            Debug.DrawRay(centerCheck, Vector2.down * rayLength, hitCenter.collider != null ? Color.green : Color.red, 0.1f);
            Debug.DrawRay(rightCheck, Vector2.down * rayLength, hitRight.collider != null ? Color.green : Color.red, 0.1f);
            #endif
            
            // If any ray hits ground, we need to determine proper positioning
            if (hitLeft.collider != null || hitCenter.collider != null || hitRight.collider != null)
            {
                // Find the closest hit for positioning
                float closestDistance = float.MaxValue;
                RaycastHit2D closestHit = default;
                
                if (hitLeft.collider != null && hitLeft.distance < closestDistance)
                {
                    closestDistance = hitLeft.distance;
                    closestHit = hitLeft;
                }
                
                if (hitCenter.collider != null && hitCenter.distance < closestDistance)
                {
                    closestDistance = hitCenter.distance;
                    closestHit = hitCenter;
                }
                
                if (hitRight.collider != null && hitRight.distance < closestDistance)
                {
                    closestDistance = hitRight.distance;
                    closestHit = hitRight;
                }
                
                // Calculate more cautious snapping thresholds
                float snapDistance = 0.08f * Mathf.Max(1f, fallSpeedFactor * 0.7f); // Less aggressive scaling
                
                // Only snap if we're very close to the ground or falling extremely fast
                if (closestHit.collider != null && 
                    (closestDistance < snapDistance || RB.linearVelocity.y < -18f)) // Higher threshold for auto-snap
                {
                    // Calculate the exact position we need to place the player at
                    float yAdjust = 0.01f; // Minimal offset
                    float targetY = closestHit.point.y + yAdjust;
                    
                    // Don't actually move the player if we're moving too slowly or the distance is large
                    if (fallSpeed > 10f || closestDistance < 0.05f)
                    {
                        // Position adjustment - move player to exactly touch the ground
                        transform.position = new Vector3(transform.position.x, targetY + playerCollider.bounds.extents.y, transform.position.z);
                        
                        // Reduce vertical velocity rather than zeroing it immediately
                        if (fallSpeed > 15f)
                        {
                            // Use a damping approach for high speeds
                            float dampingFactor = 0.2f;
                            RB.linearVelocity = new Vector2(RB.linearVelocity.x, -fallSpeed * dampingFactor);
                        }
                        
                        #if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log($"Precise ground positioning. Distance: {closestDistance}, Speed: {fallSpeed}");
                        #endif
                    }
                }
                
                // We consider ourselves grounded if we're within a reasonable distance
                grounded = closestDistance < snapDistance * 1.5f;
            }
        }
        
        // Warn if grounded while moving up (likely ground check is inside collider)
        if (grounded && RB != null && RB.linearVelocity.y > 0.1f)
        {
            // Only warn once per session about this issue
            if (!warnedAboutGroundCheck)
            {
                Debug.LogWarning("[PlayerStateMachine] IsGrounded() is true while moving upward. Adjust groundCheckPoint position or groundCheckRadius in the Inspector.");
                warnedAboutGroundCheck = true;
            }
        }
        
        return grounded;
    }

    // Simple wall check (replace with your own logic)
    public bool IsTouchingWall()
    {
        if (playerCollider == null)
        {
            return false;
        }
        
        // Wall detection using 2D raycast
        Vector2 direction = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        Vector2 checkPosition = playerCollider.bounds.center + new Vector3(0, wallCheckOffset, 0);
        
        // Use multiple raycasts for more reliable detection
        float height = playerCollider.bounds.size.y;
        float raySpacing = height / 3;
        float topOffset = height * 0.4f;
        
        Vector2 topCheckPos = checkPosition + new Vector2(0, topOffset);
        Vector2 midCheckPos = checkPosition;
        Vector2 bottomCheckPos = checkPosition - new Vector2(0, topOffset);
        
        float rayDistance = playerCollider.bounds.extents.x + wallCheckDistance;
        
        RaycastHit2D hitTop = Physics2D.Raycast(topCheckPos, direction, rayDistance, groundLayer);
        RaycastHit2D hitMid = Physics2D.Raycast(midCheckPos, direction, rayDistance, groundLayer);
        RaycastHit2D hitBottom = Physics2D.Raycast(bottomCheckPos, direction, rayDistance, groundLayer);
        
        // Visualize raycasts in editor only
        #if UNITY_EDITOR
        Color rayColor = (hitTop.collider != null || hitMid.collider != null || hitBottom.collider != null) ? Color.green : Color.red;
        Debug.DrawRay(topCheckPos, direction * rayDistance, rayColor, 0.1f);
        Debug.DrawRay(midCheckPos, direction * rayDistance, rayColor, 0.1f);
        Debug.DrawRay(bottomCheckPos, direction * rayDistance, rayColor, 0.1f);
        #endif
        
        // Return true if any ray hit a wall
        return hitTop.collider != null || hitMid.collider != null || hitBottom.collider != null;
    }

    public void ClampVelocity(Rigidbody2D rb)
    {
        if (rb == null) return;
        
        Vector2 velocity = rb.linearVelocity;
        
        // Clamp horizontal velocity
        velocity.x = Mathf.Clamp(velocity.x, -maxHorizontalSpeed, maxHorizontalSpeed);
        
        // Clamp vertical velocity - different limits for up and down
        if (velocity.y > 0)
        {
            velocity.y = Mathf.Min(velocity.y, maxVerticalSpeed);
        }
        else
        {
            // Use the configurable maxFallSpeed from the inspector
            velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        }
        
        rb.linearVelocity = velocity;
    }

    public bool CanJump()
    {
        return Time.time >= lastJumpTime + jumpCooldown;
    }

    public void OnJumpStart()
    {
        lastJumpTime = Time.time;
        jumpGroundedGraceTimer = jumpGroundedGraceDuration;
    }

    public void OnJumpEnd()
    {
        // No longer needed for variable jump height
    }

    public float GetJumpForce()
    {
        return JumpForce;
    }

    public bool IsJumpHeld()
    {
        return false; // No longer using jump hold
    }

    public void StartDash(float direction)
    {
        isDashing = true;
        // Store the direction
        float dashDirection = direction;
        dashEndTime = Time.time + DashDuration;
        // Set velocity directly to dash speed, ignoring drag
        RB.linearVelocity = new Vector2(direction * DashSpeed, 0f);
        // Disable drag during dash
        RB.linearDamping = 0f;
    }

    public void EndDash()
    {
        isDashing = false;
        // Restore normal drag
        RB.linearDamping = 1f;
        
        if (InputReader.GetMovementInput().x * Mathf.Sign(RB.linearVelocity.x) <= 0)
        {
            // If not moving in dash direction, set to 0
            RB.linearVelocity = new Vector2(0f, RB.linearVelocity.y);
        }
        else
        {
            // If still moving in dash direction, set to normal speed
            float normalSpeed = GetHorizontalSpeed(IsGrounded(), IsTouchingWall() && RB.linearVelocity.y <= 0);
            RB.linearVelocity = new Vector2(Mathf.Sign(RB.linearVelocity.x) * normalSpeed, RB.linearVelocity.y);
        }
    }

    public bool IsDashing()
    {
        return isDashing;
    }

    public bool CanDash()
    {
        bool cooldownElapsed = Time.time >= lastDashTime + DashCooldown;
        
        if (!cooldownElapsed)
            return false;
            
        if (IsGrounded())
            return true;
            
        return AllowAirDash && airDashesRemaining > 0;
    }

    public void OnDashStart()
    {
        lastDashTime = Time.time;
        isDashing = true;
        dashEndTime = Time.time + DashDuration;
        
        // Use up air dash if in the air
        if (!IsGrounded() && AllowAirDash)
        {
            airDashesRemaining--;
        }
    }

    public void ResetAirDash()
    {
        airDashesRemaining = MaxAirDashes;
    }

    public Vector2 ApplyDeceleration(Vector2 currentVelocity, bool isGrounded, float deltaTime)
    {
        float decelerationX = GetDecelerationX(isGrounded);
        float decelerationY = GetDecelerationY(isGrounded, currentVelocity.y > 0);
        
        // Scale deceleration to be applied over 1/100th of a second
        float timeScale = deltaTime / 0.01f;
        
        // Only apply deceleration if moving in that direction
        float forceX = currentVelocity.x != 0 ? -Mathf.Sign(currentVelocity.x) * decelerationX * timeScale : 0f;
        
        // Apply vertical deceleration only in the correct direction
        float forceY = 0f;
        if (currentVelocity.y > 0)
        {
            // Only apply upward deceleration when moving up
            forceY = -currentVelocity.y * decelerationY * timeScale;
        }
        else if (currentVelocity.y < 0)
        {
            // Only apply downward deceleration when moving down
            forceY = -currentVelocity.y * airborneDecelerationYDown * timeScale;
        }
        
        return new Vector2(forceX, forceY);
    }

    public void CheckGrounded()
    {
        // Check if player has become grounded
        if (IsGrounded())
        {
            // Reset jumps and air dashes when grounded
            JumpsRemaining = MaxJumps;
            ResetAirDash();
        }
    }

    public float GetDecelerationX(bool isGrounded) => isGrounded ? groundedDecelerationX : airborneDecelerationX;
    public float GetDecelerationY(bool isGrounded, bool isMovingUp) => isGrounded ? groundedDecelerationY : (isMovingUp ? airborneDecelerationY : airborneDecelerationYDown);

    public float GetHorizontalSpeed(bool isGrounded, bool isWallClinging) => 
        isGrounded ? MoveSpeed : 
        (isWallClinging ? MoveSpeed * 0.5f : AirControlSpeed);
}