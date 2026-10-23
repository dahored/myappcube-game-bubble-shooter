using System.Collections.Generic;
using UnityEngine;

// El arte de las burbujas NORMALES, las de color. Uno de los tres catálogos de burbuja, junto con
// RescueBubbleCatalog y el de obstáculos: cada tipo de burbuja tiene el suyo, y los tres se usan
// al construir niveles.
//
// Estaba repartido en catorce campos sueltos del Inspector de GridController, uno por color. Con
// catorce, el que falte sale en blanco en partida y no hay forma de verlo antes; y el día que una
// burbuja necesite dos cuadros en vez de uno, hay que tocar el componente del tablero para algo
// que es arte.
//
// Va en Resources y no cableado en la escena para que cualquier vista nueva —la tienda, el
// bestiario, una pantalla de ayuda— pueda dibujar una burbuja preguntando por el color y ya.
[CreateAssetMenu(fileName = "BubbleCatalog", menuName = "Coralia/Bubble Catalog")]
public class BubbleCatalog : ScriptableObject
{
    public const string RESOURCE_PATH = "BubbleCatalog";

    [System.Serializable]
    public class Entry
    {
        [Tooltip("De qué color es esta burbuja. Es la clave: el nivel escribe 'red' en su JSON y acá se resuelve en arte.")]
        public BubbleColor color;

        [Tooltip("El cuerpo de la burbuja. Un solo sprite se queda quieto; varios se pasan en bucle a 'Frames Per Second'.\n\nVarios cuadros son para una burbuja que respira o que tiene un brillo que recorre: el color en sí no necesita animarse.")]
        public Sprite[] frames;

        [Tooltip("A cuántos cuadros por segundo pasan los de arriba. Solo cuenta con más de uno.")]
        [Range(1f, 24f)]
        public float framesPerSecond = 8f;
    }

    [Tooltip("Una entrada por color que uses. Los que no estén salen sin arte y se avisa por consola: mejor verlo al montar el nivel que descubrirlo jugándolo.")]
    [SerializeField] Entry[] entries;

    [Tooltip("El brillo del cristal que comparten todas. Mismo campo y mismo nombre que en los otros catálogos: es la misma idea en los tres.")]
    [SerializeField] Sprite sharedGlow;

    [Tooltip("Marcado: el brillo se dibuja como CAPA encima del cuerpo. Para eso los cuerpos tienen que estar exportados PLANOS, sin el brillo pintado dentro.\n\nDesmarcado (lo de ahora): cada cuerpo trae su propio brillo y la capa no se dibuja. Encenderlo con los sprites de hoy deja dos brillos encima.")]
    [SerializeField] bool glowAsLayer;

    public Sprite SharedGlow => glowAsLayer ? sharedGlow : null;

    public Entry EntryFor(BubbleColor color)
    {
        if (entries == null) return null;

        foreach (var entry in entries)
            if (entry != null && entry.color == color) return entry;

        return null;
    }

    public Sprite[] FramesFor(BubbleColor color) =>
        EntryFor(color)?.frames ?? System.Array.Empty<Sprite>();

    public Sprite SpriteFor(BubbleColor color)
    {
        var frames = FramesFor(color);

        return frames.Length > 0 ? frames[0] : null;
    }

    public float FpsFor(BubbleColor color)
    {
        var entry = EntryFor(color);

        return entry != null && entry.framesPerSecond > 0f ? entry.framesPerSecond : 8f;
    }

    // ---- Acceso global ---------------------------------------------------------------------
    static BubbleCatalog _instance;
    static bool          _looked;

    public static BubbleCatalog Instance
    {
        get
        {
            if (_looked) return _instance;

            _looked   = true;
            _instance = Resources.Load<BubbleCatalog>(RESOURCE_PATH);

            if (_instance == null)
                Debug.LogWarning($"[BubbleCatalog] No se encontró 'Resources/{RESOURCE_PATH}': las burbujas van a salir con lo que tenga el tablero puesto a mano.");

            return _instance;
        }
    }

    public static Sprite   Sprite(BubbleColor color) => Instance != null ? Instance.SpriteFor(color) : null;
    public static Sprite[] Frames(BubbleColor color) => Instance != null ? Instance.FramesFor(color) : System.Array.Empty<Sprite>();
    public static float    Fps(BubbleColor color)    => Instance != null ? Instance.FpsFor(color)    : 8f;
    public static Sprite   Glow                      => Instance != null ? Instance.SharedGlow        : null;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (entries == null) return;

        var seen = new HashSet<BubbleColor>();

        foreach (var entry in entries)
        {
            if (entry == null) continue;

            if (!seen.Add(entry.color))
                Debug.LogWarning($"[BubbleCatalog] '{entry.color}' está dos veces. Solo se usa la primera.", this);

            if (entry.frames == null || entry.frames.Length == 0)
                Debug.LogWarning($"[BubbleCatalog] '{entry.color}' no tiene ningún cuadro: esa burbuja va a salir invisible.", this);
        }
    }
#endif
}
