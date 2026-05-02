using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerInventory : MonoBehaviour
{
    [Serializable]
    public class StringEvent : UnityEvent<string> { }

    [Serializable]
    public class IntEvent : UnityEvent<int> { }

    [Header("Inventory Source")]
    public InventoryManager inventoryManager;

    [Header("Events")]
    public StringEvent onGearCollected;
    public IntEvent onGearCountChanged;
    public IntEvent onSecondaryCountChanged;

    public int GearCount => GetInventoryManager() != null ? GetInventoryManager().GearCount : 0;
    public int SecondaryCount => GetInventoryManager() != null ? GetInventoryManager().SecondaryCount : 0;

    private InventoryManager GetInventoryManager()
    {
        if (inventoryManager == null)
            inventoryManager = InventoryManager.Instance;

        if (inventoryManager == null)
            inventoryManager = FindAnyObjectByType<InventoryManager>();

        if (inventoryManager == null)
            inventoryManager = new GameObject("InventoryManager").AddComponent<InventoryManager>();

        return inventoryManager;
    }

    public bool AddGear(string gearId)
    {
        InventoryManager manager = GetInventoryManager();
        bool added = manager != null && manager.AddGear(gearId);

        if (added)
        {
            onGearCollected.Invoke(gearId);
            onGearCountChanged.Invoke(GearCount);
        }

        return added;
    }

    public void AddSecondary(int amount = 1)
    {
        InventoryManager manager = GetInventoryManager();
        if (manager == null)
            return;

        manager.AddSecondary(amount);
        onSecondaryCountChanged.Invoke(SecondaryCount);
    }
}
