using UnityEngine;

public enum MoveMode
{
    FreeDrag,
    PushOnly,
    Rail
}

public enum WeightClass
{
    Light,
    Normal,
    Heavy
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class MovableObject : MonoBehaviour, IResettable, IWeightedObject, ILaserBlocker
{
    [Header("Movement")]
    public MoveMode moveMode = MoveMode.FreeDrag;
    public WeightClass weightClass = WeightClass.Normal;
    public bool canBeMoved = true;
    public bool canBeGrabbed = true;
    public bool canBePushed = true;
    public float playerMoveSpeed = 3.5f;
    public float pushOnlyMinDot = 0.45f;

    [Header("Rail")]
    public Vector3 railDirection = Vector3.right;
    public bool snapToRailLine = true;

    [Header("Gameplay Properties")]
    public float physicsMass = 10f;
    public float pressureWeight = 1f;
    public bool canBlockLaser = true;
    public bool canBlockWind = false;
    public bool canBeAffectedByWind = true;
    public bool canBecomeLavaPlatform = false;

    [Header("Physics")]
    public bool applyPhysicsMassOnAwake = true;
    public bool freezeTiltRotations = true;
    public bool useContinuousCollisionWhileGrabbed = true;

    [Header("Physical Grab")]
    public bool usePhysicalGrab = true;
    public float grabSpring = 1400f;
    public float grabDamper = 120f;
    public float grabSlack = 0.06f;
    public float grabBreakForce = 12000f;
    public float grabProjectionDistance = 0.35f;
    public bool collideWithGrabberWhileGrabbed = true;
    public bool increaseDampingWhileGrabbed = true;
    public float grabbedLinearDamping = 2f;
    public float grabbedAngularDamping = 3f;
    public float railSnapStrength = 12f;

    private Rigidbody rb;
    private Transform grabber;
    private Rigidbody grabAnchorBody;
    private ConfigurableJoint grabJoint;
    private Vector3 pushDirection;
    private Vector3 railOrigin;
    private Vector3 grabLocalAnchor;
    private Vector3 grabRailOffset;
    private Vector3 lastGrabAnchorPosition;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool initialKinematic;
    private RigidbodyConstraints initialConstraints;
    private CollisionDetectionMode initialCollisionDetectionMode;
    private float initialLinearDamping;
    private float initialAngularDamping;
    private bool lockedAsPlatform;

    public Rigidbody Rigidbody => rb;
    public bool CanBeGrabbed => canBeGrabbed && canBeMoved && !lockedAsPlatform && isActiveAndEnabled;
    public bool IsGrabbed => grabber != null;
    public bool IsUsingPhysicalGrab => grabJoint != null;
    public bool IsLockedAsPlatform => lockedAsPlatform;
    public Vector3 CurrentGrabPoint => grabber != null ? transform.TransformPoint(grabLocalAnchor) : transform.position;
    public float PressureWeight => pressureWeight;
    public bool IsWeightActive => isActiveAndEnabled;
    public bool CanBlockLaser => canBlockLaser && isActiveAndEnabled;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        railOrigin = transform.position;

        if (applyPhysicsMassOnAwake)
            rb.mass = Mathf.Max(0.01f, physicsMass);

        if (freezeTiltRotations)
            rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        initialLinearDamping = rb.linearDamping;
        initialAngularDamping = rb.angularDamping;
    }

    private void Start()
    {
        CaptureInitialState();
    }

