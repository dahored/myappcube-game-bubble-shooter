using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource sfxSource;
    [SerializeField] AudioSource uiSource;
    [SerializeField] AudioSource popSource;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ApplyAllVolumes();
    }

    void ApplyAllVolumes()
    {
        if (musicSource) { musicSource.volume = SaveManager.MusicVolume; musicSource.mute = !SaveManager.MusicEnabled; }
        if (sfxSource)   { sfxSource.volume   = SaveManager.SfxVolume;   sfxSource.mute   = !SaveManager.SoundEnabled; }
        if (uiSource)    { uiSource.volume     = SaveManager.UiVolume;    uiSource.mute    = !SaveManager.SoundEnabled; }
        if (popSource)   { popSource.volume    = SaveManager.PopVolume;   popSource.mute   = !SaveManager.SoundEnabled; }
    }

    public void PlayMusic(AudioClip clip)
    {
        if (!musicSource || clip == null) return;
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void StopMusic() => musicSource?.Stop();

    public void PlaySfx(AudioClip clip) => PlayOn(sfxSource, clip);
    public void PlayUi(AudioClip clip)  => PlayOn(uiSource,  clip);
    public void PlayPop(AudioClip clip) => PlayOn(popSource, clip);

    public void SetMusicEnabled(bool enabled)
    {
        SaveManager.MusicEnabled = enabled;
        if (musicSource) musicSource.mute = !enabled;
    }

    public void SetSoundEnabled(bool enabled)
    {
        SaveManager.SoundEnabled = enabled;
        if (sfxSource) sfxSource.mute = !enabled;
        if (uiSource)  uiSource.mute  = !enabled;
        if (popSource) popSource.mute  = !enabled;
    }

    public void SetMusicVolume(float v) { SaveManager.MusicVolume = v; if (musicSource) musicSource.volume = v; }
    public void SetSfxVolume(float v)   { SaveManager.SfxVolume   = v; if (sfxSource)   sfxSource.volume   = v; }
    public void SetUiVolume(float v)    { SaveManager.UiVolume    = v; if (uiSource)     uiSource.volume    = v; }
    public void SetPopVolume(float v)   { SaveManager.PopVolume   = v; if (popSource)   popSource.volume   = v; }

    static void PlayOn(AudioSource src, AudioClip clip)
    {
        if (!src || clip == null) return;
        src.PlayOneShot(clip);
    }
}
