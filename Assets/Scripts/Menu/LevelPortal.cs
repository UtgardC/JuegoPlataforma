using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelPortal : MonoBehaviour
{
    public int levelNumber = 1;
    public string sceneName;
    public bool requireUnlocked = true;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null)
            return;

        if (requireUnlocked && !GameProgress.IsLevelUnlocked(levelNumber))
            return;

        SceneManager.LoadScene(sceneName);
    }
}
