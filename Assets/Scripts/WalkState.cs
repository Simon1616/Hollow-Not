using UnityEngine;

public class WalkState : PlayerBaseState
{
    private float walkSpeedMultiplier = 0.5f; // Walk is half speed
    private float enterTime;

    public WalkState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        enterTime = Time.time;
        // Play walk animation
        if (stateMachine.Animator != null)
            stateMachine.Animator.Play("Walk");
        Debug.Log($"[WalkState] Entering Walk State at {enterTime:F2}s");
        // Play walk sound if needed
        // AudioManager.Instance?.Play("WalkSound");
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

        Vector2 moveInput = stateMachine.GetMovementInput();

        // Check for Crouch input if grounded
        if (stateMachine.IsGrounded() && stateMachine.InputReader.IsCrouchHeld()) // Use InputReader property
        {
            stateMachine.SwitchState(stateMachine.CrouchState);
            return; // Exit early after state switch
        }


        // Check for Jump input
        if (stateMachine.InputReader.IsJumpPressed() && stateMachine.JumpsRemaining > 0)
        {
            stateMachine.SwitchState(stateMachine.JumpState);
            return; // Exit early after state switch
        }


        // Walk/Run toggle
        if (stateMachine.InputReader.IsRunPressed()) // Use InputReader property
        {
            stateMachine.SwitchState(stateMachine.RunState);
            return;
        }

        if (moveInput == Vector2.zero)
        {
            stateMachine.SwitchState(stateMachine.IdleState);
            return;
        }

        // Apply walk movement (only affect horizontal velocity)
        float targetVelocityX = moveInput.x * stateMachine.MoveSpeed * walkSpeedMultiplier;
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
            Debug.Log($"[WalkState] Walking for {duration:F1} seconds");
        }
    }

    public override void Exit()
    {
        // Optionally stop walk animation or sound
        Debug.Log($"[WalkState] Exiting Walk State after {Time.time - enterTime:F2}s");
    }
}