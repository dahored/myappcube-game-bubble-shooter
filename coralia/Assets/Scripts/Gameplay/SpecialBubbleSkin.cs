using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// El aspecto de una burbuja especial: dos capas encima de la burbuja normal.
//
//   back  — el aspecto del poder. Se mueve de una de dos formas, nunca de las dos: GIRA (un solo
//           dibujo dando vueltas) o CAMBIA de dibujo (varios frames alternándose). Girar y cambiar
//           a la vez se lee como un error, no como una animación.
//   glow  — el brillo esférico, quieto. Va adelante para que el giro no arrastre el reflejo:
//           un brillo que gira con la burbuja parece una calcomanía, uno fijo parece luz.
//
// Se monta por CÓDIGO sobre cualquier RectTransform porque la misma burbuja se dibuja en tres
// objetos distintos y sin parentesco: la del grid (BubbleView), la del cañón (una Image suelta)
// y la que va en vuelo (el prefab). Pedir que las tres lleven las capas armadas a mano en el
// editor es tres oportunidades de que una quede distinta.
//
// Las capas cubren al host, no lo apagan: el arte de la burbuja especial es opaco y del mismo
// tamaño, así que tapa lo que haya debajo. Así el host sigue mandando sobre su propia Image
// (el cañón, por ejemplo, la enciende y apaga durante el disparo) sin pelearse con esto.
public class SpecialBubbleSkin : MonoBehaviour
{
    // El chisporroteo de una burbuja eléctrica. Va en el catálogo como el resto del arte del
    // poder, y apagado no cuesta nada: sin 'enabled' la capa ni se crea.
    [System.Serializable]
    public class Spark
    {
        [Tooltip("Marcado: la burbuja chisporrotea por dentro — dos rayos horizontales saliendo de un destello central. Para un poder eléctrico que barre en línea.")]
        public bool enabled;

        [Tooltip("El color del chisporroteo. Un cian claro o un blanco azulado es lo que se lee como electricidad; un tono saturado se lee como pintura.")]
        public Color color = new(0.8f, 0.95f, 1f, 0.9f);

        [Tooltip("Cuántas veces por segundo cambia de forma el rayo. Es lo único que lo hace parecer vivo: por debajo de ocho se ve una calcomanía temblando.")]
        [Range(2f, 30f)]
        public float fps = 14f;

        [Tooltip("Tamaño de los rayos respecto a la burbuja. Por debajo de uno mueren antes del borde, que es lo que hace que se vean DENTRO del cristal y no encima.")]
        [Range(0.3f, 1.2f)]
        public float scale = 0.9f;

        [Tooltip("Tamaño del destello del centro, respecto a la burbuja. Es el corazón de la descarga: tiene que iluminar, no ser un punto.")]
        [Range(0.1f, 1.2f)]
        public float coreScale = 0.62f;

        [Tooltip("Latidos por segundo del destello. Va MUY por encima del cambio de forma a propósito: la luz tiembla mientras los rayos todavía no se han movido, y eso es lo que se lee como corriente en vez de como una animación en bucle.")]
        [Range(2f, 40f)]
        public float pulseHz = 17f;
    }

    // La luz de una mina armada: un destello en el centro que parpadea. Va aparte del
    // chisporroteo eléctrico porque lo que cuentan es lo contrario — el rayo tiembla sin parar y
    // dice "esto está vivo"; la mina está APAGADA casi todo el rato y el destello corto dice
    // "esto espera". Con el mismo ritmo continuo, una mina se leería como una lámpara de adorno.
    [System.Serializable]
    public class Beacon
    {
        [Tooltip("Marcado: la burbuja lleva una luz dentro que parpadea. Para algo armado que espera a que lo toquen.")]
        public bool enabled;

        [Tooltip("El color de la luz. Un rojo encendido es lo que se lee como peligro sin una palabra.")]
        public Color color = new(1f, 0.18f, 0.06f, 1f);

        [Tooltip("Tamaño del destello respecto a la burbuja. Pequeño: es una luz DENTRO del cristal, no una burbuja pintada de rojo.")]
        [Range(0.1f, 1f)]
        public float scale = 0.66f;

        [Tooltip("Parpadeos por segundo. Lento es lo que se lee como espera; rápido pasa a leerse como alarma a punto de estallar.")]
        [Range(0.2f, 6f)]
        public float hz = 0.8f;

        [Tooltip("Qué parte del ciclo dura el destello, en tanto por uno. Es lo que separa un PARPADEO de un subir y bajar: por debajo de un tercio la luz pega y se apaga, por encima de la mitad late como una respiración.")]
        [Range(0.05f, 1f)]
        public float flash = 0.45f;

        [Tooltip("Cuánta luz queda ENTRE destellos. En cero la mina desaparece y vuelve a aparecer; un poco de brasa deja ver dónde está todo el rato.")]
        [Range(0f, 1f)]
        public float dim = 0.25f;

        [Tooltip("Marcado: la luz parpadea EN SILENCIO aunque tenga clip puesto. Para apagar el pitido sin perder el que ya estaba asignado.\n\nEs 'Mute' y no 'suena' porque Unity rellena un campo nuevo con el valor del tipo —false— en todo lo que ya estaba guardado: así el valor que llega solo significa 'como estaba'.")]
        public bool mute;

        [Tooltip("El pitido de cada destello. Suena CON la luz: son el mismo aviso visto y oído, y separarlos deja dos cosas que no se relacionan.\n\nVacío = la luz parpadea en silencio.")]
        public AudioClip clip;

        [Tooltip("Lo mínimo entre dos pitidos, contando TODAS las que haya en el tablero. Sin esto, seis minas plantadas son seis pitidos encimados cada vuelta y el aviso se vuelve ruido de fondo.\n\nLa luz de cada una sigue parpadeando a lo suyo: lo que se espacia es el sonido.")]
        [Range(0f, 8f)]
        public float clipGap = 0.5f;

