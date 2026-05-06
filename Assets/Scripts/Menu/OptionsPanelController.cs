using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class OptionsPanelController : MonoBehaviour
{
    [Header("Panel")]
    public RectTransform panel;
    public CanvasGroup canvasGroup;
    public Vector2 closedPosition;
    public Vector2 openPosition;
    public float animationDuration = 0.45f;
    public Ease ease = Ease.OutCubic;

    [Header("Audio")]
    public AudioMixer audioMixer;
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    private Sequence currentSequence;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (panel == null)
            panel = GetComponent<RectTransform>();

        LoadSavedValues();
        CloseInstant();
    }

    public void Open()
    {
        KillSequence();
        gameObject.SetActive(true);
        IsOpen = true;
        SetInput(false);

        currentSequence = DOTween.Sequence().SetUpdate(true);
        currentSequence.Join(TweenPanelPosition(openPosition, animationDuration));
        currentSequence.Join(TweenAlpha(1f, animationDuration));
        currentSequence.SetEase(ease);
        currentSequence.OnComplete(() => SetInput(true));
    }

    public void Close()
    {
        KillSequence();
        SetInput(false);

        currentSequence = DOTween.Sequence().SetUpdate(true);
        currentSequence.Join(TweenPanelPosition(closedPosition, animationDuration));
        currentSequence.Join(TweenAlpha(0f, animationDuration));
        currentSequence.SetEase(ease);
        currentSequence.OnComplete(() =>
        {
            IsOpen = false;
            gameObject.SetActive(false);
        });
    }

    public void CloseInstant()
    {
        KillSequence();

        if (panel != null)
            panel.anchoredPosition = closedPosition;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        IsOpen = false;
        SetInput(false);
        gameObject.SetActive(false);
    }

    public void LoadSavedValues()
    {
        SetSliderValue(masterSlider, GameProgress.GetMasterVolume());
        SetSliderValue(musicSlider, GameProgress.GetMusicVolume());
        SetSliderValue(sfxSlider, GameProgress.GetSfxVolume());
        ApplyMixerValues();
    }

    public void ApplyChanges()
    {
        GameProgress.SaveVolumes(GetSliderValue(masterSlider), GetSliderValue(musicSlider), GetSliderValue(sfxSlider));
        ApplyMixerValues();
    }

    public void ApplyMixerValues()
    {
        if (audioMixer == null)
            return;

        audioMixer.SetFloat(GameProgress.MasterVolumeKey, NormalizedToDecibels(GetSliderValue(masterSlider)));
        audioMixer.SetFloat(GameProgress.MusicVolumeKey, NormalizedToDecibels(GetSliderValue(musicSlider)));
        audioMixer.SetFloat(GameProgress.SfxVolumeKey, NormalizedToDecibels(GetSliderValue(sfxSlider)));
    }

    private Tween TweenPanelPosition(Vector2 targetPosition, float duration)
    {
        if (panel == null)
            return DOTween.Sequence();

        return DOTween.To(() => panel.anchoredPosition, value => panel.anchoredPosition = value, targetPosition, duration);
    }

    private Tween TweenAlpha(float targetAlpha, float duration)
    {
        if (canvasGroup == null)
            return DOTween.Sequence();

        return DOTween.To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, targetAlpha, duration);
    }

    private void SetInput(bool enabled)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
    }

    private void KillSequence()
    {
        if (currentSequence != null && currentSequence.IsActive())
            currentSequence.Kill();
    }

    private static void SetSliderValue(Slider slider, float value)
    {
        if (slider != null)
            slider.value = Mathf.Clamp01(value);
    }

    private static float GetSliderValue(Slider slider)
    {
        return slider != null ? Mathf.Clamp01(slider.value) : 1f;
    }

    private static float NormalizedToDecibels(float value)
    {
        return Mathf.Log10(Mathf.Max(0.0001f, value)) * 20f;
    }
}
