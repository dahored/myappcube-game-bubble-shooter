using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// El botón flotante que aparece cuando el nodo actual quedó fuera de la pantalla y te lleva de
// vuelta a él. Mismo comportamiento que ScrollPinController, pero para el mapa de mundo.
//
// Va aparte y no adaptado porque aquel mide todo en un ScrollRect: viewport, content, posición
// normalizada. Acá no hay ScrollRect — hay un mundo que se mueve en Z. Meter las dos formas de
// medir en un mismo script dejaría la mitad de los campos sin usar según la escena.
//
//   Top    -> el nodo actual está más adelante; el pin sale arriba.
//   Bottom -> el nodo actual quedó atrás; el pin sale abajo.
public class WorldMapScrollPin : MonoBehaviour
{
    [SerializeField] PinDirection direction;

    [Tooltip("Vacío: se busca solo en la escena.")]
    [SerializeField] WorldMapScroll     scroll;
    [SerializeField] WorldMapDefinition definition;

    [Tooltip("Solo en el pin de arriba: lo baja para que no quede tapado por el panel superior.")]
    [SerializeField] TopPanelController topPanel;
    [SerializeField] float              pinPaddingFromPanel = 40f;

    [Tooltip("Cuántos niveles se puede alejar el nodo actual antes de que aparezca el pin.")]
    [SerializeField] float nodesThreshold = 2f;

    [Tooltip("A partir de cuántos niveles de distancia el regreso es instantáneo en vez de animado. Animar un recorrido largo se ve como un borrón, y además obliga a reconstruir el mapa decenas de veces en un segundo. En 0 siempre se anima.")]
    [SerializeField] int instantBeyondNodes = 8;

    [Tooltip("Centrar el mapa en el nodo actual al abrir la escena. Marcarlo en UNO solo de los dos pines.")]
    [SerializeField] bool centerOnStart;

    [Header("Salto")]
    [SerializeField] int   jumpCount    = 2;
    [SerializeField] float jumpHeight   = 24f;
    [SerializeField] float jumpDuration = 0.50f;
    [SerializeField] float jumpPause    = 1.80f;

    [Header("Animación")]
    [SerializeField] float scrollDuration = 0.45f;
    [SerializeField] float fadeDuration   = 0.20f;

    CanvasGroup   _group;
    RectTransform _rect;
    Vector2       _basePos;
    bool          _visible;

    Coroutine _fade, _scrollTo, _jumpAfterFade, _jumpLoop;

    void Awake()
    {
        _rect  = GetComponent<RectTransform>();
        _group = GetComponent<CanvasGroup>();
        if (!_group) _group = gameObject.AddComponent<CanvasGroup>();

        _basePos = _rect.anchoredPosition;
        SetAlpha(0f);
        _group.interactable = _group.blocksRaycasts = false;

        HookButton();
    }

    // El pin se engancha su propio botón, así no hay que acordarse de conectar el OnClick a mano
    // en cada escena — que es justo el paso que se olvida y deja el pin visible pero muerto.
    //
    // Solo si nadie lo conectó desde el Inspector: si ya hay algo ahí, se respeta y no se duplica
    // la llamada.
    void HookButton()
    {
        var button = GetComponent<Button>() ?? GetComponentInChildren<Button>(true);
        if (button == null)
        {
            Debug.LogWarning($"[WorldMapScrollPin] '{name}' no tiene ningún Button — el pin se va a ver pero no va a llevar a ningún lado.", this);
            return;
        }

        if (button.onClick.GetPersistentEventCount() == 0) button.onClick.AddListener(OnTap);
    }

    void Start()
    {
        if (scroll     == null) scroll     = FindAnyObjectByType<WorldMapScroll>();
        if (definition == null) definition = FindAnyObjectByType<WorldMapDefinition>();

        if (scroll == null || definition == null)
        {
            Debug.LogWarning($"[WorldMapScrollPin] Falta el scroll o la definición del mundo en la escena — '{name}' no va a funcionar.", this);
            enabled = false;
            return;
        }

        if (topPanel != null)
        {
            var pos = _rect.anchoredPosition;
            pos.y = -(topPanel.PanelHeight + pinPaddingFromPanel);
            _rect.anchoredPosition = pos;
            _basePos = pos;
        }

        // Si el jugador viene de ganar, la tarjeta se encarga de toda la llegada: centra el mapa
        // en el nodo VIEJO y lo lleva hasta el nuevo. Centrar acá se lo pisaría.
        //
        // Solo se mira la marca, no se consume: el que la consume es la tarjeta.
        if (centerOnStart && SaveManager.JustAdvancedFromLevelId == 0) scroll.MoveTo(TargetOffset());
    }

