using UnityEngine;

public class TapaNivel : MonoBehaviour
{
    public int levelNumber = 2;

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        gameObject.SetActive(!GameProgress.IsLevelUnlocked(levelNumber));
    }
}
