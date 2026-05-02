using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [Header("References")]
    public PlayerInventory playerInventory;
    public PlayerAbilities playerAbilities;

    [Header("Text")]
    public Text gearText;
    public Text secondaryText;
    public Text doubleJumpText;

    [Header("Power Up")]
    public Slider doubleJumpSlider;

    private void Awake()
    {
        if (playerInventory == null)
            playerInventory = FindAnyObjectByType<PlayerInventory>();

        if (playerAbilities == null)
            playerAbilities = FindAnyObjectByType<PlayerAbilities>();
    }

    private void OnEnable()
    {
        UpdateHUD();
    }

    private void Update()
    {
        UpdateHUD();
    }

    public void UpdateHUD()
    {
        if (playerInventory != null)
        {
            if (gearText != null)
                gearText.text = $"Engranajes: {playerInventory.GearCount}";

            if (secondaryText != null)
                secondaryText.text = $"Coleccionables: {playerInventory.SecondaryCount}";
        }

        if (playerAbilities == null)
            return;

        bool hasTimedDoubleJump = playerAbilities.doubleJumpMode == PlayerAbilities.DoubleJumpMode.Temporary;

        if (doubleJumpText != null)
        {
            if (hasTimedDoubleJump)
                doubleJumpText.text = $"Doble salto: {Mathf.CeilToInt(playerAbilities.RemainingDoubleJumpTime)}s";
            else if (playerAbilities.CanDoubleJump)
                doubleJumpText.text = "Doble salto";
            else
                doubleJumpText.text = string.Empty;
        }

        if (doubleJumpSlider != null)
        {
            doubleJumpSlider.gameObject.SetActive(hasTimedDoubleJump);

            if (hasTimedDoubleJump)
            {
                float maxDuration = Mathf.Max(0.1f, playerAbilities.defaultTemporaryDuration);
                doubleJumpSlider.value = Mathf.Clamp01(playerAbilities.RemainingDoubleJumpTime / maxDuration);
            }
        }
    }
}