        [Range(0f, 1f)]
        public float clipVolume = 0.45f;
    }

    // Burbujas pequeñas girando DENTRO de la grande. El fondo se queda quieto y solo orbitan
    // ellas, que es lo que distingue "una burbuja que lleva tres dentro" de "un dibujo girando".
    //
    // Se compone por código en vez de pedir un sprite animado: las hijas son burbujas normales
    // del juego, así que el jugador reconoce lo que va a salir antes de dispararlo — y cambiar
    // cuántas son o de qué color es arrastrar sprites en vez de exportar frames.
    [System.Serializable]
    public class Orbit
    {
        [Tooltip("Las burbujas que van dentro. Una capa por sprite: tres sprites son tres hijas girando. Vacío = ninguna.")]
        public Sprite[] sprites;

        [Tooltip("A qué distancia del centro giran, en tanto por uno del ancho de la burbuja. 0.5 sería el borde exacto.\n\nEs el MÍNIMO: cada hija sortea el suyo entre este y 'Radius Max'.")]
        [Range(0f, 0.45f)]
        public float radius = 0.17f;

        [Tooltip("El máximo. Cada hija se acerca y se aleja entre los dos valores mientras gira. Por debajo del mínimo se ignora y las tres orbitan a distancia fija.\n\nUn par de centésimas bastan: las órbitas dejan de ser concéntricas y el conjunto se mueve como algo vivo en vez de como una pieza rígida.")]
        [Range(0f, 0.45f)]
        public float radiusMax = 0.19f;

        [Tooltip("Qué mide cada hija respecto a la burbuja que las lleva.")]
        [Range(0.05f, 0.6f)]
        public float size = 0.3f;

        [Tooltip("Grados por segundo. Despacio es lo que se lee como flotar; rápido parece una ruleta. Negativo gira al revés.")]
        [Range(-360f, 360f)]
        public float speed = 35f;

        [Tooltip("Cuántas veces por segundo se acerca y se aleja cada hija, entre 'Radius' y 'Radius Max'.\n\nMuy por debajo del giro: es un vaivén de fondo, y en cuanto se acerca al ritmo de la vuelta deja de leerse como respirar y parece que la órbita va a tirones. Cada hija lo hace a su propio ritmo alrededor de este valor.")]
        [Range(0.02f, 2f)]
        public float breathPerSecond = 0.22f;

        public bool Enabled => sprites != null && sprites.Length > 0;
    }

    Image   _back;
    Image[]  _orbit;
    float[]  _orbitPhase;    // por dónde va su respiración
    float[]  _orbitBreath;   // y a qué ritmo, el suyo
    float    _orbitClock;
    Orbit    _orbitCfg;
    float    _orbitAngle;
    Image   _sparkR;
    Image   _sparkL;
    Image   _core;
    Image[] _liquid;
    Image[] _crest;
    Image   _bubbles;
    float   _sway;          // grados que se inclina de un lado a otro
    float   _swayHz;        // y a qué ritmo
    float[] _swayPhase;     // por dónde empieza cada capa
    float[] _swayRate;      // y cuánto más rápido o lento va que las demás
    float[] _swayAmount;    // y cuánto se inclina ella
    int[]   _frameAt;       // en qué frame del ciclo va cada capa
    float   _liquidFps;     // a qué ritmo avanza ese ciclo
    float   _liquidFill;    // cuánto llena, para pedir el frame siguiente
    float   _liquidTime;    // lo que lleva acumulado hacia el frame siguiente
    Image   _glow;
    Graphic _host;   // la Image sobre la que se montaron las capas

    Spark    _sparkCfg;
    int      _sparkFrame;
    float    _sparkTime;
    float    _pulseTime;

    Image    _beacon;
    Beacon   _beaconCfg;
    float    _beaconTime;
    bool     _beaconQueued;

    // Compartido por TODAS las minas del tablero: el espaciado tiene que mirar lo que suena en
    // conjunto, no lo que suena cada una. Mirándolo por burbuja, seis minas pasarían el filtro a
    // la vez porque ninguna sabe de las otras.
    static float _nextBeep;

    // Y la COLA de las que esperan turno, por orden de llegada.
    //
    // Sin ella el turno se lo lleva la primera cuyo Update corra ese frame, y ese orden lo fija
    // Unity y no cambia nunca: con tres minas esperando, la misma se quedaba con todos los turnos
    // y las otras dos no encendían jamás (reportado por Diego dos veces).
    static readonly List<SpecialBubbleSkin> _beaconQueue = new();

    // Lo que SetAlpha pidió para las capas del chisporroteo. Hace falta guardarlo porque el
    // latido reescribe el color del destello cada frame: sin esto, una burbuja reventando se
    // desvanecía entera menos su luz, que seguía encendida hasta que el objeto moría.
    float    _sparkFade = 1f;

    Sprite[] _frames;
    float    _spin;
    float    _fps;
    float    _frameTime;
    int      _frame;

    // Si el poder está puesto. Es distinto de que las capas se vean: una bomba cargada sigue
    // puesta mientras el cañón esconde su burbuja durante el vuelo. Sin separarlo, el seguimiento
    // del host de abajo volvía a encender lo que Clear() acababa de apagar, y la bomba quedaba
    // dibujada encima de la burbuja normal que había vuelto a la recámara.
    bool _on;

