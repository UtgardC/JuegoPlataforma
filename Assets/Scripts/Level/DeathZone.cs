using UnityEngine;

public class DeathZone : MonoBehaviour
{
    public bool killOnStay = false;

    private void OnTriggerEnter(Collider other)
    {
        TryKill(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (killOnStay)
            TryKill(other);
    }

    private void TryKill(Collider other)
    {
        PlayerRespawn player = other.GetComponentInParent<PlayerRespawn>();
        if (player != null)
            player.Die();
    }
}
