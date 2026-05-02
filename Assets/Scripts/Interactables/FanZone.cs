using UnityEngine;

public class FanZone : MonoBehaviour, IActivatable, IResettable
{
    [Header("Force")]
    public Transform directionReference;
    public float playerAcceleration = 18f;
    public float rigidbodyAcceleration = 12f;
    public bool startsActive = true;

    [Header("Filtering")]
    public LayerMask affectedLayers = ~0;

    private bool active;
    private bool initialActive;

    public bool IsActive => active;

    private void Awake()
    {
        if (directionReference == null)
            directionReference = transform;
    }

    private void Start()
    {
        CaptureInitialState();
        active = startsActive;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!active || !IsLayerAffected(other.gameObject.layer))
            return;

        Vector3 forceDirection = directionReference != null ? directionReference.forward.normalized : transform.forward.normalized;

        PlayerMotor playerMotor = other.GetComponentInParent<PlayerMotor>();
        if (playerMotor != null)
        {
            playerMotor.AddExternalAcceleration(forceDirection * playerAcceleration);
            return;
        }

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null)
            return;

        MovableObject movable = rb.GetComponent<MovableObject>();
        float windMultiplier = movable != null ? movable.GetWindMultiplier() : 1f;

        if (windMultiplier <= 0f)
            return;

        rb.AddForce(forceDirection * (rigidbodyAcceleration * windMultiplier), ForceMode.Acceleration);
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
        initialActive = startsActive;
    }

    public void ResetState()
    {
        active = initialActive;
    }

    private bool IsLayerAffected(int layer)
    {
        return (affectedLayers.value & (1 << layer)) != 0;
    }

    private void OnDrawGizmosSelected()
    {
        Transform reference = directionReference != null ? directionReference : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(reference.position, reference.forward * 2f);
    }
}
