using System.Collections.Generic;
using UnityEngine;

// Las recetas de burbuja de rescate: qué lleva dentro, con qué fondo y bajo qué capas.
//
// Va SEPARADO de los catálogos de criaturas y de objetos porque son dos cosas distintas.
// Aquellos son el inventario de arte —los bichos, los cacharros, cada uno con sus sprites— y
// este es lo que se compone con ese arte. Mezclados, añadir un caballito de mar y añadir "caballito bajo doble
// hielo" serían la misma operación, y no lo son: lo primero se hace una vez y lo segundo tantas
// veces como variantes pida el diseño de niveles.
//
// Y existe para que el nivel no repita la receta en cada celda. Con treinta niveles de rescate,
// "coquí bajo doble hielo" escrito treinta veces son treinta sitios donde cambiarlo, y el que se
// olvide queda distinto sin que nada avise.
[CreateAssetMenu(fileName = "RescueBubbleCatalog", menuName = "Coralia/Rescue Bubble Catalog")]
public class RescueBubbleCatalog : ScriptableObject
{
    public const string RESOURCE_PATH = "RescueBubbleCatalog";

    [System.Serializable]
    public class Recipe
    {
        [Tooltip("Cómo la nombra el nivel. Algo que se lea solo: 'coqui_hielo', 'perla_libre'.")]
        public string id;

        [Tooltip("Qué lleva dentro. Se elige de lo que haya en los catálogos de criaturas y de objetos.")]
        [RescuableId]
        public string holds;

        [Tooltip("El fondo que va DETRÁS de lo que lleva dentro. Opcional: vacío deja ver el agua.")]
        public Sprite background;

        [Tooltip("El brillo del cristal, DELANTE de todo. Vacío usa el 'Shared Glow' de abajo, que es lo normal: el brillo es de la burbuja y no de lo que lleve.")]
        public Sprite glow;

        // Tres campos y no una lista: el tope son tres y así el Inspector lo dice solo, sin que
        // haya que acordarse de la regla ni avisar de una cuarta que no se va a dibujar.
        //
        // De dentro hacia afuera, y los huecos no se saltan: una capa 2 con la 1 vacía se lee
        // como un error al montarla, no como "quiero solo la de fuera".
        [Tooltip("Lo que suena al reventar ESTA burbuja. Opcional: vacío usa el 'Shared Burst Clip' de abajo, y si tampoco hay, el pop corriente.\n\nVa aparte del 'Rescue Clip' de la criatura: uno es el cristal rompiéndose y el otro el bicho saliendo. Suenan los dos.")]
        public AudioClip burstClip;

        [Tooltip("La capa más pegada a la criatura, la ÚLTIMA en romperse. Cada capa es UN golpe.")]
        public Sprite layer1;

        [Tooltip("Lo que suena al romperse ESTA capa. Vacío usa el 'Burst Clip' de arriba, y si tampoco hay, el 'Shared Burst Clip'.")]
        public AudioClip layer1Clip;

        [Tooltip("La de en medio. Vacía si solo lleva una.")]
        public Sprite layer2;

        [Tooltip("Lo que suena al romperse ESTA capa. Vacío usa el 'Burst Clip' de arriba, y si tampoco hay, el 'Shared Burst Clip'.")]
        public AudioClip layer2Clip;

        [Tooltip("La de fuera, la PRIMERA en romperse.")]
        public Sprite layer3;

        [Tooltip("Lo que suena al romperse ESTA capa. Vacío usa el 'Burst Clip' de arriba, y si tampoco hay, el 'Shared Burst Clip'.")]
        public AudioClip layer3Clip;

        // Cuánto cuesta abrirla: tantos golpes como capas tenga puestas.
        public int Hits => (layer1 != null ? 1 : 0) + (layer2 != null ? 1 : 0) + (layer3 != null ? 1 : 0);

        // De DENTRO hacia afuera, que es el orden en que se dibujan: la primera de la lista va
        // pegada a la criatura y la última queda encima de todas, que es la que se rompe primero.
        //
        // Iba al revés, y como las capas se dibujan en el orden de la lista, la 1 —la que el
        // Inspector llama "la más pegada a la criatura"— acababa por fuera y caía antes que la 3.
        public Sprite[] Layers()
        {
            var list = new List<Sprite>(3);

            if (layer1 != null) list.Add(layer1);
            if (layer2 != null) list.Add(layer2);
            if (layer3 != null) list.Add(layer3);

            return list.ToArray();
        }

        // Los clips de esas mismas capas, EN EL MISMO ORDEN. Van en una lista aparte y no junto
        // al sprite porque lo que recorre el juego es la lista de capas puestas, no los campos:
        // así el clip de la capa que se rompe se busca con el mismo índice y no hay nada que
        // emparejar a mano.
        public AudioClip[] LayerClips()
        {
            var list = new List<AudioClip>(3);

            if (layer1 != null) list.Add(layer1Clip);
            if (layer2 != null) list.Add(layer2Clip);
            if (layer3 != null) list.Add(layer3Clip);

            return list.ToArray();
        }
    }

