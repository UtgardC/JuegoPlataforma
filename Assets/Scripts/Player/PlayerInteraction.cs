using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerInteraction : MonoBehaviour
{
    [Header("Detection")]
    public Transform interactionOrigin;
    public bool autoCreateInteractionOrigin = true;
    public Vector3 autoInteractionOriginLocalPosition = new Vector3(0f, 1.05f, 0.55f);
    public float interactionRange = 1.8f;
    public float interactionRadius = 0.6f;
    public LayerMask movableMask = ~0;

    [Header("Pull")]
    public bool faceTargetOnPull = true;
    public bool lockFacingWhilePulling = true;
    public bool snapFacingOnPull = true;
    public bool facePulledObjectContinuously = true;
    public float maxPullDistance = 2.6f;
    public PhysicalPullSettings defaultPullValues = PhysicalPullSettings.CreateDefault();

    [Header("Pull Link Visual")]
    public bool showPullLink = true;
    public LineRenderer pullLineRenderer;
    public float pullLineWidth = 0.035f;
    public Color pullLineColor = new Color(0.95f, 0.78f, 0.35f, 1f);

    [Header("Push Impulse")]
    public Transform pushOrigin;
    public float pushCooldown = 1f;
    public float pushImpulse = 80f;
    public Vector3 pushBoxHalfExtents = new Vector3(0.65f, 0.8f, 0.75f);
    public float pushBoxForwardOffset = 0.85f;
    public bool faceTargetOnPush = true;
    public bool snapFacingOnPush = true;

    [Header("Target Facing")]
    public bool faceTargetWhenAvailable = false;

    [Header("References")]
    public PlayerMotor motor;

    [Header("Events")]
    public UnityEvent onPullStarted;
    public UnityEvent onPullEnded;
    public UnityEvent onPushStarted;
    public UnityEvent onPushEnded;
    public UnityEvent onTargetChanged;

    [Header("Legacy Events")]
    public UnityEvent onGrabStarted;
    public UnityEvent onGrabEnded;

    private readonly Collider[] detectionResults = new Collider[16];
    private readonly Collider[] pushResults = new Collider[24];
    private readonly HashSet<MovableObject> pushedMovables = new HashSet<MovableObject>();
    private MovableObject currentTarget;
    private MovableObject pulledObject;
    private Vector3 currentMoveDirection;
    private Rigidbody playerBody;
    private Rigidbody kinematicPullAnchorBody;
    private bool usingKinematicPullAnchor;
    private Vector3 pullAnchorLocalPosition;
    private float nextPushTime;
    private Material pullLineMaterial;

    public MovableObject CurrentTarget => currentTarget;
    public MovableObject PulledObject => pulledObject;
    public MovableObject GrabbedObject => pulledObject;
    public bool IsPulling => pulledObject != null;
    public bool IsGrabbing => IsPulling;
    public bool IsInteracting => IsPulling;
    public bool HasAvailableTarget => currentTarget != null;
    public bool CanPushNow => Time.time >= nextPushTime;

    private void Awake()
    {
        if (interactionOrigin == null)
            interactionOrigin = autoCreateInteractionOrigin ? CreateDefaultInteractionOrigin() : transform;

        if (pushOrigin == null)
            pushOrigin = interactionOrigin;

        if (motor == null)
            motor = GetComponent<PlayerMotor>();

        playerBody = GetComponent<Rigidbody>();
        EnsurePullLineRenderer();
    }

    private void Update()
    {
        if (!IsInteracting)
            UpdateCurrentTarget();

        if (currentTarget != null && faceTargetWhenAvailable && motor != null)
            motor.RequestFaceTowards(currentTarget.GetClosestPoint(GetInteractionOrigin()));
    }

    private void FixedUpdate()
    {
        if (pulledObject == null)
            return;

        if (!pulledObject.IsPulled || !pulledObject.IsUsingPhysicalPull)
        {
            ReleasePull();
            return;
        }

        if (facePulledObjectContinuously && lockFacingWhilePulling && motor != null)
            motor.LockFacingTowards(pulledObject.CurrentPullPoint, false);

        UpdatePullAnchor();
        pulledObject.UpdatePull(GetDesiredPullAnchorPosition(), currentMoveDirection);

        if (IsPullTooFar())
            ReleasePull();
    }

    private void LateUpdate()
    {
        UpdatePullLinkVisual();
    }

    public void SetMoveDirection(Vector3 moveDirection)
    {
        currentMoveDirection = moveDirection;
    }

    public void TryStartPull()
    {
        if (pulledObject != null)
            return;

        MovableObject target = FindBestTarget(TargetMode.Pull);
        if (target == null)
            return;

        BeginPull(target);
    }

    public void TryStartPush()
    {
        TryPushImpulse();
    }

    public bool TryPushImpulse()
    {
        if (Time.time < nextPushTime)
            return false;

        nextPushTime = Time.time + Mathf.Max(0f, pushCooldown);

        Vector3 origin = GetPushOrigin();
        Vector3 forward = GetFlatForward();
        MovableObject facingTarget = FindBestTarget(TargetMode.Push);

        if (facingTarget != null && faceTargetOnPush && motor != null)
        {
            Vector3 pushPoint = facingTarget.GetClosestPoint(origin);
            motor.FaceTowardsWorldPoint(pushPoint, snapFacingOnPush);
            forward = GetDirectionOnPlane(pushPoint - origin, forward);
        }

        int hitCount = Physics.OverlapBoxNonAlloc(
            origin + forward * pushBoxForwardOffset,
            GetPositivePushHalfExtents(),
            pushResults,
            Quaternion.LookRotation(forward, Vector3.up),
            movableMask,
            QueryTriggerInteraction.Ignore);

        pushedMovables.Clear();
        bool pushedAny = false;
        onPushStarted.Invoke();

        for (int i = 0; i < hitCount; i++)
        {
            Collider candidateCollider = pushResults[i];
            if (candidateCollider == null)
                continue;

            MovableObject movable = candidateCollider.GetComponentInParent<MovableObject>();
            if (movable == null || !movable.CanBePushed || !pushedMovables.Add(movable))
                continue;

            Vector3 pushPoint = movable.GetClosestPoint(origin);
            Vector3 toTarget = pushPoint - origin;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.0001f && Vector3.Dot(forward, toTarget.normalized) < -0.1f)
                continue;

            pushedAny |= movable.ApplyPushImpulse(transform, forward, pushImpulse, pushPoint);
        }

        onPushEnded.Invoke();
        return pushedAny;
    }

    public void BeginPull(MovableObject target)
    {
        if (target == null || !target.CanBePulled)
            return;

        Vector3 pullPoint = target.GetClosestPoint(GetInteractionOrigin());

        if (faceTargetOnPull && motor != null)
        {
            if (lockFacingWhilePulling)
                motor.LockFacingTowards(pullPoint, snapFacingOnPull);
            else
                motor.FaceTowardsWorldPoint(pullPoint, snapFacingOnPull);
        }

        EnsureKinematicPullAnchor();
        pullAnchorLocalPosition = transform.InverseTransformPoint(pullPoint);

        Rigidbody anchorBody = playerBody != null && !playerBody.isKinematic ? playerBody : kinematicPullAnchorBody;
        Vector3 connectedAnchor = anchorBody.transform.InverseTransformPoint(pullPoint);

        if (!target.BeginPull(transform, anchorBody, pullPoint, connectedAnchor, defaultPullValues))
        {
            ClearFacingAfterPull();
            return;
        }

        pulledObject = target;
        usingKinematicPullAnchor = anchorBody == kinematicPullAnchorBody;

        if (usingKinematicPullAnchor)
            MoveKinematicPullAnchor(pullPoint, transform.rotation, true);

        SetPullLinkVisible(true);
        onPullStarted.Invoke();
        onGrabStarted.Invoke();
    }

    public void ReleasePull()
    {
        if (pulledObject == null)
            return;

        pulledObject.EndPull();
        pulledObject = null;

        if (usingKinematicPullAnchor)
            SetKinematicPullAnchorActive(false);

        usingKinematicPullAnchor = false;
        SetPullLinkVisible(false);
        ClearFacingAfterPull();
        onPullEnded.Invoke();
        onGrabEnded.Invoke();
    }

    public void ReleaseAll()
    {
        ReleasePull();
    }

    public void TryGrabOrRelease()
    {
        TryStartPull();
    }

    public void ReleaseGrab()
    {
        ReleasePull();
    }

    private void UpdateCurrentTarget()
    {
        MovableObject previousTarget = currentTarget;
        currentTarget = FindBestTarget(TargetMode.Any);

        if (currentTarget != previousTarget)
            onTargetChanged.Invoke();
    }

    private MovableObject FindBestTarget(TargetMode mode)
    {
        Vector3 origin = GetInteractionOrigin();
        int count = Physics.OverlapSphereNonAlloc(origin, interactionRange, detectionResults, movableMask, QueryTriggerInteraction.Ignore);
        MovableObject bestTarget = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider candidateCollider = detectionResults[i];
            if (candidateCollider == null)
                continue;

            MovableObject movable = candidateCollider.GetComponentInParent<MovableObject>();
            if (movable == null || !MatchesTargetMode(movable, mode))
                continue;

            Vector3 closestPoint = movable.GetClosestPoint(origin);
            Vector3 toCandidate = closestPoint - origin;
            float distance = toCandidate.magnitude;

            if (distance > interactionRange + interactionRadius)
                continue;

            float facingScore = toCandidate.sqrMagnitude > 0.0001f ? Vector3.Dot(transform.forward, toCandidate.normalized) : 0f;
            float score = distance - facingScore;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = movable;
            }
        }

        return bestTarget;
    }

    private static bool MatchesTargetMode(MovableObject movable, TargetMode mode)
    {
        switch (mode)
        {
            case TargetMode.Pull:
                return movable.CanBePulled;
            case TargetMode.Push:
                return movable.CanBePushed;
            default:
                return movable.CanBeInteracted;
        }
    }

    private void EnsureKinematicPullAnchor()
    {
        if (playerBody != null && !playerBody.isKinematic)
            return;

        if (kinematicPullAnchorBody != null)
            return;

        GameObject anchorObject = new GameObject($"{name}_PullAnchor");
        anchorObject.hideFlags = HideFlags.HideInHierarchy;

        kinematicPullAnchorBody = anchorObject.AddComponent<Rigidbody>();
        kinematicPullAnchorBody.isKinematic = true;
        kinematicPullAnchorBody.useGravity = false;
        kinematicPullAnchorBody.interpolation = RigidbodyInterpolation.Interpolate;
        kinematicPullAnchorBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private Transform CreateDefaultInteractionOrigin()
    {
        GameObject originObject = new GameObject("InteractionOrigin");
        originObject.transform.SetParent(transform);
        originObject.transform.localPosition = autoInteractionOriginLocalPosition;
        originObject.transform.localRotation = Quaternion.identity;
        originObject.transform.localScale = Vector3.one;
        return originObject.transform;
    }

    private void EnsurePullLineRenderer()
    {
        if (!showPullLink)
            return;

        if (pullLineRenderer == null)
        {
            GameObject lineObject = new GameObject($"{name}_PullLink");
            lineObject.transform.SetParent(transform);
            lineObject.transform.localPosition = Vector3.zero;
            lineObject.transform.localRotation = Quaternion.identity;
            lineObject.transform.localScale = Vector3.one;
            pullLineRenderer = lineObject.AddComponent<LineRenderer>();
        }

        pullLineRenderer.useWorldSpace = true;
        pullLineRenderer.positionCount = 2;
        pullLineRenderer.startWidth = Mathf.Max(0.001f, pullLineWidth);
        pullLineRenderer.endWidth = Mathf.Max(0.001f, pullLineWidth);
        pullLineRenderer.startColor = pullLineColor;
        pullLineRenderer.endColor = pullLineColor;

        if (pullLineRenderer.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                pullLineMaterial = new Material(shader);
                pullLineRenderer.sharedMaterial = pullLineMaterial;
            }
        }

        SetPullLinkVisible(false);
    }

    private void UpdatePullLinkVisual()
    {
        if (!showPullLink || pullLineRenderer == null || pulledObject == null)
            return;

        pullLineRenderer.SetPosition(0, GetDesiredPullAnchorPosition());
        pullLineRenderer.SetPosition(1, pulledObject.CurrentPullPoint);
    }

    private void SetPullLinkVisible(bool visible)
    {
        if (pullLineRenderer != null)
            pullLineRenderer.enabled = showPullLink && visible;
    }

    private void UpdatePullAnchor()
    {
        if (pulledObject == null || !usingKinematicPullAnchor)
            return;

        MoveKinematicPullAnchor(GetDesiredPullAnchorPosition(), transform.rotation, false);
    }

    private Vector3 GetDesiredPullAnchorPosition()
    {
        return transform.TransformPoint(pullAnchorLocalPosition);
    }

    private void MoveKinematicPullAnchor(Vector3 position, Quaternion rotation, bool teleport)
    {
        SetKinematicPullAnchorActive(true);

        if (teleport)
        {
            kinematicPullAnchorBody.position = position;
            kinematicPullAnchorBody.rotation = rotation;
            kinematicPullAnchorBody.transform.SetPositionAndRotation(position, rotation);
            return;
        }

        kinematicPullAnchorBody.MovePosition(position);
        kinematicPullAnchorBody.MoveRotation(rotation);
    }

    private void SetKinematicPullAnchorActive(bool active)
    {
        if (kinematicPullAnchorBody != null)
            kinematicPullAnchorBody.gameObject.SetActive(active);
    }

    private bool IsPullTooFar()
    {
        if (pulledObject == null)
            return false;

        return Vector3.Distance(GetInteractionOrigin(), pulledObject.CurrentPullPoint) > maxPullDistance;
    }

    private Vector3 GetInteractionOrigin()
    {
        return interactionOrigin != null ? interactionOrigin.position : transform.position;
    }

    private Vector3 GetPushOrigin()
    {
        if (pushOrigin != null)
            return pushOrigin.position;

        return GetInteractionOrigin();
    }

    private Vector3 GetFlatForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;

        return forward.normalized;
    }

    private static Vector3 GetDirectionOnPlane(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return fallback;

        return direction.normalized;
    }

    private Vector3 GetPositivePushHalfExtents()
    {
        return new Vector3(
            Mathf.Max(0.01f, pushBoxHalfExtents.x),
            Mathf.Max(0.01f, pushBoxHalfExtents.y),
            Mathf.Max(0.01f, pushBoxHalfExtents.z));
    }

    private void ClearFacingAfterPull()
    {
        if (lockFacingWhilePulling && motor != null)
            motor.ClearFacingLock();
    }

    private void OnDestroy()
    {
        if (kinematicPullAnchorBody != null)
            Destroy(kinematicPullAnchorBody.gameObject);

        if (pullLineMaterial != null)
            Destroy(pullLineMaterial);
    }

    private void OnDrawGizmosSelected()
    {
        Transform origin = interactionOrigin != null ? interactionOrigin : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin.position, interactionRange);

        Vector3 pushFrom = pushOrigin != null ? pushOrigin.position : origin.position;
        Vector3 forward = Application.isPlaying ? GetFlatForward() : transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.color = Color.yellow;
        Gizmos.matrix = Matrix4x4.TRS(
            pushFrom + forward * pushBoxForwardOffset,
            Quaternion.LookRotation(forward, Vector3.up),
            Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, GetPositivePushHalfExtents() * 2f);
        Gizmos.matrix = previousMatrix;
    }

    private enum TargetMode
    {
        Any,
        Pull,
        Push
    }
}
