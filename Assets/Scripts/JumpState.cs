using UnityEngine;

public class JumpState : MovementState
{
    private float jumpStartTime;
    private const float MAX_JUMP_DURATION = 0.3f; // 300ms
    private const float MIN_JUMP_DURATION = 0.1f; // 100ms
    private const float JUMP_HEIGHT_MULTIPLIER = 1.2f; // 120% of base jump height
    private bool hasAppliedInitialForce = false;

    public JumpState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        stateMachine.SafePlayAnimation("Jump");
        jumpStartTime = Time.time;
        hasAppliedInitialForce = false;
        Debug.Log($"Entered Jump State at {jumpStartTime}");

        // Apply initial jump force immediately
        float jumpForce = stateMachine.JumpForce * JUMP_HEIGHT_MULTIPLIER;
        stateMachine.RB.velocity = new Vector2(stateMachine.RB.velocity.x, 0f); // Reset vertical velocity
        stateMachine.RB.AddForce(Vector2.up * jumpForce * stateMachine.RB.mass, ForceMode2D.Impulse);
        hasAppliedInitialForce = true;
    }

    public override void Tick(float deltaTime)
    {
        // Handle variable jump height
        if (!stateMachine.InputReader.IsJumpHeld && Time.time - jumpStartTime > MIN_JUMP_DURATION)
        {
            // Apply downward force to reduce jump height
            stateMachine.RB.AddForce(Vector2.down * stateMachine.RB.mass * 2f, ForceMode2D.Force);
        }

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
        if (stateMachine.RB.velocity.y < 0)
        {
            stateMachine.SwitchState(stateMachine.FallState);
            return;
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
        Debug.Log($"Exited Jump State after {Time.time - jumpStartTime} seconds");
    }
}