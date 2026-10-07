using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La mini-vista que enseña el cambio de burbuja dentro del TutorialPanel.
//
// Lo que hay que contar no es el gesto sino POR QUÉ se hace, así que la demostración empieza con
// la burbuja equivocada en la recámara: el color que hace match está esperando en la cola. Sin ese
// planteo, un tutorial de "tócala y se cambian" solo enseña a mover cosas de sitio.
//
// Dibujada por código como las otras: lo que hay que mantener en pie es una coreografía —la mano
// acaba donde está la burbuja, las dos se cruzan por el aire, la línea sale hacia el hueco— y
// compuesta a mano habría que realinearla cada vez que cambie el tamaño del panel.
public class TutorialSwapDemo : MonoBehaviour
{
    [Tooltip("Dos sprites de burbuja de colores distintos. El PRIMERO es el que hace match (el que espera en la cola); el segundo es el que arranca en la recámara y no sirve.")]
    [SerializeField] Sprite[] bubbleSprites;

    [Tooltip("La mano que toca. La misma que usan los otros tutoriales, para que el gesto se reconozca.")]
    [SerializeField] Sprite handSprite;

    [Tooltip("El punto de la línea de trayectoria. Vacío usa la burbuja en chiquito.")]
    [SerializeField] Sprite dotSprite;

    [SerializeField] Color dotColor = new(1f, 1f, 1f, 0.85f);

    [Header("Proporciones")]
    [Range(0.05f, 0.3f)] [SerializeField] float bubbleSize = 0.1f;

    [Tooltip("Proporción ancho/alto de la composición. Acota el área usada dentro del hueco para que la demostración no se estire en pantallas altas. 0.72 es casi el formato de un móvil en vertical.")]
    [Range(0.4f, 1.6f)]
    [SerializeField] float aspect = 0.72f;
    [Range(4, 24)]       [SerializeField] int   dots       = 10;
    [Range(0.1f, 0.6f)]  [SerializeField] float dotSize    = 0.2f;

    [Tooltip("Cuánto mide la burbuja de la cola respecto a la de la recámara. En el juego la siguiente se ve más chica.")]
    [Range(0.4f, 1f)]    [SerializeField] float nextScale  = 0.68f;

    [Tooltip("Cuánto se arquean las dos burbujas al cruzarse, en tanto por uno de la distancia entre ellas. En cero se atraviesan en línea recta y se lee como un parpadeo.")]
    [Range(0f, 0.8f)]    [SerializeField] float swapArc    = 0.45f;

    [Header("Tiempos (segundos)")]
    [SerializeField] float holdTime    = 0.5f;   // el tablero quieto, con la burbuja que no sirve
    [SerializeField] float tapTime     = 0.45f;  // la mano baja hasta la recámara
    [SerializeField] float swapTime    = 0.5f;   // las dos se cruzan por el aire
    [SerializeField] float settleTime  = 0.35f;  // un respiro con el color ya cambiado
    [SerializeField] float flyTime     = 0.45f;  // la burbuja sube
    [SerializeField] float popTime     = 0.4f;   // las tres estallan
    [SerializeField] float restTime    = 0.8f;   // y se espera antes de repetir

    // Celdas de verdad del tablero. TARGET es vecino hexagonal de (2,0) y (3,0) —las dos del color
    // que hace match—, así que el disparo completa el trío.
    static readonly Vector2Int[] ROW_CELLS = { new(1, 0), new(2, 0), new(3, 0), new(4, 0) };
    static readonly Vector2Int   TARGET    = new(2, 1);

    // El color que matchea (índice 0) en las dos del medio; el otro a los lados, para que se vea
    // que el de la recámara no sirve.
    static readonly int[] ROW = { 1, 0, 0, 1 };

    readonly List<Image> _row   = new();
    readonly List<Image> _trail = new();

    RectTransform _area;
    Image         _shot;    // la de la recámara
    Image         _queued;  // la de la cola
    Image         _hand;
    Coroutine     _loop;
    float         _builtFor;

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
        if (bubbleSprites != null && bubbleSprites.Length >= 2 && handSprite != null) return true;

