using UnityEngine;

public class WallClingState : PlayerBaseState
{
    [Header("Wall Cling Settings")]
    [SerializeField] private float slideSpeed = 2f;
    [SerializeField] private float wallSnapDistance = 0.1f;
    [SerializeField] private float wallSnapSpeed = 20f;
    [SerializeField] private float wallClingHorizontalSpeedMultiplier = 0.5f;

    private bool jumpHeldOnEnter = false;

    public WallClingState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        // Store if jump was held when entering state
        jumpHeldOnEnter = stateMachine.InputReader.IsJumpPressed();
        
        // Play wall cling animation if available
        if (stateMachine.Animator != null)
            stateMachine.Animator.Play("Cling");
            
        // Immediately snap to wall
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

            // Immediately set position
            stateMachine.RB.position = targetPosition;
        }
            
        Debug.Log("[WallClingState] Entering Wall Cling State");
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
        Vector2 moveInput = stateMachine.InputReader.GetMovementInput();

        // Apply horizontal movement while maintaining downward slide
        if (stateMachine.RB != null)
        {
            // Calculate horizontal velocity based on input
            float targetVelocityX = moveInput.x * stateMachine.MoveSpeed * wallClingHorizontalSpeedMultiplier;
            
            // Apply the velocities
            stateMachine.RB.linearVelocity = new Vector2(targetVelocityX, -slideSpeed);
        }

        // Check for jump input to perform a wall jump
        if (stateMachine.InputReader.IsJumpPressed() && !jumpHeldOnEnter)
        {
            stateMachine.SwitchState(stateMachine.JumpState);
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
            if (moveInput == Vector2.zero)
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
    }

    public override void Exit()
    {
        Debug.Log("[WallClingState] Exiting Wall Cling State");
    }
}