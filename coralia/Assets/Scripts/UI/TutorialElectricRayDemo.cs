using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La mini-vista que enseña la Raya Eléctrica dentro del TutorialPanel. Su entrada en el panel es
// 'electric_ray' —el mismo id que usan su arte y su audio— y el bucle es:
//
//   1. tres filas de burbujas de colores mezclados
//   2. la mano apunta a una burbuja de la fila de ABAJO
//   3. se marca esa fila entera, y ahí se hace una PAUSA
//   4. suelta, la raya sube, se pega DEBAJO de la que tocó, y la descarga recorre la fila
//
// Lo que hay que enseñar no es "explota" sino CUÁL fila. Y ahí está la trampa que este tutorial
// existe para desarmar: la raya se posa una fila por debajo de la que toca, así que lo que vuela
// NO es la fila donde queda la burbuja. Mostrarlo con el racimo apoyado en el aire —con una fila
// vacía debajo— es la única forma de que eso se vea en lugar de leerse.
//
// Qué vuela sale de BoosterRules.CellsHitBy, el mismo cálculo que el poder de verdad. Si mañana
// la fila se resuelve de otra manera, la demostración cambia con ella.
//
// A diferencia de TutorialBombDemo, el tamaño de burbuja sale del ANCHO y no del alto: lo que
// esto enseña es una fila completa, y una que se saliera por los lados no enseñaría nada.
public class TutorialElectricRayDemo : MonoBehaviour
{
    [Tooltip("Sprites de burbuja de colores DISTINTOS, tres o más. Lo que se enseña es que a la raya el color le da igual, así que mientras más variados, mejor.")]
    [SerializeField] Sprite[] bubbleSprites;

    [Tooltip("La mano que apunta. La misma que usan los demás tutoriales: conviene que sea la misma para que el gesto se reconozca de uno al otro.")]
    [SerializeField] Sprite handSprite;

    [Header("Proporciones")]
    [Tooltip("Proporción ancho/alto de la composición. Acota el área usada dentro del hueco para que la demostración no se estire en pantallas altas.")]
    [Range(0.4f, 1.6f)]
    [SerializeField] float aspect = 0.72f;

    [Tooltip("Color de la marca que señala la fila que va a volar.")]
    [SerializeField] Color markColor = new(0.75f, 0.95f, 1f, 0.85f);

    [Tooltip("Color de la descarga que recorre la fila.")]
    [SerializeField] Color boltColor = new(0.8f, 0.95f, 1f, 1f);

    [Tooltip("Cuántos puntos tiene la línea de la mira.")]
    [Range(4, 24)]
    [SerializeField] int dots = 10;

    [Tooltip("Tamaño de cada punto de la mira, en tanto por uno de la burbuja.")]
    [Range(0.1f, 0.6f)]
    [SerializeField] float dotSize = 0.2f;

    [Tooltip("Color de los puntos.")]
    [SerializeField] Color dotColor = new(1f, 1f, 1f, 0.9f);

    [Tooltip("Cuánto se desvía la mira al empezar a apuntar, en grados. Es lo que convierte el gesto en APUNTAR en vez de en arrastrar la burbuja hasta su sitio.")]
    [Range(0f, 60f)]
    [SerializeField] float aimSweep = 30f;

