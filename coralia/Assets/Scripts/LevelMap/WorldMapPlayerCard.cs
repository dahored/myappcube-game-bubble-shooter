using System.Collections;
using UnityEngine;

// La tarjeta con el avatar, al lado del nivel que le toca jugar. Al completar uno, se desliza
// desde ese nodo hasta el siguiente.
//
// Es su propio componente y no parte de WorldMapNodes porque no es un nodo: no se recicla en un
// pool, no se toca, y hay exactamente una. Calcula su sitio con los mismos números del mundo
// (WorldMapDefinition + WorldMapPath), así que no necesita que los nodos le cuenten nada más que
// su tamaño.
//
// Sigue las mismas convenciones que la tarjeta del mapa esférico (WorldSphereNodePositioner):
// la separación sale de los tamaños reales y no de un número a ojo, y el lado se elige solo.
[ExecuteAlways]
public class WorldMapPlayerCard : MonoBehaviour
{
    [Tooltip("Tarjeta con el avatar que acompaña al nivel actual. Vacío = no se muestra.")]
    [SerializeField] RectTransform cardPrefab;

    [Tooltip("Ancho de la tarjeta en unidades de MUNDO, igual criterio que 'Node Size'.")]
    [SerializeField] float cardSize = 1.4f;

    [Tooltip("Aire entre el borde del nodo y el borde de la tarjeta, en unidades de MUNDO. La distancia se calcula sola a partir de los dos tamaños; esto es solo el respiro entre ambos.")]
    [SerializeField] float gap = 0.3f;

    [Tooltip("El lado se elige solo: la tarjeta cae al contrario de hacia donde se desvió el nodo por el zigzag. Marca esto si prefieres el lado opuesto al que salga.")]
    [SerializeField] bool flip = false;

    [Tooltip("De dónde sale 'Node Size'. Vacío: se busca solo.")]
    [SerializeField] WorldMapNodes nodes;

    [Tooltip("Cuánto se levanta del suelo.")]
    [SerializeField] float height = 0.1f;

    [Tooltip("Encarada a la cámara, como una figurita parada. Apagado, se acuesta sobre el suelo siguiendo la pendiente, como los nodos.")]
    [SerializeField] bool faceCamera = true;

    [Tooltip("Inclinación propia, en grados. Se suma a la que le toque por 'Face Camera'.")]
    [SerializeField] float tilt = 0f;

    [Tooltip("Cuánto se adelanta respecto de las decoraciones que están a su misma profundidad.")]
    [SerializeField] int sortingBias = 3;

    [Header("Salto al nivel siguiente")]
    [Tooltip("Cuánto dura el salto hasta el nivel siguiente después de ganar, en segundos.")]
    [SerializeField] float moveDuration = 0.7f;

    [Tooltip("Cuánto se eleva en el aire, en unidades de mundo. En 0 se desliza por el suelo.")]
    [SerializeField] float jumpHeight = 0.8f;

    [Tooltip("Cuánto se deforma al impulsarse y al caer. 0.2 = hasta un 20% más alta o más ancha.")]
    [Range(0f, 0.5f)]
    [SerializeField] float squashAmount = 0.18f;

    [Tooltip("Cuánto dura el temblor de gelatina al aterrizar, en segundos.")]
    [SerializeField] float jellyDuration = 0.35f;

    [Header("Llegada tras ganar")]
    [Tooltip("De dónde sale el scroll, para centrar el mapa y bloquearlo mientras dura la llegada. Vacío: se busca solo.")]
    [SerializeField] WorldMapScroll scroll;

    [Tooltip("Cuánto se espera antes de saltar. Tiene que alcanzar para que las burbujas de la transición terminen de destapar la pantalla.")]
    [SerializeField] float startDelay = 0.7f;

    [Tooltip("Cuánto tarda el mapa en recorrer del nodo viejo al nuevo. Más largo que el salto se ve como que la cámara acompaña con calma.")]
    [SerializeField] float followDuration = 1.4f;

