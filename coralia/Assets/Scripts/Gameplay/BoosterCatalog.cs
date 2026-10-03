using UnityEngine;

// Todo el arte de cada booster, en un solo asset. Mismo criterio que DecorationCatalog.
//
// Existe porque el mismo poder se dibuja en sitios sin parentesco —la selección pre-nivel, el HUD
// de gameplay, la burbuja del cañón, la que vuela, la del tablero, y mañana la tienda— y repartir
// el arte entre los prefabs y el Inspector de GridController significa un campo suelto por poder
// en cada sitio: con seis poderes, una decena de campos que hay que acordarse de mantener a la
// vez, y un olvido se ve como el ícono de otro booster.
//
// Va en Resources y no cableado en cada vista para que un prefab nuevo no tenga que arrastrar
// nada: pregunta por el enum y ya.
[CreateAssetMenu(fileName = "BoosterCatalog", menuName = "Coralia/Booster Catalog")]
public class BoosterCatalog : ScriptableObject
{
    // Dónde se busca dentro de Resources. Si se renombra el asset, el catálogo deja de
    // encontrarse y cada cosa cae a lo que tenga puesto a mano.
    public const string RESOURCE_PATH = "BoosterCatalog";

    // Una oferta del panel de recarga: cuántos da y por cuánto.
    [System.Serializable]
    public class Pack
    {
        [Tooltip("Cuántos se llevan con esta compra.")]
        public int amount = 1;

        [Tooltip("Precio COMPLETO en monedas, sin descuento. No es lo que se cobra si hay descuento — ver abajo.")]
        public int price;

        [Range(0, 90)]
        [Tooltip("Descuento sobre el precio de arriba. 0 oculta la etiqueta.")]
        public int discountPercent;

        // Lo que se cobra de verdad. Se calcula y no se escribe para que no haya forma de que el
        // descuento anunciado y el cobrado digan cosas distintas: con los dos a mano, tocar uno y
        // olvidar el otro deja al juego prometiendo un ahorro que no hace.
        //
        // Nunca baja de 1: un descuento del 90% sobre un precio bajo llegaría a 0 y el pack se
        // regalaría.
        public int FinalPrice => Discounted(price, discountPercent);

        // Estático para que el Inspector pueda mostrar el precio final sin tener que repetir la
        // cuenta: dos copias de una fórmula de precios es poder cobrar algo distinto de lo que el
        // editor enseña.
        // Hacia ABAJO, no al más cercano: un redondeo hacia arriba cobraría una moneda más de la
        // que el descuento promete, y en una cifra pequeña esa moneda es un porcentaje visible.
        // Ante la duda, a favor del jugador.
        public static int Discounted(int price, int discountPercent) => discountPercent <= 0
            ? price
            : Mathf.Max(1, Mathf.FloorToInt(price * (1f - discountPercent / 100f)));
    }

    [System.Serializable]
    public class Entry
    {
        public BubbleSpecial booster;

        [Tooltip("El ícono que se ve en la pantalla previa, en el HUD y en la tienda.")]
        public Sprite icon;

        [Header("La burbuja en el tablero")]
        [Tooltip("El fondo de la burbuja, la capa de abajo.\n\nUNO solo: un dibujo fijo, que puede girar o no según 'Spin'.\nVARIOS: se van alternando a 'Frames Per Second' — para un poder que late, parpadea o cambia de forma en vez de girar.")]
        public Sprite[] bubbleFrames;

        [Tooltip("Grados por segundo que gira el fondo. CERO = no gira, que es lo normal para un poder con varios frames: girar y cambiar de dibujo a la vez se lee como un error.\n\nNegativo gira al revés. Pasados unos 900 el giro empieza a verse raro —o al revés— y no es un fallo: a 60 fps son más de 15 grados por fotograma y el ojo deja de seguir el dibujo. Es lo mismo que hace que las ruedas de un coche parezcan girar hacia atrás en el cine.")]
        [Range(-1440f, 1440f)]
        public float spin;

        [Tooltip("A qué ritmo se alternan los frames. Solo se usa si hay más de uno.")]
        [Range(1f, 30f)]
        public float framesPerSecond = 8f;

        [Header("Mientras está cargado en el cañón")]
        [Tooltip("Sonido EN BUCLE que suena mientras el poder está puesto en la recámara — la mecha encendida de la bomba, por ejemplo. Se corta al descargarlo o al dispararlo.\n\nOpcional: sin clip no suena nada y el poder funciona igual.")]
        public AudioClip loadedLoopClip;

        [Range(0f, 1f)]
        [Tooltip("Cuánto se baja ese bucle respecto al volumen de efectos. Un sonido que está sonando todo el rato quiere estar más abajo que un golpe.")]
        public float loadedLoopVolume = 0.5f;

        [Header("Panel de recarga")]
        [Tooltip("Las ofertas que se muestran al tocar el '+' de este booster, en orden. Vacío deja el panel sin nada que vender.")]
        public Pack[] packs;

        [Tooltip("Un brillo propio en lugar del compartido. Vacío (lo normal) usa el de arriba: lo que distingue a un poder de otro es el fondo, no el reflejo.")]
        public Sprite glowOverride;
    }

    [Tooltip("El brillo esférico que va ADELANTE de cualquier burbuja especial, quieto. Uno solo para todas — un poder puede pisarlo con su 'Glow Override'. bubble_glow@200px.")]
    [SerializeField] Sprite sharedGlow;

    [SerializeField] Entry[] entries;

    public Entry EntryFor(BubbleSpecial booster)
    {
        if (entries == null || booster == BubbleSpecial.None) return null;

        foreach (var entry in entries)
            if (entry != null && entry.booster == booster) return entry;

        return null;
    }

    public Sprite IconFor(BubbleSpecial booster) => EntryFor(booster)?.icon;

    // El primer frame, para quien necesita UN sprite y no la animación: el ícono de la recámara
    // del cañón y los puntos de la trayectoria.
    public Sprite BubbleSpriteFor(BubbleSpecial booster)
    {
        var frames = EntryFor(booster)?.bubbleFrames;

        return frames != null && frames.Length > 0 ? frames[0] : null;
    }

    public Sprite GlowFor(BubbleSpecial booster)
    {
        var over = EntryFor(booster)?.glowOverride;

        return over != null ? over : sharedGlow;
    }
}
