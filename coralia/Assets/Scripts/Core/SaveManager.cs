using UnityEngine;

public static class SaveManager
{
    const string KEY_LANGUAGE      = "language";
    const string KEY_MAX_LEVEL     = "max_level";
    const string KEY_SOUND_MUSIC   = "vol_music";
    const string KEY_SOUND_SFX     = "vol_sfx";
    const string KEY_VIBRATION     = "vibration";

    public static string Language
    {
        get => PlayerPrefs.GetString(KEY_LANGUAGE, "en");
        set { PlayerPrefs.SetString(KEY_LANGUAGE, value); PlayerPrefs.Save(); }
    }

    public static int MaxUnlockedLevel
    {
        get => PlayerPrefs.GetInt(KEY_MAX_LEVEL, 1);
        set { PlayerPrefs.SetInt(KEY_MAX_LEVEL, value); PlayerPrefs.Save(); }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(KEY_SOUND_MUSIC, 1f);
        set { PlayerPrefs.SetFloat(KEY_SOUND_MUSIC, value); PlayerPrefs.Save(); }
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(KEY_SOUND_SFX, 1f);
        set { PlayerPrefs.SetFloat(KEY_SOUND_SFX, value); PlayerPrefs.Save(); }
    }

    public static bool Vibration
    {
        get => PlayerPrefs.GetInt(KEY_VIBRATION, 1) == 1;
        set { PlayerPrefs.SetInt(KEY_VIBRATION, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
