using System;
using UnityEngine;

// Modelo del JSON de capítulo (Resources/Chapters/Chapter_N.json).
//
// Vive aparte de los niveles a propósito: LevelMapController y WorldSphereNodePositioner cargan
// TODO lo que haya en Resources/Levels y lo parsean como LevelData, así que un archivo de capítulo
// ahí adentro aparecería como un nivel fantasma con id 0.
//
// Un capítulo describe el MUNDO: qué suelo tiene su isla, qué decoraciones hay a los costados del
// camino y, más adelante, fondo y música. Los niveles siguen siendo archivos aparte.
[Serializable]
public class ChapterData
{
    public int    chapter;

    // El nombre en español; para mostrarlo se usa DisplayName. Mismo criterio que LevelData.
    public string name;

    public string NameKey     => $"chapter.{chapter}.name";
    public string DisplayName => LocaleManager.Get(NameKey, name);

    // Qué suelo usa la isla de este capítulo. Es un id de la lista de materiales de
    // WorldMapGround, no una ruta: el JSON no puede referenciar assets de Unity.
    //
    // Vacío o ausente = el suelo por defecto, así que agregarlo en un capítulo no obliga a
    // agregarlo en todos.
    public string ground;

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

    // Dónde va a lo largo del capítulo, medido en índice de nivel: 0 = el primero, 2.5 = entre
    // el tercero y el cuarto. Admite negativos y mayores para adornar antes o después.
    //
    // Es la forma cruda. Para colocar algo junto a un nodo concreto es más cómodo 'node'.
    public float at;

    // El número de nodo tal como se ve en el mapa: 1 es el primer nivel del capítulo. En 0
    // (o sin poner) manda 'at'.
    //
    // Existe porque 'at' es un índice que arranca en 0, así que "al lado del nivel 7" se escribe
    // 6 — y equivocarse en uno es casi inevitable colocando doscientas piezas a mano.
    public int node;

    // Con 'node' puesto, la corre a mitad de camino entre ese nodo y el anterior:
    // node 2 + middle cae entre el 1 y el 2.
    public bool middle;

    // Las dos puntas del capítulo, sin tener que saber cuántos niveles tiene:
    //
    //   start  ->  un nodo ANTES del primero
    //   end    ->  medio nodo DESPUÉS del último
    //
    // Solas ya colocan la pieza; 'at' es opcional y las corre desde ahí. Con 'node' puesto, 'end'
    // cambia de sentido y cuenta hacia atrás: 'node: 1, end: true' es el último nivel.
    //
    // Existen porque el marco de entrada y el de cierre se ponen una vez y tienen que seguir en
    // su sitio aunque después se le agreguen niveles al capítulo.
    public bool start;
    public bool end;

    // Lo que realmente se usa para posicionar, en índice de nivel dentro del capítulo.
    public float IndexIn(int levelsInChapter)
    {
        float lastIndex = Mathf.Max(0, levelsInChapter - 1);

        // Con 'start' o 'end' a secas, 'middle' empuja medio nodo MÁS AFUERA. Es al revés que en
        // el caso de 'node', donde tira hacia el nodo anterior — pero estas dos son las puntas
        // del capítulo y se piensan como sujetalibros: "medio nodo más allá" en ambos sentidos.
        if (end)
            return node > 0
                ? lastIndex - (node - 1) - (middle ? 0.5f : 0f)   // contando hacia atrás
                : lastIndex + 0.5f + (middle ? 0.5f : 0f) + at;   // medio nodo (o uno) después del último

        if (start)
            return node > 0
                ? (node - 1) - (middle ? 0.5f : 0f)
                : -1f - (middle ? 0.5f : 0f) + at;                // un nodo (y medio) antes del primero

        return node > 0
            ? (node - 1) - (middle ? 0.5f : 0f)
            : at;
    }

    // "left" o "right".
    public string side = "left";

    // A cuántas unidades de MUNDO del centro del camino.
    public float offset = 3f;

    // Multiplicador sobre el tamaño que trae el catálogo. 1 = tal cual.
    public float scale = 1f;

    // Espeja el sprite, para que el mismo elemento repetido no se note.
    public bool flip;
}
