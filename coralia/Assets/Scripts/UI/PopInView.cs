using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Pop de entrada reutilizable para CUALQUIER elemento de UI (no solo botones): escala
// 0 -> overshoot -> 1, con un delay opcional (para escalonar varios elementos, ej. cada
// booster de StartGamePanel apareciendo uno después del otro). Si el elemento vive adentro
// de un UIPanel, espera a que termine de abrirse (UIPanel.OnOpened) antes de animar — así no
// aparece de golpe mientras la card todavía está entrando. Si no hay panel (ej. algo suelto
// en el HUD, siempre activo), anima directo al habilitarse.
//
// Mientras dura el pop se apaga el raycastTarget del gráfico (Element) para que, si esto
// es clickeable, no pelee con ButtonPop (que anima este mismo transform.localScale al hacer
// click). Se usa Graphic genérico (Image, TMP_Text, lo que sea) en vez de Button.interactable
// a propósito — interactable=false hace que Unity tiña el botón con su color "disabled"
// (grisáceo/semi-transparente) mientras tanto, que no es lo que queremos, y además ataría
// este componente a que el elemento sea siempre un botón.
public class PopInView : MonoBehaviour
{
    [SerializeField] Graphic element; // opcional — Image/TMP_Text/etc. de este elemento; si no se asigna, se busca solo
    [SerializeField] float   delay       = 0f;   // espera extra antes de arrancar el pop
    [SerializeField] float   popDuration = 0.35f;
    [SerializeField] AnimationCurve popCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.7f, 1.2f), new Keyframe(1f, 1f));

    [Header("Sonido (opcional — dejar vacío hasta tener el clip)")]
    [SerializeField] AudioClip popSound; // suena justo cuando arranca el pop visual (después del delay), no al habilitarse

    UIPanel _panel;

    // GelatineAnimation_v2 lo consulta para pausarse mientras dura este pop — los dos animan
    // transform.localScale (mismo criterio que ButtonPop.IsPlaying), y sin esta señal se
    // pelearían por escribirlo cada frame durante la aparición.
    public bool IsPlaying { get; private set; }

    void Awake()
    {
        if (!element) element = GetComponent<Graphic>();
        _panel = GetComponentInParent<UIPanel>();
    }

    // El GameObject se activa junto con el panel (UIPanel.Open() hace
    // gameObject.SetActive(true) en la raíz), así que este OnEnable ya corre ANTES de que
    // termine la animación de apertura — llega a tiempo para suscribirse a OnOpened.
    // Se oculta ACÁ, ya mismo, no cuando arranca el pop — si no, queda visible a escala 1
    // durante toda la apertura del panel y recién desaparece de golpe justo antes de animar.
    void OnEnable()
    {
        IsPlaying = true; // desde ya, no solo durante el pop en sí — el estado "escala 0 esperando" también cuenta
        transform.localScale = Vector3.zero;
        SetClickBlocked(true);

        if (_panel != null) _panel.OnOpened += PlayPop;
        else                PlayPop();
    }

    void OnDisable()
    {
        IsPlaying = false; // por si se desactiva a mitad del pop — mismo motivo que ButtonPop.OnDisable
        if (_panel != null) _panel.OnOpened -= PlayPop;
    }

    void PlayPop() => StartCoroutine(PopRoutine());

    IEnumerator PopRoutine()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        AudioManager.Instance?.PlayUi(popSound);

        float time = 0f;
        while (time < popDuration)
        {
            time += Time.deltaTime;
            transform.localScale = Vector3.one * popCurve.Evaluate(Mathf.Clamp01(time / popDuration));
            yield return null;
        }
        transform.localScale = Vector3.one;
        SetClickBlocked(false);
        IsPlaying = false;
    }

    void SetClickBlocked(bool blocked)
    {
        if (element) element.raycastTarget = !blocked;
    }
}
