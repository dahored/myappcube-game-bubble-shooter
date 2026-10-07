using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La mini-vista que enseña la Perla Arcoíris dentro del TutorialPanel.
//
// La diferencia con TutorialShootDemo no es el color de la burbuja: es que acá hay DOS parejas de
// colores distintos y el disparo va alternando entre ellas. Un solo acierto no enseña "combina con
// cualquier color" —se lee como "combina con el rojo"—, y es el bucle del tutorial el que hace el
// trabajo: la misma perla estalla con una pareja, luego con la otra, y ahí se entiende.
//
// Dibujada por código por el mismo motivo que las otras: lo que hay que mantener en pie es una
// coreografía (la mano acaba donde acaba la línea, la línea donde aterriza la burbuja, la burbuja
// donde están las que estallan), no un dibujo. Compuesta a mano habría que realinearla cada vez
// que cambie el tamaño del panel.
public class TutorialRainbowDemo : MonoBehaviour
{
    [Tooltip("Sprites de burbuja de colores DISTINTOS, tres o más. Los dos primeros forman las dos parejas con las que estalla la perla; el tercero rellena y hace de contraste.")]
    [SerializeField] Sprite[] bubbleSprites;

    [Tooltip("La mano que apunta. La misma que usan los otros tutoriales, para que el gesto se reconozca.")]
    [SerializeField] Sprite handSprite;

    [Tooltip("El punto de la línea de trayectoria. Vacío usa la perla en chiquito.")]
    [SerializeField] Sprite dotSprite;

    [SerializeField] Color dotColor = new(1f, 1f, 1f, 0.85f);

    [Header("Proporciones")]
    [Range(0.05f, 0.3f)] [SerializeField] float bubbleSize   = 0.1f;

    [Tooltip("Proporción ancho/alto de la composición. Acota el área usada dentro del hueco para que la demostración no se estire en pantallas altas. 0.72 es casi el formato de un móvil en vertical.")]
    [Range(0.4f, 1.6f)]
    [SerializeField] float aspect = 0.72f;
    [Range(4, 24)]       [SerializeField] int   dots         = 10;
    [Range(0.1f, 0.6f)]  [SerializeField] float dotSize      = 0.2f;
    [Range(0f, 60f)]     [SerializeField] float aimSweep     = 30f;
    [Range(0.1f, 0.9f)]  [SerializeField] float handDistance = 0.42f;

    [Header("Tiempos (segundos)")]
    [SerializeField] float holdTime    = 0.5f;
    [SerializeField] float aimTime     = 0.7f;
    [SerializeField] float releaseTime = 0.15f;
    [SerializeField] float flyTime     = 0.45f;
    [SerializeField] float popTime     = 0.4f;
    [SerializeField] float restTime    = 0.7f;

    // Celdas de verdad del tablero, no posiciones inventadas. Las parejas están en (2,0)+(3,0) y
    // (4,0)+(5,0), y cada hueco de abajo toca exactamente a una de ellas: en una fila impar los
    // vecinos de arriba de (x,1) son (x,0) y (x+1,0).
    static readonly Vector2Int[] ROW_CELLS = { new(1, 0), new(2, 0), new(3, 0), new(4, 0), new(5, 0) };
    static readonly int[]        ROW       = { 2, 0, 0, 1, 1 };   // índices en bubbleSprites
    static readonly Vector2Int[] TARGETS   = { new(2, 1), new(4, 1) };
    static readonly int[][]      POPS      = { new[] { 1, 2 }, new[] { 3, 4 } };   // qué dos estallan con cada objetivo

    // El ancla de la geometría es el centro de la fila y no el objetivo, que acá cambia: si la
    // composición colgara del objetivo, el tablero entero saltaría de sitio entre repeticiones.
    static readonly Vector2Int ANCHOR = new(3, 0);

    readonly List<Image> _row   = new();
    readonly List<Image> _trail = new();

    RectTransform _area;
    Vector2       _aimTo;
    Image         _shot;
    Image         _hand;
    Coroutine     _loop;
    float         _builtFor;
    int           _pair;   // a cuál de las dos parejas le toca esta vuelta

