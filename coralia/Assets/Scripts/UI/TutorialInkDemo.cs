using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La mini-vista que enseña la Tinta de Pulpo dentro del TutorialPanel. Su entrada en el panel es
// 'ink', y el bucle es:
//
//   1. en el cañón, la tinta — y debajo, pequeña, la burbuja que VUELVE al dispararla
//   2. la mano apunta y suelta, sin marcar nada: se apunta como un disparo normal
//   3. la tinta revienta el racimo que toca, como una burbuja cualquiera
//   4. y caen manchas sobre unas cuantas de alrededor, que al irse las dejan pintadas
//   5. la recámara se queda con el color que acaba de repartir
//
// Lo que hay que enseñar no es la salpicadura: es que el color de las manchas y el que queda en
// el cañón son EL MISMO. Ese es el poder entero, y es lo que no se entiende leyendo "tiñe del
// color que vuelve".
//
// Por eso la burbuja pequeña del cañón está desde el primer frame y sigue ahí al terminar: es el
// hilo que une las dos cosas.
//
// Este objeto ES el tablero de la demostración: implementa IBoardView y le pasa su propio racimo
// a BoosterRules, así que a qué burbujas salpica lo decide el MISMO código que en una partida.
public class TutorialInkDemo : MonoBehaviour, IBoardView
{
    [Tooltip("Los colores con los que se dibuja esta demostración, elegidos de los que el juego ya tiene. El arte sale del BubbleCatalog: así una burbuja del tutorial no puede verse distinta de la del tablero, y cambiar el arte de un color lo cambia en los dos sitios.")]
    [SerializeField] BubbleColor[] bubbleColors = { BubbleColor.Blue, BubbleColor.Red, BubbleColor.Yellow };

    [Tooltip("La mano que apunta. La misma que usan los demás tutoriales: conviene que sea la misma para que el gesto se reconozca de uno al otro.")]
    [SerializeField] Sprite handSprite;

    [Header("Proporciones")]
    [Tooltip("Proporción ancho/alto de la composición. Acota el área usada dentro del hueco para que la demostración no se estire en pantallas altas.")]
    [Range(0.4f, 1.6f)]
    [SerializeField] float aspect = 0.72f;

    [Tooltip("Cuánto mide la burbuja que espera turno en el cañón, respecto a una normal. Pequeña a propósito: acompaña, no compite con la tinta.")]
    [Range(0.3f, 1f)]
    [SerializeField] float nextSize = 0.55f;

    [Tooltip("Cuántos puntos tiene la línea de la mira.")]
    [Range(4, 24)]
    [SerializeField] int dots = 10;

    [Tooltip("Tamaño de cada punto de la mira, en tanto por uno de la burbuja.")]
    [Range(0.1f, 0.6f)]
    [SerializeField] float dotSize = 0.2f;

    [SerializeField] Color dotColor = new(1f, 1f, 1f, 0.9f);

    [Tooltip("De qué color salen las manchas: el mismo que tenga el primer sprite de burbuja, que es el que la tinta reparte. En alfa 0 usa el morado de la tinta.")]
    [SerializeField] Color blotColor;

    [Tooltip("Cuánto se desvía la mira al empezar a apuntar, en grados. Es lo que convierte el gesto en APUNTAR en vez de en arrastrar la burbuja hasta su sitio.")]
    [Range(0f, 60f)]
    [SerializeField] float aimSweep = 30f;

    [Range(0.1f, 0.9f)]
    [SerializeField] float handDistance = 0.42f;

    [Header("Tiempos (segundos)")]
    [SerializeField] float holdTime    = 0.5f;
    [SerializeField] float aimTime     = 0.7f;
    [SerializeField] float previewTime = 0.9f;   // más largo que en los demás: acá se LEE el color que vuelve al cañón
    [SerializeField] float releaseTime = 0.15f;
    [SerializeField] float flyTime     = 0.4f;
    [SerializeField] float burstTime   = 0.35f;  // lo que tarda en reventar el racimo que toca
    [SerializeField] float splashTime  = 0.5f;   // y en caer todas las manchas
    [SerializeField] float fadeTime    = 0.25f;  // y cada burbuja en cambiar de color
    [SerializeField] float restTime    = 1.1f;

