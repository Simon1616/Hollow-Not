using UnityEngine;

public class InputReader : MonoBehaviour
{
    // Consider using Unity's new Input System for more robust handling
    // For now, using the legacy Input Manager

    public Vector2 Movement { get; private set; }
    public bool IsRunPressed { get; private set; }
    public bool IsJumpPressed { get; private set; }
    public bool IsJumpHeld { get; private set; }
    public bool IsDashPressed { get; private set; }

    // Input buffer settings
    private const float JUMP_BUFFER_TIME = 0.1f; // 100ms buffer for jump input
    private float jumpBufferTimer;
    private bool wasJumpPressed;

    private void Update()
    {
        // Update movement input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Movement = new Vector2(horizontal, vertical).normalized;

        // Update button states
        IsRunPressed = Input.GetKey(KeyCode.LeftShift);
        IsDashPressed = Input.GetKeyDown(KeyCode.LeftControl);

        // Handle jump input with buffer
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space);
        IsJumpHeld = Input.GetKey(KeyCode.Space);

        if (jumpPressed)
        {
            jumpBufferTimer = JUMP_BUFFER_TIME;
            wasJumpPressed = true;
        }

        if (jumpBufferTimer > 0)
        {
            jumpBufferTimer -= Time.deltaTime;
            IsJumpPressed = true;
        }
        else
        {
            IsJumpPressed = false;
        }

        // Reset jump buffer if jump is released
        if (!IsJumpHeld && wasJumpPressed)
        {
            wasJumpPressed = false;
            jumpBufferTimer = 0;
        }
    }

    public Vector2 GetMovementInput()
    {
        // Use GetAxisRaw for immediate response without smoothing
        float horizontal = Input.GetAxisRaw("Horizontal");
        // float vertical = Input.GetAxisRaw("Vertical"); // Ignore vertical axis for standard movement

        // Only use horizontal input for walking/running
        Vector2 input = new Vector2(horizontal, 0f);
        
        // Normalization might not be strictly necessary anymore with only one axis,
        // but doesn't hurt to keep if other inputs could be added later.
        // if (input.sqrMagnitude > 1) // No need to normalize a 1D vector derived this way
        // {
        //     input.Normalize();
        // }
        return input;
    }

    public bool IsShootPressed()
    {
        // Use GetButtonDown for single fire per press
        // Assumes a "Fire1" button is defined (default is Left Ctrl/Mouse 0)
        return Input.GetButtonDown("Fire1");
        // If you want continuous fire while held, use GetButton("Fire1")
    }
}