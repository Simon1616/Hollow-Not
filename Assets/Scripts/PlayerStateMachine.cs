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
    [SerializeField] private float maxFallSpeed = 20f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpCooldown = 0.67f; // 2/3 of a second
    private float lastJumpTime = 0f;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.1f;
    private float dashEndTime;
    private bool isDashing;
    private float dashDirection;

    public float GetDecelerationX(bool isGrounded) => isGrounded ? groundedDecelerationX : airborneDecelerationX;
    public float GetDecelerationY(bool isGrounded, bool isMovingUp) => isGrounded ? groundedDecelerationY : (isMovingUp ? airborneDecelerationY : airborneDecelerationYDown);

    public float GetHorizontalSpeed(bool isGrounded, bool isWallClinging) => 
        isGrounded ? MoveSpeed : 
        (isWallClinging ? MoveSpeed * 0.5f : AirControlSpeed);

    private PlayerBaseState currentState;

    // State registry for extensibility
    private Dictionary<string, PlayerBaseState> stateRegistry = new Dictionary<string, PlayerBaseState>();

    private bool isFacingRight = true;
    // Concrete states
    public PlayerIdleState IdleState { get; private set; } // Add IdleState property back

    public WalkState WalkState { get; private set; }
    public RunState RunState { get; private set; }
    public JumpState JumpState { get; private set; }
    public SlideState SlideState { get; private set; }
    public WallClingState WallClingState { get; private set; }
    public ShootState ShootState { get; private set; } // Add ShootState declaration
    public FallState FallState { get; private set; } // Add FallState declaration

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

    public float JumpForce = 10f;
    public bool IsFacingRight { get; private set; } = true;
    public PlayerBaseState CurrentState { get; private set; }

    private void Awake()
    {
        // Get Components
        RB = GetComponent<Rigidbody2D>();
        Animator = GetComponentInChildren<Animator>(); // Or GetComponent<Animator>()
        if (playerCollider == null)
        {
            playerCollider = GetComponent<CapsuleCollider2D>();
            if (playerCollider != null)
            {
                // Store initial size/offset if not set via Inspector
                if (standingColliderSize == Vector2.zero) standingColliderSize = playerCollider.size;
                if (standingColliderOffset == Vector2.zero && playerCollider.offset != Vector2.zero) standingColliderOffset = playerCollider.offset;
            }
            else
            {
                Debug.LogError("Player Collider not found or assigned!", this);
            }
        }

    
        // Initialize input reader
        InputReader = new InputReader(); // Instantiate the new InputReader class

        // Initialize concrete states
        IdleState = new PlayerIdleState(this); // Instantiate the new PlayerIdleState class
        // MoveState removed
        WalkState = new WalkState(this);
        RunState = new RunState(this);
    
        // Register states
        stateRegistry[nameof(PlayerIdleState)] = IdleState; // Register the new PlayerIdleState
        // MoveState registration removed
        stateRegistry[nameof(WalkState)] = WalkState;
        stateRegistry[nameof(RunState)] = RunState;
        JumpState = new JumpState(this);
        stateRegistry[nameof(JumpState)] = JumpState;
        SlideState = new SlideState(this);
        // ... register other states
        WallClingState = new WallClingState(this);
        stateRegistry[nameof(WallClingState)] = WallClingState;
        stateRegistry[nameof(SlideState)] = SlideState;
        ShootState = new ShootState(this); // Initialize ShootState
        stateRegistry[nameof(ShootState)] = ShootState; // Register ShootState
        FallState = new FallState(this); // Initialize FallState
        stateRegistry[nameof(FallState)] = FallState; // Register FallState

        // Initialize jumps
        JumpsRemaining = MaxJumps;
    }

    private void Start()
    {
        // Set the initial state
        SwitchState(IdleState); // Start in Idle state
        JumpsRemaining = MaxJumps;
    }

    private void Update()
    {
        // Update coyote time timer
        if (jumpGroundedGraceTimer > 0f)
            jumpGroundedGraceTimer -= Time.deltaTime;

        // Track grounded state for jump reset logic
        bool isGroundedNow = IsGrounded();
        if (!wasGroundedLastFrame && isGroundedNow)
        {
            // Landed this frame, reset jumps
            JumpsRemaining = MaxJumps;
        }
        wasGroundedLastFrame = isGroundedNow;

        // Check for dash end
        if (isDashing && Time.time >= dashEndTime)
        {
            EndDash();
        }

        // Update current state
        if (currentState != null)
        {
            currentState.Tick(Time.deltaTime);
        }
    }

    public void SwitchState(PlayerBaseState newState)
    {
        if (newState == null)
        {
            Debug.LogError("[PlayerStateMachine] Attempted to switch to a null state!");
            return;
        }

        // Exit current state if it exists
        if (currentState != null)
        {
            currentState.Exit();
        }

        // Switch to new state
        currentState = newState;
        CurrentState = newState; // Set the public CurrentState property
        currentState.Enter();
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
    // Robust ground check using OverlapCircle
    public bool IsGrounded()
    {
        // During coyote time after jump, always return false
        if (jumpGroundedGraceTimer > 0f)
            return false;

        if (groundCheckPoint == null)
        {
            Debug.LogError("Ground Check Point not assigned in the Inspector!", this);
            return false;
        }

        Vector2 checkPosition = groundCheckPoint.position + new Vector3(0, groundCheckOffset, 0);
        bool grounded = Physics2D.OverlapCircle(checkPosition, groundCheckRadius, groundLayer);
        
        // Warn if grounded while moving up (likely ground check is inside collider)
        if (grounded && RB != null && RB.linearVelocity.y > 0.1f)
        {
            Debug.LogWarning("[PlayerStateMachine] IsGrounded() is true while moving upward. Adjust groundCheckPoint position or groundCheckRadius in the Inspector.");
        }
        return grounded;
    }

    // Simple wall check (replace with your own logic)
    public bool IsTouchingWall()
    {
        // Wall detection using 2D raycast
        Vector2 direction = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        Vector2 checkPosition = playerCollider.bounds.center + new Vector3(0, wallCheckOffset, 0);
        RaycastHit2D hit = Physics2D.Raycast(checkPosition, direction, playerCollider.bounds.extents.x + wallCheckDistance, groundLayer);
        Debug.DrawRay(checkPosition, direction * (playerCollider.bounds.extents.x + wallCheckDistance), hit.collider != null ? Color.green : Color.red);
        return hit.collider != null;
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
            velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        }
        
        rb.linearVelocity = velocity;
    }

    public bool CanJump()
    {
        return Time.time >= lastJumpTime + jumpCooldown;
    }

    public void OnJump()
    {
        lastJumpTime = Time.time;
        jumpGroundedGraceTimer = jumpGroundedGraceDuration; // Set the grace timer when jumping
    }

    public void StartDash(float direction)
    {
        isDashing = true;
        dashDirection = direction;
        dashEndTime = Time.time + dashDuration;
        // Set velocity directly to dash speed, ignoring drag
        RB.linearVelocity = new Vector2(direction * dashSpeed, 0f);
        // Disable drag during dash
        RB.linearDamping = 0f;
    }

    public void EndDash()
    {
        isDashing = false;
        // Restore normal drag
        RB.linearDamping = 1f;
        
        if (InputReader.GetMovementInput().x * dashDirection <= 0)
        {
            // If not moving in dash direction, set to 0
            RB.linearVelocity = new Vector2(0f, RB.linearVelocity.y);
        }
        else
        {
            // If still moving in dash direction, set to normal speed
            float normalSpeed = GetHorizontalSpeed(IsGrounded(), IsTouchingWall() && RB.linearVelocity.y <= 0);
            RB.linearVelocity = new Vector2(dashDirection * normalSpeed, RB.linearVelocity.y);
        }
    }

    public bool IsDashing()
    {
        return isDashing;
    }

    public bool CanDash()
    {
        return !isDashing;
    }
}