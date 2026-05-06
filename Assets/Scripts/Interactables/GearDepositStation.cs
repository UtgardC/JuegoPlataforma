using UnityEngine;
using UnityEngine.Events;

public class GearDepositStation : MonoBehaviour
{
    [Header("Requirement")]
    public int requiredGears = 3;
    public bool depositOnPlayerEnter = true;
    public bool onlyDepositOnce = true;

    [Header("Victory")]
    public bool registerVictory = true;
    public bool markLevelCompleted = false;
    public int levelNumber = 1;
    public DoorActivator doorActivator;

    [Header("Events")]
    public UnityEvent onDepositAccepted;
    public UnityEvent onDepositRejected;
    public UnityEvent onVictory;

    private bool deposited;

    private void OnTriggerEnter(Collider other)
    {
        if (!depositOnPlayerEnter)
            return;

        PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
        if (inventory != null)
            TryDeposit(inventory);
    }

    public void TryDeposit(PlayerInventory inventory)
    {
        if (inventory == null)
            return;

        if (onlyDepositOnce && deposited)
            return;

        if (inventory.GearCount < requiredGears)
        {
            onDepositRejected.Invoke();
            return;
        }

        deposited = true;
        onDepositAccepted.Invoke();

        if (doorActivator != null)
            doorActivator.Open();

        if (markLevelCompleted)
            GameProgress.MarkLevelCompleted(levelNumber);

        if (registerVictory && GameManager.Instance != null)
            GameManager.Instance.RegisterVictory();

        onVictory.Invoke();
    }
}
