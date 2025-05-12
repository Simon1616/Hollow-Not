using UnityEngine;

public class JumpState : PlayerBaseState
{
    private float jumpForce = 7.5f;
    private float wallJumpHorizontalForce = 5f;
    private float enterTime;
    private bool hasJumped = false;
    private bool isWallJump = false;

    public JumpState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        enterTime = Time.time;
        Debug.Log($"[JumpState] Entering Jump State at {enterTime:F2}s");

        // Check if this is a wall jump
        bool isWallJump = stateMachine.IsTouchingWall() && stateMachine.RB.linearVelocity.y <= 0;

        if (isWallJump)
        {
            // Wall jump
            if (stateMachine.RB != null)
            {
                // Get wall jump direction
                Vector2 jumpDirection = GetWallJumpDirection();
                
                // Apply wall jump force
                stateMachine.RB.linearVelocity = Vector2.zero; // Reset velocity for clean wall jump
                stateMachine.RB.AddForce(jumpDirection * stateMachine.WallJumpForce, ForceMode2D.Impulse);
                Debug.Log($"[JumpState] Wall Jump Force: {jumpDirection * stateMachine.WallJumpForce}");
            }
        }
        else
        {
            // Normal jump (ground or air)
            if (stateMachine.RB != null)
            {
                // If this is an air jump, reset vertical velocity first
                if (!stateMachine.IsGrounded())
                {
                    stateMachine.RB.linearVelocity = new Vector2(
                        stateMachine.RB.linearVelocity.x,
                        0f
                    );
                }
                
                // Apply jump force as an impulse
                stateMachine.RB.AddForce(Vector2.up * stateMachine.JumpForce, ForceMode2D.Impulse);
                Debug.Log($"[JumpState] Jump Force: {Vector2.up * stateMachine.JumpForce}");
            }
        }
        
        // Only decrement jumps if not grounded and not a wall jump
        if (!stateMachine.IsGrounded() && !isWallJump)
        {
            stateMachine.JumpsRemaining = 0;
        }
        
        // Record the jump start
        stateMachine.OnJumpStart();
        
        // Play jump animation
        if (stateMachine.Animator != null)
        {
            stateMachine.Animator.Play("Jump");
        }
    }

    public override void Tick(float deltaTime)
    {
        // Debug: Print grounded state and vertical velocity every frame in JumpState
        Debug.Log($"[JumpState] Tick: IsGrounded={stateMachine.IsGrounded()} VelocityY={stateMachine.RB.linearVelocity.y}");

        // Check for Dash input
        if (stateMachine.InputReader.IsDashPressed() && stateMachine.CanDash())
        {
            float direction = Mathf.Sign(stateMachine.GetMovementInput().x);
            if (direction != 0)
            {
                stateMachine.StartDash(direction);
                return;
            }
        }

        // Check for Shoot input first
        if (stateMachine.InputReader.IsShootPressed())
        {
            stateMachine.SwitchState(stateMachine.ShootState);
            return;
        }

        // Apply horizontal movement input while airborne
        Vector2 moveInputAir = stateMachine.GetMovementInput();
        float targetVelocityX = moveInputAir.x * stateMachine.GetHorizontalSpeed(
            stateMachine.IsGrounded(),
            stateMachine.IsTouchingWall() && stateMachine.RB.linearVelocity.y <= 0
        );
        
        // Calculate deceleration force
        float decelerationX = stateMachine.GetDecelerationX(stateMachine.IsGrounded());
        float currentVelocityX = stateMachine.RB.linearVelocity.x;
        float forceX = (targetVelocityX - currentVelocityX) * decelerationX;
        
        // Apply horizontal movement with deceleration
        if (stateMachine.RB != null)
        {
            // Apply movement force
            stateMachine.RB.AddForce(new Vector2(forceX, 0f));
            
            // Apply deceleration
            Vector2 decelerationForce = stateMachine.ApplyDeceleration(stateMachine.RB.linearVelocity, stateMachine.IsGrounded(), deltaTime);
            stateMachine.RB.AddForce(decelerationForce);
        }

        // Clamp velocity to max speeds
        stateMachine.ClampVelocity(stateMachine.RB);

        // If grounded, reset jumps and transition to Idle/Walk/Run
        if (stateMachine.IsGrounded())
        {
            stateMachine.JumpsRemaining = stateMachine.MaxJumps;
            Vector2 moveInput = stateMachine.GetMovementInput();
            if (moveInput == Vector2.zero)
                stateMachine.SwitchState(stateMachine.IdleState);
            else if (stateMachine.InputReader.IsRunPressed())
                stateMachine.SwitchState(stateMachine.RunState);
            else
                stateMachine.SwitchState(stateMachine.WalkState);
            return;
        }
    
        // Check if falling against a wall -> transition to Wall Cling
        if (stateMachine.IsTouchingWall() && stateMachine.RB.linearVelocity.y <= 0)
        {
            // If jump is pressed and we have jumps remaining, perform wall jump
            if (stateMachine.InputReader.IsJumpPressed() && stateMachine.JumpsRemaining > 0 && stateMachine.CanJump())
            {
                stateMachine.SwitchState(stateMachine.JumpState);
                return;
            }
            stateMachine.SwitchState(stateMachine.WallClingState);
            return;
        }

        // Check for double jump (only on button press)
        if (stateMachine.InputReader.IsJumpPressed() && stateMachine.JumpsRemaining > 0 && stateMachine.CanJump())
        {
            stateMachine.SwitchState(stateMachine.JumpState);
            return;
        }

        // If not grounded and not touching wall, transition to FallState
        if (!stateMachine.IsGrounded() && !stateMachine.IsTouchingWall())
        {
            stateMachine.SwitchState(stateMachine.FallState);
            return;
        }
    }

    public override void Exit()
    {
        Debug.Log($"[JumpState] Exiting Jump State after {Time.time - enterTime:F2}s");
    }

    // Helper method to calculate wall jump direction
    private Vector2 GetWallJumpDirection()
    {
        // Get the wall normal (direction away from wall)
        float facing = stateMachine.transform.localScale.x;
        Vector2 wallNormal = facing > 0 ? Vector2.left : Vector2.right;
        
        // Create a 45-degree upward vector away from the wall
        Vector2 jumpDirection = (wallNormal + Vector2.up).normalized;
        
        return jumpDirection;
    }
}