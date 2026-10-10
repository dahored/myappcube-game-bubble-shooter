using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La mini-vista que enseña la Burbuja Madre dentro del TutorialPanel. Su entrada en el panel es
// 'mother', y el bucle es:
//
//   1. un racimo con dos burbujas SOLAS a los lados, que es el problema que el poder resuelve
//   2. la mano apunta al montón del medio
//   3. suelta, la Madre llega y revienta ese montón entero
//   4. y de ahí salen dos hijas, cada una a por una de las solas
//
// Lo que hay que enseñar no es que explote: es a QUÉ va. Por eso el racimo tiene dos burbujas
// sueltas puestas a propósito en los extremos — sin ellas el poder se vería como una bomba
// pequeña, que es justo lo que no es.
//
// Este objeto ES el tablero de la demostración: implementa IBoardView y le pasa su propio racimo
// a BoosterRules, así que a quién van las hijas lo decide el MISMO código que en una partida. Si
// mañana cambia el criterio, la demostración cambia con él en vez de quedar enseñando otra cosa.
public class TutorialMotherDemo : MonoBehaviour, IBoardView
{
    [Tooltip("Sprites de burbuja de colores DISTINTOS, tres o más. El tercero es el del montón y el de las dos solas: conviene que destaque sobre los otros dos.")]
    [SerializeField] Sprite[] bubbleSprites;

    [Tooltip("La mano que apunta. La misma que usan los demás tutoriales: conviene que sea la misma para que el gesto se reconozca de uno al otro.")]
    [SerializeField] Sprite handSprite;

    [Header("Proporciones")]
    [Tooltip("Proporción ancho/alto de la composición. Acota el área usada dentro del hueco para que la demostración no se estire en pantallas altas.")]
    [Range(0.4f, 1.6f)]
    [SerializeField] float aspect = 0.72f;

    [Tooltip("Color de la marca que señala dónde va a pegar. Solo se marca eso: a dónde van las hijas es una sorpresa también en el tutorial, igual que en el juego.")]
    [SerializeField] Color markColor = new(1f, 1f, 1f, 0.85f);

    [Tooltip("Cuántos puntos tiene la línea de la mira.")]
    [Range(4, 24)]
    [SerializeField] int dots = 10;

    [Tooltip("Tamaño de cada punto de la mira, en tanto por uno de la burbuja.")]
    [Range(0.1f, 0.6f)]
    [SerializeField] float dotSize = 0.2f;

    [SerializeField] Color dotColor = new(1f, 1f, 1f, 0.9f);

    [Tooltip("Cuánto se desvía la mira al empezar a apuntar, en grados. Es lo que convierte el gesto en APUNTAR en vez de en arrastrar la burbuja hasta su sitio.")]
    [Range(0f, 60f)]
    [SerializeField] float aimSweep = 30f;

    [Range(0.1f, 0.9f)]
    [SerializeField] float handDistance = 0.42f;

    [Header("Tiempos (segundos)")]
    [SerializeField] float holdTime    = 0.5f;
    [SerializeField] float aimTime     = 0.7f;
    [SerializeField] float previewTime = 0.7f;
    [SerializeField] float releaseTime = 0.15f;
    [SerializeField] float flyTime     = 0.4f;
    [SerializeField] float burstTime   = 0.35f;  // el montón del medio
    [SerializeField] float travelTime  = 0.75f;  // lo que tardan las hijas en llegar
    [SerializeField] float staggerTime = 0.18f;  // y lo que la segunda sale después de la primera
    [SerializeField] float restTime    = 0.9f;

    // El racimo, escrito a mano y no generado: hace falta que haya EXACTAMENTE dos burbujas solas
    // y que estén lejos, porque es lo único que la demostración tiene que enseñar. Un racimo al
    // azar algunas veces no tendría ninguna.
    //
    // El color 2 es el del montón y el de las dos solas. Las dos primeras filas van alternando
    // los otros dos, así que las solas no tienen a nadie de su color al lado — que es la medida
    // con la que el poder las elige.
    static readonly (int col, int row, int color)[] CLUSTER =
    {
        (1, 0, 0), (2, 0, 1), (3, 0, 0), (4, 0, 1), (5, 0, 0), (6, 0, 1), (7, 0, 0), (8, 0, 1), (9, 0, 0),
        (0, 1, 2),                                                                              (8, 1, 2),
        (3, 1, 2), (4, 1, 2), (5, 1, 2),
    };

    static Vector2Int Struck => new(4, 1);   // contra cuál choca: el medio del montón
    static Vector2Int Landed => new(4, 2);   // y dónde se posa, justo debajo

    readonly Dictionary<Vector2Int, Image> _cluster = new();
    readonly Dictionary<Vector2Int, int>   _colors  = new();
    readonly List<Image>                   _marks   = new();
    readonly List<Image>                   _trail   = new();
    readonly List<Image>                   _kids    = new();

