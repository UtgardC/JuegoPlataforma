using UnityEngine;
using UnityEngine.Events;

public class PlayerAbilities : MonoBehaviour
{
    public enum DoubleJumpMode
    {
        Disabled,
        Temporary,
        UntilRespawn,
        Permanent
    }

    [Header("Double Jump")]
    public DoubleJumpMode doubleJumpMode = DoubleJumpMode.Disabled;
    public float defaultTemporaryDuration = 20f;

    [Header("Events")]
    public UnityEvent onDoubleJumpEnabled;
    public UnityEvent onDoubleJumpDisabled;

    private float remainingDoubleJumpTime;
    private int availableAirJumps;

    public bool CanDoubleJump => doubleJumpMode != DoubleJumpMode.Disabled;
    public float RemainingDoubleJumpTime => remainingDoubleJumpTime;
    public int AvailableAirJumps => availableAirJumps;

    private void Update()
    {
        if (doubleJumpMode != DoubleJumpMode.Temporary)
            return;

        remainingDoubleJumpTime -= Time.deltaTime;

        if (remainingDoubleJumpTime <= 0f)
            DisableDoubleJump();
    }

    public void ResetAirJumps()
    {
        availableAirJumps = CanDoubleJump ? 1 : 0;
    }

    public bool ConsumeDoubleJump()
    {
        if (!CanDoubleJump || availableAirJumps <= 0)
            return false;

        availableAirJumps--;
        return true;
    }

    public void GrantTemporaryDoubleJump()
    {
        GrantTemporaryDoubleJump(defaultTemporaryDuration);
    }

    public void GrantTemporaryDoubleJump(float duration)
    {
        doubleJumpMode = DoubleJumpMode.Temporary;
        remainingDoubleJumpTime = Mathf.Max(0.1f, duration);
        ResetAirJumps();
        onDoubleJumpEnabled.Invoke();
    }

    public void GrantDoubleJumpUntilRespawn()
    {
        doubleJumpMode = DoubleJumpMode.UntilRespawn;
        remainingDoubleJumpTime = 0f;
        ResetAirJumps();
        onDoubleJumpEnabled.Invoke();
    }

    public void UnlockPermanentDoubleJump()
    {
        doubleJumpMode = DoubleJumpMode.Permanent;
        remainingDoubleJumpTime = 0f;
        ResetAirJumps();
        onDoubleJumpEnabled.Invoke();
    }

    public void DisableDoubleJump()
    {
        if (doubleJumpMode == DoubleJumpMode.Disabled)
            return;

        doubleJumpMode = DoubleJumpMode.Disabled;
        remainingDoubleJumpTime = 0f;
        availableAirJumps = 0;
        onDoubleJumpDisabled.Invoke();
    }

    public void ResetForRespawn()
    {
        if (doubleJumpMode == DoubleJumpMode.Temporary || doubleJumpMode == DoubleJumpMode.UntilRespawn)
            DisableDoubleJump();
        else
            ResetAirJumps();
    }
}
