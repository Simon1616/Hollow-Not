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

// Base class for movement-related functionality
public abstract class MovementState : PlayerBaseState
{
    protected MovementState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    protected virtual void HandleMovement(float deltaTime)
    {
        Vector2 moveInput = stateMachine.GetMovementInput();
        float targetVelocityX = moveInput.x * stateMachine.MoveSpeed;
        
        // Apply force in newtons (mass * acceleration)
        float forceX = targetVelocityX * stateMachine.RB.mass;
        stateMachine.RB.AddForce(new Vector2(forceX, 0f));
        
        // Apply deceleration
        Vector2 decelerationForce = stateMachine.ApplyDeceleration(stateMachine.RB.velocity, stateMachine.IsGrounded(), deltaTime);
        stateMachine.RB.AddForce(decelerationForce);
        stateMachine.ClampVelocity(stateMachine.RB);
    }

    protected virtual void HandleStateTransitions()
    {
        if (!stateMachine.IsGrounded())
        {
            if (stateMachine.IsTouchingWall() && stateMachine.RB.velocity.y <= 0)
            {
                stateMachine.SwitchState(stateMachine.WallClingState);
                return;
            }
            else if (stateMachine.RB.velocity.y < 0)
            {
                stateMachine.SwitchState(stateMachine.FallState);
                return;
            }
            else if (stateMachine.RB.velocity.y > 0)
            {
                stateMachine.SwitchState(stateMachine.JumpState);
                return;
            }
        }

        if (stateMachine.InputReader.IsDashPressed && stateMachine.CanDash())
        {
            stateMachine.SwitchState(stateMachine.DashState);
            return;
        }

        if (stateMachine.InputReader.IsJumpPressed && stateMachine.CanJump())
        {
            stateMachine.SwitchState(stateMachine.JumpState);
            return;
        }
    }
}

// The main state machine component
public class PlayerStateMachine : MonoBehaviour
{
    private bool wasGroundedLastFrame = true;

    // --- Coyote time (grounded grace period) --- 
    private float jumpGroundedGraceTimer = 0f; // Initialize to 0 instead of 3
    private const float jumpGroundedGraceDuration = 0.10f; // 0.1 seconds of grace after jumping

    // Reference dimensions in Unity units
    private const float REFERENCE_HEIGHT = 5f; // Height in Unity units
    private const float REFERENCE_WIDTH = 3f;  // Width in Unity units
    
    // Screen scaling factor - only used for visual/physics checks, not movement
    private float screenScaleFactor = 1f;
    
    // Movement parameters (in meters per second)
    [Header("Movement Settings")]
    [SerializeField] private float baseMoveSpeed = 7f; // 7 m/s = 25.2 km/h
    [SerializeField] private float baseAirControlSpeed = 6f; // 6 m/s (slightly less than ground speed)
    [SerializeField] private float baseWallJumpForce = 6f; // 6 m/s
    [SerializeField] private float baseJumpForce = 6f; // 6 m/s
    [SerializeField] private float baseDashSpeed = 12f; // 12 m/s

    // Physics parameters (in meters)
    [Header("Physics Settings")]
    [SerializeField] private float baseGroundCheckRadius = 0.1f; // 10 cm
    [SerializeField] private float baseWallCheckDistance = 0.05f; // 5 cm
    [SerializeField] private float baseMaxHorizontalSpeed = 7f; // 7 m/s
    [SerializeField] private float baseMaxVerticalSpeed = 10f; // 10 m/s
    [SerializeField] private float baseMaxFallSpeed = 8f; // 8 m/s

    // Acceleration and deceleration (in m/s²)
    [Header("Acceleration Settings")]
    [SerializeField] private float groundAcceleration = 50f; // 50 m/s² for quick ground acceleration
    [SerializeField] private float airAcceleration = 40f; // 40 m/s² for slightly slower air acceleration
    [SerializeField] private float groundDeceleration = 60f; // 60 m/s² for quick ground deceleration
    [SerializeField] private float airDeceleration = 30f; // 30 m/s² for slower air deceleration

