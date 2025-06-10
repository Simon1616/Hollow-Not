using UnityEngine;

public class WalkState : MovementState
{
    private float walkStartTime;
    private const float WALK_SPEED_MULTIPLIER = 0.7f; // 70% of base speed

    public WalkState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        stateMachine.SafePlayAnimation("Walk");
        walkStartTime = Time.time;
        Debug.Log($"Entered Walk State at {walkStartTime}");
    }

    public override void Tick(float deltaTime)
    {
        // Apply movement with walk speed multiplier
        Vector2 moveInput = stateMachine.GetMovementInput();
        float targetVelocityX = moveInput.x * stateMachine.MoveSpeed * WALK_SPEED_MULTIPLIER;
        
        // Calculate acceleration force
        float currentVelocityX = stateMachine.RB.velocity.x;
        float velocityDifference = targetVelocityX - currentVelocityX;
        float accelerationForce = velocityDifference * stateMachine.RB.mass * 50f; // 50 m/s² acceleration
        
        // Apply force in newtons (mass * acceleration)
        stateMachine.RB.AddForce(new Vector2(accelerationForce, 0f));
        
        // Apply deceleration
        Vector2 decelerationForce = stateMachine.ApplyDeceleration(stateMachine.RB.velocity, stateMachine.IsGrounded(), deltaTime);
        stateMachine.RB.AddForce(decelerationForce);
        stateMachine.ClampVelocity(stateMachine.RB);

        // Update animation parameters
        stateMachine.SafeSetAnimatorFloat("Speed", Mathf.Abs(stateMachine.RB.velocity.x));
        stateMachine.SafeSetAnimatorFloat("VerticalSpeed", stateMachine.RB.velocity.y);

        // Check for state transitions
        if (moveInput.magnitude < 0.1f)
        {
            stateMachine.SwitchState(stateMachine.IdleState);
            return;
        }

        if (stateMachine.InputReader.IsRunPressed)
        {
            stateMachine.SwitchState(stateMachine.RunState);
            return;
        }

        HandleStateTransitions();
    }

    public override void Exit()
    {
        Debug.Log($"Exited Walk State after {Time.time - walkStartTime} seconds");
    }
}