    public void BeginGrab(Transform grabberTransform)
    {
        if (!CanBeGrabbed || grabberTransform == null)
            return;

        grabber = grabberTransform;
        Vector3 fromGrabber = rb.position - grabber.position;
        fromGrabber.y = 0f;
        pushDirection = fromGrabber.sqrMagnitude > 0.001f ? fromGrabber.normalized : grabber.forward;
        grabLocalAnchor = transform.InverseTransformPoint(GetClosestPoint(grabber.position));
        lastGrabAnchorPosition = transform.TransformPoint(grabLocalAnchor);

        if (useContinuousCollisionWhileGrabbed)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    public void BeginGrab(Transform grabberTransform, Rigidbody anchorBody, Vector3 worldGrabPoint)
    {
        BeginGrab(grabberTransform, anchorBody, worldGrabPoint, Vector3.zero);
    }

    public void BeginGrab(Transform grabberTransform, Rigidbody anchorBody, Vector3 worldGrabPoint, Vector3 connectedAnchorLocal)
    {
        if (!CanBeGrabbed || grabberTransform == null || anchorBody == null || !usePhysicalGrab)
        {
            BeginGrab(grabberTransform);
            return;
        }

        grabber = grabberTransform;
        grabAnchorBody = anchorBody;
        grabLocalAnchor = transform.InverseTransformPoint(worldGrabPoint);
        grabRailOffset = worldGrabPoint - ClosestPointOnRail(worldGrabPoint);
        lastGrabAnchorPosition = worldGrabPoint;

        Vector3 fromGrabber = rb.position - grabber.position;
        fromGrabber.y = 0f;
        pushDirection = fromGrabber.sqrMagnitude > 0.001f ? fromGrabber.normalized : grabber.forward;

        if (useContinuousCollisionWhileGrabbed)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (increaseDampingWhileGrabbed)
        {
            rb.linearDamping = Mathf.Max(rb.linearDamping, grabbedLinearDamping);
            rb.angularDamping = Mathf.Max(rb.angularDamping, grabbedAngularDamping);
        }

        ConfigureGrabJoint();
        grabJoint.connectedAnchor = connectedAnchorLocal;
        rb.WakeUp();
    }

    public void EndGrab()
    {
        grabber = null;
        grabAnchorBody = null;

        if (grabJoint != null)
        {
            Destroy(grabJoint);
            grabJoint = null;
        }

        if (rb != null)
        {
            rb.collisionDetectionMode = initialCollisionDetectionMode;
            rb.linearDamping = initialLinearDamping;
            rb.angularDamping = initialAngularDamping;
        }
    }

    public void MoveByPlayer(Vector3 desiredDirection)
    {
        if (rb == null || grabber == null || !canBeMoved || lockedAsPlatform)
            return;

        desiredDirection.y = 0f;

        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        desiredDirection.Normalize();

        if (moveMode == MoveMode.PushOnly)
        {
            if (!canBePushed || Vector3.Dot(desiredDirection, pushDirection) < pushOnlyMinDot)
                return;
        }
        else if (moveMode == MoveMode.Rail)
        {
            desiredDirection = ProjectOnRail(desiredDirection);
            if (desiredDirection.sqrMagnitude <= 0.0001f)
                return;
        }

        Vector3 nextPosition = rb.position + desiredDirection * (playerMoveSpeed * Time.fixedDeltaTime);

        if (moveMode == MoveMode.Rail && snapToRailLine)
            nextPosition = ClosestPointOnRail(nextPosition);

        rb.MovePosition(nextPosition);
    }

    public void UpdatePhysicalGrab(Vector3 desiredDirection)
    {
        if (rb == null || grabber == null || grabJoint == null || lockedAsPlatform)
            return;

        if (moveMode == MoveMode.Rail && snapToRailLine)
        {
            Vector3 railPosition = ClosestPointOnRail(rb.position);
            Vector3 correctedPosition = Vector3.MoveTowards(rb.position, railPosition, railSnapStrength * Time.fixedDeltaTime);
            rb.MovePosition(correctedPosition);
        }
    }

    public Vector3 GetClosestPoint(Vector3 fromWorldPoint)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        Vector3 closestPoint = transform.position;
        float closestSqrDistance = float.MaxValue;

        foreach (Collider childCollider in colliders)
        {
            if (childCollider == null || childCollider.isTrigger)
                continue;

            Vector3 point = childCollider.ClosestPoint(fromWorldPoint);
            float sqrDistance = (point - fromWorldPoint).sqrMagnitude;

            if (sqrDistance >= closestSqrDistance)
                continue;

            closestSqrDistance = sqrDistance;
            closestPoint = point;
        }

        return closestPoint;
    }

    public Vector3 GetConstrainedGrabAnchorPosition(Vector3 desiredAnchorPosition, Vector3 desiredMoveDirection)
    {
        if (grabber == null)
            return desiredAnchorPosition;

        desiredMoveDirection.y = 0f;

        if (moveMode == MoveMode.PushOnly)
        {
            if (desiredMoveDirection.sqrMagnitude <= 0.0001f)
                return lastGrabAnchorPosition;

            desiredMoveDirection.Normalize();

            if (!canBePushed || Vector3.Dot(desiredMoveDirection, pushDirection) < pushOnlyMinDot)
                return lastGrabAnchorPosition;
        }
        else if (moveMode == MoveMode.Rail)
        {
            desiredAnchorPosition = ClosestPointOnRail(desiredAnchorPosition) + grabRailOffset;
        }

        lastGrabAnchorPosition = desiredAnchorPosition;
        return lastGrabAnchorPosition;
    }

    public void SetConnectedAnchorWorldPosition(Vector3 worldPosition)
    {
        if (grabJoint == null || grabAnchorBody == null)
            return;

        grabJoint.connectedAnchor = grabAnchorBody.transform.InverseTransformPoint(worldPosition);
    }

