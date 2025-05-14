using UnityEngine;

public class PlayerDashState : PlayerBaseState
{
    private float enterTime;
    private float dashDirection;

    public PlayerDashState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        enterTime = Time.time;
        
        // Get direction based on player facing
        dashDirection = stateMachine.IsFacingRight ? 1f : -1f;
        
        // Reset velocity
        stateMachine.RB.linearVelocity = Vector2.zero;
        
        // Disable gravity during dash
        stateMachine.RB.gravityScale = 0f;
        
        // Apply initial dash velocity
        stateMachine.RB.linearVelocity = new Vector2(dashDirection * stateMachine.DashSpeed, 0f);
        
        // Record dash start
        stateMachine.OnDashStart();
        
        // Play Run animation instead of Dash for dashing
        if (stateMachine.Animator != null)
        {
            stateMachine.SafePlayAnimation("Run");
        }
        
        Debug.Log($"[PlayerDashState] Entering Dash State, Direction: {dashDirection}");
    }

    public override void Tick(float deltaTime)
    {
        // Keep velocity consistent during dash
        stateMachine.RB.linearVelocity = new Vector2(dashDirection * stateMachine.DashSpeed, 0f);
        
        // Check if dash duration is complete
        if (!stateMachine.IsDashing())
        {
            // Dash is complete, switch to appropriate state
            if (stateMachine.IsGrounded())
            {
                Vector2 moveInput = stateMachine.GetMovementInput();
                if (moveInput == Vector2.zero)
                    stateMachine.SwitchState(stateMachine.IdleState);
                else if (stateMachine.InputReader.IsRunPressed())
                    stateMachine.SwitchState(stateMachine.RunState);
                else
                    stateMachine.SwitchState(stateMachine.WalkState);
            }
            else if (stateMachine.IsTouchingWall() && stateMachine.RB.linearVelocity.y <= 0)
            {
                stateMachine.SwitchState(stateMachine.WallClingState);
            }
            else
            {
                stateMachine.SwitchState(stateMachine.FallState);
            }
            return;
        }
        
        // No input is processed during dash as per requirements
    }

    public override void Exit()
    {
        // Reset gravity to default
        stateMachine.RB.gravityScale = 1f;
        
        Debug.Log($"[PlayerDashState] Exiting Dash State after {Time.time - enterTime:F2}s");
    }
} 