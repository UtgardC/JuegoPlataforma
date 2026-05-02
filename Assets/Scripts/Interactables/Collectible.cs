using System;
using UnityEngine;
using UnityEngine.Events;

public class Collectible : MonoBehaviour, IResettable
{
    public enum CollectibleType
    {
        Gear,
        Secondary,
        DoubleJumpTemporary,
        DoubleJumpUntilRespawn,
        DoubleJumpPermanent
    }

    [Serializable]
    public class CollectibleEvent : UnityEvent<Collectible> { }

    [Header("Collectible")]
    public CollectibleType collectibleType = CollectibleType.Gear;
    public string collectibleId;
    public int amount = 1;
    public float doubleJumpDuration = 20f;
    public bool resetWhenPuzzleResets = false;

    [Header("Events")]
    public CollectibleEvent onCollected;

    private bool initiallyActive;

    private void Start()
    {
        CaptureInitialState();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryCollect(other))
            return;

        onCollected.Invoke(this);
        gameObject.SetActive(false);
    }

    public virtual bool TryCollect(Collider other)
    {
        PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
        PlayerAbilities abilities = other.GetComponentInParent<PlayerAbilities>();

        switch (collectibleType)
        {
            case CollectibleType.Gear:
                return inventory != null && inventory.AddGear(collectibleId);
            case CollectibleType.Secondary:
                if (inventory == null)
                    return false;
                inventory.AddSecondary(amount);
                return true;
            case CollectibleType.DoubleJumpTemporary:
                if (abilities == null)
                    return false;
                abilities.GrantTemporaryDoubleJump(doubleJumpDuration);
                return true;
            case CollectibleType.DoubleJumpUntilRespawn:
                if (abilities == null)
                    return false;
                abilities.GrantDoubleJumpUntilRespawn();
                return true;
            case CollectibleType.DoubleJumpPermanent:
                if (abilities == null)
                    return false;
                abilities.UnlockPermanentDoubleJump();
                return true;
            default:
                return false;
        }
    }

    public void CaptureInitialState()
    {
        initiallyActive = gameObject.activeSelf;
    }

    public void ResetState()
    {
        if (resetWhenPuzzleResets)
            gameObject.SetActive(initiallyActive);
    }
}
