using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference pauseAction;

    [Header("Panel")]
    public RectTransform panel;
    public CanvasGroup canvasGroup;
    public Vector2 closedPosition;
    public Vector2 openPosition;
    public float animationDuration = 0.25f;
    public Ease ease = Ease.OutCubic;

    [Header("Scenes")]
    public string mainMenuSceneName = "MainMenu";

    private Sequence currentSequence;

    public bool IsPaused { get; private set; }

    private void Awake()
    {
        if (panel == null)
            panel = GetComponent<RectTransform>();

        HideInstant();
    }

    private void OnEnable()
    {
        if (pauseAction == null)
            return;

        pauseAction.action.Enable();
        pauseAction.action.performed += OnPausePerformed;
    }

    private void OnDisable()
    {
        if (pauseAction == null)
            return;

        pauseAction.action.performed -= OnPausePerformed;
        pauseAction.action.Disable();
    }

    private void Update()
    {
        if (pauseAction == null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    public void TogglePause()
    {
        if (IsPaused)
            Resume();
        else
            Pause();
    }

    public void Pause()
    {
        KillSequence();
        IsPaused = true;
        Time.timeScale = 0f;
        SetInput(false);

        currentSequence = DOTween.Sequence().SetUpdate(true);
        currentSequence.Join(TweenPanelPosition(openPosition, animationDuration));
        currentSequence.Join(TweenAlpha(1f, animationDuration));
        currentSequence.SetEase(ease);
        currentSequence.OnComplete(() => SetInput(true));
    }

    public void Resume()
    {
        KillSequence();
        SetInput(false);

        currentSequence = DOTween.Sequence().SetUpdate(true);
        currentSequence.Join(TweenPanelPosition(closedPosition, animationDuration));
        currentSequence.Join(TweenAlpha(0f, animationDuration));
        currentSequence.SetEase(ease);
        currentSequence.OnComplete(() =>
        {
            IsPaused = false;
            Time.timeScale = 1f;
        });
    }

    public void RestartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private void HideInstant()
    {
        if (panel != null)
            panel.anchoredPosition = closedPosition;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        IsPaused = false;
        SetInput(false);
    }

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        TogglePause();
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
}
