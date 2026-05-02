using UnityEngine;

public class LaserBlocker : MonoBehaviour, ILaserBlocker
{
    public bool canBlockLaser = true;

    public bool CanBlockLaser => canBlockLaser && isActiveAndEnabled;
}
