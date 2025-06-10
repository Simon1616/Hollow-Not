using UnityEngine;

public class PlayerIdleState : MovementState
{
    private float idleStartTime;
    private const float DECELERATION_FORCE = 20f; // 20 m/s²

    public PlayerIdleState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        stateMachine.SafePlayAnimation("Idle");
        stateMachine.SafeSetAnimatorBool("IsIdle", true);
        idleStartTime = Time.time;
        Debug.Log($"Entered Idle State at {idleStartTime}");
    }

    public override void Tick(float deltaTime)
    {
        // Apply deceleration to stop movement
        Vector2 decelerationForce = stateMachine.ApplyDeceleration(stateMachine.RB.velocity, stateMachine.IsGrounded(), deltaTime);
        stateMachine.RB.AddForce(decelerationForce);
        stateMachine.ClampVelocity(stateMachine.RB);

        // Check for state transitions
        Vector2 moveInput = stateMachine.GetMovementInput();
        if (moveInput.magnitude > 0.1f)
        {
            if (stateMachine.InputReader.IsRunPressed)
            {
                stateMachine.SwitchState(stateMachine.RunState);
                return;
            }
            else
            {
                stateMachine.SwitchState(stateMachine.WalkState);
                return;
            }
        }

        HandleStateTransitions();

        // Update animation parameters
        stateMachine.SafeSetAnimatorFloat("Speed", Mathf.Abs(stateMachine.RB.velocity.x));
        stateMachine.SafeSetAnimatorFloat("VerticalSpeed", stateMachine.RB.velocity.y);
    }

    public override void Exit()
    {
        stateMachine.SafeSetAnimatorBool("IsIdle", false);
        Debug.Log($"Exited Idle State after {Time.time - idleStartTime} seconds");
    }
}