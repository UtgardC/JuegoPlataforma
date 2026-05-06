using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Flow")]
    public LevelSceneEntry[] levels;

    [Header("UI")]
    public MainMenuSplashController splash;
    public OptionsPanelController optionsPanel;
    public TMP_Text startContinueText;
    public string newGameText = "Empezar";
    public string continueTextFormat = "Continuar (Nivel {0})";

    private void Start()
    {
        RefreshStartContinueText();
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            ShowSplash();
    }

    public void EnterInteractiveMenu()
    {
        if (splash != null)
            splash.Hide();
    }

    public void ShowSplash()
    {
        if (optionsPanel != null && optionsPanel.IsOpen)
            optionsPanel.Close();

        if (splash != null)
            splash.Show();
    }

    public void StartOrContinueGame()
    {
        int levelToLoad = GameProgress.ContinueLevel;
        string sceneName = GetSceneName(levelToLoad);

        if (!string.IsNullOrWhiteSpace(sceneName))
            SceneManager.LoadScene(sceneName);
    }

    public void OpenOptions()
    {
        if (optionsPanel != null)
            optionsPanel.Open();
    }

    public void CloseOptions()
    {
        if (optionsPanel != null)
            optionsPanel.Close();
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void RefreshStartContinueText()
    {
        if (startContinueText == null)
            return;

        if (GameProgress.CompletedLevel <= 0)
            startContinueText.text = newGameText;
        else
            startContinueText.text = string.Format(continueTextFormat, GameProgress.ContinueLevel);
    }

    private string GetSceneName(int levelNumber)
    {
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i].levelNumber == levelNumber)
                return levels[i].sceneName;
        }

        return string.Empty;
    }
}
