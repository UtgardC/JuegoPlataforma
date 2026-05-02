using UnityEngine;

[RequireComponent(typeof(MovableObject))]
public class RailMover : MonoBehaviour, IResettable
{
    [Header("Rail Limits")]
    public Transform railStart;
    public Transform railEnd;
    public bool clampToLimits = true;

    private MovableObject movable;
    private Rigidbody rb;
    private Vector3 initialPosition;

    private void Awake()
    {
        movable = GetComponent<MovableObject>();
        rb = GetComponent<Rigidbody>();
        ConfigureMovable();
    }

    private void Start()
    {
        CaptureInitialState();
    }

    private void LateUpdate()
    {
        if (!clampToLimits || railStart == null || railEnd == null)
            return;

        Vector3 clampedPosition = ClampPointToSegment(transform.position, railStart.position, railEnd.position);

        if ((clampedPosition - transform.position).sqrMagnitude <= 0.0001f)
            return;

        if (rb != null)
            rb.position = clampedPosition;
        else
            transform.position = clampedPosition;
    }

    public void CaptureInitialState()
    {
        initialPosition = transform.position;
    }

    public void ResetState()
    {
        if (rb != null)
            rb.position = initialPosition;
        else
            transform.position = initialPosition;
    }

    private void ConfigureMovable()
    {
        if (movable == null)
            return;

        movable.moveMode = MoveMode.Rail;

        if (railStart != null && railEnd != null)
            movable.railDirection = railEnd.position - railStart.position;
    }

    private static Vector3 ClampPointToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float segmentLengthSqr = segment.sqrMagnitude;

        if (segmentLengthSqr <= 0.0001f)
            return start;

        float t = Vector3.Dot(point - start, segment) / segmentLengthSqr;
        t = Mathf.Clamp01(t);
        return start + segment * t;
    }
}
