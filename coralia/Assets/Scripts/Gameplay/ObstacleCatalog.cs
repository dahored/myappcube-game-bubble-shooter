using System.Collections.Generic;
using UnityEngine;

// Los materiales de los que puede estar hecho un caparazón: hielo, metal, madera, piedra y
// cristal (GDD §2.3). Solo arte y sonido — qué se dibuja al romperse y cómo suena.
//
// Va aparte de RescueBubbleCatalog por lo mismo que las criaturas: aquel es lo que se COMPONE
// —qué lleva dentro, bajo qué caparazón y de cuántas capas— y este es el inventario con el que
// se compone. Mezclados, cambiar el arte del hielo obligaría a abrir las quince recetas que lo
// usan, y la que se olvidara quedaría distinta sin que nada avise.
//
// Cinco materiales por tres grosores son quince combinaciones, pero NO quince assets: la receta
// dice "hielo, 3 capas" y de acá salen los tres sprites.
[CreateAssetMenu(fileName = "ObstacleCatalog", menuName = "Coralia/Obstacle Catalog")]
public class ObstacleCatalog : ScriptableObject
{
    public const string RESOURCE_PATH = "ObstacleCatalog";

    // El tope son tres capas, igual que en las recetas: más no se dibujan.
    public const int MAX_LAYERS = 3;

    [System.Serializable]
    public class Entry
    {
        [Tooltip("Cómo lo nombran las recetas. Texto libre en minúsculas y sin espacios, igual que los sprites: 'ice', 'metal', 'wood', 'stone', 'glass'.\n\nElegir uno ESTABLE: cambiarlo deja sin caparazón a toda receta que ya lo escribió.")]
        public string id;

        [Tooltip("Los estados del material, de DENTRO hacia afuera: primero los fragmentos sueltos, luego el anillo roto, y al final la esfera entera.\n\nEse orden es el de los nombres de los sprites ('bubble_ice_1', '_2', '_3') y al revés del de rotura: lo que se ve al empezar es el ÚLTIMO de la lista, porque se dibuja encima de todos.")]
        public Sprite[] layers;

        [Tooltip("Lo que suena al romperse una capa de este material. Va acá y no en la receta porque romper hielo suena a hielo, sin que importe qué haya debajo.\n\nVacío cae en el sonido de la receta, y sin ese en el pop corriente.")]
        public AudioClip breakClip;
    }

    [Tooltip("Uno por material, no uno por grosor. El grosor lo elige cada receta.")]
    [SerializeField] Entry[] entries;

    public string[] Ids
    {
        get
        {
            if (entries == null) return System.Array.Empty<string>();

            var ids = new List<string>(entries.Length);

            foreach (var entry in entries)
                if (entry != null && !string.IsNullOrWhiteSpace(entry.id)) ids.Add(entry.id);

            return ids.ToArray();
        }
    }

    public Entry EntryFor(string id)
    {
        if (string.IsNullOrEmpty(id) || entries == null) return null;

        foreach (var entry in entries)
            if (entry != null && entry.id == id) return entry;

        return null;
    }

    public bool Has(string id) => EntryFor(id) != null;

    // Las 'count' PRIMERAS capas del material, en el orden en que están puestas en el catálogo.
    //
    // Las capas van de menos a más: fragmentos sueltos, anillo, esfera entera. Así el grosor se
    // ve de un vistazo —uno arranca hecho pedazos y tres arranca entero— y el jugador sabe
    // cuántos golpes le cuesta antes de gastar el primero. Tomando las últimas, los tres grosores
    // arrancaban iguales y solo se distinguían golpeándolos (pedido de Diego).
    //
    // Se ROMPEN al revés, de la última a la primera, que es lo que deja ver cómo se desmorona:
    // entera, anillo, fragmentos.
    //
    // Devueltas de dentro hacia afuera, que es el orden en que las dibuja BubbleView.
    public Sprite[] LayersFor(string id, int count)
    {
        var entry = EntryFor(id);

        if (entry == null || entry.layers == null || count <= 0) return System.Array.Empty<Sprite>();

        var available = new List<Sprite>(entry.layers.Length);

        foreach (var sprite in entry.layers)
            if (sprite != null) available.Add(sprite);

        if (available.Count == 0) return System.Array.Empty<Sprite>();

        // Pedir más capas de las que tiene el material no inventa ninguna: se dan las que haya.
        // El aviso lo da OnValidate, que es donde se puede arreglar.
        int take = Mathf.Min(count, available.Count);

        return available.GetRange(0, take).ToArray();
    }

