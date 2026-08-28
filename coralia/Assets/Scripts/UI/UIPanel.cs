using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public abstract class UIPanel : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] RectTransform card;

    [Header("Durations")]
    [SerializeField] float openDuration  = 0.25f;
    [SerializeField] float closeDuration = 0.18f;

    [Header("Card scale curves")]
    [SerializeField] AnimationCurve openCurve  = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2f),
        new Keyframe(0.7f, 1.1f),
        new Keyframe(1f, 1f));
    [SerializeField] AnimationCurve closeCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.4f, 1.1f),
        new Keyframe(1f, 0f, -2f, 0f));

    // Expuesto para que subclases (ej. WinPanel) puedan esperar a que la card termine de
    // entrar antes de arrancar animaciones propias (estrellas, contador de score, etc.).
    public float OpenDuration => openDuration;

    // Se dispara cuando el panel termina de cerrarse, sin importar quién llamó Close() ni
    // cómo (botón asignado en el Inspector o código). Sirve para el patrón "volver al panel
    // anterior" — ej. PausedPanel se esconde al abrir SettingsPanel y se suscribe acá para
    // reabrirse solo cuando Settings se cierra — sin que SettingsPanel necesite saber nada
    // sobre quién lo abrió (HomeGame/LevelMap lo abren igual, sin este comportamiento).
    public event System.Action OnClosed;

    CanvasGroup _overlay;
    Vector3     _baseScale;
    Coroutine   _anim;
    bool        _initialized;

    protected virtual void Awake() => Init();

    public virtual void Open()
    {
        gameObject.SetActive(true);
        Init();
        Swap(ref _anim, AnimOpen());
    }

    public virtual void Close() => Swap(ref _anim, AnimClose());

    void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _overlay   = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _baseScale = card != null ? card.localScale : Vector3.one;
    }

    void Swap(ref Coroutine slot, IEnumerator routine)
    {
        if (slot != null) StopCoroutine(slot);
        slot = StartCoroutine(routine);
    }

    IEnumerator AnimOpen()
    {
        _overlay.alpha = 0f;
        SetCardScale(0f);

        float t = 0f;
        while (t < openDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / openDuration);
            _overlay.alpha = p;
            SetCardScale(openCurve.Evaluate(p));
            yield return null;
        }

        _overlay.alpha = 1f;
        SetCardScale(1f);
    }

    IEnumerator AnimClose()
    {
        float t = 0f;
        while (t < closeDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / closeDuration);
            _overlay.alpha = 1f - p;
            SetCardScale(closeCurve.Evaluate(p));
            yield return null;
        }

        gameObject.SetActive(false);
        _overlay.alpha = 1f;
        SetCardScale(1f);
        OnClosed?.Invoke();
    }

    void SetCardScale(float s)
    {
        if (card != null) card.localScale = _baseScale * s;
    }
}
