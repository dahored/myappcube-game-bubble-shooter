using UnityEngine;

// Patrones de COLOR para el generador de niveles.
//
// Son la mitad que faltaba. La silueta decide qué celdas se ocupan; el patrón decide de qué color
// va cada una, y es el que hace que un nivel se lea como algo diseñado: los anillos concéntricos,
// las rayas, la equis, el diamante del centro. Un nivel bien hecho casi siempre es una masa densa
// con un patrón de color encima — no un recorte con huecos.
//
// Cada patrón es una función (celda) -> número de banda. El generador después reparte los colores
// de la paleta entre las bandas, así que el mismo patrón se ve distinto con dos colores que con
// cinco.
//
// Las distancias se miden sobre la posición REAL de la burbuja (HexGridMath.CellToLocalPos), no
// sobre fila y columna: en un grid hexagonal las filas impares van corridas media burbuja, y
// contar celdas da anillos ovalados y diagonales torcidas.
public static class LevelPatternLibrary
{
    // Marco de referencia del layout: dónde está su centro y cuánto mide, para que el mismo
    // patrón funcione igual en un nivel de 9 filas y en uno de 22.
    public struct Field
    {
        public Vector2 center;
        public float   radius;   // del centro al punto más lejano, en diámetros de burbuja
        public int     rows;
    }

    public delegate int Band(Vector2Int cell, Field field);

    public struct Pattern
    {
        public string name;
        public Band   band;
    }

    public static readonly Pattern[] All =
    {
        // Anillos concéntricos desde el centro. El clásico de las referencias: diana, aureola.
        new Pattern { name = "anillos", band = (cell, f) => Mathf.FloorToInt(Radial(cell, f) * 3.2f) },

        // Bandas horizontales — el patrón más común en los bubble shooters comerciales.
        new Pattern { name = "franjas", band = (cell, f) => cell.y / 2 },

        // Bandas horizontales finas: una fila de cada color, muy legible.
        new Pattern { name = "líneas", band = (cell, f) => cell.y },

        // Columnas. En hexagonal quedan levemente dentadas, que es lo que las hace ver dibujadas.
        new Pattern { name = "columnas", band = (cell, f) => cell.x / 2 },

        // Diagonales en un sentido y en el otro.
        new Pattern { name = "diagonal", band = (cell, f) => (cell.x + cell.y) / 2 },
        new Pattern { name = "diagonal invertida", band = (cell, f) => (cell.x - cell.y + f.rows) / 2 },

        // Equis: las dos diagonales se cruzan en el centro.
        new Pattern { name = "equis", band = (cell, f) =>
        {
            var p = Local(cell, f);
            return Mathf.FloorToInt(Mathf.Min(Mathf.Abs(p.x - p.y), Mathf.Abs(p.x + p.y)) * 1.6f);
        }},

        // Rombo concéntrico: como los anillos pero con distancia de diamante en vez de circular.
        new Pattern { name = "rombos", band = (cell, f) =>
        {
            var p = Local(cell, f);
            return Mathf.FloorToInt((Mathf.Abs(p.x) + Mathf.Abs(p.y)) * 1.8f);
        }},

        // Damero. Con dos colores es un tablero de ajedrez; con más, un mosaico.
        new Pattern { name = "damero", band = (cell, f) => (cell.x + cell.y * 2) % 3 + (cell.y / 3) },

        // Mitades: el nivel partido por el eje vertical, cada lado con su color.
        new Pattern { name = "mitades", band = (cell, f) => Local(cell, f).x < 0f ? 0 : 1 },

        // Cuadrantes.
        new Pattern { name = "cuadrantes", band = (cell, f) =>
        {
            var p = Local(cell, f);
            return (p.x < 0f ? 0 : 1) + (p.y < 0f ? 0 : 2);
        }},

        // Uve: se abre desde el centro hacia abajo, como un embudo.
        new Pattern { name = "uve", band = (cell, f) =>
        {
            var p = Local(cell, f);
            return Mathf.FloorToInt((Mathf.Abs(p.x) - p.y) * 1.3f);
        }},

        // Corazón del nivel: un núcleo compacto de un color con el resto alrededor. Es el patrón
        // que mejor se lee cuando hay solo dos colores.
        new Pattern { name = "núcleo", band = (cell, f) => Radial(cell, f) < 0.45f ? 0 : 1 },

        // Borde y relleno: un marco de un color alrededor de todo lo demás.
        new Pattern { name = "marco", band = (cell, f) => Radial(cell, f) > 0.75f ? 0 : 1 },
    };

    // Posición de la burbuja respecto al centro del layout, en diámetros. Y positivo hacia abajo,
    // para que los patrones se lean como se ven en pantalla.
    static Vector2 Local(Vector2Int cell, Field field)
    {
        var pos = HexGridMath.CellToLocalPos(cell);
        return new Vector2(pos.x - field.center.x, field.center.y - pos.y) / HexGridMath.BubbleDiameter;
    }

    // Distancia al centro, de 0 en el medio a 1 en el punto más lejano del layout.
    static float Radial(Vector2Int cell, Field field) =>
        Local(cell, field).magnitude / Mathf.Max(0.001f, field.radius);

    // Arma el marco de referencia a partir de las celdas que ocupa el nivel.
    public static Field FieldOf(System.Collections.Generic.ICollection<Vector2Int> cells, int rows)
    {
        var center = Vector2.zero;
        foreach (var cell in cells) center += HexGridMath.CellToLocalPos(cell);
        center /= Mathf.Max(1, cells.Count);

        float radius = 0f;
        foreach (var cell in cells)
        {
            float d = (HexGridMath.CellToLocalPos(cell) - center).magnitude / HexGridMath.BubbleDiameter;
            radius = Mathf.Max(radius, d);
        }

        return new Field { center = center, radius = radius, rows = rows };
    }
}
