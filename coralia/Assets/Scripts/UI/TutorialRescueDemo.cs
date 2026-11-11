using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La mini-vista que enseña el RESCATE dentro del TutorialPanel: una mano apunta, suelta, la
// burbuja sube al hueco y se lleva el trío — y con él sale lo que estaba atrapado.
//
// Cuenta UNA sola idea: hay algo dentro de una burbuja y se libera haciendo match con su color.
// Las capas NO se explican acá a propósito; son otra mecánica y tienen su propio tutorial.
//
// Con disparo y no con el racimo reventando solo: sin el gesto, lo que el jugador ve es un efecto
// que ocurre; con él, ve la jugada que tiene que hacer. Así lo enseñan todos los demás.
//
// Mismo criterio que el resto de demostraciones: el arte sale de los catálogos del juego
// —BubbleCatalog para los cuerpos, Rescuable para lo que va dentro— y la rejilla de HexGridMath,
// así que nada de lo que se ve acá puede separarse de lo que se ve jugando.
public class TutorialRescueDemo : MonoBehaviour
{
    [Tooltip("El color de la burbuja que lleva algo dentro, el de su compañera y el del disparo. Uno solo: lo que se enseña es que ESE color la libera.")]
    [SerializeField] BubbleColor matchColor = BubbleColor.Red;

    [Tooltip("Con qué se rellena el resto del tablero. Sirven de contraste: sin burbujas de otros colores alrededor, 'su color' no significa nada.\n\nVacío usa tres colores distintos del de rescate, así la demostración se ve bien sin configurar nada.")]
    [SerializeField] BubbleColor[] fillerColors;

    // El relleno por defecto. Va en código y no en el inicializador del campo porque Unity rellena
    // un campo nuevo de un asset ya serializado con el valor por defecto del TIPO —un array vacío—
    // y no con el del inicializador: puesto solo ahí arriba, la demostración saldría toda azul
    // hasta que alguien abriera el Inspector.
    static readonly BubbleColor[] DEFAULT_FILLERS =
        { BubbleColor.Blue, BubbleColor.Green, BubbleColor.Yellow };

    [Tooltip("Qué lleva dentro la burbuja. Vacío coge la primera criatura del catálogo, así la demostración funciona sin configurar nada.")]
    [RescuableId]
    [SerializeField] string holds;

    [Header("El disparo")]
    [SerializeField] TutorialShot shot = new();

    [Header("Proporciones")]
    [Range(0.05f, 0.3f)]
    [SerializeField] float bubbleSize = 0.11f;

    [Tooltip("Proporción ancho/alto de la composición. Acota el área usada dentro del hueco para que la demostración no se estire en pantallas altas.")]
    [Range(0.4f, 1.6f)]
    [SerializeField] float aspect = 0.72f;

    [Header("Tiempos (segundos)")]
    [SerializeField] float holdTime = 0.5f;   // el tablero quieto, antes de empezar
    [SerializeField] float popTime  = 0.45f;
    [SerializeField] float flyTime  = 0.75f;  // lo que tarda en subir lo rescatado
    [SerializeField] float restTime = 0.8f;

    // Celdas de verdad del tablero: las vecindades son las del juego, así que el trío que revienta
    // es un match legítimo y no tres burbujas colocadas a ojo.
    static readonly Vector2Int RESCUE = new(2, 1);   // la que lleva algo dentro
    static readonly Vector2Int MATE   = new(3, 1);   // la otra de su color
    static readonly Vector2Int HOLE   = new(2, 2);   // donde cae el disparo

    // El trío que se va: las dos de arriba más la que acaba de llegar. No se tocan entre ellas
    // —el grupo se cierra A TRAVÉS de la de rescate—, que es exactamente lo que hay que enseñar:
    // cuenta como una más de su color.
    static readonly Vector2Int[] GROUP = { HOLE, RESCUE, MATE };

    // El resto del tablero, de otros colores. Están para que el trío se lea como un match y no
    // como "las tres que había".
    static readonly Vector2Int[] FILLER =
        { new(1, 0), new(2, 0), new(3, 0), new(1, 1), new(1, 2), new(3, 2) };

    readonly Dictionary<Vector2Int, Image> _board = new();

    RectTransform _area;
    Image         _creature;      // lo que va dentro, hijo de la burbuja de rescate
    Coroutine     _loop;
    float         _builtFor;

