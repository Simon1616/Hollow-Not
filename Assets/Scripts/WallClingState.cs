using UnityEngine;

public class WallClingState : PlayerBaseState
{
    private float slideSpeed = 1.5f; // Adjust this value for desired slide speed
    private float enterTime;
    private bool jumpHeldOnEnter;

    public WallClingState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        enterTime = Time.time;
        // Reset jumps when starting to wall cling
        stateMachine.JumpsRemaining = stateMachine.MaxJumps;
        jumpHeldOnEnter = stateMachine.InputReader.IsJumpPressed();
        Debug.Log($"[WallClingState] Entering Wall Cling State at {enterTime:F2}s");

        if (stateMachine.Animator != null)
            stateMachine.Animator.Play("Cling");

        // Optional: Play wall cling animation
        // if (stateMachine.Animator != null)
        //     stateMachine.Animator.Play("WallClingAnimation"); // Replace with your animation name

        // Reduce initial vertical velocity slightly to make the cling feel better
        if (stateMachine.RB != null)
        {
            stateMachine.RB.linearVelocity = new Vector2(stateMachine.RB.linearVelocity.x, Mathf.Clamp(stateMachine.RB.linearVelocity.y, -slideSpeed, float.MaxValue));
        }
    }

    public override void Tick(float deltaTime)
    {
        // Check for Shoot input first
        if (stateMachine.InputReader.IsShootPressed())
        {
            stateMachine.SwitchState(stateMachine.ShootState);
            return;
        }

        // Get movement input
        Vector2 moveInput = stateMachine.InputReader.GetMovementInput();

        // Apply horizontal movement while maintaining downward slide
        if (stateMachine.RB != null)
        {
            // Calculate horizontal velocity based on input
            float targetVelocityX = moveInput.x * stateMachine.MoveSpeed * 0.5f; // Reduced speed while wall clinging
            
            // Apply the velocities
            stateMachine.RB.linearVelocity = new Vector2(targetVelocityX, -slideSpeed);
        }

        // Check for jump input to perform a wall jump
        if (stateMachine.InputReader.IsJumpPressed() && !jumpHeldOnEnter)
        {
            stateMachine.SwitchState(stateMachine.JumpState);
            return;
        }
        // Update lockout: if jump is released, allow wall jump again
        if (!stateMachine.InputReader.IsJumpPressed())
        {
            jumpHeldOnEnter = false;
        }

        // Check if grounded
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

        // Check if no longer touching the wall
        if (!stateMachine.IsTouchingWall())
        {
            stateMachine.SwitchState(stateMachine.FallState);
            return;
        }
    }

    public override void Exit()
    {
        Debug.Log($"[WallClingState] Exiting Wall Cling State after {Time.time - enterTime:F2}s");
        // Reset gravity if it was modified, or ensure velocity isn't stuck at slideSpeed
        // The JumpState or other subsequent states should handle setting appropriate velocities.
    }
}