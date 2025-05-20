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
        // Store original gravity scale
        originalGravityScale = stateMachine.RB.gravityScale;
        
        // Set wall cling gravity scale
        stateMachine.RB.gravityScale = wallClingGravityScale;
        
        // Store if jump was held when entering state
        jumpHeldOnEnter = stateMachine.InputReader.IsJumpPressed();
        
        // Reset horizontal input delay
        horizontalInputTime = Time.time;
        lastMoveInput = Vector2.zero;
        
        // Play wall cling animation if available
        stateMachine.SafePlayAnimation("Cling");
        
        // Reset double jump charge when touching wall
        stateMachine.JumpsRemaining = stateMachine.MaxJumps;
        
        // Snap to wall
        SnapToWall();
    }

    private void SnapToWall()
    {
        float direction = stateMachine.IsFacingRight ? 1f : -1f;
        RaycastHit2D hit = Physics2D.Raycast(
            stateMachine.RB.position,
            new Vector2(direction, 0),
            wallSnapDistance,
            stateMachine.groundLayer
        );

        if (hit.collider != null)
        {
            // Calculate target position (snapped to wall)
            Vector2 targetPosition = new Vector2(
                hit.point.x - (direction * stateMachine.playerCollider.bounds.extents.x),
                stateMachine.RB.position.y
            );

            // Smoothly move to target position
            stateMachine.RB.position = Vector2.Lerp(
                stateMachine.RB.position,
                targetPosition,
                wallSnapSpeed * Time.deltaTime
            );
        }
    }

    public override void Tick(float deltaTime)
    {
        // Check for Shoot input first
        if (stateMachine.InputReader.IsShootPressed())
        {
            stateMachine.SwitchState(stateMachine.ShootState);
            return;
        }

        // Get movement input
        Vector2 currentMoveInput = stateMachine.InputReader.GetMovementInput();
        
        // Check if we should stick to the wall
        float wallDirection = stateMachine.IsFacingRight ? 1f : -1f;
        isStickingToWall = currentMoveInput.x * wallDirection > 0;
        
        if (stateMachine.RB != null)
        {
            // Apply wall stickiness when holding towards wall
            if (isStickingToWall)
            {
                // Reduce vertical velocity when sticking
                stateMachine.RB.linearVelocity = new Vector2(
                    stateMachine.RB.linearVelocity.x,
                    Mathf.Lerp(stateMachine.RB.linearVelocity.y, -slideSpeed, wallClingStickiness)
                );
            }
            else
            {
                // Normal slide when not sticking
                stateMachine.RB.linearVelocity = new Vector2(
                    stateMachine.RB.linearVelocity.x,
                    -slideSpeed
                );
            }
            
            // Handle horizontal movement
            if (currentMoveInput.x != lastMoveInput.x)
            {
                horizontalInputTime = Time.time;
                lastMoveInput = currentMoveInput;
            }
            
            float targetVelocityX = 0f;
            if (Time.time >= horizontalInputTime + horizontalInputDelay)
            {
                targetVelocityX = lastMoveInput.x * stateMachine.MoveSpeed * wallClingHorizontalSpeedMultiplier;
            }
            
            // Apply horizontal velocity with smooth transition
            stateMachine.RB.linearVelocity = new Vector2(
                Mathf.Lerp(stateMachine.RB.linearVelocity.x, targetVelocityX, 0.2f),
                stateMachine.RB.linearVelocity.y
            );
        }

        // Check for jump input to perform a wall jump
        if (stateMachine.InputReader.IsJumpPressed() && !jumpHeldOnEnter)
        {
            stateMachine.ApplyWallJump();
            return;
        }
        
        // Update lockout: if jump is released, allow wall jump again
        if (!stateMachine.InputReader.IsJumpPressed())
        {
            jumpHeldOnEnter = false;
        }

        // Check if grounded
        if (stateMachine.IsGrounded())
        {
            stateMachine.JumpsRemaining = stateMachine.MaxJumps;
            if (currentMoveInput == Vector2.zero)
                stateMachine.SwitchState(stateMachine.IdleState);
            else if (stateMachine.InputReader.IsRunPressed())
                stateMachine.SwitchState(stateMachine.RunState);
            else
                stateMachine.SwitchState(stateMachine.WalkState);
            return;
        }

        // Check if no longer touching the wall
        if (!stateMachine.IsTouchingWall())
        {
            stateMachine.SwitchState(stateMachine.FallState);
            return;
        }
        
        // Continuously snap to wall while in this state
        SnapToWall();
    }

    public override void Exit()
    {
        // Restore original gravity scale
        stateMachine.RB.gravityScale = originalGravityScale;
        
        Debug.Log("[WallClingState] Exiting Wall Cling State");
    }
}