using UnityEngine;

public class FallState : PlayerBaseState
{
    private float enterTime;

    public FallState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        enterTime = Time.time;
        // Play fall animation if available
        if (stateMachine.Animator != null)
            stateMachine.Animator.Play("Fall");
        Debug.Log($"[FallState] Entering Fall State at {enterTime:F2}s");
    }

    public override void Tick(float deltaTime)
    {
        // Allow air control
        Vector2 moveInput = stateMachine.GetMovementInput();
        float targetVelocityX = moveInput.x * stateMachine.GetHorizontalSpeed(
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

        // If grounded, transition to Idle/Walk/Run
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

        // Check for wall cling
        if (stateMachine.IsTouchingWall() && stateMachine.RB.linearVelocity.y <= 0)
        {
            stateMachine.SwitchState(stateMachine.WallClingState);
            return;
        }

        // Check for double jump
        if (stateMachine.InputReader.IsJumpPressed() && stateMachine.JumpsRemaining > 0 && stateMachine.CanJump())
        {
            stateMachine.SwitchState(stateMachine.JumpState);
            return;
        }

        // Check for Dash input
        if (stateMachine.InputReader.IsDashPressed() && stateMachine.CanDash())
        {
            float direction = Mathf.Sign(moveInput.x);
            if (direction != 0)
            {
                stateMachine.StartDash(direction);
                return;
            }
        }

        // Allow shooting in air
        if (stateMachine.InputReader.IsShootPressed())
        {
            stateMachine.SwitchState(stateMachine.ShootState);
            return;
        }
    }

    public override void Exit()
    {
        Debug.Log($"[FallState] Exiting Fall State after {Time.time - enterTime:F2}s");
    }
}