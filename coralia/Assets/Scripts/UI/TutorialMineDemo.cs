using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La mini-vista que enseña Plantar Bombas dentro del TutorialPanel.
//
// No hay mano ni cañón, y es la primera que no los tiene: este poder no se apunta ni se dispara,
// se aplica solo al abrir el nivel. Lo que hay que enseñar no es un gesto sino una REGLA, y la
// regla son tres momentos seguidos:
//
//   1. caen las luces y plantan las minas  — qué compraste
//   2. las minas parpadean y no pasa nada  — esperan, no estallan solas
//   3. algo revienta al lado y la cadena   — qué las enciende, y que una enciende a la siguiente
//
// El tercero es el que no se adivina y por eso es el que más dura. Que la segunda mina estalle
// por culpa de la primera cuenta la reacción en cadena sin una palabra.
//
// Este objeto ES el tablero de la demostración: implementa IBoardView y le pasa sus celdas a
// BoosterRules, así que hasta dónde llega cada estallido lo decide el MISMO código que en una
// partida — mover 'Mine Radius' en el catálogo mueve también lo que se enseña.
public class TutorialMineDemo : MonoBehaviour, IBoardView
{
    [Tooltip("Sprites de burbuja de colores DISTINTOS, dos o más. Son las que la mina se lleva por delante: con un solo color el estallido no se distingue de un match.")]
    [SerializeField] Sprite[] bubbleSprites;

    [Header("Proporciones")]
    [Range(0.05f, 0.3f)]
    [SerializeField] float bubbleSize = 0.1f;

    [Tooltip("Proporción ancho/alto de la composición. Acota el área usada dentro del hueco para que la demostración no se estire en pantallas altas.")]
    [Range(0.4f, 1.6f)]
    [SerializeField] float aspect = 0.72f;

    [Tooltip("Tamaño de la luz que viaja a plantar cada mina respecto a la burbuja.")]
    [Range(0.2f, 1.5f)]
    [SerializeField] float lightSize = 0.75f;

    [Header("Tiempos (segundos)")]
    [SerializeField] float holdTime  = 0.4f;
    [SerializeField] float plantTime = 0.45f;   // lo que tarda cada luz en llegar
    [SerializeField] float plantStep = 0.18f;   // y lo que se escalonan entre ellas
    [SerializeField] float waitTime  = 1.1f;    // parpadeando sin que pase nada: es la mitad de la idea
    [SerializeField] float popTime   = 0.35f;   // el match que la enciende
    [SerializeField] float blastTime = 0.5f;    // lo que tarda el estallido en abrirse
    [SerializeField] float chainTime = 0.3f;    // y lo que la segunda espera para encenderse
    [SerializeField] float restTime  = 1f;

    // Celdas de verdad del tablero, no posiciones inventadas: así la trama hexagonal y las
    // vecindades son las del juego, y lo que BoosterRules calcule encima vale.
    static readonly Vector2Int[] TOP    = { new(1, 0), new(2, 0), new(3, 0), new(4, 0), new(5, 0) };
    static readonly Vector2Int[] BOTTOM = { new(1, 1), new(2, 1), new(3, 1), new(4, 1) };

    // Las dos minas, a dos celdas una de otra: dentro del radio por defecto, que es lo que hace
    // que la segunda se encienda sola y la cadena se vea.
    static readonly Vector2Int[] MINES = { new(2, 1), new(4, 1) };

    // El racimo que revienta al lado de la primera. Mismo color los tres: lo que enciende la mina
    // es un match corriente del jugador, no otro poder.
    static readonly Vector2Int[] MATCH = { new(1, 0), new(2, 0), new(3, 0) };

    const int MATCH_COLOR = 0;   // índice en bubbleSprites

    // El ancla de la geometría. Fija y en el centro de la fila de arriba, para que el tablero no
    // salte de sitio entre repeticiones.
    static readonly Vector2Int ANCHOR = new(3, 0);

    readonly Dictionary<Vector2Int, Image> _board  = new();
    readonly List<Image>                   _lights = new();

