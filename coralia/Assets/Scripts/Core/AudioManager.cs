using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// Gestor central de audio. Singleton que sobrevive entre escenas.
// Controla música, SFX, UI y pop con canales independientes.
public class AudioManager : MonoBehaviour
{
    // Instancia global accesible desde cualquier script
    public static AudioManager Instance { get; private set; }

#if UNITY_IOS && !UNITY_EDITOR
    // Llama al plugin nativo iOS para ignorar el switch de silencio del iPhone
    [DllImport("__Internal")]
    static extern void SetAudioSessionPlayback();
#endif

    [SerializeField] AudioSource musicSource; // Canal de música de fondo
    [SerializeField] AudioSource sfxSource;   // Canal de efectos de juego
    [SerializeField] AudioSource uiSource;    // Canal de clicks de botones
    [SerializeField] AudioSource popSource;   // Canal de sonido de burbujas
    [SerializeField] AudioClip   lobbyMusic;     // Música del menú principal (Home + Level Map)
    [SerializeField] AudioClip[] gameplayMusic;  // Música in-game — distinta y más calma que la del lobby, varias pistas elegidas al azar por nivel (pedido de Diego)
    [SerializeField, Range(0f, 1f)] float gameplayMusicVolumeScale = 0.5f; // multiplicador propio sobre MusicVolume — los clips de gameplay vienen masterizados más fuerte que el de lobby y sonaban "muy alto" (reportado por Diego), sin esto solo se podía bajar con el slider general (que también afecta el lobby)
    [SerializeField] AudioClip   panelOpenClip;  // pop default al abrir CUALQUIER UIPanel — un solo lugar para asignarlo en vez de repetirlo panel por panel (UIPanel.openSound lo puede pisar puntualmente si algún panel necesita otro sonido o ninguno)

    float _currentMusicVolumeScale = 1f; // escala del clip que está sonando ahora — se reaplica si el jugador mueve el slider de música a mitad de una canción

    [Header("iOS — switch de silencio físico")]
    [Tooltip("Apagado (default): el switch de silencio del dispositivo manda, como cualquier app — si el jugador lo pone en silencio, el juego no fuerza sonido. Prendido: ignora el switch (sonido siempre forzado), útil solo si en algún momento se necesita ese comportamiento de nuevo.")]
#pragma warning disable 0414 // solo se lee dentro de #if UNITY_IOS && !UNITY_EDITOR — en Editor/otras plataformas el compilador lo marca como "sin usar", pero sí se usa al compilar para iOS real
    [SerializeField] bool ignoreSilentSwitch = false;
#pragma warning restore 0414

    void Awake()
    {
        // Destruye duplicados — solo puede existir un AudioManager
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Persiste al cambiar de escena

#if UNITY_IOS && !UNITY_EDITOR
        if (ignoreSilentSwitch) SetAudioSessionPlayback(); // fuerza sonido, ignora el switch — apagado por default
#endif
        ApplyAllVolumes(); // Aplica volúmenes guardados al arrancar
    }

    // Aplica los volúmenes y estados guardados en SaveManager a cada canal
    void ApplyAllVolumes()
    {
        if (musicSource) { musicSource.volume = SaveManager.MusicVolume * _currentMusicVolumeScale; musicSource.mute = !SaveManager.MusicEnabled; }
        if (sfxSource)   { sfxSource.volume   = SaveManager.SfxVolume;   sfxSource.mute   = !SaveManager.SoundEnabled; }
        if (uiSource)    { uiSource.volume     = SaveManager.UiVolume;    uiSource.mute    = !SaveManager.SoundEnabled; }
        if (popSource)   { popSource.volume    = SaveManager.PopVolume;   popSource.mute   = !SaveManager.SoundEnabled; }
    }

    // Reproduce la música del lobby (Home + LevelMap)
    public void PlayLobbyMusic() => PlayMusic(lobbyMusic);

