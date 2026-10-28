using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// El inventario de lo que puede quedar atrapado en una burbuja: solo arte e identidad — qué se
// dibuja y cómo se llama. Nada de cómo se juega.
//
// Hay DOS catálogos que heredan de esto, el de criaturas y el de objetos, y están en archivos
// separados porque son dos trabajos distintos: una criatura tiene nombre, ficha de bestiario y se
// queda a vivir en el santuario; un objeto es un cacharro del fondo del mar que cuenta para el
// objetivo y ya. En un solo asset, tocar uno obligaba a abrir el otro.
//
// Lo que se COMPONE con esto —fondo, brillo, capas de hielo— vive en RescueBubbleCatalog. Añadir
// un caballito de mar se hace una vez; "caballito bajo doble hielo" tantas veces como variantes
// pida el diseño de niveles.
//
// OJO: quien busca un id NO sabe de cuál de los dos sale, y eso es a propósito — un nivel escribe
// 'holds: coqui' a secas. Quien contesta por los dos es Rescuable, y es el único sitio que
// debería saber que son dos.
public abstract class RescuableCatalog : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("Cómo lo nombran el JSON del nivel y las recetas de burbuja. Texto libre en minúsculas y sin espacios ('coqui', 'sea_glass').\n\nElegir uno ESTABLE: cambiarlo deja sin arte a todo lo que ya lo escribió. Y tiene que ser único entre LOS DOS catálogos, porque quien lo pide lo escribe a secas sin decir de cuál sale.")]
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

    // 'creatures' era como se llamaba cuando criaturas y objetos vivían en el mismo asset. El
    // nombre viejo se declara para que Unity migre solo lo que ya estaba escrito: sin esto, los
    // doce ids tecleados a mano se perderían en la primera recompilación.
    [FormerlySerializedAs("creatures")]
    [SerializeField] Entry[] entries;

    [Tooltip("Con qué se dibuja algo de este catálogo que no tenga arte propio. Es la red de seguridad para diseñar niveles antes de que exista el arte definitivo.\n\nVacío cae al relleno del OTRO catálogo, y si ese tampoco lo tiene, sale invisible.")]
    [SerializeField] Sprite genericIcon;

    public Sprite GenericIcon => genericIcon;

    // Los ids de este catálogo, para que el Inspector pueda ofrecerlos en un desplegable en vez
    // de obligar a escribirlos a mano. Un id escrito con un dedazo no falla al guardar: falla en
    // partida, con la criatura saliendo genérica y sin que nada diga por qué.
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

    // Si este id es de ESTE catálogo. Es la pregunta con la que Rescuable decide a cuál de los
    // dos preguntarle el resto, y la que contesta si algo es criatura u objeto: ahora la
    // respuesta es en qué archivo está, no un campo que haya que acordarse de marcar.
    public bool Has(string id) => EntryFor(id) != null;

    // Los cuatro devuelven vacío cuando el id NO es de este catálogo, en vez de caer al genérico.
    //
    // El relleno lo pone Rescuable, que es el que sabe que hay dos: puesto acá, el catálogo de
    // criaturas contestaría con SU genérico a la pregunta por un cofre, y el cofre saldría
    // dibujado como un bicho.
    public Sprite[] FramesFor(string id)
    {
        var entry = EntryFor(id);

        if (entry == null) return null;

        if (entry.frames != null && entry.frames.Length > 0) return entry.frames;

        return entry.icon != null ? new[] { entry.icon } : null;
    }

    public Sprite IconFor(string id) => EntryFor(id)?.icon;

    // Cero significa "no es mío o no lo dijo", y el valor por defecto lo pone Rescuable: acá no
    // se puede distinguir un id ajeno de uno propio sin fps, y los dos necesitan la misma
    // respuesta de quien pregunta.
    public float FpsFor(string id)
    {
        var entry = EntryFor(id);

        return entry != null && entry.framesPerSecond > 0f ? entry.framesPerSecond : 0f;
    }

    public AudioClip RescueClipFor(string id) => EntryFor(id)?.rescueClip;

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        var seen = new HashSet<string>();

        if (entries != null)
            foreach (var entry in entries)
            {
                if (entry == null) continue;

                if (string.IsNullOrWhiteSpace(entry.id))
                    Debug.LogWarning($"[{name}] Hay una entrada sin 'id': nada va a poder pedirla.", this);
                else if (!seen.Add(entry.id))
                    Debug.LogWarning($"[{name}] '{entry.id}' está dos veces. Solo se usa la primera.", this);
            }

        if (genericIcon == null)
            Debug.LogWarning($"[{name}] Falta 'Generic Icon': lo de acá sin arte propio cae al relleno del otro catálogo, y si ese tampoco lo tiene sale invisible.", this);
    }
#endif
}
