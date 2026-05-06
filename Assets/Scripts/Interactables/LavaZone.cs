using UnityEngine;

public class LavaZone : MonoBehaviour
{
    [Header("Lava")]
    public bool killsPlayer = true;

    private void OnTriggerEnter(Collider other)
    {
        TryKillPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryKillPlayer(other);
    }

    private void TryKillPlayer(Collider other)
    {
        if (!killsPlayer)
            return;

        PlayerRespawn player = other.GetComponentInParent<PlayerRespawn>();
        if (player != null)
            player.Die();
    }
}
