using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La mini-vista que enseña el Punto Débil dentro del TutorialPanel.
//
// No hay cañón ni trayectoria, y es el único demo sin ellos: este poder no se dispara, se SEÑALA.
// El gesto que hay que enseñar es un toque sobre una burbuja cualquiera del tablero, así que la
// mano va directa a ella en vez de apuntar desde abajo.
//
// Lo que de verdad hay que contar no es el gesto sino la EXCEPCIÓN: funciona por debajo del
// mínimo de tres. Por eso el bucle alterna dos objetivos —una pareja y una burbuja sola— y esa
// alternancia es la explicación: con un solo ejemplo, "se va con las de su color" se lee como un
// match normal y el poder parecería no aportar nada.
//
// Este objeto ES el tablero de la demostración: implementa IBoardView y le pasa sus celdas a
// BoosterRules, así que qué se lleva cada toque lo decide el MISMO código que en una partida.
public class TutorialWeakSpotDemo : MonoBehaviour, IBoardView
{
    [Tooltip("Sprites de burbuja de colores DISTINTOS, cuatro o más. Con menos, la pareja y la burbuja sola acaban compartiendo color y el poder parece hacer otra cosa.")]
    [SerializeField] Sprite[] bubbleSprites;

    [Tooltip("La mano que señala. La misma que usan los demás tutoriales: conviene que sea la misma para que el gesto se reconozca de uno al otro.")]
    [SerializeField] Sprite handSprite;

    [Header("Proporciones")]
    [Range(0.05f, 0.3f)]
    [SerializeField] float bubbleSize = 0.1f;

    [Tooltip("Proporción ancho/alto de la composición. Acota el área usada dentro del hueco para que la demostración no se estire en pantallas altas.")]
    [Range(0.4f, 1.6f)]
    [SerializeField] float aspect = 0.72f;

    [Header("Tiempos (segundos)")]
    [SerializeField] float holdTime  = 0.45f;
    [SerializeField] float reachTime = 0.6f;    // lo que tarda la mano en llegar
    [SerializeField] float pressTime = 0.18f;   // el toque
    [SerializeField] float popTime   = 0.4f;
    [SerializeField] float restTime  = 0.9f;

    // Celdas de verdad del tablero, no posiciones inventadas: así las vecindades son las del
    // juego y lo que BoosterRules calcule encima vale.
    static readonly Vector2Int[] TOP    = { new(1, 0), new(2, 0), new(3, 0), new(4, 0), new(5, 0) };
    static readonly Vector2Int[] BOTTOM = { new(1, 1), new(2, 1), new(3, 1), new(4, 1) };

    // Los colores están escritos a mano y no sorteados: la demostración entera depende de que
    // haya EXACTAMENTE una pareja y EXACTAMENTE una suelta. Al azar, dos burbujas del mismo color
    // podrían tocarse por accidente y el ejemplo dejaría de serlo.
    static readonly int[] TOP_COLORS    = { 0, 1, 1, 2, 0 };   // (2,0)+(3,0) son la pareja
    static readonly int[] BOTTOM_COLORS = { 3, 0, 2, 0 };      // (1,1) es la que está sola

    // Y los dos objetivos, en el orden en que se enseñan: primero la pareja —que es la excepción
    // que hay que contar— y después la suelta, que es su caso extremo.
    static readonly Vector2Int[] TARGETS = { new(2, 0), new(1, 1) };

    // El ancla de la geometría. Fija, para que el tablero no salte de sitio entre repeticiones.
    static readonly Vector2Int ANCHOR = new(3, 0);

    readonly Dictionary<Vector2Int, Image> _board  = new();
    readonly Dictionary<Vector2Int, int>   _colors = new();

    RectTransform _area;
    Image         _hand;
    Coroutine     _loop;
    float         _builtFor;
    int           _target;   // a cuál de los dos le toca esta vuelta

    void OnEnable()
    {
        if (!Ready()) return;

        _loop = StartCoroutine(Run());
    }

    void OnDisable()
    {
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
    }

    bool Ready()
    {
        if (bubbleSprites != null && bubbleSprites.Length >= 4 && handSprite != null) return true;

        Debug.LogWarning($"[TutorialWeakSpotDemo] '{name}' necesita al menos cuatro sprites de burbuja de colores distintos y el de la mano: sin eso no hay nada que mostrar.", this);
        return false;
    }