    // Enciende (o cambia) las capas sobre 'host'.
    //
    // Recibe el booster y no los sprites: el arte vive en BoosterCatalog, y los tres sitios que
    // dibujan una burbuja especial —el grid, la recámara del cañón y la que vuela— no tienen por
    // qué saber de dónde sale ni repetir la consulta cada uno a su manera.
    public static void Apply(Component host, Booster booster)
    {
        if (host == null) return;

        var art = BoosterRules.ArtFor(booster);
        if (art == null || art.bubbleFrames == null || art.bubbleFrames.Length == 0) return;

        var skin = host.GetComponent<SpecialBubbleSkin>() ?? host.gameObject.AddComponent<SpecialBubbleSkin>();

        skin._frames = art.bubbleFrames;
        skin._spin   = art.spin;
        skin._fps    = art.framesPerSecond;
        skin._on     = true;

        skin._frame     = 0;
        skin._frameTime = 0f;

        skin._sparkCfg  = art.bubbleSpark != null && art.bubbleSpark.enabled ? art.bubbleSpark : null;
        skin._sparkFrame = 0;
        skin._sparkTime  = 0f;

        bool spark = skin._sparkCfg != null;

        skin._back   = skin.Layer(skin._back,   "SpecialBack",   art.bubbleFrames[0]);
        skin.BuildOrbit(art.bubbleOrbit);
        skin._core   = skin.Layer(skin._core,   "SpecialCore",   spark ? SparkleTextures.Glow : null);
        skin._sparkR = skin.Layer(skin._sparkR, "SpecialSparkR", spark ? SparkleTextures.SparkArm(0) : null);
        skin._sparkL = skin.Layer(skin._sparkL, "SpecialSparkL", spark ? SparkleTextures.SparkArm(1) : null);
        // El líquido va DEBAJO del brillo y encima del fondo: es lo que la burbuja contiene, así
        // que el reflejo del vidrio tiene que quedarle por encima o se ve como pintura.
        skin.BuildLiquid(art);

        skin._beaconCfg  = art.bubbleBeacon != null && art.bubbleBeacon.enabled ? art.bubbleBeacon : null;
        // Cada una arranca en un punto distinto de su ciclo. Empezando todas en cero, lo único
        // que las separaba era el escalonado de la siembra, y con una siembra rápida el tablero
        // entero parpadeaba al unísono: eso se lee como una animación en bucle, no como seis
        // cosas armadas cada una por su cuenta.
        //
        // Con el pitido encendido la cola les reparte los turnos igual; esto es lo que las hace
        // independientes cuando está en silencio (pedido de Diego).
        skin._beaconTime  = skin._beaconCfg != null
            ? Random.Range(0f, 1f / Mathf.Max(0.05f, skin._beaconCfg.hz))
            : 0f;

        skin.Dequeue();
        // Arranca 'dado la vuelta' para que el PRIMER destello traiga su pitido: empezando en
        // cero, la primera luz salía muda y el sonido entraba recién en la segunda vuelta.
        skin._beacon     = skin.Layer(skin._beacon, "SpecialBeacon",
                                      skin._beaconCfg != null ? SparkleTextures.Beacon : null);

        skin._glow   = skin.Layer(skin._glow,   "SpecialGlow",   BoosterRules.GlowFor(booster));

        // La luz va EN MEDIO: encima del arte de la burbuja y debajo del brillo del cristal. Al
        // revés se vería pegada sobre el vidrio en vez de encendida dentro de él, que es toda la
        // diferencia entre una mina y una pegatina roja.
        if (skin._beaconCfg != null)
        {
            skin._back.transform.SetAsLastSibling();
            skin._beacon.transform.SetAsLastSibling();
            if (skin._glow) skin._glow.transform.SetAsLastSibling();
        }

        if (spark)
        {
            skin._pulseTime = 0f;

            // Las escalas se ponen con la escala y no con los márgenes: la capa se estira al
            // host, que mide distinto en el grid (92px) que en el ícono del cañón.
            //
            // El brazo izquierdo es el mismo dibujo con la X al revés. Lo que impide que se vea
            // un espejo no es la forma sino la VARIANTE: cada lado va por su propio número, y
            // además desfasados, así que nunca coinciden.
            float k = skin._sparkCfg.scale;
            skin._sparkR.transform.localScale = new Vector3( k, k, 1f);
            skin._sparkL.transform.localScale = new Vector3(-k, k, 1f);
            skin._core.transform.localScale   = Vector3.one * skin._sparkCfg.coreScale;

            skin._sparkR.color = skin._sparkL.color = skin._sparkCfg.color;
            skin._core.color   = skin._sparkCfg.color;
            skin._sparkFade    = 1f;

            // Entre el arte y el brillo, siempre, y el destello por debajo de los rayos: creadas
            // en otro orden quedarían encima del reflejo y la burbuja dejaría de parecer cristal.
            skin._back.transform.SetAsLastSibling();
            skin._core.transform.SetAsLastSibling();
            skin._sparkR.transform.SetAsLastSibling();
            skin._sparkL.transform.SetAsLastSibling();
            if (skin._glow) skin._glow.transform.SetAsLastSibling();
        }

        // Se reinicia en cada Apply: una burbuja que vuelve a ser especial arranca derecha, no
        // en el ángulo donde quedó la anterior que usó este mismo objeto.
        if (skin._back) skin._back.transform.localRotation = Quaternion.identity;

        // Y el brillo del cristal, lo ÚLTIMO de todo, siempre. Las capas de más arriba se suben
        // al final cada una por su cuenta, pero 'Layer' reusa la que ya existe sin volver a
        // subirla: a partir del segundo Apply la órbita quedaba por encima del reflejo y el
        // brillo de la burbuja grande se veía detrás de las tres de dentro (reportado por Diego).
        if (skin._glow) skin._glow.transform.SetAsLastSibling();
    }

