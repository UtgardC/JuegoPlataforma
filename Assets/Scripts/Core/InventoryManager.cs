using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Serializable]
    public class StringEvent : UnityEvent<string> { }

    [Serializable]
    public class IntEvent : UnityEvent<int> { }

    [Header("Events")]
    public StringEvent onGearCollected;
    public IntEvent onGearCountChanged;
    public IntEvent onSecondaryCountChanged;

    private readonly HashSet<string> collectedGears = new HashSet<string>();
    private int secondaryCount;

    public int GearCount => collectedGears.Count;
    public int SecondaryCount => secondaryCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public bool HasGear(string gearId)
    {
        return !string.IsNullOrWhiteSpace(gearId) && collectedGears.Contains(gearId);
    }

    public bool AddGear(string gearId)
    {
        if (string.IsNullOrWhiteSpace(gearId))
            gearId = Guid.NewGuid().ToString("N");

        if (!collectedGears.Add(gearId))
            return false;

        onGearCollected.Invoke(gearId);
        onGearCountChanged.Invoke(GearCount);
        return true;
    }

    public void AddSecondary(int amount = 1)
    {
        secondaryCount = Mathf.Max(0, secondaryCount + amount);
        onSecondaryCountChanged.Invoke(secondaryCount);
    }

    public void ClearSecondary()
    {
        secondaryCount = 0;
        onSecondaryCountChanged.Invoke(secondaryCount);
    }
}