    // --- IBoardView: lo que BoosterRules necesita saber de este tablero -------------------

    public IEnumerable<Vector2Int> Occupied => _board.Keys;

    public bool TryGetColor(Vector2Int cell, out BubbleColor color)
    {
        color = default;

        if (!_colors.TryGetValue(cell, out int index)) return false;

        // Los índices de sprite se leen como colores del juego para que LinksWith —que es lo que
        // agrupa de verdad— tenga algo con qué comparar. Rainbow queda fuera: acá no hay comodín
        // y uno por accidente conectaría el tablero entero.
        color = (BubbleColor)(index % (int)BubbleColor.Rainbow);
        return true;
    }

    // --- Armado ---

    IEnumerator Run()
    {
        _area = (RectTransform)transform;

        // Ocupar el hueco que le toque: todas las medidas salen del alto del área, así que sin
        // esto la coreografía se dibujaría a la escala del prefab y no a la del panel.
        _area.anchorMin = Vector2.zero;
        _area.anchorMax = Vector2.one;
        _area.offsetMin = Vector2.zero;
        _area.offsetMax = Vector2.zero;

        const float TIMEOUT = 1f;

        for (float t = 0f; _area.rect.height < 1f && t < TIMEOUT; t += Time.unscaledDeltaTime)
            yield return null;

        if (_area.rect.height < 1f)
        {
            Debug.LogWarning($"[TutorialWeakSpotDemo] '{name}' quedó sin alto: el layout del panel no le dio sitio.", this);
            yield break;
        }

        Build();

        yield return Loop();
    }

    void Build()
    {
        _builtFor = _area.rect.height;

        for (int i = _area.childCount - 1; i >= 0; i--) Discard(_area.GetChild(i).gameObject);

        _board.Clear();
        _colors.Clear();

        float size = Size();

        for (int i = 0; i < TOP.Length; i++)    Place(TOP[i],    TOP_COLORS[i],    size);
        for (int i = 0; i < BOTTOM.Length; i++) Place(BOTTOM[i], BOTTOM_COLORS[i], size);

        _hand = Spawn("Hand", handSprite, size * 1.5f);
    }

    void Place(Vector2Int cell, int color, float size)
    {
        var bubble = Spawn($"Bubble{cell.x}_{cell.y}", bubbleSprites[color % bubbleSprites.Length], size);

        ((RectTransform)bubble.transform).anchoredPosition = CellPos(cell);

        _board[cell]  = bubble;
        _colors[cell] = color;
    }