    [Tooltip("Hasta qué distancia de la cámara se dibuja. Más allá se apaga.")]
    [SerializeField] float cullRange = 90f;

    const string CARD_NAME = "PlayerCard";

    WorldMapDefinition _definition;
    Camera             _camera;
    RectTransform      _card;
    Canvas             _canvas;

    // Índice DE MUNDO donde está parada. Es float porque durante el deslizamiento queda a mitad
    // de camino entre dos nodos.
    float _at;
    bool  _placed;

    // Lo que anima el salto y consume Place(): cuánto está levantada del suelo y cuánto
    // deformada. Van aparte de la posición porque la posición la manda el índice de mundo.
    float _hop;
    float _jelly;

    // De qué lado del camino está, entre -1 y 1. Se interpola durante el salto en vez de sacarse
    // del zigzag en cada frame: el zigzag cambia de signo a mitad de tramo, y recalcularlo ahí
    // teletransportaba la tarjeta de un lado al otro en pleno vuelo.
    float _lateral;

    Coroutine _move;
    bool      _needsRebuild = true;

    void OnEnable()   => _needsRebuild = true;
    void OnValidate() => _needsRebuild = true;

    void LateUpdate()
    {
        if (_needsRebuild)
        {
            _needsRebuild = false;
            Build();
        }

        if (_card == null || _definition == null) return;

        if (!_placed) Settle();
        else if (_move == null && Application.isPlaying)
        {
            // Si el nivel actual cambió por otra vía, se reacomoda sin animación.
            float target = WorldIndexOf(SaveManager.MaxUnlockedLevel);
            if (!Mathf.Approximately(target, _at)) { _at = target; _lateral = SideOf(_at); }
        }

        Place();
    }

    // La primera colocación. Si viene de completar un nivel, arranca en el anterior y se desliza;
    // es la misma marca de sesión que consume el mapa esférico.
    //
    // En modo edición no se desliza: sería una animación que el jugador nunca ve, y consumir la
    // marca desde el editor se la robaría a la partida.
    void Settle()
    {
        _placed  = true;
        _at      = WorldIndexOf(SaveManager.MaxUnlockedLevel);
        _lateral = SideOf(_at);

        if (!Application.isPlaying) return;

        int from = SaveManager.ConsumeJustAdvancedFromLevel();
        if (from <= 0) return;

        _at      = WorldIndexOf(from);
        _lateral = SideOf(_at);
        _move    = StartCoroutine(Arrive(from, _at, WorldIndexOf(SaveManager.MaxUnlockedLevel)));
    }

    // La llegada al mapa después de ganar, de punta a punta.
    //
    // La maneja la tarjeta y no el pin ni el scroll porque es una sola secuencia: dónde arranca la
    // vista, cuánto se espera, cuándo salta y cuándo se le devuelve el control al jugador. Repartida
    // entre tres scripts, basta que uno cambie su tiempo para que la animación se vea a medias.
    IEnumerator Arrive(int completedLevel, float from, float to)
    {
        float spacing = _definition.Layout.spacing;

        if (scroll)
        {
            // Bloqueado desde el primer frame: si el jugador alcanzara a arrastrar, se perdería
            // justo la animación que estamos esperando a que se vea.
            scroll.Locked = true;
            scroll.CenterOn(from * spacing);
        }

        // Sin escalar y no WaitForSeconds: la espera es contra el reloj de la transición, que no
        // depende de la escala de tiempo del juego.
        yield return new WaitForSecondsRealtime(startDelay);

        // Primero las estrellas del nivel recién ganado, y recién después el salto. Es una
        // secuencia y no dos cosas a la vez: el jugador está mirando el nodo que acaba de
        // completar, y mover la tarjeta al mismo tiempo le parte la atención en dos.
        if (nodes) yield return nodes.Reveal(completedLevel);

        var follow = scroll ? StartCoroutine(FollowMap(from * spacing, to * spacing)) : null;

        yield return Jump(from, to);
        if (follow != null) yield return follow;

        if (scroll) scroll.Locked = false;
        _move = null;
    }

