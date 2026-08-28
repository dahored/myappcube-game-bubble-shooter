using System;
using UnityEngine;

// Persistencia de preferencias del jugador usando PlayerPrefs.
// Clase estática — no necesita instancia, se usa directamente: SaveManager.MusicVolume
// PlayerPrefs guarda en disco (iOS: NSUserDefaults, Android: SharedPreferences)
public static class SaveManager
{
    // Claves internas usadas para leer/escribir en PlayerPrefs
    const string KEY_LANGUAGE         = "language";
    const string KEY_MAX_LEVEL        = "max_level";
    const string KEY_COINS            = "coins";
    const string KEY_LIVES            = "lives";
    const string KEY_NEXT_LIFE_AT     = "next_life_at";     // unix seconds (string) — vacío = sin timer corriendo
    const string KEY_INFINITE_LIVES_UNTIL = "infinite_lives_until"; // unix seconds (string) — vacío = sin boost activo
    const string KEY_SOUND_MUSIC      = "vol_music";
    const string KEY_SOUND_SFX        = "vol_sfx";
    const string KEY_SOUND_UI         = "vol_ui";
    const string KEY_SOUND_POP        = "vol_pop";
    const string KEY_VIBRATION        = "vibration";
    const string KEY_SOUND_ENABLED    = "sound_enabled";
    const string KEY_MUSIC_ENABLED    = "music_enabled";

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

