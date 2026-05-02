using UnityEngine;
using UnityEngine.UI;

public class InteractionPrompt : MonoBehaviour
{
    [Header("References")]
    public PlayerInteraction playerInteraction;
    public CanvasGroup canvasGroup;
    public Text promptText;

    [Header("Text")]
    public string grabText = "E";
    public string releaseText = "Soltar";

    private void Awake()
    {
        if (playerInteraction == null)
            playerInteraction = FindAnyObjectByType<PlayerInteraction>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Update()
    {
        bool visible = playerInteraction != null && (playerInteraction.HasAvailableTarget || playerInteraction.IsGrabbing);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (promptText != null && playerInteraction != null)
            promptText.text = playerInteraction.IsGrabbing ? releaseText : grabText;
    }
}
