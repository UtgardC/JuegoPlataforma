using UnityEngine;

public class ResettableTransform : MonoBehaviour, IResettable
{
    public bool resetScale = false;
    public bool resetActiveState = true;
    public bool resetRigidbodyState = true;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 startScale;
    private bool startActive;
    private Rigidbody rb;
    private bool rbStartKinematic;
    private RigidbodyConstraints rbStartConstraints;
    private CollisionDetectionMode rbStartCollisionMode;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        CaptureInitialState();
    }

    public void CaptureInitialState()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        startScale = transform.localScale;
        startActive = gameObject.activeSelf;

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rbStartKinematic = rb.isKinematic;
            rbStartConstraints = rb.constraints;
            rbStartCollisionMode = rb.collisionDetectionMode;
        }
    }

    public void ResetState()
    {
        if (resetActiveState)
            gameObject.SetActive(startActive);

        transform.SetPositionAndRotation(startPosition, startRotation);

        if (resetScale)
            transform.localScale = startScale;

        if (!resetRigidbodyState || rb == null)
            return;

        rb.isKinematic = rbStartKinematic;
        rb.constraints = rbStartConstraints;
        rb.collisionDetectionMode = rbStartCollisionMode;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.WakeUp();
    }
}