    public static void Clear(Component host)
    {
        var skin = host == null ? null : host.GetComponent<SpecialBubbleSkin>();
        if (skin == null) return;

        skin._on = false;

        if (skin._back)   skin._back.gameObject.SetActive(false);
        if (skin._orbit != null) foreach (var child in skin._orbit) if (child) child.gameObject.SetActive(false);
        if (skin._core)   skin._core.gameObject.SetActive(false);
        if (skin._sparkR) skin._sparkR.gameObject.SetActive(false);
        if (skin._sparkL) skin._sparkL.gameObject.SetActive(false);
        if (skin._liquid != null) foreach (var layer in skin._liquid) if (layer) layer.gameObject.SetActive(false);
        if (skin._crest  != null) foreach (var layer in skin._crest)  if (layer) layer.gameObject.SetActive(false);
        if (skin._bubbles) skin._bubbles.gameObject.SetActive(false);
        skin.Dequeue();
        if (skin._beacon) skin._beacon.gameObject.SetActive(false);
        if (skin._glow)   skin._glow.gameObject.SetActive(false);
    }

    // Las capas acompañan el desvanecido de la burbuja (pop, caída). Sin esto la bomba se
    // apaga pero su giro queda flotando opaco hasta que el objeto se destruye.
    // De qué color está llena esta burbuja. Lo usa la Tinta de Pulpo, que se dibuja con el color
    // que va a repartir dentro: el poder entero es "lo que ves dentro es lo que va a salir", y
    // ese color cambia a cada disparo, así que no puede vivir en el catálogo.
    // Las capas de líquido, con su onda, su color y su vaivén.
    //
    // Se reusan entre poderes como el resto de capas: el cañón cambia de uno a otro a cada rato
    // y recrearlas sería basura por turno.
    void BuildLiquid(BoosterCatalog.Entry art)
    {
        int want = art.liquidFill > 0f && art.liquidColor.a > 0f
            ? Mathf.Max(1, art.liquidLayers > 0 ? art.liquidLayers : DEFAULT_LIQUID_LAYERS)
            : 0;

        if (_liquid != null)
            for (int i = 0; i < _liquid.Length; i++)
            {
                if (_liquid[i] != null && i >= want) _liquid[i].gameObject.SetActive(false);
                if (_crest  != null && i < _crest.Length && _crest[i] != null && i >= want)
                    _crest[i].gameObject.SetActive(false);
            }

        if (want == 0 && _bubbles) _bubbles.gameObject.SetActive(false);

        _sway        = art.liquidSway;
        _swayHz      = art.liquidSwayHz;
        _liquidFps   = art.liquidFlowFps > 0f ? art.liquidFlowFps : DEFAULT_LIQUID_FPS;
        _liquidFill  = art.liquidFill;
        _liquidTime  = 0f;

        if (want == 0) return;

        if (_liquid     == null || _liquid.Length     < want) System.Array.Resize(ref _liquid,     want);
        if (_crest      == null || _crest.Length      < want) System.Array.Resize(ref _crest,      want);
        if (_swayPhase  == null || _swayPhase.Length  < want) System.Array.Resize(ref _swayPhase,  want);
        if (_swayRate   == null || _swayRate.Length   < want) System.Array.Resize(ref _swayRate,   want);
        if (_swayAmount == null || _swayAmount.Length < want) System.Array.Resize(ref _swayAmount, want);
        if (_frameAt    == null || _frameAt.Length    < want) System.Array.Resize(ref _frameAt,    want);

        for (int i = 0; i < want; i++)
        {
            // Cada capa con SU onda: la variante desplaza la superficie, así que dos capas nunca
            // tienen la cresta en el mismo sitio.
            // Cada capa arranca su ciclo por un frame distinto, así que en el mismo instante una
            // tiene la cresta levantada donde otra la tiene baja. Esa diferencia es la que, vista
            // en conjunto, se lee como una superficie que se mueve de verdad.
            _frameAt[i] = i * _liquidFrames / Mathf.Max(1, want);

            // Ninguna capa lleva burbujas: van en su propia capa por delante de todas, porque
            // las de superficie son semitransparentes y las lavaban.
            _liquid[i] = Layer(_liquid[i], $"SpecialLiquid{i}",
                               SparkleTextures.Liquid(art.liquidFill, _frameAt[i], _liquidFrames, withBubbles: false));

            _crest[i]  = Layer(_crest[i],  $"SpecialLiquidCrest{i}",
                               SparkleTextures.Liquid(art.liquidFill, _frameAt[i], _liquidFrames, withBubbles: false, crestOnly: true));

            // Y con su propio vaivén. Los ritmos se sortean en vez de repartirse parejo: con
            // valores proporcionales acaban coincidiendo cada pocos ciclos y vuelve el bloque.
            _swayPhase[i]  = Random.Range(0f, Mathf.PI * 2f);
            _swayRate[i]   = Random.Range(0.65f, 1.45f);
            _swayAmount[i] = Random.Range(0.7f, 1.25f);
        }

        // Las burbujas de dentro, por delante de todo el líquido y a alfa completo. Quietas: no
        // viajan con la ola porque están DENTRO del líquido, no en su superficie.
        _bubbles = Layer(_bubbles, "SpecialLiquidBubbles",
                         SparkleTextures.Liquid(art.liquidFill, _frameAt[0], _liquidFrames, bubblesOnly: true));

        TintLiquid(art.liquidColor, art.liquidCrestColor);
    }

