using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public struct PhysicalPullSettings
{
    public PullLinkMode linkMode;
    public float ropeLength;
    public float spring;
    public float damper;
    public float slack;
    public float breakForce;
    public float projectionDistance;
    public bool collideWithPuller;
    public float massScale;
    public float connectedMassScale;
    public float linearDamping;
    public float angularDamping;

    public static PhysicalPullSettings CreateDefault()
    {
        return new PhysicalPullSettings
        {
            linkMode = PullLinkMode.Rope,
            ropeLength = 0.85f,
            spring = 1400f,
            damper = 120f,
            slack = 0.06f,
            breakForce = 12000f,
            projectionDistance = 0.35f,
            collideWithPuller = true,
            massScale = 1f,
            connectedMassScale = 1f,
            linearDamping = 2f,
            angularDamping = 3f
        };
    }
}

public enum PullLinkMode
{
    Rope,
    Spring
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class MovableObject : MonoBehaviour, IResettable, IWeightedObject, ILaserBlocker
{
    [Header("Interaction")]
    [FormerlySerializedAs("canBePulled")]
    public bool canBeGrabbed = true;
    public bool canBePushed = true;

    [Header("Gameplay Properties")]
    public float physicsMass = 10f;
    public float pressureWeight = 1f;
    public bool canBlockLaser = true;
    public bool canBlockWind = false;
    public bool canBeAffectedByWind = true;

    [Header("Runtime Physics Material")]
    public bool useRuntimePhysicsMaterial = false;
    public float staticFriction = 0.6f;
    public float dynamicFriction = 0.6f;
    public float bounciness = 0f;
    public PhysicsMaterialCombine frictionCombine = PhysicsMaterialCombine.Average;
    public PhysicsMaterialCombine bounceCombine = PhysicsMaterialCombine.Average;

    [Header("Rigidbody Setup")]
    public bool applyPhysicsMassOnAwake = true;
    public bool freezeTiltRotations = true;
    public bool continuousCollisionWhilePulled = true;

    [Header("Pull Joint")]
    public bool useDefaultPullValues = true;
    public PhysicalPullSettings customPullValues = PhysicalPullSettings.CreateDefault();

    private Rigidbody rb;
    private Transform puller;
    private Rigidbody pullAnchorBody;
    private ConfigurableJoint pullJoint;
    private Vector3 pullLocalAnchor;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool startKinematic;
    private RigidbodyConstraints startConstraints;
    private CollisionDetectionMode startCollisionMode;
    private float startMass;
    private float startLinearDamping;
    private float startAngularDamping;
    private PhysicsMaterial runtimePhysicsMaterial;

    protected Rigidbody Body => rb;
    protected Transform Puller => puller;

    public Rigidbody Rigidbody => rb;
    public bool CanBePulled => canBeGrabbed && isActiveAndEnabled;
    public bool CanBePushed => canBePushed && isActiveAndEnabled;
    public bool CanBeInteracted => CanBePulled || CanBePushed;
    public bool IsPulled => puller != null;
    public bool IsGrabbed => IsPulled;
    public bool IsUsingPhysicalPull => pullJoint != null;
    public Vector3 CurrentPullPoint => puller != null ? transform.TransformPoint(pullLocalAnchor) : transform.position;
    public Vector3 CurrentGrabPoint => CurrentPullPoint;
    public float PressureWeight => pressureWeight;
    public bool IsWeightActive => isActiveAndEnabled;
    public bool CanBlockLaser => canBlockLaser && isActiveAndEnabled;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (applyPhysicsMassOnAwake)
            rb.mass = Mathf.Max(0.01f, physicsMass);

        if (freezeTiltRotations)
            rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (useRuntimePhysicsMaterial)
            ApplyRuntimePhysicsMaterial();

        startMass = rb.mass;
        startLinearDamping = rb.linearDamping;
        startAngularDamping = rb.angularDamping;
    }

    protected virtual void Start()
    {
        CaptureInitialState();
    }

