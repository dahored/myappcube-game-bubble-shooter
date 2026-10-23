using System.Collections.Generic;
using UnityEngine;

// El inventario de lo que puede quedar atrapado en una burbuja: las criaturas con nombre y los
// objetos. Solo arte e identidad — qué se dibuja y cómo se llama.
//
// Lo que se COMPONE con esto (fondo, brillo, capas de hielo) vive en RescueBubbleCatalog. Son dos
// trabajos distintos: añadir un caballito de mar se hace una vez, y "caballito bajo doble hielo"
// tantas veces como variantes pida el diseño de niveles. Mezclados serían la misma operación.
//
// Los niveles y las recetas los nombran por 'id' ("coqui", "pearl"), así que este asset es lo que
// convierte ese texto en algo que se puede dibujar. Un id que no esté acá no rompe nada: sale con
// el arte genérico y se avisa por consola.
[CreateAssetMenu(fileName = "CreatureCatalog", menuName = "Coralia/Creature Catalog")]
public class CreatureCatalog : ScriptableObject
{
    // Dónde se busca dentro de Resources. Si se renombra el asset, el catálogo deja de
    // encontrarse y todo cae al arte genérico.
    public const string RESOURCE_PATH = "CreatureCatalog";

    [System.Serializable]
    public class Entry
    {
        [Tooltip("Cómo lo nombran el JSON del nivel y las recetas de burbuja. Texto libre en minúsculas y sin espacios ('coqui', 'sea_glass').\n\nElegir uno ESTABLE: cambiarlo deja sin arte a todo lo que ya lo escribió.")]
        public string id;

        [Tooltip("El icono del hub de objetivos, arriba. Pequeño y plano: se ve a 60 píxeles junto a un número.")]
        public Sprite icon;

        [Tooltip("Cómo se ve DENTRO de la burbuja que lo atrapa, en el tablero.\n\nUn solo sprite se queda quieto; varios se pasan en bucle a 'Frames Per Second'. Vacío usa el icono, que siempre se ve mejor que nada.")]
        public Sprite[] frames;

        [Tooltip("A cuántos cuadros por segundo pasan los de arriba. Solo cuenta con más de uno.")]
        [Range(1f, 24f)]
        public float framesPerSecond = 8f;

        [Tooltip("Lo que suena al liberarlo. Es el momento que el jugador vino a buscar: sin sonido propio se confunde con una burbuja más que revienta.")]
        public AudioClip rescueClip;

        // El nombre sale de las traducciones y no de un campo: escribirlo acá sería la versión
        // que nadie traduce. La clave se deriva del id, igual que la de los niveles.
        public string NameKey => $"creature.{id}.name";
    }

    // Dos listas y no una sola con un campo "es criatura": así el Inspector separa lo que son dos
    // cosas distintas —doce bichos con nombre y personalidad frente a cacharros del fondo del
    // mar— y además queda contestada una pregunta que el santuario va a hacer, que es a cuál de
    // los dos se le monta una ficha de bestiario.
    //
    // Los ids son ÚNICOS entre las dos: quien los pide escribe un id a secas y no dice de qué
    // lista sale. Uno repetido se avisa por consola.
    [Tooltip("Las criaturas con nombre (GDD §13.3). Son las que el jugador recuerda y las que van al santuario.")]
    [SerializeField] Entry[] creatures;

    [Tooltip("Cosas sin nombre que también se rescatan: perlas, caracolas, cofres. Cuentan para el objetivo igual que una criatura, pero no se coleccionan.")]
    [SerializeField] Entry[] objects;

    [Tooltip("Con qué se dibuja algo que no tenga entrada, o que la tenga sin arte. Es la red de seguridad para diseñar niveles antes de que exista el arte definitivo.")]
    [SerializeField] Sprite genericIcon;

    public Sprite GenericIcon => genericIcon;

    // Los ids de cada lista, para que el Inspector pueda ofrecerlos en un desplegable en vez de
    // obligar a escribirlos a mano. Un id escrito con un dedazo no falla al guardar: falla en
    // partida, con la criatura saliendo genérica y sin que nada diga por qué.
    public string[] CreatureIds => Ids(creatures);
    public string[] ObjectIds   => Ids(objects);