    // El arte sale del catálogo, igual que en el juego: enseñar una perla que luego no coincide
    // con la del tablero es enseñar mal.
    static Sprite PearlSprite => BoosterRules.BubbleSpriteFor(Booster.Rainbow);

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
        if (bubbleSprites != null && bubbleSprites.Length >= 3 && PearlSprite != null && handSprite != null) return true;

        Debug.LogWarning($"[TutorialRainbowDemo] '{name}' necesita tres sprites de burbuja de colores distintos, el de la mano, y que BoosterCatalog tenga el arte de la perla: sin eso no hay nada que mostrar.", this);
        return false;
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
            Debug.LogWarning($"[TutorialRainbowDemo] '{name}' quedó sin alto: el layout del panel no le dio sitio.", this);
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

        // La línea primero, para que quede DETRÁS: en UGUI el orden de dibujo es el de la
        // jerarquía, no una propiedad que haya que acordarse de poner.
        for (int i = 0; i < dots; i++)
        {
            var dot = Spawn("Dot", dotSprite != null ? dotSprite : PearlSprite, size * dotSize);
            dot.color = dotColor;
            _trail.Add(dot);
        }

        for (int i = 0; i < ROW.Length; i++)
        {
            var bubble = Spawn($"Bubble{i}", bubbleSprites[ROW[i] % bubbleSprites.Length], size);
            Place(bubble, CellPos(ROW_CELLS[i]));
            _row.Add(bubble);
        }

        _shot = Spawn("Pearl", PearlSprite, size);

        // Las dos capas de verdad, no una imitación: la perla de la demostración gira y brilla
        // igual que la del tablero porque es el mismo componente leyendo el mismo catálogo.
        SpecialBubbleSkin.Apply(_shot, Booster.Rainbow);
        _shot.color = new Color(1f, 1f, 1f, 0f);   // el dibujo lo pone la capa, no esta Image

        _hand = Spawn("Hand", handSprite, size * 1.5f);
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

    // La MISMA fórmula del tablero, escalada y medida desde el ancla. Derivada y no copiada: el
    // tejido hexagonal de la demostración es el del juego por construcción.
    Vector2 CellPos(Vector2Int cell) =>
        (HexGridMath.CellToLocalPos(cell) - HexGridMath.CellToLocalPos(ANCHOR)) * Scale + AnchorPos();

    Vector2 AnchorPos() => new(0f, Height() * 0.14f);

    Vector2 TargetPos() => CellPos(TARGETS[_pair]);

    Vector2 CannonPos() => new(0f, -Height() * 0.34f);

    // --- La coreografía ---

    IEnumerator Loop()
    {
        while (true)
        {
            // El panel puede cambiar de alto (otro idioma con un texto más largo lo estira) y todas
            // las medidas salen de ahí. Se rehace entre repeticiones, nunca a mitad de una.
            if (!Mathf.Approximately(_builtFor, _area.rect.height) && _area.rect.height > 1f) Build();

            Reset();
            yield return new WaitForSecondsRealtime(holdTime);

            yield return Aim();
            yield return Release();
            yield return Fly();
            yield return Pop();

            yield return new WaitForSecondsRealtime(restTime);

            // Y a la otra pareja: es el bucle el que enseña que la perla sirve para cualquier color.
            _pair = (_pair + 1) % TARGETS.Length;
        }
    }

    void Reset()
    {
        for (int i = 0; i < _row.Count; i++)
        {
            _row[i].color = Color.white;
            _row[i].transform.localScale = Vector3.one;
            Place(_row[i], CellPos(ROW_CELLS[i]));
        }

        // La CAPA, no la Image: el dibujo de la perla lo pone SpecialBubbleSkin, así que la
        // Image de abajo se queda siempre transparente y lo que hay que devolver a opaco entre
        // repeticiones es la capa. Sin esto la perla vuelve al cañón invisible desde la segunda.
        _shot.enabled = true;
        _shot.color   = new Color(1f, 1f, 1f, 0f);
        SpecialBubbleSkin.SetAlpha(_shot, 1f);
        _shot.transform.localScale = Vector3.one;
        Place(_shot, CannonPos());

        // La mano arranca SOBRE la burbuja del cañón: el gesto empieza tocándola, y apareciendo
        // a medio camino no se vería de dónde sale.
        _hand.enabled = true;
        Place(_hand, HandAtCannon());
        _hand.transform.localScale = Vector3.one;

        _aimTo = AimPointAt(StartAngle());
        ShowTrail(0f);
    }