    Image Spawn(string label, Sprite sprite, float size)
    {
        var image = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                        .GetComponent<Image>();
        var rt = (RectTransform)image.transform;

        rt.SetParent(_area, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * size;

        image.sprite         = sprite;
        image.raycastTarget  = false;   // es una demostración: nada acá se toca
        image.preserveAspect = true;

        return image;
    }

    static void Discard(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else                       DestroyImmediate(go);
    }

    // --- Geometría, toda relativa al hueco que haya ---

    float Height() => Mathf.Min(_area.rect.height, _area.rect.width / Mathf.Max(0.1f, aspect));
    float Size()   => Height() * bubbleSize;

    float Scale => Size() / HexGridMath.BubbleDiameter;

    // La MISMA fórmula del tablero, escalada y medida desde el ancla. Derivada y no copiada: el
    // tejido hexagonal de la demostración es el del juego por construcción.
    Vector2 CellPos(Vector2Int cell) =>
        (HexGridMath.CellToLocalPos(cell) - HexGridMath.CellToLocalPos(ANCHOR)) * Scale + new Vector2(0f, Height() * 0.08f);

    // De dónde sale la mano: de abajo y a un lado, fuera del tablero. Entrando desde el borde se
    // lee como una mano que llega; naciendo encima de una burbuja, como un dibujo que aparece.
    Vector2 HandRest() => new(Height() * 0.22f, -Height() * 0.34f);

    // --- La coreografía ---

    IEnumerator Loop()
    {
        while (true)
        {
            // El panel puede cambiar de alto (otro idioma con un texto más largo lo estira) y
            // todas las medidas salen de ahí. Se rehace entre repeticiones, nunca a mitad de una.
            if (!Mathf.Approximately(_builtFor, _area.rect.height) && _area.rect.height > 1f) Build();

            Reset();
            yield return new WaitForSecondsRealtime(holdTime);

            yield return Reach();
            yield return Press();
            yield return Pop();

            yield return new WaitForSecondsRealtime(restTime);

            // Y al otro objetivo. Es el bucle el que enseña que el poder no pide tres: una vuelta
            // se lleva una pareja y la siguiente una burbuja sola.
            _target = (_target + 1) % TARGETS.Length;
        }
    }

    void Reset()
    {
        float size = Size();

        foreach (var pair in _board)
        {
            var bubble = pair.Value;
            if (bubble == null) continue;

            bubble.enabled              = true;
            bubble.color                = Color.white;
            bubble.transform.localScale = Vector3.one;

            ((RectTransform)bubble.transform).sizeDelta = Vector2.one * size;
        }

        if (_hand == null) return;

        _hand.enabled              = true;
        _hand.transform.localScale = Vector3.one;
        ((RectTransform)_hand.transform).anchoredPosition = HandRest();
    }

    // La mano va DERECHA a la burbuja, sin pasar por el cañón: lo que se enseña es señalar.
    IEnumerator Reach()
    {
        if (_hand == null) yield break;

        var rt   = (RectTransform)_hand.transform;
        var from = HandRest();
        var to   = CellPos(TARGETS[_target]) + new Vector2(Size() * 0.3f, -Size() * 0.45f);

        for (float t = 0f; t < reachTime; t += Time.unscaledDeltaTime)
        {
            rt.anchoredPosition = Vector2.Lerp(from, to, Smooth(Mathf.Clamp01(t / reachTime)));
            yield return null;
        }

        rt.anchoredPosition = to;
    }

    // El toque: la mano se encoge y la burbuja responde. Las dos cosas a la vez son lo que ata el
    // gesto a su efecto — la mano sola se lee como que pasaba por ahí.
    IEnumerator Press()
    {
        _board.TryGetValue(TARGETS[_target], out var target);

        for (float t = 0f; t < pressTime; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Sin(Mathf.Clamp01(t / pressTime) * Mathf.PI);

            if (_hand != null) _hand.transform.localScale = Vector3.one * (1f - 0.18f * p);
            if (target != null) target.transform.localScale = Vector3.one * (1f + 0.22f * p);

            yield return null;
        }

        if (_hand != null)  _hand.transform.localScale  = Vector3.one;
        if (target != null) target.transform.localScale = Vector3.one;
    }

    // Y se va con su racimo, preguntado al MISMO sitio que en una partida: si algún día el poder
    // dejara de ignorar el mínimo de tres, acá dejaría de llevarse la pareja solo.
    IEnumerator Pop()
    {
        var cell  = TARGETS[_target];
        var group = BoosterRules.CellsHitBy(Booster.WeakSpot, cell, cell, this);

        float step = group.Count > 1 ? popTime * 0.4f / (group.Count - 1) : 0f;

        for (int i = 0; i < group.Count; i++) StartCoroutine(Vanish(group[i], i * step));

        // La mano se retira mientras revienta: quedándose encima taparía justo lo que hay que ver.
        if (_hand != null) StartCoroutine(Withdraw());

        yield return new WaitForSecondsRealtime(popTime);
    }

    IEnumerator Withdraw()
    {
        var rt   = (RectTransform)_hand.transform;
        var from = rt.anchoredPosition;
        var to   = HandRest();

        for (float t = 0f; t < popTime * 0.6f; t += Time.unscaledDeltaTime)
        {
            rt.anchoredPosition = Vector2.Lerp(from, to, Smooth(Mathf.Clamp01(t / (popTime * 0.6f))));
            yield return null;
        }
    }

    IEnumerator Vanish(Vector2Int cell, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        if (!_board.TryGetValue(cell, out var bubble) || bubble == null) yield break;

        var   rt   = (RectTransform)bubble.transform;
        float size = rt.sizeDelta.x;

        // Crece y se deshace, igual que el pop del tablero: encogerse a secas se lee como que se
        // va hacia atrás, no como que revienta.
        for (float t = 0f; t < popTime * 0.6f; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / (popTime * 0.6f));

            rt.sizeDelta = Vector2.one * size * (1f + 0.3f * p);
            bubble.color = new Color(1f, 1f, 1f, 1f - p);
            yield return null;
        }

        bubble.enabled = false;
    }

    static float Smooth(float p) => p * p * (3f - 2f * p);
}
