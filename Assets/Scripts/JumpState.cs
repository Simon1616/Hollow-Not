using UnityEngine;

public class JumpState : PlayerBaseState
{
    private float enterTime;
    private bool isDoubleJump = false;

    public JumpState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        enterTime = Time.time;
        
        // Determine if this is a double jump
        isDoubleJump = !stateMachine.IsGrounded() && !stateMachine.IsTouchingWall();
        
        if (isDoubleJump)
        {
            // For double jump, reset vertical velocity first
            Vector2 currentVelocity = stateMachine.RB.linearVelocity;
            stateMachine.RB.linearVelocity = new Vector2(currentVelocity.x, 0f);
            
            // Apply jump force
            stateMachine.RB.AddForce(Vector2.up * stateMachine.JumpForce, ForceMode2D.Impulse);
            
            Debug.Log("[JumpState] Double jump initiated");
        }
        else
        {
            // For ground/wall jump, apply jump force directly
            if (stateMachine.IsTouchingWall() || stateMachine.HasBufferedWallJump())
            {
                stateMachine.ApplyWallJump();
            }
            else
            {
                stateMachine.RB.AddForce(Vector2.up * stateMachine.JumpForce, ForceMode2D.Impulse);
            }
            
            Debug.Log("[JumpState] Ground/Wall jump initiated");
        }
        
        // Play jump animation
        stateMachine.SafePlayAnimation("Jump");
        
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[JumpState] Entering Jump State at {enterTime:F2}s");
        #endif
    }

    public override void Tick(float deltaTime)
    {
        // Apply deceleration
        Vector2 deceleration = stateMachine.ApplyDeceleration(stateMachine.RB.linearVelocity, false, deltaTime);
        stateMachine.RB.linearVelocity += deceleration * deltaTime;
        
        // Clamp velocity
        stateMachine.ClampVelocity(stateMachine.RB);
        
        // Check for state transitions
        if (stateMachine.IsGrounded())
        {
            stateMachine.SwitchState(stateMachine.IdleState);
            return;
        }
        
        // Check for wall cling transition
        if (stateMachine.IsTouchingWall() && stateMachine.RB.linearVelocity.y <= 0)
        {
            // If we're falling against a wall, check for buffered wall jump
            if (stateMachine.HasBufferedWallJump())
            {
                stateMachine.ApplyWallJump();
                return;
            }
            
            stateMachine.SwitchState(stateMachine.WallClingState);
            return;
        }
        
        // Check for dash input
        if (stateMachine.InputReader.IsDashPressed() && stateMachine.CanDash())
        {
            stateMachine.SwitchState(stateMachine.DashState);
            return;
        }
        
        // Check for shoot input
        if (stateMachine.InputReader.IsShootPressed())
        {
            stateMachine.SwitchState(stateMachine.ShootState);
            return;
        }
        
        // Check for movement input
        Vector2 moveInput = stateMachine.GetMovementInput();
        if (moveInput != Vector2.zero)
        {
            // Apply air control
            float targetSpeed = moveInput.x * stateMachine.AirControlSpeed;
            float currentSpeed = stateMachine.RB.linearVelocity.x;
            float speedDiff = targetSpeed - currentSpeed;
            
            // Apply air control force
            float airControlForce = speedDiff * 10f;
            stateMachine.RB.linearVelocity += new Vector2(airControlForce * deltaTime, 0f);
        }
        
        // Check for extreme fall speed
        if (stateMachine.RB.linearVelocity.y < -stateMachine.MaxFallSpeed)
        {
            stateMachine.RB.linearVelocity = new Vector2(stateMachine.RB.linearVelocity.x, -stateMachine.MaxFallSpeed);
        }
    }

    public override void Exit()
    {
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[JumpState] Exiting Jump State after {Time.time - enterTime:F2}s");
        #endif
    }
}