        Debug.LogWarning($"[TutorialSwapDemo] '{name}' necesita dos sprites de burbuja de colores distintos y uno de mano: sin eso no hay nada que mostrar.", this);
        return false;
    }

    // --- Armado ---

    IEnumerator Run()
    {
        _area = (RectTransform)transform;

        _area.anchorMin = Vector2.zero;
        _area.anchorMax = Vector2.one;
        _area.offsetMin = Vector2.zero;
        _area.offsetMax = Vector2.zero;

        const float TIMEOUT = 1f;

        for (float t = 0f; _area.rect.height < 1f && t < TIMEOUT; t += Time.unscaledDeltaTime)
            yield return null;

        if (_area.rect.height < 1f)
        {
            Debug.LogWarning($"[TutorialSwapDemo] '{name}' quedó sin alto: el layout del panel no le dio sitio.", this);
            yield break;
        }

        Build();

        yield return Loop();
    }

    void Build()
    {
        _builtFor = _area.rect.height;

        for (int i = _area.childCount - 1; i >= 0; i--) Discard(_area.GetChild(i).gameObject);
        _row.Clear();
        _trail.Clear();

        float size = Size();

        // La línea primero, para que quede detrás: en UGUI el orden de dibujo es el de la jerarquía.
        for (int i = 0; i < dots; i++)
        {
            var dot = Spawn("Dot", dotSprite != null ? dotSprite : bubbleSprites[0], size * dotSize);
            dot.color = dotColor;
            _trail.Add(dot);
        }

        for (int i = 0; i < ROW.Length; i++)
        {
            var bubble = Spawn($"Bubble{i}", bubbleSprites[ROW[i] % bubbleSprites.Length], size);
            Place(bubble, CellPos(ROW_CELLS[i]));
            _row.Add(bubble);
        }

        _shot   = Spawn("Loaded", bubbleSprites[1 % bubbleSprites.Length], size);
        _queued = Spawn("Queued", bubbleSprites[0], size * nextScale);
        _hand   = Spawn("Hand", handSprite, size * 1.5f);
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

    static void Place(Graphic graphic, Vector2 at) => ((RectTransform)graphic.transform).anchoredPosition = at;

    static void Size(Graphic graphic, float size) => ((RectTransform)graphic.transform).sizeDelta = Vector2.one * size;

    static void Discard(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else                       DestroyImmediate(go);
    }

    // --- Geometría, toda relativa al hueco que haya ---

    // Todas las medidas salen de un área de PROPORCIÓN FIJA centrada en el hueco, no del hueco
    // entero. En una pantalla alta —un iPad, un panel estirado por un texto largo— la composición
    // se repartía por todo el alto y la acción quedaba perdida en el medio con los bordes vacíos.
    //
    // Acotando el alto a lo que el ancho permite, la demostración se ve igual de compacta en
    // cualquier pantalla y solo cambia su tamaño, que es lo que debe cambiar.
    float Height() => Mathf.Min(_area.rect.height, _area.rect.width / Mathf.Max(0.1f, aspect));
    float Width()  => Mathf.Min(_area.rect.width, Height() * aspect);
    float Size()   => Height() * bubbleSize;

    float Scale => Size() / HexGridMath.BubbleDiameter;

    Vector2 CellPos(Vector2Int cell) =>
        (HexGridMath.CellToLocalPos(cell) - HexGridMath.CellToLocalPos(TARGET)) * Scale + TargetPos();

    Vector2 TargetPos() => new(0f, Height() * 0.14f);

    Vector2 CannonPos() => new(0f, -Height() * 0.32f);

    // La cola va al costado y más abajo, como en el cañón del juego.
    Vector2 QueuePos() => CannonPos() + new Vector2(Width() * 0.17f, -Size() * 0.55f);

    // --- La coreografía ---

    IEnumerator Loop()
    {
        while (true)
        {
            if (!Mathf.Approximately(_builtFor, _area.rect.height) && _area.rect.height > 1f) Build();

            Reset();
            yield return new WaitForSecondsRealtime(holdTime);

            yield return Tap();
            yield return Swap();
            yield return new WaitForSecondsRealtime(settleTime);
            yield return Fly();
            yield return Pop();

            yield return new WaitForSecondsRealtime(restTime);
        }
    }

    void Reset()
    {
        float size = Size();

        foreach (var bubble in _row)
        {
            bubble.color = Color.white;
            bubble.transform.localScale = Vector3.one;
        }

        // Arranca con el color equivocado en la recámara: es el planteo del tutorial, no un
        // detalle. El que sirve espera en la cola.
        _shot.enabled = true;
        _shot.color   = Color.white;
        _shot.sprite  = bubbleSprites[1 % bubbleSprites.Length];
        _shot.transform.localScale = Vector3.one;
        Size(_shot, size);
        Place(_shot, CannonPos());

        _queued.enabled = true;
        _queued.color   = Color.white;
        _queued.sprite  = bubbleSprites[0];
        Size(_queued, size * nextScale);
        Place(_queued, QueuePos());

        _hand.enabled = true;
        _hand.transform.localScale = Vector3.one;
        Place(_hand, HandAway());

        ShowTrail(0f);
    }

    // La mano entra desde abajo y a un lado, para que se vea de dónde viene.
    Vector2 HandAway()   => CannonPos() + new Vector2(Size() * 1.6f, -Size() * 2.2f);
    Vector2 HandOnShot() => CannonPos() + new Vector2(Size() * 0.45f, -Size() * 0.65f);

    IEnumerator Tap()
    {
        Vector2 from = HandAway();
        Vector2 to   = HandOnShot();

        for (float t = 0f; t < tapTime; t += Time.unscaledDeltaTime)
        {
            float p = Smooth(t / tapTime);
            Place(_hand, Vector2.Lerp(from, to, p));
            yield return null;
        }

        Place(_hand, to);

        // El toque: la mano se encoge y la burbuja responde. Sin la respuesta, el dedo parece
        // posarse sin que pase nada.
        const float PRESS = 0.12f;
        for (float t = 0f; t < PRESS; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Sin(Mathf.Clamp01(t / PRESS) * Mathf.PI);

            _hand.transform.localScale = Vector3.one * (1f - 0.18f * p);
            _shot.transform.localScale = Vector3.one * (1f - 0.12f * p);
            yield return null;
        }

        _hand.transform.localScale = Vector3.one;
        _shot.transform.localScale = Vector3.one;
    }

    // Las dos se cruzan por el aire, cada una arqueada hacia un lado — igual que en el cañón de
    // verdad. Un intercambio instantáneo de sprites se ve como un parpadeo y no cuenta nada.
    IEnumerator Swap()
    {
        _hand.enabled = false;

        Vector2 here  = CannonPos();
        Vector2 there = QueuePos();
        float   big   = Size();
        float   small = Size() * nextScale;

        // Perpendicular al recorrido y no "hacia arriba": las dos ranuras están en diagonal, así
        // que un arco vertical fijo se vería torcido respecto del camino.
        Vector2 bow = Vector2.Perpendicular(there - here).normalized * Vector2.Distance(here, there) * swapArc;

        for (float t = 0f; t < swapTime; t += Time.unscaledDeltaTime)
        {
            float p     = Smooth(t / swapTime);
            float curve = Mathf.Sin(p * Mathf.PI);   // 0 -> 1 -> 0: el arco abre y vuelve a cerrar

            Place(_shot,   Vector2.Lerp(here, there, p) + bow * curve);
            Place(_queued, Vector2.Lerp(there, here, p) - bow * curve);

            // El tamaño viaja con la burbuja: las ranuras no miden lo mismo, y sin esto cada una
            // daría un salto de escala al llegar.
            Size(_shot,   Mathf.Lerp(big, small, p));
            Size(_queued, Mathf.Lerp(small, big, p));

            yield return null;
        }

        // Ya en su sitio. A partir de acá la que dispara es la que estaba en la cola, así que las
        // dos referencias se intercambian y el resto de la coreografía no se entera del cambio.
        Place(_shot, there);   Size(_shot, small);
        Place(_queued, here);  Size(_queued, big);

        (_shot, _queued) = (_queued, _shot);

        ShowTrail(1f);
    }

    IEnumerator Fly()
    {
        Vector2 from = CannonPos();
        Vector2 to   = TargetPos();

        for (float t = 0f; t < flyTime; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / flyTime);

            Place(_shot, Vector2.Lerp(from, to, p));
            ShowTrail(1f - p);   // la línea se consume por detrás de la burbuja
            yield return null;
        }

        Place(_shot, to);
        ShowTrail(0f);
    }

    IEnumerator Pop()
    {
        var popping = new List<Graphic> { _row[1], _row[2], _shot };

        for (float t = 0f; t < popTime; t += Time.unscaledDeltaTime)
        {
            float p = t / popTime;

            foreach (var g in popping)
            {
                g.transform.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(p * Mathf.PI) - p * 0.5f);
                g.color = new Color(1f, 1f, 1f, 1f - p);
            }

            yield return null;
        }

        foreach (var g in popping) g.color = new Color(1f, 1f, 1f, 0f);
        _shot.enabled = false;
    }

    void ShowTrail(float progress)
    {
        Vector2 from = CannonPos();
        Vector2 to   = TargetPos();
        int     show = Mathf.RoundToInt(Mathf.Clamp01(progress) * _trail.Count);

        for (int i = 0; i < _trail.Count; i++)
        {
            _trail[i].enabled = i < show;

            if (i < show) Place(_trail[i], Vector2.Lerp(from, to, (i + 1f) / _trail.Count));
        }
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