    public virtual bool BeginPull(
        Transform pullerTransform,
        Rigidbody anchorBody,
        Vector3 worldPullPoint,
        Vector3 connectedAnchorLocal,
        PhysicalPullSettings defaultPullValues)
    {
        if (!CanBePulled || pullerTransform == null || anchorBody == null)
            return false;

        puller = pullerTransform;
        pullAnchorBody = anchorBody;
        pullLocalAnchor = transform.InverseTransformPoint(worldPullPoint);

        PhysicalPullSettings pullValues = useDefaultPullValues ? defaultPullValues : customPullValues;

        if (continuousCollisionWhilePulled)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        rb.linearDamping = Mathf.Max(rb.linearDamping, pullValues.linearDamping);
        rb.angularDamping = Mathf.Max(rb.angularDamping, pullValues.angularDamping);

        OnPullStarted(worldPullPoint);
        ConfigurePullJoint(connectedAnchorLocal, pullValues);
        rb.WakeUp();
        return true;
    }

    public virtual void EndPull()
    {
        puller = null;
        pullAnchorBody = null;

        if (pullJoint != null)
        {
            Destroy(pullJoint);
            pullJoint = null;
        }

        RestoreBaseRigidbodyTuning();
    }

    public virtual void UpdatePull(Vector3 desiredAnchorPosition, Vector3 desiredMoveDirection)
    {
        if (pullJoint == null || pullAnchorBody == null)
            return;

        Vector3 constrainedPosition = ConstrainPullAnchorPosition(desiredAnchorPosition, desiredMoveDirection);
        pullJoint.connectedAnchor = pullAnchorBody.transform.InverseTransformPoint(constrainedPosition);
        OnPullFixedUpdate(desiredMoveDirection);
    }

    public virtual bool ApplyPushImpulse(Transform pusherTransform, Vector3 desiredPushDirection, float impulse, Vector3 worldPoint)
    {
        if (!CanBePushed || rb == null)
            return false;

        desiredPushDirection.y = 0f;
        if (desiredPushDirection.sqrMagnitude <= 0.0001f)
            return false;

        Vector3 forceDirection = ConstrainPushDirection(desiredPushDirection.normalized);
        forceDirection.y = 0f;

        if (forceDirection.sqrMagnitude <= 0.0001f)
            return false;

        rb.WakeUp();
        rb.AddForceAtPosition(forceDirection.normalized * Mathf.Max(0f, impulse), worldPoint, ForceMode.Impulse);
        OnPushImpulse(pusherTransform, forceDirection.normalized, impulse, worldPoint);
        return true;
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

    public virtual void CaptureInitialState()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        startPosition = transform.position;
        startRotation = transform.rotation;
        startKinematic = rb.isKinematic;
        startConstraints = rb.constraints;
        startCollisionMode = rb.collisionDetectionMode;
        startMass = rb.mass;
        startLinearDamping = rb.linearDamping;
        startAngularDamping = rb.angularDamping;
    }

    public virtual void ResetState()
    {
        EndPull();
        transform.SetPositionAndRotation(startPosition, startRotation);

        rb.isKinematic = startKinematic;
        rb.constraints = startConstraints;
        rb.collisionDetectionMode = startCollisionMode;
        rb.mass = startMass;
        rb.linearDamping = startLinearDamping;
        rb.angularDamping = startAngularDamping;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.WakeUp();
    }

    public float GetWindMultiplier()
    {
        if (!canBeAffectedByWind || canBlockWind)
            return 0f;

        return 1f;
    }

    protected virtual void OnPullStarted(Vector3 worldPullPoint) { }
    protected virtual void OnPullFixedUpdate(Vector3 desiredMoveDirection) { }
    protected virtual void OnPushImpulse(Transform pusherTransform, Vector3 direction, float impulse, Vector3 worldPoint) { }

    protected virtual Vector3 ConstrainPullAnchorPosition(Vector3 desiredAnchorPosition, Vector3 desiredMoveDirection)
    {
        return desiredAnchorPosition;
    }

    protected virtual Vector3 ConstrainPushDirection(Vector3 desiredPushDirection)
    {
        return desiredPushDirection;
    }