    Vector2 _aimTo;

    RectTransform _area;
    Image         _mother;
    Image         _hand;
    Coroutine     _loop;
    float         _builtFor;

    // --- IBoardView: el racimo de la demostración, visto como lo ve un poder ---

    public IEnumerable<Vector2Int> Occupied => _colors.Keys;

    public bool TryGetColor(Vector2Int cell, out BubbleColor color)
    {
        // Los índices de sprite se hacen pasar por colores. Da igual cuáles sean mientras dos
        // iguales se cuenten como iguales: el poder solo compara.
        if (_colors.TryGetValue(cell, out int index)) { color = (BubbleColor)index; return true; }

        color = default;
        return false;
    }

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

    static Sprite MotherSprite => BoosterRules.BubbleSpriteFor(Booster.Mother);

    bool Ready()
    {
        if (bubbleSprites != null && bubbleSprites.Length >= 3 && MotherSprite != null) return true;

        Debug.LogWarning($"[TutorialMotherDemo] '{name}' necesita al menos tres sprites de burbuja de colores distintos, y que BoosterCatalog tenga el arte de la Burbuja Madre: sin eso no hay nada que mostrar.", this);
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
            Debug.LogWarning($"[TutorialMotherDemo] '{name}' quedó sin alto: el layout del panel no le dio sitio y no hay dónde dibujar la demostración.", this);
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
        _marks.Clear();
        _trail.Clear();
        _kids.Clear();

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
            var bubble = Spawn($"Bubble{col}_{row}", bubbleSprites[color % bubbleSprites.Length], size);

            Place(bubble, CellPos(cell));
            _cluster[cell] = bubble;
            _colors[cell]  = color;
        }

        // Solo se marca dónde va a pegar. A dónde van las hijas no se enseña ni aquí: es la misma
        // sorpresa que en el juego, y adelantarla en el tutorial le quita la gracia a la primera
        // vez que se dispara de verdad.
        var mark = Spawn("Mark", SparkleTextures.Ring(0.10f), size);
        mark.color   = markColor;
        mark.enabled = false;
        Place(mark, CellPos(Landed));
        _marks.Add(mark);

        _mother = Spawn("Mother", MotherSprite, size);

        // Las capas de verdad, no una imitación: las tres hijas giran dentro porque es el mismo
        // componente leyendo el mismo catálogo que la burbuja del tablero.
        SpecialBubbleSkin.Apply(_mother, Booster.Mother);
        _mother.color = new Color(1f, 1f, 1f, 0f); // el fondo lo dibuja la capa, no esta Image

        // Las dos que saldrán. Se crean ya, apagadas, para no instanciar a mitad de la animación.
        for (int i = 0; i < Targets().Count; i++)
        {
            var kid = Spawn($"Kid{i}", bubbleSprites[0], size * 0.55f);
            kid.enabled = false;
            _kids.Add(kid);
        }