    public AudioClip BreakClipFor(string id) => EntryFor(id)?.breakClip;

    // ---- Acceso global ---------------------------------------------------------------------
    //
    // Se cachea el resultado INCLUIDO el fallo, igual que los otros catálogos: un nivel sin
    // catálogo reintentaría un Resources.Load por cada burbuja con caparazón que coloque.
    static ObstacleCatalog _instance;
    static bool            _looked;

    public static ObstacleCatalog Instance
    {
        get
        {
            if (_looked) return _instance;

            _looked   = true;
            _instance = Resources.Load<ObstacleCatalog>(RESOURCE_PATH);

            return _instance;
        }
    }

    public static Sprite[] Layers(string id, int count) =>
        Instance != null ? Instance.LayersFor(id, count) : System.Array.Empty<Sprite>();

    public static AudioClip BreakClip(string id) => Instance != null ? Instance.BreakClipFor(id) : null;

    // Cómo se llama este material, traducido. Lo piden los tutoriales y cualquier texto que tenga
    // que nombrarlo —"rompe el hielo"— y sin esto diría "ice" en los seis idiomas.
    //
    // Misma fórmula que el resto del proyecto: la clave sale del id y el nombre sale del CSV, en
    // vez de un campo en el asset que nadie traduce.
    public string NameKeyFor(string id) => $"obstacle.{id}.name";

    public static string NameKey(string id) =>
        Instance != null ? Instance.NameKeyFor(id) : $"obstacle.{id}.name";

    // Con el id crudo de reserva, igual que los rescatables: en pantalla es peor un hueco que un
    // "ice" sin traducir, que al menos dice cuál falta.
    public static string Name(string id) =>
        string.IsNullOrEmpty(id) ? "" : LocaleManager.Get(NameKey(id), id);

    public static bool Exists(string id) => Instance != null && Instance.Has(id);

    public static string[] All => Instance != null ? Instance.Ids : System.Array.Empty<string>();

#if UNITY_EDITOR
    void OnValidate()
    {
        if (entries == null) return;

        var seen = new HashSet<string>();

        foreach (var entry in entries)
        {
            if (entry == null) continue;

            if (string.IsNullOrWhiteSpace(entry.id))
            {
                Debug.LogWarning($"[ObstacleCatalog] Hay un material sin 'id': ninguna receta va a poder pedirlo.", this);
                continue;
            }

            if (!seen.Add(entry.id))
                Debug.LogWarning($"[ObstacleCatalog] '{entry.id}' está dos veces. Solo se usa el primero.", this);

            int puestos = 0;

            if (entry.layers != null)
                foreach (var sprite in entry.layers) if (sprite != null) puestos++;

            if (puestos == 0)
                Debug.LogWarning($"[ObstacleCatalog] '{entry.id}' no tiene ningún sprite: las recetas que lo pidan van a salir sin caparazón, y lo que llevan dentro se abrirá al primer golpe.", this);
            else if (puestos < MAX_LAYERS)
                Debug.LogWarning($"[ObstacleCatalog] '{entry.id}' tiene {puestos} sprite(s) y el tope son {MAX_LAYERS}: una receta que pida más capas se va a quedar en {puestos}.", this);
        }
    }
#endif
}
