using System.Collections.Generic;
using UnityEngine;

// Única fuente de verdad del estado del grid. Nadie más toca el diccionario de celdas
// directamente — CannonController y GameplayController leen/piden cambios vía esta API.
public class GridController : MonoBehaviour
{
    [SerializeField] GameObject bubblePrefab;

    [Header("Sprites por color (arrastrar el sub-sprite bubble_X_0 de cada PNG)")]
    [SerializeField] Sprite spriteRed;
    [SerializeField] Sprite spriteBlue;
    [SerializeField] Sprite spriteYellow;
    [SerializeField] Sprite spriteGreen;
    [SerializeField] Sprite spritePurple;
    [SerializeField] Sprite spriteOrange;
    [SerializeField] Sprite spritePink;
    [SerializeField] Sprite spriteGrey;
    [SerializeField] Sprite spriteBrown;
    [SerializeField] Sprite spriteBlack;
    [SerializeField] Sprite spriteRedWine;
    [SerializeField] Sprite spriteMintGreen;
    [SerializeField] Sprite spriteDarkBlue;
    [SerializeField] Sprite spriteDarkGrey;
    [SerializeField] Sprite spriteRainbow;

    [Header("Scroll de retirada (GDD: el grid se aleja del cañón cuando se llena)")]
    [Range(0f, 1f)]
    [SerializeField] float scrollTriggerRatio = 0.65f; // a qué % de la distancia techo→cañón empieza a retirarse (60-70% sugerido)
    [SerializeField] float maxScrollOffset    = 6f * HexGridMath.BubbleDiameter; // tope de seguridad, no se retira más que esto
    [SerializeField] float scrollSpeed        = 900f; // px/seg de la animación de scroll

    [Header("Shake — combos grandes (referencia: Candy Crush)")]
    [SerializeField] int   shakeThreshold = 8;   // match.Count + drop.Count a partir del cual tiembla
    [SerializeField] float shakeDuration  = 0.25f; // cuánto tarda en apagarse desde el tope
    [SerializeField] float shakeMagnitude = 12f;   // px del temblor con la energía al tope
    [Tooltip("Cuántos pops seguidos hacen falta para llegar al temblor máximo.")]
    [SerializeField] float shakeMaxEnergy = 4f;

    readonly Dictionary<Vector2Int, BubbleView> _cells = new();

    RectTransform _rt;
    Vector2       _baseAnchoredPos;
    float         _muzzleReferenceY;
    float         _scrollOffsetY;
    float         _scrollTarget;
    float         _scrollWait;   // cuánto lleva esperando a que terminen las animaciones

    // Tope de esa espera. Una caída dura hasta 1s (BubbleView.DROP_MAX_DURATION) más el pop en
    // cadena, así que con 1.5s alcanza para el caso normal y sigue cortando cualquier cuelgue.
    const float WAIT_TIMEOUT = 1.5f;
    Vector2       _shakeOffset;
    float         _shakeEnergy;   // sube con cada pop, baja sola — ver ShakePulse

    // Cuánto se retiró el grid del cañón ahora mismo (0 = posición normal) — CannonController
    // lo resta de la posición del muzzle para que el disparo/mira sigan apuntando bien aunque
    // el grid entero se haya movido.
    public float ScrollOffsetY => _scrollOffsetY;

    public int CellCount => _cells.Count;

    // Tocar una burbuja del grid = apuntar y disparar hacia ella — CannonController se
    // suscribe en su Start(). No usa la celda struck, avisa la celda de la burbuja tocada.
    public event System.Action<Vector2Int> OnBubbleTapped;
    void HandleBubbleViewTapped(BubbleView view) => OnBubbleTapped?.Invoke(view.Cell);

    // Reenvío de drag iniciado sobre una burbuja — mismo destino que AimInputRelay
    // (CannonController.OnAimBegin/OnAimDrag/OnAimEnd), para que apuntar arrastrando funcione
    // igual empiece donde empiece el gesto, no solo en huecos vacíos del grid.
    public event System.Action<Vector2> OnBubbleDragBegin;
    public event System.Action<Vector2> OnBubbleDragMove;
    public event System.Action<Vector2> OnBubbleDragEnd;

    void Awake()
    {
        _rt = (RectTransform)transform;
        _baseAnchoredPos = _rt.anchoredPosition;
    }