    // La de abajo opaca y las de encima cada vez más tenues: superpuestas al mismo alfa se suman
    // hasta tapar el degradado y vuelve a verse plano.
    void TintLiquid(Color body, Color crest)
    {
        if (_liquid == null) return;

        // Si no se eligió color de cresta, se aclara el del cuerpo: la superficie de un líquido
        // nunca es del mismo tono que el fondo, y un valor sin poner no puede dejarla plana.
        if (crest.a <= 0f) crest = new Color(Mathf.Lerp(body.r, 1f, 0.45f),
                                             Mathf.Lerp(body.g, 1f, 0.45f),
                                             Mathf.Lerp(body.b, 1f, 0.45f), body.a);

        for (int i = 0; i < _liquid.Length; i++)
        {
            float fade = i == 0 ? 1f : Mathf.Lerp(0.55f, 0.25f, (i - 1f) / Mathf.Max(1f, _liquid.Length - 2f));

            if (_liquid[i] != null)
                _liquid[i].color = new Color(body.r, body.g, body.b, body.a * fade);

            // La cresta se desvanece menos que el cuerpo: es lo que hay que ver de las capas de
            // encima, y con el mismo desvanecido se perdería justo lo que las hace servir.
            if (_crest != null && i < _crest.Length && _crest[i] != null)
                _crest[i].color = new Color(crest.r, crest.g, crest.b, crest.a * Mathf.Lerp(fade, 1f, 0.5f));
        }

        // Las burbujas, a alfa completo y con el color del cuerpo: son del mismo líquido, solo
        // que con su propia luz. Cualquier transparencia acá las devolvería a ser manchas.
        if (_bubbles != null) _bubbles.color = new Color(body.r, body.g, body.b, body.a);
    }

    const int DEFAULT_LIQUID_LAYERS = 3;

    // Cuántos pasos tiene un ciclo completo de la ola, y a qué ritmo se recorren por defecto.
    //
    // Doce es el equilibrio: con menos se ve saltar de una forma a otra, y con más son texturas
    // de sobra para un detalle que se dibuja a tamaño de burbuja. Se generan una vez y quedan
    // cacheadas, compartidas por todas las burbujas del mismo poder.
    const int   _liquidFrames      = 12;
    const float DEFAULT_LIQUID_FPS = 3.5f;

    public static void SetAlpha(Component host, float alpha)
    {
        var skin = host == null ? null : host.GetComponent<SpecialBubbleSkin>();
        if (skin == null) return;

        skin._sparkFade = alpha;

        float sparkAlpha = alpha * (skin._sparkCfg?.color.a ?? 1f);

        Fade(skin._back,   alpha);
        if (skin._orbit != null) foreach (var child in skin._orbit) Fade(child, alpha);
        Fade(skin._core,   sparkAlpha);
        Fade(skin._sparkR, sparkAlpha);
        Fade(skin._sparkL, sparkAlpha);
        Fade(skin._beacon, alpha * (skin._beaconCfg?.color.a ?? 1f));
        Fade(skin._glow,   alpha);
    }

    static void Fade(Image image, float alpha)
    {
        if (image == null) return;
        var c = image.color;
        image.color = new Color(c.r, c.g, c.b, alpha);
    }

