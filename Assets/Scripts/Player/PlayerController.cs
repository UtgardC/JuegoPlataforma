using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    [Header("Input Actions")]
    public InputActionReference moveAction;
    public InputActionReference sprintAction;
    public InputActionReference jumpAction;
    public InputActionReference interactAction;
    public InputActionReference resetAction;

    [Header("References")]
    public PlayerMotor motor;
    public PlayerInteraction interaction;
    public PlayerRespawn respawn;

    [Header("Legacy Tuning")]
    public float walkAcceleration = 50f;
    public float sprintAcceleration = 80f;
    public float maxWalkSpeed = 6f;
    public float maxSprintSpeed = 10f;
    public float jumpForce = 6f;
    public float groundFriction = 6f;

    private Vector2 moveInput;
    private Vector2 callbackMoveInput;
    private bool callbackSprintInput;
    private bool hasMoveCallback;
    private bool hasSprintCallback;

    private void Awake()
    {
        EnsureReferences();
        ApplyLegacyTuning();
    }

    private void OnEnable()
    {
        EnableAction(moveAction);
        EnableAction(sprintAction);

        if (jumpAction != null)
        {
            jumpAction.action.Enable();
            jumpAction.action.performed += OnJumpPerformed;
        }

        if (interactAction != null)
        {
            interactAction.action.Enable();
            interactAction.action.performed += OnInteractPerformed;
            interactAction.action.canceled += OnInteractCanceled;
        }

        if (resetAction != null)
        {
            resetAction.action.Enable();
            resetAction.action.performed += OnResetPerformed;
        }
    }

    private void OnDisable()
    {
        DisableAction(moveAction);
        DisableAction(sprintAction);

        if (jumpAction != null)
        {
            jumpAction.action.performed -= OnJumpPerformed;
            jumpAction.action.Disable();
        }

        if (interactAction != null)
        {
            interactAction.action.performed -= OnInteractPerformed;
            interactAction.action.canceled -= OnInteractCanceled;
            interactAction.action.Disable();
        }

        if (resetAction != null)
        {
            resetAction.action.performed -= OnResetPerformed;
            resetAction.action.Disable();
        }
    }

    private void Update()
    {
        moveInput = ReadMoveInput();

        motor.SetMoveInput(moveInput);
        motor.SetSprinting(ReadSprintInput());

        if (jumpAction == null && WasJumpPressed())
            motor.RequestJump();

        if (interactAction == null)
        {
            if (WasInteractPressed())
                interaction.TryGrabOrRelease();

            if (WasInteractReleased())
                interaction.ReleaseGrab();
        }

        if (resetAction == null && WasResetPressed())
            respawn.ResetToCheckpoint();

        interaction.SetMoveDirection(motor.GetCameraRelativeMoveDirection());
    }

    private void EnsureReferences()
    {
        PlayerAbilities abilities = GetComponent<PlayerAbilities>();
        if (abilities == null)
            abilities = gameObject.AddComponent<PlayerAbilities>();

        PlayerInventory inventory = GetComponent<PlayerInventory>();
        if (inventory == null)
            inventory = gameObject.AddComponent<PlayerInventory>();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        Collider[] colliders = GetComponents<Collider>();
        bool hasUsableCollider = false;

        foreach (Collider candidate in colliders)
        {
            if (candidate == null || candidate is CharacterController)
                continue;

            hasUsableCollider = true;
            break;
        }

        if (!hasUsableCollider)
        {
            CapsuleCollider capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
            capsuleCollider.height = 2f;
            capsuleCollider.radius = 0.35f;
            capsuleCollider.center = new Vector3(0f, 1f, 0f);
        }

        WeightedObject weightedObject = GetComponent<WeightedObject>();
        if (weightedObject == null)
        {
            weightedObject = gameObject.AddComponent<WeightedObject>();
            weightedObject.pressureWeight = 1f;
        }

        if (motor == null)
        {
            motor = GetComponent<PlayerMotor>();
            if (motor == null)
                motor = gameObject.AddComponent<PlayerMotor>();
        }

        motor.abilities = abilities;

        if (interaction == null)
        {
            interaction = GetComponent<PlayerInteraction>();
            if (interaction == null)
                interaction = gameObject.AddComponent<PlayerInteraction>();
        }

        if (respawn == null)
        {
            respawn = GetComponent<PlayerRespawn>();
            if (respawn == null)
                respawn = gameObject.AddComponent<PlayerRespawn>();
        }
    }

    private void ApplyLegacyTuning()
    {
        if (motor == null)
            return;

        motor.walkSpeed = maxWalkSpeed;
        motor.sprintSpeed = maxSprintSpeed;
        motor.acceleration = Mathf.Max(1f, walkAcceleration);
        motor.airAcceleration = Mathf.Max(1f, walkAcceleration * 0.35f);
        motor.jumpHeight = Mathf.Max(0.25f, jumpForce * 0.25f);
    }

    private Vector2 ReadMoveInput()
    {
        if (moveAction != null)
            return moveAction.action.ReadValue<Vector2>();

        if (hasMoveCallback)
            return callbackMoveInput;

        Vector2 input = Vector2.zero;
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                input.y += 1f;
        }

        if (Gamepad.current != null)
            input += Gamepad.current.leftStick.ReadValue();

        return Vector2.ClampMagnitude(input, 1f);
    }

    private bool ReadSprintInput()
    {
        if (sprintAction != null)
            return sprintAction.action.IsPressed();

        if (hasSprintCallback)
            return callbackSprintInput;

        return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
    }

    private bool WasJumpPressed()
    {
        return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }

    private bool WasInteractPressed()
    {
        return (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
    }

    private bool WasInteractReleased()
    {
        return (Keyboard.current != null && Keyboard.current.eKey.wasReleasedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonWest.wasReleasedThisFrame);
    }

    private bool WasResetPressed()
    {
        return (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame);
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        motor.RequestJump();
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        interaction.TryGrabOrRelease();
    }

    private void OnInteractCanceled(InputAction.CallbackContext context)
    {
        interaction.ReleaseGrab();
    }

    private void OnResetPerformed(InputAction.CallbackContext context)
    {
        respawn.ResetToCheckpoint();
    }

    public void OnMove(InputValue value)
    {
        callbackMoveInput = value.Get<Vector2>();
        hasMoveCallback = true;
    }

    public void OnSprint(InputValue value)
    {
        callbackSprintInput = value.isPressed;
        hasSprintCallback = true;
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
            motor.RequestJump();
    }

    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
            interaction.TryGrabOrRelease();
        else
            interaction.ReleaseGrab();
    }

    public void OnClimb(InputValue value)
    {
        OnInteract(value);
    }

    public void OnResetCheckpoint(InputValue value)
    {
        if (value.isPressed)
            respawn.ResetToCheckpoint();
    }

    public void OnReset(InputValue value)
    {
        OnResetCheckpoint(value);
    }

    private static void EnableAction(InputActionReference reference)
    {
        if (reference != null)
            reference.action.Enable();
    }

    private static void DisableAction(InputActionReference reference)
    {
        if (reference != null)
            reference.action.Disable();
    }
}