    void Update()
    {
        RecomputeScroll();

        // El retiro espera a que no quede ninguna burbuja explotando ni cayendo. Todas son hijas
        // de este RectTransform, así que moverlo mientras se animan les arrastra la posición: el
        // pop se ve desplazarse a mitad de la explosión y las que caen cambian de rumbo.
        //
        // El destino (_scrollTarget) ya está calculado desde que se resolvió el disparo — lo
        // único que espera es el movimiento, así que no se pierde ni se acumula nada.
        //
        // WAIT_TIMEOUT es la red de seguridad: una animación que no termine nunca dejaría el
        // grid encima del cañón para siempre, y eso es mucho peor que un pop que se corte.
        bool pending = !Mathf.Approximately(_scrollOffsetY, _scrollTarget);

        if (pending && BubbleView.AnimatingCount > 0 && _scrollWait < WAIT_TIMEOUT)
        {
            _scrollWait += Time.deltaTime;
        }

        bool scrolling = pending && (BubbleView.AnimatingCount == 0 || _scrollWait >= WAIT_TIMEOUT);
        if (scrolling) _scrollOffsetY = Mathf.MoveTowards(_scrollOffsetY, _scrollTarget, scrollSpeed * Time.deltaTime);
        if (!pending) _scrollWait = 0f;

        // La energía baja sola. Mientras siguen llegando pops la reponen más rápido de lo que
        // decae, así que el temblor se mantiene durante toda la cadena; cuando dejan de llegar,
        // se apaga suave en vez de cortarse.
        bool shaking = _shakeEnergy > 0f;
        if (shaking)
        {
            _shakeEnergy = Mathf.Max(0f, _shakeEnergy - Time.deltaTime / Mathf.Max(0.01f, shakeDuration));
            _shakeOffset = Random.insideUnitCircle * shakeMagnitude * (_shakeEnergy / shakeMaxEnergy);
        }

        // Se suma al offset de scroll, no lo reemplaza — así el shake no pelea con el
        // retiro del grid si ambos coinciden en el mismo momento.
        if (scrolling || shaking)
            _rt.anchoredPosition = _baseAnchoredPos + Vector2.up * _scrollOffsetY + _shakeOffset;
    }

    // Llamado por GameplayController después de resolver un match+drop — solo tiembla si el
    // combo fue lo suficientemente grande (shakeThreshold), como el efecto de Candy Crush en
    // combos grandes.
    // Un golpe de temblor, que se llama UNA VEZ POR BURBUJA a medida que explota.
    //
    // Antes era un solo evento al final del combo: los pops y la vibración iban de a uno, pero la
    // pantalla daba una única sacudida ya terminada la cadena. Ahora cada burbuja aporta lo suyo
    // y la energía se acumula, así que un combo grande sostiene el temblor mientras dura y se
    // apaga solo al final — que es como se siente un derrumbe.
    public void ShakePulse()
    {
        _shakeEnergy = Mathf.Min(_shakeEnergy + 1f, shakeMaxEnergy);
    }

    // Umbral: por debajo de este tamaño de cadena no se sacude nada. Lo decide quien llama, que
    // es el que conoce el combo entero antes de empezar a reventarlo.
    public bool ShakeWorthIt(int chainSize) => chainSize >= shakeThreshold;

    // Llamado una vez por CannonController.Start() con la posición Y del muzzle (sin scroll) —
    // es la referencia contra la que medimos qué tan cerca está la fila más baja del cañón.
    public void SetMuzzleReferenceY(float muzzleLocalY)
    {
        _muzzleReferenceY = muzzleLocalY;

        // Recién con la referencia del cañón se puede saber si el nivel YA nace demasiado bajo.
        // Un nivel largo llega al cañón desde el primer frame, y hasta acá el retiro solo se
        // recalculaba después de disparar — o sea que el jugador veía el grid encima del cañón
        // hasta que tiraba la primera burbuja.
        RecomputeScroll(immediate: true);
    }

