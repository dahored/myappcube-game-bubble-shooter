using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La mini-vista que enseña el CAPARAZÓN dentro del TutorialPanel.
//
// Sirve para las dos entradas que lo necesitan —el obstáculo suelto y el rescate acorazado— con
// un solo interruptor: 'Holds Something'. Es el mismo componente porque la regla que cuentan es
// la misma y lo único que cambia es lo que queda al final; dos scripts serían dos sitios donde la
// explicación se puede separar de sí misma.
//
// Las tres cosas que hay que contar, y ninguna es obvia:
//
//   1. Mientras esté tapada, CUALQUIER match al lado le quita una capa, sea del color que sea.
//      Su color está debajo del caparazón, así que pedirle al jugador que lo matchee sería
//      pedirle que lo adivine.
//   2. Romper la última capa NO se la lleva: solo la destapa. Hace falta un golpe más.
//   3. Destapada vuelve a ser una burbuja normal — recupera su brillo y su color cuenta otra vez.
//
// El bucle las enseña en ese orden, y por eso los dos primeros disparos son de OTRO color y el
// tercero del suyo: la diferencia entre ellos es la lección. Los tres caen en el MISMO hueco, que
// está justo encima del cañón — así la trayectoria es recta y no cruza por delante de nada.
public class TutorialShellDemo : MonoBehaviour
{
    [Tooltip("Apagado explica el OBSTÁCULO suelto: debajo del caparazón hay una burbuja de color y nada más.\n\nEncendido explica el RESCATE ACORAZADO: debajo hay algo atrapado, y al final sale.")]
    [SerializeField] bool holdsSomething;

    [Tooltip("De qué es el caparazón. Las capas salen del ObstacleCatalog, así que cambiar el arte del hielo lo cambia también acá.")]
    [ObstacleId]
    [SerializeField] string material = "ice";

    [Tooltip("El color que hay DEBAJO del caparazón, y el del disparo que se la lleva al final.")]
    [SerializeField] BubbleColor shellColor = BubbleColor.Red;

    [Tooltip("El color de los dos disparos que le rompen las capas. Tiene que ser OTRO: que un match ajeno le quite una capa es justo lo que hay que enseñar.")]
    [SerializeField] BubbleColor knockColor = BubbleColor.Blue;

    [Tooltip("Con qué se rellena el techo. Solo contraste.")]
    [SerializeField] BubbleColor fillerColor = BubbleColor.Yellow;

    [Tooltip("Qué lleva dentro, cuando lleva algo. Vacío coge la primera criatura del catálogo, así la demostración funciona sin configurar nada.")]
    [RescuableId]
    [SerializeField] string holds;

    [Header("El disparo")]
    [SerializeField] TutorialShot shot = new();

    [Header("Proporciones")]
    [Range(0.05f, 0.3f)]
    [SerializeField] float bubbleSize = 0.1f;

    [Tooltip("Proporción ancho/alto de la composición. Acota el área usada dentro del hueco para que la demostración no se estire en pantallas altas.")]
    [Range(0.4f, 1.6f)]
    [SerializeField] float aspect = 0.72f;

    [Header("Tiempos (segundos)")]
    [SerializeField] float holdTime    = 0.5f;
    [SerializeField] float popTime     = 0.35f;
    [SerializeField] float shatterTime = 0.3f;   // lo que tarda una capa en saltar
    [SerializeField] float revealTime  = 0.7f;   // la pausa con la burbuja ya destapada
    [SerializeField] float flyTime     = 0.7f;
    [SerializeField] float restTime    = 0.8f;

    // Celdas de verdad del tablero: las vecindades son las del juego, así que los tres tríos son
    // matches legítimos y no burbujas colocadas a ojo.
    static readonly Vector2Int SHELLED = new(2, 1);
    static readonly Vector2Int MATE    = new(3, 1);   // la otra de su color
    static readonly Vector2Int HOLE    = new(2, 2);   // donde cae cada disparo

    // Los dos pares que le rompen las capas, de OTRO color. Cada par se cierra A TRAVÉS del hueco
    // —no se tocan entre ellos— y los dos quedan pegados a la acorazada, que es lo que hace que el
    // golpe la alcance.
    static readonly Vector2Int[][] KNOCKS =
    {
        new[] { new Vector2Int(1, 1), new Vector2Int(1, 2) },
        new[] { new Vector2Int(3, 2), new Vector2Int(3, 3) },
    };

    static readonly Vector2Int[] FILLER = { new(1, 0), new(2, 0), new(3, 0) };

