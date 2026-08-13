// -----------------------------------------------------------------------------
// File: PlayerController.cs
// Project: WAWD Integrated Studio Project
// Purpose: Handles first-person movement, looking, sprinting, jumping, gravity,
//          and cursor state using Unity's Input System.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls a first-person player through a <see cref="CharacterController"/>
/// and actions supplied by a Unity Input Actions asset.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private const string MoveActionName = "Move";
    private const string LookActionName = "Look";
    private const string JumpActionName = "Jump";
    private const string SprintActionName = "Sprint";

    [Header("References")]
    [Tooltip("Input Actions asset containing the player action map.")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [Tooltip("The camera used for looking. If empty, the first child Camera is used.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 5f;
    [SerializeField, Min(0f)] private float sprintSpeed = 8f;
    [SerializeField, Min(0f)] private float acceleration = 45f;
    [SerializeField, Min(0f)] private float deceleration = 70f;
    [SerializeField, Range(0f, 1f)] private float airControl = 0.35f;

    [Header("Jumping")]
    [SerializeField] private bool allowJump;
    [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -25f;
    [SerializeField, Min(0f)] private float groundedForce = 2f;

    [Header("Looking")]
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
    [SerializeField, Min(0f)] private float gamepadLookSpeed = 160f;
    [SerializeField, Range(0f, 89f)] private float maxLookAngle = 85f;
    [SerializeField] private bool lockCursor = true;

    /// <summary>Gets whether the character controller is touching the ground.</summary>
    public bool IsGrounded => controller != null && controller.isGrounded;

    /// <summary>Gets whether the player is currently moving at sprint speed.</summary>
    public bool IsSprinting { get; private set; }

    /// <summary>Gets the player's current planar and vertical velocity.</summary>
    public Vector3 Velocity => planarVelocity + Vector3.up * verticalVelocity;

    private CharacterController controller;
    private Vector3 planarVelocity;
    private float verticalVelocity;
    private float pitch;
    private InputActionMap playerActions;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private bool interactionLocked;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            cameraTransform = childCamera != null ? childCamera.transform : null;
        }

        CacheInputActions();
    }

    private void OnEnable()
    {
        playerActions?.Enable();
        SetCursorState(lockCursor);
    }

    private void OnDisable()
    {
        playerActions?.Disable();

        if (lockCursor)
            SetCursorState(false);
    }

    private void Update()
    {
        if (interactionLocked)
            return;
        HandleLook();
        HandleMovement();
    }

    public void SetInteractionLocked(bool locked)
    {
        interactionLocked = locked;
        planarVelocity = Vector3.zero;
        IsSprinting = false;
        SetCursorState(!locked && lockCursor);
    }

    public void LookAtPoint(Vector3 worldPoint)
    {
        Vector3 flatDirection = worldPoint - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(flatDirection);

        if (cameraTransform == null)
            return;
        Vector3 cameraDirection = worldPoint - cameraTransform.position;
        float horizontalDistance = new Vector2(cameraDirection.x,
            cameraDirection.z).magnitude;
        pitch = Mathf.Clamp(-Mathf.Atan2(cameraDirection.y, horizontalDistance)
            * Mathf.Rad2Deg, -maxLookAngle, maxLookAngle);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        bool grounded = controller.isGrounded;

        IsSprinting = sprintAction.IsPressed() && input.y > 0.1f;
        float targetSpeed = IsSprinting ? sprintSpeed : walkSpeed;

        Vector3 desiredDirection = transform.right * input.x + transform.forward * input.y;
        if (desiredDirection.sqrMagnitude > 1f)
            desiredDirection.Normalize();

        Vector3 desiredVelocity = desiredDirection * targetSpeed;
        bool hasMovementInput = input.sqrMagnitude > 0.0001f;
        float responsiveness = hasMovementInput ? acceleration : deceleration;
        float control = grounded ? 1f : airControl;
        planarVelocity = Vector3.MoveTowards(
            planarVelocity,
            desiredVelocity,
            responsiveness * control * Time.deltaTime);

        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -groundedForce;

        if (allowJump && grounded && jumpAction.WasPressedThisFrame())
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 motion = planarVelocity + Vector3.up * verticalVelocity;
        CollisionFlags flags = controller.Move(motion * Time.deltaTime);

        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
    }

    private void HandleLook()
    {
        Vector2 look = lookAction.ReadValue<Vector2>();
        bool isPointerDelta = lookAction.activeControl?.device is Pointer;
        look *= isPointerDelta ? mouseSensitivity : gamepadLookSpeed * Time.deltaTime;

        transform.Rotate(Vector3.up, look.x, Space.Self);

        pitch = Mathf.Clamp(pitch - look.y, -maxLookAngle, maxLookAngle);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void CacheInputActions()
    {
        if (inputActions == null)
            inputActions = InputSystem.actions;

        if (inputActions == null)
        {
            Debug.LogError($"{nameof(PlayerController)} on '{name}' needs an Input Actions asset.", this);
            enabled = false;
            return;
        }

        playerActions = inputActions.FindActionMap(actionMapName, false);
        if (playerActions == null)
        {
            Debug.LogError($"Action map '{actionMapName}' was not found in '{inputActions.name}'.", this);
            enabled = false;
            return;
        }

        moveAction = playerActions.FindAction(MoveActionName, true);
        lookAction = playerActions.FindAction(LookActionName, true);
        jumpAction = playerActions.FindAction(JumpActionName, true);
        sprintAction = playerActions.FindAction(SprintActionName, true);
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
