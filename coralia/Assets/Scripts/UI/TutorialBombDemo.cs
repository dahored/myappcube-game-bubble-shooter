using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La mini-vista que enseña la Bomba de Coral dentro del TutorialPanel. En bucle:
//
//   1. un racimo de burbujas de colores MEZCLADOS
//   2. la mano arrastra para apuntar
//   3. se marca el hexágono que va a volar, y ahí se hace una PAUSA
//   4. suelta, la bomba sube y estalla
//
// Los tres momentos son necesarios y ninguno sobra. Lo que hay que enseñar no es "explota" —eso
// ya se entiende solo— sino DÓNDE y CUÁNTO: por eso la zona se marca antes y se queda quieta un
// momento, para poder contarla con la vista antes de que desaparezca. Y las que vuelan son a
// propósito de colores distintos entre sí, con otras del mismo color justo al lado: un racimo
// de un solo color mostraría lo mismo que un match normal.
//
// Dibujada por código porque lo que hay que mantener en pie es una coreografía (la mano acaba
// donde cae la bomba, la bomba donde empieza la explosión, la explosión donde está la marca), no
// un dibujo. Las medidas salen del hueco que le toque, así sirve en cualquier panel.
//
// Quién vuela NO está escrito a mano: sale de HexGridMath.CellsWithinRadius, el mismo cálculo que
// usa el poder de verdad. Si el radio cambia, la demostración cambia con él en vez de quedar
// enseñando una forma que el juego ya no hace.
public class TutorialBombDemo : MonoBehaviour
{
    [Tooltip("Sprites de burbuja de colores DISTINTOS, tres o más. Lo que se enseña es que a la bomba el color le da igual, así que mientras más variados, mejor.")]
    [SerializeField] Sprite[] bubbleSprites;

    [Tooltip("La mano que apunta. La misma que usa TutorialShootDemo: conviene que sea la misma para que el gesto se reconozca de un tutorial al otro.")]
    [SerializeField] Sprite handSprite;

    [Header("Proporciones")]
    [Tooltip("Cuánto mide una burbuja, en tanto por uno del alto del hueco.")]
    [Range(0.05f, 0.3f)]
    [SerializeField] float bubbleSize = 0.1f;

    [Tooltip("Proporción ancho/alto de la composición. Acota el área usada dentro del hueco para que la demostración no se estire en pantallas altas. 0.72 es casi el formato de un móvil en vertical.")]
    [Range(0.4f, 1.6f)]
    [SerializeField] float aspect = 0.72f;

    [Tooltip("Color de la marca que señala qué va a volar.")]
    [SerializeField] Color markColor = new(1f, 1f, 1f, 0.85f);

    [Tooltip("Cuántos puntos tiene la línea de la mira.")]
    [Range(4, 24)]
    [SerializeField] int dots = 10;

    [Tooltip("Tamaño de cada punto de la mira, en tanto por uno de la burbuja.")]
    [Range(0.1f, 0.6f)]
    [SerializeField] float dotSize = 0.2f;

    [Tooltip("Color de los puntos. Blanco para los boosters: una fila de bombitas en miniatura se leería como si fueran a salir muchas.")]
    [SerializeField] Color dotColor = new(1f, 1f, 1f, 0.9f);

    [Tooltip("Cuánto se desvía la mira al empezar a apuntar, en grados. Es lo que convierte el gesto en APUNTAR: la línea entra torcida y se asienta sobre el objetivo. En cero, la mano va derecho del cañón al hueco y se lee como si arrastrara la bomba hasta ahí.")]
    [Range(0f, 60f)]
    [SerializeField] float aimSweep = 30f;

