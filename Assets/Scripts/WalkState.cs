using UnityEngine;

public class WalkState : PlayerBaseState
{
    private float walkSpeedMultiplier = 0.95f; // Walk is half speed
    private float enterTime;

    public WalkState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        enterTime = Time.time;
        // Play walk animation
        if (stateMachine.Animator != null)
        {
            stateMachine.SafePlayAnimation("Walk");
        }
        Debug.Log($"[WalkState] Entering Walk State at {enterTime:F2}s");
        // Play walk sound if needed
        // AudioManager.Instance?.Play("WalkSound");
    }

    public override void Tick(float deltaTime)
    {
        // Get movement input and update facing direction
        Vector2 moveInput = stateMachine.GetMovementInput();
        
        // Calculate target velocity
        float targetVelocityX = moveInput.x * stateMachine.MoveSpeed * walkSpeedMultiplier;
        
        // Apply movement force
        stateMachine.RB.AddForce(new Vector2(targetVelocityX, 0f));
        
        // Apply deceleration
        Vector2 decelerationForce = stateMachine.ApplyDeceleration(stateMachine.RB.linearVelocity, true, deltaTime);
        stateMachine.RB.AddForce(decelerationForce);
        
        // Clamp velocity to max speeds
        stateMachine.ClampVelocity(stateMachine.RB);
        
        // Check for state transitions
        if (stateMachine.IsGrounded())
        {
            if (moveInput == Vector2.zero)
            {
                stateMachine.SwitchState(stateMachine.IdleState);
                return;
            }
            else if (stateMachine.InputReader.IsRunPressed())
            {
                stateMachine.SwitchState(stateMachine.RunState);
                return;
            }
        }
        else
        {
            // If not grounded, transition to appropriate air state
            if (stateMachine.IsTouchingWall() && stateMachine.RB.linearVelocity.y <= 0)
            {
                stateMachine.SwitchState(stateMachine.WallClingState);
                return;
            }
            else if (stateMachine.RB.linearVelocity.y < 0)
            {
                stateMachine.SwitchState(stateMachine.FallState);
                return;
            }
            else if (stateMachine.RB.linearVelocity.y > 0)
            {
                stateMachine.SwitchState(stateMachine.JumpState);
                return;
            }
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
        
        // Check for jump input
        if (stateMachine.InputReader.IsJumpPressed() && stateMachine.CanJump())
        {
            stateMachine.SwitchState(stateMachine.JumpState);
            return;
        }

        // Update animation parameters using safe methods
        if (stateMachine.Animator != null)
        {
            stateMachine.SafeSetAnimatorFloat("Speed", Mathf.Abs(moveInput.x));
            stateMachine.SafeSetAnimatorFloat("Horizontal", moveInput.x);
        }

        // Debug: log duration in state
        float duration = Time.time - enterTime;
        if (Mathf.FloorToInt(duration * 2) % 2 == 0) // Log every half second
        {
            Debug.Log($"[WalkState] Walking for {duration:F2}s");
        }
    }

    public override void Exit()
    {
        // Optionally stop walk animation or sound
        Debug.Log($"[WalkState] Exiting Walk State after {Time.time - enterTime:F2}s");
    }
}