        _hand = handSprite != null ? Spawn("Hand", handSprite, size * 1.6f) : null;
    }

    // Qué se lleva el poder, preguntado al MISMO sitio que en una partida. Las últimas de la
    // lista son las que se alcanzan a distancia, igual que allí.
    List<Vector2Int> Hit() => BoosterRules.CellsHitBy(Booster.Mother, Landed, Struck, this);

    List<Vector2Int> Targets()
    {
        var hit  = Hit();
        var far  = new List<Vector2Int>();
        int from = hit.Count - BoosterRules.TravelersOf(Booster.Mother);

        for (int i = Mathf.Max(0, from); i < hit.Count; i++) far.Add(hit[i]);

        return far;
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

    static void Discard(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else                       DestroyImmediate(go);
    }

    // --- Geometría, toda relativa al hueco que haya ---

    float Height() => Mathf.Min(_area.rect.height, _area.rect.width / Mathf.Max(0.1f, aspect));
    float Width()  => Mathf.Min(_area.rect.width, Height() * aspect);

    // Del ANCHO, como en el demo de la Raya: lo que esto enseña son dos burbujas en los extremos
    // opuestos, y un racimo que se saliera por los lados no enseñaría nada.
    float Size() => Width() / (HexGridMath.ColsInRow(0) + 0.5f);

    float Scale => Size() / HexGridMath.BubbleDiameter;

    Vector2 CellPos(Vector2Int cell) =>
        (HexGridMath.CellToLocalPos(cell) - HexGridMath.CellToLocalPos(Landed)) * Scale + LandedPos;

    Vector2 LandedPos   => new(0f, Height() * 0.06f);
    Vector2 CannonPos() => new(LandedPos.x, -Height() * 0.40f);

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
            yield return Burst();
            yield return SendKids();

            yield return new WaitForSecondsRealtime(restTime);
        }
    }

    void Reset()
    {
        foreach (var bubble in _cluster.Values)
        {
            bubble.enabled              = true;
            bubble.color                = Color.white;
            bubble.transform.localScale = Vector3.one;
        }

        foreach (var mark in _marks) { mark.enabled = false; mark.color = markColor; }

        // Las hijas vuelven ENTERAS, no solo apagadas: terminan el viaje desvanecidas, y
        // encenderlas sin devolverles el color las dejaba medio transparentes en la segunda
        // vuelta del bucle y en todas las siguientes (reportado por Diego).
        foreach (var kid in _kids)
        {
            kid.enabled              = false;
            kid.color                = Color.white;
            kid.transform.localScale = Vector3.one;
        }

        _mother.enabled              = true;
        _mother.transform.localScale = Vector3.one;
        SpecialBubbleSkin.SetAlpha(_mother, 1f);
        Place(_mother, CannonPos());

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
            foreach (var mark in _marks) mark.enabled = p > 0.5f;
            yield return null;
        }

        _aimTo = CellPos(Landed);
        if (_hand != null) Place(_hand, HandOnAim());
        ShowTrail(1f);
        foreach (var mark in _marks) mark.enabled = true;
    }

    IEnumerator Preview()
    {
        for (float t = 0f; t < previewTime; t += Time.unscaledDeltaTime)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(t / previewTime * Mathf.PI * 4f);

            foreach (var mark in _marks)
                mark.color = new Color(markColor.r, markColor.g, markColor.b, markColor.a * pulse);

            yield return null;
        }

        foreach (var mark in _marks) mark.color = markColor;
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

            Place(_mother, Vector2.Lerp(from, to, p));
            ShowTrail(1f - p);
            yield return null;
        }

        Place(_mother, to);
        ShowTrail(0f);
        foreach (var mark in _marks) mark.enabled = false;
    }

    // El montón del medio, que es lo que la Madre revienta al llegar. Las dos solas NO: esas son
    // de las hijas, y reventarlas aquí borraría la única diferencia entre este poder y una bomba.
    IEnumerator Burst()
    {
        var targets = Targets();
        var near    = new List<Graphic>();

        foreach (var cell in Hit())
            if (!targets.Contains(cell) && _cluster.TryGetValue(cell, out var bubble)) near.Add(bubble);

        for (float t = 0f; t < burstTime; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / burstTime);

            foreach (var bubble in near)
            {
                bubble.transform.localScale = Vector3.one * (1f + 0.4f * Mathf.Sin(p * Mathf.PI) - p * 0.5f);
                bubble.color = new Color(1f, 1f, 1f, 1f - p);
            }

            SpecialBubbleSkin.SetAlpha(_mother, 1f - Mathf.Clamp01(p * 2f));
            yield return null;
        }

        foreach (var bubble in near) bubble.enabled = false;

        _mother.enabled = false;
        SpecialBubbleSkin.SetAlpha(_mother, 0f);
    }

    // Y las dos hijas. Salen escalonadas y cada una del color de la que va a buscar, igual que en
    // el juego: es lo que explica a dónde va cada una sin una palabra.
    IEnumerator SendKids()
    {
        var targets = Targets();
        var from    = CellPos(Landed);

        for (int i = 0; i < targets.Count && i < _kids.Count; i++)
        {
            int color = _colors.TryGetValue(targets[i], out int c) ? c : 0;

            _kids[i].sprite  = bubbleSprites[color % bubbleSprites.Length];
            _kids[i].color   = Color.white;
            _kids[i].enabled = true;
            Place(_kids[i], from);
        }

        float total = travelTime + staggerTime * Mathf.Max(0, targets.Count - 1);

        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            for (int i = 0; i < targets.Count && i < _kids.Count; i++)
            {
                float p = Mathf.Clamp01((t - i * staggerTime) / travelTime);
                if (p <= 0f) continue;

                Place(_kids[i], Vector2.Lerp(from, CellPos(targets[i]), Smooth(p)));

                // La de destino se apaga en el último tramo del viaje, no antes: tiene que
                // apagarse PORQUE llegó la hija, no mientras viene de camino.
                if (p > 0.8f && _cluster.TryGetValue(targets[i], out var hitBubble))
                {
                    float fade = Mathf.InverseLerp(0.8f, 1f, p);

                    hitBubble.color                = new Color(1f, 1f, 1f, 1f - fade);
                    hitBubble.transform.localScale = Vector3.one * (1f + 0.4f * Mathf.Sin(fade * Mathf.PI) - fade * 0.5f);
                    _kids[i].color                 = new Color(1f, 1f, 1f, 1f - fade);
                }
            }

            yield return null;
        }

        foreach (var cell in targets)
            if (_cluster.TryGetValue(cell, out var bubble)) bubble.enabled = false;

        foreach (var kid in _kids) kid.enabled = false;
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
