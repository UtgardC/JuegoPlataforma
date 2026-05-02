using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    public bool pauseOnVictory = true;

    [Header("Events")]
    public UnityEvent onVictory;
    public UnityEvent onRestartScene;

    public bool HasWon { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void RegisterVictory()
    {
        if (HasWon)
            return;

        HasWon = true;
        onVictory.Invoke();

        if (pauseOnVictory)
            Time.timeScale = 0f;
    }

    public void RestartScene()
    {
        Time.timeScale = 1f;
        HasWon = false;
        onRestartScene.Invoke();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
