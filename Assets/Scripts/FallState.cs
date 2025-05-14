using UnityEngine;

public class FallState : PlayerBaseState
{
    private float enterTime;

    public FallState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        enterTime = Time.time;
        // Play fall animation if available
        stateMachine.SafePlayAnimation("Fall");
        
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[FallState] Entering Fall State at {enterTime:F2}s");
        #endif
    }

    public override void Tick(float deltaTime)
    {
        // Allow air control
        Vector2 moveInput = stateMachine.GetMovementInput();
        float targetVelocityX = moveInput.x * stateMachine.GetHorizontalSpeed(
            stateMachine.IsGrounded(),
            stateMachine.IsTouchingWall() && stateMachine.RB.linearVelocity.y <= 0
        );
        
        // Calculate deceleration force
        float decelerationX = stateMachine.GetDecelerationX(stateMachine.IsGrounded());
        float currentVelocityX = stateMachine.RB.linearVelocity.x;
        float forceX = (targetVelocityX - currentVelocityX) * decelerationX;
        
        // Apply horizontal movement with deceleration
        if (stateMachine.RB != null)
        {
            // Apply movement force
            stateMachine.RB.AddForce(new Vector2(forceX, 0f));
            
            // Apply deceleration
            Vector2 decelerationForce = stateMachine.ApplyDeceleration(stateMachine.RB.linearVelocity, stateMachine.IsGrounded(), deltaTime);
            stateMachine.RB.AddForce(decelerationForce);
            
            // Limit fall speed to match the configured max fall speed in PlayerStateMachine
            float fallSpeed = Mathf.Abs(stateMachine.RB.linearVelocity.y);
            if (fallSpeed > stateMachine.maxFallSpeed)
            {
                Vector2 clampedVelocity = stateMachine.RB.linearVelocity;
                clampedVelocity.y = -stateMachine.maxFallSpeed;
                stateMachine.RB.linearVelocity = clampedVelocity;
                
                // Log when clamping speed for debugging, but only occasionally
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (Time.frameCount % 30 == 0) // Only log every 30 frames to avoid spam
                {
                    Debug.Log($"[FallState] Clamped fall speed from {fallSpeed} to {stateMachine.maxFallSpeed}");
                }
                #endif
            }
        }

        // Clamp velocity to max speeds
        stateMachine.ClampVelocity(stateMachine.RB);

        // Ground detection for all fall speeds
        bool isGrounded = stateMachine.IsGrounded();
        if (!isGrounded && stateMachine.RB != null && stateMachine.RB.linearVelocity.y < 0f) // Any downward motion
        {
            // Track fall speed for ground detection logic
            float fallSpeed = Mathf.Abs(stateMachine.RB.linearVelocity.y);
            
            // Get current position for expanded check
            Vector2 position = stateMachine.transform.position;
            if (stateMachine.playerCollider != null)
            {
                position = new Vector2(position.x, position.y - stateMachine.playerCollider.bounds.extents.y);
            }
            
            // Calculate ray distance based on fall speed
            float fallSpeedFactor = fallSpeed / 10f;
            float rayDistance = 0.3f * Mathf.Max(1f, fallSpeedFactor); // Scale with fall speed, at least 0.3
            
            // Cast multiple rays for better coverage
            float rayWidth = 0.2f;
            RaycastHit2D[] hits = new RaycastHit2D[3];
            hits[0] = Physics2D.Raycast(position + new Vector2(-rayWidth, 0), Vector2.down, rayDistance, stateMachine.groundLayer);
            hits[1] = Physics2D.Raycast(position, Vector2.down, rayDistance, stateMachine.groundLayer);
            hits[2] = Physics2D.Raycast(position + new Vector2(rayWidth, 0), Vector2.down, rayDistance, stateMachine.groundLayer);
            
            // Debug visualization only in editor
            #if UNITY_EDITOR
            Debug.DrawRay(position + new Vector2(-rayWidth, 0), Vector2.down * rayDistance, hits[0].collider != null ? Color.green : Color.red, 0.1f);
            Debug.DrawRay(position, Vector2.down * rayDistance, hits[1].collider != null ? Color.green : Color.red, 0.1f);
            Debug.DrawRay(position + new Vector2(rayWidth, 0), Vector2.down * rayDistance, hits[2].collider != null ? Color.green : Color.red, 0.1f);
            #endif
            
            // If any ray hits, we're close to ground
            bool hitDetected = false;
            float closestDistance = float.MaxValue;
            Vector2 hitPoint = Vector2.zero;
            
            foreach (var hit in hits)
            {
                if (hit.collider != null && hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    hitPoint = hit.point;
                    hitDetected = true;
                }
            }
            
            // Enhanced ground detection and snapping
            if (hitDetected)
            {
                // Determine when to snap based on fall speed and distance
                float snapThreshold = Mathf.Lerp(0.03f, 0.1f, fallSpeedFactor / 3f); // Less aggressive thresholds
                
                if (closestDistance < snapThreshold || fallSpeed > stateMachine.maxFallSpeed * 0.88f) // 88% of max fall speed
                {
                    // More precise height adjustment - move exactly to the collision point plus a small offset
                    float heightOffset = 0.02f; // Smaller offset to avoid oversnapping
                    
                    // Calculate exact position to place the player
                    float targetY = hitPoint.y + heightOffset;
                    if (stateMachine.playerCollider != null)
                    {
                        // Add the collider height (from bottom to center)
                        targetY += stateMachine.playerCollider.bounds.extents.y;
                    }
                    
                    // Move to the calculated position
                    stateMachine.transform.position = new Vector3(
                        stateMachine.transform.position.x,
                        targetY,
                        stateMachine.transform.position.z
                    );
                    
                    // Zero out vertical velocity
                    stateMachine.RB.linearVelocity = new Vector2(stateMachine.RB.linearVelocity.x, 0);
                    
                    // Now we're definitely grounded
                    isGrounded = true;
                    
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    // Limit debug messages frequency
                    if (Time.frameCount % 20 == 0)
                    {
                        Debug.Log($"[FallState] Positioned to ground. Distance: {closestDistance}, Speed: {fallSpeed}");
                    }
                    #endif
                }
                else if (closestDistance < rayDistance * 0.75f) // Reduced distance for grounded detection
                {
                    // We're close enough to ground to consider ourselves grounded
                    isGrounded = true;
                }
            }
        }

        // If grounded, transition to Idle/Walk/Run
        if (isGrounded)
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

        // Check for wall cling
        if (stateMachine.IsTouchingWall() && stateMachine.RB.linearVelocity.y <= 0)
        {
            stateMachine.SwitchState(stateMachine.WallClingState);
            return;
        }

        // Check for double jump
        if (stateMachine.InputReader.IsJumpPressed() && stateMachine.JumpsRemaining > 0 && stateMachine.CanJump())
        {
            stateMachine.SwitchState(stateMachine.JumpState);
            return;
        }

        // Check for Dash input
        if (stateMachine.InputReader.IsDashPressed() && stateMachine.CanDash())
        {
            float direction = Mathf.Sign(moveInput.x);
            if (direction != 0)
            {
                stateMachine.StartDash(direction);
                return;
            }
        }

        // Allow shooting in air
        if (stateMachine.InputReader.IsShootPressed())
        {
            stateMachine.SwitchState(stateMachine.ShootState);
            return;
        }
    }

    public override void Exit()
    {
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[FallState] Exiting Fall State after {Time.time - enterTime:F2}s");
        #endif
    }
}