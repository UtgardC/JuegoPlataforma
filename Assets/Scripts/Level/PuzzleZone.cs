using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PuzzleZone : MonoBehaviour
{
    [Header("Resettable Objects")]
    public bool autoCollectResettableChildren = false;
    public List<MonoBehaviour> resettableBehaviours = new List<MonoBehaviour>();

    [Header("Events")]
    public UnityEvent onStateCaptured;
    public UnityEvent onZoneReset;

    private bool captured;

    private void Awake()
    {
        if (autoCollectResettableChildren)
            CollectResettableChildren();
    }

    private void Start()
    {
        CaptureInitialState();
    }

    public void CaptureInitialState()
    {
        foreach (MonoBehaviour behaviour in resettableBehaviours)
        {
            if (behaviour is IResettable resettable)
                resettable.CaptureInitialState();
        }

        captured = true;
        onStateCaptured.Invoke();
    }

    public void ResetZone()
    {
        if (!captured)
            CaptureInitialState();

        foreach (MonoBehaviour behaviour in resettableBehaviours)
        {
            if (behaviour is IResettable resettable)
                resettable.ResetState();
        }

        onZoneReset.Invoke();
    }

    public void Register(MonoBehaviour behaviour)
    {
        if (behaviour == null || resettableBehaviours.Contains(behaviour))
            return;

        resettableBehaviours.Add(behaviour);
    }

    [ContextMenu("Collect Resettable Children")]
    public void CollectResettableChildren()
    {
        resettableBehaviours.Clear();
        MonoBehaviour[] children = GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour child in children)
        {
            if (child == this)
                continue;

            if (child is IResettable)
                resettableBehaviours.Add(child);
        }
    }
}
