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

    readonly Dictionary<Vector2Int, BubbleView> _cells = new();

    public int CellCount => _cells.Count;

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
