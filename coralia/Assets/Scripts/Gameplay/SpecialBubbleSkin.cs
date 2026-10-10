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
    Image   _glow;
    Graphic _host;   // la Image sobre la que se montaron las capas

    Spark    _sparkCfg;
    int      _sparkFrame;
    float    _sparkTime;
    float    _pulseTime;

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
        skin._glow   = skin.Layer(skin._glow,   "SpecialGlow",   BoosterRules.GlowFor(booster));

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
        if (skin._glow)   skin._glow.gameObject.SetActive(false);
    }

    // Las capas acompañan el desvanecido de la burbuja (pop, caída). Sin esto la bomba se
    // apaga pero su giro queda flotando opaco hasta que el objeto se destruye.
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

        if (_back   != null && _back.gameObject.activeSelf   != visible)      _back.gameObject.SetActive(visible);

        if (_orbit != null)
        {
            foreach (var child in _orbit)
                if (child != null && child.gameObject.activeSelf != visible) child.gameObject.SetActive(visible);

            if (visible) TickOrbit();
        }

        if (_core   != null && _core.gameObject.activeSelf   != sparkVisible) _core.gameObject.SetActive(sparkVisible);
        if (_sparkR != null && _sparkR.gameObject.activeSelf != sparkVisible) _sparkR.gameObject.SetActive(sparkVisible);
        if (_sparkL != null && _sparkL.gameObject.activeSelf != sparkVisible) _sparkL.gameObject.SetActive(sparkVisible);
        if (_glow   != null && _glow.gameObject.activeSelf   != visible)      _glow.gameObject.SetActive(visible);

        if (sparkVisible) TickSpark();

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