    [Tooltip("A qué altura de la línea se dibuja la mano, en tanto por uno de la distancia al objetivo.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] float handDistance = 0.42f;

    [Header("Tiempos (segundos)")]
    [SerializeField] float holdTime    = 0.5f;   // el racimo quieto, antes de empezar
    [SerializeField] float aimTime     = 0.7f;   // la mano va del cañón a la burbuja
    [SerializeField] float previewTime = 0.9f;   // la fila marcada, quieta — LA pausa que hace legible el poder
    [SerializeField] float releaseTime = 0.15f;  // el tirón al soltar
    [SerializeField] float flyTime     = 0.4f;   // la raya sube
    [SerializeField] float sweepTime   = 0.7f;   // la descarga recorre la fila
    [SerializeField] float restTime    = 0.8f;   // y se espera antes de repetir

    // Tres filas pobladas y nada más: con el racimo colgando en el aire se ve que la fila de
    // abajo del todo está VACÍA, que es donde la raya se posa. Con el racimo apoyado contra el
    // borde no habría forma de notar la diferencia entre la fila que toca y la que ocupa.
    const int ROWS = 3;

    // Contra cuál choca: una de la fila más baja del racimo, descentrada a propósito. En el
    // centro exacto, la descarga saldría simétrica y parecería que siempre empieza en el medio.
    static Vector2Int Struck => new(HexGridMath.ColsInRow(ROWS - 1) / 2 - 1, ROWS - 1);

    // Y dónde acaba posada: justo debajo, que es lo que hace el disparo de verdad.
    static Vector2Int Landed => new(Struck.x, ROWS);

    readonly Dictionary<Vector2Int, Image> _cluster = new();
    readonly List<Image>                   _marks   = new();
    readonly List<Image>                   _trail   = new();
    readonly List<Image>                   _bolts   = new();

    Vector2 _aimTo;

    RectTransform _area;
    Image         _ray;
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

    static Sprite RaySprite => BoosterRules.BubbleSpriteFor(Booster.ElectricRay);

    bool Ready()
    {
        if (bubbleSprites != null && bubbleSprites.Length >= 3 && RaySprite != null) return true;

        Debug.LogWarning($"[TutorialElectricRayDemo] '{name}' necesita al menos tres sprites de burbuja de colores distintos, y que BoosterCatalog tenga el arte de la Raya Eléctrica: sin eso no hay nada que mostrar.", this);
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
            Debug.LogWarning($"[TutorialElectricRayDemo] '{name}' quedó sin alto: el layout del panel no le dio sitio y no hay dónde dibujar la demostración.", this);
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
        _marks.Clear();
        _trail.Clear();
        _bolts.Clear();

        float size = Size();

        for (int i = 0; i < dots; i++)
        {
            var dot = Spawn("Dot", SparkleTextures.Glow, size * dotSize);
            dot.color   = dotColor;
            dot.enabled = false;
            _trail.Add(dot);
        }

        // El color va por posición y no al azar: al repetirse el bucle tiene que verse el MISMO
        // racimo, o parece que cambió el tablero en vez de haber estallado.
        for (int row = 0; row < ROWS; row++)
        for (int col = 0; col < HexGridMath.ColsInRow(row); col++)
        {
            var cell   = new Vector2Int(col, row);
            var bubble = Spawn($"Bubble{col}_{row}", bubbleSprites[(col + row * 2) % bubbleSprites.Length], size);

            Place(bubble, CellPos(cell));
            _cluster[cell] = bubble;
        }

        // Las marcas, encima de las burbujas y sobre la fila ENTERA. En el orden que devuelve el
        // poder, que es desde el impacto hacia los dos lados: así se encienden como se va a
        // encender la descarga.
        foreach (var cell in HitCells())
        {
            var mark = Spawn($"Mark{cell.x}", SparkleTextures.Ring(0.10f), size);
            mark.color   = markColor;
            mark.enabled = false;
            Place(mark, CellPos(cell));
            _marks.Add(mark);
        }

        // Un arco por tramo de la fila, encadenados. Se crean aquí y se encienden en el barrido.
        var row2 = HitCells();
        for (int i = 0; i < row2.Count; i++)
        {
            var bolt = Spawn($"Bolt{i}", SparkleTextures.SparkArm(i), size * 1.2f);
            bolt.color          = boltColor;
            bolt.enabled        = false;
            bolt.preserveAspect = false;
            _bolts.Add(bolt);
        }

        _ray = Spawn("Ray", RaySprite, size);

        // Las capas de verdad, no una imitación: la raya de la demostración chisporrotea igual
        // que la del juego porque es el mismo componente leyendo el mismo catálogo.
        SpecialBubbleSkin.Apply(_ray, Booster.ElectricRay);
        _ray.color = new Color(1f, 1f, 1f, 0f); // el fondo lo dibuja la capa, no esta Image

        _hand = handSprite != null ? Spawn("Hand", handSprite, size * 1.6f) : null;
    }

    List<Vector2Int> HitCells() => BoosterRules.CellsHitBy(Booster.ElectricRay, Landed, Struck);

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

    // Del ANCHO, no del alto: lo que esto enseña es una fila entera, y el único tamaño que la
    // garantiza dentro del hueco es el que la hace caber a lo ancho. El medio diámetro de más es
    // el corrimiento de las filas impares, que si no se asoma por un lado.
    float Size() => Width() / (HexGridMath.ColsInRow(0) + 0.5f);

    float Scale => Size() / HexGridMath.BubbleDiameter;

    // La MISMA fórmula del tablero, escalada y medida desde donde acaba la raya. Derivada y no
    // copiada: el corrimiento de las filas impares es idéntico al del juego por construcción.
    Vector2 CellPos(Vector2Int cell) =>
        (HexGridMath.CellToLocalPos(cell) - HexGridMath.CellToLocalPos(Landed)) * Scale + LandedPos;

    Vector2 LandedPos  => new(0f, Height() * 0.06f);
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

    void ShowMarks(float progress)
    {
        int show = Mathf.RoundToInt(Mathf.Clamp01(progress) * _marks.Count);

        for (int i = 0; i < _marks.Count; i++) _marks[i].enabled = i < show;
    }

    // --- La coreografía ---

    IEnumerator Loop()
    {
        while (true)
        {
            // El panel puede cambiar de alto (otro idioma con un texto más largo lo estira), y
            // todas las medidas salen de ahí. Se rehace entre repeticiones, nunca a mitad de una.
            if (!Mathf.Approximately(_builtFor, _area.rect.height) && _area.rect.height > 1f) Build();

            Reset();
            yield return new WaitForSecondsRealtime(holdTime);

            yield return Aim();
            yield return Preview();
            yield return Release();
            yield return Fly();
            yield return Sweep();

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
        foreach (var bolt in _bolts)   bolt.enabled = false;

        _ray.enabled              = true;
        _ray.transform.localScale = Vector3.one;
        SpecialBubbleSkin.SetAlpha(_ray, 1f);
        Place(_ray, CannonPos());

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
            ShowMarks(p);   // la fila se va revelando con la mira: depende de a dónde se apunta
            yield return null;
        }

        _aimTo = CellPos(Landed);
        if (_hand != null) Place(_hand, HandOnAim());
        ShowTrail(1f);
        ShowMarks(1f);
    }

    // La pausa. La fila entera marcada y quieta, el tiempo suficiente para recorrerla con la
    // vista antes de que desaparezca.
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

            Place(_ray, Vector2.Lerp(from, to, p));
            ShowTrail(1f - p);   // la línea se consume por detrás de la burbuja
            yield return null;
        }

        Place(_ray, to);
        ShowTrail(0f);
    }