    // Reproduce la música de gameplay — pistas aparte, más calmas (GDD: no deben competir con
    // la concentración de apuntar/disparar, a diferencia de la música más animada del lobby).
    // Una elegida al azar por llamada (ej. cada vez que se carga/reinicia un nivel) — si sale
    // la misma que ya está sonando, PlayMusic no la reinicia, sigue de largo sin cortes.
    public void PlayGameplayMusic()
    {
        if (gameplayMusic == null || gameplayMusic.Length == 0) return;

        // Descarta slots vacíos del array — si se arma a mano en el Inspector y algún
        // elemento queda sin arrastrar el clip, no queremos que Random.Range() caiga justo
        // ahí y el nivel arranque en silencio sin ningún aviso.
        List<AudioClip> valid = null;
        for (int i = 0; i < gameplayMusic.Length; i++)
        {
            if (!gameplayMusic[i]) continue;
            valid ??= new List<AudioClip>();
            valid.Add(gameplayMusic[i]);
        }

        if (valid == null)
        {
            Debug.LogWarning("[AudioManager] 'Gameplay Music' no tiene ningún clip asignado (todos los slots están vacíos).");
            return;
        }
        PlayMusic(valid[Random.Range(0, valid.Count)], gameplayMusicVolumeScale);
    }

    // Reproduce un clip de música en loop. No reinicia si ya está sonando el mismo clip.
    // volumeScale: multiplicador propio de este clip sobre SaveManager.MusicVolume (ej. la
    // música de gameplay suena más fuerte que la de lobby en la fuente original — PlayLobbyMusic
    // usa el default 1, sin cambios).
    public void PlayMusic(AudioClip clip, float volumeScale = 1f)
    {
        if (!musicSource || clip == null) return;
        _currentMusicVolumeScale = volumeScale;

        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            musicSource.volume = SaveManager.MusicVolume * volumeScale; // por si cambió el volumeScale sin cambiar de clip
            return;
        }
        musicSource.clip   = clip;
        musicSource.loop   = true;
        musicSource.volume = SaveManager.MusicVolume * volumeScale;
        musicSource.mute   = !SaveManager.MusicEnabled;
        musicSource.Play();
    }

    public void StopMusic() => musicSource?.Stop();

    // Toggle de música desde Settings — guarda preferencia y mutea/desmutea
    public void SetMusicEnabled(bool enabled)
    {
        SaveManager.MusicEnabled = enabled;
        if (musicSource) musicSource.mute = !enabled;
    }

    // Toggle de sonidos desde Settings — afecta sfx, ui y pop simultáneamente
    public void SetSoundEnabled(bool enabled)
    {
        SaveManager.SoundEnabled = enabled;
        if (sfxSource) sfxSource.mute = !enabled;
        if (uiSource)  uiSource.mute  = !enabled;
        if (popSource) popSource.mute  = !enabled;
    }

    // Sliders de volumen desde Settings — guardan valor y lo aplican en tiempo real
    public void SetMusicVolume(float v) { SaveManager.MusicVolume = v; if (musicSource) musicSource.volume = v * _currentMusicVolumeScale; }
    public void SetSfxVolume(float v)   { SaveManager.SfxVolume   = v; if (sfxSource)   sfxSource.volume   = v; }
    public void SetUiVolume(float v)    { SaveManager.UiVolume    = v; if (uiSource)    uiSource.volume    = v; }
    public void SetPopVolume(float v)   { SaveManager.PopVolume   = v; if (popSource)   popSource.volume   = v; }

    // Reproducen un clip puntual en su canal (PlayOneShot permite superposición). volumeScale
    // (0-1) permite bajar un clip puntual sin tocar el volumen general del canal (ej. un
    // sonido de victoria que suena "duro" a full si comparte canal con simples clicks de UI).
    public void PlaySfx(AudioClip clip, float volumeScale = 1f) => PlayOn(sfxSource, clip, volumeScale);
    public void PlayUi(AudioClip clip, float volumeScale = 1f)  => PlayOn(uiSource,  clip, volumeScale);
    public void PlayPop(AudioClip clip, float volumeScale = 1f) => PlayOn(popSource, clip, volumeScale);

    // Llamado por UIPanel.Open() en cada panel de la app. overrideClip: el 'Open Sound' propio
    // del panel (UIPanel.openSound), si tiene uno asignado — si no, cae al default de acá.
    public void PlayPanelOpen(AudioClip overrideClip = null) => PlayOn(uiSource, overrideClip ? overrideClip : panelOpenClip);

    static void PlayOn(AudioSource src, AudioClip clip, float volumeScale = 1f)
    {
        if (!src || clip == null) return;
        src.PlayOneShot(clip, volumeScale);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        var prefab = Resources.Load<GameObject>("Audio/AudioManager");
        Debug.Log($"[AudioManager] Bootstrap — prefab: {prefab}");
        if (prefab) Instantiate(prefab);
    }
}
