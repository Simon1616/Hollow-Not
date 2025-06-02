using UnityEngine;

public class RunState : PlayerBaseState
{
    private float enterTime;

    public RunState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        enterTime = Time.time;
        // Play run animation
        if (stateMachine.Animator != null)
        {
            stateMachine.SafePlayAnimation("Run");
        }
        Debug.Log($"[RunState] Entering Run State at {enterTime:F2}s");
        // Play run sound if needed
        // AudioManager.Instance?.Play("RunSound");
    }

    public override void Tick(float deltaTime)
    {
        // Get movement input and update facing direction
        Vector2 moveInput = stateMachine.GetMovementInput();
        
        // Calculate target velocity
        float targetVelocityX = moveInput.x * stateMachine.MoveSpeed * 1.5f; // Run is 1.5x walk speed
        
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
            else if (!stateMachine.InputReader.IsRunPressed())
            {
                stateMachine.SwitchState(stateMachine.WalkState);
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
        if (duration > 0 && Mathf.FloorToInt(duration) % 2 == 0)
        {
            Debug.Log($"[RunState] Running for {duration:F1} seconds");
        }
    }

    // Helper method to check if an animator parameter exists
    private bool HasAnimatorParameter(Animator animator, string paramName)
    {
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName)
            {
                return true;
            }
        }
        return false;
    }

    public override void Exit()
    {
        // Optionally stop run animation or sound
        Debug.Log($"[RunState] Exiting Run State after {Time.time - enterTime:F2}s");
    }
}