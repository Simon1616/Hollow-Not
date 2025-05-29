using UnityEngine;

public class WallClingState : PlayerBaseState
{
    [Header("Wall Cling Settings")]
    [SerializeField] private float slideSpeed = 2f;
    [SerializeField] private float wallSnapDistance = 0.1f;
    [SerializeField] private float wallSnapSpeed = 20f;
    [SerializeField] private float wallClingHorizontalSpeedMultiplier = 0.5f;
    [SerializeField] private float horizontalInputDelay = 0.05f;
    [SerializeField] private float wallClingStickiness = 0.8f;  // New parameter for wall stickiness
    [SerializeField] private float wallClingGravityScale = 0.2f;  // New parameter for wall cling gravity

    private bool jumpHeldOnEnter = false;
    private float horizontalInputTime = 0f;
    private Vector2 lastMoveInput = Vector2.zero;
    private float originalGravityScale;
    private bool isStickingToWall = false;

    public WallClingState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        // Reset jumps and air dashes when starting to wall cling
        stateMachine.ResetDoubleJumps();
        stateMachine.ResetAirDash();
        
        // Store original gravity scale
        originalGravityScale = stateMachine.RB.gravityScale;
        
        // Set wall cling gravity scale
        stateMachine.RB.gravityScale = wallClingGravityScale;
        
        // Store if jump was held when entering state
        jumpHeldOnEnter = stateMachine.InputReader.IsJumpPressed();
        
        // Reset horizontal input delay
        horizontalInputTime = Time.time;
        lastMoveInput = Vector2.zero;
        
        // Play wall cling animation
        stateMachine.SafePlayAnimation("WallCling");
        
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[WallClingState] Entering Wall Cling State");
        #endif
    }

    private void SnapToWall()
    {
        if (stateMachine.RB == null) return;

        // Get wall direction
        float wallDirection = stateMachine.IsFacingRight ? 1f : -1f;
        
        // Calculate target position
        float targetX = stateMachine.RB.position.x + (wallDirection * stateMachine.WallCheckDistance);
        
        // Smoothly move to wall
        Vector2 targetPosition = new Vector2(targetX, stateMachine.RB.position.y);
        stateMachine.RB.position = Vector2.Lerp(stateMachine.RB.position, targetPosition, 0.5f);
        
        // Reset double jump charge when touching wall
        stateMachine.ResetDoubleJumps();
    }

    public override void Tick(float deltaTime)
    {
        // Check if still touching wall
        if (!stateMachine.IsTouchingWall())
        {
            stateMachine.SwitchState(stateMachine.FallState);
            return;
        }

        // Get movement input
        Vector2 moveInput = stateMachine.GetMovementInput();
        
        // Apply wall slide gravity
        if (stateMachine.RB.linearVelocity.y < 0)
        {
            stateMachine.RB.linearVelocity = new Vector2(
                stateMachine.RB.linearVelocity.x,
                Mathf.Max(stateMachine.RB.linearVelocity.y, -stateMachine.MaxFallSpeed * 0.5f)
            );
        }
        
        // Handle wall jump input
        if (stateMachine.InputReader.IsJumpPressed() && stateMachine.CanJump())
        {
            stateMachine.SwitchState(stateMachine.JumpState);
            return;
        }
        
        // Handle dash input
        if (stateMachine.InputReader.IsDashPressed() && stateMachine.CanDash())
        {
            stateMachine.SwitchState(stateMachine.DashState);
            return;
        }
        
        // Handle shoot input
        if (stateMachine.InputReader.IsShootPressed())
        {
            stateMachine.SwitchState(stateMachine.ShootState);
            return;
        }
        
        // Update last move input
        lastMoveInput = moveInput;
    }

    public override void Exit()
    {
        // Restore original gravity scale
        stateMachine.RB.gravityScale = originalGravityScale;
        
        Debug.Log("[WallClingState] Exiting Wall Cling State");
    }
}