    // Cuánto debería retirarse el grid ahora mismo. Se recalcula cada frame desde Update: el
    // grid crece hacia abajo con cada disparo que no matchea y se vacía con cada match, así que
    // el destino tiene que seguir el estado real y no depender de que alguien avise.
    //
    // El umbral es un % de la distancia TOTAL techo→cañón (no un gap fijo en píxeles) —
    // así escala solo con niveles que tengan más o menos espacio disponible, en vez de
    // disparar siempre al mismo puñado de píxeles sin importar qué tan largo sea el grid.
    public void RecomputeScroll(bool immediate = false)
    {
        if (_cells.Count == 0) { _scrollTarget = 0f; return; }

        float ceilingY   = -HexGridMath.BubbleRadius; // fila 0, referencia fija
        float totalSpan  = ceilingY - _muzzleReferenceY;
        if (totalSpan <= 0f) { _scrollTarget = 0f; return; } // referencia de muzzle todavía no seteada

        float lowestRowY = float.PositiveInfinity;
        foreach (var cell in _cells.Keys)
        {
            float y = HexGridMath.CellToLocalPos(cell).y;
            if (y < lowestRowY) lowestRowY = y;
        }

        float requiredGap = (1f - scrollTriggerRatio) * totalSpan; // colchón mínimo según el % configurado
        float rawGap      = lowestRowY - _muzzleReferenceY;        // colchón actual sin scroll (ambos valores son negativos)

        // El tope no puede ser un número fijo de diámetros: un nivel de 20 filas mide más de
        // 1600px y necesita retirarse mucho más que uno de 8. Se toma el mayor entre el valor
        // configurado y la altura real del grid, así el límite acompaña al nivel en vez de
        // dejar las burbujas encima del cañón.
        float gridSpan = ceilingY - lowestRowY;
        float allowed  = Mathf.Max(maxScrollOffset, gridSpan);

        _scrollTarget = Mathf.Clamp(requiredGap - rawGap, 0f, allowed);

        // Al cargar el nivel no hay nada que animar: si el grid ya nace bajo, tiene que estar
        // en su sitio desde el primer frame en vez de deslizarse a la vista del jugador.
        if (!immediate) return;

        _scrollOffsetY = _scrollTarget;
        _scrollWait    = 0f;
        if (_rt) _rt.anchoredPosition = _baseAnchoredPos + Vector2.up * _scrollOffsetY;
    }

    // Colores que todavía están en el grid — usado por CannonController para no ofrecer
    // colores que ya no tienen con qué matchear (GDD: "smart queue"). Rainbow no cuenta,
    // matchea con cualquier color así que no representa una necesidad de suministro.
    public HashSet<BubbleColor> ColorsOnGrid()
    {
        var colors = new HashSet<BubbleColor>();
        foreach (var view in _cells.Values)
            if (view.ColorType != BubbleColor.Rainbow) colors.Add(view.ColorType);
        return colors;
    }

    // Colores del FRENTE del grid, repetidos según cuánto conviene ofrecer cada uno. La lista
    // sale con repeticiones a propósito: sortear un elemento al azar ya respeta los pesos.
    //
    // Que un color exista en el grid no significa que se pueda usar. Una burbuja enterrada
    // detrás de tres filas es inalcanzable, y ofrecerla es regalarle al jugador un disparo que
    // solo puede soltar en cualquier lado — que es de donde salen los dos o tres disparos
    // desperdiciados seguidos.
    //
    // Cuenta doble la burbuja que ya tiene un vecino de su mismo color: ahí un solo disparo
    // completa el match, así que es el color más útil que se puede entregar.
    public List<BubbleColor> ReachableColorPool()
    {
        var pool = new List<BubbleColor>();

        // Primero los HUECOS donde caer completa un match: un espacio libre que ya tiene dos o
        // más burbujas del mismo color alrededor. Ahí un solo disparo de ese color explota.
        //
        // Esto es lo que convierte la cola en algo jugable. Contar burbujas del frente solo
        // asegura que el color exista; lo que el jugador necesita es que exista un LUGAR donde
        // ese color sirva. Con cinco o seis colores en pantalla, la diferencia entre una cosa y
        // la otra son varios disparos seguidos sin nada que hacer.
        foreach (var slot in EmptyNeighbors())
        {
            var around = new Dictionary<BubbleColor, int>();

            foreach (var neighbor in HexGridMath.GetNeighbors(slot))
            {
                if (!TryGetBubble(neighbor, out var view) || view.ColorType == BubbleColor.Rainbow) continue;
                around.TryGetValue(view.ColorType, out int count);
                around[view.ColorType] = count + 1;
            }

            foreach (var pair in around)
                if (pair.Value >= 2) for (int i = 0; i < MATCH_SLOT_WEIGHT; i++) pool.Add(pair.Key);
        }

        // Y después, con mucho menos peso, los colores del frente: mantienen variedad y evitan que
        // la cola se vuelva un único color cuando hay un solo sitio de match.
        foreach (var pair in _cells)
        {
            var color = pair.Value.ColorType;
            if (color == BubbleColor.Rainbow || !IsReachable(pair.Key)) continue;

            pool.Add(color);
        }

        return pool;
    }

    // Cuánto pesa un color que tiene dónde matchear, frente a uno que solo está en el frente.
    const int MATCH_SLOT_WEIGHT = 6;

