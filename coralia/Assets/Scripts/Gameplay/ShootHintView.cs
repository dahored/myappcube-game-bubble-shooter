using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Mano fantasma que enseña a disparar: en la punta del dedo (TouchPoint) alterna entre el
// sprite normal y el sprite "presionado" en loop, simulando el gesto de mantener apretado.
// CannonController mueve esta raíz (SetPosition) en simultáneo con el balanceo de la línea
// de trayectoria real (TrajectoryLine, ya existe, en modo semitransparente) — así la mano
// se desplaza junto con hacia dónde apunta la línea, como si alguien la estuviera
// arrastrando despacio. CannonController decide CUÁNDO y HACIA DÓNDE, este componente solo
// anima el swap de sprites y el fade.
public class ShootHintView : MonoBehaviour
{
    [SerializeField] Image       touchPoint;         // en la punta del dedo de HandIcon
    [SerializeField] Sprite      pointSprite;         // hand_point — estado normal
    [SerializeField] Sprite      pointPressedSprite;  // hand_point_pressed — estado presionado
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] TMP_Text    messageText;         // opcional — "Mantén presionado..." / "Suelta para disparar". NO cuelga de este GameObject (que se mueve con SetPosition) — queda fijo en su propio lugar, este script solo lo prende/apaga y le cambia el texto.

    RectTransform _root; // este mismo GameObject — se mueve entero al seguir el balanceo de la línea

    [Header("Gesto de presión (loop) — timer independiente del balanceo de CannonController")]
    [SerializeField] float pauseBetween    = 3f;   // cuánto se ve "sin presionar" (da tiempo a leer el texto)
    [SerializeField] float pressedDuration = 3f; // cuánto se ve "presionado"
    [SerializeField] float fadeDuration    = 0.25f;

    Coroutine _loopRoutine;

    void Awake()
    {
        _root = transform as RectTransform;
        gameObject.SetActive(false);
    }

    // CannonController la llama cada frame mientras balancea la línea de trayectoria, para
    // que la mano se desplace junto con hacia dónde apunta (mismo espacio local que
    // GridContainer, ya que este objeto cuelga de ahí).
    public void SetPosition(Vector2 anchoredPosition)
    {
        if (_root) _root.anchoredPosition = anchoredPosition;
    }

    public void Show()
    {
        if (_loopRoutine != null) return; // ya está mostrándose, no reiniciar a mitad de gesto
        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // por encima de TrajectoryDots y todo lo demás del grid, sin depender del orden en el Editor
        if (canvasGroup)  canvasGroup.alpha = 0f;
        if (messageText)  messageText.gameObject.SetActive(true); // fijo en su propio lugar, no cuelga de este transform que se mueve
        _loopRoutine = StartCoroutine(Loop());
    }

    public void Hide()
    {
        if (messageText) messageText.gameObject.SetActive(false);
        if (_loopRoutine != null) { StopCoroutine(_loopRoutine); _loopRoutine = null; }
        gameObject.SetActive(false);
    }

    IEnumerator Loop()
    {
        yield return Fade(0f, 1f, fadeDuration);
        while (true)
        {
            SetPressed(false);
            yield return new WaitForSeconds(pauseBetween);
            SetPressed(true);
            yield return new WaitForSeconds(pressedDuration);
        }
    }

    void SetPressed(bool pressed)
    {
        if (touchPoint)  touchPoint.sprite = pressed ? pointPressedSprite : pointSprite;
        if (messageText) messageText.text  = LocaleManager.Get(pressed
            ? "ui.gameplay.hint.release_to_shoot"
            : "ui.gameplay.hint.hold_to_aim");
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        if (!canvasGroup) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}
