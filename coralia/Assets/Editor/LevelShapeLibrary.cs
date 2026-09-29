using System.Collections.Generic;

// Catálogo de figuras para el generador de niveles. '#' es burbuja, '.' es hueco: cada figura se
// lee tal cual se ve acá, así que agregar una es dibujarla.
//
// Cada figura tiene tres partes, y esa es la idea central:
//
//   entrada — cómo arranca desde el techo
//   cuerpo  — la o las filas que se REPITEN para llenar la altura que pida el nivel
//   cierre  — cómo termina abajo
//
// Así una misma figura sirve para un nivel de 9 filas y para uno de 22 sin deformarse: se estira
// por el medio, como un jarrón. Antes se apilaban figuras distintas una tras otra y el resultado
// no se leía como una pieza sino como cosas amontonadas.
//
// Y todas son MACIZAS. Un anillo o una caja hueca se ven bien dibujados en un papel, pero en un
// bubble shooter se leen como un nivel al que le faltan burbujas. Los huecos que hay acá son
// pequeños y puntuales: le dan textura al interior sin vaciarlo.
//
// Reglas al dibujar una figura nueva:
//
//   1. Diez caracteres por fila. Las filas impares del grid tienen nueve celdas (van corridas
//      media burbuja), así que ahí se ignora la última columna.
//   2. La primera fila de la entrada necesita burbujas: es de donde cuelga el nivel entero.
//   3. Todo tiene que llegar al techo por vecinos; lo suelto se descarta al generar.
//   4. Simétrica respecto al centro. El generador también las espeja, y las asimétricas quedan
//      raras cuando salen invertidas.
public static class LevelShapeLibrary
{
    public struct Shape
    {
        public string   name;
        public string[] entrada;
        public string[] cuerpo;
        public string[] cierre;

        public int MinHeight => entrada.Length + cuerpo.Length + cierre.Length;
    }

