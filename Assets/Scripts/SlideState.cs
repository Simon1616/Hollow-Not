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
        }

        // Play slide animation
        if (stateMachine.Animator != null)
            stateMachine.Animator.Play("Slide"); // Ensure this animation exists

        Debug.Log($"[SlideState] Entering Slide State at {slideStartTime:F2}s");
        // Apply initial slide impulse or set velocity
        // stateMachine.RB.velocity = slideDirection * stateMachine.SlideSpeed; // Need SlideSpeed property

        // Adjust collider size/shape for sliding
        // stateMachine.Collider.height = stateMachine.SlideColliderHeight; // Need properties
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

        // Apply sliding physics (e.g., decreasing velocity over time)
        // float slideFactor = 1 - (timeSinceSlideStarted / slideDuration);
        // stateMachine.RB.velocity = slideDirection * stateMachine.SlideSpeed * slideFactor;

        // Check for slide end condition (duration elapsed, collision, etc.)
        if (timeSinceSlideStarted >= slideDuration)
        {
            // Transition back to a grounded state like Idle or Crouch
            // Check if crouch is still held to decide between CrouchState or Idle/WalkState
            if (stateMachine.InputReader.IsCrouchHeld()) // Use InputReader property
            {
                 stateMachine.SwitchState(stateMachine.CrouchState);
            }
            else
            {
                 stateMachine.SwitchState(stateMachine.IdleState); // Or WalkState if moving
            }
            return;
        }

        // Optional: Allow slight direction control during slide?
        // Vector2 moveInput = stateMachine.GetMovementInput();
        // Apply influence based on moveInput

        // Debug log
        if (Mathf.FloorToInt(timeSinceSlideStarted * 2) % 2 == 0) // Log every half second
        {
             Debug.Log($"[SlideState] Sliding for {timeSinceSlideStarted:F1} seconds");
        }
    }

    public override void Exit()
    {
        // Restore collider size/shape
        // stateMachine.Collider.height = stateMachine.OriginalColliderHeight; // Need properties

        // Ensure velocity is reasonable upon exiting slide
        // stateMachine.RB.velocity *= 0.5f; // Example: reduce speed slightly

        Debug.Log($"[SlideState] Exiting Slide State after {Time.time - slideStartTime:F2}s");
    }
}