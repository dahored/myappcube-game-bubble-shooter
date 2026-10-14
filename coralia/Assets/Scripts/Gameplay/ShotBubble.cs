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

    // El avance de un frame se parte en tramos cortos y se busca colisión en cada uno.
    //
    // Moviendo de una sola vez, la comprobación solo ocurre donde termina el salto: con un frame
    // algo más lento de lo normal ese salto supera los 92px de una burbuja, y la burbuja disparada
    // ATRAVIESA el grid sin detectar nada hasta chocar el techo. Es intermitente y depende del
    // rendimiento, así que aparece justo en los niveles grandes, que son los que más tardan.
    //
    // Medio radio por tramo deja cuatro comprobaciones por burbuja recorrida, que es de sobra.
    const float MAX_STEP = HexGridMath.BubbleRadius * 0.5f;

    // Y lo que puede avanzar en UN frame, por largo que ese frame haya sido.
    //
    // Los tramos de arriba parten el movimiento para no atravesar nada, pero todos ocurren dentro
    // del mismo frame: lo que se dibuja es solo dónde acabó. A 60 fps son 42px, menos de media
    // burbuja, y el vuelo se ve seguido; en un tirón de 100ms serían 250px de un salto, y la
    // burbuja aparece en el destino sin haber pasado por el medio.
    //
    // Se nota sobre todo en los tiros que rebotan, que son los de recorrido más largo y por tanto
    // los que más frames viven (reportado por Diego).
    //
    // Un diámetro por frame es el tope: a ritmo normal no se alcanza nunca, y cuando el frame se
    // alarga el disparo prefiere tardar un poco más a teletransportarse. Lo que cuesta es tiempo
    // de vuelo, que nadie cronometra; lo que se gana es que el tiro se pueda SEGUIR, que es de
    // donde el jugador aprende a calcular los rebotes.
    const float MAX_FRAME_TRAVEL = HexGridMath.BubbleDiameter;

    public ImpactInfo? Tick(float dt)
    {
        float remaining = Mathf.Min(Velocity.magnitude * dt, MAX_FRAME_TRAVEL);

        while (remaining > 0f)
        {
            float step = Mathf.Min(remaining, MAX_STEP);
            remaining -= step;

            Vector2 pos = _rt.anchoredPosition + Velocity.normalized * step;
            HexGridMath.ReflectIfNeeded(ref pos, ref Velocity);
            _rt.anchoredPosition = pos;

            var impact = ImpactAt(pos);
            if (impact.HasValue) return impact;
        }

        return null;
    }

    ImpactInfo? ImpactAt(Vector2 pos)
    {
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