    // Movement values (no screen scaling)
    public float MoveSpeed => baseMoveSpeed;
    public float AirControlSpeed => baseAirControlSpeed;
    public float WallJumpForce => baseWallJumpForce;
    public float JumpForce => baseJumpForce;
    public float DashSpeed => baseDashSpeed;
    
    // Scaled physics values (only for collision detection)
    public float GroundCheckRadius => baseGroundCheckRadius * screenScaleFactor;
    public float WallCheckDistance => baseWallCheckDistance * screenScaleFactor;
    public float MaxHorizontalSpeed => baseMaxHorizontalSpeed;
    public float MaxVerticalSpeed => baseMaxVerticalSpeed;
    public float MaxFallSpeed => baseMaxFallSpeed;

    [Header("Collider Settings")]
    [SerializeField] public CapsuleCollider2D playerCollider;
    [SerializeField] private Vector2 standingColliderSize = new Vector2(1f, 2f);
    [SerializeField] private Vector2 standingColliderOffset = new Vector2(0f, 0f);
    [SerializeField] private Vector2 crouchingColliderSize = new Vector2(1f, 1f);
    [SerializeField] private Vector2 crouchingColliderOffset = new Vector2(0f, -0.5f);
    [SerializeField] private float standUpCheckDistance = 0.1f;
    [SerializeField] public LayerMask groundLayer;

    // Ground check settings (in meters)
    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckOffset = 0.05f; // 5 cm
    [SerializeField] private float groundCheckRadius = 0.1f; // 10 cm
    [SerializeField] private float groundCheckBufferTime = 0.1f; // 100ms buffer for ground detection
    private float lastGroundedTime;

    // Wall check settings (in meters)
    [Header("Wall Check Settings")]
    [SerializeField] private float wallCheckOffset = 0.1f; // Offset from center for wall checks
    [SerializeField] private float wallCheckDistance = 0.05f; // Increased from 0.02f to 0.05f for larger trigger area
    [SerializeField] private float wallCheckHeight = 0.5f; // Height range for wall checks
    private bool isTouchingWall;
    private int wallDirection; // 1 for right wall, -1 for left wall

    [Header("Jump Settings")]
    [SerializeField] private float jumpCooldown = 0.1f;
    [field: SerializeField] public int MaxDoubleJumps { get; private set; } = 1;  // Set in inspector
    private int doubleJumpsRemaining = 0;  // Track remaining double jumps
    public bool HasDoubleJumpAvailable => doubleJumpsRemaining > 0;  // Check if double jump is available
    [field: SerializeField] public float WallJumpAngle { get; private set; } = 35f;
    [field: SerializeField] public float WallJumpHorizontalForce { get; private set; } = 10f;
    [field: SerializeField] public float WallJumpVerticalForce { get; private set; } = 8f;
    [field: SerializeField] public float WallJumpBufferTime { get; private set; } = 0.1f;  // Time window for buffering wall jumps
    private float lastJumpTime = 0f;
    private bool wasJumpPressed = false;
    private bool wallJumpBuffered = false;  // Track if a wall jump is buffered
    private float wallJumpBufferTimer = 0f;  // Timer for wall jump buffer

    [Header("Dash Settings")]
    [field: SerializeField] public float DashDuration { get; private set; } = 0.2f;
    [field: SerializeField] public float DashCooldown { get; private set; } = 0.45f;
    [field: SerializeField] public bool AllowAirDash { get; private set; } = true;
    [field: SerializeField] public int MaxAirDashes { get; private set; } = 1;
    [field: SerializeField] public float DashForce { get; private set; } = 32f;  // New parameter for dash force
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
    private PlayerBaseState currentState;
    public PlayerBaseState CurrentState => currentState; // Make it a read-only property

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
        // Calculate screen scale factor based on camera height
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            // Calculate scale factor based on camera height in Unity units
            float cameraHeight = mainCamera.orthographicSize * 2f; // Full height in Unity units
            screenScaleFactor = cameraHeight / REFERENCE_HEIGHT;
            
