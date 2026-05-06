using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelCompletionTrigger : MonoBehaviour
{
    public int levelNumber = 1;
    public bool completeOnPlayerTrigger = true;
    public string sceneToLoadAfterComplete;

    private void OnTriggerEnter(Collider other)
    {
        if (!completeOnPlayerTrigger || other.GetComponentInParent<PlayerController>() == null)
            return;

        CompleteLevel();
    }

    public void CompleteLevel()
    {
        GameProgress.MarkLevelCompleted(levelNumber);

        if (!string.IsNullOrWhiteSpace(sceneToLoadAfterComplete))
            SceneManager.LoadScene(sceneToLoadAfterComplete);
    }
}
