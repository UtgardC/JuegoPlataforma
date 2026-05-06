using DG.Tweening;
using UnityEngine;

public class MainMenuSplashController : MonoBehaviour
{
    [Header("Splash Objects")]
    public CanvasGroup splashRoot;
    public CanvasGroup backgroundGroup;
    public CanvasGroup buttonsGroup;
    public RectTransform logo;

    [Header("Animation")]
    public Vector2 logoVisiblePosition;
    public Vector2 logoHiddenPosition = new Vector2(0f, 700f);
    public float fadeDuration = 0.65f;
    public float logoMoveDuration = 0.75f;
    public Ease ease = Ease.OutCubic;
    public bool showOnStart = true;

    private Sequence currentSequence;

    public bool IsVisible { get; private set; }

    private void Start()
    {
        if (showOnStart)
            ShowInstant();
        else
            HideInstant();
    }

    public void Show()
    {
        KillSequence();
        gameObject.SetActive(true);
        SetInteractable(false);

        currentSequence = DOTween.Sequence().SetUpdate(true);
        currentSequence.Join(TweenCanvasGroup(splashRoot, 1f, fadeDuration));
        currentSequence.Join(TweenCanvasGroup(backgroundGroup, 1f, fadeDuration));
        currentSequence.Join(TweenCanvasGroup(buttonsGroup, 1f, fadeDuration));

        if (logo != null)
            currentSequence.Join(TweenAnchoredPosition(logo, logoVisiblePosition, logoMoveDuration));

        currentSequence.SetEase(ease);
        currentSequence.OnComplete(() =>
        {
            IsVisible = true;
            SetInteractable(true);
        });
    }

    public void Hide()
    {
        KillSequence();
        SetInteractable(false);

        currentSequence = DOTween.Sequence().SetUpdate(true);
        currentSequence.Join(TweenCanvasGroup(backgroundGroup, 0f, fadeDuration));
        currentSequence.Join(TweenCanvasGroup(buttonsGroup, 0f, fadeDuration));

        if (logo != null)
            currentSequence.Join(TweenAnchoredPosition(logo, logoHiddenPosition, logoMoveDuration));

        currentSequence.SetEase(ease);
        currentSequence.OnComplete(() =>
        {
            SetAlpha(splashRoot, 0f);
            IsVisible = false;
            gameObject.SetActive(false);
        });
    }

    public void ShowInstant()
    {
        KillSequence();
        gameObject.SetActive(true);
        SetAlpha(splashRoot, 1f);
        SetAlpha(backgroundGroup, 1f);
        SetAlpha(buttonsGroup, 1f);

        if (logo != null)
            logo.anchoredPosition = logoVisiblePosition;

        IsVisible = true;
        SetInteractable(true);
    }

    public void HideInstant()
    {
        KillSequence();
        SetAlpha(splashRoot, 0f);
        SetAlpha(backgroundGroup, 0f);
        SetAlpha(buttonsGroup, 0f);

        if (logo != null)
            logo.anchoredPosition = logoHiddenPosition;

        IsVisible = false;
        SetInteractable(false);
        gameObject.SetActive(false);
    }

    private void SetInteractable(bool interactable)
    {
        SetCanvasGroupInput(splashRoot, interactable);
        SetCanvasGroupInput(buttonsGroup, interactable);
    }

    private void KillSequence()
    {
        if (currentSequence != null && currentSequence.IsActive())
            currentSequence.Kill();
    }

    private static Tween TweenCanvasGroup(CanvasGroup group, float endValue, float duration)
    {
        if (group == null)
            return DOTween.Sequence();

        return DOTween.To(() => group.alpha, value => group.alpha = value, endValue, duration);
    }

    private static Tween TweenAnchoredPosition(RectTransform rectTransform, Vector2 endValue, float duration)
    {
        return DOTween.To(() => rectTransform.anchoredPosition, value => rectTransform.anchoredPosition = value, endValue, duration);
    }

    private static void SetAlpha(CanvasGroup group, float alpha)
    {
        if (group != null)
            group.alpha = alpha;
    }

    private static void SetCanvasGroupInput(CanvasGroup group, bool interactable)
    {
        if (group == null)
            return;

        group.interactable = interactable;
        group.blocksRaycasts = interactable;
    }
}
