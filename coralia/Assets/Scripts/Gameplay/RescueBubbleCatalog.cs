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

        [Tooltip("Lo que suena al reventar ESTA burbuja. Opcional: vacío usa el 'Shared Burst Clip' de abajo, y si tampoco hay, el pop corriente.\n\nVa aparte del 'Rescue Clip' de la criatura: uno es el cristal rompiéndose y el otro el bicho saliendo. Suenan los dos.")]
        public AudioClip burstClip;

        // El material y el grosor, en vez de los tres sprites sueltos que había antes. Los
        // sprites salen del ObstacleCatalog: así cambiar el arte del hielo se hace una vez y no
        // en las quince recetas que lo usan, donde la que se olvidara quedaría distinta sin que
        // nada avise.
        [Tooltip("De qué está hecho el caparazón. Vacío la deja sin caparazón: se abre al primer golpe.")]
        [ObstacleId]
        public string obstacle;

        [Tooltip("Cuántos golpes cuesta abrirla. Se usan las PRIMERAS capas del material, que van de menos a más: uno arranca hecho pedazos y tres arranca entero, así el grosor se ve sin tener que golpearlo.\n\nCero la deja sin caparazón, igual que dejar el material vacío.")]
        [Range(0, ObstacleCatalog.MAX_LAYERS)]
        public int layers;

        // Cuánto cuesta abrirla DE VERDAD: un material sin arte puesto no da golpes, por mucho
        // que la receta pida tres.
        public int Hits => Layers().Length;

        // De DENTRO hacia afuera, que es el orden en que se dibujan: la primera de la lista va
        // pegada a la criatura y la última queda encima de todas, que es la que se rompe primero.
        public Sprite[] Layers() => ObstacleCatalog.Layers(obstacle, layers);
    }

    [Tooltip("Una receta por combinación que uses de verdad, no una por criatura.")]
    [SerializeField] Recipe[] recipes;

    [Tooltip("El brillo que usan las recetas que no traen el suyo. Es el del cristal de una burbuja normal: por defecto lo llevan todas y por eso se pone una sola vez acá.")]
    [SerializeField] Sprite sharedGlow;

    [Tooltip("Lo que suena al reventar una burbuja de rescate que no traiga el suyo. Vacío deja el pop corriente.")]
    [SerializeField] AudioClip sharedBurstClip;

    public Sprite GlowFor(Recipe recipe) => recipe?.glow != null ? recipe.glow : sharedGlow;

    public AudioClip BurstFor(Recipe recipe) => recipe?.burstClip != null ? recipe.burstClip : sharedBurstClip;

    // Lo que suena al romperse una capa. Lo pone el MATERIAL y no la receta: romper hielo suena
    // a hielo, sin que importe qué haya debajo. Sin clip propio cae en el de la receta, y sin
    // ese en el común — una capa siempre suena.
    public AudioClip LayerBurstFor(Recipe recipe)
    {
        var clip = recipe != null ? ObstacleCatalog.BreakClip(recipe.obstacle) : null;

        return clip != null ? clip : BurstFor(recipe);
    }

    public static AudioClip Burst(Recipe recipe) => Instance != null ? Instance.BurstFor(recipe) : null;

    public static AudioClip LayerBurst(Recipe recipe) =>
        Instance != null ? Instance.LayerBurstFor(recipe) : null;

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

            // Pedir capas de un material que no existe deja la burbuja sin caparazón, y eso se
            // ve jugando y no acá: la criatura se abre al primer golpe sin que nada lo explique.
            if (recipe.layers > 0 && string.IsNullOrWhiteSpace(recipe.obstacle))
                Debug.LogWarning($"[RescueBubbleCatalog] '{recipe.id}' pide {recipe.layers} capa(s) pero no dice de qué material: va a salir sin caparazón.", this);
            else if (!string.IsNullOrWhiteSpace(recipe.obstacle) && !ObstacleCatalog.Exists(recipe.obstacle))
                Debug.LogWarning($"[RescueBubbleCatalog] '{recipe.id}' pide el material '{recipe.obstacle}' y eso no está en el ObstacleCatalog.", this);
            else if (recipe.layers > 0 && recipe.Hits < recipe.layers)
                Debug.LogWarning($"[RescueBubbleCatalog] '{recipe.id}' pide {recipe.layers} capa(s) de '{recipe.obstacle}' y ese material solo tiene {recipe.Hits}: se queda en {recipe.Hits}.", this);
        }
    }
#endif
}
