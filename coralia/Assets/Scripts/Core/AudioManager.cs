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
    [SerializeField] AudioClip lobbyMusic;    // Música del menú principal

    void Awake()
    {
        // Destruye duplicados — solo puede existir un AudioManager
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Persiste al cambiar de escena

#if UNITY_IOS && !UNITY_EDITOR
        SetAudioSessionPlayback(); // iOS: ignorar switch de silencio
#endif
        ApplyAllVolumes(); // Aplica volúmenes guardados al arrancar
    }

    // Aplica los volúmenes y estados guardados en SaveManager a cada canal
    void ApplyAllVolumes()
    {
        if (musicSource) { musicSource.volume = SaveManager.MusicVolume; musicSource.mute = !SaveManager.MusicEnabled; }
        if (sfxSource)   { sfxSource.volume   = SaveManager.SfxVolume;   sfxSource.mute   = !SaveManager.SoundEnabled; }
        if (uiSource)    { uiSource.volume     = SaveManager.UiVolume;    uiSource.mute    = !SaveManager.SoundEnabled; }
        if (popSource)   { popSource.volume    = SaveManager.PopVolume;   popSource.mute   = !SaveManager.SoundEnabled; }
    }

    // Reproduce la música del lobby (Home + LevelMap)
    public void PlayLobbyMusic() => PlayMusic(lobbyMusic);

    // Reproduce un clip de música en loop. No reinicia si ya está sonando el mismo clip.
    public void PlayMusic(AudioClip clip)
    {
        if (!musicSource || clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;
        musicSource.clip   = clip;
        musicSource.loop   = true;
        musicSource.volume = SaveManager.MusicVolume;
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
    public void SetMusicVolume(float v) { SaveManager.MusicVolume = v; if (musicSource) musicSource.volume = v; }
    public void SetSfxVolume(float v)   { SaveManager.SfxVolume   = v; if (sfxSource)   sfxSource.volume   = v; }
    public void SetUiVolume(float v)    { SaveManager.UiVolume    = v; if (uiSource)    uiSource.volume    = v; }
    public void SetPopVolume(float v)   { SaveManager.PopVolume   = v; if (popSource)   popSource.volume   = v; }

    // Reproducen un clip puntual en su canal (PlayOneShot permite superposición)
    public void PlaySfx(AudioClip clip) => PlayOn(sfxSource, clip);
    public void PlayUi(AudioClip clip)  => PlayOn(uiSource,  clip);
    public void PlayPop(AudioClip clip) => PlayOn(popSource, clip);

    static void PlayOn(AudioSource src, AudioClip clip)
    {
        if (!src || clip == null) return;
        src.PlayOneShot(clip);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        var prefab = Resources.Load<GameObject>("Audio/AudioManager");
        Debug.Log($"[AudioManager] Bootstrap — prefab: {prefab}");
        if (prefab) Instantiate(prefab);
    }
}