            // Ensure the camera's orthographic size is set correctly
            if (mainCamera.orthographicSize != REFERENCE_HEIGHT / 2f)
            {
                mainCamera.orthographicSize = REFERENCE_HEIGHT / 2f;
            }
        }
        else
        {
            Debug.LogWarning("Main camera not found, using default scale factor");
            screenScaleFactor = 1f;
        }
        
        // Initialize InputReader
        InputReader = GetComponent<InputReader>();
        
        // Get component references
        RB = GetComponent<Rigidbody2D>();
        Animator = GetComponent<Animator>();
        
        // Initialize state registry
        stateRegistry = new Dictionary<string, PlayerBaseState>();
        
        // Initialize animator parameter caches
        existingAnimatorParameters = new HashSet<string>();
        nonExistingAnimatorParameters = new HashSet<string>();
        
        // Check for necessary colliders/check points
        if (playerCollider == null)
        {
            playerCollider = GetComponent<CapsuleCollider2D>();
            if (playerCollider == null)
            {
                playerCollider = gameObject.AddComponent<CapsuleCollider2D>();
                Debug.LogWarning("No CapsuleCollider2D found, adding one automatically.", this);
            }
            
            // Scale collider size based on screen size
            if (standingColliderSize == Vector2.zero)
            {
                standingColliderSize = playerCollider.size;
                standingColliderSize *= screenScaleFactor;
            }
            if (standingColliderOffset == Vector2.zero)
            {
                standingColliderOffset = Vector2.zero;
                standingColliderOffset.y *= screenScaleFactor;
            }
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

        // Configure Rigidbody2D for physics-based movement
        if (RB != null)
        {
            RB.gravityScale = 1f; // Use Unity's default gravity
            RB.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            RB.interpolation = RigidbodyInterpolation2D.Interpolate;
            RB.freezeRotation = true;
            RB.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }
    
    private void InitializeStateMachine()
    {
        try
        {
            // First ensure all required components are initialized
            if (RB == null)
            {
                RB = GetComponent<Rigidbody2D>();
                if (RB == null)
                {
                    RB = gameObject.AddComponent<Rigidbody2D>();
                    Debug.LogWarning("Added Rigidbody2D component automatically");
                }
            }

            if (InputReader == null)
            {
                InputReader = GetComponent<InputReader>();
                if (InputReader == null)
                {
                    throw new System.Exception("Failed to initialize InputReader");
                }
            }

            if (Animator == null)
            {
                Animator = GetComponentInChildren<Animator>();
                if (Animator == null)
                {
                    Debug.LogWarning("Animator component not found in children. Animations will not play.");
                }
            }

            // Initialize all states
            IdleState = new PlayerIdleState(this);
            WalkState = new WalkState(this);
            RunState = new RunState(this);
            JumpState = new JumpState(this);
            FallState = new FallState(this);
            WallClingState = new WallClingState(this);
            DashState = new PlayerDashState(this);
            ShootState = new ShootState(this);
            SlideState = new SlideState(this);
            
            // Verify all states are initialized
            if (IdleState == null) throw new System.Exception("Failed to initialize IdleState");
            if (WalkState == null) throw new System.Exception("Failed to initialize WalkState");
            if (RunState == null) throw new System.Exception("Failed to initialize RunState");
            if (JumpState == null) throw new System.Exception("Failed to initialize JumpState");
            if (FallState == null) throw new System.Exception("Failed to initialize FallState");
            if (WallClingState == null) throw new System.Exception("Failed to initialize WallClingState");
            if (DashState == null) throw new System.Exception("Failed to initialize DashState");
            if (ShootState == null) throw new System.Exception("Failed to initialize ShootState");
            if (SlideState == null) throw new System.Exception("Failed to initialize SlideState");
            
            // Set initial state
            currentState = IdleState;
            currentState.Enter();
            
            // Set default values
            doubleJumpsRemaining = MaxDoubleJumps;
            airDashesRemaining = MaxAirDashes;
            
            Debug.Log("State machine initialized successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize state machine: {e.Message}");
            // Try to recover by creating at least the idle state
            if (IdleState == null)
            {
                IdleState = new PlayerIdleState(this);
                if (IdleState != null)
                {
                    currentState = IdleState;
                    currentState.Enter();
                }
            }
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
        doubleJumpsRemaining = MaxDoubleJumps;
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
                if (fallSpeed > MaxFallSpeed * 0.88f) // Set threshold to 88% of max fall speed
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

        // Update wall jump buffer timer
        if (wallJumpBufferTimer > 0f)
        {
            wallJumpBufferTimer -= Time.deltaTime;
            if (wallJumpBufferTimer <= 0f)
            {
                wallJumpBuffered = false;
            }
        }

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
            // Landed this frame, reset double jumps and air dashes
            ResetDoubleJumps();
            ResetAirDash();
        }
        wasGroundedLastFrame = isGroundedNow;

        // Check for dash end
        if (isDashing && Time.time >= dashEndTime)
        {
            EndDash();
        }

        // Handle dash input first
        if (InputReader != null && DashState != null && InputReader.IsDashPressed() && CanDash())
        {
            StartDash(IsFacingRight ? 1f : -1f);
            return;
        }

        // Handle other state transitions
        if (currentState == null)
        {
            Debug.LogWarning("Current state is null, reinitializing state machine");
            InitializeStateMachine();
            return;
        }

        // Update current state
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

    public void SwitchState(PlayerBaseState newState)
    {
        if (newState == null)
        {
            Debug.LogError("[PlayerStateMachine] Attempted to switch to a null state!");
            return;
        }

        // Exit current state
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
        try
        {
            currentState.Enter();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerStateMachine] Error during Enter of state {currentState.GetType().Name}: {e.Message}");
            // If Enter fails, try falling back to IdleState
            if (newState != IdleState && IdleState != null)
            {
                currentState = IdleState;
                currentState.Enter();
            }
        }

        OnStateChanged?.Invoke(currentState, newState);
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
        bool isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);
        if (isGrounded)
        {
            lastGroundedTime = Time.time;
        }
        return isGrounded || (Time.time - lastGroundedTime < groundCheckBufferTime);
    }

    // Simple wall check (replace with your own logic)
    public bool IsTouchingWall()
    {
        // Check for walls at multiple heights
        Vector2 checkStart = transform.position;
        bool isWallOnRight = false;
        bool isWallOnLeft = false;

        // Check at top, center, and bottom
        float[] checkHeights = { wallCheckHeight, 0f, -wallCheckHeight };
        foreach (float height in checkHeights)
        {
            Vector2 checkPoint = checkStart + new Vector2(0f, height);
            
            // Check right side
            RaycastHit2D rightHit = Physics2D.Raycast(checkPoint, transform.right, wallCheckDistance, groundLayer);
            if (rightHit.collider != null)
            {
                isWallOnRight = true;
            }
            
            // Check left side
            RaycastHit2D leftHit = Physics2D.Raycast(checkPoint, -transform.right, wallCheckDistance, groundLayer);
            if (leftHit.collider != null)
            {
                isWallOnLeft = true;
            }
            
            // Debug visualization
            Debug.DrawRay(checkPoint, transform.right * wallCheckDistance, Color.red);
            Debug.DrawRay(checkPoint, -transform.right * wallCheckDistance, Color.red);
        }

        // Update wall direction
        if (isWallOnRight)
        {
            wallDirection = 1;
        }
        else if (isWallOnLeft)
        {
            wallDirection = -1;
        }

        return isWallOnRight || isWallOnLeft;
    }

    public int GetWallDirection()
    {
        return wallDirection;
    }

    public void ClampVelocity(Rigidbody2D rb)
    {
        if (rb == null) return;
        
        Vector2 velocity = rb.linearVelocity;
        
        velocity.x = Mathf.Clamp(velocity.x, -MaxHorizontalSpeed, MaxHorizontalSpeed);
        
        if (velocity.y > 0)
        {
            velocity.y = Mathf.Min(velocity.y, MaxVerticalSpeed);
        }
        else
        {
            velocity.y = Mathf.Max(velocity.y, -MaxFallSpeed);
        }
        
        rb.linearVelocity = velocity;
    }

    public bool CanJump()
    {
        return IsGrounded() || IsTouchingWall();
    }

    public void OnJumpStart()
    {
        lastJumpTime = Time.time;
        jumpGroundedGraceTimer = jumpGroundedGraceDuration;
        
        // Only use up a double jump if in the air (not grounded and not wall clinging)
        if (!IsGrounded() && !IsTouchingWall())
        {
            if (doubleJumpsRemaining > 0)
            {
                doubleJumpsRemaining--;
                Debug.Log($"[PlayerStateMachine] Double jump used. Double jumps remaining: {doubleJumpsRemaining}");
            }
        }
        else
        {
            // Reset double jumps when jumping from ground or wall
            doubleJumpsRemaining = MaxDoubleJumps;
            Debug.Log($"[PlayerStateMachine] Ground/Wall jump. Double jumps reset to: {doubleJumpsRemaining}");
        }
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
        if (!CanDash()) return;

        isDashing = true;
        dashEndTime = Time.time + DashDuration;
        lastDashTime = Time.time;
        
        // Use up air dash if in the air
        if (!IsGrounded() && !IsTouchingWall() && AllowAirDash)
        {
            airDashesRemaining--;
        }
        
        // Apply dash force
        Vector2 dashForce = new Vector2(direction * DashForce, 0f);
        RB.linearVelocity = Vector2.zero; // Reset velocity first
        RB.AddForce(dashForce, ForceMode2D.Impulse);
        
        // Switch to dash state
        SwitchState(DashState);
    }

    public void EndDash()
    {
        isDashing = false;
        
        // Calculate post-dash velocity
        Vector2 currentVelocity = RB.linearVelocity;
        float horizontalSpeed = Mathf.Abs(currentVelocity.x);
        
        if (horizontalSpeed > MoveSpeed)
        {
            // If moving faster than normal speed, gradually reduce to normal speed
            float targetSpeed = MoveSpeed * Mathf.Sign(currentVelocity.x);
            RB.linearVelocity = new Vector2(targetSpeed, currentVelocity.y);
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
            
        // Can always dash on ground or while wall clinging
        if (IsGrounded() || IsTouchingWall())
            return true;
            
        // In air, need to have air dashes remaining
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
        // Only reset air dashes when touching ground or wall
        if (IsGrounded() || IsTouchingWall())
        {
            airDashesRemaining = MaxAirDashes;
        }
    }

    public Vector2 ApplyDeceleration(Vector2 currentVelocity, bool isGrounded, float deltaTime)
    {
        // Calculate deceleration in m/s²
        float decelerationX = isGrounded ? groundDeceleration : airDeceleration;
        float decelerationY = isGrounded ? 60f : (currentVelocity.y > 0 ? 30f : 20f);

        // Convert to force (F = ma)
        Vector2 deceleration = new Vector2(
            -Mathf.Sign(currentVelocity.x) * decelerationX * RB.mass,
            -Mathf.Sign(currentVelocity.y) * decelerationY * RB.mass
        );

        return deceleration * deltaTime;
    }

    public void CheckGrounded()
    {
        // Check if player has become grounded
        if (IsGrounded())
        {
            // Reset double jumps and air dashes when grounded
            doubleJumpsRemaining = MaxDoubleJumps;
            ResetAirDash();
            
            // Preserve horizontal velocity when landing
            if (RB != null)
            {
                Vector2 currentVelocity = RB.linearVelocity;
                RB.linearVelocity = new Vector2(currentVelocity.x, 0f);
            }
        }
    }

    public float GetDecelerationX(bool isGrounded) => isGrounded ? groundDeceleration : airDeceleration;
    public float GetDecelerationY(bool isGrounded, bool isMovingUp) => isGrounded ? 60f : (isMovingUp ? 30f : 20f);

    public float GetHorizontalSpeed(bool isGrounded, bool isWallClinging) => 
        isGrounded ? MoveSpeed : 
        (isWallClinging ? MoveSpeed * 0.5f : AirControlSpeed);

    public void ApplyWallJump()
    {
        if (!IsTouchingWall()) return;

        // Calculate wall jump direction
        float wallDirection = IsFacingRight ? -1f : 1f;
        
        // Convert angle to radians
        float angleInRadians = WallJumpAngle * Mathf.Deg2Rad;
        
        // Calculate the force components with better control
        Vector2 jumpForce = new Vector2(
            wallDirection * WallJumpHorizontalForce * Mathf.Cos(angleInRadians),
            WallJumpVerticalForce * Mathf.Sin(angleInRadians)
        );
        
        // Reset velocity and apply force
        RB.linearVelocity = Vector2.zero;
        RB.AddForce(jumpForce, ForceMode2D.Impulse);
        
        // Update facing direction
        IsFacingRight = !IsFacingRight;
        transform.localScale = new Vector3(IsFacingRight ? 1 : -1, 1, 1);
        
        // Start jump cooldown and grace period
        lastJumpTime = Time.time;
        jumpGroundedGraceTimer = jumpGroundedGraceDuration;
        
        // Reset wall jump buffer
        wallJumpBuffered = false;
        wallJumpBufferTimer = 0f;
        
        // Switch to jump state
        SwitchState(JumpState);
    }

    public void BufferWallJump()
    {
        if (IsTouchingWall())
        {
            wallJumpBuffered = true;
            wallJumpBufferTimer = WallJumpBufferTime;
            Debug.Log("[PlayerStateMachine] Wall jump buffered");
        }
    }

    public bool HasBufferedWallJump()
    {
        return wallJumpBuffered && wallJumpBufferTimer > 0f;
    }

    // Add a new method to reset double jumps
    public void ResetDoubleJumps()
    {
        doubleJumpsRemaining = MaxDoubleJumps;
        Debug.Log($"[PlayerStateMachine] Double jumps reset to: {doubleJumpsRemaining}");
    }

    public bool IsOnSolidGround()
    {
        // Get the player's collider bounds
        Bounds colliderBounds = GetComponent<Collider2D>().bounds;
        float checkWidth = colliderBounds.size.x * 0.8f; // Use 80% of width to avoid edge cases
        float checkStart = colliderBounds.min.x + (colliderBounds.size.x - checkWidth) * 0.5f;
        float checkEnd = checkStart + checkWidth;
        
        // Number of rays to cast across the width
        int numRays = 5;
        float raySpacing = checkWidth / (numRays - 1);
        
        // Cast multiple rays across the bottom of the player
        int groundHits = 0;
        float groundCheckDistance = 0.1f; // Small distance to check for ground
        
        for (int i = 0; i < numRays; i++)
        {
            Vector2 rayStart = new Vector2(
                checkStart + (raySpacing * i),
                colliderBounds.min.y
            );
            
            RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, groundCheckDistance, groundLayer);
            
            // Debug visualization
            Debug.DrawRay(rayStart, Vector2.down * groundCheckDistance, hit.collider != null ? Color.green : Color.red);
            
            if (hit.collider != null)
            {
                groundHits++;
            }
        }
        
        // Require at least 60% of the rays to hit ground to consider it solid ground
        return groundHits >= (numRays * 0.6f);
    }
}

// Extension method for Animator to check parameter existence
public static class AnimatorExtensions
{
    public static bool HasParameter(this Animator animator, string paramName)
    {
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }
}