    public static readonly Shape[] All =
    {
        new Shape
        {
            name    = "muro",
            entrada = new[] { "##########" },
            cuerpo  = new[] { "##########" },
            cierre  = new[] { ".########.", "..######.." },
        },

        new Shape
        {
            name    = "rombo",
            entrada = new[] { "...####...", "..######..", ".########." },
            cuerpo  = new[] { "##########" },
            cierre  = new[] { ".########.", "..######..", "...####...", "....##...." },
        },

        new Shape
        {
            name    = "campana",
            entrada = new[] { "...####...", "..######..", ".########." },
            cuerpo  = new[] { "##########" },
            cierre  = new[] { "##########", "##########", "..######.." },
        },

        new Shape
        {
            name    = "copa",
            entrada = new[] { "##########", "##########" },
            cuerpo  = new[] { "##########" },
            cierre  = new[] { ".########.", "..######..", "...####..." },
        },

        new Shape
        {
            name    = "corazón",
            entrada = new[] { ".##....##.", "##########", "##########" },
            cuerpo  = new[] { ".########." },
            cierre  = new[] { "..######..", "...####...", "....##...." },
        },

        new Shape
        {
            name    = "flecha",
            entrada = new[] { "....##....", "...####...", "..######..", ".########.", "##########" },
            cuerpo  = new[] { "..######.." },
            cierre  = new[] { "..######..", "...####..." },
        },

        new Shape
        {
            name    = "torre",
            entrada = new[] { "##########", "##########" },
            cuerpo  = new[] { "..######..", "..######.." },
            cierre  = new[] { ".########.", "..######..", "...####..." },
        },

        new Shape
        {
            name    = "diente",
            entrada = new[] { "##########", "##########" },
            cuerpo  = new[] { "###....###", "##########" },
            cierre  = new[] { ".########.", "...####..." },
        },

        new Shape
        {
            name    = "ola",
            entrada = new[] { "##########", "####..####" },
            cuerpo  = new[] { "##########", "####..####" },
            cierre  = new[] { "##########", ".########.", "..######.." },
        },

        new Shape
        {
            name    = "columnas",
            entrada = new[] { "##########", "##########" },
            cuerpo  = new[] { "###....###" },
            cierre  = new[] { "##########", ".########.", "..######.." },
        },

        new Shape
        {
            name    = "reloj",
            entrada = new[] { "##########", ".########.", "..######.." },
            cuerpo  = new[] { "...####..." },
            cierre  = new[] { "..######..", ".########.", "##########", ".########." },
        },

        new Shape
        {
            name    = "corona",
            entrada = new[] { "##..##..##", "##########", "##########" },
            cuerpo  = new[] { "##########" },
            cierre  = new[] { ".########.", "..######..", "...####..." },
        },

        new Shape
        {
            name    = "escudo",
            entrada = new[] { "##########", "##########", "##########" },
            cuerpo  = new[] { ".########." },
            cierre  = new[] { ".########.", "..######..", "...####...", "....##...." },
        },

        new Shape
        {
            name    = "hoja",
            entrada = new[] { "....##....", "...####...", "..######.." },
            cuerpo  = new[] { ".########." },
            cierre  = new[] { "..######..", "...####...", "....##...." },
        },

        new Shape
        {
            name    = "puente",
            entrada = new[] { "##########", "##########" },
            cuerpo  = new[] { "###....###" },
            cierre  = new[] { "##########", "##########", ".########." },
        },

        new Shape
        {
            name    = "abanico",
            entrada = new[] { "...####...", ".########.", "##########" },
            cuerpo  = new[] { "##########" },
            cierre  = new[] { "###....###", "..######..", "...####..." },
        },

        new Shape
        {
            name    = "peine",
            entrada = new[] { "##########", "##########", "##########" },
            cuerpo  = new[] { "###....###" },
            cierre  = new[] { "###....###", "..######.." },
        },

        new Shape
        {
            name    = "cáliz",
            entrada = new[] { "##########", ".########." },
            cuerpo  = new[] { "..######.." },
            cierre  = new[] { ".########.", "##########", "##########" },
        },

        new Shape
        {
            name    = "roca",
            entrada = new[] { ".########.", "##########", "##########" },
            cuerpo  = new[] { "##########", "##########" },
            cierre  = new[] { "##########", ".########." },
        },

        new Shape
        {
            name    = "gota",
            entrada = new[] { "....##....", "...####...", "..######..", ".########." },
            cuerpo  = new[] { "##########" },
            cierre  = new[] { "##########", ".########.", "..######..", "...####..." },
        },

        new Shape
        {
            name    = "hexágono",
            entrada = new[] { "..######..", ".########." },
            cuerpo  = new[] { "##########" },
            cierre  = new[] { ".########.", "..######..", "...####..." },
        },

        new Shape
        {
            name    = "bloque redondeado",
            entrada = new[] { ".########.", "##########" },
            cuerpo  = new[] { "##########" },
            cierre  = new[] { "##########", ".########." },
        },

        new Shape
        {
            name    = "domo",
            entrada = new[] { "...####...", "..######..", ".########." },
            cuerpo  = new[] { "##########" },
            cierre  = new[] { "##########", "##########" },
        },
    };

    // Las filas de la figura estiradas a la altura pedida: entrada, el cuerpo repetido las veces
    // que haga falta, y el cierre. Si la altura no alcanza ni para entrada y cierre, se recorta el
    // cierre — antes que deformar la entrada, que es lo que sostiene el nivel.
    public static List<string> Expand(Shape shape, int rows)
    {
        var result = new List<string>(shape.entrada);

        int room = rows - shape.entrada.Length - shape.cierre.Length;
        for (int i = 0; i < room; i++)
            result.Add(shape.cuerpo[i % shape.cuerpo.Length]);

        foreach (var row in shape.cierre)
        {
            if (result.Count >= rows) break;
            result.Add(row);
        }

        return result;
    }
}