    // Por frame y no por evento: el scroll se mueve por arrastre, por inercia y por los propios
    // pines, y engancharse a las tres fuentes es más frágil que comparar dos números.
    void LateUpdate()
    {
        float distance = CurrentNodeZ() - scroll.Offset;
        float threshold = nodesThreshold * definition.Layout.spacing;

        bool shouldShow = direction == PinDirection.Top
            ? distance >  threshold    // el nodo está más adelante
            : distance < -threshold;   // el nodo quedó atrás

        if (shouldShow == _visible) return;
        _visible = shouldShow;

        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(Fade(_visible ? 1f : 0f));
        _group.interactable = _group.blocksRaycasts = _visible;

        StopJump();

        if (_visible) _jumpAfterFade = StartCoroutine(JumpAfterFade());
        else          _rect.anchoredPosition = _basePos;
    }

    // Dónde está, en unidades de mundo, el nodo del nivel al que le toca jugar.
    float CurrentNodeZ()
    {
        var entries = LevelLoader.Entries;
        if (entries.Count == 0) return 0f;

        int current = SaveManager.MaxUnlockedLevel;
        int index   = 0;

        for (int i = 0; i < entries.Count; i++)
            if (entries[i].id == current) { index = i; break; }

        return definition.WorldIndex(index) * definition.Layout.spacing;
    }

    // Dónde queda centrado lo decide el scroll, no el pin: si cada uno tuviera su propio
    // ajuste, el pin y la tarjeta del jugador dejarían el mapa en sitios distintos.
    float TargetOffset() => scroll.OffsetFor(CurrentNodeZ());

    // Se engancha sola al Button del pin. Queda pública por si se quiere disparar desde otro lado.
    public void OnTap()
    {
        if (_scrollTo != null) { StopCoroutine(_scrollTo); _scrollTo = null; }

        float target   = TargetOffset();
        float distance = Mathf.Abs(target - scroll.Offset);
        float limit    = instantBeyondNodes * definition.Layout.spacing;

        // De cerca conviene animar: se entiende que el mapa volvió hacia el nivel actual. De lejos
        // el recorrido deja de leerse y pasa a ser un borrón, y encima obliga a reconstruir las
        // islas y sus decoraciones una vez por capítulo atravesado, todo dentro del mismo segundo.
        if (instantBeyondNodes > 0 && distance > limit) scroll.MoveTo(target);
        else                                           _scrollTo = StartCoroutine(ScrollToNode());
    }

    IEnumerator ScrollToNode()
    {
        float from = scroll.Offset;
        float to   = TargetOffset();

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, scrollDuration))
        {
            scroll.MoveTo(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        scroll.MoveTo(to);
    }

    void StopJump()
    {
        if (_jumpAfterFade != null) { StopCoroutine(_jumpAfterFade); _jumpAfterFade = null; }
        if (_jumpLoop      != null) { StopCoroutine(_jumpLoop);      _jumpLoop      = null; }
    }

    IEnumerator JumpAfterFade()
    {
        yield return new WaitForSeconds(fadeDuration);
        _basePos  = _rect.anchoredPosition;
        _jumpLoop = StartCoroutine(JumpLoop());
    }

    IEnumerator JumpLoop()
    {
        // Cada pin salta hacia donde está el nodo: el de arriba hacia abajo, el de abajo hacia
        // arriba. El salto es la flecha.
        float dir = direction == PinDirection.Top ? -1f : 1f;

        while (true)
        {
            for (int j = 0; j < jumpCount; j++)
            {
                for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, jumpDuration))
                {
                    _rect.anchoredPosition = _basePos + new Vector2(0f, Mathf.Sin(t * Mathf.PI) * jumpHeight * dir);
                    yield return null;
                }
                _rect.anchoredPosition = _basePos;
            }
            yield return new WaitForSeconds(jumpPause);
        }
    }

    IEnumerator Fade(float to)
    {
        float from = _group.alpha;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, fadeDuration))
        {
            SetAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }
        SetAlpha(to);
    }

    void SetAlpha(float a) => _group.alpha = a;
}