    [Tooltip("Una receta por combinación que uses de verdad, no una por criatura.")]
    [SerializeField] Recipe[] recipes;

    [Tooltip("El brillo que usan las recetas que no traen el suyo. Es el del cristal de una burbuja normal: por defecto lo llevan todas y por eso se pone una sola vez acá.")]
    [SerializeField] Sprite sharedGlow;

    [Tooltip("Lo que suena al reventar una burbuja de rescate que no traiga el suyo. Vacío deja el pop corriente.")]
    [SerializeField] AudioClip sharedBurstClip;

    public Sprite GlowFor(Recipe recipe) => recipe?.glow != null ? recipe.glow : sharedGlow;

    public AudioClip BurstFor(Recipe recipe) => recipe?.burstClip != null ? recipe.burstClip : sharedBurstClip;

    // Lo que suena al romperse la capa número 'index' de esa receta, contando desde la de dentro.
    // Sin clip propio cae en el de la receta, y sin ese en el común: una capa siempre suena.
    public AudioClip LayerBurstFor(Recipe recipe, int index)
    {
        var clips = recipe != null ? recipe.LayerClips() : null;

        return clips != null && index >= 0 && index < clips.Length && clips[index] != null
             ? clips[index]
             : BurstFor(recipe);
    }

    public static AudioClip Burst(Recipe recipe) => Instance != null ? Instance.BurstFor(recipe) : null;

    public static AudioClip LayerBurst(Recipe recipe, int index) =>
        Instance != null ? Instance.LayerBurstFor(recipe, index) : null;

    public static Sprite Glow(Recipe recipe) => Instance != null ? Instance.GlowFor(recipe) : null;

    public Recipe[] All => recipes;

    public Recipe For(string id)
    {
        if (recipes == null || string.IsNullOrEmpty(id)) return null;

        foreach (var recipe in recipes)
            if (recipe != null && recipe.id == id) return recipe;

        return null;
    }

    // ---- Acceso global ---------------------------------------------------------------------
    //
    // Se cachea el resultado INCLUIDO el fallo: un nivel sin catálogo reintentaría un
    // Resources.Load por cada burbuja de rescate que coloque.
    static RescueBubbleCatalog _instance;
    static bool                _looked;

    public static RescueBubbleCatalog Instance
    {
        get
        {
            if (_looked) return _instance;

            _looked   = true;
            _instance = Resources.Load<RescueBubbleCatalog>(RESOURCE_PATH);

            return _instance;
        }
    }

    // Sin aviso cuando no existe: un juego que todavía no tiene niveles de rescate no tiene por
    // qué quejarse en cada arranque. Quien sí avisa es el nivel que pide una receta por nombre y
    // no la encuentra, que es cuando falta algo de verdad.
    public static Recipe Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var recipe = Instance != null ? Instance.For(id) : null;

        if (recipe == null)
            Debug.LogWarning($"[RescueBubbleCatalog] Un nivel pide la receta '{id}' y no está en Resources/{RESOURCE_PATH}: esa burbuja va a salir con lo que tenga escrito a mano, o vacía.");

        return recipe;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (recipes == null) return;

        var seen = new HashSet<string>();

        foreach (var recipe in recipes)
        {
            if (recipe == null) continue;

            if (string.IsNullOrWhiteSpace(recipe.id))
                Debug.LogWarning("[RescueBubbleCatalog] Hay una receta sin 'id': el editor de niveles no va a poder ofrecerla.", this);
            else if (!seen.Add(recipe.id))
                Debug.LogWarning($"[RescueBubbleCatalog] '{recipe.id}' está dos veces. Solo se usa la primera.", this);

            // Se comprueba contra el inventario: una receta que lleva algo inexistente sale con
            // el icono genérico y se ve en el juego, no acá.
            if (!string.IsNullOrWhiteSpace(recipe.holds) && !Rescuable.Exists(recipe.holds))
                Debug.LogWarning($"[RescueBubbleCatalog] '{recipe.id}' lleva '{recipe.holds}' y eso no está ni en el CreatureCatalog ni en el ObjectCatalog.", this);

            // Un hueco salteado es un error de montaje: las capas van de dentro hacia afuera y
            // dejar la 1 vacía con la 2 puesta no significa nada.
            if (recipe.layer1 == null && (recipe.layer2 != null || recipe.layer3 != null))
                Debug.LogWarning($"[RescueBubbleCatalog] '{recipe.id}' tiene capas puestas pero la 'Layer 1' vacía: las capas se llenan desde la primera.", this);
            else if (recipe.layer2 == null && recipe.layer3 != null)
                Debug.LogWarning($"[RescueBubbleCatalog] '{recipe.id}' tiene 'Layer 3' con la 2 vacía: las capas se llenan en orden.", this);
        }
    }
#endif
}
