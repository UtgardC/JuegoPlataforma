using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    [Header("State")]
    public Checkpoint startingCheckpoint;

    public Checkpoint CurrentCheckpoint { get; private set; }
    public PuzzleZone CurrentPuzzleZone => CurrentCheckpoint != null ? CurrentCheckpoint.puzzleZone : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentCheckpoint = startingCheckpoint;
    }

    public void SetCurrentCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null)
            return;

        CurrentCheckpoint = checkpoint;
    }

    public void ResetCurrentPuzzleZone()
    {
        CurrentPuzzleZone?.ResetZone();
    }
}
