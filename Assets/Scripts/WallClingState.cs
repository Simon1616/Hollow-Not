using UnityEngine;

public class WallClingState : MovementState
{
    private float wallClingStartTime;
    private const float WALL_SLIDE_SPEED = 2f; // 2 m/s
    private const float WALL_JUMP_FORCE = 6f; // 6 m/s
    private const float WALL_JUMP_HORIZONTAL_FORCE = 5f; // 5 m/s
    private const float WALL_DETACH_BUFFER_TIME = 0.15f; // 150ms buffer before detaching
    private const float WALL_STICK_FORCE = 2f; // Force to keep player against wall
    private const float WALL_SNAP_DISTANCE = 0.1f; // Distance to snap to wall
    private const float WALL_SNAP_SPEED = 10f; // Speed to snap to wall
    private const float GROUND_CHECK_BUFFER = 0.05f; // Small buffer for ground check

    private float wallDetachTimer;
    private bool isMovingAwayFromWall;
    private int wallDirection; // 1 for right wall, -1 for left wall
    private Vector2 targetWallPosition;
    private float wallCheckTimer;
    private bool wasOnGround;

    public WallClingState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        stateMachine.SafePlayAnimation("WallCling");
        wallClingStartTime = Time.time;
        wallDetachTimer = 0f;
        isMovingAwayFromWall = false;
        wallCheckTimer = 0f;
        wasOnGround = stateMachine.IsOnSolidGround();

        // Determine which wall we're clinging to
        wallDirection = stateMachine.GetWallDirection();
        
        // Calculate target wall position
        Vector2 currentPos = transform.position;
        targetWallPosition = new Vector2(
            currentPos.x + (wallDirection * WALL_SNAP_DISTANCE),
            currentPos.y
        );

        // Reset vertical velocity
        Vector2 velocity = stateMachine.RB.velocity;
        velocity.y = 0f;
        stateMachine.RB.velocity = velocity;

        Debug.Log($"Entered WallCling State at {wallClingStartTime} on {(wallDirection > 0 ? "right" : "left")} wall");
    }

    public override void Tick(float deltaTime)
    {
        // Get movement input
        Vector2 moveInput = stateMachine.GetMovementInput();
        float inputDirection = Mathf.Sign(moveInput.x);

        // Check if we just landed on solid ground
        bool isOnGround = stateMachine.IsOnSolidGround();
        if (isOnGround && !wasOnGround)
        {
            // We just landed on solid ground, transition to appropriate state
            if (moveInput.magnitude < 0.1f)
            {
                stateMachine.SwitchState(stateMachine.IdleState);
                return;
            }
            else if (stateMachine.InputReader.IsRunPressed)
            {
                stateMachine.SwitchState(stateMachine.RunState);
                return;
            }
            else
            {
                stateMachine.SwitchState(stateMachine.WalkState);
                return;
            }
        }
        wasOnGround = isOnGround;

        // Snap to wall if not already at target position
        Vector2 currentPos = transform.position;
        if (Vector2.Distance(currentPos, targetWallPosition) > 0.01f)
        {
            Vector2 newPosition = Vector2.MoveTowards(
                currentPos,
                targetWallPosition,
                WALL_SNAP_SPEED * deltaTime
            );
            stateMachine.RB.MovePosition(newPosition);
        }

        // Check if player is trying to move away from wall
        if (inputDirection != 0 && inputDirection != wallDirection)
        {
            if (!isMovingAwayFromWall)
            {
                isMovingAwayFromWall = true;
                wallDetachTimer = WALL_DETACH_BUFFER_TIME;
            }
            else if (wallDetachTimer > 0)
            {
                wallDetachTimer -= deltaTime;
            }
        }
        else
        {
            isMovingAwayFromWall = false;
            wallDetachTimer = 0f;
        }

        // Apply wall slide
        stateMachine.RB.velocity = new Vector2(stateMachine.RB.velocity.x, -WALL_SLIDE_SPEED);

        // Apply force to keep player against wall
        stateMachine.RB.AddForce(new Vector2(wallDirection * WALL_STICK_FORCE * stateMachine.RB.mass, 0f), ForceMode2D.Force);

        // Handle wall jump
        if (stateMachine.InputReader.IsJumpPressed)
        {
            // Apply wall jump force
            Vector2 wallJumpForce = new Vector2(-wallDirection * WALL_JUMP_HORIZONTAL_FORCE, WALL_JUMP_FORCE);
            stateMachine.RB.velocity = new Vector2(stateMachine.RB.velocity.x, 0f); // Reset vertical velocity
            stateMachine.RB.AddForce(wallJumpForce * stateMachine.RB.mass, ForceMode2D.Impulse);

            // Switch to jump state
            stateMachine.SwitchState(stateMachine.JumpState);
            return;
        }

        // Check if we're still against the wall
        if (!stateMachine.IsTouchingWall())
        {
            wallCheckTimer += deltaTime;
            if (wallCheckTimer >= 0.1f) // Give a small buffer before detaching
            {
                if (stateMachine.RB.velocity.y < 0)
                {
                    stateMachine.SwitchState(stateMachine.FallState);
                    return;
                }
                else
                {
                    stateMachine.SwitchState(stateMachine.JumpState);
                    return;
                }
            }
        }
        else
        {
            wallCheckTimer = 0f;
        }

        // Update animation parameters
        stateMachine.SafeSetAnimatorFloat("Speed", Mathf.Abs(stateMachine.RB.velocity.x));
        stateMachine.SafeSetAnimatorFloat("VerticalSpeed", stateMachine.RB.velocity.y);

        HandleStateTransitions();
    }

    public override void Exit()
    {
        Debug.Log($"Exited WallCling State after {Time.time - wallClingStartTime} seconds");
    }
}