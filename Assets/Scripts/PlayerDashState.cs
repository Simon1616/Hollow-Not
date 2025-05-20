using UnityEngine;

public class PlayerDashState : PlayerBaseState
{
    private float originalDrag;
    private float originalGravityScale;
    private float dashEndTime;
    private float dashDirection;
    private const float DASH_COOLDOWN = 0.45f;
    private static float lastDashTime = -DASH_COOLDOWN; // Initialize to allow immediate first dash
    private static bool canAirDash = true; // Track if air dash is available

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
        
        // If dashing in air, disable air dash until grounded
        if (!stateMachine.IsGrounded())
        {
            canAirDash = false;
        }
        
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
            // Reset air dash when grounded
            canAirDash = true;
        }
        else if (stateMachine.IsTouchingWall() && stateMachine.RB.linearVelocity.y <= 0)
        {
            // If wall clinging, use wall cling speed
            postDashVelocityX = stateMachine.GetMovementInput().x * stateMachine.MoveSpeed * 0.5f;
            postDashVelocityY = 0f;
            // Reset air dash when wall clinging
            canAirDash = true;
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

    public static bool CanDash(bool isGrounded)
    {
        // Check cooldown
        if (Time.time < lastDashTime + DASH_COOLDOWN)
            return false;
            
        // If grounded, can always dash
        if (isGrounded)
            return true;
            
        // If in air, can only dash if air dash is available
        return canAirDash;
    }
} 