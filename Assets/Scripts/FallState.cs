using UnityEngine;

public class FallState : PlayerBaseState
{
    private float enterTime;

    public FallState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        enterTime = Time.time;
        // Play fall animation if available
        stateMachine.SafePlayAnimation("Fall");
        
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[FallState] Entering Fall State at {enterTime:F2}s");
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
        
        if (stateMachine.IsTouchingWall() && stateMachine.RB.linearVelocity.y <= 0)
        {
            stateMachine.SwitchState(stateMachine.WallClingState);
            return;
        }
        
        // Check for jump input
        if (stateMachine.InputReader.IsJumpPressed() && stateMachine.CanJump())
        {
            stateMachine.SwitchState(stateMachine.JumpState);
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
            float airControlForce = speedDiff * 10f; // Adjust this multiplier to control air control strength
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
        Debug.Log($"[FallState] Exiting Fall State after {Time.time - enterTime:F2}s");
        #endif
    }
}