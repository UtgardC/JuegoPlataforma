using UnityEngine;

public class WeightedObject : MonoBehaviour, IWeightedObject
{
    public float pressureWeight = 1f;
    public bool countsWhileDisabled = false;

    public float PressureWeight => pressureWeight;
    public bool IsWeightActive => countsWhileDisabled || isActiveAndEnabled;
}
