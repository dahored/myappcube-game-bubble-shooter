using System;

// Modelo del JSON de capítulo (Resources/Chapters/Chapter_N.json).
//
// Vive aparte de los niveles a propósito: LevelMapController y WorldSphereNodePositioner cargan
// TODO lo que haya en Resources/Levels y lo parsean como LevelData, así que un archivo de capítulo
// ahí adentro aparecería como un nivel fantasma con id 0.
//
// Un capítulo describe el MUNDO: qué decoraciones tiene a los costados del camino y, más adelante,
// texturas de suelo, fondo y música. Los niveles siguen siendo archivos aparte.
[Serializable]
public class ChapterData
{
    public int    chapter;
    public string name;

    public DecorationPlacement[] decorations;
}

// Una decoración puesta a mano sobre el camino.
//
// La posición NO se guarda en grados ni en coordenadas de la esfera, sino anclada a los niveles:
// así, si cambia la separación entre nodos, el zigzag o el radio del mundo, las decoraciones se
// mueven solas con el camino en vez de quedar flotando al costado.
[Serializable]
public class DecorationPlacement
{
    // Id del catálogo (DecorationCatalog), no una ruta a un archivo.
    public string id;

    // Dónde va a lo largo del capítulo, medido en niveles: 0 = el primero, 2.5 = entre el
    // tercero y el cuarto. Admite valores negativos y mayores para adornar antes o después.
    public float at;

    // "left" o "right".
    public string side = "left";

    // A cuántas unidades de MUNDO del centro del camino.
    public float offset = 3f;

    // Multiplicador sobre el tamaño que trae el catálogo. 1 = tal cual.
    public float scale = 1f;

    // Espeja el sprite, para que el mismo elemento repetido no se note.
    public bool flip;
}
