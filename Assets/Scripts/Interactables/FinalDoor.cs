using UnityEngine;
using UnityEngine.Events;

public class FinalDoor : MonoBehaviour, IResettable
{
    [Header("Requirement")]
    public int requiredGears = 3;
    public bool openAutomatically = true;

    [Header("Door")]
    public DoorActivator doorActivator;

    [Header("Events")]
    public UnityEvent onRequirementMet;
    public UnityEvent onVictoryTriggered;

    private bool opened;

    private void Awake()
    {
        if (doorActivator == null)
            doorActivator = GetComponent<DoorActivator>();
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
        if (inventory == null)
            return;

        TryOpen(inventory);

        if (opened)
            TriggerVictory();
    }

    public void TryOpen(PlayerInventory inventory)
    {
        if (inventory == null || inventory.GearCount < requiredGears || opened)
            return;

        opened = true;
        onRequirementMet.Invoke();

        if (openAutomatically && doorActivator != null)
            doorActivator.Open();
    }

    public void CheckGlobalInventory()
    {
        PlayerInventory inventory = FindAnyObjectByType<PlayerInventory>();
        TryOpen(inventory);
    }

    public void TriggerVictory()
    {
        onVictoryTriggered.Invoke();

        if (GameManager.Instance != null)
            GameManager.Instance.RegisterVictory();
    }

    public void CaptureInitialState()
    {
        opened = false;
    }

    public void ResetState()
    {
        opened = false;
    }
}
