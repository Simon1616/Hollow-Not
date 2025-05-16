using UnityEngine;

public class PlayerDashState : PlayerBaseState
{
    private float originalDrag;
    private float originalGravityScale;
    private float dashEndTime;
    private float dashDirection;
    private const float DASH_COOLDOWN = 0.45f;
    private static float lastDashTime = -DASH_COOLDOWN; // Initialize to allow immediate first dash

    public PlayerDashState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        // Store original physics values
        originalDrag = stateMachine.RB.linearDamping;
        originalGravityScale = stateMachine.RB.gravityScale;

        // Disable drag and gravity
        stateMachine.RB.linearDamping = 0f;
        stateMachine.RB.gravityScale = 0f;
        
        // Always use facing direction for dash
        dashDirection = stateMachine.IsFacingRight ? 1f : -1f;
        
        // Set velocity to dash speed with zero vertical velocity
        stateMachine.RB.linearVelocity = new Vector2(dashDirection * stateMachine.DashSpeed, 0f);
        
        // Set dash end time
        dashEndTime = Time.time + stateMachine.DashDuration;
        
        // Update last dash time
        lastDashTime = Time.time;
        
        // Play dash animation
        stateMachine.Animator.Play("Run", 0, 0f);
        
        Debug.Log($"Entered Dash State with direction: {dashDirection}");
    }

    public override void Tick(float deltaTime)
    {
        // Maintain dash velocity with zero vertical velocity
        stateMachine.RB.linearVelocity = new Vector2(dashDirection * stateMachine.DashSpeed, 0f);
        
        // Check if dash duration is complete
        if (Time.time >= dashEndTime)
        {
            stateMachine.SwitchState(stateMachine.IdleState);
        }
    }

    public override void Exit()
    {
        // Restore original physics values
        stateMachine.RB.linearDamping = originalDrag;
        stateMachine.RB.gravityScale = originalGravityScale;
        
        // Calculate post-dash velocity based on current state
        float postDashVelocityX = 0f;
        float postDashVelocityY = 0f;
        
        if (stateMachine.IsGrounded())
        {
            // If grounded, use normal movement speed
            postDashVelocityX = stateMachine.GetMovementInput().x * stateMachine.MoveSpeed;
            postDashVelocityY = 0f;
        }
        else if (stateMachine.IsTouchingWall() && stateMachine.RB.linearVelocity.y <= 0)
        {
            // If wall clinging, use wall cling speed
            postDashVelocityX = stateMachine.GetMovementInput().x * stateMachine.MoveSpeed * 0.5f;
            postDashVelocityY = 0f;
        }
        else
        {
            // If in air, use air control speed and current vertical velocity
            postDashVelocityX = stateMachine.GetMovementInput().x * stateMachine.AirControlSpeed;
            postDashVelocityY = stateMachine.RB.linearVelocity.y;
        }
        
        // Apply the calculated velocity
        stateMachine.RB.linearVelocity = new Vector2(postDashVelocityX, postDashVelocityY);
    }

    public static bool CanDash()
    {
        return Time.time >= lastDashTime + DASH_COOLDOWN;
    }
} 