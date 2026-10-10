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

    // A cuál de las dos familias del GDD §3.2 pertenece. La regla: si el jugador tiene que elegir
    // dónde o cuándo, es de gameplay; si se aplica solo, es de inicio.
    //
    // Es un campo y no algo deducido de si tiene arte de burbuja: la Pinza de Langosta es de
    // gameplay y no transforma ninguna burbuja, así que cualquier deducción fallaría con ella.
    //
    // InGame va PRIMERO porque es el valor 0: Unity añade un campo nuevo a los assets ya
    // serializados con el default del tipo, no con el del inicializador. Con Start en el 0, añadir
    // este campo habría cambiado en silencio la familia de todo lo que ya estaba en el catálogo.
    public enum Family { InGame, Start }

    [System.Serializable]
    // Los títulos de sección NO van como [Header]: los pone BoosterEntryDrawer, que es quien sabe
    // qué secciones tocan para cada poder. Con los dos, cada encabezado salía por duplicado.
    public class Entry
    {
        public Booster booster;

        [Tooltip("De inicio: se elige en la pantalla previa y se aplica solo al empezar.\nDe gameplay: aparece en el HUD y lo activa el jugador durante la partida.")]
        public Family family;

        [Tooltip("El ícono que se ve en la pantalla previa, en el HUD y en la tienda.")]
        public Sprite icon;

        [Tooltip("El arte de la burbuja que este poder produce. Lo usan el tablero, la recámara del cañón y el tutorial, así que con ponerlo acá se ve igual en los tres.\n\nUNO solo: un dibujo fijo, que puede girar o no según 'Spin'.\nVARIOS: se van alternando a 'Frames Per Second' — para un poder que late, parpadea o cambia de forma en vez de girar.\n\nEn un poder de INICIO es simplemente el sprite de la burbuja con la que arrancas: no se monta ninguna capa encima, porque esos nunca pasan por la recámara.")]
        public Sprite[] bubbleFrames;

        [Tooltip("Grados por segundo que gira el fondo. CERO = no gira, que es lo normal para un poder con varios frames: girar y cambiar de dibujo a la vez se lee como un error.\n\nNegativo gira al revés. Pasados unos 900 el giro empieza a verse raro —o al revés— y no es un fallo: a 60 fps son más de 15 grados por fotograma y el ojo deja de seguir el dibujo. Es lo mismo que hace que las ruedas de un coche parezcan girar hacia atrás en el cine.")]
        [Range(-1440f, 1440f)]
        public float spin;

        [Tooltip("A qué ritmo se alternan los frames. Solo se usa si hay más de uno.")]
        [Range(1f, 30f)]
        public float framesPerSecond = 8f;

        [Tooltip("Burbujas pequeñas girando DENTRO de la grande. Arrastra un sprite por cada hija: tres sprites son tres burbujas orbitando. El fondo lo pone 'Bubble Frames' y se queda quieto.")]
        public SpecialBubbleSkin.Orbit bubbleOrbit = new();

        [Tooltip("El chisporroteo de DENTRO de la burbuja: dos rayos horizontales saliendo de un destello en el medio. Se dibuja por código, así que no hace falta arte ni frames.")]
        public SpecialBubbleSkin.Spark bubbleSpark = new();

        [Tooltip("Sonido EN BUCLE que suena mientras el poder está puesto en la recámara — la mecha encendida de la bomba, por ejemplo. Se corta al descargarlo o al dispararlo.\n\nOpcional: sin clip no suena nada y el poder funciona igual.")]
        public AudioClip loadedLoopClip;

        [Range(0f, 1f)]
        [Tooltip("Cuánto se baja ese bucle respecto al volumen de efectos. Un sonido que está sonando todo el rato quiere estar más abajo que un golpe.")]
        public float loadedLoopVolume = 0.5f;

        [Tooltip("El golpe sonoro del efecto. Va aparte del pop de las burbujas: lo que suena es el poder haciendo lo suyo, no las burbujas muriendo.")]
        public AudioClip burstClip;

        [Tooltip("Sonido de lo que pasa ANTES de estallar — la mecha de la bomba mientras carga. Vacío si el poder actúa en el acto.")]
        public AudioClip chargeClip;

        [Range(0f, 4f)]
        [Tooltip("Cuánto se adelanta 'Burst Clip' al estallido, en segundos. Para clips que empiezan con una subida: con el adelanto justo, el golpe del audio cae a la vez que el de la pantalla. Solo sirve si hay carga.")]
        public float clipLead;

        [Tooltip("Si el móvil vibra al estallar.")]
        public bool vibrate = true;

        [Tooltip("Si la pantalla tiembla al estallar, aunque el racimo sea pequeño. Un poder tiene que sentirse grande incluso cuando se lleva pocas burbujas.")]
        public bool shake = true;

        [Tooltip("El chispazo del estallido. Marca 'Rainbow Motes' para que cada chispa tome un color del espectro.")]
        public Sparkle.Settings burstSparkle = new();

        [Tooltip("El destello de PANTALLA y las ondas expansivas. Es lo que hace que el estallido se note mirara donde mirara el jugador, en vez de quedarse en el racimo.\n\nPara un poder de color: sube 'Wave Count' y marca 'Rainbow Waves' — lo que se abre por la pantalla pasa a ser un arcoíris.")]
        public BombBlast.Settings burstBlast = new();

        // Por defecto FALSE: es lo que ya hace la bomba, así que las entradas que existen hoy no
        // cambian de comportamiento al aparecer este campo.
        [Tooltip("Marcado: no hay una explosión en el centro, sino que cada burbuja alcanzada estalla con su propia onda, una detrás de otra. Para un poder que barre en línea, donde un solo estallido central deja los extremos sin nada que mirar.\n\nLas ondas se configuran igual, en 'Burst Blast' — conviene bajarles el tamaño, porque ahora se ven una por burbuja.")]
        public bool burstPerBubble;

        [Tooltip("Las hijas que salen del impacto a buscar sus objetivos. Usa los mismos sprites que 'Bubble Orbit': son las que estaban girando dentro y ahora salen.")]
        public TravelingBubble.Settings burstTravel = new();

        [Tooltip("La descarga eléctrica que recorre la fila desde el impacto hacia los dos lados. Solo tiene sentido en un poder que barra en línea: marca 'Enabled' aquí dentro para encenderla.")]
        public Lightning.Settings burstLightning = new();

        [Range(0.02f, 0.3f)]
        [Tooltip("Cuánto tarda el color en saltar de una burbuja a la siguiente.")]
        public float claimStep = 0.06f;

        [Range(0.05f, 0.6f)]
        [Tooltip("Cuánto tarda cada burbuja en teñirse del todo. Subirlo hace el contagio más suave y más visible; bajarlo lo vuelve un parpadeo.")]
        public float claimFade = 0.16f;

        [Range(0f, 1f)]
        [Tooltip("La pausa con TODAS ya teñidas, justo antes de que estallen. Es el momento que deja ver lo que hizo el poder: sin ella el último tinte y la explosión se pisan.")]
        public float claimHold = 0.12f;

        [Range(0.3f, 3f)]
        [Tooltip("Tope de lo que puede durar el contagio entero. En racimos grandes el paso se comprime para no pasarse de acá — igual que la cadena de pops.")]
        public float claimMaxTotal = 0.8f;

        [Tooltip("Las ofertas que se muestran al tocar el '+' de este booster, en orden. Vacío deja el panel sin nada que vender.")]
        public Pack[] packs;

        [Tooltip("Un brillo propio en lugar del compartido. Vacío (lo normal) usa el de arriba: lo que distingue a un poder de otro es el fondo, no el reflejo.")]
        public Sprite glowOverride;
    }

    [Tooltip("El brillo esférico que va ADELANTE de cualquier burbuja especial, quieto. Uno solo para todas — un poder puede pisarlo con su 'Glow Override'. bubble_glow@200px.")]
    [SerializeField] Sprite sharedGlow;

    [SerializeField] Entry[] entries;