    // Huecos pegados a alguna burbuja, que son los únicos a los que un disparo puede llegar.
    IEnumerable<Vector2Int> EmptyNeighbors()
    {
        var seen = new HashSet<Vector2Int>();

        foreach (var cell in _cells.Keys)
            foreach (var neighbor in HexGridMath.GetNeighbors(cell))
            {
                if (IsOccupied(neighbor) || !HexGridMath.IsValidCell(neighbor)) continue;
                if (seen.Add(neighbor)) yield return neighbor;
            }
    }

    // Alcanzable = tiene un hueco al lado o por debajo. Los huecos de ARRIBA no cuentan: una
    // burbuja disparada sube, no baja, así que nunca entra por encima de otra.
    bool IsReachable(Vector2Int cell)
    {
        foreach (var neighbor in HexGridMath.GetNeighbors(cell))
        {
            if (neighbor.y < cell.y || !HexGridMath.IsValidCell(neighbor)) continue;
            if (!IsOccupied(neighbor)) return true;
        }
        return false;
    }

    public void SpawnFromLevel(LevelData level)
    {
        Clear();

        Vector2Int creatureCell = new(-1, -1);
        if (level.objective != null && level.objective.type == "rescue" && level.objective.creature_position?.Count == 2)
        {
            // creature_position es [fila, col] (ver LevelData.cs) -> Vector2Int(col, fila)
            creatureCell = new Vector2Int(level.objective.creature_position[1], level.objective.creature_position[0]);
        }

        foreach (var entry in level.bubbles)
        {
            var cell  = new Vector2Int(entry.col, entry.row);
            var color = BubbleColorExtensions.Parse(entry.color);
            var view  = PlaceBubble(cell, color);
            if (cell == creatureCell) view.SetCreatureMarker(true);
        }
    }

    public BubbleView PlaceBubble(Vector2Int cell, BubbleColor color)
    {
        var go   = Instantiate(bubblePrefab, transform);
        var view = go.GetComponent<BubbleView>();
        view.Setup(cell, color, SpriteFor(color));
        SubscribeBubbleInput(view);
        _cells[cell] = view;
        return view;
    }

    // Re-parenta una burbuja ya instanciada (la que acaba de aterrizar) en vez de
    // destruir + recrear.
    public void RegisterExisting(BubbleView view, Vector2Int cell)
    {
        view.transform.SetParent(transform, false);
        view.SetCell(cell);
        ((RectTransform)view.transform).anchoredPosition = HexGridMath.CellToLocalPos(cell);
        SubscribeBubbleInput(view); // esta instancia nunca pasó por PlaceBubble (viene de CannonController.Fire)
        _cells[cell] = view;
    }

    void SubscribeBubbleInput(BubbleView view)
    {
        view.OnTapped    += HandleBubbleViewTapped;
        view.OnDragBegin += p => OnBubbleDragBegin?.Invoke(p);
        view.OnDragMove  += p => OnBubbleDragMove?.Invoke(p);
        view.OnDragEnd   += p => OnBubbleDragEnd?.Invoke(p);
    }

    public bool IsOccupied(Vector2Int cell) => _cells.ContainsKey(cell);

    public bool TryGetBubble(Vector2Int cell, out BubbleView view) => _cells.TryGetValue(cell, out view);

    public void RemoveBubble(Vector2Int cell) => _cells.Remove(cell);

    void Clear()
    {
        foreach (var view in _cells.Values)
            if (view) Destroy(view.gameObject);
        _cells.Clear();
    }

    // Al impactar una celda ocupada: BFS en anillos crecientes buscando la primera
    // celda vacía más cercana al punto de impacto. Maneja "los 6 vecinos están
    // ocupados" sin caso especial, expandiendo al siguiente anillo.
    public Vector2Int FindNearestEmptyCell(Vector2 impactLocalPos, Vector2Int struckCell)
    {
        var visited = new HashSet<Vector2Int> { struckCell };
        var ring    = new List<Vector2Int> { struckCell };

        while (true)
        {
            Vector2Int? best     = null;
            float       bestDist = float.MaxValue;
            foreach (var cell in ring)
            {
                if (IsOccupied(cell) || !HexGridMath.IsValidCell(cell)) continue;
                float dist = Vector2.Distance(impactLocalPos, HexGridMath.CellToLocalPos(cell));
                if (dist < bestDist) { bestDist = dist; best = cell; }
            }
            if (best.HasValue) return best.Value;

            var nextRing = new List<Vector2Int>();
            foreach (var cell in ring)
                foreach (var n in HexGridMath.GetNeighbors(cell))
                    if (visited.Add(n)) nextRing.Add(n);
            ring = nextRing;
        }
    }