    // El racimo, escrito a mano y no generado: la tinta tiene que caer en medio de colores
    // MEZCLADOS para que se vea que reparte el suyo entre ellos. Un racimo al azar podría salir
    // casi monocromo y entonces la salpicadura no se distinguiría de un match.
    //
    // El color 0 —el que vuelve al cañón— no aparece en el tablero a propósito: así todo lo que
    // se tiña es visiblemente nuevo.
    static readonly (int col, int row, int color)[] CLUSTER =
    {
        (2, 0, 1), (3, 0, 2), (4, 0, 1), (5, 0, 2), (6, 0, 1), (7, 0, 2),
        (2, 1, 2), (3, 1, 1), (4, 1, 2), (5, 1, 1), (6, 1, 2), (7, 1, 1),
        (3, 2, 1), (4, 2, 2), (5, 2, 1), (6, 2, 2),
    };

    static Vector2Int Struck => new(4, 2);   // contra cuál choca
    static Vector2Int Landed => new(4, 3);   // y dónde se posa, justo debajo

    // El color que vuelve a la recámara, y con el que la tinta pinta. Es un índice de
    // 'bubbleColors', igual que en el racimo.
    const int RETURNING = 0;

    // Para que la demostración salpique SIEMPRE las mismas. InkCells sortea a propósito —cada
    // tiro distinto— y en un tutorial eso dejaría la frase hablando de un reparto que cambia a
    // cada vuelta del bucle.
    const int SEED = 20261014;

    readonly Dictionary<Vector2Int, Image> _cluster = new();
    readonly Dictionary<Vector2Int, int>   _colors  = new();
    readonly Dictionary<Vector2Int, Image> _blots   = new();

    List<Vector2Int> _chain;
    readonly List<Image>                   _trail   = new();

    List<Vector2Int> _splashed;

    Vector2 _aimTo;

    RectTransform _area;
    Image         _ink;
    Image         _next;
    Image         _hand;
    Coroutine     _loop;
    float         _builtFor;

    // --- IBoardView: el racimo de la demostración, visto como lo ve un poder ---

    public IEnumerable<Vector2Int> Occupied => _colors.Keys;

    public bool TryGetColor(Vector2Int cell, out BubbleColor color)
    {
        // El color es el de verdad, el que esté elegido en 'Bubble Colors': antes acá se colaba
        // el índice del sprite disfrazado de color, y dos azules en huecos distintos del array se
        // leían como colores distintos.
        if (_colors.TryGetValue(cell, out int index)) { color = ColorAt(index); return true; }

        color = default;
        return false;
    }


    // El sprite de uno de los colores de arriba. El módulo vive acá y no en cada llamada: con
    // diez demostraciones pidiéndolo, el que se olvide revienta con un índice fuera de rango.
    Sprite SpriteAt(int index) =>
        bubbleColors != null && bubbleColors.Length > 0
            ? BubbleCatalog.Sprite(bubbleColors[((index % bubbleColors.Length) + bubbleColors.Length) % bubbleColors.Length])
            : null;

    // Y el color en sí, para lo que necesite saberlo y no solo dibujarlo.
    BubbleColor ColorAt(int index) =>
        bubbleColors != null && bubbleColors.Length > 0
            ? bubbleColors[((index % bubbleColors.Length) + bubbleColors.Length) % bubbleColors.Length]
            : BubbleColor.Red;

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

    // En el tablero la burbuja de tinta se arma por capas (SpecialBubbleSkin: frame, líquido,
    // crestas, burbujitas). Acá es una sola Image, y el frame a secas se ve como una burbuja azul
    // cualquiera — que es justo lo que el tutorial no puede permitirse. El icono ya es la burbuja
    // con el frasco dentro, así que se usa ese y el frame queda de respaldo.
    static Sprite InkSprite => BoosterRules.IconFor(Booster.Ink) ?? BoosterRules.BubbleSpriteFor(Booster.Ink);

    bool Ready()
    {
        if (bubbleColors != null && bubbleColors.Length >= 3 && InkSprite != null) return true;

        Debug.LogWarning($"[TutorialInkDemo] '{name}' necesita al menos tres colores de burbuja distintos en 'Bubble Colors', y que BoosterCatalog tenga el arte de la Tinta de Pulpo: sin eso no hay nada que mostrar.", this);
        return false;
    }