    // El color de cada celda, en dos copias: '_base' es el tablero como se dibujó y '_colors' es
    // cómo está AHORA. Plantar una mina borra su entrada de '_colors' —desde ese momento no tiene
    // color, igual que en la partida— y la vuelta siguiente la repone desde '_base'.
    readonly Dictionary<Vector2Int, int> _base   = new();
    readonly Dictionary<Vector2Int, int> _colors = new();

    RectTransform _area;
    Coroutine     _loop;
    float         _builtFor;

    // Lo que cada mina se lleva, preguntado al MISMO sitio que en una partida.
    List<Vector2Int> _blastA = new();
    List<Vector2Int> _blastB = new();

    // El arte sale del catálogo, igual que en el juego: enseñar una mina que luego no coincide
    // con la del tablero es enseñar mal.
    static Sprite MineSprite => BoosterRules.BubbleSpriteFor(Booster.Mine);

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
        if (bubbleSprites != null && bubbleSprites.Length >= 2 && MineSprite != null) return true;

        Debug.LogWarning($"[TutorialMineDemo] '{name}' necesita al menos dos sprites de burbuja de colores distintos, y que BoosterCatalog tenga los 'Bubble Frames' de Plantar Bombas: sin eso no hay nada que mostrar.", this);
        return false;
    }

    // --- IBoardView: lo que BoosterRules necesita saber de este tablero -------------------

    public IEnumerable<Vector2Int> Occupied => _board.Keys;

    // Una mina no responde de qué color es, igual que en la partida: es lo que la deja fuera de
    // los grupos y de los recuentos sin un caso especial en ningún sitio.
    public bool TryGetColor(Vector2Int cell, out BubbleColor color)
    {
        color = default;

        if (!_colors.TryGetValue(cell, out int index)) return false;

        color = (BubbleColor)(index % System.Enum.GetValues(typeof(BubbleColor)).Length);
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
            Debug.LogWarning($"[TutorialMineDemo] '{name}' quedó sin alto: el layout del panel no le dio sitio.", this);
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
        _base.Clear();
        _colors.Clear();
        _lights.Clear();

        float size = Size();

        // El racimo que revienta va de un color y el resto alterna: lo que tiene que distinguirse
        // es el match (tres iguales) del estallido (lo que pille, del color que sea).
        int next = 1;

        foreach (var cell in TOP)    Place(cell, System.Array.IndexOf(MATCH, cell) >= 0 ? MATCH_COLOR : next++, size);
        foreach (var cell in BOTTOM) Place(cell, next++, size);

        // Las luces que plantan, una por mina. Creadas apagadas para no instanciar a mitad de la
        // animación, que es donde se nota.
        foreach (var _ in MINES)
        {
            var light = Spawn("Light", SparkleTextures.Beacon, size * lightSize);

            light.color   = BoosterRules.ArtFor(Booster.Mine)?.bubbleBeacon?.color ?? Color.red;
            light.enabled = false;
            _lights.Add(light);
        }

        // Y a quién se lleva cada una, con el radio del catálogo. Se calcula una vez acá: entre
        // repeticiones el tablero es el mismo, y preguntarlo en cada vuelta sería trabajo por
        // frame para una respuesta que no cambia.
        _blastA = OnBoard(BoosterRules.CellsHitBy(Booster.Mine, MINES[0], null, this), MINES[0]);
        _blastB = OnBoard(BoosterRules.CellsHitBy(Booster.Mine, MINES[1], null, this), MINES[1]);
    }

    // El radio devuelve diecinueve celdas y este tablero tiene nueve. Escalonando sobre las que
    // no existen, medio estallido serían esperas sin nada que reventar.
    //
    // Ordenadas de la mina hacia afuera: el índice ya sirve para escalonar y el estallido se abre
    // en vez de aparecer.
    List<Vector2Int> OnBoard(List<Vector2Int> cells, Vector2Int from)
    {
        var kept = cells.FindAll(_board.ContainsKey);

        kept.Sort((a, b) => Vector2.Distance(CellPos(from), CellPos(a))
                     .CompareTo(Vector2.Distance(CellPos(from), CellPos(b))));

        return kept;
    }

    void Place(Vector2Int cell, int color, float size)
    {
        var bubble = Spawn($"Bubble{cell.x}_{cell.y}", bubbleSprites[color % bubbleSprites.Length], size);

        ((RectTransform)bubble.transform).anchoredPosition = CellPos(cell);

        _board[cell]  = bubble;
        _base[cell]   = color;
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

    // Todas las medidas salen de un área de PROPORCIÓN FIJA centrada en el hueco, no del hueco
    // entero: en una pantalla alta la composición se repartiría por todo el alto y la acción
    // quedaría perdida en el medio con los bordes vacíos.
    float Height() => Mathf.Min(_area.rect.height, _area.rect.width / Mathf.Max(0.1f, aspect));
    float Width()  => Mathf.Min(_area.rect.width, Height() * aspect);
    float Size()   => Height() * bubbleSize;

    float Scale => Size() / HexGridMath.BubbleDiameter;

    // La MISMA fórmula del tablero, escalada y medida desde el ancla. Derivada y no copiada: el
    // tejido hexagonal de la demostración es el del juego por construcción.
    Vector2 CellPos(Vector2Int cell) =>
        (HexGridMath.CellToLocalPos(cell) - HexGridMath.CellToLocalPos(ANCHOR)) * Scale + new Vector2(0f, Height() * 0.1f);

    // De dónde salen las luces: del centro de la composición, igual que en el juego salen del
    // centro de la pantalla porque es donde se apaga el icono del poder.
    Vector2 Origin() => new(0f, -Height() * 0.34f);

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

            yield return Plant();

            // El rato en que no pasa nada es parte de lo que se enseña: una mina plantada ESPERA.
            // Sin esta pausa, plantar y estallar se leerían como un solo efecto automático.
            yield return new WaitForSecondsRealtime(waitTime);

            yield return Pop();
            yield return Blast();

            yield return new WaitForSecondsRealtime(restTime);
        }
    }

    void Reset()
    {
        float size = Size();

        foreach (var pair in _board)
        {
            var bubble = pair.Value;
            if (bubble == null) continue;

            // Las minas vuelven a ser burbujas de color: la vuelta siguiente las planta otra vez,
            // que es justo lo que hay que ver.
            SpecialBubbleSkin.Clear(bubble);

            // Y el alfa de las capas vuelve a uno. 'Clear' las apaga pero no las repone, y el
            // estallido de la vuelta anterior las dejó desvanecidas: sin esto, la segunda vez que
            // el tutorial planta una mina sale translúcida y la tercera no se ve.
            SpecialBubbleSkin.SetAlpha(bubble, 1f);

            _colors[pair.Key] = _base[pair.Key];

            bubble.sprite               = bubbleSprites[_base[pair.Key] % bubbleSprites.Length];
            bubble.enabled              = true;
            bubble.color                = Color.white;
            bubble.transform.localScale = Vector3.one;

            ((RectTransform)bubble.transform).sizeDelta = Vector2.one * size;
        }

        foreach (var light in _lights)
        {
            if (light == null) continue;

            light.enabled              = false;
            light.transform.localScale = Vector3.one;
            ((RectTransform)light.transform).anchoredPosition = Origin();
        }
    }

    // Las luces salen del centro y cada una convierte su burbuja AL LLEGAR: el mismo reloj que
    // usa la siembra de verdad, así lo que se ve y lo que pasa no se separan nunca.
    IEnumerator Plant()
    {
        for (int i = 0; i < MINES.Length; i++) StartCoroutine(SendLight(i, i * plantStep));

        yield return new WaitForSecondsRealtime((MINES.Length - 1) * plantStep + plantTime);
    }

    IEnumerator SendLight(int index, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        var light = _lights[index];
        if (light == null) yield break;

        var rt   = (RectTransform)light.transform;
        var from = Origin();
        var to   = CellPos(MINES[index]);

        light.enabled = true;

        for (float t = 0f; t < plantTime; t += Time.unscaledDeltaTime)
        {
            float p = Smooth(Mathf.Clamp01(t / plantTime));

            rt.anchoredPosition  = Vector2.Lerp(from, to, p);
            rt.localScale        = Vector3.one * Mathf.Lerp(1f, 0.4f, p);
            yield return null;
        }

        light.enabled = false;

        // Y acá deja de ser una burbuja de color. Las capas de verdad, no una imitación: la mina
        // de la demostración late igual que la del tablero porque es el mismo componente leyendo
        // el mismo catálogo.
        if (_board.TryGetValue(MINES[index], out var bubble) && bubble != null)
        {
            _colors.Remove(MINES[index]);   // desde ahora no tiene color, igual que en la partida

            // Las capas de verdad, no una imitación: la mina de la demostración late igual que la
            // del tablero porque es el mismo componente leyendo el mismo catálogo. El dibujo lo
            // pone la capa 'SpecialBack', así que esta Image se aparta — pintando las dos, la
            // esfera saldría duplicada y el destello se vería a través de la de abajo.
            SpecialBubbleSkin.Apply(bubble, Booster.Mine);
            SpecialBubbleSkin.SetAlpha(bubble, 1f);

            bubble.color = new Color(1f, 1f, 1f, 0f);

            yield return Pulse(bubble, 0.18f, 1.25f);
        }
    }

    // El match que la enciende: tres del mismo color reventando pegadas a la primera mina.
    IEnumerator Pop()
    {
        foreach (var cell in MATCH) StartCoroutine(Vanish(cell, 0f));

        yield return new WaitForSecondsRealtime(popTime);
    }

    // Y la cadena. La segunda no se dispara por una lista escrita a mano: se enciende porque la
    // primera la alcanzó, igual que en la partida — si se cambia el radio en el catálogo y deja
    // de alcanzarla, acá deja de encadenarse y el tutorial sigue contando la verdad.
    IEnumerator Blast()
    {
        yield return Fire(MINES[0], _blastA);

        if (!_blastA.Contains(MINES[1])) yield break;

        yield return new WaitForSecondsRealtime(chainTime);
        yield return Fire(MINES[1], _blastB);
    }

    IEnumerator Fire(Vector2Int mine, List<Vector2Int> reach)
    {
        // El fogonazo antes que nada: es lo que dice QUIÉN causó lo que viene detrás.
        if (_board.TryGetValue(mine, out var armed) && armed != null)
            yield return Pulse(armed, 0.12f, 1.5f);

        // De la mina hacia afuera, escalonado por distancia: el estallido se abre, no aparece.
        float step = reach.Count > 1 ? blastTime / reach.Count : 0f;

        for (int i = 0; i < reach.Count; i++)
        {
            // Otra mina dentro del radio NO revienta como una burbuja más: se queda para estallar
            // ella, que es de lo que va la cadena. Misma regla que en la partida.
            if (reach[i] != mine && System.Array.IndexOf(MINES, reach[i]) >= 0) continue;

            StartCoroutine(Vanish(reach[i], i * step));
        }

        yield return new WaitForSecondsRealtime(blastTime);
    }

    IEnumerator Vanish(Vector2Int cell, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        if (!_board.TryGetValue(cell, out var bubble) || bubble == null || !bubble.enabled) yield break;

        var   rt   = (RectTransform)bubble.transform;
        float size = rt.sizeDelta.x;

        // Se parte del alfa que TENGA, no de uno. Una mina tiene esta Image apartada en cero —la
        // dibuja su capa— y desvanecerla desde uno la haría reaparecer de golpe con su color
        // viejo justo cuando está estallando.
        float from = bubble.color.a;

        // Crece y se deshace, igual que el pop del tablero: encogerse a secas se lee como que se
        // va hacia atrás, no como que revienta.
        for (float t = 0f; t < popTime; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / popTime);

            rt.sizeDelta = Vector2.one * size * (1f + 0.3f * p);
            SpecialBubbleSkin.SetAlpha(bubble, 1f - p);
            bubble.color = new Color(1f, 1f, 1f, from * (1f - p));
            yield return null;
        }

        bubble.enabled = false;
        SpecialBubbleSkin.Clear(bubble);
    }

    // Un golpe de escala y vuelve. Lo usan la mina recién plantada y la que se enciende: es el
    // mismo gesto porque son el mismo momento visto dos veces —"acá hay una"— y repetirlo es lo
    // que ata el estallido a la luz que llevaba un rato parpadeando.
    IEnumerator Pulse(Image target, float time, float peak)
    {
        var rt = (RectTransform)target.transform;

        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / time);

            rt.localScale = Vector3.one * (1f + (peak - 1f) * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }

        rt.localScale = Vector3.one;
    }

    static float Smooth(float p) => p * p * (3f - 2f * p);
}
