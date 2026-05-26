using UnityEngine;

public static class SaveManager
{
    const string KEY_LANGUAGE      = "language";
    const string KEY_MAX_LEVEL     = "max_level";
    const string KEY_SOUND_MUSIC   = "vol_music";
    const string KEY_SOUND_SFX     = "vol_sfx";
    const string KEY_SOUND_UI      = "vol_ui";
    const string KEY_SOUND_POP     = "vol_pop";
    const string KEY_VIBRATION     = "vibration";
    const string KEY_SOUND_ENABLED = "sound_enabled";
    const string KEY_MUSIC_ENABLED = "music_enabled";

    public static string Language
    {
        get
        {
            if (!PlayerPrefs.HasKey(KEY_LANGUAGE))
                Language = DetectLanguage();
            return PlayerPrefs.GetString(KEY_LANGUAGE, "en");
        }
        set { PlayerPrefs.SetString(KEY_LANGUAGE, value); PlayerPrefs.Save(); }
    }

    static string DetectLanguage()
    {
        return Application.systemLanguage switch
        {
            SystemLanguage.Spanish   => "es",
            SystemLanguage.Italian   => "it",
            SystemLanguage.French    => "fr",
            SystemLanguage.German    => "de",
            SystemLanguage.Portuguese => "pt",
            _                        => "en",
        };
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

    public static float UiVolume
    {
        get => PlayerPrefs.GetFloat(KEY_SOUND_UI, 1f);
        set { PlayerPrefs.SetFloat(KEY_SOUND_UI, value); PlayerPrefs.Save(); }
    }

    public static float PopVolume
    {
        get => PlayerPrefs.GetFloat(KEY_SOUND_POP, 1f);
        set { PlayerPrefs.SetFloat(KEY_SOUND_POP, value); PlayerPrefs.Save(); }
    }

    public static bool Vibration
    {
        get => PlayerPrefs.GetInt(KEY_VIBRATION, 1) == 1;
        set { PlayerPrefs.SetInt(KEY_VIBRATION, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool SoundEnabled
    {
        get => PlayerPrefs.GetInt(KEY_SOUND_ENABLED, 1) == 1;
        set { PlayerPrefs.SetInt(KEY_SOUND_ENABLED, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool MusicEnabled
    {
        get => PlayerPrefs.GetInt(KEY_MUSIC_ENABLED, 1) == 1;
        set { PlayerPrefs.SetInt(KEY_MUSIC_ENABLED, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
