using System.Collections.Generic;
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

    // Ancho fijo del diseño del grid (10 columnas, la fila par más ancha, + márgenes a los
    // costados) — NO es el ancho real del contenedor en pantalla (eso varía por dispositivo).
    // Se usa para centrar el grid dentro del contenedor, sea cual sea su ancho real.
    public const float DesignWidth = LeftMargin * 2f + 10f * BubbleDiameter;

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

    public static int ColsInRow(int row) => IsOddRow(row) ? 9 : 10;

    public static bool IsValidCell(Vector2Int cell) =>
        cell.y >= 0 && cell.x >= 0 && cell.x < ColsInRow(cell.y);

    // GridContainer está anclado al centro (X=0 = centro de pantalla, no el borde izquierdo),
    // así que todo el resto del sistema (mouse, MuzzlePoint, burbuja en vuelo) también es
    // centrado — este offset usa DesignWidth (fijo) para centrar el grid dentro del
    // contenedor, sin importar cuál sea su ancho real (eso varía por dispositivo).
    public static Vector2 CellToLocalPos(Vector2Int cell)
    {
        float x = LeftMargin + BubbleRadius + cell.x * BubbleDiameter + (IsOddRow(cell.y) ? BubbleRadius : 0f) - DesignWidth / 2f;
        float y = -(BubbleRadius + cell.y * RowHeight);
        return new Vector2(x, y);
    }

    // Estimación barata de la celda más cercana a una posición — punto de partida
    // para el escaneo de colisión, no el resultado final (ver GridController).
    public static Vector2Int EstimateNearestCell(Vector2 localPos)
    {
        float edgeX    = localPos.x + DesignWidth / 2f; // de centrado a borde-izquierdo, ver comentario arriba
        int   row      = Mathf.Max(0, Mathf.RoundToInt((-localPos.y - BubbleRadius) / RowHeight));
        float xOffset  = IsOddRow(row) ? BubbleRadius : 0f;
        int   col      = Mathf.RoundToInt((edgeX - LeftMargin - BubbleRadius - xOffset) / BubbleDiameter);
        col = Mathf.Clamp(col, 0, ColsInRow(row) - 1);
        return new Vector2Int(col, row);
    }

    // Disco hexagonal: la celda + todo lo que esté a 'radius' pasos o menos. Es un BFS por
    // vecinos y no una fórmula geométrica porque en coordenadas offset el salto a la fila de
    // arriba/abajo cambia según la fila sea par o impar — caminar los vecinos ya resuelve eso.
    //
    // Se filtra por IsValidCell durante la expansión, así contra la pared del tablero el disco
    // se recorta solo (radio 1 en la columna 0 son 4 celdas, no 7) en vez de devolver celdas
    // que no existen.
    public static List<Vector2Int> CellsWithinRadius(Vector2Int center, int radius)
    {
        var result   = new List<Vector2Int> { center };
        var visited  = new HashSet<Vector2Int> { center };
        var frontier = new List<Vector2Int> { center };

        for (int step = 0; step < radius; step++)
        {
            var next = new List<Vector2Int>();
            foreach (var cell in frontier)
                foreach (var n in GetNeighbors(cell))
                {
                    if (!IsValidCell(n) || !visited.Add(n)) continue;
                    next.Add(n);
                    result.Add(n);
                }
            frontier = next;
        }

        return result;
    }

    // Toda la fila de esa celda. Las filas pares e impares no miden lo mismo —la impar va
    // desplazada media burbuja y tiene una columna menos— así que el ancho sale de ColsInRow y no
    // de un número fijo.
    public static List<Vector2Int> CellsInRow(int row) => CellsInRowFrom(row, -1);

    // La misma fila, pero ordenada DESDE 'fromCol' hacia los dos lados a la vez: el impacto
    // primero, luego sus dos vecinas, luego las siguientes. Quien recorra la lista y escalone por
    // el índice obtiene gratis una descarga que se abre en abanico desde donde pegó, en vez de un
    // barrido que empieza en el borde izquierdo porque así se numeran las columnas.
    //
    // fromCol fuera de la fila (o -1) devuelve el orden natural, de un extremo al otro.
    public static List<Vector2Int> CellsInRowFrom(int row, int fromCol)
    {
        var result = new List<Vector2Int>();

        if (row < 0) return result;

        int cols = ColsInRow(row);

        if (fromCol < 0 || fromCol >= cols)
        {
            for (int col = 0; col < cols; col++) result.Add(new Vector2Int(col, row));
            return result;
        }

        result.Add(new Vector2Int(fromCol, row));

        for (int d = 1; d < cols; d++)
        {
            if (fromCol - d >= 0)   result.Add(new Vector2Int(fromCol - d, row));
            if (fromCol + d < cols) result.Add(new Vector2Int(fromCol + d, row));
        }

        return result;
    }

    public static Vector2Int[] GetNeighbors(Vector2Int cell)
    {
        var offsets = IsOddRow(cell.y) ? OddRowNeighborOffsets : EvenRowNeighborOffsets;
        var result  = new Vector2Int[6];
        for (int i = 0; i < 6; i++) result[i] = cell + offsets[i];
        return result;
    }

    // Rebote en las paredes izquierda/derecha del grid — sin rebote de techo, eso se maneja
    // aparte como impacto (GDD 1.5). Devuelve true si hubo rebote, para que quien llama
    // (ShotBubble, TrajectoryLine) pueda contar rebotes.
    //
    // Las paredes se sacan de DesignWidth, NO del ancho real del contenedor en pantalla.
    // Las celdas se posicionan con DesignWidth (fijo), así que si los rebotes usaran el ancho
    // real, en pantallas anchas la pelota rebotaría más afuera de donde terminan las columnas
    // y al pegarse se ajustaría a la columna más cercana, que está más adentro: la trayectoria
    // indica un punto y la bola llega a otro. En iPhone casi no se nota; en iPad, mucho.
    //
    // El anchor del grid está centrado (X=0 es el centro, no el borde izquierdo), así que las
    // paredes quedan en ±DesignWidth/2.
    public static bool ReflectIfNeeded(ref Vector2 pos, ref Vector2 dir)
    {
        float leftWall  = -DesignWidth / 2f + BubbleRadius;
        float rightWall =  DesignWidth / 2f - BubbleRadius;

        if (pos.x < leftWall)
        {
            pos.x = 2f * leftWall - pos.x;
            dir.x = -dir.x;
            return true;
        }
        if (pos.x > rightWall)
        {
            pos.x = 2f * rightWall - pos.x;
            dir.x = -dir.x;
            return true;
        }
        return false;
    }
}
