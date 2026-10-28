using UnityEngine;

// Lo que puede quedar atrapado en una burbuja, se llame criatura u objeto: el ÚNICO sitio que
// contesta por los dos catálogos.
//
// Existe porque quien pregunta no sabe —ni tiene por qué saber— de cuál sale el id. Un nivel
// escribe 'holds: coqui' y una receta escribe 'holds: chest', las dos a secas. Si cada sitio
// tuviera que elegir a qué catálogo preguntarle, el nivel tendría que declarar además "esto es
// una criatura", y entonces el id y esa declaración podrían contradecirse.
//
// Así, separar los catálogos fue mover archivos: ni los niveles ni las recetas ni el tablero se
// enteraron.
public static class Rescuable
{
    // Lo que se usa cuando una entrada no dijo a qué velocidad pasa sus cuadros. El mismo que
    // trae el Inspector por defecto, para que no haya dos respuestas a la misma pregunta.
    const float DEFAULT_FPS = 8f;

    // De qué catálogo es este id. Null si no está en ninguno, que es justo cuando hace falta el
    // relleno genérico.
    //
    // Primero las criaturas y luego los objetos, que es el mismo orden que tenían las dos listas
    // cuando vivían en un solo asset. Como los ids son únicos entre los dos, el orden solo decide
    // cuál se mira antes y nunca qué se encuentra — salvo con un id repetido, y de eso avisa el
    // OnValidate de ObjectCatalog.
    static RescuableCatalog From(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var creatures = CreatureCatalog.Instance;
        if (creatures != null && creatures.Has(id)) return creatures;

        var objects = ObjectCatalog.Instance;
        if (objects != null && objects.Has(id)) return objects;

        return null;
    }

    // El relleno del catálogo al que pertenezca, y el de criaturas si no pertenece a ninguno:
    // algo tiene que dibujarse, y una burbuja vacía es peor que un icono que no es el suyo.
    static Sprite Generic(string id) =>
        From(id)?.GenericIcon
        ?? CreatureCatalog.Instance?.GenericIcon
        ?? ObjectCatalog.Instance?.GenericIcon;

    // El arte del tablero, ya resuelto: los cuadros propios si los tiene, el icono si no, y el
    // genérico como último recurso. Devuelve vacío solo cuando no hay ni genérico, que es cuando
    // de verdad no hay nada que dibujar.
    public static Sprite[] Frames(string id)
    {
        var frames = From(id)?.FramesFor(id);

        if (frames != null) return frames;

        var generic = Generic(id);

        return generic != null ? new[] { generic } : System.Array.Empty<Sprite>();
    }

    public static Sprite Icon(string id) => From(id)?.IconFor(id) ?? Generic(id);

    public static float Fps(string id)
    {
        float fps = From(id)?.FpsFor(id) ?? 0f;

        return fps > 0f ? fps : DEFAULT_FPS;
    }

    public static AudioClip RescueClip(string id) => From(id)?.RescueClipFor(id);

    // Si es una criatura y no un objeto. Lo pregunta la gelatina —un cofre no respira— y lo va a
    // preguntar el santuario: una perla rescatada suma al objetivo del nivel, pero no se queda a
    // vivir en el acuario.
    //
    // La respuesta es EN QUÉ ARCHIVO está, no un campo que haya que acordarse de marcar.
    public static bool IsCreature(string id)
    {
        var creatures = CreatureCatalog.Instance;

        return creatures != null && creatures.Has(id);
    }

    // Si está en alguno de los dos. Lo usan el desplegable del Inspector y el OnValidate de
    // RescueBubbleCatalog, para marcar lo que sobra sin borrarlo.
    public static bool Exists(string id) => From(id) != null;

    public static string[] CreatureIds =>
        CreatureCatalog.Instance != null ? CreatureCatalog.Instance.Ids : System.Array.Empty<string>();

    public static string[] ObjectIds =>
        ObjectCatalog.Instance != null ? ObjectCatalog.Instance.Ids : System.Array.Empty<string>();

    // El nombre traducido, con el id crudo de reserva: en pantalla es peor un hueco en blanco que
    // un "coqui" sin traducir, que al menos dice cuál falta.
    //
    // La misma clave para criaturas y objetos. Son dos catálogos pero una sola pregunta —cómo se
    // llama esto—, y dos prefijos serían dos sitios donde buscar en el CSV.
    public static string Name(string id) =>
        string.IsNullOrEmpty(id) ? "" : LocaleManager.Get($"creature.{id}.name", id);
}
