using UnityEngine;

public class PlayerDashState : MovementState
{
    private float dashStartTime;
    private const float DASH_DURATION = 0.2f; // 200ms
    private const float DASH_SPEED = 15f; // 15 m/s
    private Vector2 dashDirection;

    public PlayerDashState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        stateMachine.SafePlayAnimation("Dash");
        dashStartTime = Time.time;
        Debug.Log($"Entered Dash State at {dashStartTime}");

        // Determine dash direction
        Vector2 moveInput = stateMachine.GetMovementInput();
        dashDirection = moveInput.magnitude > 0.1f ? moveInput.normalized : new Vector2(stateMachine.transform.right.x, 0f);

        // Apply initial dash force
        stateMachine.RB.velocity = dashDirection * DASH_SPEED;
    }

    public override void Tick(float deltaTime)
    {
        // Check if dash duration has elapsed
        if (Time.time - dashStartTime >= DASH_DURATION)
        {
            // Transition to appropriate state based on conditions
            if (stateMachine.IsGrounded())
            {
                if (stateMachine.GetMovementInput().magnitude < 0.1f)
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
            else
            {
                if (stateMachine.RB.velocity.y < 0)
                {
                    stateMachine.SwitchState(stateMachine.FallState);
                    return;
                }
                else
                {
                    stateMachine.SwitchState(stateMachine.JumpState);
                    return;
                }
            }
        }

        // Maintain dash velocity
        stateMachine.RB.velocity = dashDirection * DASH_SPEED;

        // Update animation parameters
        stateMachine.SafeSetAnimatorFloat("Speed", Mathf.Abs(stateMachine.RB.velocity.x));
        stateMachine.SafeSetAnimatorFloat("VerticalSpeed", stateMachine.RB.velocity.y);
    }

    public override void Exit()
    {
        Debug.Log($"Exited Dash State after {Time.time - dashStartTime} seconds");
    }
} 