using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Estados posibles de un nodo en el mapa de niveles
public enum NodeState { Locked, Available, Completed, CompleteFirstTry }

// Controla la apariencia visual de un nodo en el mapa de niveles.
// Se llama desde LevelMapController pasando el id, estado y estrellas ganadas.
public class LevelNodeView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Image background;      // círculo de fondo del nodo
    [SerializeField] TMP_Text numberLabel;  // número del nivel
    [SerializeField] GameObject starRow;    // fila de estrellas (se oculta si no hay progreso)
    [SerializeField] Image[] stars;         // Star1, Star2, Star3 — tamaño fijo 3
    [SerializeField] GameObject lockIcon;   // candado (solo visible si bloqueado)
    [SerializeField] Button   button;       // clic/tap del nodo — dispara OnClicked si no está bloqueado

    public event System.Action<int> OnClicked;

    int       _id;
    NodeState _state;

    [Header("Sprites — nodo")]
    [SerializeField] Sprite spriteDefault;  // azul — bloqueado o completado con reintentos
    [SerializeField] Sprite spriteCurrent;  // morado — nivel disponible para jugar
    [SerializeField] Sprite spriteGold;     // dorado — completado en el primer intento

    [Header("Pulse — nodo actual")]
    [SerializeField] float pulseMin      = 1.00f;
    [SerializeField] float pulseMax      = 1.10f;
    [SerializeField] float pulseDuration = 0.25f;
    [SerializeField] float pulsePause    = 2.00f;
    [SerializeField] float pulseTotals   = 2.00f;

    [Header("Ripple — anillo expansivo")]
    [SerializeField] Image  ringImage      = null;  // Image hijo detrás del círculo
    [SerializeField] float  rippleScale    = 1.70f; // escala máxima del anillo
    [SerializeField] float  rippleAlpha    = 0.55f; // alpha inicial del anillo
    [SerializeField] float  rippleDuration = 0.45f; // duración de cada ripple

    [Header("Reveal al volver de Gameplay (issue #55)")]
    [SerializeField] float nodePopDuration   = 0.35f; // pop del círculo entero, antes de las estrellas
    [SerializeField] float popDuration       = 0.3f;  // pop de cada estrella
    [SerializeField] float delayBetweenStars = 0.15f;
    [SerializeField] float spinRotations     = 2f;    // vueltas completas de cada estrella al aparecer
    [SerializeField] AnimationCurve popCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.6f, 1.5f), new Keyframe(1f, 1f));

    Coroutine  _pulseRoutine;
    Vector3[]  _starBaseScales;

    void Awake()
    {
        _starBaseScales = new Vector3[stars.Length];
        for (int i = 0; i < stars.Length; i++)
            _starBaseScales[i] = stars[i].transform.localScale;
    }

    // Configura el nodo con los datos del nivel
    public void Setup(int id, NodeState state, int starsEarned)
    {
        _id    = id;
        _state = state;
        numberLabel.text = id.ToString();

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (_state != NodeState.Locked) OnClicked?.Invoke(_id);
            });
        }

        // Sprite del círculo según el estado del nivel
        switch (state)
        {
            case NodeState.Locked:           background.sprite = spriteDefault; break;
            case NodeState.Available:        background.sprite = spriteCurrent; break;
            case NodeState.Completed:        background.sprite = spriteDefault; break;
            case NodeState.CompleteFirstTry: background.sprite = spriteGold;    break;
        }

        // StarRow visible si el nivel está pasado (los StarFieldX de fondo, siempre grises,
        // quedan visibles ahí adentro por defecto) — Star1/2/3 (dorados) son los que se
        // prenden según las estrellas ganadas.
        bool passed = state == NodeState.Completed || state == NodeState.CompleteFirstTry;
        starRow.SetActive(passed);
        for (int i = 0; i < stars.Length; i++)
            stars[i].gameObject.SetActive(i < starsEarned);

        // Candado solo visible si el nivel está bloqueado
        lockIcon.SetActive(state == NodeState.Locked);

        // Pulso + ripple solo en el nodo disponible. Si Setup() se llama de nuevo sobre la
        // misma instancia (ver PlayCompletionTransition), hay que parar el pulso anterior
        // antes de arrancar otro — dos PulseLoop corriendo a la vez pelean por el mismo
        // transform.localScale y queda tembloroso.
        if (_pulseRoutine != null) { StopCoroutine(_pulseRoutine); _pulseRoutine = null; }
        transform.localScale = Vector3.one;

        if (state == NodeState.Available)
        {
            if (ringImage) ringImage.gameObject.SetActive(true);
            _pulseRoutine = StartCoroutine(PulseLoop());
        }
        else if (ringImage)
        {
            ringImage.gameObject.SetActive(false);
        }
    }

    IEnumerator PulseLoop()
    {
        while (true)
        {
            // 2 pulsos seguidos
            for (int p = 0; p < pulseTotals; p++)
            {
                if (ringImage) StartCoroutine(RippleOnce());
                for (float t = 0f; t < 1f; t += Time.deltaTime / pulseDuration)
                {
                    transform.localScale = Vector3.one * Mathf.Lerp(pulseMin, pulseMax, t);
                    yield return null;
                }
                for (float t = 0f; t < 1f; t += Time.deltaTime / pulseDuration)
                {
                    transform.localScale = Vector3.one * Mathf.Lerp(pulseMax, pulseMin, t);
                    yield return null;
                }
            }
            transform.localScale = Vector3.one * pulseMin;
            // Pausa
            yield return new WaitForSeconds(pulsePause);
        }
    }

    IEnumerator RippleOnce()
    {
        var c = ringImage.color;
        for (float t = 0f; t < 1f; t += Time.deltaTime / rippleDuration)
        {
            ringImage.transform.localScale = Vector3.one * Mathf.Lerp(1f, rippleScale, t);
            c.a = Mathf.Lerp(rippleAlpha, 0f, t);
            ringImage.color = c;
            yield return null;
        }
        // Reset para el próximo ripple
        ringImage.transform.localScale = Vector3.one;
        c.a = 0f;
        ringImage.color = c;
    }

    // Deja el nodo invisible (escala 0) esperando el pop de PlayCompletionTransition — llamado
    // por LevelMapController justo después del Setup() real (que ya dejó sprite/estrellas
    // finales listos, solo ocultos) cuando hay una animación de retorno pendiente (issue #55).
    public void HideForReveal()
    {
        if (_pulseRoutine != null) { StopCoroutine(_pulseRoutine); _pulseRoutine = null; }
        if (ringImage) ringImage.gameObject.SetActive(false);
        transform.localScale = Vector3.zero;
    }

    // Retorno de Gameplay tras ganar (issue #55): este nodo quedó invisible por HideForReveal().
    // Secuencia: 1) se fija el sprite final (gold/default) y el círculo entero aparece con
    // un pop 0→1.1→1, 2) recién ahí las estrellas ganadas se revelan en cadena con su propio
    // pop + rotación (mismo criterio que LevelStarsView.PlayStars en WinPanel).
    public IEnumerator PlayCompletionTransition(int starsEarned, bool gold)
    {
        if (_pulseRoutine != null) { StopCoroutine(_pulseRoutine); _pulseRoutine = null; }
        if (ringImage) ringImage.gameObject.SetActive(false);

        background.sprite = gold ? spriteGold : spriteDefault;
        _state = gold ? NodeState.CompleteFirstTry : NodeState.Completed;

        starRow.SetActive(true); // los StarFieldX de fondo quedan visibles detrás por defecto
        for (int i = 0; i < stars.Length; i++) stars[i].gameObject.SetActive(false);

        // 1. Pop del nodo entero (sin rotar)
        transform.localScale = Vector3.zero;
        float time = 0f;
        while (time < nodePopDuration)
        {
            time += Time.deltaTime;
            transform.localScale = Vector3.one * popCurve.Evaluate(Mathf.Clamp01(time / nodePopDuration));
            yield return null;
        }
        transform.localScale = Vector3.one;

        // 2. Estrellas ganadas, en cadena, con pop + rotación
        for (int i = 0; i < starsEarned && i < stars.Length; i++)
        {
            yield return PopStar(i);
            if (i < starsEarned - 1) yield return new WaitForSeconds(delayBetweenStars);
        }
    }

    IEnumerator PopStar(int index)
    {
        var t = stars[index].transform;
        stars[index].gameObject.SetActive(true);

        float time = 0f;
        while (time < popDuration)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / popDuration);
            t.localScale = _starBaseScales[index] * popCurve.Evaluate(p);

            // Ease-out cúbico: gira rápido al principio y frena hasta quedar derecha justo
            // cuando termina el pop — mismo criterio que LevelStarsView (WinPanel).
            float spinP = 1f - Mathf.Pow(1f - p, 3f);
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(spinRotations * 360f, 0f, spinP));
            yield return null;
        }
        t.localScale    = _starBaseScales[index];
        t.localRotation = Quaternion.identity;
    }
}