    static string[] Ids(Entry[] list)
    {
        if (list == null) return System.Array.Empty<string>();

        var ids = new List<string>(list.Length);

        foreach (var entry in list)
            if (entry != null && !string.IsNullOrWhiteSpace(entry.id)) ids.Add(entry.id);

        return ids.ToArray();
    }

    public Entry EntryFor(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        return Find(creatures, id) ?? Find(objects, id);
    }

    // Si ese id es una criatura y no un objeto. Lo va a preguntar el santuario: una perla
    // rescatada suma al objetivo del nivel, pero no se queda a vivir en el acuario.
    public bool IsCreature(string id) => Find(creatures, id) != null;

    static Entry Find(Entry[] list, string id)
    {
        if (list == null) return null;

        foreach (var entry in list)
            if (entry != null && entry.id == id) return entry;

        return null;
    }

    // El arte del tablero, ya resuelto: los cuadros propios si los tiene, el icono si no, y el
    // genérico como último recurso. Devuelve vacío solo si el catálogo no tiene ni genérico, que
    // es cuando de verdad no hay nada que dibujar.
    public Sprite[] FramesFor(string id)
    {
        var entry = EntryFor(id);

        if (entry?.frames != null && entry.frames.Length > 0) return entry.frames;

        var single = entry?.icon != null ? entry.icon : genericIcon;

        return single != null ? new[] { single } : System.Array.Empty<Sprite>();
    }

    public Sprite IconFor(string id)
    {
        var entry = EntryFor(id);

        return entry?.icon != null ? entry.icon : genericIcon;
    }

    public float FpsFor(string id)
    {
        var entry = EntryFor(id);

        return entry != null && entry.framesPerSecond > 0f ? entry.framesPerSecond : 8f;
    }

    public AudioClip RescueClipFor(string id) => EntryFor(id)?.rescueClip;

    // ---- Acceso global ---------------------------------------------------------------------
    //
    // Se cachea el resultado INCLUIDO el fallo, igual que el catálogo de poderes: un nivel sin
    // catálogo reintentaría un Resources.Load por cada criatura y por cada refresco del hub.
    static CreatureCatalog _instance;
    static bool            _looked;

    public static CreatureCatalog Instance
    {
        get
        {
            if (_looked) return _instance;

            _looked   = true;
            _instance = Resources.Load<CreatureCatalog>(RESOURCE_PATH);

            if (_instance == null)
                Debug.LogWarning($"[CreatureCatalog] No se encontró 'Resources/{RESOURCE_PATH}': los niveles de rescate van a salir sin arte.");

            return _instance;
        }
    }

    public static Sprite[]  Frames(string id)     => Instance != null ? Instance.FramesFor(id)     : System.Array.Empty<Sprite>();
    public static Sprite    Icon(string id)       => Instance != null ? Instance.IconFor(id)       : null;
    public static float     Fps(string id)        => Instance != null ? Instance.FpsFor(id)        : 8f;
    public static AudioClip RescueClip(string id) => Instance != null ? Instance.RescueClipFor(id) : null;
    public static bool      Creature(string id)   => Instance != null && Instance.IsCreature(id);

    // El nombre traducido, con el id crudo de reserva: en pantalla es peor un hueco en blanco que
    // un "coqui" sin traducir, que al menos dice cuál falta.
    public static string Name(string id) =>
        string.IsNullOrEmpty(id) ? "" : LocaleManager.Get($"creature.{id}.name", id);

#if UNITY_EDITOR
    void OnValidate()
    {
        // Las dos listas a la vez: el id tiene que ser único ENTRE AMBAS, porque quien lo pide lo
        // escribe a secas y no dice de cuál sale.
        var seen = new HashSet<string>();

        Check(creatures, seen);
        Check(objects,   seen);

        if (genericIcon == null)
            Debug.LogWarning("[CreatureCatalog] Falta 'Generic Icon': algo sin arte propio va a salir invisible en vez de con un relleno.", this);
    }

    void Check(Entry[] list, HashSet<string> seen)
    {
        if (list == null) return;

        foreach (var entry in list)
        {
            if (entry == null) continue;

            if (string.IsNullOrWhiteSpace(entry.id))
                Debug.LogWarning("[CreatureCatalog] Hay una entrada sin 'id': nada va a poder pedirla.", this);
            else if (!seen.Add(entry.id))
                Debug.LogWarning($"[CreatureCatalog] '{entry.id}' está dos veces. Solo se usa la primera.", this);
        }
    }
#endif
}
