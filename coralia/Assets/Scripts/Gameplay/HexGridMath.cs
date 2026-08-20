using UnityEngine;

// Grid hexagonal offset: fila 0 = techo, filas impares desplazadas medio diámetro a la derecha.
// cell.x = columna, cell.y = fila. OJO: la dirección de los vecinos fila-1/fila+1 se
// invierte entre fila par e impar — es el error más fácil de cometer acá.
public static class HexGridMath
{
    public const float BubbleDiameter = 92f;
    public const float BubbleRadius   = BubbleDiameter / 2f;
    public const float LeftMargin     = 34f;
    public const float RowHeight      = BubbleDiameter * 0.866f;

    static readonly Vector2Int[] EvenRowNeighborOffsets =
    {
        new(-1, 0), new(1, 0),    // misma fila
        new(-1, -1), new(0, -1),  // fila de arriba
        new(-1, 1), new(0, 1),    // fila de abajo
    };

    static readonly Vector2Int[] OddRowNeighborOffsets =
    {
        new(-1, 0), new(1, 0),    // misma fila
        new(0, -1), new(1, -1),   // fila de arriba
        new(0, 1), new(1, 1),     // fila de abajo
    };

    public static bool IsOddRow(int row) => row % 2 != 0;

    public static int ColsInRow(int row) => IsOddRow(row) ? 10 : 11;

    public static bool IsValidCell(Vector2Int cell) =>
        cell.y >= 0 && cell.x >= 0 && cell.x < ColsInRow(cell.y);

    public static Vector2 CellToLocalPos(Vector2Int cell)
    {
        float x = LeftMargin + BubbleRadius + cell.x * BubbleDiameter + (IsOddRow(cell.y) ? BubbleRadius : 0f);
        float y = -(BubbleRadius + cell.y * RowHeight);
        return new Vector2(x, y);
    }

    // Estimación barata de la celda más cercana a una posición — punto de partida
    // para el escaneo de colisión, no el resultado final (ver GridController).
    public static Vector2Int EstimateNearestCell(Vector2 localPos)
    {
        int   row      = Mathf.Max(0, Mathf.RoundToInt((-localPos.y - BubbleRadius) / RowHeight));
        float xOffset  = IsOddRow(row) ? BubbleRadius : 0f;
        int   col      = Mathf.RoundToInt((localPos.x - LeftMargin - BubbleRadius - xOffset) / BubbleDiameter);
        col = Mathf.Clamp(col, 0, ColsInRow(row) - 1);
        return new Vector2Int(col, row);
    }

    public static Vector2Int[] GetNeighbors(Vector2Int cell)
    {
        var offsets = IsOddRow(cell.y) ? OddRowNeighborOffsets : EvenRowNeighborOffsets;
        var result  = new Vector2Int[6];
        for (int i = 0; i < 6; i++) result[i] = cell + offsets[i];
        return result;
    }

    // Rebote en las paredes izquierda/derecha del contenedor — sin rebote de techo,
    // eso se maneja aparte como impacto (GDD 1.5). Devuelve true si hubo rebote,
    // para que quien llama (ShotBubble, TrajectoryLine) pueda contar rebotes.
    public static bool ReflectIfNeeded(ref Vector2 pos, ref Vector2 dir, float containerWidth)
    {
        if (pos.x - BubbleRadius < 0f)
        {
            pos.x = BubbleRadius - (pos.x - BubbleRadius);
            dir.x = -dir.x;
            return true;
        }
        if (pos.x + BubbleRadius > containerWidth)
        {
            pos.x = containerWidth - BubbleRadius - ((pos.x + BubbleRadius) - containerWidth);
            dir.x = -dir.x;
            return true;
        }
        return false;
    }
}