    readonly Dictionary<Vector2Int, Image> _board = new();
    readonly List<Image>                   _shell = new();   // de dentro hacia afuera

    RectTransform _area;
    Image         _body;        // la acorazada, para apagarla y encenderla
    Image         _creature;
    Coroutine     _loop;
    float         _builtFor;
    int           _left;        // capas que le quedan en esta vuelta

    string Holds
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(holds)) return holds;

            var ids = Rescuable.CreatureIds;
            return ids != null && ids.Length > 0 ? ids[0] : "";
        }
    }

    // Tantas capas como disparos tenga la coreografía: con más, el bucle acabaría con caparazón
    // puesto y la lección quedaría a medias.
    int Layers => KNOCKS.Length;

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
        if (ObstacleCatalog.Layers(material, Layers).Length == 0)
        {
            Debug.LogWarning($"[TutorialShellDemo] '{name}' no encuentra capas de '{material}': sin caparazón no hay nada que enseñar.", this);
            return false;
        }

        if (holdsSomething && Rescuable.Icon(Holds) == null)
        {
            Debug.LogWarning($"[TutorialShellDemo] '{name}' no encuentra arte para '{Holds}': la burbuja saldría vacía.", this);
            return false;
        }

        if (!shot.Ready)
        {
            Debug.LogWarning($"[TutorialShellDemo] '{name}' no tiene sprite de mano en 'Shot': sin él no se ve quién dispara.", this);
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
            Debug.LogWarning($"[TutorialShellDemo] '{name}' quedó sin alto: el layout del panel no le dio sitio.", this);
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
        _shell.Clear();

        float size = Size();

        foreach (var cell in FILLER) Place(cell, fillerColor, size);

        foreach (var knock in KNOCKS)
            foreach (var cell in knock)
                Place(cell, knockColor, size);

        Place(MATE,    shellColor, size);
        Place(SHELLED, shellColor, size);

        _body = _board[SHELLED];

        // Lo que lleva dentro va DENTRO de la burbuja y DEBAJO del caparazón, que es el orden en
        // que lo monta el juego: primero el cuerpo, luego lo rescatado, y las capas encima.
        if (holdsSomething)
        {
            _creature = Spawn("Creature", Rescuable.Icon(Holds), size * 0.62f);
            _creature.transform.SetParent(_body.transform, false);
            ((RectTransform)_creature.transform).anchoredPosition = Vector2.zero;
        }

        // Las capas, de dentro hacia afuera: la última de la lista queda encima de todas, que es
        // la primera que se rompe. Mismo orden que ObstacleCatalog.Layers y que BubbleView.
        foreach (var sprite in ObstacleCatalog.Layers(material, Layers))
        {
            var layer = Spawn("Shell", sprite, size);
            layer.transform.SetParent(_body.transform, false);
            ((RectTransform)layer.transform).anchoredPosition = Vector2.zero;
            _shell.Add(layer);
        }

        // El disparo al final: en UGUI el orden de dibujo es el de la jerarquía, así que la
        // burbuja que viaja y la mano tienen que pasar por delante del tablero.
        shot.Build(_area, size);
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

    Vector2 TargetPos() => new(0f, Height() * 0.08f);
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

            // Un disparo por capa, todos del color EQUIVOCADO. Que le quiten una igual es la
            // primera regla, y repetirlo es la segunda: se cuentan.
            foreach (var knock in KNOCKS)
            {
                yield return Shoot(knockColor);
                yield return Pop(knock);
                yield return Shatter();
            }

            // Destapada. La pausa es parte de la explicación: el cambio de apagado a encendido es
            // lo que avisa de que ya se puede matchear, y sin un momento quieto no se ve.
            yield return new WaitForSecondsRealtime(revealTime);

            // Y ahora sí, el disparo de SU color. El trío solo existe a través de ella, destapada.
            yield return Shoot(shellColor);
            yield return Pop(new[] { SHELLED, MATE });
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
            bubble.transform.localScale = Vector3.one;
            bubble.color                = Color.white;

            ((RectTransform)bubble.transform).sizeDelta = Vector2.one * size;
        }

        _left = _shell.Count;

        foreach (var layer in _shell)
        {
            if (layer == null) continue;

            layer.enabled              = true;
            layer.color                = Color.white;
            layer.transform.localScale = Vector3.one;
            ((RectTransform)layer.transform).sizeDelta = Vector2.one * size;
        }

        shot.Hide();

        if (_creature == null) { Shade(BubbleView.SHELL_SHADE); return; }

        _creature.transform.SetParent(_body.transform, false);
        _creature.enabled              = true;
        _creature.transform.localScale = Vector3.one;

        // El COLOR también, no solo el enabled: Fly() lo deja en alfa 0 al salir volando, y
        // Shade() de abajo respeta el alfa que encuentre. Sin esto la criatura volvía a su sitio
        // en la segunda vuelta del bucle pero transparente, o sea que desaparecía (reportado por
        // Diego).
        _creature.color = Color.white;

        var rt = (RectTransform)_creature.transform;
        rt.sizeDelta        = Vector2.one * size * 0.62f;
        rt.anchoredPosition = Vector2.zero;

        // Y al final, con todo ya en su sitio y a pleno color: apagarlos antes dejaría a Shade
        // trabajando sobre valores que el resto del Reset va a pisar.
        Shade(BubbleView.SHELL_SHADE);
    }

    // El cuerpo —y lo que lleve dentro— apagado mientras quede caparazón. Es la misma señal que
    // da el tablero: apagado significa "está ahí pero todavía no cuenta".
    void Shade(float lit)
    {
        if (_body != null) _body.color = new Color(lit, lit, lit, _body.color.a);

        if (_creature != null) _creature.color = new Color(lit, lit, lit, _creature.color.a);
    }

    IEnumerator Shoot(BubbleColor color)
    {
        shot.Arm(BubbleCatalog.Sprite(color), CannonPos(), TargetPos());

        yield return shot.Aim(CannonPos(), TargetPos());
        yield return shot.Release();
        yield return shot.Fly(CannonPos(), TargetPos());
    }

    // Revienta el grupo MÁS la que acaba de llegar: el disparo se va con ellas, no se queda
    // pegado. Escalonadas, igual que en el tablero.
    IEnumerator Pop(Vector2Int[] group)
    {
        float step = popTime * 0.3f / Mathf.Max(1, group.Length);

        StartCoroutine(Burst(shot.Bubble, 0f, null));

        for (int i = 0; i < group.Length; i++)
            StartCoroutine(Burst(_board[group[i]], (i + 1) * step, group[i]));

        yield return new WaitForSecondsRealtime(popTime);
    }

    IEnumerator Burst(Image bubble, float delay, Vector2Int? cell)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        if (bubble == null) yield break;

        // Lo que llevaba dentro NO se va con el cuerpo: se desengancha y se queda donde estaba,
        // que es justo lo que hay que ver.
        if (cell == SHELLED && _creature != null)
        {
            _creature.transform.SetParent(_area, false);
            ((RectTransform)_creature.transform).anchoredPosition = CellPos(SHELLED);
            _creature.color = Color.white;
        }

        var   rt   = (RectTransform)bubble.transform;
        float size = rt.sizeDelta.x;

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

    // Salta la capa de FUERA, que es la de índice más alto porque es la que se dibuja encima.
    // Misma idea que BubbleView.ShatterLayer: crece, gira un poco y se apaga — así se ve
    // ROMPERSE y no sólo desaparecer.
    IEnumerator Shatter()
    {
        if (_left <= 0) yield break;

        var layer = _shell[--_left];

        // El cuerpo se enciende EN CUANTO cae la última: el cambio de brillo y la rotura son el
        // mismo suceso, y separarlos deja al jugador sin saber qué acaba de habilitarse.
        if (_left == 0) Shade(1f);

        if (layer == null) yield break;

        var   rt   = (RectTransform)layer.transform;
        float spin = Random.Range(-28f, 28f);

        for (float t = 0f; t < shatterTime; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / shatterTime);

            rt.localScale       = Vector3.one * (1f + 0.5f * Mathf.Sqrt(p));
            rt.localEulerAngles = new Vector3(0f, 0f, spin * p);
            layer.color         = new Color(1f, 1f, 1f, 1f - p * p);
            yield return null;
        }

        rt.localScale       = Vector3.one;
        rt.localEulerAngles = Vector3.zero;
        layer.enabled       = false;
    }

    // Y sube y se va, que es lo que hace en la partida camino del hub. Solo cuando llevaba algo:
    // un obstáculo destapado no libera nada, debajo había una burbuja de color y ya.
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
            rt.sizeDelta        = Vector2.one * size * (1f + 0.25f * Mathf.Sin(p * Mathf.PI) - 0.25f * p);
            _creature.color     = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((p - 0.6f) / 0.4f));

            yield return null;
        }

        _creature.enabled = false;
    }

    static float Smooth(float p) => p * p * (3f - 2f * p);
}