    private void ConfigurePullJoint(Vector3 connectedAnchorLocal, PhysicalPullSettings pullValues)
    {
        if (pullJoint != null)
            Destroy(pullJoint);

        pullJoint = gameObject.AddComponent<ConfigurableJoint>();
        pullJoint.connectedBody = pullAnchorBody;
        pullJoint.autoConfigureConnectedAnchor = false;
        pullJoint.anchor = pullLocalAnchor;
        pullJoint.connectedAnchor = connectedAnchorLocal;
        pullJoint.xMotion = ConfigurableJointMotion.Limited;
        pullJoint.yMotion = ConfigurableJointMotion.Limited;
        pullJoint.zMotion = ConfigurableJointMotion.Limited;
        pullJoint.angularXMotion = ConfigurableJointMotion.Free;
        pullJoint.angularYMotion = ConfigurableJointMotion.Free;
        pullJoint.angularZMotion = ConfigurableJointMotion.Free;
        pullJoint.enableCollision = pullValues.collideWithPuller;
        pullJoint.enablePreprocessing = true;
        pullJoint.breakForce = pullValues.breakForce;
        pullJoint.breakTorque = pullValues.breakForce;
        pullJoint.projectionMode = JointProjectionMode.PositionAndRotation;
        pullJoint.projectionDistance = pullValues.projectionDistance;
        pullJoint.projectionAngle = 20f;
        pullJoint.massScale = Mathf.Max(0.0001f, pullValues.massScale);
        pullJoint.connectedMassScale = Mathf.Max(0.0001f, pullValues.connectedMassScale);

        SoftJointLimit linearLimit = pullJoint.linearLimit;
        linearLimit.limit = GetLinearLimit(pullValues);
        pullJoint.linearLimit = linearLimit;

        SoftJointLimitSpring limitSpring = pullJoint.linearLimitSpring;
        limitSpring.spring = pullValues.linkMode == PullLinkMode.Spring ? Mathf.Max(0f, pullValues.spring) : 0f;
        limitSpring.damper = pullValues.linkMode == PullLinkMode.Spring ? Mathf.Max(0f, pullValues.damper) : 0f;
        pullJoint.linearLimitSpring = limitSpring;

        ConfigureLinearMotion(pullValues.linkMode);
    }

    private void ConfigureLinearMotion(PullLinkMode linkMode)
    {
        switch (linkMode)
        {
            case PullLinkMode.Spring:
            case PullLinkMode.Rope:
            default:
                pullJoint.xMotion = ConfigurableJointMotion.Limited;
                pullJoint.yMotion = ConfigurableJointMotion.Limited;
                pullJoint.zMotion = ConfigurableJointMotion.Limited;
                return;
        }
    }

    private static float GetLinearLimit(PhysicalPullSettings pullValues)
    {
        switch (pullValues.linkMode)
        {
            case PullLinkMode.Rope:
                return Mathf.Max(0.001f, pullValues.ropeLength);
            case PullLinkMode.Spring:
                return Mathf.Max(0.001f, pullValues.slack);
            default:
                return Mathf.Max(0.001f, pullValues.ropeLength);
        }
    }

    private void RestoreBaseRigidbodyTuning()
    {
        if (rb == null)
            return;

        rb.collisionDetectionMode = startCollisionMode;
        rb.linearDamping = startLinearDamping;
        rb.angularDamping = startAngularDamping;
    }

    private void ApplyRuntimePhysicsMaterial()
    {
        runtimePhysicsMaterial = new PhysicsMaterial($"{name}_RuntimePhysicsMaterial")
        {
            staticFriction = Mathf.Max(0f, staticFriction),
            dynamicFriction = Mathf.Max(0f, dynamicFriction),
            bounciness = Mathf.Clamp01(bounciness),
            frictionCombine = frictionCombine,
            bounceCombine = bounceCombine
        };

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider childCollider in colliders)
        {
            if (childCollider == null || childCollider.isTrigger)
                continue;

            childCollider.material = runtimePhysicsMaterial;
        }
    }

    private void OnJointBreak(float breakForce)
    {
        pullJoint = null;
        puller = null;
        pullAnchorBody = null;
        RestoreBaseRigidbodyTuning();
    }
}
