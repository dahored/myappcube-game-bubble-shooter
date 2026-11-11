using System.Collections;
using System.Collections.Generic;
using Solo.MOST_IN_ONE;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// IPointerClickHandler: permite tocar directamente una burbuja del grid para apuntar y
// disparar hacia ella (pedido de Diego, visto en otros bubble shooters) — alternativa rápida
// al drag normal desde el cañón. CannonController se suscribe vía GridController.OnBubbleTapped.
//
// También reenvía drag (IBeginDrag/IDrag/IEndDrag): el sistema de eventos de Unity solo sube
// por los ANCESTROS del objeto tocado buscando quién maneja el drag, nunca cruza a hermanos —
// como AimArea es hermano de las burbujas (no ancestro), si el gesto de apuntado empieza
// justo encima de una burbuja, nunca llegaba a AimInputRelay. Con gran parte de la pantalla
// cubierta de burbujas, en la práctica el drag solo funcionaba en los huecos vacíos (reportado
// por Diego, comparando con otros bubble shooters que dejan apuntar desde cualquier lado).
public class BubbleView : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler,
                          IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("La burbuja — lo que lleva CUALQUIERA")]
    [Tooltip("El cuerpo: el sprite de color, o el de la burbuja de un poder.")]
    [SerializeField] Image bubbleImage;

    [Header("Lo que lleve dentro — solo las de rescate")]
    [Tooltip("La criatura o el objeto atrapado, animado si su entrada del catálogo trae varios cuadros.")]
    [SerializeField] Image creatureIcon;

    public event System.Action<BubbleView> OnTapped;

    // El dedo APOYADO, aparte del toque completo. Lo usa el modo de elegir del Punto Débil para
    // enseñar qué se va a llevar antes de soltar — y para que deslizar fuera y soltar sea un
    // arrepentimiento, no una compra.
    public event System.Action<BubbleView> OnPressed;

    // El dedo ENTRA en esta burbuja, también si llega arrastrando desde otra. Se lo preguntamos
    // al sistema de eventos en vez de traducir la posición a celda por nuestra cuenta: la cuenta
    // depende del pivote y del desplazamiento del tablero, y marcaba la de al lado.
    public event System.Action<BubbleView> OnHovered;
    public event System.Action<BubbleView> OnUnhovered;
    public event System.Action<Vector2> OnDragBegin;
    public event System.Action<Vector2> OnDragMove;
    public event System.Action<Vector2> OnDragEnd;

    public void OnPointerClick(PointerEventData eventData) => OnTapped?.Invoke(this);

    // El dedo apoyado marca el objetivo; moverlo y soltarlo ya tienen sus propios eventos de
    // arrastre, así que no hace falta escuchar el 'Up'.
    public void OnPointerDown(PointerEventData eventData)  => OnPressed?.Invoke(this);
    public void OnPointerEnter(PointerEventData eventData) => OnHovered?.Invoke(this);
    public void OnPointerExit(PointerEventData eventData)  => OnUnhovered?.Invoke(this);
    public void OnBeginDrag(PointerEventData eventData)    => OnDragBegin?.Invoke(eventData.position);
    public void OnDrag(PointerEventData eventData)         => OnDragMove?.Invoke(eventData.position);
    public void OnEndDrag(PointerEventData eventData)      => OnDragEnd?.Invoke(eventData.position);

    const float POP_DURATION      = 0.25f; // GDD 1.5 — animación de explosión
    const float DROP_GRAVITY      = 2600f; // px/s² — caída acelerada en vez de velocidad constante, se siente más real
    const float DROP_MAX_DURATION = 1f;    // tope de seguridad
    const float DROP_FADE_START   = 0.5f;  // a partir de acá empieza a desvanecerse

    // Cuánto tarda una burbuja en caer 'distance' px. Es gravedad pura desde reposo, así que
    // sale de la fórmula sin simular nada — lo usa GridController para saber CUÁNDO mostrar el
    // número de puntos: recién cuando la burbuja llegó abajo y desapareció.
    //
    // Con el mismo tope que la animación: pasado DROP_MAX_DURATION la burbuja ya se destruyó,
    // y un número que salga después de eso no acompaña a nada.
    public static float DropDuration(float distance) =>
        Mathf.Min(DROP_MAX_DURATION, Mathf.Sqrt(2f * Mathf.Max(0f, distance) / DROP_GRAVITY));

    const int   POP_PARTICLE_COUNT    = 8;
    const float POP_PARTICLE_SIZE     = 22f;  // px — bastante más chico que la burbuja (92px)
    const float POP_PARTICLE_SPEED    = 260f; // px/s
    const float POP_PARTICLE_LIFETIME = 0.35f;

    public Vector2Int    Cell       { get; private set; }
    public BubbleColor   ColorType  { get; private set; }
    public bool          IsCreature { get; private set; }

    // Qué criatura encierra, si encierra alguna. Lo guarda la burbuja y no una tabla aparte
    // porque es suyo: al reventar hay que saber a quién se liberó para sumarlo al objetivo y
    // para tocar SU sonido, y un mapa de celda a criatura se desincroniza en cuanto el tablero
    // se desplaza o una burbuja cae.
    public string CreatureId { get; private set; }

    // Cuántos golpes le quedan a su caparazón. Cero es "a la vista": el siguiente golpe la libera.
    public int LayersLeft { get; private set; }

    // Lo que suena al romperse una de sus capas. Lo guarda la BURBUJA y no quien la golpea
    // porque el caparazón le llega por dos caminos —la receta de un rescate y la celda de un
    // obstáculo suelto— y preguntárselo a la receta deja al obstáculo mudo, que no tiene.
    public AudioClip LayerClip { get; private set; }

    // Si además de llevar criatura TIENE color, y entonces matchea como una burbuja normal. Lo
    // decide el nivel al colocarla, no la receta: la misma criatura puede ir suelta en un nivel y
    // metida en una verde en otro.
    public bool CreatureMatches { get; private set; }

    bool _hasBody;

    // El cuerpo se pinta aparte del color de match: la burbuja puede verse de un color y encajar
    // con otro, o con ninguno.
    public void SetBody(Sprite sprite)
    {
        if (bubbleImage != null && sprite != null) bubbleImage.sprite = sprite;
    }
    public Booster Special    { get; private set; }

    // Una mina plantada ocupa su celda pero no tiene color: no encaja con nada, no cuenta como
    // color del tablero y no entra en ningún grupo. Lo único que hace es estallar cuando algo la
    // toca. Se pregunta en un solo sitio para que nadie tenga que acordarse de la regla.
    public bool IsMine => Special == Booster.Mine;

    // El poder que lleva esta burbuja, con su arte. Las capas van sobre la raíz (este mismo
    // objeto), encima del sprite de color: una bomba no se mezcla con su color, lo tapa.
    public void SetSpecial(Booster special)
    {
        Special = special;

        if (special != Booster.None) SpecialBubbleSkin.Apply(this, special);
        else                         RefreshColorSkin();   // al descargar vuelve el skin del color, si lo tiene
    }

    public void Setup(Vector2Int cell, BubbleColor color, Sprite sprite)
    {
        Cell      = cell;
        ColorType = color;
        if (bubbleImage) bubbleImage.sprite = sprite;
        RefreshColorSkin();
        RefreshGlow();

        StopNudge();

        var rt = (RectTransform)transform;
        rt.anchoredPosition = HexGridMath.CellToLocalPos(cell);
    }

    public void SetCell(Vector2Int cell) => Cell = cell;

    // Cambia el color sin tocar la posición, para la arcoíris que adopta el color al aterrizar.
    public void SetColor(BubbleColor color, Sprite sprite)
    {
        ColorType = color;
        if (bubbleImage) bubbleImage.sprite = sprite;
        RefreshColorSkin();
    }

    // La arcoíris también es una burbuja especial, así que lleva el brillo esférico del catálogo
    // igual que la bomba. Lo pide el color y no un booster cargado: una arcoíris puede venir del
    // JSON de un nivel, de la Perla o de la cola del cañón, y en los tres casos se ve igual.
    //
    // El booster manda sobre el color: una bomba tapa lo que haya debajo, incluido esto.
    void RefreshColorSkin()
    {
        if (Special != Booster.None) return;

        if (ColorType == BubbleColor.Rainbow) SpecialBubbleSkin.Apply(this, Booster.Rainbow);
        else                                  SpecialBubbleSkin.Clear(this);
    }

    // Mete una criatura dentro de esta burbuja, con el arte que diga el catálogo. Un solo cuadro
    // se queda quieto; varios se pasan en bucle, que es lo que distingue a un bicho vivo esperando
    // a que lo saquen de una calcomanía pegada en el cristal.
    public void SetCreature(string creatureId) => SetCreature(creatureId, null, false);

    public void SetCreature(string creatureId, RescueBubbleCatalog.Recipe recipe) =>
        SetCreature(creatureId, recipe, false);

    // 'recipe' trae el arte de la BURBUJA —fondo, brillo y capas—; el catálogo de criaturas trae
    // el de lo que lleva dentro. Separados a propósito: lo de dentro se define una vez y la
    // burbuja se compone tantas veces como variantes pida el diseño de niveles.
    // 'matches' es si encaja por color; 'body' es si tiene cuerpo que dibujar. Van separados
    // porque son dos decisiones del nivel y ninguna implica la otra.
    public void SetCreature(string creatureId, RescueBubbleCatalog.Recipe recipe, bool matches, bool body = false)
    {
        CreatureMatches = matches;
        _hasBody        = body;

        IsCreature = !string.IsNullOrEmpty(creatureId);
        CreatureId = IsCreature ? creatureId : null;

        // Lo vivo respira dentro de su burbuja; un cofre no. Sale de en qué lista del catálogo
        // esté puesto, igual que el rebote de cuando sale.
        _breathes = IsCreature && Rescuable.IsCreature(creatureId);

        // Cada una arranca su respiración por un sitio. Empezando todas a la vez, un tablero con
        // tres bichos los tendría latiendo al unísono, que se lee como un efecto y no como vida.
        _breathTime = Random.Range(0f, 10f);

        _creatureFrames = IsCreature ? Rescuable.Frames(creatureId) : null;
        _creatureFps    = IsCreature ? Rescuable.Fps(creatureId)    : 0f;
        _creatureFrame  = 0;
        _creatureTime   = 0f;
        _blinkStep      = 0;

        // El primer parpadeo se hace esperar un rato distinto en cada burbuja, igual que la
        // respiración. Con la misma espera, las tres tortugas de un tablero parpadean al
        // unísono y eso se lee como un efecto, no como tres bichos (reportado por Diego).
        _blinkWait      = Random.Range(0f, BLINK_MAX);

        if (creatureIcon == null) return;

        bool draws = _creatureFrames != null && _creatureFrames.Length > 0;

        if (draws) creatureIcon.sprite = _creatureFrames[0];

        if (creatureIcon.gameObject.activeSelf != draws) creatureIcon.gameObject.SetActive(draws);

        // El cuerpo se enseña si el nivel le dio uno. Es independiente del match: una criatura
        // puede verse verde y abrirse con cualquier match, o no tener cuerpo y encajar solo con
        // las verdes. Con cuerpo, el fondo de la receta no se usa — ya hay algo detrás.
        if (bubbleImage != null && bubbleImage.enabled != _hasBody) bubbleImage.enabled = _hasBody;

        var back = IsCreature && !_hasBody ? recipe?.background : null;
        // El brillo es de la BURBUJA, no de lo que lleve: una de rescate lo saca de su receta y
        // una de color del catálogo común. Vacío en los dos sitios significa que el cuerpo ya lo
        // trae pintado, que es como están hoy los sprites.
        BuildLayers();
        Dress(_back, back);

        _recipe = recipe;
        RefreshGlow();

        SetShell(recipe != null ? recipe.Layers() : System.Array.Empty<Sprite>(),
                 RescueBubbleCatalog.LayerBurst(recipe));

        if (IsCreature && !draws)
            Debug.LogWarning($"[BubbleView] La criatura '{creatureId}' no tiene arte en ningún catálogo ni icono genérico: la burbuja que la encierra se va a ver vacía.", this);
    }

    // Le pone el caparazón: las capas que lo forman y lo que suena al romperse una.
    //
    // Va aparte de SetCreature porque el caparazón NO es de la criatura. Una burbuja de color
    // puede llevarlo sin nada dentro —eso es un obstáculo— y una criatura puede ir suelta sin
    // él. Metido dentro de SetCreature, el obstáculo tendría que fingir que lleva algo.
    //
    // Las capas se visten de dentro hacia afuera: la primera de la lista queda pegada a lo que
    // haya debajo y la última encima de todas, que es la primera que se cae.
    public void SetShell(Sprite[] layers, AudioClip breakClip)
    {
        // Se construye las suyas: un obstáculo suelto no pasa por SetCreature, y dependiendo de
        // que otro las hubiera hecho antes se quedaría sin caparazón según por dónde lo monten.
        BuildLayers();

        layers    = layers ?? System.Array.Empty<Sprite>();
        LayerClip = breakClip;

        if (_shells == null) { LayersLeft = 0; return; }

        // Más capas que huecos no se cuentan: una que no se dibuja sería un golpe invisible.
        LayersLeft = Mathf.Min(layers.Length, _shells.Length);

        for (int i = 0; i < _shells.Length; i++)
            Dress(_shells[i], i < layers.Length ? layers[i] : null);

        ShadeBody();
    }

    // Lo apagado que queda el cuerpo debajo del caparazón.
    //
    // Público porque la demostración del tutorial compone sus burbujas a mano, sin pasar por
    // esta clase, y una burbuja tapada que allí se viera a otro brillo enseñaría algo que no es.
    public const float SHELL_SHADE = 0.5f;

    // El color de debajo se ve APAGADO mientras quede caparazón, y recupera su brillo al quedar
    // destapado.
    //
    // Las capas finas dejan ver el color por los huecos, y a brillo pleno eso se lee como una
    // burbuja normal medio tapada: el jugador va a buscarle su color y resulta que la abre
    // cualquier match. Apagado se entiende que está ahí pero todavía no cuenta — y el cambio de
    // apagado a encendido es justo lo que avisa de que ya se puede matchear (pedido de Diego).
    void ShadeBody() => ShadeBody(LayersLeft > 0 ? SHELL_SHADE : 1f);

    void ShadeBody(float lit)
    {
        if (bubbleImage == null) return;

        var c = bubbleImage.color;

        bubbleImage.color = new Color(lit, lit, lit, c.a);
    }

    // Le quita una capa. Devuelve si con eso quedó a la vista, que es cuando el siguiente golpe
    // la libera.
    //
    // Se rompe la de FUERA, que es la del índice más alto porque es la que se dibuja encima.
    // 'LayersLeft - 1' es además el índice de esa capa en 'Layers()', que es con lo que quien
    // llame busca su sonido.
    public bool BreakLayer()
    {
        if (LayersLeft <= 0) return true;

        int outer = LayersLeft - 1;

        if (_shells != null && outer < _shells.Length && _shells[outer] != null)
            StartCoroutine(ShatterLayer(_shells[outer]));

        LayersLeft--;

        ShadeBody();

        return LayersLeft <= 0;
    }

    // Lo que tarda una capa en romperse. Público porque quien encadene algo DETRÁS de la rotura
    // —el traslado de lo que se queda sin techo— necesita saber cuándo termina.
    public const float SHATTER_TIME = 0.25f;

    // Cuántos trozos saltan de una capa y a qué velocidad. Son más grandes y más lentos que los
    // del pop de una burbuja: aquello es agua que revienta y esto son pedazos de roca.
    const int   SHARD_COUNT = 7;
    const float SHARD_SIZE  = 34f;  // px
    const float SHARD_SPEED = 190f; // px/s
    const float SHARD_LIFE  = 0.45f;

    // Lo que la burbuja se sacude al acusar el golpe, y cuánto dura.
    const float JOLT_SHAKE = 5f;    // px
    const float JOLT_TIME  = 0.14f;

    // La capa no se desvanece y ya: se parte.
    //
    // Tres cosas a la vez, y las tres hacen falta. La burbuja se sacude —el golpe lo recibe ELLA,
    // no solo su cáscara—, la capa salta hacia fuera girando, y de ella salen pedazos que se
    // dispersan. Con solo el fundido, tres capas eran la misma imagen aclarándose tres veces y no
    // se leía que algo se hubiera roto (pedido de Diego).
    IEnumerator ShatterLayer(Image layer)
    {
        var rt = (RectTransform)layer.transform;

        Shards(layer);
        StartCoroutine(Jolt());

        const float TIME = SHATTER_TIME;

        // Hacia dónde se abre esta capa. Al azar por capa, para que las tres de un mismo
        // caparazón no se rompan calcadas.
        float spin = Random.Range(-28f, 28f);

        for (float t = 0f; t < TIME; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / TIME);

            // Salta de golpe y se va apagando: el crecimiento se gasta pronto —raíz, no lineal—
            // y la opacidad aguanta un poco más, así se ve ROMPERSE y no sólo desaparecer.
            rt.localScale       = Vector3.one * (1f + 0.5f * Mathf.Sqrt(p));
            rt.localEulerAngles = new Vector3(0f, 0f, spin * p);
            layer.color         = new Color(1f, 1f, 1f, 1f - p * p);
            yield return null;
        }

        rt.localScale       = Vector3.one;
        rt.localEulerAngles = Vector3.zero;
        layer.color         = Color.white;

        // El COMPONENTE y no el objeto: es el mismo interruptor que usa Dress, y apagando el
        // objeto una capa vuelta a vestir se quedaría invisible mientras LayersLeft dice que
        // está puesta.
        layer.enabled = false;
    }

    // La entrada del caparazón al empezar el nivel.
    public const float INTRO_TIME  = 0.3f;
    const        float INTRO_SCALE = 3.2f;   // de qué tamaño llega, o sea cuán "de frente" viene

    List<Image> _introHidden;

    // Esconde el caparazón para que llegue después, cuando el nivel ya esté presentado.
    //
    // Va SEPARADO de la llegada porque los dos momentos no son el mismo: esconder tiene que pasar
    // mientras se arma el tablero, antes del primer frame dibujado, y llegar tiene que pasar al
    // final de la presentación. Juntos, o el obstáculo abría ya montado o se esfumaba a mitad de
    // la apertura para volver a entrar (reportado por Diego).
    public void HideShellForIntro()
    {
        if (_shells == null || LayersLeft <= 0) return;

        _introHidden = new List<Image>();

        // A brillo pleno mientras el caparazón no está puesto. Apagado desde el principio, la
        // burbuja abría el nivel oscura sin que nada explicara por qué (reportado por Diego).
        ShadeBody(1f);

        foreach (var shell in _shells)
            if (shell != null && shell.enabled)
            {
                shell.color = new Color(1f, 1f, 1f, 0f);
                ((RectTransform)shell.transform).localScale = Vector3.one * INTRO_SCALE;
                _introHidden.Add(shell);
            }
    }

    // El caparazón llega: viene de frente, grande y transparente, y se asienta sobre la burbuja.
    // Al llegar suena el material y la burbuja acusa el peso.
    //
    // No es adorno: un obstáculo que ya está puesto cuando se levanta el telón es parte del
    // decorado, y uno que llega delante del jugador es algo que le han puesto ahí. Lo segundo es
    // lo que hace que se fije en él antes de disparar (pedido de Diego).
    public void PlayShellIntro(float delay)
    {
        if (_introHidden == null || _introHidden.Count == 0) return;

        StartCoroutine(ShellIntro(delay));
    }

    IEnumerator ShellIntro(float delay)
    {
        var shown = _introHidden;

        _introHidden = null;

        if (delay > 0f) yield return new WaitForSeconds(delay);

        for (float t = 0f; t < INTRO_TIME; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / INTRO_TIME);

            // Frena al llegar, en vez de caer a plomo: una cúbica invertida deja casi todo el
            // recorrido en el primer tercio del tiempo y el resto es el asiento.
            float e = 1f - Mathf.Pow(1f - p, 3f);

            float scale = Mathf.Lerp(INTRO_SCALE, 1f, e);

            foreach (var shell in shown)
            {
                ((RectTransform)shell.transform).localScale = Vector3.one * scale;
                shell.color = new Color(1f, 1f, 1f, e);
            }

            // El color de debajo se apaga AL MISMO RITMO: es el caparazón posándose lo que lo
            // tapa, así que las dos cosas son una sola y tienen que ir juntas.
            ShadeBody(Mathf.Lerp(1f, SHELL_SHADE, e));

            yield return null;
        }

        foreach (var shell in shown)
        {
            ((RectTransform)shell.transform).localScale = Vector3.one;
            shell.color = Color.white;
        }

        ShadeBody();

        // Y el golpe de asentarse, con el sonido del material: es lo que convierte el aterrizaje
        // en algo que ocurrió y no en una imagen que apareció.
        AudioManager.Instance?.PlaySfx(LayerClip);

        yield return Jolt();
    }

    // Los pedazos que saltan de una capa al romperse. Llevan el sprite de la PROPIA capa, así
    // que el hielo suelta astillas de hielo y la piedra cascotes, sin un set de partículas por
    // material: el arte que ya está puesto sirve para las dos cosas.
    //
    // Cuelgan del padre del grid y no de la burbuja, igual que los del pop: dentro de ella, lo
    // que se mueva o se destruya después se los lleva a mitad de vuelo.
    void Shards(Image layer)
    {
        if (layer == null || layer.sprite == null) return;

        var parent = transform.parent;
        var origin = ((RectTransform)transform).anchoredPosition;

        for (int i = 0; i < SHARD_COUNT; i++)
        {
            var go = new GameObject("Shard", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;

            rt.SetParent(parent, false);
            rt.anchoredPosition = origin;
            rt.sizeDelta        = Vector2.one * SHARD_SIZE * Random.Range(0.7f, 1.3f);
            rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));

            var img = go.GetComponent<Image>();

            img.sprite        = layer.sprite;
            img.raycastTarget = false;

            // Repartidos en círculo con un poco de desorden: a ángulos exactos salen en estrella
            // y eso se lee como un efecto, no como algo que se partió.
            float   angle = (360f / SHARD_COUNT) * i + Random.Range(-22f, 22f);
            Vector2 dir   = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            go.AddComponent<PopParticle>().Init(origin, dir * SHARD_SPEED * Random.Range(0.7f, 1.3f), SHARD_LIFE);
        }
    }

    // La escala de una burbuja QUIETA. Todas las sacudidas de abajo —el tirón del golpe, el
    // encogerse antes de caer, el estirón de la caída, el rebote al posarse, el gesto de
    // cambiar de color— deforman alrededor de esta y vuelven exactamente a ella.
    //
    // Antes cada una guardaba la escala que encontraba al empezar y restauraba ESA. Dos que se
    // solaparan —y se solapan todo el rato: una burbuja que acusa un golpe mientras baja, o que
    // recibe dos derrumbes seguidos— hacían que la segunda tomara por reposo una deformada a
    // medias, y al terminar la dejaba así para siempre. Cada solape lo empeoraba, así que a los
    // pocos disparos las burbujas eran óvalos (reportado por Diego).
    static readonly Vector3 REST = Vector3.one;

    // El tirón de acusar el golpe: la burbuja se sacude en el sitio y vuelve.
    //
    // En la ESCALA y no en la posición: la posición ya la escriben el traslado y el empujón, y
    // una sacudida que se pelee con ellos deja la burbuja descuadrada en su celda.
    IEnumerator Jolt()
    {
        var start = REST;

        for (float t = 0f; t < JOLT_TIME; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / JOLT_TIME);

            // Se achata y se ensancha a la vez, y el temblor se apaga con el tiempo: el volumen
            // se conserva, que es lo que hace que parezca que encajó un golpe y no que creció.
            float squash = JOLT_SHAKE / 100f * Mathf.Sin(p * Mathf.PI * 3f) * (1f - p);

            transform.localScale = new Vector3(start.x * (1f + squash), start.y * (1f - squash), start.z);
            yield return null;
        }

        transform.localScale = start;
    }

    // Las capas se CREAN acá y no se piden en el Inspector: su arte ya sale del catálogo, así
    // que pedir además el hueco sería cablear a mano algo que el código puede montar solo — y
    // cinco huecos por prefab son cinco sitios donde olvidarse (pedido de Diego).
    //
    // Mismo criterio que SpecialBubbleSkin con las suyas: hideFlags para que no se serialicen
    // dentro del prefab, estiradas al host, y sin recibir toques.
    Image   _back;
    Image   _glow;
    Image[] _shells;

    void FadeRescueLayers(float alpha)
    {
        var tint = new Color(1f, 1f, 1f, alpha);

        if (creatureIcon) creatureIcon.color = tint;
        if (_back)        _back.color        = tint;
        if (_glow)        _glow.color        = tint;

        if (_shells == null) return;

        foreach (var shell in _shells) if (shell) shell.color = tint;
    }

    RescueBubbleCatalog.Recipe _recipe;

    Image MakeLayer(string label)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
        {
            hideFlags = HideFlags.DontSave,
        };

        var rt = (RectTransform)go.transform;

        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();

        image.raycastTarget = false;   // el toque tiene que llegar a la burbuja, no a su brillo
        image.enabled       = false;

        return image;
    }

    // El orden de dibujo es el de la jerarquía, así que se reafirma cada vez que se montan: el
    // fondo justo detrás de la criatura, el brillo encima de ella, y las capas sobre el brillo.
    void BuildLayers()
    {
        if (_glow != null) return;

        _back = MakeLayer("BubbleBack");

        if (creatureIcon != null) _back.transform.SetSiblingIndex(creatureIcon.transform.GetSiblingIndex());

        if (creatureIcon != null) creatureIcon.transform.SetAsLastSibling();

        _glow   = MakeLayer("BubbleGlow");
        _shells = new[] { MakeLayer("BubbleShell1"), MakeLayer("BubbleShell2"), MakeLayer("BubbleShell3") };
    }

    // El brillo es de la BURBUJA, no de lo que lleve dentro: una de rescate lo saca de su receta
    // y una de color del catálogo común.
    //
    // Se refresca desde Setup y no solo al meterle criatura: una burbuja de color NO pasa por
    // SetCreature nunca, así que metido ahí dentro el brillo solo lo veían las de rescate y el
    // tablero entero salía plano (reportado por Diego).
    //
    // Vacío significa que el cuerpo ya lo trae pintado, que es como estaban los sprites antes de
    // exportarlos planos — por eso no se avisa de que falte.
    void RefreshGlow()
    {
        BuildLayers();

        Dress(_glow, IsCreature ? RescueBubbleCatalog.Glow(_recipe) : BubbleCatalog.Glow);
    }

    // Pone el sprite y enciende o apaga la capa. Sin sprite no hay capa: una encendida con la
    // imagen de la burbuja anterior es peor que ninguna.
    //
    // Se enciende y apaga el COMPONENTE y no el objeto: estas capas las crea el código en el
    // primer Setup y apagar el objeto impediría que una corrutina suya terminara.
    static void Dress(Image layer, Sprite sprite)
    {
        if (layer == null) return;

        if (sprite != null) layer.sprite = sprite;

        layer.enabled = sprite != null;
    }

    Sprite[] _creatureFrames;
    float    _creatureFps;
    int      _creatureFrame;
    float    _creatureTime;
    float    _blinkWait;
    int      _blinkStep;
    bool     _breathes;
    float    _breathTime;

    // Cuánto se deforma al respirar. Muy poco a propósito: tiene que notarse que está vivo sin
    // que parezca que se está retorciendo.
    const float BREATH = 0.06f;

    // Cada cuánto parpadea. Los cuadros de una criatura NO son un bucle: son ojo abierto, medio
    // y cerrado, y pasados en bucle la tortuga parpadea una vez por segundo sin descanso — que
    // se lee como un GIF y no como un bicho esperando a que lo saquen (reportado por Diego).
    //
    // Va en código y no en el catálogo porque es lo mismo para todas: lo que cambia de una a
    // otra es el arte, no cada cuánto abre los ojos.
    const float BLINK_MIN = 2.5f;
    const float BLINK_MAX = 5.5f;

    // Lo que tarda el empujón en deshacerse.
    const float NUDGE_TIME = 0.3f;

    Coroutine _nudge;
    Vector2   _nudgeHome;

    void Update()
    {
        if (creatureIcon == null || !creatureIcon.gameObject.activeSelf) return;

        // La respiración va ANTES del avance de cuadros y fuera de su condición: un bicho de un
        // solo sprite —que es como van a estar casi todos— se quedaría tieso si dependiera de
        // tener animación, y es justo el que más la necesita.
        if (_breathes)
        {
            _breathTime += Time.deltaTime;

            creatureIcon.rectTransform.localScale = Vector3.one + Jelly.Idle(_breathTime, BREATH);
        }

        if (_creatureFrames == null || _creatureFrames.Length < 2 || _creatureFps <= 0f) return;

        // En reposo —ojos abiertos, el primer cuadro— hasta que toque el siguiente parpadeo. El
        // gesto dura un suspiro y la espera son segundos: es la espera la que hace que parezca
        // un parpadeo en vez de una animación.
        if (_blinkStep == 0)
        {
            _blinkWait -= Time.deltaTime;

            if (_blinkWait > 0f) return;

            _blinkStep    = 1;
            _creatureTime = 0f;
        }

        _creatureTime += Time.deltaTime;

        float step = 1f / _creatureFps;

        // El párpado sube POR DONDE BAJÓ: el cuadro del medio se pasa dos veces, una al cerrar y
        // otra al abrir. En círculo —que es como iba— el ojo se cerraba en dos pasos y se abría
        // de golpe en uno.
        int span = (_creatureFrames.Length - 1) * 2;

        // Se descuenta en vez de ponerlo a cero: con un fps alto y un frame largo pueden tocar
        // dos pasos de una, y reiniciando se perdería uno y el parpadeo iría más lento de lo que
        // dice el catálogo.
        while (_creatureTime >= step && _blinkStep > 0)
        {
            _creatureTime -= step;
            _blinkStep++;

            if (_blinkStep < span) continue;

            // Cerrado el gesto, vuelve al reposo y se sortea la siguiente espera. Sorteada cada
            // vez y no una sola, porque si no las que arrancaron juntas se quedan juntas.
            _blinkStep = 0;
            _blinkWait = Random.Range(BLINK_MIN, BLINK_MAX);
        }

        _creatureFrame = _blinkStep < _creatureFrames.Length ? _blinkStep : span - _blinkStep;

        creatureIcon.sprite = _creatureFrames[_creatureFrame];
    }

    // delay: para que el match no explote todo junto — GameplayController le pasa un
    // delay creciente según qué tan lejos está cada burbuja de la que disparó el jugador.
    // popClip: se reproduce acá, no en GameplayController — así el sonido (y la vibración
    // LightImpact que lo acompaña) suenan una vez POR burbuja, en el momento exacto en que
    // le toca explotar (mismo delay que la animación), no una sola vez para todo el match.
    // AudioManager.PlayPop ya usa PlayOneShot, así que varias burbujas superponiéndose no se
    // cortan entre sí.
    // Cuántas burbujas están explotando o cayendo ahora mismo. El grid lo consulta para no
    // moverse mientras tanto: todas ellas son hijas suyas, así que un scroll a mitad de camino
    // les arrastra la animación y se ve cortada.
    public static int AnimatingCount { get; private set; }

    bool _animating;

    void BeginAnimation()
    {
        if (_animating) return;
        _animating = true;
        AnimatingCount++;
    }

    void EndAnimation()
    {
        if (!_animating) return;
        _animating = false;
        AnimatingCount--;
    }

    // Por si la burbuja se destruye a mitad de la animación (fin de nivel, Clear del grid): sin
    // esto el contador quedaría colgado en un número que nunca vuelve a cero y el grid no
    // volvería a moverse nunca.
    void OnDestroy() => EndAnimation();



    // El comodín reclama esta burbuja: se tiñe de arcoíris y un instante después estalla.
    //
    // Cuenta lo que hizo el booster. Sin esto, un match de arcoíris se ve idéntico a uno normal y
    // el jugador no tiene forma de saber que lo que pagó es lo que acaba de pasar.
    //
    // Pasa por SetSpecial y no por un tinte propio porque así el pop lo desvanece solo: ya sabe
    // bajarle el alfa a las capas de una burbuja con poder.
    public void ClaimAsRainbow(float delay, float fade) => StartCoroutine(RecolorRoutine(delay, fade, ColorType, null));

    // La Perla convierte burbujas YA PUESTAS en comodines al abrir el nivel, y esas se quedan.
    //
    // Por eso cambia el COLOR y no el 'Special' como el contagio: lo que hace comodín a una
    // burbuja es su ColorType (BubbleColorExtensions.LinksWith), así que teñirla sin tocarlo
    // daría una que se ve arcoíris y matchea como lo que era — exactamente el bug que el jugador
    // leería como que el booster no funciona.
    //
    // El gesto es el mismo a propósito: el jugador ya aprendió qué significa esa animación.
    public void TurnIntoRainbow(Sprite sprite, float delay, float fade) =>
        StartCoroutine(RecolorRoutine(delay, fade, BubbleColor.Rainbow, sprite));

    // La misma conversión para cualquier color. La usa el Agua Clara, que pasa las burbujas del
    // color más escaso al más abundante: no revienta nada, así que el cambio tiene que verse o el
    // jugador no se entera de qué pagó.
    public void TurnInto(BubbleColor color, Sprite sprite, float delay, float fade) =>
        StartCoroutine(RecolorRoutine(delay, fade, color, sprite));

    // Plantar Bombas convierte burbujas del tablero en minas, y con el MISMO gesto que la Perla:
    // el jugador ya aprendió que esa animación significa "este poder tocó esta burbuja", y dos
    // gestos distintos para la misma idea serían dos cosas que aprender en vez de una.
    //
    // Conserva su ColorType por dentro y nadie lo mira: lo que la saca de los matches y de los
    // recuentos es IsMine, no el color. Quitárselo obligaría a inventar un color "ninguno" y a
    // que todo lo que recorre la paleta se acordara de saltarlo.
    public void TurnIntoMine(float delay, float fade) =>
        StartCoroutine(RecolorRoutine(delay, fade, ColorType, null, Booster.Mine));

    IEnumerator RecolorRoutine(float delay, float fade, BubbleColor color, Sprite permanentSprite,
                               Booster special = Booster.Rainbow)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (permanentSprite != null) SetColor(color, permanentSprite);
        else                         SetSpecial(special);

        // El alfa solo vale para una burbuja con capas encima (arcoíris, poderes). Una de color
        // corriente no tiene ninguna, y bajárselo la dejaría invisible hasta el final del gesto.
        bool layered = ColorType == BubbleColor.Rainbow || Special != Booster.None;

        if (layered) SpecialBubbleSkin.SetAlpha(this, 0f);

        // Un pelín más grande mientras se tiñe, y vuelve. Sin el gesto, el cambio de color sobre
        // una burbuja quieta se pierde entre las demás que ya están cambiando.
        Vector3 baseScale = REST;

        for (float t = 0f; t < fade; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / fade);

            if (layered) SpecialBubbleSkin.SetAlpha(this, p);
            transform.localScale = baseScale * (1f + 0.12f * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }

        if (layered) SpecialBubbleSkin.SetAlpha(this, 1f);
        transform.localScale = baseScale;
    }

    public void PlayPopAnimation(float delay = 0f, AudioClip popClip = null) => StartCoroutine(PopAndDestroy(delay, popClip));

    // dropClip: mismo criterio que popClip — suena una vez POR burbuja que cae, no una sola
    // vez para todo el lote. Todas arrancan a la vez (sin delay escalonado, la caída en sí
    // ya no es instantánea), así que varias sonando juntas es el efecto esperado de "se
    // desprendió una cadena grande", no un bug.
    public void PlayDropAnimation(AudioClip dropClip = null, float delay = 0f) =>
        StartCoroutine(DropAndDestroy(dropClip, delay));

    // Caer hasta una celda y QUEDARSE ahí, que es lo contrario de PlayDropAnimation: esa se va
    // del tablero y se destruye. Lo necesita lo que se desprende y tiene caparazón, que no se
    // libera por perder el techo — baja hasta donde pueda agarrarse.
    //
    // Misma gravedad que la caída de verdad: son el mismo gesto visto dos veces, y con tiempos
    // distintos una de las dos se sentiría falsa.
    //
    // Devuelve lo que dura el RECORRIDO, sin contar lo que se encoge antes de soltarse ni el
    // rebote de aterrizar. Hoy no lo usa nadie: la capa que salta ya no espera a que aterrice,
    // salta antes de moverse (ver GameplayController.ClingOn).
    public float PlaySlideTo(Vector2 localPos, float delay = 0f, AudioClip landClip = null)
    {
        float time = SlideTime(((RectTransform)transform).anchoredPosition, localPos);

        StartCoroutine(SlideTo(localPos, delay, time, landClip));
        return time;
    }

    // Con un piso de tiempo: el salto puede ser de media celda, y a gravedad pura eso se
    // resuelve en dos frames y no se ve que haya caído.
    public static float SlideTime(Vector2 from, Vector2 to) =>
        Mathf.Max(SLIDE_MIN_TIME, DropDuration(Vector2.Distance(from, to)));

    // Lo que dura el aterrizaje y cuánto se achata al tocar.
    const float LAND_TIME   = 0.2f;
    const float LAND_SQUASH = 0.18f;

    // Cuánto se aparta el recorrido de la línea recta, en fracción de lo que mide el salto.
    const float SLIDE_BEND = 0.22f;

    // La estela: cada cuánto suelta, cuántas por vez, de qué tamaño, lo que suben y lo que duran.
    //
    // Van varias por paso y no una: con una por tanda sale una hilera de cuentas, y lo que tiene
    // que leerse es un borboteo (pedido de Diego).
    const float TRAIL_EVERY = 0.02f;
    const int   TRAIL_PER   = 3;
    const float TRAIL_SIZE  = 26f;  // px
    const float TRAIL_RISE  = 70f;  // px/s
    const float TRAIL_LIFE  = 0.8f;
    const float TRAIL_ALPHA = 1f;

    // Una burbujita del rastro, en el sitio por el que acaba de pasar.
    //
    // Cuelga del padre del grid y no de la burbuja, igual que los pedazos de una capa: dentro de
    // ella, moverse se las llevaría consigo y dejarían de ser un rastro para ser un adorno que
    // viaja pegado.
    void TrailBubble(Vector2 at)
    {
        var sprite = BubbleCatalog.TrailBubble;

        if (sprite == null) return;

        var go = new GameObject("Trail", typeof(RectTransform), typeof(Image)) { hideFlags = HideFlags.DontSave };
        var rt = (RectTransform)go.transform;

        rt.SetParent(transform.parent, false);
        // Repartidas a lo ANCHO de la burbuja que pasa: amontonadas en su centro se leen como un
        // punto que parpadea, y lo que tiene que verse es un reguero del grosor de lo que baja.
        rt.anchoredPosition = at + new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f))
                                 * (HexGridMath.BubbleDiameter * 0.4f);
        rt.sizeDelta        = Vector2.one * TRAIL_SIZE * Random.Range(0.55f, 1.35f);

        var img = go.GetComponent<Image>();

        img.sprite         = sprite;
        img.raycastTarget  = false;
        img.preserveAspect = true;
        img.color          = new Color(1f, 1f, 1f, TRAIL_ALPHA);

        // Suben, que es lo que hace el aire en el agua, con algo de deriva para que no salgan
        // todas en columna.
        var drift = new Vector2(Random.Range(-28f, 28f), TRAIL_RISE * Random.Range(0.6f, 1.4f));

        go.AddComponent<PopParticle>().Init(rt.anchoredPosition, drift, TRAIL_LIFE * Random.Range(0.7f, 1.2f));
    }

    IEnumerator SlideTo(Vector2 target, float delay, float time, AudioClip landClip)
    {
        BeginAnimation();   // cuenta desde ya: el grid no debe moverse mientras esta espera su turno

        if (delay > 0f) yield return new WaitForSeconds(delay);

        var     rt    = (RectTransform)transform;
        Vector2 start = rt.anchoredPosition;
        Vector3 scale = REST;

        // Se encoge un poco ANTES de soltarse. Es media décima de nada, pero es lo que hace que
        // se lea como que se deja caer en vez de como que la movieron: toda caída empieza con
        // algo juntando fuerzas.
        const float COIL_TIME = 0.08f;

        for (float t = 0f; t < COIL_TIME; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / COIL_TIME);

            rt.localScale = new Vector3(scale.x * (1f + 0.07f * p), scale.y * (1f - 0.07f * p), scale.z);
            yield return null;
        }

        // La curva del recorrido: el punto de control del arco, a un lado de la recta.
        //
        // Se desvía hacia la izquierda o hacia la derecha al azar, para que dos burbujas que
        // bajan a la vez no dibujen la misma curva — en espejo se leen como una animación y no
        // como dos cosas escurriéndose.
        Vector2 straight = target - start;
        Vector2 side     = new Vector2(-straight.y, straight.x).normalized * (Random.value < 0.5f ? -1f : 1f);
        Vector2 mid      = (start + target) * 0.5f + side * (straight.magnitude * SLIDE_BEND);

        float dropped = 0f;

        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            // Cuadrática y no lineal, para que acelere como cualquier cosa que se suelta: a
            // velocidad constante parece que la arrastraron.
            float p = Mathf.Clamp01(t / time);
            float e = p * p;

            // Por un arco y no en línea recta: lo que baja por el agua se escurre entre lo que
            // tiene al lado, y una recta de celda a celda se lee como un salto de cuadrícula.
            // Es la misma curva que hace lo rescatado camino del hub (pedido de Diego).
            float u = 1f - e;

            rt.anchoredPosition = u * u * start + 2f * u * e * mid + e * e * target;

            // Y se estira en el sentido de la caída mientras va cogiendo velocidad.
            rt.localScale = new Vector3(scale.x * (1f - 0.09f * p), scale.y * (1f + 0.09f * p), scale.z);

            // La estela: va soltando burbujitas por donde pasa, que suben y se deshacen.
            if ((dropped += Time.deltaTime) >= TRAIL_EVERY)
            {
                dropped = 0f;

                int many = Random.Range(TRAIL_PER, TRAIL_PER * 2);

                for (int i = 0; i < many; i++) TrailBubble(rt.anchoredPosition);
            }

            yield return null;
        }

        rt.anchoredPosition = target;

        // El golpe suena CUANDO toca, no al soltarse: es el impacto lo que hace ruido.
        AudioManager.Instance?.PlaySfx(landClip);

        // El aterrizaje: se achata contra el suelo y vuelve rebotando. Sin esto la burbuja
        // llegaba y se quedaba clavada, que es lo que hace que una caída se sienta sin peso.
        for (float t = 0f; t < LAND_TIME; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / LAND_TIME);

            // Un rebote que se apaga: arranca achatada del todo y oscila cada vez menos hasta
            // quedarse quieta.
            float squash = LAND_SQUASH * Mathf.Cos(p * Mathf.PI * 2.2f) * (1f - p);

            rt.localScale = new Vector3(scale.x * (1f + squash), scale.y * (1f - squash), scale.z);
            yield return null;
        }

        rt.localScale = scale;
        EndAnimation();
    }

    // El empujón de cuando algo se pega al lado: la burbuja cede en esa dirección y vuelve.
    //
    // NO entra en AnimatingCount, a diferencia del resto: el desplazamiento es relativo a su
    // celda, así que el tablero puede desplazarse debajo sin que se vea cortado, y contarlo
    // congelaría el scroll en cada disparo que no hace match.
    public void PlayNudge(Vector2 offset)
    {
        // Una animación de verdad manda sobre esto: ella escribe la posición y la deja donde
        // toque, y las dos a la vez se pelean por el mismo campo.
        if (_animating) return;

        // El reposo se guarda UNA sola vez. Guardándolo en cada empujón, la burbuja que recibe
        // dos seguidos tomaría por sitio suyo una posición ya desplazada, y el tablero se iría
        // descuadrando disparo a disparo sin que se note hasta mucho después.
        if (_nudge != null) StopCoroutine(_nudge);
        else                _nudgeHome = ((RectTransform)transform).anchoredPosition;

        _nudge = StartCoroutine(Nudge(offset));
    }

    void StopNudge()
    {
        if (_nudge == null) return;

        StopCoroutine(_nudge);
        _nudge = null;
    }

    IEnumerator Nudge(Vector2 offset)
    {
        var rt = (RectTransform)transform;

        for (float t = 0f; t < NUDGE_TIME; t += Time.deltaTime)
        {
            // Si a mitad le toca caer, reventar o deslizarse, se suelta sin restaurar: esa
            // animación ya se encarga de dejar la burbuja donde corresponde.
            if (_animating)
            {
                _nudge = null;
                yield break;
            }

            float p = Mathf.Clamp01(t / NUDGE_TIME);

            // Sale de golpe y vuelve despacio. Al revés se leería como que la burbuja se
            // estira sola, no como que algo la empujó.
            rt.anchoredPosition = _nudgeHome + offset * ((1f - p) * (1f - p));
            yield return null;
        }

        rt.anchoredPosition = _nudgeHome;
        _nudge = null;
    }

    const float SLIDE_MIN_TIME = 0.22f;

    IEnumerator PopAndDestroy(float delay, AudioClip popClip)
    {
        BeginAnimation();   // ya cuenta durante el delay: el pop en cadena es parte de la misma

        if (delay > 0f) yield return new WaitForSeconds(delay);

        SpawnPopParticles();
        AudioManager.Instance?.PlayPop(popClip);
        if (SaveManager.Vibration) MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.LightImpact);

        Vector3 baseScale = REST;
        float   t = 0f;
        while (t < POP_DURATION)
        {
            t += Time.deltaTime;
            float p = t / POP_DURATION;
            transform.localScale = baseScale * (1f + 0.3f * p);
            if (bubbleImage) bubbleImage.color = new Color(1f, 1f, 1f, 1f - p);
            if (Special != Booster.None) SpecialBubbleSkin.SetAlpha(this, 1f - p);

            // Y lo que la burbuja de rescate lleve encima. Sin esto, la que entra en un match
            // reventaba el cuerpo y dejaba al bicho y su cristal plantados hasta que el objeto se
            // destruía, en vez de estallar entera (reportado por Diego).
            FadeRescueLayers(1f - p);
            yield return null;
        }
        Destroy(gameObject);
    }

    // "Explosión" gratis, sin arte nuevo: reusa el mismo sprite de la burbuja, achicado,
    // como un puñado de partículas que salen disparadas en círculo y se desvanecen. No usa
    // ParticleSystem de Unity porque acá todo es uGUI (Canvas), no mundo 2D/3D — ParticleSystem
    // no rinde bien dentro de un Canvas Screen Space Overlay sin configuración extra.
    void SpawnPopParticles()
    {
        if (!bubbleImage || !bubbleImage.sprite) return;

        var parent = transform.parent;
        var origin = ((RectTransform)transform).anchoredPosition;
        var color  = bubbleImage.color;

        for (int i = 0; i < POP_PARTICLE_COUNT; i++)
        {
            var go = new GameObject("PopParticle", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = origin;
            rt.sizeDelta        = Vector2.one * POP_PARTICLE_SIZE;

            var img = go.GetComponent<Image>();
            img.sprite        = bubbleImage.sprite;
            img.color         = color;
            img.raycastTarget = false;

            float angle = (360f / POP_PARTICLE_COUNT) * i + Random.Range(-15f, 15f); // jitter, no todas parejitas
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            float   speed = POP_PARTICLE_SPEED * Random.Range(0.75f, 1.25f);

            go.AddComponent<PopParticle>().Init(origin, dir * speed, POP_PARTICLE_LIFETIME);
        }
    }

    // Gravedad simulada (acelera en vez de moverse a velocidad constante) + un giro
    // aleatorio por burbuja, para que la caída en cadena no se vea toda igual/rígida.
    IEnumerator DropAndDestroy(AudioClip dropClip, float delay)
    {
        BeginAnimation();   // cuenta desde ya: el grid no debe moverse mientras esta espera su turno

        if (delay > 0f) yield return new WaitForSeconds(delay);

        AudioManager.Instance?.PlayPop(dropClip);

        // Un toque al desprenderse, igual que el pop. Sin esto un derrumbe de quince burbujas se
        // ve pero no se siente.
        if (SaveManager.Vibration) MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.LightImpact);

        var     rt       = (RectTransform)transform;
        Vector2 pos      = rt.anchoredPosition;
        float   velocity = 0f;
        float   spin     = Random.Range(-180f, 180f); // grados/seg
        float   t        = 0f;

        while (t < DROP_MAX_DURATION)
        {
            t += Time.deltaTime;
            velocity -= DROP_GRAVITY * Time.deltaTime;
            pos.y    += velocity * Time.deltaTime;
            rt.anchoredPosition = pos;
            rt.Rotate(0f, 0f, spin * Time.deltaTime);

            if (t > DROP_FADE_START && bubbleImage)
            {
                float fadeP = (t - DROP_FADE_START) / (DROP_MAX_DURATION - DROP_FADE_START);
                bubbleImage.color = new Color(1f, 1f, 1f, 1f - fadeP);
                if (Special != Booster.None) SpecialBubbleSkin.SetAlpha(this, 1f - fadeP);
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}