    // La descarga recorre la fila desde el impacto hacia los dos lados, y cada burbuja se apaga
    // cuando le llega. Escalonarlo es lo que enseña la DIRECCIÓN del poder: todas a la vez se
    // leerían como una explosión, que es lo que hace la bomba y justo lo que hay que distinguir.
    IEnumerator Sweep()
    {
        var cells = HitCells();

        for (float t = 0f; t < sweepTime; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / sweepTime);

            for (int i = 0; i < cells.Count; i++)
            {
                // Cada celda tiene su propio tramo del tiempo total, en el orden en que el poder
                // las devuelve. 'local' es cuánto ha avanzado LO SUYO.
                float start = i / (float)cells.Count * 0.7f;
                float local = Mathf.Clamp01((p - start) / 0.3f);

                if (i < _bolts.Count)
                {
                    var bolt = _bolts[i];
                    bolt.enabled = local > 0f && local < 1f;

                    if (bolt.enabled)
                    {
                        Place(bolt, CellPos(cells[i]));
                        bolt.color = new Color(boltColor.r, boltColor.g, boltColor.b,
                                               boltColor.a * Mathf.Sin(local * Mathf.PI));
                    }
                }

                if (!_cluster.TryGetValue(cells[i], out var bubble)) continue;

                bubble.transform.localScale = Vector3.one * (1f + 0.4f * Mathf.Sin(local * Mathf.PI) - local * 0.5f);
                bubble.color                = new Color(1f, 1f, 1f, 1f - local);
            }

            foreach (var mark in _marks)
                mark.color = new Color(markColor.r, markColor.g, markColor.b, markColor.a * (1f - p));

            SpecialBubbleSkin.SetAlpha(_ray, 1f - Mathf.Clamp01(p * 3f));

            yield return null;
        }

        foreach (var cell in cells)
            if (_cluster.TryGetValue(cell, out var bubble)) bubble.enabled = false;

        foreach (var mark in _marks) mark.enabled = false;
        foreach (var bolt in _bolts) bolt.enabled = false;

        _ray.enabled = false;
        SpecialBubbleSkin.SetAlpha(_ray, 0f);
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
