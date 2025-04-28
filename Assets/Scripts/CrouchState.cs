using UnityEngine;

public class CrouchState : PlayerBaseState
{
    private float enterTime;
    private float crouchMoveSpeed;

    public CrouchState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
        // Calculate actual crouch speed based on multipliers
        // Walk speed is 0.5 * MoveSpeed, Crouch is half of Walk speed (0.25 * MoveSpeed)
        crouchMoveSpeed = stateMachine.MoveSpeed * stateMachine.CrouchSpeedMultiplier;
    }

    public override void Enter()
    {
        enterTime = Time.time;
        stateMachine.SetColliderCrouching();
        // Play crouch animation
        if (stateMachine.Animator != null)
            stateMachine.Animator.Play("Jump"); // Replace with actual animation name
        Debug.Log($"[CrouchState] Entering Crouch State at {enterTime:F2}s");
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

        // --- Check for Exit Conditions ---

        // 1. Crouch key released?
        if (!stateMachine.InputReader.IsCrouchHeld()) // Use InputReader property
        {
            // Check if space to stand up
            if (stateMachine.CanStandUp())
            {
                // Determine next state based on input
                Vector2 moveInputCheck = stateMachine.InputReader.GetMovementInput(); // Use InputReader property
                if (moveInputCheck == Vector2.zero)
                {
                    stateMachine.SwitchState(stateMachine.IdleState);
                }
                else
                {
                    if (stateMachine.InputReader.IsRunPressed()) // Use InputReader property
                        stateMachine.SwitchState(stateMachine.RunState);
                    else
                        stateMachine.SwitchState(stateMachine.WalkState);
                }
                return; // Exit after state switch
            }
            else
            {
                // Cannot stand up, remain crouching (play bump sound/effect?)
                Debug.Log("[CrouchState] Cannot stand up, obstacle detected.");
            }
        }

        // 2. No longer grounded? (Handled above)

        // --- Apply Crouch Movement ---
        Vector2 moveInput = stateMachine.InputReader.GetMovementInput(); // Use InputReader property
        // Apply movement using the calculated crouch speed
        stateMachine.RB.linearVelocity = moveInput * crouchMoveSpeed;

        // Update animation blend tree if needed
        if (stateMachine.Animator != null && moveInput != Vector2.zero)
        {
            stateMachine.Animator.SetFloat("Horizontal", moveInput.x);
            // stateMachine.Animator.SetFloat("Vertical", moveInput.y); // If needed
        }
    }

    public override void Exit()
    {
        // Only restore collider if we could actually stand up (handled in Tick)
        // Ensure collider is restored if exiting for other reasons (e.g., death state)
        // We rely on the Tick logic ensuring CanStandUp before switching state.
         if (stateMachine.CanStandUp()) // Double check on exit
         {
            stateMachine.SetColliderStanding();
         }
         else
         {
            // This case should ideally not happen if Tick logic is correct.
            // If it does, we might be stuck crouching.
            Debug.LogError("[CrouchState] Exiting state but cannot stand up!");
         }

        // Stop crouch animation if needed
        // if (stateMachine.Animator != null)
        //     stateMachine.Animator.StopPlayback(); // Or transition to appropriate animation

        Debug.Log($"[CrouchState] Exiting Crouch State after {Time.time - enterTime:F2}s");
    }
}