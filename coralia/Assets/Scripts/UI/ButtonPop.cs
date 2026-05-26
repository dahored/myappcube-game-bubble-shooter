using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Solo.MOST_IN_ONE;

[RequireComponent(typeof(Button))]
public class ButtonPop : MonoBehaviour
{
    [SerializeField] float     scalePeak    = 1.2f;
    [SerializeField] float     durationUp   = 0.08f;
    [SerializeField] float     durationDown = 0.2f;
    [SerializeField] AudioClip clickSound;
    [SerializeField] bool      haptic       = true;

    Animator _animator;
    Vector3  _originalScale;

    static readonly int ClickHash = Animator.StringToHash("Click");

    void Awake()
    {
        _animator      = GetComponent<Animator>();
        _originalScale = transform.localScale;
        GetComponent<Button>().onClick.AddListener(Pop);
    }

    void Pop()
    {
        if (clickSound && SaveManager.SoundEnabled) PlayUI(clickSound);
        if (haptic && SaveManager.Vibration)
            MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.LightImpact);
        StartCoroutine(DoPop());
    }

    static void PlayUI(AudioClip clip)
    {
        var go  = new GameObject("UISound");
        DontDestroyOnLoad(go);
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.Play();
        Destroy(go, clip.length + 0.1f);
    }

    IEnumerator DoPop()
    {
        if (_animator != null) _animator.enabled = false;

        Vector3 peak = _originalScale * scalePeak;

        float t = 0f;
        while (t < durationUp)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(_originalScale, peak, t / durationUp);
            yield return null;
        }

        t = 0f;
        while (t < durationDown)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(peak, _originalScale, t / durationDown);
            yield return null;
        }

        transform.localScale = _originalScale;
        if (_animator != null) _animator.enabled = true;
    }
}
