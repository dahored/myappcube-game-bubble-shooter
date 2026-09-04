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
    [SerializeField] Sprite spriteRainbow;

    [Header("Scroll de retirada (GDD: el grid se aleja del cañón cuando se llena)")]
    [Range(0f, 1f)]
    [SerializeField] float scrollTriggerRatio = 0.65f; // a qué % de la distancia techo→cañón empieza a retirarse (60-70% sugerido)
    [SerializeField] float maxScrollOffset    = 6f * HexGridMath.BubbleDiameter; // tope de seguridad, no se retira más que esto
    [SerializeField] float scrollSpeed        = 900f; // px/seg de la animación de scroll

    [Header("Shake — combos grandes (referencia: Candy Crush)")]
    [SerializeField] int   shakeThreshold = 8;   // match.Count + drop.Count a partir del cual tiembla
    [SerializeField] float shakeDuration  = 0.25f;
    [SerializeField] float shakeMagnitude = 12f; // px, se amortigua a 0 durante shakeDuration

    readonly Dictionary<Vector2Int, BubbleView> _cells = new();

    RectTransform _rt;
    Vector2       _baseAnchoredPos;
    float         _muzzleReferenceY;
    float         _scrollOffsetY;
    float         _scrollTarget;
    Vector2       _shakeOffset;
    float         _shakeTimer;

    // Cuánto se retiró el grid del cañón ahora mismo (0 = posición normal) — CannonController
    // lo resta de la posición del muzzle para que el disparo/mira sigan apuntando bien aunque
    // el grid entero se haya movido.
    public float ScrollOffsetY => _scrollOffsetY;

    public int CellCount => _cells.Count;

    // Tocar una burbuja del grid = apuntar y disparar hacia ella — CannonController se
    // suscribe en su Start(). No usa la celda struck, avisa la celda de la burbuja tocada.
    public event System.Action<Vector2Int> OnBubbleTapped;
    void HandleBubbleViewTapped(BubbleView view) => OnBubbleTapped?.Invoke(view.Cell);

    void Awake()
    {
        _rt = (RectTransform)transform;
        _baseAnchoredPos = _rt.anchoredPosition;
    }

    void Update()
    {
        bool scrolling = !Mathf.Approximately(_scrollOffsetY, _scrollTarget);
        if (scrolling) _scrollOffsetY = Mathf.MoveTowards(_scrollOffsetY, _scrollTarget, scrollSpeed * Time.deltaTime);

        bool shaking = _shakeTimer > 0f;
        if (shaking)
        {
            _shakeTimer -= Time.deltaTime;
            float damp = Mathf.Clamp01(_shakeTimer / shakeDuration); // se amortigua a 0 al final, no corta de golpe
            _shakeOffset = _shakeTimer > 0f ? Random.insideUnitCircle * shakeMagnitude * damp : Vector2.zero;
        }

        // Se suma al offset de scroll, no lo reemplaza — así el shake no pelea con el
        // retiro del grid si ambos coinciden en el mismo momento.
        if (scrolling || shaking)
            _rt.anchoredPosition = _baseAnchoredPos + Vector2.up * _scrollOffsetY + _shakeOffset;
    }

    // Llamado por GameplayController después de resolver un match+drop — solo tiembla si el
    // combo fue lo suficientemente grande (shakeThreshold), como el efecto de Candy Crush en
    // combos grandes.
    public void Shake(int chainSize)
    {
        if (chainSize < shakeThreshold) return;
        _shakeTimer = shakeDuration;
    }

    // Llamado una vez por CannonController.Start() con la posición Y del muzzle (sin scroll) —
    // es la referencia contra la que medimos qué tan cerca está la fila más baja del cañón.
    public void SetMuzzleReferenceY(float muzzleLocalY) => _muzzleReferenceY = muzzleLocalY;

    // Recalcula cuánto debería retirarse el grid ahora — llamar después de cada disparo
    // resuelto (match + drop ya aplicados), así reacciona tanto a que el grid creció
    // (dispara el retiro) como a que se vació por un match (permite que vuelva a bajar).
    //
    // El umbral es un % de la distancia TOTAL techo→cañón (no un gap fijo en píxeles) —
    // así escala solo con niveles que tengan más o menos espacio disponible, en vez de
    // disparar siempre al mismo puñado de píxeles sin importar qué tan largo sea el grid.
    public void RecomputeScroll()
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
        _scrollTarget = Mathf.Clamp(requiredGap - rawGap, 0f, maxScrollOffset);
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
        view.OnTapped += HandleBubbleViewTapped;
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
        view.OnTapped += HandleBubbleViewTapped; // esta instancia nunca pasó por PlaceBubble (viene de CannonController.Fire)
        _cells[cell] = view;
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
        BubbleColor.Red     => spriteRed,
        BubbleColor.Blue    => spriteBlue,
        BubbleColor.Yellow  => spriteYellow,
        BubbleColor.Green   => spriteGreen,
        BubbleColor.Purple  => spritePurple,
        BubbleColor.Orange  => spriteOrange,
        BubbleColor.Rainbow => spriteRainbow,
        _                   => null,
    };
}