    // Mismo motivo que en los demás demos: dentro de un VerticalLayoutGroup el hueco todavía no
    // tiene tamaño cuando el objeto se activa, y medir ahí deja todo apilado en un punto.
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
            Debug.LogWarning($"[TutorialInkDemo] '{name}' quedó sin alto: el layout del panel no le dio sitio y no hay dónde dibujar la demostración.", this);
            yield break;
        }

        Build();

        yield return Loop();
    }

    void Build()
    {
        _builtFor = _area.rect.height;

        for (int i = _area.childCount - 1; i >= 0; i--) Discard(_area.GetChild(i).gameObject);
        _cluster.Clear();
        _colors.Clear();
        _blots.Clear();
        _trail.Clear();

        float size = Size();

        for (int i = 0; i < dots; i++)
        {
            var dot = Spawn("Dot", SparkleTextures.Glow, size * dotSize);
            dot.color   = dotColor;
            dot.enabled = false;
            _trail.Add(dot);
        }

        foreach (var (col, row, color) in CLUSTER)
        {
            var cell   = new Vector2Int(col, row);
            var bubble = BubbleGlow.AddTo(Spawn($"Bubble{col}_{row}", SpriteAt(color), size));

            Place(bubble, CellPos(cell));
            _cluster[cell] = bubble;
            _colors[cell]  = color;
        }

        _splashed = Splashed();
        _chain    = BoosterRules.CellsHitBy(Booster.Ink, Landed, Struck, this);

        // Sin marcas de zona: la Tinta se apunta como un disparo normal, y el tutorial tiene que
        // enseñar el gesto que el jugador va a hacer de verdad.
        //
        // Las manchas se crean ya, apagadas, para no instanciar a mitad de la animación.
        foreach (var cell in _splashed)
        {
            var blot = Spawn($"Blot{cell.x}_{cell.y}", BoosterRules.InkDrop, size * BoosterRules.InkDropSize);

            blot.enabled = false;
            Place(blot, CellPos(cell));
            _blots[cell] = blot;
        }

        _ink  = Spawn("Ink",  InkSprite, size);
        _next = BubbleGlow.AddTo(Spawn("Next", SpriteAt(RETURNING), size * nextSize));

        _hand = handSprite != null ? Spawn("Hand", handSprite, size * 1.6f) : null;
    }

    // A qué burbujas salpica, preguntado al MISMO sitio que en una partida.
    //
    // Con la semilla fijada para que el reparto sea siempre el mismo, y devuelta después a donde
    // estaba: sortear acá y dejar el generador tocado le cambiaría los resultados a todo lo demás
    // que sortee en este frame.
    List<Vector2Int> Splashed()
    {
        var state = Random.state;
        Random.InitState(SEED);

        var cells = BoosterRules.InkCells(Landed, this, BoosterRules.InkSplash, BoosterRules.InkRadius);

        Random.state = state;

        // De la más cercana hacia afuera, igual que en el juego: la mancha se abre desde donde
        // cayó la tinta.
        Vector2 from = CellPos(Landed);
        cells.Sort((a, b) => Vector2.Distance(from, CellPos(a)).CompareTo(Vector2.Distance(from, CellPos(b))));

        return cells;
    }

    // De qué color sale la mancha. En el juego sale sola: el color que reparte es un BubbleColor y
    // tiene su tinte en la tabla. Acá los colores son índices de 'bubbleColors', y qué color tiene
    // el sprite que se le asigne no hay manera de saberlo sin leerle los píxeles — que es justo lo
    // que no se puede hacer con una textura importada. Por eso se configura, y sin configurar cae
    // en el morado de la tinta, que es de lo que va el poder.
    Color TintColor() => blotColor.a > 0f ? blotColor : BoosterRules.InkDropTint(BubbleColor.Purple);

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

    static void Discard(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else                       DestroyImmediate(go);
    }

    // --- Geometría, toda relativa al hueco que haya ---

    float Height() => Mathf.Min(_area.rect.height, _area.rect.width / Mathf.Max(0.1f, aspect));
    float Width()  => Mathf.Min(_area.rect.width, Height() * aspect);

    float Size()  => Width() / (HexGridMath.ColsInRow(0) + 0.5f);
    float Scale   => Size() / HexGridMath.BubbleDiameter;

    Vector2 CellPos(Vector2Int cell) =>
        (HexGridMath.CellToLocalPos(cell) - HexGridMath.CellToLocalPos(Landed)) * Scale + LandedPos;

    Vector2 LandedPos   => new(0f, Height() * 0.10f);
    Vector2 CannonPos() => new(LandedPos.x, -Height() * 0.40f);

    // La que espera turno va PEGADA a la tinta y un poco abajo, como el pulpo del juego respecto
    // a la concha: tiene que leerse como "esta es la siguiente", no como otra cosa suelta.
    Vector2 NextPos() => CannonPos() + new Vector2(-Size() * 0.85f, -Size() * 0.15f);

    // --- La mira ---

    float TargetAngle()
    {
        Vector2 d = CellPos(Landed) - CannonPos();
        return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
    }

    float StartAngle() => TargetAngle() + aimSweep;

    Vector2 AimPointAt(float degrees)
    {
        float   rad = degrees * Mathf.Deg2Rad;
        Vector2 dir = new(Mathf.Sin(rad), Mathf.Cos(rad));

        return CannonPos() + dir * Vector2.Distance(CannonPos(), CellPos(Landed));
    }

    Vector2 HandAtCannon() => CannonPos() + new Vector2(Size() * 0.45f, -Size() * 0.7f);

    Vector2 HandOnAim() =>
        Vector2.Lerp(CannonPos(), _aimTo, handDistance) + new Vector2(Size() * 0.6f, -Size() * 0.4f);

    void ShowTrail(float progress)
    {
        int show = Mathf.RoundToInt(Mathf.Clamp01(progress) * _trail.Count);

        for (int i = 0; i < _trail.Count; i++)
        {
            _trail[i].enabled = i < show;

            if (i < show) Place(_trail[i], Vector2.Lerp(CannonPos(), _aimTo, (i + 1f) / _trail.Count));
        }
    }

    // --- La coreografía ---

    IEnumerator Loop()
    {
        while (true)
        {
            if (!Mathf.Approximately(_builtFor, _area.rect.height) && _area.rect.height > 1f) Build();

            Reset();
            yield return new WaitForSecondsRealtime(holdTime);

            yield return Aim();
            yield return Preview();
            yield return Release();
            yield return Fly();
            yield return Splash();

            yield return new WaitForSecondsRealtime(restTime);
        }
    }

    void Reset()
    {
        // Los colores vuelven a los del racimo, no solo las imágenes: la vuelta anterior dejó
        // unas cuantas teñidas, y arrancar sin deshacerlo iría tiñendo el tablero entero vuelta
        // a vuelta hasta que no quedara nada que salpicar.
        foreach (var (col, row, color) in CLUSTER)
        {
            var cell = new Vector2Int(col, row);

            _colors[cell]  = color;
            _cluster[cell].sprite                = SpriteAt(color);
            _cluster[cell].enabled               = true;
            _cluster[cell].color                 = Color.white;
            _cluster[cell].transform.localScale  = Vector3.one;
        }


        // Las manchas vuelven apagadas y a su tamaño: terminan desvanecidas y encogidas, y sin
        // esto la segunda vuelta del bucle las heredaría así.
        foreach (var blot in _blots.Values)
        {
            blot.enabled              = false;
            blot.color                = TintColor();
            blot.transform.localScale = Vector3.one;
        }

        _ink.enabled              = true;
        _ink.color                = Color.white;
        _ink.transform.localScale = Vector3.one;
        Place(_ink, CannonPos());

        _next.enabled              = true;
        _next.color                = Color.white;
        _next.transform.localScale = Vector3.one;
        Place(_next, NextPos());

        if (_hand != null)
        {
            _hand.enabled              = true;
            _hand.transform.localScale = Vector3.one;
            Place(_hand, HandAtCannon());
        }

        _aimTo = AimPointAt(StartAngle());
        ShowTrail(0f);
    }

    IEnumerator Aim()
    {
        Vector2 handFrom = HandAtCannon();
        float   from     = StartAngle();
        float   to       = TargetAngle();

        for (float t = 0f; t < aimTime; t += Time.unscaledDeltaTime)
        {
            float p = Smooth(t / aimTime);

            _aimTo = AimPointAt(Mathf.Lerp(from, to, p));
            if (_hand != null) Place(_hand, Vector2.Lerp(handFrom, HandOnAim(), p));

            ShowTrail(1f);
            yield return null;
        }

        _aimTo = CellPos(Landed);
        if (_hand != null) Place(_hand, HandOnAim());
        ShowTrail(1f);
    }

    // La pausa más larga de la demostración, y la que importa: acá es donde el jugador tiene que
    // atar la zona marcada con la burbuja pequeña del cañón. Laten juntas para decir que son lo
    // mismo.
    IEnumerator Preview()
    {
        for (float t = 0f; t < previewTime; t += Time.unscaledDeltaTime)
        {
            float pulse = 0.7f + 0.3f * Mathf.Sin(t / previewTime * Mathf.PI * 4f);


            _next.transform.localScale = Vector3.one * (1f + 0.14f * Mathf.Sin(t / previewTime * Mathf.PI * 4f));

            yield return null;
        }

        _next.transform.localScale = Vector3.one;
    }

    IEnumerator Release()
    {
        for (float t = 0f; t < releaseTime; t += Time.unscaledDeltaTime)
        {
            if (_hand != null)
                _hand.transform.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(t / releaseTime * Mathf.PI));
            yield return null;
        }

        if (_hand != null) _hand.enabled = false;
    }

    IEnumerator Fly()
    {
        Vector2 from = CannonPos();
        Vector2 to   = CellPos(Landed);

        for (float t = 0f; t < flyTime; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / flyTime);

            Place(_ink, Vector2.Lerp(from, to, p));
            ShowTrail(1f - p);
            yield return null;
        }

        Place(_ink, to);
        ShowTrail(0f);
    }

    // El impacto y la salpicadura.
    //
    // Primero revienta el racimo que tocó, como una burbuja normal — eso es lo que hace que se
    // lea como un disparo y no como algo que se queda pegado. Y recién después caen las manchas,
    // que al irse dejan las burbujas pintadas.
    IEnumerator Splash()
    {
        // La tinta se deshace en el sitio: lo que queda es su color, no ella.
        for (float t = 0f; t < burstTime; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / burstTime);

            _ink.color                = new Color(1f, 1f, 1f, 1f - p);
            _ink.transform.localScale = Vector3.one * (1f + 0.35f * p);
            yield return null;
        }

        _ink.enabled = false;

        // El racimo que tocó, reventando desde el impacto hacia afuera.
        float chainStep = _chain.Count > 1 ? burstTime / _chain.Count : 0f;

        for (int i = 0; i < _chain.Count; i++)
            StartCoroutine(Pop(_chain[i], i * chainStep));

        yield return new WaitForSecondsRealtime(burstTime);

        // Y las manchas, cada una sobre su burbuja. Quietas mientras se leen y después se van
        // dejando el color: el mismo gesto que en el juego.
        float step = _splashed.Count > 1 ? splashTime / (_splashed.Count - 1) : 0f;

        for (int i = 0; i < _splashed.Count; i++)
            StartCoroutine(Stain(_splashed[i], i * step));

        yield return new WaitForSecondsRealtime(splashTime + holdTime + fadeTime);

        // Y el color que repartió es el que ahora está listo para disparar.
        Vector2 from = NextPos();
        Vector2 to   = CannonPos();

        for (float t = 0f; t < fadeTime * 2f; t += Time.unscaledDeltaTime)
        {
            float p = Smooth(t / (fadeTime * 2f));

            Place(_next, Vector2.Lerp(from, to, p));
            _next.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1f / Mathf.Max(0.1f, nextSize), p);
            yield return null;
        }
    }

    // Una burbuja del racimo reventando: crece un poco y se va.
    IEnumerator Pop(Vector2Int cell, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        if (!_cluster.TryGetValue(cell, out var bubble) || bubble == null) yield break;

        for (float t = 0f; t < burstTime * 0.5f; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / (burstTime * 0.5f));

            bubble.transform.localScale = Vector3.one * (1f + 0.3f * p);
            bubble.color                = new Color(1f, 1f, 1f, 1f - p);
            yield return null;
        }

        bubble.enabled = false;
        _colors.Remove(cell);
    }

    // La mancha cayendo sobre una burbuja: aparece de golpe, se queda quieta mientras se lee, y
    // al irse deja el color. Mientras está encima no se mueve — es lo que la hace una mancha y no
    // algo que pasa por ahí.
    IEnumerator Stain(Vector2Int cell, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        if (!_blots.TryGetValue(cell, out var blot) || blot == null) yield break;

        blot.color   = TintColor();
        blot.enabled = true;
        blot.transform.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));

        // El golpe de llegada, corto: es un salpicón.
        for (float t = 0f; t < fadeTime * 0.5f; t += Time.unscaledDeltaTime)
        {
            blot.transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, Smooth(t / (fadeTime * 0.5f)));
            yield return null;
        }

        blot.transform.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(holdTime);

        // Se va, y la burbuja de debajo cambia a la vez: los dos gestos son el mismo.
        bool swapped = false;

        _cluster.TryGetValue(cell, out var bubble);

        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            float p    = Mathf.Clamp01(t / fadeTime);
            var   tint = TintColor();

            blot.color = new Color(tint.r, tint.g, tint.b, tint.a * (1f - p));

            if (bubble != null)
                bubble.transform.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(p * Mathf.PI));

            if (!swapped && p >= 0.5f && bubble != null)
            {
                swapped       = true;
                bubble.sprite = SpriteAt(RETURNING);
                _colors[cell] = RETURNING;
            }

            yield return null;
        }

        blot.enabled = false;
        if (bubble != null) bubble.transform.localScale = Vector3.one;
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