    // El mapa acompaña con su propio tiempo, más largo que el salto: la tarjeta llega primero y la
    // vista termina de acomodarse detrás, que es lo que hace que no se sienta un corte.
    IEnumerator FollowMap(float from, float to)
    {
        for (float t = 0f; t < 1f;)
        {
            t += Step() / Mathf.Max(0.01f, followDuration);
            scroll.CenterOn(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t))));
            yield return null;
        }
        scroll.CenterOn(to);
    }

    void Build()
    {
        _definition = WorldMapDefinition.For(this);
        if (_definition == null || cardPrefab == null) return;

        if (nodes  == null) nodes  = FindAnyObjectByType<WorldMapNodes>();
        if (scroll == null) scroll = FindAnyObjectByType<WorldMapScroll>();

        // La referencia no se serializa, pero el objeto instanciado sobrevive a una recompilación
        // de scripts. Sin adoptarlo, cada recompilación creaba otra tarjeta encima de la anterior.
        if (_card == null)
            foreach (Transform child in transform)
                if (child.name == CARD_NAME && child is RectTransform rect) { _card = rect; break; }

        if (_card != null)
        {
            _canvas = _card.GetComponent<Canvas>();
            return;
        }

        var card = Instantiate(cardPrefab, transform, false);
        card.name = CARD_NAME;

        // En el GameObject y NO en el RectTransform: poner hideFlags en el componente no evita
        // que el objeto se guarde en la escena, y al abrirla aparecía el guardado más el recién
        // creado — las dos tarjetas.
        card.gameObject.hideFlags = HideFlags.DontSave;

        // El prefab de la tarjeta es UI, y la UI necesita un Canvas para dibujarse. Acá no hay
        // ninguno, así que se le agrega uno propio en World Space. Si el prefab ya trae uno, se
        // respeta el suyo.
        _canvas = card.GetComponent<Canvas>();
        if (_canvas == null)
        {
            _canvas = card.gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
        }
        _canvas.worldCamera = Eye;

        _card = card;
    }

    void Place()
    {
        var layout = _definition.Layout;

        // La escala se aplica acá y no al crear la tarjeta: si se hiciera una sola vez, mover
        // 'Card Size' en el Inspector no cambiaría nada, porque la tarjeta ya está creada.
        //
        // El prefab mide en píxeles de UI: sin convertirlo, sale del tamaño de la isla.
        float source = cardPrefab.rect.width > 0.001f ? cardPrefab.rect.width : 1f;
        float scale  = cardSize / source;

        // Ancho y alto van al revés para que la tarjeta conserve más o menos su volumen: al
        // achatarse se ensancha y al estirarse se afina. Deformar solo el alto se lee como que la
        // tarjeta cambia de tamaño, no como que rebota.
        float jelly = _jelly * squashAmount;
        _card.localScale = new Vector3(scale * (1f - jelly), scale * (1f + jelly), scale);

        // La separación sale de los tamaños reales y no a ojo: medio nodo más media tarjeta más
        // el aire pedido. Así queda pegada aunque cambies 'Node Size' o el tamaño de la tarjeta.
        float nodeSize = nodes ? nodes.NodeSize : cardSize;
        float apart    = (nodeSize + cardSize) * 0.5f + gap;

        // Aparta solo en X, no por la perpendicular: la tarjeta tiene que quedar a la misma altura
        // de pantalla que su nodo, y en un tramo diagonal la perpendicular la adelantaría.
        Vector3 ground = WorldMapPath.SampleOffset(_at, layout, _lateral * apart, false);
        ground.y += height + _hop;

        Vector3 world = transform.TransformPoint(ground);
        _card.localPosition = transform.InverseTransformPoint(WorldMapCurve.Curve(world, Eye));

        // La curva la aplica C# y no el shader: esto es un Canvas con UI, no pasa por el nuestro.
        _card.localRotation = faceCamera && Eye != null
            ? Quaternion.Euler(Eye.transform.eulerAngles.x + tilt, 0f, 0f)
            : Quaternion.Euler(tilt + WorldMapCurve.Pitch(world, Eye), 0f, 0f);

        if (_canvas) _canvas.sortingOrder = _definition.SortingOrder(ground.z) + sortingBias;

        if (Eye != null)
        {
            float distance = world.z - Eye.transform.position.z;
            bool  visible  = distance > -cullRange * 0.25f && distance < cullRange;
            if (_card.gameObject.activeSelf != visible) _card.gameObject.SetActive(visible);
        }
    }

    // El salto al nivel siguiente, en dos tramos: el vuelo y el temblor al aterrizar.
    //
    // Tiempo sin escalar: si algún día el mapa se abre con el juego en pausa, el salto igual
    // tiene que correr.
    IEnumerator Jump(float from, float target)
    {
        float sideFrom = SideOf(from);
        float sideTo   = SideOf(target);

        for (float t = 0f; t < 1f;)
        {
            t += Step() / Mathf.Max(0.01f, moveDuration);

            float k  = Mathf.Clamp01(t);
            float e  = Mathf.SmoothStep(0f, 1f, k);
            _at      = Mathf.Lerp(from, target, e);
            _lateral = Mathf.Lerp(sideFrom, sideTo, e);
            _hop     = Mathf.Sin(k * Mathf.PI) * jumpHeight;
            _jelly   = Airborne(k);
            yield return null;
        }

        _at      = target;
        _lateral = sideTo;
        _hop     = 0f;

        // El temblor arranca en -1, justo donde lo deja el vuelo, así que el impacto no se ve
        // como un salto de escala sino como la continuación del aplastamiento.
        for (float t = 0f; t < 1f;)
        {
            t += Step() / Mathf.Max(0.01f, jellyDuration);

            float k = Mathf.Clamp01(t);
            _jelly  = -Mathf.Cos(k * Mathf.PI * 3f) * (1f - k);
            yield return null;
        }

        _jelly = 0f;
    }

    // El frame siguiente a cargar una escena puede traer un delta enorme: todo lo que tardó la
    // carga llega junto. Sin el tope, ese único frame se comía la animación entera y la tarjeta
    // aparecía ya puesta en el nodo nuevo.
    //
    // Sin escalar por el tiempo del juego: esto corre contra el reloj de la transición.
    static float Step() => Mathf.Min(Time.unscaledDeltaTime, 0.05f);

    // La deformación a lo largo del vuelo. Negativo achata, positivo estira.
    //
    // Es el squash & stretch de manual: se achata para tomar impulso, se estira en el aire donde
    // más rápido va, y se vuelve a achatar justo antes de tocar el suelo. Los tres tramos se
    // encuentran en cero salvo el último, que termina en -1 para enganchar con el temblor.
    static float Airborne(float k)
    {
        if (k < 0.15f) return -Mathf.Sin(k / 0.15f * Mathf.PI);
        if (k > 0.85f) return -Mathf.Sin((k - 0.85f) / 0.15f * Mathf.PI * 0.5f);

        return Mathf.Sin((k - 0.15f) / 0.7f * Mathf.PI) * 0.7f;
    }

    // Al lado CONTRARIO de hacia donde el zigzag desvió al nodo: así la tarjeta siempre cae hacia
    // el centro del camino y nunca se va contra el borde de la pantalla.
    float SideOf(float index)
    {
        float side = WorldMapPath.Sample(index, _definition.Layout).x > 0f ? -1f : 1f;
        return flip ? -side : side;
    }

    // De id de nivel a índice de mundo, contando las separaciones entre capítulos.
    float WorldIndexOf(int levelId)
    {
        var entries = LevelLoader.Entries;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].id == levelId) return _definition.WorldIndex(i);

        return 0f;
    }

    Camera Eye => _camera != null ? _camera : _camera = Camera.main;
}
