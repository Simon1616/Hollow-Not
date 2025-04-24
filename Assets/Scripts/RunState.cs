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
            stateMachine.Animator.Play("Run");
        Debug.Log($"[RunState] Entering Run State at {enterTime:F2}s");
        // Play run sound if needed
        // AudioManager.Instance?.Play("RunSound");
    }

    public override void Tick(float deltaTime)
    {
        // --- NEW: Check for loss of ground or wall contact ---
        if (!stateMachine.IsGrounded())
        {
            if (stateMachine.IsTouchingWall() && stateMachine.RB.linearVelocity.y <= 0)
            {
                stateMachine.SwitchState(stateMachine.WallClingState);
            }
            else
            {
                stateMachine.SwitchState(stateMachine.FallState);
            }
            return;
        }

        // Check for Shoot input first
        if (stateMachine.InputReader.IsShootPressed()) // Use InputReader property
        {
            stateMachine.SwitchState(stateMachine.ShootState);
            return; // Exit early
        }

        Vector2 moveInput = stateMachine.InputReader.GetMovementInput(); // Use InputReader property

        // Walk/Run toggle
        // Check for Crouch input FIRST if grounded
        // Check for Slide input FIRST if grounded and moving
        if (stateMachine.IsGrounded() && stateMachine.InputReader.IsCrouchHeld()) // Use InputReader property
        {
            stateMachine.SwitchState(stateMachine.SlideState); // Transition to SlideState
            return; // Exit early after state switch
        }

        // Check for Jump input
        if (stateMachine.InputReader.IsJumpPressed() && stateMachine.JumpsRemaining > 0)
        {
            stateMachine.SwitchState(stateMachine.JumpState);
            return; // Exit early after state switch
        }


        if (stateMachine.InputReader.IsRunPressed()) {
            stateMachine.SwitchState(stateMachine.RunState);
        }
        else
        {
            stateMachine.SwitchState(stateMachine.WalkState);
            return;
        }

        if (moveInput == Vector2.zero)
        {
            stateMachine.SwitchState(stateMachine.IdleState);
            return;
        }

        // Apply run movement (full speed, only affect horizontal velocity)
        float targetVelocityX = moveInput.x * stateMachine.MoveSpeed;
        stateMachine.RB.linearVelocity = new Vector2(targetVelocityX, stateMachine.RB.linearVelocity.y); // Preserve Y velocity

        // Optionally update animation direction
        if (stateMachine.Animator != null)
        {
            stateMachine.Animator.SetFloat("Horizontal", moveInput.x);
            stateMachine.Animator.SetFloat("Vertical", moveInput.y);
        }

        // Debug: log duration in state
        float duration = Time.time - enterTime;
        if (duration > 0 && Mathf.FloorToInt(duration) % 2 == 0)
        {
            Debug.Log($"[RunState] Running for {duration:F1} seconds");
        }
    }

    public override void Exit()
    {
        // Optionally stop run animation or sound
        Debug.Log($"[RunState] Exiting Run State after {Time.time - enterTime:F2}s");
    }
}