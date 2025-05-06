using UnityEngine;

public class SlideState : PlayerBaseState
{
    private float slideStartTime;
    private float slideDuration = 1.0f; // Example duration, adjust as needed
    private Vector2 slideDirection;

    public SlideState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        slideStartTime = Time.time;
        slideDirection = stateMachine.InputReader.GetMovementInput().normalized; // Use InputReader property
        if (slideDirection == Vector2.zero)
        {
            // If no input, slide in the direction the player was last moving, or default forward
            // This needs refinement based on how movement direction is tracked
            slideDirection = stateMachine.transform.forward; // Placeholder

            if (stateMachine.Animator != null)
                stateMachine.Animator.Play("Slide");
        }

        // Play slide animation
        if (stateMachine.Animator != null)
            stateMachine.Animator.Play("Slide"); // Ensure this animation exists

        Debug.Log($"[SlideState] Entering Slide State at {slideStartTime:F2}s");
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

        float timeSinceSlideStarted = Time.time - slideStartTime;

        // Check for slide end condition (duration elapsed, collision, etc.)
        if (timeSinceSlideStarted >= slideDuration)
        {
            // Transition back to a grounded state
            Vector2 moveInput = stateMachine.InputReader.GetMovementInput();
            if (moveInput == Vector2.zero)
                stateMachine.SwitchState(stateMachine.IdleState);
            else if (stateMachine.InputReader.IsRunPressed())
                stateMachine.SwitchState(stateMachine.RunState);
            else
                stateMachine.SwitchState(stateMachine.WalkState);
            return;
        }

        // Debug log
        if (Mathf.FloorToInt(timeSinceSlideStarted * 2) % 2 == 0) // Log every half second
        {
            Debug.Log($"[SlideState] Sliding for {timeSinceSlideStarted:F2}s");
        }
    }

    public override void Exit()
    {
        Debug.Log($"[SlideState] Exiting Slide State after {Time.time - slideStartTime:F2}s");
    }
}