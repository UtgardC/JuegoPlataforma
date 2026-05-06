using UnityEngine;

public class RailMovableObject : MovableObject
{
    [Header("Rail")]
    public Vector3 railDirection = Vector3.right;
    public bool snapToRailLine = true;
    public float railSnapSpeed = 12f;

    [Header("Rail Limits")]
    public Transform railStart;
    public Transform railEnd;
    public bool clampToLimits = true;

    private Vector3 railOrigin;
    private Vector3 pullRailOffset;

    protected override void Awake()
    {
        base.Awake();
        railOrigin = transform.position;
    }

    public override void CaptureInitialState()
    {
        base.CaptureInitialState();
        railOrigin = transform.position;
    }

    protected override void OnPullStarted(Vector3 worldPullPoint)
    {
        pullRailOffset = worldPullPoint - ClosestPointOnRail(worldPullPoint);
    }

    protected override Vector3 ConstrainPullAnchorPosition(Vector3 desiredAnchorPosition, Vector3 desiredMoveDirection)
    {
        return ClosestPointOnRail(desiredAnchorPosition) + pullRailOffset;
    }

    protected override Vector3 ConstrainPushDirection(Vector3 desiredPushDirection)
    {
        Vector3 direction = Vector3.Project(desiredPushDirection, GetRailDirection());
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    protected override void OnPullFixedUpdate(Vector3 desiredMoveDirection)
    {
        KeepBodyOnRail();
    }

    protected override void OnPushImpulse(Transform pusherTransform, Vector3 direction, float impulse, Vector3 worldPoint)
    {
        KeepBodyOnRail();
    }

    private void FixedUpdate()
    {
        if (snapToRailLine || clampToLimits)
            KeepBodyOnRail();
    }

    private void KeepBodyOnRail()
    {
        if (Body == null)
            return;

        Vector3 targetPosition = Body.position;

        if (snapToRailLine)
            targetPosition = ClosestPointOnRail(targetPosition);

        if (clampToLimits && railStart != null && railEnd != null)
            targetPosition = ClampPointToSegment(targetPosition, railStart.position, railEnd.position);

        if ((targetPosition - Body.position).sqrMagnitude <= 0.0001f)
            return;

        Body.MovePosition(Vector3.MoveTowards(Body.position, targetPosition, railSnapSpeed * Time.fixedDeltaTime));
    }

    private Vector3 ClosestPointOnRail(Vector3 position)
    {
        Vector3 direction = GetRailDirection();
        Vector3 fromOrigin = position - GetRailOrigin();
        return GetRailOrigin() + Vector3.Project(fromOrigin, direction);
    }

    private Vector3 GetRailOrigin()
    {
        if (railStart != null)
            return railStart.position;

        return railOrigin;
    }

    private Vector3 GetRailDirection()
    {
        Vector3 direction = railDirection;

        if (railStart != null && railEnd != null)
            direction = railEnd.position - railStart.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.right;

        return direction.normalized;
    }

    private static Vector3 ClampPointToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float segmentLengthSqr = segment.sqrMagnitude;

        if (segmentLengthSqr <= 0.0001f)
            return start;

        float t = Vector3.Dot(point - start, segment) / segmentLengthSqr;
        return start + segment * Mathf.Clamp01(t);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = railStart != null ? railStart.position : transform.position;
        Vector3 direction = railDirection.sqrMagnitude > 0.0001f ? railDirection.normalized : Vector3.right;

        if (railStart != null && railEnd != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(railStart.position, railEnd.position);
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin - direction * 4f, origin + direction * 4f);
    }
}
