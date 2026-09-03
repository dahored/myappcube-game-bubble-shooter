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

    UIPanel _panel;

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
        transform.localScale = Vector3.zero;
        SetClickBlocked(true);

        if (_panel != null) _panel.OnOpened += PlayPop;
        else                PlayPop();
    }

    void OnDisable()
    {
        if (_panel != null) _panel.OnOpened -= PlayPop;
    }

    void PlayPop() => StartCoroutine(PopRoutine());

    IEnumerator PopRoutine()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float time = 0f;
        while (time < popDuration)
        {
            time += Time.deltaTime;
            transform.localScale = Vector3.one * popCurve.Evaluate(Mathf.Clamp01(time / popDuration));
            yield return null;
        }
        transform.localScale = Vector3.one;
        SetClickBlocked(false);
    }

    void SetClickBlocked(bool blocked)
    {
        if (element) element.raycastTarget = !blocked;
    }
}
