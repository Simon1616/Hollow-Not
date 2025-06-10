using UnityEngine;

public class FallState : MovementState
{
    private float fallStartTime;
    private const float FALL_ACCELERATION = 20f; // 20 m/s²
    private const float MAX_FALL_SPEED = 10f; // 10 m/s

    public FallState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        stateMachine.SafePlayAnimation("Fall");
        fallStartTime = Time.time;
        Debug.Log($"Entered Fall State at {fallStartTime}");
    }

    public override void Tick(float deltaTime)
    {
        // Apply fall acceleration
        stateMachine.RB.AddForce(Vector2.down * FALL_ACCELERATION * stateMachine.RB.mass, ForceMode2D.Force);

        // Apply movement in air
        Vector2 moveInput = stateMachine.GetMovementInput();
        float targetVelocityX = moveInput.x * stateMachine.AirControlSpeed;
        
        // Calculate acceleration force
        float currentVelocityX = stateMachine.RB.velocity.x;
        float velocityDifference = targetVelocityX - currentVelocityX;
        float accelerationForce = velocityDifference * stateMachine.RB.mass * 40f; // 40 m/s² air acceleration
        
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
        if (stateMachine.IsGrounded())
        {
            if (moveInput.magnitude < 0.1f)
            {
                stateMachine.SwitchState(stateMachine.IdleState);
                return;
            }
            else if (stateMachine.InputReader.IsRunPressed)
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

        // Check for wall cling
        if (stateMachine.IsTouchingWall() && stateMachine.RB.velocity.y <= 0)
        {
            stateMachine.SwitchState(stateMachine.WallClingState);
            return;
        }

        HandleStateTransitions();
    }

    public override void Exit()
    {
        Debug.Log($"Exited Fall State after {Time.time - fallStartTime} seconds");
    }
}