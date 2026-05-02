using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PlayerMotor : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public PlayerAbilities abilities;

    [Header("Movement")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 10f;
    public float acceleration = 18f;
    public float airAcceleration = 8f;
    public float rotationSpeed = 720f;
    public float maxControlledSpeed = 12f;

    [Header("Jump")]
    public float jumpHeight = 1.5f;
    public float doubleJumpHeight = 1.35f;
    public float gravity = -24f;
    public float groundedStickForce = -2f;

    [Header("Ground Check")]
    public LayerMask groundMask = ~0;
    public float groundCheckRadius = 0.25f;
    public float groundCheckDistance = 0.2f;

    [Header("External Forces")]
    public float externalVelocityDamping = 6f;
    public float maxExternalVelocity = 14f;

    private Rigidbody rb;
    private Vector2 moveInput;
    private Vector3 externalVelocity;
    private Vector3 lastMoveDirection;
    private bool sprinting;
    private bool jumpRequested;
    private bool wasGrounded;
    private bool hasRequestedLookDirection;
    private Vector3 requestedLookDirection;
    private bool isFacingLocked;
    private Vector3 lockedFacingDirection;

    public bool IsGrounded { get; private set; }
    public Vector3 MoveDirection => lastMoveDirection;
    public Vector3 ExternalVelocity => externalVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (abilities == null)
            abilities = GetComponent<PlayerAbilities>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        CharacterController legacyController = GetComponent<CharacterController>();
        if (legacyController != null)
            legacyController.enabled = false;
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();
        ApplyHorizontalMovement();
        UpdateJumpAndGravity();
        UpdateExternalVelocity();
        RotateTowardsMovement();
    }

    public void SetMoveInput(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    public void SetSprinting(bool value)
    {
        sprinting = value;
    }

    public void RequestJump()
    {
        jumpRequested = true;
    }

    public void RequestFaceTowards(Vector3 worldPoint)
    {
        Vector3 direction = worldPoint - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        requestedLookDirection = direction.normalized;
        hasRequestedLookDirection = true;
    }

    public void LockFacingTowards(Vector3 worldPoint, bool immediate)
    {
        Vector3 direction = worldPoint - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        LockFacingDirection(direction.normalized, immediate);
    }

    public void LockFacingDirection(Vector3 worldDirection, bool immediate)
    {
        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude <= 0.0001f)
            return;

        lockedFacingDirection = worldDirection.normalized;
        isFacingLocked = true;

        if (immediate)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lockedFacingDirection, Vector3.up);
            rb.rotation = targetRotation;
            transform.rotation = targetRotation;
        }
    }

    public void ClearFacingLock()
    {
        isFacingLocked = false;
        lockedFacingDirection = Vector3.zero;
    }

    public void FaceTowardsWorldPoint(Vector3 worldPoint, bool immediate)
    {
        Vector3 direction = worldPoint - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        if (immediate)
        {
            rb.rotation = targetRotation;
            transform.rotation = targetRotation;
        }
        else
        {
            Quaternion nextRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            rb.MoveRotation(nextRotation);
        }
    }

    public void AddExternalVelocity(Vector3 velocity)
    {
        externalVelocity = Vector3.ClampMagnitude(externalVelocity + velocity, maxExternalVelocity);
    }

    public void AddExternalAcceleration(Vector3 accelerationValue)
    {
        float deltaTime = Time.inFixedTimeStep ? Time.fixedDeltaTime : Time.deltaTime;
        externalVelocity = Vector3.ClampMagnitude(externalVelocity + accelerationValue * deltaTime, maxExternalVelocity);
    }

    public void ClearExternalVelocity()
    {
        externalVelocity = Vector3.zero;
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        rb.position = position;
        rb.rotation = rotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        externalVelocity = Vector3.zero;
        transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
    }

    public Vector3 GetCameraRelativeMoveDirection()
    {
        if (moveInput.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        Transform reference = cameraTransform != null ? cameraTransform : transform;
        Vector3 forward = reference.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = reference.right;
        right.y = 0f;
        right.Normalize();

        return Vector3.ClampMagnitude(forward * moveInput.y + right * moveInput.x, 1f);
    }

    private void UpdateGroundedState()
    {
        Vector3 origin = transform.position + Vector3.up * Mathf.Max(groundCheckRadius, 0.05f);
        bool sphereGrounded = Physics.SphereCast(origin, groundCheckRadius, Vector3.down, out _, groundCheckRadius + groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);

        IsGrounded = sphereGrounded;

        if (IsGrounded && !wasGrounded)
            abilities?.ResetAirJumps();

        wasGrounded = IsGrounded;
    }

    private void ApplyHorizontalMovement()
    {
        Vector3 desiredDirection = GetCameraRelativeMoveDirection();
        float targetSpeed = sprinting ? sprintSpeed : walkSpeed;
        Vector3 targetVelocity = desiredDirection * targetSpeed;
        float currentAcceleration = IsGrounded ? acceleration : airAcceleration;
        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        Vector3 externalHorizontalVelocity = Vector3.ProjectOnPlane(externalVelocity, Vector3.up);
        Vector3 controlledVelocity = horizontalVelocity - externalHorizontalVelocity;
        Vector3 velocityDelta = targetVelocity - controlledVelocity;
        Vector3 requestedAcceleration = Vector3.ClampMagnitude(velocityDelta / Time.fixedDeltaTime, currentAcceleration);

        rb.AddForce(requestedAcceleration, ForceMode.Acceleration);

        Vector3 limitedControlledVelocity = Vector3.ClampMagnitude(controlledVelocity, maxControlledSpeed);
        if (controlledVelocity.sqrMagnitude > maxControlledSpeed * maxControlledSpeed)
            rb.linearVelocity = limitedControlledVelocity + externalHorizontalVelocity + Vector3.up * velocity.y;

        if (desiredDirection.sqrMagnitude > 0.0001f)
            lastMoveDirection = desiredDirection.normalized;
        else if (horizontalVelocity.sqrMagnitude < 0.01f)
            lastMoveDirection = Vector3.zero;
    }

    private void UpdateJumpAndGravity()
    {
        Vector3 velocity = rb.linearVelocity;

        if (IsGrounded && velocity.y < groundedStickForce)
        {
            velocity.y = groundedStickForce;
            rb.linearVelocity = velocity;
        }

        if (jumpRequested)
        {
            if (IsGrounded)
            {
                SetVerticalVelocity(CalculateJumpVelocity(jumpHeight));
                abilities?.ResetAirJumps();
            }
            else if (abilities != null && abilities.ConsumeDoubleJump())
            {
                SetVerticalVelocity(CalculateJumpVelocity(doubleJumpHeight));
            }

            jumpRequested = false;
        }

        rb.AddForce(Vector3.up * gravity, ForceMode.Acceleration);
    }

    private void UpdateExternalVelocity()
    {
        if (externalVelocity.sqrMagnitude <= 0.0001f)
        {
            externalVelocity = Vector3.zero;
            return;
        }

        externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, externalVelocityDamping * Time.fixedDeltaTime);
        rb.AddForce(externalVelocity, ForceMode.Acceleration);
    }

    private void RotateTowardsMovement()
    {
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        Vector3 lookDirection = isFacingLocked ? lockedFacingDirection : hasRequestedLookDirection ? requestedLookDirection : horizontalVelocity;
        hasRequestedLookDirection = false;

        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude <= 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(nextRotation);
    }

    private void SetVerticalVelocity(float velocityY)
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.y = velocityY;
        rb.linearVelocity = velocity;
    }

    private float CalculateJumpVelocity(float height)
    {
        return Mathf.Sqrt(Mathf.Max(0f, height) * -2f * gravity);
    }
}
