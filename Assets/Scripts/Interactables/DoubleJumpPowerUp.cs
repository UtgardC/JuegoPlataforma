using UnityEngine;

public class DoubleJumpPowerUp : MonoBehaviour, IResettable
{
    public enum GrantMode
    {
        Temporary,
        UntilRespawn,
        Permanent
    }

    [Header("Power Up")]
    public GrantMode grantMode = GrantMode.Temporary;
    public float duration = 20f;
    public bool resetWhenPuzzleResets = true;

    private bool initiallyActive;

    private void Start()
    {
        CaptureInitialState();
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerAbilities abilities = other.GetComponentInParent<PlayerAbilities>();
        if (abilities == null)
            return;

        switch (grantMode)
        {
            case GrantMode.Temporary:
                abilities.GrantTemporaryDoubleJump(duration);
                break;
            case GrantMode.UntilRespawn:
                abilities.GrantDoubleJumpUntilRespawn();
                break;
            case GrantMode.Permanent:
                abilities.UnlockPermanentDoubleJump();
                break;
        }

        gameObject.SetActive(false);
    }

    public void CaptureInitialState()
    {
        initiallyActive = gameObject.activeSelf;
    }

    public void ResetState()
    {
        if (resetWhenPuzzleResets)
            gameObject.SetActive(initiallyActive);
    }
}