    // Se reusan las Image en vez de destruirlas y recrearlas: el cañón cambia de especial a
    // normal y de vuelta en cada disparo, y eso sería dos GameObject nuevos por turno.
    Image Layer(Image existing, string name, Sprite sprite)
    {
        if (sprite == null)
        {
            if (existing) existing.gameObject.SetActive(false);
            return existing;
        }

        if (existing == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image))
            {
                // Misma convención que el resto de los hijos generados del proyecto: sin esto
                // quedan serializados dentro del .unity y del .prefab.
                hideFlags = HideFlags.DontSave,
            };

            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;   // estirada al host: así sirve igual para la burbuja
            rt.anchorMax = Vector2.one;    // del grid (92px) que para el ícono del cañón
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            existing = go.GetComponent<Image>();
            existing.raycastTarget = false; // el tap para apuntar tiene que llegar a la burbuja de abajo
        }

        existing.sprite = sprite;
        existing.gameObject.SetActive(true);
        return existing;
    }

    void Awake() => _host = GetComponent<Graphic>();

    void Update()
    {
        // Las capas siguen al host. Si su Image se apaga, se apagan con ella.
        //
        // Hace falta porque son HIJAS suyas, y apagar un componente Image no apaga a sus hijos:
        // el cañón apaga la burbuja de la recámara mientras el disparo vuela —para que no se vea
        // la bola quieta y la que vuela al mismo tiempo— y sin esto la bomba se quedaba pintada
        // en la almeja durante todo el vuelo, como si no se hubiera movido de ahí.
        //
        // Va acá y no en cada sitio que apaga la Image porque esos sitios son varios (el disparo,
        // la aparición al abrir el nivel, el remate de victoria) y los que vengan no van a saber
        // que esto existe.
        bool visible = _on && (_host == null || _host.enabled);

        bool sparkVisible = visible && _sparkCfg != null;

        // Cuántas hijas quiere el poder que está puesto AHORA. Las capas se reusan entre poderes
        // —el cañón pasa de uno a otro a cada rato y recrearlas sería basura por turno—, así que
        // el array puede tener de sobra: la Madre deja tres y la arcoíris no quiere ninguna.
        //
        // Sin esta cuenta, este bucle las encendía TODAS por el simple hecho de existir, y
        // deshacía el apagado que BuildOrbit acababa de hacer. Se veía al descargar la Madre
        // sobre una arcoíris: el disco de colores salía con las tres burbujas de la Madre encima,
        // un poder al que nadie había llamado (reportado por Diego).
        int orbitWanted = visible ? _orbitCfg?.sprites?.Length ?? 0 : 0;

        if (_back   != null && _back.gameObject.activeSelf   != visible)      _back.gameObject.SetActive(visible);

        if (_orbit != null)
        {
            for (int i = 0; i < _orbit.Length; i++)
            {
                bool on = i < orbitWanted;
                if (_orbit[i] != null && _orbit[i].gameObject.activeSelf != on) _orbit[i].gameObject.SetActive(on);
            }

            if (orbitWanted > 0) TickOrbit();
        }

        if (_core   != null && _core.gameObject.activeSelf   != sparkVisible) _core.gameObject.SetActive(sparkVisible);
        if (_sparkR != null && _sparkR.gameObject.activeSelf != sparkVisible) _sparkR.gameObject.SetActive(sparkVisible);
        if (_sparkL != null && _sparkL.gameObject.activeSelf != sparkVisible) _sparkL.gameObject.SetActive(sparkVisible);
        if (_liquid != null)
            foreach (var layer in _liquid)
                if (layer != null && layer.gameObject.activeSelf != visible) layer.gameObject.SetActive(visible);

        if (_crest != null)
            foreach (var layer in _crest)
                if (layer != null && layer.gameObject.activeSelf != visible) layer.gameObject.SetActive(visible);

        if (_bubbles != null && _bubbles.gameObject.activeSelf != visible) _bubbles.gameObject.SetActive(visible);
        if (_glow   != null && _glow.gameObject.activeSelf   != visible)      _glow.gameObject.SetActive(visible);

        bool beaconVisible = visible && _beaconCfg != null;

        if (_beacon != null && _beacon.gameObject.activeSelf != beaconVisible) _beacon.gameObject.SetActive(beaconVisible);

        if (sparkVisible)  TickSpark();
        if (beaconVisible) PulseBeacon();

        // El líquido se mece, cada capa a su ritmo y por su lado.
        //
        // Inclinando las capas y no moviéndolas, la superficie se queda horizontal mientras el
        // resto gira — que es lo que hace un líquido dentro de algo que se balancea. Un
        // desplazamiento vertical se vería como que sube el nivel, y lo que se mueve es la
        // burbuja, no cuánto lleva dentro.
        //
        // Lo que convierte esto en líquido es que las capas NO vayan juntas: con la misma fase y
        // el mismo ritmo se moverían como un bloque, que es lo mismo que una sola capa.
        // La ola AVANZA: cada capa pasa al frame siguiente de su ciclo, así que la cresta cambia
        // de forma mientras el conjunto se balancea. Inclinar sin esto movía el líquido entero
        // como una pieza rígida (reportado por Diego).
        if (_liquid != null && visible && _liquidFps > 0f)
        {
            _liquidTime += Time.deltaTime;

            float flowStep = 1f / _liquidFps;

            if (_liquidTime >= flowStep)
            {
                // Se descuenta en vez de poner a cero: con un fps alto y un frame largo pueden
                // tocar dos saltos, y poniéndolo a cero la ola se arrastraría respecto al reloj.
                _liquidTime -= flowStep;

                for (int i = 0; i < _liquid.Length; i++)
                {
                    _frameAt[i] = (_frameAt[i] + 1) % _liquidFrames;

                    if (_liquid[i] != null)
                        _liquid[i].sprite = SparkleTextures.Liquid(_liquidFill, _frameAt[i], _liquidFrames, withBubbles: i == 0);

                    if (_crest != null && i < _crest.Length && _crest[i] != null)
                        _crest[i].sprite = SparkleTextures.Liquid(_liquidFill, _frameAt[i], _liquidFrames, withBubbles: false, crestOnly: true);
                }

                // Y las burbujas suben con el mismo reloj: comparten ciclo con la ola, así que
                // llegan arriba justo cuando la superficie vuelve a su sitio.
                if (_bubbles != null)
                    _bubbles.sprite = SparkleTextures.Liquid(_liquidFill, _frameAt[0], _liquidFrames, bubblesOnly: true);
            }
        }

        if (_liquid != null && visible && _sway > 0f)
            for (int i = 0; i < _liquid.Length; i++)
            {
                if (_liquid[i] == null) continue;

                float angle = _sway * _swayAmount[i] * Mathf.Sin(_swayPhase[i] + Time.time * _swayHz * _swayRate[i] * Mathf.PI * 2f);

                // Cuerpo y cresta se inclinan JUNTOS: son la misma masa de líquido partida en dos
                // piezas para poder pintarlas de distinto color, no dos cosas que se mueven.
                _liquid[i].transform.localEulerAngles = new Vector3(0f, 0f, angle);

                if (_crest != null && i < _crest.Length && _crest[i] != null)
                    _crest[i].transform.localEulerAngles = new Vector3(0f, 0f, angle);
            }

        if (!visible || _back == null) return;

        // Girar y pasar frames son excluyentes por diseño, pero no se fuerza acá: si alguien pone
        // las dos cosas en el catálogo, se ven las dos y se nota enseguida que está mal. Imponerlo
        // en código sería decidir en silencio cuál de las dos quiso.
        if (_spin != 0f) _back.transform.Rotate(0f, 0f, _spin * Time.deltaTime);

        if (_frames == null || _frames.Length < 2 || _fps <= 0f) return;

        _frameTime += Time.deltaTime;

        float step = 1f / _fps;
        if (_frameTime < step) return;

        // Se descuenta en vez de poner a cero: con un fps alto y un frame largo pueden tocar dos
        // saltos, y poniéndolo a cero la animación se arrastraría respecto al reloj.
        _frameTime -= step;
        _frame      = (_frame + 1) % _frames.Length;

        _back.sprite = _frames[_frame];
    }

    // Dos relojes distintos a propósito: la forma de los rayos cambia despacio y la luz del
    // centro tiembla deprisa. Al mismo ritmo todo late a la vez y se ve un bucle; desacoplados,
    // la burbuja no repite nunca la misma imagen y eso es lo que se lee como corriente.
    void TickSpark()
    {
        PulseCore();

        _sparkTime += Time.deltaTime;

        float step = 1f / Mathf.Max(0.01f, _sparkCfg.fps);
        if (_sparkTime < step) return;

        // Se descuenta en vez de ponerse a cero: con un fps alto y un frame largo pueden tocar
        // dos saltos, y poniéndolo a cero la animación se arrastraría respecto al reloj.
        _sparkTime -= step;

        // El rayo SALTA de una forma a la siguiente, no se interpola: interpolar entre dos
        // quebradas da una cinta que se retuerce, justo lo contrario de lo que hace una chispa.
        //
        // Los dos lados avanzan a la par pero separados por un número IMPAR de variantes sobre un
        // total par: así nunca se alcanzan y ningún par se repite en toda la vuelta.
        _sparkFrame = (_sparkFrame + 1) % SparkleTextures.SPARK_VARIANTS;

        if (_sparkR) _sparkR.sprite = SparkleTextures.SparkArm(_sparkFrame);
        if (_sparkL) _sparkL.sprite = SparkleTextures.SparkArm(_sparkFrame + 3);
    }

    // Encendido corto y apagado largo, no un sube y baja. Lo que convierte una luz en el aviso de
    // algo armado es la PAUSA: el destello pega, se va, y queda un rato de brasa antes del
    // siguiente. Una sinusoide entera se lee como que respira, y lo que respira no da miedo.
    void PulseBeacon()
    {
        if (_beacon == null || _beaconCfg == null) return;

        float cycle = 1f / Mathf.Max(0.05f, _beaconCfg.hz);
        float flash = Mathf.Clamp01(_beaconCfg.flash);

        _beaconTime += Time.deltaTime;

        // Al cerrar la vuelta, la mina ESPERA SU TURNO en vez de perder el destello.
        //
        // Antes el espaciado le negaba el pitido a la que no llegaba a tiempo, y con él la luz:
        // tres minas plantadas y una se quedaba con todos los turnos mientras las otras dos
        // parecían apagadas (reportado por Diego). Reteniendo el ciclo, cada una enciende cuando
        // le toca y las tres se ven y se oyen, una detrás de otra.
        if (_beaconTime >= cycle)
        {
            if (MyTurn()) { _beaconTime -= cycle; Beep(); }
            else            _beaconTime  = cycle;   // esperando, en el tramo apagado
        }

        float t = Mathf.Clamp01(_beaconTime / cycle);

        // Medio seno dentro del tramo encendido: sube y baja una vez, sin esquinas. El resto del
        // ciclo se queda en la brasa.
        float beat = t < flash ? Mathf.Sin(t / flash * Mathf.PI) : 0f;
        float lit  = Mathf.Lerp(Mathf.Clamp01(_beaconCfg.dim), 1f, beat);

        var c = _beaconCfg.color;

        _beacon.color = new Color(c.r, c.g, c.b, c.a * _sparkFade * lit);

        // Y crece con la luz, igual que el destello eléctrico: un alfa que cambia solo se lee
        // como un parpadeo de pantalla, uno que además se hincha se lee como una lámpara.
        _beacon.transform.localScale = Vector3.one * (_beaconCfg.scale * (0.88f + 0.12f * beat));
    }

    // Si le toca encender: ser la primera de la cola, y que haya pasado el espaciado.
    //
    // Sin pitido no hay cola que respetar — la luz va a su ritmo y ya. Es lo que hace que una mina
    // sin sonido configurado parpadee normal en vez de esperar un turno que no existe.
    bool MyTurn()
    {
        if (!Beeps) return true;

        if (!_beaconQueued) { _beaconQueued = true; _beaconQueue.Add(this); }

        // Las que ya no están: una burbuja destruida o un poder descargado dejan su sitio en la
        // cola, y sin limpiarlo la fila entera se queda esperando detrás de alguien que no va a
        // encender nunca.
        while (_beaconQueue.Count > 0 && !Alive(_beaconQueue[0])) _beaconQueue.RemoveAt(0);

        if (_beaconQueue.Count == 0 || _beaconQueue[0] != this) return false;
        if (Time.time < _nextBeep)                              return false;

        _beaconQueue.RemoveAt(0);
        _beaconQueued = false;
        return true;
    }

    static bool Alive(SpecialBubbleSkin skin) => skin != null && skin._on && skin.Beeps;

    // Si esta mina hace ruido. Callada no entra en la cola ni ocupa el espaciado: las que sí
    // suenan se reparten los turnos entre ellas y esta parpadea a su ritmo.
    bool Beeps => _beaconCfg != null && _beaconCfg.clip != null && !_beaconCfg.mute;

    void Beep()
    {
        if (!Beeps) return;

        _nextBeep = Time.time + Mathf.Max(0f, _beaconCfg.clipGap);

        AudioManager.Instance?.PlaySfx(_beaconCfg.clip, _beaconCfg.clipVolume);
    }

    // Sale de la cola al apagarse, al destruirse o al dejar de ser mina: un hueco de alguien que
    // ya no enciende frena a toda la fila que viene detrás.
    void Dequeue()
    {
        if (!_beaconQueued) return;

        _beaconQueued = false;
        _beaconQueue.Remove(this);
    }

    void OnDisable() => Dequeue();
    void OnDestroy() => Dequeue();

    // Dos ondas de frecuencias que no son múltiplo una de otra. Con una sola el destello sube y
    // baja como un metrónomo; sumadas nunca repiten el mismo pico y el temblor sale irregular,
    // que es como late algo eléctrico.
    void PulseCore()
    {
        if (_core == null) return;

        _pulseTime += Time.deltaTime;

        float w     = _pulseTime * _sparkCfg.pulseHz * Mathf.PI * 2f;
        float beat  = Mathf.Abs(Mathf.Sin(w)) * 0.65f + Mathf.Abs(Mathf.Sin(w * 2.7f)) * 0.35f;

        _core.color = new Color(_sparkCfg.color.r, _sparkCfg.color.g, _sparkCfg.color.b,
                                _sparkCfg.color.a * _sparkFade * (0.45f + 0.55f * beat));

        // Y crece con la luz: un destello que solo cambia de alfa se lee como un parpadeo de
        // pantalla, uno que además respira se lee como una lámpara forzada.
        float k = _sparkCfg.coreScale * (0.88f + 0.12f * beat);
        _core.transform.localScale = Vector3.one * k;
    }

    // Las hijas se colocan con ANCLAS y no con posiciones: ancladas en fracciones del padre, la
    // órbita escala sola con la burbuja. Así el mismo ajuste vale para la del tablero (92 px) y
    // para el ícono del cañón, sin medir nada ni enterarse de cuándo cambia de tamaño.
    void BuildOrbit(Orbit cfg)
    {
        _orbitCfg   = cfg != null && cfg.Enabled ? cfg : null;
        _orbitAngle = 0f;

        int want = _orbitCfg?.sprites.Length ?? 0;

        // Se reusan las que ya hay: el cañón pasa de especial a normal y de vuelta en cada
        // disparo, y recrearlas sería tres GameObject nuevos por turno.
        if (_orbit != null)
            for (int i = 0; i < _orbit.Length; i++)
                if (_orbit[i] != null && i >= want) _orbit[i].gameObject.SetActive(false);

        if (want == 0) return;

        if (_orbit == null || _orbit.Length < want) System.Array.Resize(ref _orbit, want);
        if (_orbitPhase  == null || _orbitPhase.Length  < want) System.Array.Resize(ref _orbitPhase,  want);
        if (_orbitBreath == null || _orbitBreath.Length < want) System.Array.Resize(ref _orbitBreath, want);

        // Dónde empieza la respiración de cada una y a qué ritmo. Las dos cosas sorteadas, no
        // solo la fase: con el mismo ritmo acaban sincronizándose a la vista aunque arranquen
        // separadas, porque la distancia entre ellas se repite cada ciclo. Con ritmos distintos
        // no vuelven a coincidir nunca.
        //
        // Se sortea en cada Apply, así que dos burbujas del mismo poder tampoco se mueven igual.
        for (int i = 0; i < want; i++)
        {
            _orbitPhase[i]  = Random.Range(0f, Mathf.PI * 2f);
            _orbitBreath[i] = Random.Range(0.7f, 1.35f);
        }

        _orbitClock = 0f;

        for (int i = 0; i < want; i++)
        {
            if (_orbit[i] == null)
            {
                var go = new GameObject($"OrbitBubble{i}", typeof(RectTransform), typeof(Image))
                {
                    hideFlags = HideFlags.DontSave,
                };

                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.offsetMin = rt.offsetMax = Vector2.zero;   // el tamaño sale de las anclas

                _orbit[i] = go.GetComponent<Image>();
                _orbit[i].raycastTarget  = false;
                _orbit[i].preserveAspect = true;

                // Y el mismo brillo que cualquier burbuja del tablero. Los sprites que van acá
                // son los cuerpos PLANOS, así que sin esto las tres de dentro salían mates
                // mientras la que las lleva brillaba (reportado por Diego).
                //
                // Se añade solo al crearla: las hijas se reusan entre Apply y Apply, y repetirlo
                // iría apilando un cristal encima de otro cada vez que el cañón carga.
                BubbleGlow.AddTo(_orbit[i]);
            }

            _orbit[i].sprite = _orbitCfg.sprites[i];
            _orbit[i].color  = Color.white;
            _orbit[i].gameObject.SetActive(true);

            // Entre el fondo y lo que venga después, en el orden en que se montan.
            _orbit[i].transform.SetAsLastSibling();
        }

        PlaceOrbit();
    }

    // A qué distancia del centro está esa hija ahora mismo. Se acerca y se aleja entre el mínimo
    // y el máximo, en vez de quedarse en un valor: con la distancia fija las tres describen
    // circunferencias y el conjunto se lee como un engranaje.
    //
    // El vaivén va con su propio RELOJ y no con el ángulo. Atado al ángulo iba al ritmo del giro,
    // así que subir la velocidad de la órbita aceleraba también la respiración y dejaba de
    // parecer respirar. Separados, una puede girar deprisa mientras late despacio.
    float RadiusOf(int index)
    {
        float low  = _orbitCfg.radius;
        float high = Mathf.Max(low, _orbitCfg.radiusMax);

        if (high - low < 0.0001f) return low;

        float phase = _orbitPhase  != null && _orbitPhase.Length  > index ? _orbitPhase[index]  : 0f;
        float rate  = _orbitBreath != null && _orbitBreath.Length > index ? _orbitBreath[index] : 1f;

        float wave = 0.5f + 0.5f * Mathf.Sin(_orbitClock * _orbitCfg.breathPerSecond * rate * Mathf.PI * 2f + phase);

        return Mathf.Lerp(low, high, wave);
    }

    void TickOrbit()
    {
        if (_orbitCfg == null) return;

        _orbitAngle += _orbitCfg.speed * Time.deltaTime;
        _orbitClock += Time.deltaTime;
        PlaceOrbit();
    }

    void PlaceOrbit()
    {
        if (_orbitCfg == null || _orbit == null) return;

        int count = _orbitCfg.sprites.Length;
        float half = _orbitCfg.size * 0.5f;

        for (int i = 0; i < count; i++)
        {
            if (_orbit[i] == null) continue;

            // Repartidas en el círculo. Arrancan desde arriba para que con tres quede el triángulo
            // apuntando hacia arriba, que es como se dibujaría a mano.
            float angle = (_orbitAngle + 90f + i * 360f / count) * Mathf.Deg2Rad;

            float radius = RadiusOf(i);

            var center = new Vector2(0.5f + Mathf.Cos(angle) * radius,
                                     0.5f + Mathf.Sin(angle) * radius);

            var rt = (RectTransform)_orbit[i].transform;
            rt.anchorMin = center - new Vector2(half, half);
            rt.anchorMax = center + new Vector2(half, half);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