    // Una arcoíris recién aterrizada se vuelve un color de verdad, el que más le conviene al
    // jugador: se prueba cada color que tiene al lado y gana el que forme el grupo más grande.
    //
    // No es cosmético. LinksWith trata a la arcoíris como comodín EN AMBAS DIRECCIONES, así que
    // si queda de semilla del flood-fill conecta con todo lo que toque, y eso con todo lo que
    // toque: un solo disparo limpiaba el nivel entero. Como comodín pasivo dentro de un grupo de
    // otro color sigue funcionando igual — el problema era solo cuando arrancaba la búsqueda.
    public void ResolveRainbow(Vector2Int cell)
    {
        if (!TryGetBubble(cell, out var view) || view.ColorType != BubbleColor.Rainbow) return;

        var best      = BubbleColor.Rainbow;
        int bestCount = 0;

        foreach (var candidate in NeighborColors(cell))
        {
            view.SetColor(candidate, SpriteFor(candidate));

            int count = FindConnectedSameColor(cell).Count;
            if (count <= bestCount) continue;

            bestCount = count;
            best      = candidate;
        }

        // Si no tenía ningún vecino con color (cayó entre arcoíris, o sola), vuelve a ser comodín.
        view.SetColor(best, SpriteFor(best));
    }

    HashSet<BubbleColor> NeighborColors(Vector2Int cell)
    {
        var colors = new HashSet<BubbleColor>();

        foreach (var neighbor in HexGridMath.GetNeighbors(cell))
            if (TryGetBubble(neighbor, out var view) && view.ColorType != BubbleColor.Rainbow)
                colors.Add(view.ColorType);

        return colors;
    }

    // Match (GDD 1.4): flood-fill desde `start` conectando por color, rainbow como comodín.
    public List<Vector2Int> FindConnectedSameColor(Vector2Int start)
    {
        var result = new List<Vector2Int>();
        if (!TryGetBubble(start, out var startView)) return result;

        var visited = new HashSet<Vector2Int> { start };
        var queue   = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            result.Add(cell);
            foreach (var n in HexGridMath.GetNeighbors(cell))
            {
                if (visited.Contains(n) || !TryGetBubble(n, out var neighborView)) continue;
                if (!startView.ColorType.LinksWith(neighborView.ColorType)) continue;
                visited.Add(n);
                queue.Enqueue(n);
            }
        }
        return result;
    }

    // Drop de flotantes (GDD 1.4): recompute global de qué celdas siguen conectadas
    // al techo (fila 0, conectada por definición). Lo que esta BFS no alcanza, cae —
    // en una sola pasada, sin importar la profundidad de la cadena.
    public List<Vector2Int> FindUnreachableFromCeiling()
    {
        var reachable = new HashSet<Vector2Int>();
        var queue     = new Queue<Vector2Int>();

        foreach (var cell in _cells.Keys)
        {
            if (cell.y != 0) continue;
            reachable.Add(cell);
            queue.Enqueue(cell);
        }

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var n in HexGridMath.GetNeighbors(cell))
            {
                if (reachable.Contains(n) || !IsOccupied(n)) continue;
                reachable.Add(n);
                queue.Enqueue(n);
            }
        }

        var unreachable = new List<Vector2Int>();
        foreach (var cell in _cells.Keys)
            if (!reachable.Contains(cell)) unreachable.Add(cell);
        return unreachable;
    }

    public Sprite SpriteFor(BubbleColor color) => color switch
    {
        BubbleColor.Red       => spriteRed,
        BubbleColor.Blue      => spriteBlue,
        BubbleColor.Yellow    => spriteYellow,
        BubbleColor.Green     => spriteGreen,
        BubbleColor.Purple    => spritePurple,
        BubbleColor.Orange    => spriteOrange,
        BubbleColor.Pink      => spritePink,
        BubbleColor.Grey      => spriteGrey,
        BubbleColor.Brown     => spriteBrown,
        BubbleColor.Black     => spriteBlack,
        BubbleColor.RedWine   => spriteRedWine,
        BubbleColor.MintGreen => spriteMintGreen,
        BubbleColor.DarkBlue  => spriteDarkBlue,
        BubbleColor.DarkGrey  => spriteDarkGrey,
        BubbleColor.Rainbow   => spriteRainbow,
        _                     => null,
    };
}
