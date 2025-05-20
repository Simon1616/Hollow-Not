using UnityEngine;

public class JumpState : PlayerBaseState
{
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
        isWallJump = stateMachine.IsTouchingWall() && stateMachine.RB.linearVelocity.y <= 0;

        if (isWallJump)
        {
            // Wall jump
            if (stateMachine.RB != null)
            {
                // Get wall jump direction
                Vector2 jumpDirection = GetWallJumpDirection();
                
                // Apply wall jump force immediately
                stateMachine.RB.linearVelocity = Vector2.zero; // Reset velocity for clean wall jump
                stateMachine.RB.AddForce(jumpDirection * stateMachine.WallJumpForce * 1.2f, ForceMode2D.Impulse);
                Debug.Log($"[JumpState] Wall Jump Force: {jumpDirection * stateMachine.WallJumpForce * 1.2f}");
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
                
                // Apply jump force immediately
                stateMachine.RB.AddForce(Vector2.up * stateMachine.JumpForce, ForceMode2D.Impulse);
                Debug.Log($"[JumpState] Jump Force: {Vector2.up * stateMachine.JumpForce}");
            }
        }
        
        // Record the jump start
        stateMachine.OnJumpStart();
        
        // Play jump animation
        stateMachine.SafePlayAnimation("Jump");
    }

    public override void Tick(float deltaTime)
    {
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
            
            // Apply deceleration/acceleration
            Vector2 decelerationForce = stateMachine.ApplyDeceleration(stateMachine.RB.linearVelocity, stateMachine.IsGrounded(), deltaTime);
            stateMachine.RB.AddForce(decelerationForce);
            
            // Additional downward force when falling
            if (stateMachine.RB.linearVelocity.y < 0)
            {
                stateMachine.RB.AddForce(Vector2.down * 2f, ForceMode2D.Force);
            }
        }

        // Clamp velocity to max speeds
        stateMachine.ClampVelocity(stateMachine.RB);

        // If grounded, transition to Idle/Walk/Run
        if (stateMachine.IsGrounded())
        {
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
            stateMachine.SwitchState(stateMachine.WallClingState);
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
        
        // Convert wall jump angle from degrees to radians
        float angleRadians = stateMachine.WallJumpAngle * Mathf.Deg2Rad;
        
        // Calculate direction components based on the angle
        float xComponent = Mathf.Cos(angleRadians) * Mathf.Sign(wallNormal.x);
        float yComponent = Mathf.Sin(angleRadians);
        
        // Create angled jump vector and normalize it
        Vector2 jumpDirection = new Vector2(xComponent, yComponent).normalized;
        
        return jumpDirection;
    }
}