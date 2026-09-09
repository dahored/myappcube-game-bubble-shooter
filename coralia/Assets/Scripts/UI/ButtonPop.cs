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

    // GelatineAnimation_v2 lo consulta para pausarse mientras dura este pop — los dos animan
    // transform.localScale, y sin esta señal se pelearían por escribirlo el mismo frame.
    public bool IsPlaying { get; private set; }

    void Awake()
    {
        _animator      = GetComponent<Animator>();
        _originalScale = transform.localScale;
        GetComponent<Button>().onClick.AddListener(Pop);
    }

    void Pop()
    {
        if (clickSound && SaveManager.SoundEnabled) AudioManager.Instance?.PlayUi(clickSound);
        if (haptic && SaveManager.Vibration)
            MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.LightImpact);
        StartCoroutine(DoPop());
    }

    IEnumerator DoPop()
    {
        IsPlaying = true;
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
        IsPlaying = false;
    }

    // Si el GameObject se desactiva a mitad del pop (ej. un botón que además cierra su panel
    // al tocarlo, y la animación de cierre termina antes que esta) Unity mata la corutina de
    // golpe SIN llegar a la línea de arriba que resetea IsPlaying — sin esto quedaba trabado
    // en true para siempre, y GelatineAnimation_v2 (que lo consulta) se quedaba pausado por el
    // resto de la vida del objeto. Restaura también la escala por si quedó a mitad de camino.
    void OnDisable()
    {
        IsPlaying = false;
        transform.localScale = _originalScale;
    }
}