    // Qué lleva dentro, resuelto. Vacío coge lo primero que haya en el catálogo de criaturas: una
    // demostración que no se ve por un campo sin rellenar es peor que una que elige sola.
    string Holds
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(holds)) return holds;

            var ids = Rescuable.CreatureIds;
            return ids != null && ids.Length > 0 ? ids[0] : "";
        }
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

    bool Ready()
    {
        if (Rescuable.Icon(Holds) == null)
        {
            Debug.LogWarning($"[TutorialRescueDemo] '{name}' no encuentra arte para '{Holds}': sin eso la burbuja sale vacía y no hay nada que enseñar.", this);
            return false;
        }

        if (!shot.Ready)
        {
            Debug.LogWarning($"[TutorialRescueDemo] '{name}' no tiene sprite de mano en 'Shot': sin él no se ve quién dispara.", this);
            return false;
        }

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
            Debug.LogWarning($"[TutorialRescueDemo] '{name}' quedó sin alto: el layout del panel no le dio sitio.", this);
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

        float size = Size();

        for (int i = 0; i < FILLER.Length; i++) Place(FILLER[i], Filler(i), size);

        Place(RESCUE, matchColor, size);
        Place(MATE,   matchColor, size);

        // Lo que lleva dentro va DENTRO de su burbuja y no suelto al lado: es hijo suyo, así que
        // la sigue sin que haya que moverlo en dos sitios — y al soltarse basta con cambiarle el
        // padre para que suba por su cuenta.
        _creature = Spawn("Creature", Rescuable.Icon(Holds), size * 0.62f);
        _creature.transform.SetParent(_board[RESCUE].transform, false);
        ((RectTransform)_creature.transform).anchoredPosition = Vector2.zero;

        // El disparo al final: en UGUI el orden de dibujo es el de la jerarquía, así que la
        // burbuja que viaja y la mano tienen que pasar por delante del tablero.
        shot.Build(_area, size);
    }

    BubbleColor Filler(int index)
    {
        var palette = fillerColors != null && fillerColors.Length > 0 ? fillerColors : DEFAULT_FILLERS;

        return palette[index % palette.Length];
    }

    void Place(Vector2Int cell, BubbleColor color, float size)
    {
        var bubble = BubbleGlow.AddTo(Spawn($"Bubble{cell.x}_{cell.y}", BubbleCatalog.Sprite(color), size));

        ((RectTransform)bubble.transform).anchoredPosition = CellPos(cell);

        _board[cell] = bubble;
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

    // Dónde cae el disparo dentro del área, y de dónde sale. El tablero cuelga del hueco, así que
    // la trayectoria es siempre recta y de abajo arriba: nada que cruzar por el camino.
    Vector2 TargetPos() => new(0f, Height() * 0.10f);
    Vector2 CannonPos() => new(0f, -Height() * 0.34f);

    // La MISMA fórmula del tablero, escalada y medida desde el hueco de destino. Derivada y no
    // copiada: el tejido hexagonal de la demostración es el del juego por construcción.
    Vector2 CellPos(Vector2Int cell) =>
        (HexGridMath.CellToLocalPos(cell) - HexGridMath.CellToLocalPos(HOLE)) * Scale + TargetPos();

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

            yield return shot.Aim(CannonPos(), TargetPos());
            yield return shot.Release();
            yield return shot.Fly(CannonPos(), TargetPos());

            yield return Pop();
            yield return Fly();

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

            bubble.gameObject.SetActive(true);
            bubble.color                = Color.white;
            bubble.transform.localScale = Vector3.one;

            ((RectTransform)bubble.transform).sizeDelta = Vector2.one * size;
        }

        shot.Arm(BubbleCatalog.Sprite(matchColor), CannonPos(), TargetPos());

        if (_creature == null) return;

        // Vuelve a su burbuja: la vuelta anterior lo dejó colgando del área para poder subir por
        // encima de todo.
        _creature.transform.SetParent(_board[RESCUE].transform, false);
        _creature.enabled              = true;
        _creature.color                = Color.white;
        _creature.transform.localScale = Vector3.one;

        var rt = (RectTransform)_creature.transform;
        rt.sizeDelta        = Vector2.one * size * 0.62f;
        rt.anchoredPosition = Vector2.zero;
    }

    // Revientan escalonadas, igual que en el tablero: todas en el mismo frame se leen como un
    // corte de la animación y no como una cadena. La que acaba de llegar se va con las otras dos.
    IEnumerator Pop()
    {
        float step = popTime * 0.35f / 2f;

        StartCoroutine(Burst(shot.Bubble, 0f, null));
        StartCoroutine(Burst(_board[RESCUE], step, RESCUE));
        StartCoroutine(Burst(_board[MATE], step * 2f, MATE));

        yield return new WaitForSecondsRealtime(popTime);
    }

    IEnumerator Burst(Image bubble, float delay, Vector2Int? cell)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        if (bubble == null) yield break;

        // Lo que llevaba dentro NO se va con el cuerpo: se desengancha y se queda donde estaba,
        // que es justo lo que hay que ver. Colgado todavía de la burbuja, se desvanecería con
        // ella y el rescate no se vería nunca.
        if (cell == RESCUE && _creature != null)
        {
            _creature.transform.SetParent(_area, false);
            ((RectTransform)_creature.transform).anchoredPosition = CellPos(RESCUE);
        }

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

        // El objeto entero: el brillo que le cuelga BubbleGlow no se apaga con el componente.
        bubble.gameObject.SetActive(false);
    }

    // Y sube y se va, que es lo que hace en la partida camino del hub. Sin esta última parte la
    // demostración solo enseña que la burbuja revienta, no que se RESCATA algo.
    IEnumerator Fly()
    {
        if (_creature == null) yield break;

        var   rt   = (RectTransform)_creature.transform;
        var   from = rt.anchoredPosition;
        var   to   = from + new Vector2(0f, Height() * 0.40f);
        float size = rt.sizeDelta.x;

        for (float t = 0f; t < flyTime; t += Time.unscaledDeltaTime)
        {
            float p = Smooth(Mathf.Clamp01(t / flyTime));

            rt.anchoredPosition = Vector2.Lerp(from, to, p);

            // Un respingo al salir y luego se aleja: sale de dentro de algo, no se desliza.
            rt.sizeDelta    = Vector2.one * size * (1f + 0.25f * Mathf.Sin(p * Mathf.PI) - 0.25f * p);
            _creature.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((p - 0.6f) / 0.4f));

            yield return null;
        }

        _creature.enabled = false;
    }

    static float Smooth(float p) => p * p * (3f - 2f * p);
}
