using UnityEngine;
using UnityEngine.Events;

public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint")]
    public Transform respawnPoint;
    public PuzzleZone puzzleZone;
    public bool capturePuzzleStateOnActivate = false;

    [Header("Events")]
    public UnityEvent onActivated;

    private void Awake()
    {
        if (respawnPoint == null)
            respawnPoint = transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerRespawn player = other.GetComponentInParent<PlayerRespawn>();
        if (player == null)
            return;

        Activate(player);
    }

    public void Activate(PlayerRespawn player)
    {
        player.SetCheckpoint(this);

        if (CheckpointManager.Instance != null)
            CheckpointManager.Instance.SetCurrentCheckpoint(this);

        if (capturePuzzleStateOnActivate && puzzleZone != null)
            puzzleZone.CaptureInitialState();

        onActivated.Invoke();
    }

    public Vector3 GetRespawnPosition()
    {
        return respawnPoint != null ? respawnPoint.position : transform.position;
    }

    public Quaternion GetRespawnRotation()
    {
        return respawnPoint != null ? respawnPoint.rotation : transform.rotation;
    }
}
