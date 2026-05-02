using UnityEngine;

public class DoorActivator : MonoBehaviour, IActivatable, IResettable
{
    [Header("Door")]
    public Transform doorVisual;
    public Vector3 openOffset = new Vector3(0f, 3f, 0f);
    public float moveSpeed = 3f;
    public bool startsOpen = false;

    private Vector3 closedPosition;
    private bool isOpen;

    public bool IsActive => isOpen;

    private void Awake()
    {
        if (doorVisual == null)
            doorVisual = transform;
    }

    private void Start()
    {
        CaptureInitialState();

        if (startsOpen)
            Open();
    }

    private void Update()
    {
        if (doorVisual == null)
            return;

        Vector3 targetPosition = closedPosition + (isOpen ? openOffset : Vector3.zero);
        doorVisual.localPosition = Vector3.MoveTowards(doorVisual.localPosition, targetPosition, moveSpeed * Time.deltaTime);
    }

    public void Open()
    {
        isOpen = true;
    }

    public void Close()
    {
        isOpen = false;
    }

    public void Activate()
    {
        Open();
    }

    public void Deactivate()
    {
        Close();
    }

    public void Toggle()
    {
        isOpen = !isOpen;
    }

    public void CaptureInitialState()
    {
        if (doorVisual == null)
            doorVisual = transform;

        closedPosition = doorVisual.localPosition;
        isOpen = startsOpen;
    }

    public void ResetState()
    {
        isOpen = startsOpen;

        if (doorVisual != null)
            doorVisual.localPosition = closedPosition + (isOpen ? openOffset : Vector3.zero);
    }
}
