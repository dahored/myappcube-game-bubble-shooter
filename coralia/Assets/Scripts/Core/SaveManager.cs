using UnityEngine;

// Persistencia de preferencias del jugador usando PlayerPrefs.
// Clase estática — no necesita instancia, se usa directamente: SaveManager.MusicVolume
// PlayerPrefs guarda en disco (iOS: NSUserDefaults, Android: SharedPreferences)
public static class SaveManager
{
    // Claves internas usadas para leer/escribir en PlayerPrefs
    const string KEY_LANGUAGE      = "language";
    const string KEY_MAX_LEVEL     = "max_level";
    const string KEY_GEMS          = "gems";
    const string KEY_LIVES         = "lives";
    const string KEY_SOUND_MUSIC   = "vol_music";
    const string KEY_SOUND_SFX     = "vol_sfx";
    const string KEY_SOUND_UI      = "vol_ui";
    const string KEY_SOUND_POP     = "vol_pop";
    const string KEY_VIBRATION     = "vibration";
    const string KEY_SOUND_ENABLED = "sound_enabled";
    const string KEY_MUSIC_ENABLED = "music_enabled";

    // Idioma activo. Si no hay guardado, detecta el idioma del sistema automáticamente.
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

    // Mapea el idioma del sistema operativo a los 6 idiomas soportados (es, en, it, fr, de, pt)
    static string DetectLanguage()
    {
        return Application.systemLanguage switch
        {
            SystemLanguage.Spanish    => "es",
            SystemLanguage.Italian    => "it",
            SystemLanguage.French     => "fr",
            SystemLanguage.German     => "de",
            SystemLanguage.Portuguese => "pt",
            _                         => "en",
        };
    }

    // Último nivel desbloqueado. Valor por defecto: 1 (primer nivel siempre abierto)
    public static int MaxUnlockedLevel
    {
        get => PlayerPrefs.GetInt(KEY_MAX_LEVEL, 1);
        set { PlayerPrefs.SetInt(KEY_MAX_LEVEL, value); PlayerPrefs.Save(); }
    }

    // Moneda premium (GDD §6.3). Sin fuentes reales de gemas todavía — arranca en 0.
    public static int Gems
    {
        get => PlayerPrefs.GetInt(KEY_GEMS, 0);
        set { PlayerPrefs.SetInt(KEY_GEMS, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    // Vidas (GDD §6.2). Por defecto: 5 llenas. Regen por tiempo todavía no implementado.
    public static int Lives
    {
        get => PlayerPrefs.GetInt(KEY_LIVES, 5);
        set { PlayerPrefs.SetInt(KEY_LIVES, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    // Volúmenes de cada canal de audio (0.0 a 1.0). Por defecto: 1 (máximo)
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

    // Vibración activada/desactivada. PlayerPrefs no soporta bool — se guarda como int (1/0)
    public static bool Vibration
    {
        get => PlayerPrefs.GetInt(KEY_VIBRATION, 1) == 1;
        set { PlayerPrefs.SetInt(KEY_VIBRATION, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // Toggle global de sonidos (SFX, UI, pop). Por defecto: activado
    public static bool SoundEnabled
    {
        get => PlayerPrefs.GetInt(KEY_SOUND_ENABLED, 1) == 1;
        set { PlayerPrefs.SetInt(KEY_SOUND_ENABLED, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // Toggle de música. Por defecto: activado
    public static bool MusicEnabled
    {
        get => PlayerPrefs.GetInt(KEY_MUSIC_ENABLED, 1) == 1;
        set { PlayerPrefs.SetInt(KEY_MUSIC_ENABLED, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