    // --- La mira ---
    //
    // El dedo no ARRASTRA la perla hasta el hueco: elige una DIRECCIÓN, y la línea sale del cañón
    // hacia ahí. Por eso entra desviada y se asienta sobre el objetivo — que la mano viajara
    // derecho del cañón al destino se lee como arrastrar, que es justo lo que el gesto no es.

    float TargetAngle()
    {
        Vector2 d = TargetPos() - CannonPos();
        return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
    }

    // El barrido entra siempre por el lado contrario al objetivo, así la mira cruza por encima del
    // tablero en vez de salirse por el borde cuando el objetivo ya está a un costado.
    float StartAngle() => TargetAngle() - Mathf.Sign(TargetAngle() == 0f ? 1f : TargetAngle()) * aimSweep;

    Vector2 AimPointAt(float degrees)
    {
        float   rad = degrees * Mathf.Deg2Rad;
        Vector2 dir = new(Mathf.Sin(rad), Mathf.Cos(rad));

        return CannonPos() + dir * Vector2.Distance(CannonPos(), TargetPos());
    }

    Vector2 HandAtCannon() => CannonPos() + new Vector2(Size() * 0.4f, -Size() * 0.6f);

    Vector2 HandOnAim() =>
        Vector2.Lerp(CannonPos(), _aimTo, handDistance) + new Vector2(Size() * 0.6f, -Size() * 0.35f);

    IEnumerator Aim()
    {
        Vector2 handFrom = HandAtCannon();
        float   from     = StartAngle();
        float   to       = TargetAngle();

        for (float t = 0f; t < aimTime; t += Time.unscaledDeltaTime)
        {
            float p = Smooth(t / aimTime);

            _aimTo = AimPointAt(Mathf.Lerp(from, to, p));
            Place(_hand, Vector2.Lerp(handFrom, HandOnAim(), p));
            ShowTrail(1f);   // completa desde el primer frame, como en el juego: cambia hacia dónde apunta, no cuánto mide
            yield return null;
        }

        _aimTo = TargetPos();
        Place(_hand, HandOnAim());
        ShowTrail(1f);
    }

    IEnumerator Release()
    {
        for (float t = 0f; t < releaseTime; t += Time.unscaledDeltaTime)
        {
            float p = t / releaseTime;

            _hand.transform.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }

        _hand.enabled = false;
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
        var popping = new List<Graphic> { _shot };
        foreach (int i in POPS[_pair]) popping.Add(_row[i]);

        for (float t = 0f; t < popTime; t += Time.unscaledDeltaTime)
        {
            float p = t / popTime;

            foreach (var g in popping)
            {
                g.transform.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(p * Mathf.PI) - p * 0.5f);

                // La perla se desvanece por su capa; las burbujas de color, por su Image.
                if (g == _shot) SpecialBubbleSkin.SetAlpha(_shot, 1f - p);
                else            g.color = new Color(1f, 1f, 1f, 1f - p);
            }

            yield return null;
        }

        foreach (var g in popping)
            if (g != _shot) g.color = new Color(1f, 1f, 1f, 0f);

        SpecialBubbleSkin.SetAlpha(_shot, 0f);
        _shot.enabled = false;
    }

    // Cuántos puntos de la línea se ven, de 0 a 1. Van del cañón a _aimTo, o sea a donde apunte la
    // mira en este instante — durante el vuelo eso ya es el objetivo, y la fracción sirve para que
    // la línea se consuma por detrás de la burbuja.
    void ShowTrail(float progress)
    {
        Vector2 from = CannonPos();
        int     show = Mathf.RoundToInt(Mathf.Clamp01(progress) * _trail.Count);

        for (int i = 0; i < _trail.Count; i++)
        {
            _trail[i].enabled = i < show;

            if (i < show) Place(_trail[i], Vector2.Lerp(from, _aimTo, (i + 1f) / _trail.Count));
        }
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