    [Tooltip("A qué altura de la línea se dibuja la mano, en tanto por uno de la distancia al objetivo.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] float handDistance = 0.42f;

    [Header("Tiempos (segundos)")]
    [SerializeField] float holdTime    = 0.5f;  // el racimo quieto, antes de empezar
    [SerializeField] float aimTime     = 0.7f;  // la mano va del cañón al hueco
    [SerializeField] float previewTime = 0.9f;  // la zona marcada, quieta — es LA pausa que hace legible el poder
    [SerializeField] float releaseTime = 0.15f; // el tirón al soltar
    [SerializeField] float flyTime     = 0.4f;  // la bomba sube
    [SerializeField] float blastTime   = 0.5f;  // la explosión se abre
    [SerializeField] float restTime    = 0.8f;  // y se espera antes de repetir

    // El racimo crece con el radio para que la zona entre entera por arriba. El hueco va en la
    // FILA DE ABAJO, que es donde se pega una burbuja de verdad: así la bomba sube por espacio
    // libre sin tener que abrirle un túnel, y la zona se recorta contra el borde del racimo
    // exactamente como se recorta al jugar.
    //
    // Filas PARES en total, para que la última sea impar. Es la condición para que el racimo se
    // vea centrado: todo cuelga del hueco, así que el hueco tiene que caer en el centro, y en un
    // tejido hexagonal eso solo pasa en una fila impar —que va corrida medio diámetro— cuando las
    // pares tienen un número PAR de columnas. Con 7 columnas no hay ninguna celda en el centro:
    // siempre sobra medio diámetro para un lado.
    int        Rows => Mathf.Max(2, (BoosterRules.BOMB_RADIUS + 3) / 2 * 2);
    Vector2Int Hole => new(ColsEven / 2 - 1, Rows - 1);

    // Las impares llevan una menos que las pares, igual que el tablero real (10 y 9), en chiquito.
    // Siempre PAR y nunca menos de 8: par por la simetría explicada arriba, y 8 porque con menos
    // el hexágono toca los bordes y deja de verse dónde acaba la zona, que es lo que hay que
    // mostrar. Sigue al radio para que una explosión más grande no se lleve el racimo entero.
    int ColsEven        => Mathf.Max(8, BoosterRules.BOMB_RADIUS * 2 + 4);
    int ColsIn(int row) => row % 2 == 0 ? ColsEven : ColsEven - 1;

    readonly Dictionary<Vector2Int, Image> _cluster = new();
    readonly List<Image>                   _marks   = new();
    readonly List<Image>                   _trail   = new();

    Vector2 _aimTo;   // hacia dónde apunta la mira ahora mismo

    RectTransform _area;
    Image         _bomb;
    Image         _hand;
    Image         _flash;
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

    static Sprite BombSprite => BoosterRules.BubbleSpriteFor(Booster.Bomb);

    bool Ready()
    {
        if (bubbleSprites != null && bubbleSprites.Length >= 3 && BombSprite != null) return true;

        Debug.LogWarning($"[TutorialBombDemo] '{name}' necesita al menos tres sprites de burbuja de colores distintos, y que BoosterCatalog tenga el arte de la bomba: sin eso no hay nada que mostrar.", this);
        return false;
    }

    // Mismo motivo que en TutorialShootDemo: dentro de un VerticalLayoutGroup el hueco todavía
    // no tiene tamaño cuando el objeto se activa, y medir ahí deja todo apilado en un punto.
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
            Debug.LogWarning($"[TutorialBombDemo] '{name}' quedó sin alto: el layout del panel no le dio sitio y no hay dónde dibujar la demostración.", this);
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

        float size = Size();

        // El destello primero, para que quede DETRÁS del racimo: en UGUI el orden de dibujo es el
        // orden en la jerarquía. Delante taparía justo lo que hay que ver desaparecer.
        _flash = Spawn("Flash", SparkleTextures.Glow, size * 3.4f);
        _flash.enabled = false;

        // La línea, también detrás del racimo. SparkleTextures.Glow es un disco blanco difuso:
        // es exactamente el punto que hace falta y no hay que asignar ningún sprite.
        for (int i = 0; i < dots; i++)
        {
            var dot = Spawn("Dot", SparkleTextures.Glow, size * dotSize);
            dot.color   = dotColor;
            dot.enabled = false;
            _trail.Add(dot);
        }

        // El color de cada burbuja va por posición y no al azar: al repetirse el bucle tiene que
        // verse el MISMO racimo, o parece que cambió el tablero en vez de haber explotado.
        for (int row = 0; row < Rows; row++)
        for (int col = 0; col < ColsIn(row); col++)
        {
            var cell = new Vector2Int(col, row);
            if (cell == Hole) continue;

            var bubble = Spawn($"Bubble{col}_{row}", bubbleSprites[(col + row * 2) % bubbleSprites.Length], size);
            Place(bubble, CellPos(cell));
            _cluster[cell] = bubble;
        }

        // Las marcas van DESPUÉS de las burbujas para dibujarse encima, y son el hexágono entero:
        // también sobre las celdas vacías. Marcando solo donde hay burbuja, la forma se vería
        // mordida y justo lo que hay que entender es que la zona es siempre la misma.
        foreach (var cell in BlastCells())
        {
            var mark = Spawn($"Mark{cell.x}_{cell.y}", SparkleTextures.Ring(0.10f), size);
            mark.color   = markColor;
            mark.enabled = false;
            Place(mark, CellPos(cell));
            _marks.Add(mark);
        }

        _bomb = Spawn("Bomb", BombSprite, size);

        // Las dos capas de verdad, no una imitación: la bomba de la demostración se mueve igual
        // que la del juego porque es el mismo componente leyendo el mismo catálogo. Enseñar un
        // arte que luego no coincide con el del tablero es enseñar mal.
        SpecialBubbleSkin.Apply(_bomb, Booster.Bomb);
        _bomb.color = new Color(1f, 1f, 1f, 0f); // el fondo lo dibuja la capa, no esta Image

        _hand = handSprite != null ? Spawn("Hand", handSprite, size * 1.6f) : null;
    }

    List<Vector2Int> BlastCells() => HexGridMath.CellsWithinRadius(Hole, BoosterRules.BOMB_RADIUS);

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

    // Del tablero real a la escala de la demostración.
    float Scale => Size() / HexGridMath.BubbleDiameter;

    // La MISMA fórmula del tablero, escalada y medida desde el hueco. DERIVADA y no copiada a
    // mano: así el tejido —el corrimiento de medio diámetro de las filas impares, el alto de
    // fila— es idéntico al del juego por construcción, y lo sigue siendo si HexGridMath cambia.
    //
    // Copiarlo era justamente el error anterior: centrar cada fila por su propio ancho Y además
    // aplicarle el medio diámetro es corregir dos veces, y las impares quedaban medio diámetro
    // corridas respecto del tablero de verdad.
    Vector2 CellPos(Vector2Int cell) =>
        (HexGridMath.CellToLocalPos(cell) - HexGridMath.CellToLocalPos(Hole)) * Scale + HolePos;

    // Dónde queda el hueco dentro del área. Todo el racimo cuelga de acá.
    Vector2 HolePos => new(0f, Height() * 0.08f);

    Vector2 CannonPos() => new(HolePos.x, -Height() * 0.40f);

    // --- La mira ---
    //
    // El dedo no ARRASTRA la bomba hasta el hueco: elige una DIRECCIÓN, y la línea sale del cañón
    // hacia ahí. Por eso entra desviada y se asienta sobre el objetivo, en vez de que la mano
    // viaje derecho del cañón al destino — eso último se lee como arrastrar, que no es el gesto.

    // Ángulo en grados hacia el hueco, 0 = recto hacia arriba.
    float TargetAngle()
    {
        Vector2 d = CellPos(Hole) - CannonPos();
        return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
    }

    float StartAngle() => TargetAngle() + aimSweep;

    // Hasta dónde llega la mira con ese ángulo: siempre la misma distancia, así la línea no se
    // estira ni se encoge mientras barre.
    Vector2 AimPointAt(float degrees)
    {
        float   rad = degrees * Mathf.Deg2Rad;
        Vector2 dir = new(Mathf.Sin(rad), Mathf.Cos(rad));

        return CannonPos() + dir * Vector2.Distance(CannonPos(), CellPos(Hole));
    }

    // La mano se dibuja abajo y a la derecha de donde toca, como un dedo real tapando lo menos
    // posible de lo que hay que mirar.
    Vector2 HandAtCannon() => CannonPos() + new Vector2(Size() * 0.45f, -Size() * 0.7f);

    Vector2 HandOnAim() =>
        Vector2.Lerp(CannonPos(), _aimTo, handDistance) + new Vector2(Size() * 0.6f, -Size() * 0.4f);

    // Cuántos puntos de la línea se ven, de 0 a 1. Van del cañón a _aimTo, o sea a donde apunte
    // la mira en este instante.
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
            // El panel puede cambiar de alto (otro idioma con un texto más largo lo estira), y
            // todas las medidas salen de ahí. Se rehace entre repeticiones, nunca a mitad de una.
            if (!Mathf.Approximately(_builtFor, _area.rect.height) && _area.rect.height > 1f) Build();

            Reset();
            yield return new WaitForSecondsRealtime(holdTime);

            yield return Aim();
            yield return Preview();
            yield return Release();
            yield return Fly();
            yield return Blast();

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

        foreach (var mark in _marks)
        {
            mark.enabled = false;
            mark.color   = markColor;
        }

        // La capa, no la Image: el aspecto de la bomba lo dibuja SpecialBubbleSkin, así que
        // devolverle el alfa a la Image de abajo no la haría reaparecer.
        _bomb.enabled               = true;
        _bomb.transform.localScale  = Vector3.one;
        SpecialBubbleSkin.SetAlpha(_bomb, 1f);
        Place(_bomb, CannonPos());

        if (_hand != null)
        {
            // La mano arranca SOBRE la bomba, no en un sitio cualquiera: el gesto empieza tocando
            // la burbuja, y si apareciera ya a medio camino no se vería de dónde sale.
            _hand.enabled              = true;
            _hand.transform.localScale = Vector3.one;
            Place(_hand, HandAtCannon());
        }

        _aimTo = AimPointAt(StartAngle());
        ShowTrail(0f);

        _flash.enabled              = false;
        _flash.transform.localScale = Vector3.one;
    }