#if UNITY_EDITOR
    // Avisa de lo que un Inspector no puede enseñar: que dos entradas apunten al mismo poder, o
    // que ninguna apunte a uno que ya existe en el enum.
    //
    // Existe porque el dropdown de un enum muestra NOMBRES y Unity guarda NÚMEROS: si alguna vez
    // se reordena el enum, cada entrada pasa a apuntar a otro poder sin que nada cambie de aspecto
    // hasta que se abre el asset. Con dos boosters se nota; con veinte, no.
    void OnValidate()
    {
        if (entries == null) return;

        var seen = new System.Collections.Generic.HashSet<Booster>();

        foreach (var entry in entries)
        {
            if (entry == null) continue;

            if (entry.booster == Booster.None)
                Debug.LogWarning($"[BoosterCatalog] Hay una entrada sin booster asignado: no la va a usar nadie.", this);
            else if (!seen.Add(entry.booster))
                Debug.LogWarning($"[BoosterCatalog] '{entry.booster}' está dos veces. Solo se usa la primera — suele ser señal de que el enum se reordenó y las entradas quedaron apuntando a otro poder.", this);
        }

        foreach (Booster booster in System.Enum.GetValues(typeof(Booster)))
            if (booster != Booster.None && !seen.Contains(booster))
                Debug.LogWarning($"[BoosterCatalog] '{booster}' no tiene entrada: va a salir sin arte y sin ofertas.", this);
    }
#endif

    public Entry EntryFor(Booster booster)
    {
        if (entries == null || booster == Booster.None) return null;

        foreach (var entry in entries)
            if (entry != null && entry.booster == booster) return entry;

        return null;
    }

    public Sprite IconFor(Booster booster) => EntryFor(booster)?.icon;

    // El primer frame, para quien necesita UN sprite y no la animación: el ícono de la recámara
    // del cañón y los puntos de la trayectoria.
    public Sprite BubbleSpriteFor(Booster booster)
    {
        var frames = EntryFor(booster)?.bubbleFrames;

        return frames != null && frames.Length > 0 ? frames[0] : null;
    }

    public Sprite GlowFor(Booster booster)
    {
        var over = EntryFor(booster)?.glowOverride;

        return over != null ? over : sharedGlow;
    }
}
