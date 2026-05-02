using UnityEngine;

public class CameraTargetFollower : MonoBehaviour
{
    [Header("Target")]
    public Transform followTarget;
    public Vector3 localOffset = new Vector3(0f, 1.4f, 0f);
    public bool matchTargetRotation = false;

    private void LateUpdate()
    {
        if (followTarget == null)
            return;

        transform.position = followTarget.TransformPoint(localOffset);

        if (matchTargetRotation)
            transform.rotation = followTarget.rotation;
    }
}