    // Única moneda del juego (GDD §6.3 mencionaba una moneda premium "gemas" separada,
    // pero el proyecto nunca llegó a tener arte/UI para eso — Diego confirmó que todo usa
    // el ícono/balance de monedas, así que se unificó acá). Arranca en 0.
    public static int Coins
    {
        get => PlayerPrefs.GetInt(KEY_COINS, 0);
        set { PlayerPrefs.SetInt(KEY_COINS, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    // Vidas (GDD §6.2). Máximo 5, regen 30 min POR VIDA individual (confirmado con Diego —
    // de 0 a 5 llenas tardaría 2.5h). El regen se calcula por diferencia de tiempo real
    // (DateTimeOffset.UtcNow vs. un timestamp guardado), no por un timer corriendo en
    // memoria — así sigue contando aunque se cierre la app (issue #52).
    public const int MAX_LIVES          = 5;
    const     int LIFE_REGEN_MINUTES = 30;

    public static int Lives
    {
        get { ApplyLivesRegen(); return PlayerPrefs.GetInt(KEY_LIVES, MAX_LIVES); }
        set { PlayerPrefs.SetInt(KEY_LIVES, Mathf.Clamp(value, 0, MAX_LIVES)); PlayerPrefs.Save(); }
    }

    // Bandera de sesión (NO persiste, se resetea sola en cada arranque de la app) que se
    // arma cuando una vida cae exactamente a 0, y consume quien la muestre (LevelMapController
    // al cargar, o GameplayController si ya la mostró él mismo primero — ver ambos Start()).
    // Existe para que el aviso de "sin vidas" sea reactivo al momento en que se gastó la
    // última vida, no un chequeo pasivo de "Lives<=0" en cada carga del mapa — así navegar
    // Home → LevelMap con 0 vidas de antes no dispara el panel solo, pero perder la última
    // vida por Quit/Restart/derrota sí avisa apenas se aterriza donde corresponda.
    public static bool NotifyOutOfLivesOnMapLoad;

    // Resta 1 vida por gameplay (abandonar/reiniciar/declinar continuar). Los call sites que
    // restan vidas deben usar esto en vez de "Lives--" directo, para que el cronómetro de
    // regen arranque en el momento correcto (solo al pasar de llena a no-llena — si ya
    // estaba regenerando, no se reinicia el timer en curso).
    public static void LoseLife()
    {
        int current = Lives; // ya aplica regen pendiente antes de restar
        if (current <= 0) return;
        bool wasFull = current >= MAX_LIVES;
        Lives = current - 1;
        if (wasFull) SetNextLifeAt(DateTimeOffset.UtcNow.AddMinutes(LIFE_REGEN_MINUTES));
        if (Lives <= 0) NotifyOutOfLivesOnMapLoad = true;
    }

    // Tiempo restante para la próxima vida. TimeSpan.Zero si está llena o no hay timer.
    public static TimeSpan TimeUntilNextLife()
    {
        ApplyLivesRegen();
        if (Lives >= MAX_LIVES) return TimeSpan.Zero;
        long nextAt = GetNextLifeAt();
        if (nextAt == 0) return TimeSpan.Zero; // no debería pasar con Lives<MAX, defensivo
        long remaining = nextAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return remaining > 0 ? TimeSpan.FromSeconds(remaining) : TimeSpan.Zero;
    }

    // Boost de vidas infinitas por tiempo limitado (GDD §7/§9 — compra todavía sin definir,
    // ver issue #53). Esto solo guarda el vencimiento; otorgar el boost es responsabilidad
    // de quien implemente la compra.
    public static void GrantInfiniteLives(TimeSpan duration)
    {
        PlayerPrefs.SetString(KEY_INFINITE_LIVES_UNTIL, DateTimeOffset.UtcNow.Add(duration).ToUnixTimeSeconds().ToString());
        PlayerPrefs.Save();
    }

    public static bool IsInfiniteLivesActive => TimeUntilInfiniteLivesEnds() > TimeSpan.Zero;

    public static TimeSpan TimeUntilInfiniteLivesEnds()
    {
        long until = 0;
        long.TryParse(PlayerPrefs.GetString(KEY_INFINITE_LIVES_UNTIL, ""), out until);
        if (until == 0) return TimeSpan.Zero;
        long remaining = until - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return remaining > 0 ? TimeSpan.FromSeconds(remaining) : TimeSpan.Zero;
    }

    static long GetNextLifeAt()
    {
        long.TryParse(PlayerPrefs.GetString(KEY_NEXT_LIFE_AT, ""), out var value);
        return value;
    }

    static void SetNextLifeAt(DateTimeOffset at)
    {
        PlayerPrefs.SetString(KEY_NEXT_LIFE_AT, at.ToUnixTimeSeconds().ToString());
        PlayerPrefs.Save();
    }

    static void ClearNextLifeAt()
    {
        PlayerPrefs.DeleteKey(KEY_NEXT_LIFE_AT);
        PlayerPrefs.Save();
    }

    // Aplica toda la regeneración pendiente de una sola vez, contra el reloj real — cubre
    // el caso de cerrar la app varias horas y volver con varias vidas ya recuperadas.
    static void ApplyLivesRegen()
    {
        int current = PlayerPrefs.GetInt(KEY_LIVES, MAX_LIVES);
        if (current >= MAX_LIVES) { ClearNextLifeAt(); return; }

        long nextAt = GetNextLifeAt();
        if (nextAt == 0)
        {
            // Self-healing: vidas por debajo del máximo pero sin timer corriendo (ej. save
            // viejo de antes de este sistema, o cualquier otro camino que haya bajado Lives
            // sin pasar por LoseLife). Sin esto el contador se queda en 0 para siempre.
            SetNextLifeAt(DateTimeOffset.UtcNow.AddMinutes(LIFE_REGEN_MINUTES));
            return;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now < nextAt) return;

        long intervalSeconds  = LIFE_REGEN_MINUTES * 60;
        long elapsedSinceDue  = now - nextAt;
        int  livesToAdd       = 1 + (int)(elapsedSinceDue / intervalSeconds);
        int  newLives         = Mathf.Min(MAX_LIVES, current + livesToAdd);
        int  actuallyAdded    = newLives - current;

        PlayerPrefs.SetInt(KEY_LIVES, newLives);
        if (newLives >= MAX_LIVES) ClearNextLifeAt();
        else SetNextLifeAt(DateTimeOffset.FromUnixTimeSeconds(nextAt + actuallyAdded * intervalSeconds));
        PlayerPrefs.Save();
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