    // La mano sube hasta el hueco. Las marcas van apareciendo con ella: así se ve que la zona
    // depende de a dónde se apunta, no que sea un adorno fijo del tablero.
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

            ShowTrail(1f);   // completa desde el primer frame, como en el juego: lo que cambia es hacia dónde apunta
            ShowMarks(p);    // la zona se va revelando con la mira: depende de a dónde se apunta
            yield return null;
        }

        _aimTo = CellPos(Hole);
        if (_hand != null) Place(_hand, HandOnAim());
        ShowTrail(1f);
        ShowMarks(1f);
    }

    // La pausa. El hexágono entero marcado y quieto, el tiempo suficiente para recorrerlo con la
    // vista antes de que desaparezca. Late despacio para que se lea como algo a punto de pasar y
    // no como una parte más del tablero.
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
        Vector2 to   = CellPos(Hole);

        for (float t = 0f; t < flyTime; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / flyTime);

            Place(_bomb, Vector2.Lerp(from, to, p));
            ShowTrail(1f - p);   // la línea se consume por detrás de la bomba
            yield return null;
        }

        Place(_bomb, to);
        ShowTrail(0f);
    }

    IEnumerator Blast()
    {
        // El mismo cálculo que el poder de verdad. Las celdas del disco que en este racimo están
        // vacías simplemente no están en el diccionario y no hacen nada.
        var caught = new List<Graphic>();
        foreach (var cell in BlastCells())
            if (_cluster.TryGetValue(cell, out var bubble)) caught.Add(bubble);

        _flash.enabled = true;

        for (float t = 0f; t < blastTime; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / blastTime);

            // El destello se abre y se apaga enseguida; las burbujas tardan todo el tramo.
            _flash.transform.localScale = Vector3.one * (0.3f + p * 1.5f);
            _flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - p * 2f) * 0.9f);

            // Las marcas se van con la explosión: ya cumplieron, y dejarlas encendidas sobre el
            // agujero las convertiría en un resto del tablero que nadie sabe qué significa.
            foreach (var mark in _marks)
                mark.color = new Color(markColor.r, markColor.g, markColor.b, markColor.a * (1f - p));

            SpecialBubbleSkin.SetAlpha(_bomb, 1f - Mathf.Clamp01(p * 3f));

            foreach (var bubble in caught)
            {
                bubble.transform.localScale = Vector3.one * (1f + 0.4f * Mathf.Sin(p * Mathf.PI) - p * 0.5f);
                bubble.color = new Color(1f, 1f, 1f, 1f - p);
            }

            yield return null;
        }

        foreach (var bubble in caught) bubble.enabled = false;
        foreach (var mark in _marks)   mark.enabled   = false;

        _flash.enabled = false;
        _bomb.enabled  = false;
        SpecialBubbleSkin.SetAlpha(_bomb, 0f);
    }

    // Cuántas marcas se ven, de 0 a 1 — crecen desde el centro hacia afuera, que es el orden en
    // que la explosión va a ocurrir.
    void ShowMarks(float progress)
    {
        int show = Mathf.RoundToInt(Mathf.Clamp01(progress) * _marks.Count);

        for (int i = 0; i < _marks.Count; i++) _marks[i].enabled = i < show;
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