    public void BecomePlatformOnLava(Vector3 snapPosition)
    {
        if (!canBecomeLavaPlatform || rb == null)
            return;

        lockedAsPlatform = true;
        EndGrab();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeAll;
        transform.position = snapPosition;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
    }

    public void CaptureInitialState()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialKinematic = rb != null && rb.isKinematic;
        initialConstraints = rb != null ? rb.constraints : RigidbodyConstraints.None;
        initialCollisionDetectionMode = rb != null ? rb.collisionDetectionMode : CollisionDetectionMode.Discrete;
        railOrigin = transform.position;
    }

    public void ResetState()
    {
        EndGrab();
        lockedAsPlatform = false;
        transform.SetPositionAndRotation(initialPosition, initialRotation);

        if (rb == null)
            return;

        rb.isKinematic = initialKinematic;
        rb.constraints = initialConstraints;
        rb.collisionDetectionMode = initialCollisionDetectionMode;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.WakeUp();
    }

    public float GetWindMultiplier()
    {
        if (!canBeAffectedByWind || canBlockWind || lockedAsPlatform)
            return 0f;

        switch (weightClass)
        {
            case WeightClass.Light:
                return 1.35f;
            case WeightClass.Heavy:
                return 0.35f;
            default:
                return 1f;
        }
    }

    private Vector3 ProjectOnRail(Vector3 direction)
    {
        Vector3 normalizedRail = GetRailDirection();
        return Vector3.Project(direction, normalizedRail);
    }

    private Vector3 ClosestPointOnRail(Vector3 position)
    {
        Vector3 normalizedRail = GetRailDirection();
        Vector3 fromOrigin = position - railOrigin;
        return railOrigin + Vector3.Project(fromOrigin, normalizedRail);
    }

    private Vector3 GetRailDirection()
    {
        Vector3 direction = railDirection.sqrMagnitude > 0.0001f ? railDirection : Vector3.right;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.right;

        return direction.normalized;
    }

    private void ConfigureGrabJoint()
    {
        if (grabJoint != null)
        {
            Destroy(grabJoint);
            grabJoint = null;
        }

        grabJoint = gameObject.AddComponent<ConfigurableJoint>();
        grabJoint.connectedBody = grabAnchorBody;
        grabJoint.autoConfigureConnectedAnchor = false;
        grabJoint.anchor = grabLocalAnchor;
        grabJoint.connectedAnchor = Vector3.zero;
        grabJoint.xMotion = ConfigurableJointMotion.Limited;
        grabJoint.yMotion = ConfigurableJointMotion.Limited;
        grabJoint.zMotion = ConfigurableJointMotion.Limited;
        grabJoint.angularXMotion = ConfigurableJointMotion.Free;
        grabJoint.angularYMotion = ConfigurableJointMotion.Free;
        grabJoint.angularZMotion = ConfigurableJointMotion.Free;
        grabJoint.enableCollision = collideWithGrabberWhileGrabbed;
        grabJoint.enablePreprocessing = true;
        grabJoint.breakForce = grabBreakForce;
        grabJoint.breakTorque = grabBreakForce;
        grabJoint.projectionMode = JointProjectionMode.PositionAndRotation;
        grabJoint.projectionDistance = grabProjectionDistance;
        grabJoint.projectionAngle = 20f;

        SoftJointLimit linearLimit = grabJoint.linearLimit;
        linearLimit.limit = Mathf.Max(0.001f, grabSlack);
        grabJoint.linearLimit = linearLimit;

        SoftJointLimitSpring limitSpring = grabJoint.linearLimitSpring;
        limitSpring.spring = Mathf.Max(0f, grabSpring);
        limitSpring.damper = Mathf.Max(0f, grabDamper);
        grabJoint.linearLimitSpring = limitSpring;
    }

    private void OnJointBreak(float breakForce)
    {
        grabJoint = null;
        grabber = null;
        grabAnchorBody = null;

        if (rb == null)
            return;

        rb.collisionDetectionMode = initialCollisionDetectionMode;
        rb.linearDamping = initialLinearDamping;
        rb.angularDamping = initialAngularDamping;
    }

    private void OnDrawGizmosSelected()
    {
        if (moveMode != MoveMode.Rail)
            return;

        Vector3 origin = Application.isPlaying ? railOrigin : transform.position;
        Vector3 direction = railDirection.sqrMagnitude > 0.0001f ? railDirection.normalized : Vector3.right;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin - direction * 4f, origin + direction * 4f);
    }
}
