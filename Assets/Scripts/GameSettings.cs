using UnityEngine;

// The player's saved preferences: volumes and fullscreen.
public static class GameSettings
{
    public static float Music
    {
        get => PlayerPrefs.GetFloat("music", 0.7f);
        set { PlayerPrefs.SetFloat("music", Mathf.Clamp01(value)); Apply(); }
    }

    public static float Sound
    {
        get => PlayerPrefs.GetFloat("sound", 0.8f);
        set { PlayerPrefs.SetFloat("sound", Mathf.Clamp01(value)); Apply(); }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt("fullscreen", 1) == 1;
        set
        {
            PlayerPrefs.SetInt("fullscreen", value ? 1 : 0);
            if (value)
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            else
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            PlayerPrefs.Save();
        }
    }

    // Relaxed gives longer timers and slower hazards. In an online game the
    // host's choice applies to everyone.
    public static bool Relaxed
    {
        get => PlayerPrefs.GetInt("relaxed", 1) == 1;
        set { PlayerPrefs.SetInt("relaxed", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static void Apply()
    {
        Sfx.SetVolumes(Music, Sound);
        PlayerPrefs.Save();
    }
}
