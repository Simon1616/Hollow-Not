using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    public PlayerIdleState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        // Stop horizontal movement if any residual velocity
        if (stateMachine.RB != null)
            stateMachine.RB.linearVelocity = new Vector2(0f, stateMachine.RB.linearVelocity.y);
        // Play Idle Animation (Example)
        // stateMachine.Animator.Play("IdleAnimationName");
        Debug.Log("Entering Idle State");

        if (stateMachine.Animator != null)
            stateMachine.Animator.Play("Idle");
    }

    public override void Tick(float deltaTime)
    {
        // Ensure we have all required components
        if (stateMachine == null || stateMachine.RB == null || stateMachine.InputReader == null)
        {
            Debug.LogError("[PlayerIdleState] Required components are null. Cannot process state.");
            return;
        }

        // --- NEW: Check for loss of ground or wall contact ---
        if (!stateMachine.IsGrounded())
        {
            // If touching wall and falling, go to WallClingState
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

        // Check for Shoot input
        if (stateMachine.InputReader.IsShootPressed())
        {
            stateMachine.SwitchState(stateMachine.ShootState);
            return;
        }

        // Check for Jump input
        if (stateMachine.InputReader.IsJumpPressed())
        {
            Debug.Log($"[IdleState] Jump pressed. IsGrounded: {stateMachine.IsGrounded()}, CanJump: {stateMachine.CanJump()}");
            // Check if we can jump
            if (stateMachine.CanJump())
            {
                stateMachine.SwitchState(stateMachine.JumpState);
            }
            return;
        }

        // Check for movement input to transition to Walk/Run
        Vector2 moveInput = stateMachine.InputReader.GetMovementInput();
        if (moveInput != Vector2.zero)
        {
            if (stateMachine.InputReader.IsRunPressed())
                stateMachine.SwitchState(stateMachine.RunState);
            else
                stateMachine.SwitchState(stateMachine.WalkState);
        }
    }

    public override void Exit()
    {
         Debug.Log("Exiting Idle State");
    }
}