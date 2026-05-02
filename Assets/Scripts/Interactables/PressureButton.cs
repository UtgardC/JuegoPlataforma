using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PressureButton : MonoBehaviour, IResettable
{
    [Header("Weight")]
    public float requiredWeight = 1f;

    [Header("Visual")]
    public Transform buttonVisual;
    public Vector3 pressedLocalOffset = new Vector3(0f, -0.08f, 0f);
    public float visualMoveSpeed = 10f;

    [Header("Events")]
    public UnityEvent onPressed;
    public UnityEvent onReleased;
    public UnityEvent<float> onWeightChanged;

    private readonly Dictionary<Component, int> overlappingObjects = new Dictionary<Component, int>();
    private Vector3 releasedLocalPosition;
    private bool isPressed;
    private float currentWeight;

    public bool IsPressed => isPressed;
    public float CurrentWeight => currentWeight;

    private void Awake()
    {
        if (buttonVisual == null)
            buttonVisual = transform;

        releasedLocalPosition = buttonVisual.localPosition;
    }

    private void Start()
    {
        CaptureInitialState();
    }

    private void Update()
    {
        if (buttonVisual == null)
            return;

        Vector3 targetPosition = releasedLocalPosition + (isPressed ? pressedLocalOffset : Vector3.zero);
        buttonVisual.localPosition = Vector3.Lerp(buttonVisual.localPosition, targetPosition, visualMoveSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryFindWeightedObject(other, out Component owner))
            return;

        if (!overlappingObjects.ContainsKey(owner))
            overlappingObjects.Add(owner, 0);

        overlappingObjects[owner]++;
        Recalculate();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!TryFindWeightedObject(other, out Component owner))
            return;

        if (!overlappingObjects.ContainsKey(owner))
            return;

        overlappingObjects[owner]--;

        if (overlappingObjects[owner] <= 0)
            overlappingObjects.Remove(owner);

        Recalculate();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!TryFindWeightedObject(other, out Component owner))
            return;

        if (overlappingObjects.ContainsKey(owner))
            return;

        overlappingObjects.Add(owner, 1);
        Recalculate();
    }

    public void CaptureInitialState()
    {
        if (buttonVisual != null)
            releasedLocalPosition = buttonVisual.localPosition;
    }

    public void ResetState()
    {
        bool wasPressed = isPressed;
        overlappingObjects.Clear();
        currentWeight = 0f;
        isPressed = false;

        if (buttonVisual != null)
            buttonVisual.localPosition = releasedLocalPosition;

        onWeightChanged.Invoke(currentWeight);

        if (wasPressed)
            onReleased.Invoke();
    }

    private void Recalculate()
    {
        float totalWeight = 0f;
        List<Component> staleObjects = null;

        foreach (Component owner in overlappingObjects.Keys)
        {
            if (owner == null)
            {
                staleObjects ??= new List<Component>();
                staleObjects.Add(owner);
                continue;
            }

            IWeightedObject weightedObject = owner as IWeightedObject;
            if (weightedObject != null && weightedObject.IsWeightActive)
                totalWeight += weightedObject.PressureWeight;
        }

        if (staleObjects != null)
        {
            foreach (Component staleObject in staleObjects)
                overlappingObjects.Remove(staleObject);
        }

        if (!Mathf.Approximately(totalWeight, currentWeight))
        {
            currentWeight = totalWeight;
            onWeightChanged.Invoke(currentWeight);
        }

        bool shouldBePressed = currentWeight >= requiredWeight;
        if (shouldBePressed == isPressed)
            return;

        isPressed = shouldBePressed;

        if (isPressed)
            onPressed.Invoke();
        else
            onReleased.Invoke();
    }

    private static bool TryFindWeightedObject(Collider collider, out Component owner)
    {
        WeightedObject weightedObject = collider.GetComponentInParent<WeightedObject>();
        if (weightedObject != null)
        {
            owner = weightedObject;
            return true;
        }

        MovableObject movableObject = collider.GetComponentInParent<MovableObject>();
        if (movableObject != null)
        {
            owner = movableObject;
            return true;
        }

        owner = null;
        return false;
    }
}
