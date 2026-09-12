using System.Collections;
using UnityEngine;

// Botón flotante que aparece cuando el jugador se alejó del nivel actual, y al tocarlo lo trae de
// vuelta. Mismo comportamiento que ScrollPinController del mapa real, pero para el mapa 3D:
// allá la referencia es la posición de un ScrollRect, acá es el ángulo de giro de la esfera.
//
// Top:    aparece arriba cuando el nivel actual quedó MÁS ADELANTE (todavía no bajó del horizonte).
// Bottom: aparece abajo  cuando el nivel actual quedó ATRÁS (ya pasó de largo hacia la cámara).
public class WorldSphereScrollPin : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] WorldSphereDragRotate     dragRotate;
    [SerializeField] WorldSphereNodePositioner positioner;
    [SerializeField] PinDirection              direction;
    [Tooltip("Asignar solo en el pin de arriba — lo baja para que no quede tapado por el panel superior.")]
    [SerializeField] TopPanelController        topPanel;
    [SerializeField] float                     pinPaddingFromPanel = 40f;

    [Header("Umbral (en cantidad de nodos)")]
    [SerializeField] int nodesThreshold = 2;

    [Header("Salto")]
    [SerializeField] int   jumpCount    = 2;
    [SerializeField] float jumpHeight   = 24f;
    [SerializeField] float jumpDuration = 0.50f;
    [SerializeField] float jumpPause    = 1.80f;

    [Header("Animación")]
    [SerializeField] float fadeDuration = 0.20f;

    CanvasGroup   _cg;
    RectTransform _rt;
    Vector2       _basePos;
    bool          _isVisible;
    Coroutine     _fade;
    Coroutine     _jumpAfterFade;
    Coroutine     _jumpLoop;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _cg = GetComponent<CanvasGroup>();
        if (!_cg) _cg = gameObject.AddComponent<CanvasGroup>();

        _basePos = _rt.anchoredPosition;
        _cg.alpha = 0f;
        _cg.interactable = _cg.blocksRaycasts = false;
    }

    IEnumerator Start()
    {
        if (!dragRotate || !positioner)
        {
            Debug.LogWarning($"[WorldSphereScrollPin] '{name}': faltan 'Drag Rotate' o 'Positioner' en el Inspector — el pin no va a aparecer nunca.", this);
            enabled = false;
            yield break;
        }

        if (!topPanel) yield break;

        // Hay que esperar un frame: TopPanelController calcula el padding del notch y reconstruye
        // su layout en su propio Start(), y Unity no garantiza cuál de los dos corre primero. Si
        // se lee antes, la altura todavía no está y el pin termina pegado al borde de arriba.
        yield return null;

        var pos = _rt.anchoredPosition;
        pos.y = -(topPanel.PanelHeight + pinPaddingFromPanel);
        _rt.anchoredPosition = pos;
        _basePos = pos;
    }

    // Se evalúa por frame en vez de por evento: el ángulo cambia tanto por el drag como por la
    // inercia y por los desplazamientos automáticos, y no hay un único callback que cubra los tres.
    void Update()
    {
        float away      = positioner.CurrentNodeAngle - dragRotate.CurrentAngle;
        float threshold = nodesThreshold * positioner.AngleSpacing;

        bool shouldShow = direction == PinDirection.Top
            ? away >  threshold   // el nivel actual está más adelante → pin arriba
            : away < -threshold;  // el nivel actual quedó atrás       → pin abajo

        if (shouldShow == _isVisible) return;
        _isVisible = shouldShow;

        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(Fade(_isVisible ? 1f : 0f));
        _cg.interactable = _cg.blocksRaycasts = _isVisible;

        StopJump();
        if (_isVisible) _jumpAfterFade = StartCoroutine(JumpAfterFade());
        else            _rt.anchoredPosition = _basePos;
    }

    // Se engancha al Button del pin desde el Inspector.
    public void OnTap()
    {
        if (dragRotate && positioner) dragRotate.AnimateTo(positioner.CurrentNodeAngle);
    }

    void StopJump()
    {
        if (_jumpAfterFade != null) { StopCoroutine(_jumpAfterFade); _jumpAfterFade = null; }
        if (_jumpLoop      != null) { StopCoroutine(_jumpLoop);      _jumpLoop      = null; }
    }

    IEnumerator JumpAfterFade()
    {
        yield return new WaitForSeconds(fadeDuration);
        _jumpLoop = StartCoroutine(JumpLoop());
    }

    IEnumerator JumpLoop()
    {
        // Cada pin salta hacia donde está el nodo: el de arriba hacia abajo, el de abajo hacia arriba.
        float dir = direction == PinDirection.Top ? -1f : 1f;
        while (true)
        {
            for (int j = 0; j < jumpCount; j++)
            {
                for (float t = 0f; t < 1f; t += Time.deltaTime / jumpDuration)
                {
                    _rt.anchoredPosition = _basePos + new Vector2(0f, Mathf.Sin(t * Mathf.PI) * jumpHeight * dir);
                    yield return null;
                }
                _rt.anchoredPosition = _basePos;
            }
            yield return new WaitForSeconds(jumpPause);
        }
    }

    IEnumerator Fade(float to)
    {
        float from = _cg.alpha;
        for (float t = 0f; t < 1f; t += Time.deltaTime / fadeDuration)
        {
            _cg.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        _cg.alpha = to;
    }
}
