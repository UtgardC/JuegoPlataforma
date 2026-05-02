using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerRespawn : MonoBehaviour
{
    [Header("Respawn")]
    public Transform defaultSpawnPoint;
    public float respawnDelay = 0.15f;
    public bool resetPuzzleOnDeath = true;

    [Header("Events")]
    public UnityEvent onDeath;
    public UnityEvent onRespawn;

    private PlayerMotor motor;
    private PlayerAbilities abilities;
    private Checkpoint currentCheckpoint;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool respawning;

    public Checkpoint CurrentCheckpoint => currentCheckpoint;

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        abilities = GetComponent<PlayerAbilities>();
        initialPosition = defaultSpawnPoint != null ? defaultSpawnPoint.position : transform.position;
        initialRotation = defaultSpawnPoint != null ? defaultSpawnPoint.rotation : transform.rotation;
    }

    public void SetCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null)
            return;

        currentCheckpoint = checkpoint;
    }

    public void Die()
    {
        if (respawning)
            return;

        StartCoroutine(RespawnRoutine(resetPuzzleOnDeath));
    }

    public void ResetToCheckpoint()
    {
        if (respawning)
            return;

        StartCoroutine(RespawnRoutine(true));
    }

    private IEnumerator RespawnRoutine(bool resetPuzzle)
    {
        respawning = true;
        onDeath.Invoke();

        PlayerInteraction interaction = GetComponent<PlayerInteraction>();
        if (interaction != null)
            interaction.ReleaseGrab();

        if (respawnDelay > 0f)
            yield return new WaitForSeconds(respawnDelay);

        if (resetPuzzle)
            GetCurrentPuzzleZone()?.ResetZone();

        abilities?.ResetForRespawn();

        Vector3 targetPosition = currentCheckpoint != null ? currentCheckpoint.GetRespawnPosition() : initialPosition;
        Quaternion targetRotation = currentCheckpoint != null ? currentCheckpoint.GetRespawnRotation() : initialRotation;

        if (motor != null)
            motor.Teleport(targetPosition, targetRotation);
        else
            transform.SetPositionAndRotation(targetPosition, targetRotation);

        onRespawn.Invoke();
        respawning = false;
    }

    private PuzzleZone GetCurrentPuzzleZone()
    {
        if (currentCheckpoint != null && currentCheckpoint.puzzleZone != null)
            return currentCheckpoint.puzzleZone;

        if (CheckpointManager.Instance != null)
            return CheckpointManager.Instance.CurrentPuzzleZone;

        return null;
    }
}
