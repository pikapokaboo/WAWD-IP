using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The camera used for looking. If empty, the first child Camera is used.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 5f;
    [SerializeField, Min(0f)] private float sprintSpeed = 8f;
    [SerializeField, Min(0f)] private float acceleration = 25f;
    [SerializeField, Range(0f, 1f)] private float airControl = 0.35f;

    [Header("Jumping")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -25f;
    [SerializeField, Min(0f)] private float groundedForce = 2f;

    [Header("Looking")]
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
    [SerializeField, Min(0f)] private float gamepadLookSpeed = 160f;
    [SerializeField, Range(0f, 89f)] private float maxLookAngle = 85f;
    [SerializeField] private bool lockCursor = true;

    public bool IsGrounded => controller != null && controller.isGrounded;
    public bool IsSprinting { get; private set; }
    public Vector3 Velocity => planarVelocity + Vector3.up * verticalVelocity;

    private CharacterController controller;
    private Vector3 planarVelocity;
    private float verticalVelocity;
    private float pitch;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            cameraTransform = childCamera != null ? childCamera.transform : null;
        }
    }

    private void OnEnable()
    {
        SetCursorState(lockCursor);
    }

    private void OnDisable()
    {
        if (lockCursor)
            SetCursorState(false);
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();
    }

    private void HandleMovement()
    {
        Vector2 input = ReadMovement();
        bool grounded = controller.isGrounded;

        IsSprinting = ReadSprint() && input.y > 0.1f;
        float targetSpeed = IsSprinting ? sprintSpeed : walkSpeed;

        Vector3 desiredDirection = transform.right * input.x + transform.forward * input.y;
        if (desiredDirection.sqrMagnitude > 1f)
            desiredDirection.Normalize();

        Vector3 desiredVelocity = desiredDirection * targetSpeed;
        float control = grounded ? 1f : airControl;
        planarVelocity = Vector3.MoveTowards(
            planarVelocity,
            desiredVelocity,
            acceleration * control * Time.deltaTime);

        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -groundedForce;

        if (grounded && ReadJumpPressed())
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 motion = planarVelocity + Vector3.up * verticalVelocity;
        CollisionFlags flags = controller.Move(motion * Time.deltaTime);

        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
    }

    private void HandleLook()
    {
        Vector2 look = Vector2.zero;

        if (Mouse.current != null)
            look += Mouse.current.delta.ReadValue() * mouseSensitivity;

        if (Gamepad.current != null)
            look += Gamepad.current.rightStick.ReadValue() * gamepadLookSpeed * Time.deltaTime;

        transform.Rotate(Vector3.up, look.x, Space.Self);

        pitch = Mathf.Clamp(pitch - look.y, -maxLookAngle, maxLookAngle);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private static Vector2 ReadMovement()
    {
        Vector2 input = Vector2.zero;

        if (Keyboard.current != null)
        {
            input.x = (Keyboard.current.dKey.isPressed ? 1f : 0f)
                    - (Keyboard.current.aKey.isPressed ? 1f : 0f);
            input.y = (Keyboard.current.wKey.isPressed ? 1f : 0f)
                    - (Keyboard.current.sKey.isPressed ? 1f : 0f);
        }

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            if (stick.sqrMagnitude > input.sqrMagnitude)
                input = stick;
        }

        return Vector2.ClampMagnitude(input, 1f);
    }

    private static bool ReadJumpPressed()
    {
        return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }

    private static bool ReadSprint()
    {
        return (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
            || (Gamepad.current != null && Gamepad.current.leftStickButton.isPressed);
    }

    private static void SetCursorState(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnValidate()
    {
        if (sprintSpeed < walkSpeed)
            sprintSpeed = walkSpeed;

        if (gravity > -0.01f)
            gravity = -0.01f;
    }
}
