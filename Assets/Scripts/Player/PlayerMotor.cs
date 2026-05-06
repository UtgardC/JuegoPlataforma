using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PlayerMotor : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public PlayerAbilities abilities;
    public PlayerInteraction interaction;

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
    public bool preventJumpWhilePulling = true;

    [Header("Ground Check")]
    public Transform groundCheckOrigin;
    public LayerMask groundMask = ~0;
    public float groundCheckRadius = 0.25f;
    public float groundCheckDistance = 0.2f;
    public float groundCheckStartOffset = 0.08f;
    public float maxGroundSlopeAngle = 65f;
    public bool drawGroundCheckGizmo = true;
    public Color groundedGizmoColor = new Color(0.2f, 0.9f, 0.35f, 0.9f);
    public Color airborneGizmoColor = new Color(1f, 0.25f, 0.2f, 0.9f);

    [Header("Jump Forgiveness")]
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;

    [Header("Moving Ground")]
    public bool inheritMovingGround = true;
    public bool inheritGroundRotation = true;
    public float maxMovingGroundDelta = 4f;

    [Header("External Forces")]
    public float externalVelocityDamping = 6f;
    public float maxExternalVelocity = 14f;

    private Rigidbody rb;
    private Collider[] ownColliders;
    private readonly RaycastHit[] groundHits = new RaycastHit[12];
    private readonly Collider[] groundOverlapResults = new Collider[12];
    private Vector2 moveInput;
    private Vector3 externalVelocity;
    private Vector3 lastMoveDirection;
    private bool sprinting;
    private bool jumpRequested;
    private float lastJumpRequestTime = -999f;
    private float lastGroundedTime = -999f;
    private bool wasGrounded;
    private bool hasRequestedLookDirection;
    private Vector3 requestedLookDirection;
    private bool isFacingLocked;
    private Vector3 lockedFacingDirection;
    private Vector3 lastGroundCheckOrigin;
    private Vector3 lastGroundCheckEnd;
    private Vector3 lastGroundHitPoint;
    private bool hasGroundHitPoint;
    private Transform currentGroundTransform;
    private Rigidbody currentGroundRigidbody;
    private Vector3 lastGroundPosition;
    private Quaternion lastGroundRotation;
    private bool hasMovingGroundReference;

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
        ownColliders = GetComponentsInChildren<Collider>();

        if (abilities == null)
            abilities = GetComponent<PlayerAbilities>();

        if (interaction == null)
            interaction = GetComponent<PlayerInteraction>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        CharacterController legacyController = GetComponent<CharacterController>();
        if (legacyController != null)
            legacyController.enabled = false;
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();
        ApplyMovingGroundMotion();
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
        if (IsJumpBlockedByInteraction())
        {
            jumpRequested = false;
            lastJumpRequestTime = -999f;
            return;
        }

        jumpRequested = true;
        lastJumpRequestTime = Time.time;
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
        Transform previousGroundTransform = currentGroundTransform;
        currentGroundTransform = null;
        currentGroundRigidbody = null;

        Vector3 origin = GetGroundCheckOrigin();
        float castDistance = GetGroundCheckCastDistance();

        lastGroundCheckOrigin = origin;
        lastGroundCheckEnd = origin + Vector3.down * castDistance;
        hasGroundHitPoint = false;

        IsGrounded = FindGroundWithSphereCast(origin, castDistance) || FindGroundWithOverlap(origin);

        if (IsGrounded)
        {
            lastGroundedTime = Time.time;

            if (currentGroundTransform != previousGroundTransform)
                ResetMovingGroundReference();
        }
        else
        {
            hasMovingGroundReference = false;
        }

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
            if (IsJumpBlockedByInteraction())
            {
                jumpRequested = false;
                lastJumpRequestTime = -999f;
                rb.AddForce(Vector3.up * gravity, ForceMode.Acceleration);
                return;
            }

            bool jumpBuffered = Time.time - lastJumpRequestTime <= jumpBufferTime;
            bool canUseGroundJump = IsGrounded || Time.time - lastGroundedTime <= coyoteTime;

            if (jumpBuffered && canUseGroundJump)
            {
                SetVerticalVelocity(CalculateJumpVelocity(jumpHeight));
                abilities?.ResetAirJumps();
                IsGrounded = false;
                lastGroundedTime = -999f;
                jumpRequested = false;
            }
            else if (jumpBuffered && abilities != null && abilities.ConsumeDoubleJump())
            {
                SetVerticalVelocity(CalculateJumpVelocity(doubleJumpHeight));
                jumpRequested = false;
            }
            else if (!jumpBuffered)
            {
                jumpRequested = false;
            }
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

    private bool FindGroundWithSphereCast(Vector3 origin, float castDistance)
    {
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            Mathf.Max(0.01f, groundCheckRadius),
            Vector3.down,
            groundHits,
            castDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        bool foundGround = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];

            if (hit.collider == null || IsOwnCollider(hit.collider) || !IsValidGroundNormal(hit.normal))
                continue;

            if (hit.distance >= bestDistance)
                continue;

            bestDistance = hit.distance;
            lastGroundHitPoint = hit.point;
            hasGroundHitPoint = true;
            SetCurrentGround(hit.collider);
            foundGround = true;
        }

        return foundGround;
    }

    private bool FindGroundWithOverlap(Vector3 origin)
    {
        Vector3 overlapCenter = origin + Vector3.down * Mathf.Max(0f, groundCheckStartOffset);
        int overlapCount = Physics.OverlapSphereNonAlloc(
            overlapCenter,
            Mathf.Max(0.01f, groundCheckRadius),
            groundOverlapResults,
            groundMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider candidate = groundOverlapResults[i];

            if (candidate == null || IsOwnCollider(candidate))
                continue;

            lastGroundHitPoint = candidate.ClosestPoint(overlapCenter);
            hasGroundHitPoint = true;
            SetCurrentGround(candidate);
            return true;
        }

        return false;
    }

    private Vector3 GetGroundCheckOrigin()
    {
        if (groundCheckOrigin != null)
            return groundCheckOrigin.position;

        Bounds bodyBounds = GetBodyBounds();
        float radius = Mathf.Max(0.01f, groundCheckRadius);
        float startOffset = Mathf.Max(0f, groundCheckStartOffset);
        return new Vector3(rb.position.x, bodyBounds.min.y + radius + startOffset, rb.position.z);
    }

    private float GetGroundCheckCastDistance()
    {
        return Mathf.Max(0f, groundCheckStartOffset) + Mathf.Max(0.01f, groundCheckDistance);
    }

    private Bounds GetBodyBounds()
    {
        if (ownColliders == null || ownColliders.Length == 0)
            ownColliders = GetComponentsInChildren<Collider>();

        Bounds bounds = new Bounds(transform.position, Vector3.one * Mathf.Max(0.1f, groundCheckRadius * 2f));
        bool hasBounds = false;

        foreach (Collider ownCollider in ownColliders)
        {
            if (ownCollider == null || ownCollider.isTrigger)
                continue;

            if (!hasBounds)
            {
                bounds = ownCollider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(ownCollider.bounds);
            }
        }

        return bounds;
    }

    private bool IsOwnCollider(Collider candidate)
    {
        if (ownColliders == null)
            return false;

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (candidate == ownColliders[i])
                return true;
        }

        return false;
    }

    private bool IsValidGroundNormal(Vector3 normal)
    {
        if (normal.sqrMagnitude <= 0.0001f)
            return true;

        return Vector3.Angle(normal, Vector3.up) <= maxGroundSlopeAngle;
    }

    private void ApplyMovingGroundMotion()
    {
        if (!inheritMovingGround || !IsGrounded || currentGroundTransform == null)
            return;

        Vector3 groundPosition = GetCurrentGroundPosition();
        Quaternion groundRotation = GetCurrentGroundRotation();

        if (!hasMovingGroundReference)
        {
            lastGroundPosition = groundPosition;
            lastGroundRotation = groundRotation;
            hasMovingGroundReference = true;
            return;
        }

        Vector3 deltaPosition = groundPosition - lastGroundPosition;
        Quaternion deltaRotation = groundRotation * Quaternion.Inverse(lastGroundRotation);

        if (deltaPosition.sqrMagnitude > maxMovingGroundDelta * maxMovingGroundDelta)
            deltaPosition = Vector3.zero;

        Vector3 nextPosition = rb.position + deltaPosition;

        if (inheritGroundRotation)
        {
            Vector3 relativePosition = nextPosition - groundPosition;
            nextPosition = groundPosition + deltaRotation * relativePosition;
            rb.MoveRotation(deltaRotation * rb.rotation);
        }

        if ((nextPosition - rb.position).sqrMagnitude > 0.0000001f)
            rb.MovePosition(nextPosition);

        lastGroundPosition = groundPosition;
        lastGroundRotation = groundRotation;
    }

    private void ResetMovingGroundReference()
    {
        if (currentGroundTransform == null)
        {
            hasMovingGroundReference = false;
            return;
        }

        lastGroundPosition = GetCurrentGroundPosition();
        lastGroundRotation = GetCurrentGroundRotation();
        hasMovingGroundReference = true;
    }

    private void SetCurrentGround(Collider groundCollider)
    {
        currentGroundRigidbody = groundCollider.attachedRigidbody;
        currentGroundTransform = currentGroundRigidbody != null ? currentGroundRigidbody.transform : groundCollider.transform;
    }

    private Vector3 GetCurrentGroundPosition()
    {
        return currentGroundRigidbody != null ? currentGroundRigidbody.position : currentGroundTransform.position;
    }

    private Quaternion GetCurrentGroundRotation()
    {
        return currentGroundRigidbody != null ? currentGroundRigidbody.rotation : currentGroundTransform.rotation;
    }

    private bool IsJumpBlockedByInteraction()
    {
        return preventJumpWhilePulling && interaction != null && interaction.IsPulling;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGroundCheckGizmo)
            return;

        Vector3 origin;
        Vector3 end;

        if (Application.isPlaying)
        {
            origin = lastGroundCheckOrigin;
            end = lastGroundCheckEnd;
            Gizmos.color = IsGrounded ? groundedGizmoColor : airborneGizmoColor;
        }
        else
        {
            Rigidbody cachedBody = rb != null ? rb : GetComponent<Rigidbody>();
            Vector3 basePosition = cachedBody != null ? cachedBody.position : transform.position;
            Bounds bodyBounds = GetBodyBounds();
            float radius = Mathf.Max(0.01f, groundCheckRadius);
            origin = groundCheckOrigin != null
                ? groundCheckOrigin.position
                : new Vector3(basePosition.x, bodyBounds.min.y + radius + Mathf.Max(0f, groundCheckStartOffset), basePosition.z);
            end = origin + Vector3.down * GetGroundCheckCastDistance();
            Gizmos.color = airborneGizmoColor;
        }

        float drawRadius = Mathf.Max(0.01f, groundCheckRadius);
        Gizmos.DrawWireSphere(origin, drawRadius);
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawWireSphere(end, drawRadius);

        if (Application.isPlaying && hasGroundHitPoint)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(lastGroundHitPoint, 0.05f);
        }
    }
}
