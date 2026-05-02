using UnityEngine;

public class LavaZone : MonoBehaviour
{
    [Header("Lava")]
    public bool killsPlayer = true;
    public bool convertsValidBoxesToPlatforms = true;
    public float platformSurfaceY = 0f;
    public bool useTransformYAsSurface = true;
    public bool keepBoxXZPosition = true;
    public Vector3 platformOffset = Vector3.zero;

    private void OnTriggerEnter(Collider other)
    {
        HandleCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        HandleCollider(other);
    }

    private void HandleCollider(Collider other)
    {
        if (killsPlayer)
        {
            PlayerRespawn player = other.GetComponentInParent<PlayerRespawn>();
            if (player != null)
            {
                player.Die();
                return;
            }
        }

        if (!convertsValidBoxesToPlatforms)
            return;

        MovableObject movable = other.GetComponentInParent<MovableObject>();
        if (movable == null || !movable.canBecomeLavaPlatform || movable.IsLockedAsPlatform)
            return;

        Vector3 snapPosition = keepBoxXZPosition ? movable.transform.position : transform.position;
        snapPosition.y = useTransformYAsSurface ? transform.position.y : platformSurfaceY;
        snapPosition += platformOffset;
        movable.BecomePlatformOnLava(snapPosition);
    }
}
