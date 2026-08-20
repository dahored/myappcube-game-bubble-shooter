using System.Collections.Generic;
using UnityEngine;

// Burbuja en vuelo. Sin Update() propio — CannonController llama Tick() cada frame,
// así todo el flujo de disparo queda orquestado (y eventualmente pausable) desde un solo lugar.
public class ShotBubble : MonoBehaviour
{
    public struct ImpactInfo
    {
        public Vector2Int StruckCell; // inválida (-1,-1) si el impacto fue contra el techo
        public bool       HitCeiling;
        public Vector2    LocalPos;
    }

    public Vector2     Velocity;
    public BubbleColor ColorType { get; private set; }

    BubbleView    _bubbleView;
    RectTransform _rt;
    RectTransform _container;
    GridController _grid;

    void Awake() => _bubbleView = GetComponent<BubbleView>();

    public void Init(RectTransform container, GridController grid, Vector2 startLocalPos, Vector2 dir, float speed, BubbleColor color, Sprite sprite)
    {
        _rt        = (RectTransform)transform;
        _container = container;
        _grid      = grid;
        ColorType  = color;
        Velocity   = dir.normalized * speed;

        _rt.SetParent(container, false);
        _bubbleView.Setup(new Vector2Int(-1, -1), color, sprite); // celda real recién al aterrizar
        _rt.anchoredPosition = startLocalPos;
    }

    public ImpactInfo? Tick(float dt)
    {
        Vector2 pos = _rt.anchoredPosition + Velocity * dt;
        HexGridMath.ReflectIfNeeded(ref pos, ref Velocity, _container.rect.width);
        _rt.anchoredPosition = pos;

        // El techo está en y=0 (fila 0 vive en y=-BubbleRadius); "toca techo" cuando el
        // borde superior de la burbuja (pos.y + radio) llega a esa línea.
        if (pos.y >= -HexGridMath.BubbleRadius)
            return new ImpactInfo { HitCeiling = true, LocalPos = pos, StruckCell = new Vector2Int(-1, -1) };

        var cell = HexGridMath.EstimateNearestCell(pos);
        foreach (var candidate in CandidateCells(cell))
        {
            if (_grid.IsOccupied(candidate) &&
                Vector2.Distance(pos, HexGridMath.CellToLocalPos(candidate)) < HexGridMath.BubbleDiameter)
            {
                return new ImpactInfo { HitCeiling = false, LocalPos = pos, StruckCell = candidate };
            }
        }
        return null;
    }

    static IEnumerable<Vector2Int> CandidateCells(Vector2Int cell)
    {
        yield return cell;
        foreach (var n in HexGridMath.GetNeighbors(cell)) yield return n;
    }
}
