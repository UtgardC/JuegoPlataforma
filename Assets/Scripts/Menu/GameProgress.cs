using UnityEngine;

public static class GameProgress
{
    public const string CompletedLevelKey = "CompletedLevel";
    public const string MasterVolumeKey = "MasterVol";
    public const string MusicVolumeKey = "MusicVol";
    public const string SfxVolumeKey = "SFXVol";

    public static int CompletedLevel => PlayerPrefs.GetInt(CompletedLevelKey, 0);
    public static int ContinueLevel => Mathf.Max(1, CompletedLevel + 1);

    public static bool IsLevelUnlocked(int levelNumber)
    {
        return levelNumber <= ContinueLevel;
    }

    public static void MarkLevelCompleted(int levelNumber)
    {
        if (levelNumber > CompletedLevel)
            PlayerPrefs.SetInt(CompletedLevelKey, levelNumber);

        PlayerPrefs.Save();
    }

    public static float GetMasterVolume()
    {
        return PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
    }

    public static float GetMusicVolume()
    {
        return PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
    }

    public static float GetSfxVolume()
    {
        return PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
    }

    public static void SaveVolumes(float masterVolume, float musicVolume, float sfxVolume)
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(masterVolume));
        PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(musicVolume));
        PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(sfxVolume));
        PlayerPrefs.Save();
    }
}
