using UnityEngine;

[DisallowMultipleComponent]
public class MovingPlatform : MonoBehaviour, IActivatable, IResettable
{
    [Header("Path")]
    public Transform pointA;
    public Transform pointB;
    public float speed = 2f;
    public bool startsActive = false;
    public bool pingPong = true;

    private Rigidbody rb;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool active;
    private bool movingToB = true;

    public bool IsActive => active;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        CaptureInitialState();
        active = startsActive;
    }

    private void FixedUpdate()
    {
        if (!active || pointA == null || pointB == null)
            return;

        Vector3 target = movingToB ? pointB.position : pointA.position;
        Vector3 nextPosition = Vector3.MoveTowards(transform.position, target, speed * Time.fixedDeltaTime);

        if (rb != null)
            rb.MovePosition(nextPosition);
        else
            transform.position = nextPosition;

        if ((transform.position - target).sqrMagnitude <= 0.0004f)
        {
            if (pingPong)
                movingToB = !movingToB;
            else
                active = false;
        }
    }

    public void Activate()
    {
        active = true;
    }

    public void Deactivate()
    {
        active = false;
    }

    public void Toggle()
    {
        active = !active;
    }

    public void CaptureInitialState()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        movingToB = true;
        active = startsActive;
    }

    public void ResetState()
    {
        active = startsActive;
        movingToB = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = initialPosition;
            rb.rotation = initialRotation;
        }
        else
        {
            transform.SetPositionAndRotation(initialPosition, initialRotation);
        }
    }
}
