using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerInteraction : MonoBehaviour
{
    [Header("Detection")]
    public Transform interactionOrigin;
    public float interactionRange = 1.8f;
    public float interactionRadius = 0.6f;
    public LayerMask movableMask = ~0;

    [Header("Grab")]
    public bool holdButtonToGrab = false;
    public bool usePhysicalGrab = true;
    public bool connectPhysicalGrabToPlayerRigidbody = true;
    public bool faceTargetWhenAvailable = false;
    public bool faceTargetOnGrab = true;
    public bool lockFacingWhileGrabbed = true;
    public bool faceGrabbedObject = false;
    public bool snapFacingOnGrab = true;
    public float maxGrabDistance = 2.6f;

    [Header("References")]
    public PlayerMotor motor;

    [Header("Events")]
    public UnityEvent onGrabStarted;
    public UnityEvent onGrabEnded;
    public UnityEvent onTargetChanged;

    private readonly Collider[] detectionResults = new Collider[16];
    private MovableObject currentTarget;
    private MovableObject grabbedObject;
    private Vector3 currentMoveDirection;
    private Rigidbody playerBody;
    private Rigidbody kinematicGrabAnchorBody;
    private bool usingKinematicGrabAnchor;
    private Vector3 grabAnchorLocalPosition;

    public MovableObject CurrentTarget => currentTarget;
    public MovableObject GrabbedObject => grabbedObject;
    public bool IsGrabbing => grabbedObject != null;
    public bool HasAvailableTarget => currentTarget != null;

    private void Awake()
    {
        if (interactionOrigin == null)
            interactionOrigin = transform;

        if (motor == null)
            motor = GetComponent<PlayerMotor>();

        playerBody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (!IsGrabbing)
            UpdateCurrentTarget();

        UpdateFacing();
    }

    private void FixedUpdate()
    {
        if (grabbedObject == null)
            return;

        if (usePhysicalGrab && grabbedObject.usePhysicalGrab)
        {
            if (!grabbedObject.IsUsingPhysicalGrab)
            {
                ReleaseGrab();
                return;
            }

            UpdatePhysicalGrabAnchor();
            grabbedObject.UpdatePhysicalGrab(currentMoveDirection);

            if (IsGrabTooFar())
                ReleaseGrab();
        }
        else
        {
            grabbedObject.MoveByPlayer(currentMoveDirection);
        }
    }

    public void SetMoveDirection(Vector3 moveDirection)
    {
        currentMoveDirection = moveDirection;
    }

    public void TryGrabOrRelease()
    {
        if (grabbedObject != null)
        {
            if (!holdButtonToGrab)
                ReleaseGrab();

            return;
        }

        UpdateCurrentTarget();

        if (currentTarget == null)
            return;

        grabbedObject = currentTarget;

        Vector3 grabPoint = grabbedObject.GetClosestPoint(interactionOrigin.position);

        if (faceTargetOnGrab && motor != null)
        {
            if (lockFacingWhileGrabbed)
                motor.LockFacingTowards(grabPoint, snapFacingOnGrab);
            else
                motor.FaceTowardsWorldPoint(grabPoint, snapFacingOnGrab);
        }

        if (usePhysicalGrab && grabbedObject.usePhysicalGrab)
        {
            EnsureGrabAnchor();
            grabAnchorLocalPosition = transform.InverseTransformPoint(grabPoint);

            if (connectPhysicalGrabToPlayerRigidbody && playerBody != null && !playerBody.isKinematic)
            {
                usingKinematicGrabAnchor = false;
                Vector3 connectedAnchor = playerBody.transform.InverseTransformPoint(grabPoint);
                grabbedObject.BeginGrab(transform, playerBody, grabPoint, connectedAnchor);
            }
            else
            {
                usingKinematicGrabAnchor = true;
                MoveGrabAnchor(grabPoint, transform.rotation, true);
                grabbedObject.BeginGrab(transform, kinematicGrabAnchorBody, grabPoint, Vector3.zero);
            }
        }
        else
        {
            grabbedObject.BeginGrab(transform);
        }

        onGrabStarted.Invoke();
    }

    public void ReleaseGrab()
    {
        if (grabbedObject == null)
            return;

        grabbedObject.EndGrab();
        grabbedObject = null;

        if (lockFacingWhileGrabbed && motor != null)
            motor.ClearFacingLock();

        if (usingKinematicGrabAnchor)
            SetGrabAnchorActive(false);

        usingKinematicGrabAnchor = false;
        onGrabEnded.Invoke();
    }

    private void UpdateCurrentTarget()
    {
        MovableObject previousTarget = currentTarget;
        currentTarget = FindBestTarget();

        if (currentTarget != previousTarget)
            onTargetChanged.Invoke();
    }

    private MovableObject FindBestTarget()
    {
        Vector3 origin = interactionOrigin != null ? interactionOrigin.position : transform.position;
        int count = Physics.OverlapSphereNonAlloc(origin, interactionRange, detectionResults, movableMask, QueryTriggerInteraction.Ignore);
        MovableObject bestTarget = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider candidateCollider = detectionResults[i];
            if (candidateCollider == null)
                continue;

            MovableObject movable = candidateCollider.GetComponentInParent<MovableObject>();
            if (movable == null || !movable.CanBeGrabbed)
                continue;

            Vector3 closestPoint = movable.GetClosestPoint(origin);
            Vector3 toCandidate = closestPoint - origin;
            float distance = toCandidate.magnitude;

            if (distance > interactionRange + interactionRadius)
                continue;

            float facingScore = Vector3.Dot(transform.forward, toCandidate.normalized);
            float score = distance - facingScore;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = movable;
            }
        }

        return bestTarget;
    }

    private void UpdateFacing()
    {
        if (motor == null)
            return;

        if (grabbedObject != null && faceGrabbedObject && !lockFacingWhileGrabbed)
        {
            motor.RequestFaceTowards(grabbedObject.CurrentGrabPoint);
            return;
        }

        if (currentTarget != null && faceTargetWhenAvailable)
            motor.RequestFaceTowards(currentTarget.GetClosestPoint(interactionOrigin.position));
    }

    private void EnsureGrabAnchor()
    {
        if (connectPhysicalGrabToPlayerRigidbody && playerBody != null && !playerBody.isKinematic)
            return;

        if (kinematicGrabAnchorBody != null)
            return;

        GameObject anchorObject = new GameObject($"{name}_PhysicalGrabAnchor");
        anchorObject.hideFlags = HideFlags.HideInHierarchy;

        kinematicGrabAnchorBody = anchorObject.AddComponent<Rigidbody>();
        kinematicGrabAnchorBody.isKinematic = true;
        kinematicGrabAnchorBody.useGravity = false;
        kinematicGrabAnchorBody.interpolation = RigidbodyInterpolation.Interpolate;
        kinematicGrabAnchorBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void UpdatePhysicalGrabAnchor()
    {
        if (grabbedObject == null)
            return;

        Vector3 desiredPosition = transform.TransformPoint(grabAnchorLocalPosition);
        desiredPosition = grabbedObject.GetConstrainedGrabAnchorPosition(desiredPosition, currentMoveDirection);

        if (usingKinematicGrabAnchor)
            MoveGrabAnchor(desiredPosition, transform.rotation, false);
        else
            grabbedObject.SetConnectedAnchorWorldPosition(desiredPosition);
    }

    private void MoveGrabAnchor(Vector3 position, Quaternion rotation, bool teleport)
    {
        SetGrabAnchorActive(true);

        if (teleport)
        {
            kinematicGrabAnchorBody.position = position;
            kinematicGrabAnchorBody.rotation = rotation;
            kinematicGrabAnchorBody.transform.SetPositionAndRotation(position, rotation);
            return;
        }

        kinematicGrabAnchorBody.MovePosition(position);
        kinematicGrabAnchorBody.MoveRotation(rotation);
    }

    private void SetGrabAnchorActive(bool active)
    {
        if (kinematicGrabAnchorBody != null)
            kinematicGrabAnchorBody.gameObject.SetActive(active);
    }

    private void OnDestroy()
    {
        if (kinematicGrabAnchorBody != null)
            Destroy(kinematicGrabAnchorBody.gameObject);
    }

    private bool IsGrabTooFar()
    {
        if (grabbedObject == null)
            return false;

        Vector3 origin = interactionOrigin != null ? interactionOrigin.position : transform.position;
        return Vector3.Distance(origin, grabbedObject.CurrentGrabPoint) > maxGrabDistance;
    }

    private void OnDrawGizmosSelected()
    {
        Transform origin = interactionOrigin != null ? interactionOrigin : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin.position, interactionRange);
    }
}
