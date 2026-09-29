using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Coloca los nodos de nivel como hijos FIJOS de la esfera del mundo (World3D/Sphere) — al girar
// la esfera (WorldSphereDragRotate), los nodos giran con ella, y el que quede mirando hacia la
// cámara es "el nodo actual". Lee los niveles reales de Resources/Levels; si no encuentra
// ninguno, genera esferas placeholder numeradas para poder seguir probando la geometría.
//
// Reglas (pedidas explícitamente):
// 1. Nodo 1 abajo, ascendiendo hacia el nodo N — se van "imprimiendo" de menor a mayor.
// 2. El ángulo de la esfera arranca en 0, que es exactamente el nodo 1 (con su padding).
// 3. No hay giro por debajo del nodo 1 (ángulo nunca negativo).
// 4 y 5. El límite superior de giro es el último nodo — no se puede girar más allá.
// 6. Separación angular pareja entre nodos consecutivos.
// 7. Padding angular tanto abajo del nodo 1 como arriba del último nodo.
public class WorldSphereNodePositioner : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("La esfera del mundo (World3D/Sphere) — los nodos se instancian como hijos de esto.")]
    [SerializeField] Transform sphere;
    [Tooltip("Cámara del mundo — si se deja vacío usa Camera.main. De acá sale dónde queda el 'frente' real.")]
    [SerializeField] Camera worldCamera;

    [Tooltip("El componente del DragArea que gira la esfera — para poder posicionar el mapa en el nodo actual al abrirlo.")]
    [SerializeField] WorldSphereDragRotate dragRotate;
    [Tooltip("Opcional — el camino que une los nodos. Se dibuja solo cuando el recorrido ya está armado.")]
    [SerializeField] WorldSpherePathRibbon pathRibbon;
    [Tooltip("Opcional — las decoraciones a los costados del camino, leídas del JSON de cada capítulo.")]
    [SerializeField] WorldSphereDecorationPositioner decorations;

    [Header("Entrar al nivel (mismo flujo que LevelMapController)")]
    [Tooltip("Objetivo + boosters antes de entrar a Gameplay. Si se deja vacío, se navega directo.")]
    [SerializeField] StartGamePanel  startGamePanel;
    [Tooltip("Se muestra al tocar un nivel sin vidas disponibles.")]
    [SerializeField] OutOfLivesPanel outOfLivesPanel;

    [Header("Nodos")]
    [SerializeField] Transform nodePrefab; // opcional — si está vacío, genera una esfera simple
    [Tooltip("Se usa solo si no se pudo cargar ningún nivel real de Resources/Levels (fallback).")]
    [SerializeField] int   nodeCount    = 15;
    [Tooltip("Separación angular entre nodos consecutivos, en grados.")]
    [SerializeField] float angleSpacing = 8f;
    [Tooltip("Separación EXTRA al pasar de un capítulo al siguiente, en grados — el hueco que separa un capítulo del otro en el mapa.")]
    [SerializeField] float chapterGap   = 10f;
    [Tooltip("Diámetro del nodo en unidades de MUNDO (no de la esfera).")]
    [SerializeField] float nodeSize     = 1f;
    [Tooltip("Espesor del nodo (el canto de la moneda) en unidades de MUNDO — sobre qué apoya cuando está acostado.")]
    [SerializeField] float nodeThickness = 0.15f;

    [Header("Desarrollo")]
    [Tooltip("Solo para probar: corta la cantidad de nodos a los primeros N. En 0 se muestran todos los niveles que haya.")]
    [SerializeField] int debugMaxNodes = 0;

    [Header("Card del jugador")]
    [Tooltip("Tarjeta con el avatar que acompaña al nivel actual. Vacío = no se muestra.")]
    [SerializeField] RectTransform playerCardPrefab;
    [Tooltip("Ancho de la tarjeta en unidades de MUNDO, igual criterio que 'Node Size'.")]
    [SerializeField] float playerCardSize = 0.7f;
    [Tooltip("Aire entre el borde del nodo y el borde de la tarjeta, en unidades de MUNDO. La distancia se calcula sola a partir de los dos tamaños; esto es solo el respiro entre ambos.")]
    [SerializeField] float playerCardGap  = 0.15f;
    [Tooltip("El lado se elige solo: la tarjeta cae al contrario de hacia donde se desvió el nodo por el zigzag. Marca esto si prefieres el lado opuesto al que salga.")]
    [SerializeField] bool  playerCardFlip = false;
    [Tooltip("Cuánto tarda en deslizarse hasta el nivel siguiente después de ganar, en segundos.")]
    [SerializeField] float playerCardMoveDuration = 0.7f;

    [Header("Zigzag")]
    [Tooltip("Cuánto se desvía el camino hacia los costados, en grados sobre la esfera. 0 = camino recto.")]
    [SerializeField] float zigzagAmount = 4f;
    [Tooltip("Cada cuántos nodos completa un vaivén entero (izquierda y derecha).")]
    [SerializeField] float zigzagPeriod = 4f;

    [Header("Encarado a cámara")]
    [Tooltip("A cuántos grados del frente el nodo deja de encararse y queda apoyado plano sobre la superficie (efecto de 'ya se está yendo'). 0 = nunca se encara.")]
    [SerializeField] float faceCameraRange = 30f;
    [Tooltip("Cuánto llega a encararse el nodo que está justo al frente (1 = mira de lleno a la cámara).")]
    [Range(0f, 1f)]
    [SerializeField] float faceCameraMax   = 1f;

    [Header("Rendimiento")]
    [Tooltip("Cuántos nodos se instancian por frame. Armar el mapa entero en un solo frame traba la animación de transición justo al final. Como en ese momento la pantalla está tapada por el fundido, repartirlo no se ve.")]
    [SerializeField] int nodesPerFrame = 6;
    [Tooltip("A cuántos grados del frente se apaga un nodo. Los que quedan detrás del horizonte no se ven pero igual se dibujan y se procesan — apagarlos es el mayor ahorro del mapa. 0 = no apagar ninguno.")]
    [SerializeField] float cullRange = 55f;

    [Header("Padding (en grados)")]
    [Tooltip("En reposo, cuánto se levanta el nodo 1 del punto justo debajo de la cámara — para que no quede pegado al borde de abajo.")]
    [SerializeField] float bottomPadding = 6f;
    [Tooltip("Cuántos grados ANTES se frena el scroll, para que el último nodo no baje del todo y no quede tanta agua vacía encima. 0 = baja hasta donde arranca el nodo 1.")]
    [SerializeField] float topPadding    = 12f;

    // Cuánto puede girar la esfera como máximo (siempre ≥ 0) — lo lee WorldSphereDragRotate
    // para no dejar pasar el giro más allá del último nodo. Se calcula solo, sin ningún valor
    // manual de "límite" en ningún lado.
    public float AngleMax { get; private set; }

    // A qué ángulo de giro queda centrado el nivel actual, y cuánto mide un nodo en ángulo — lo
    // usan los pines para saber si el jugador se alejó del nivel disponible y para volver a él.
    public float CurrentNodeAngle { get; private set; }
    public float AngleSpacing => angleSpacing;

    // Los tamaños en unidades de mundo de este componente (y los del camino y las decoraciones)
    // están ajustados a ojo para un planeta de este radio. No es un ajuste: es el punto de
    // partida de toda la calibración, y solo cambiaría si algún día se rehiciera entera.
    const float SIZES_CALIBRATED_FOR_RADIUS = 20f;

    // Cuánto hay que multiplicar cualquier medida en unidades de MUNDO para que conserve su
    // proporción con el planeta. Con el Scale del Sphere en 40 (radio 20) da 1 y todo queda como
    // se calibró; en 20 (radio 10) da 0.5 y todo se achica a la mitad solo.
    //
    // Los ángulos no lo necesitan: ya son proporcionales por naturaleza.
    public float WorldScale => (_sphereScale * 0.5f) / SIZES_CALIBRATED_FOR_RADIUS;

    // El mapa se arma a lo largo de varios frames, así que hasta que esto no sea true los valores
    // de arriba todavía no son definitivos.
    public bool Ready { get; private set; }

    // Geometría del recorrido, para que otros puedan dibujar sobre él (ej. el camino).
    // Las rotaciones son las de REPOSO de cada nodo, en el espacio local de la esfera.
    public IReadOnlyList<int>        NodeChapters      => _chapters;
    public int                       LevelCount        => _restAngle?.Length ?? 0;

    // Qué tramo de niveles está en pantalla ahora mismo. El camino y las decoraciones se
    // reconstruyen sobre esta ventana en vez de sobre el capítulo entero.
    public int WindowFirst { get; private set; }
    public int WindowLast  { get; private set; }
    public event System.Action OnWindowChanged;
    public float                     SurfaceRadiusLocal => _baseRadius;
    public float                     SphereScale        => _sphereScale;

    // Geometría de TODOS los niveles: son solo floats, así que tener miles no cuesta nada.
    // Lo que se recicla son los objetos, no estos datos.
    float[] _restAngle;     // ángulo de reposo de cada nivel
    float[] _scrollAngle;   // cuánto hay que girar para centrarlo
    IReadOnlyList<LevelIndexEntry> _levels;   // el índice, no los niveles completos

    // El grupo de nodos que se recicla. El slot de un nivel es su índice módulo el tamaño del
    // grupo: como la ventana visible siempre es un tramo contiguo más corto que el grupo, dos
    // niveles visibles nunca pueden caer en el mismo slot.
    Transform[]  _pool;
    int[]        _poolLevel;   // qué nivel tiene cada slot, -1 si está libre
    Quaternion[] _poolRot;

    float _frontAngle;        // punto de la esfera más cercano a la cámara
    float _nodeRadius;        // radio de apoyo de los nodos, en unidades locales
    int   _currentIndex = -1; // el nivel disponible
    int   _justAdvancedFrom;

    // Cuánto se deja ver por detrás del frente antes de soltar un nodo.
    const float BACK_MARGIN = 12f;

    Camera      _cam;
    float       _baseRadius;   // radio de apoyo, en unidades locales de la esfera
    float       _sphereScale;
    bool        _isFlat;       // el prefab es de UI (plano) y no una primitiva con volumen
    float[]       _lateralDeg;  // desvío del zigzag de cada nodo, para saber de qué lado va la card
    int[]         _chapters;    // capítulo de cada nodo, para cortar el camino entre capítulos
    RectTransform _playerCard;
    Quaternion    _playerCardRot;
    float         _cardRadius;
    float         _cardScrollAngle;  // a qué altura del recorrido está la card, sin dar la vuelta
    Coroutine     _cardMove;

    bool _held;

    void Awake()
    {
        // El mapa se arma a lo largo de varios frames, así que le pide a la transición que no
        // descubra la pantalla hasta que termine.
        SceneReady.Hold();
        _held = true;
    }

    void ReleaseHold()
    {
        if (!_held) return;
        _held = false;
        SceneReady.Release();
    }

    void OnDestroy() => ReleaseHold();

    // Corrutina y no Start() normal: ver 'Nodes Per Frame'.
    IEnumerator Start()
    {
        if (!sphere) { Debug.LogWarning("[WorldSphereNodePositioner] Falta asignar 'Sphere' en el Inspector."); ReleaseHold(); yield break; }

        var cam = worldCamera ? worldCamera : Camera.main;
        if (!cam) { Debug.LogWarning("[WorldSphereNodePositioner] No hay cámara (ni 'World Camera' asignada ni Camera.main)."); ReleaseHold(); yield break; }
        _cam = cam;

        var levels = LevelLoader.Entries;
        int count  = levels.Count > 0 ? levels.Count : nodeCount;
        if (levels.Count == 0)
            Debug.LogWarning($"[WorldSphereNodePositioner] No se encontró ningún nivel en Resources/Levels — usando {nodeCount} nodos placeholder.");

        // Recorte de desarrollo. Solo acorta la lista: todo lo demás (límite del scroll, card,
        // huecos entre capítulos) se calcula sobre los nodos que quedaron, así que el mapa sigue
        // siendo coherente con 3 nodos igual que con 30.
        if (debugMaxNodes > 0 && debugMaxNodes < count)
        {
            count = debugMaxNodes;
            Debug.LogWarning($"[WorldSphereNodePositioner] 'Debug Max Nodes' está en {debugMaxNodes} — se están mostrando solo los primeros {count} niveles. Ponlo en 0 para ver todos.");
        }

        _levels      = levels;
        _restAngle   = new float[count];
        _scrollAngle = new float[count];
        _lateralDeg  = new float[count];
        _chapters    = new int[count];

        // El "frente" NO es el -Z local de la esfera: es el punto de la superficie más cercano a
        // la cámara, que depende de dónde esté puesta la esfera. Con la esfera abajo y adelante,
        // ese punto queda decenas de grados por encima del -Z — si no se tiene en cuenta, el
        // nodo 1 termina detrás de la cámara y el orden se ve al revés.
        Vector3 toCamera = cam.transform.position - sphere.position;
        _frontAngle      = Mathf.Atan2(toCamera.y, -toCamera.z) * Mathf.Rad2Deg;

        // Todo sale del Scale real de la esfera, no de un campo a mano — así no puede quedar
        // desincronizado si se cambia la escala.
        float sphereScale = Mathf.Max(sphere.localScale.x, 0.0001f);

        _baseRadius  = SurfaceRadius(sphere);
        _sphereScale = sphereScale;
        _isFlat      = nodePrefab is RectTransform;
        _nodeRadius  = _baseRadius + NodeLift(1f) / sphereScale;

        _justAdvancedFrom = SaveManager.ConsumeJustAdvancedFromLevel();
        int maxUnlocked   = SaveManager.MaxUnlockedLevel;
        int currentIndex  = -1;

        // Precálculo de la geometría de TODOS los niveles. Son cuatro arrays de números: aunque
        // hubiera miles, el costo es despreciable. Lo caro son los GameObjects, y de esos solo se
        // crean los del grupo reciclable.
        float offset = bottomPadding;
        for (int i = 0; i < count; i++)
        {
            // Separación pareja entre niveles, más un hueco extra al cambiar de capítulo — así
            // los capítulos se leen como tramos distintos del camino sin necesitar ningún dato
            // nuevo en el JSON: alcanza con el campo 'chapter' que los niveles ya traen.
            if (i > 0)
            {
                offset += angleSpacing;
                if (levels.Count > 0 && levels[i].chapter != levels[i - 1].chapter) offset += chapterGap;
            }

            _restAngle[i]   = _frontAngle + offset;
            _scrollAngle[i] = offset - bottomPadding;
            _lateralDeg[i]  = zigzagPeriod > 0.01f
                ? Mathf.Sin(i / zigzagPeriod * Mathf.PI * 2f) * zigzagAmount
                : 0f;
            _chapters[i]    = levels.Count > 0 ? levels[i].chapter : 1;

            if (levels.Count > 0 && GetState(levels[i].id, maxUnlocked) == NodeState.Available)
                currentIndex = i;
        }

        // Tamaño del grupo: lo que entra en la ventana visible más margen. Se calcula solo a
        // partir del rango de descarte y la separación, así que si se cambia cualquiera de los
        // dos sigue alcanzando sin tocar nada más.
        float visibleSpan = (cullRange > 0.01f ? cullRange : 60f) + BACK_MARGIN;
        int   poolSize    = Mathf.Min(count, Mathf.CeilToInt(visibleSpan / Mathf.Max(angleSpacing, 0.01f)) + 4);

        _pool      = new Transform[poolSize];
        _poolLevel = new int[poolSize];
        _poolRot   = new Quaternion[poolSize];

        for (int slot = 0; slot < poolSize; slot++)
        {
            // Se cede el frame cada tantos nodos para no trabar la animación de transición.
            if (nodesPerFrame > 0 && slot > 0 && slot % nodesPerFrame == 0) yield return null;

            Transform node = nodePrefab ? Instantiate(nodePrefab, sphere) : CreatePlaceholder();
            node.SetParent(sphere, false);

            // 'Node Size' es siempre el diámetro en unidades de MUNDO. Un prefab de UI mide
            // cientos (píxeles) en su propio espacio y una primitiva mide 1, así que hay que
            // normalizar por su tamaño propio antes de dividir por la escala de la esfera.
            float sourceSize = node is RectTransform rt && rt.rect.width > 0.001f ? rt.rect.width : 1f;
            node.localScale  = Vector3.one * (nodeSize * WorldScale / sourceSize / sphereScale);

            var view = node.GetComponentInChildren<LevelNodeView>(true);
            if (view) view.OnClicked += OnLevelSelected;

            node.gameObject.SetActive(false);
            _pool[slot]      = node;
            _poolLevel[slot] = -1;
        }

        // El scroll (giro de la esfera) RESTA ángulo: trae el mundo hacia la cámara y va bajando
        // los nodos siguientes desde el horizonte. El recorrido completo es exactamente la
        // distancia del nodo 1 al último — así el último baja hasta el mismo lugar donde arranca
        // el nodo 1. Se mide sobre el recorrido REAL (que ya incluye los huecos entre capítulos,
        // así que no se puede calcular multiplicando) y se le descuenta un nodo entero: si el
        // último baja hasta el fondo, arriba queda la pantalla entera de agua vacía. Termina
        // entonces en el lugar del nodo 2. 'Top Padding' recorta todavía un poco más.
        float travel = offset - bottomPadding;
        AngleMax = Mathf.Max(0f, travel - angleSpacing - topPadding);

        // Si no hay ninguno disponible (capítulos terminados), se queda en el último.
        if (currentIndex < 0) currentIndex = count - 1;
        _currentIndex = currentIndex;

        int justCompletedIndex = _justAdvancedFrom > 0 ? IndexOfLevel(levels, _justAdvancedFrom) : -1;

        float currentAngle = Mathf.Clamp(_scrollAngle[currentIndex], 0f, AngleMax);
        CurrentNodeAngle   = currentAngle;

        // El scroll se posiciona ANTES de poblar la ventana: qué niveles hay que mostrar depende
        // de dónde esté el scroll, así que al revés se poblaría la ventana equivocada y habría un
        // frame con los nodos del principio del capítulo.
        float startAngle = justCompletedIndex >= 0
            ? Mathf.Clamp(_scrollAngle[justCompletedIndex], 0f, AngleMax)
            : currentAngle;

        if (dragRotate) dragRotate.JumpTo(startAngle);
        else Debug.LogWarning("[WorldSphereNodePositioner] Falta asignar 'Drag Rotate' en el Inspector — el mapa va a abrir en el nodo 1 en vez del actual.");

        UpdateWindow(startAngle);

        // El camino y las decoraciones se apoyan sobre el recorrido, así que se construyen recién
        // cuando la ventana ya está poblada.
        if (pathRibbon)  pathRibbon.Build();
        if (decorations) decorations.Build();

        // Si se vuelve de ganar, la tarjeta aparece todavía al lado del nivel que se acaba de
        // pasar, y se desliza al siguiente una vez terminada la revelación del nodo.
        if (playerCardPrefab)
            CreatePlayerCard(justCompletedIndex >= 0 ? justCompletedIndex : currentIndex, _nodeRadius, sphereScale);

        if (justCompletedIndex >= 0)
        {
            var view = ViewOf(justCompletedIndex);
            if (view)
            {
                int  levelId = levels[justCompletedIndex].id;
                bool gold    = GetState(levelId, maxUnlocked) == NodeState.CompleteFirstTry;
                view.HideForReveal();
                StartCoroutine(PlayCompletion(view, SaveManager.GetLevelStars(levelId), gold, currentAngle, currentIndex));
            }
        }

        Ready = true;
        ReleaseHold();
    }

    // Qué slot le toca a un nivel. Como la ventana visible es siempre un tramo contiguo más corto
    // que el grupo, dos niveles visibles nunca caen en el mismo slot.
    int SlotOf(int levelIndex) => ((levelIndex % _pool.Length) + _pool.Length) % _pool.Length;

    LevelNodeView ViewOf(int levelIndex)
    {
        int slot = SlotOf(levelIndex);
        return _poolLevel[slot] == levelIndex ? _pool[slot].GetComponentInChildren<LevelNodeView>(true) : null;
    }

    // Decide qué niveles entran en pantalla con el scroll actual y le asigna a cada uno su slot.
    // Solo reconfigura los que cambiaron: al scrollear entra y sale un nivel por vez, así que el
    // trabajo por frame es casi nulo por más largo que sea el capítulo.
    void UpdateWindow(float scroll)
    {
        int count = _restAngle.Length;
        float visibleMax = cullRange > 0.01f ? cullRange : 180f;

        // El ángulo de reposo crece con el índice, así que la ventana es un tramo contiguo.
        int first = 0, last = count - 1;
        while (first < count && _restAngle[first] - scroll - _frontAngle < -BACK_MARGIN) first++;
        while (last >= first && _restAngle[last] - scroll - _frontAngle > visibleMax) last--;

        if (first > last) { first = Mathf.Clamp(first, 0, count - 1); last = first; }

        // Soltar los slots que quedaron fuera.
        for (int slot = 0; slot < _pool.Length; slot++)
        {
            int held = _poolLevel[slot];
            if (held < 0) continue;
            if (held >= first && held <= last) continue;

            _poolLevel[slot] = -1;
            _pool[slot].gameObject.SetActive(false);
        }

        // El ancla se mueve con la ventana, y hay que fijarla ANTES de vincular: los nodos
        // calculan su orden contra ella.
        float anchor = _restAngle[first];
        bool  moved  = !Mathf.Approximately(anchor, DrawOrderAnchor);
        DrawOrderAnchor = anchor;

        for (int i = first; i <= last; i++) Bind(i);

        // Bind se saltea los nodos que ya estaban puestos, así que si el ancla se movió esos
        // conservan el orden calculado con la anterior. Son treinta como mucho: se reasignan.
        if (moved) RefreshDrawOrders(first, last);

        WindowFirst = first;
        WindowLast  = last;
    }

    void RefreshDrawOrders(int first, int last)
    {
        for (int i = first; i <= last; i++)
        {
            var node = _pool[SlotOf(i)];
            if (!node) continue;

            var canvas = node.GetComponent<Canvas>();
            if (canvas) canvas.sortingOrder = DrawOrderAt(_restAngle[i]) + NODE_ORDER_BIAS;
        }
    }

    void Bind(int levelIndex)
    {
        int slot = SlotOf(levelIndex);
        if (_poolLevel[slot] == levelIndex) return; // ya está puesto, no hay nada que rehacer

        var node = _pool[slot];
        if (!node) return; // slot todavía sin crear

        _poolLevel[slot] = levelIndex;

        Quaternion rot = RestRotation(levelIndex);
        _poolRot[slot]     = rot;
        node.localPosition = rot * (Vector3.back * _nodeRadius);
        node.localRotation = rot;
        node.gameObject.SetActive(true);

        // Mismo criterio de orden que las decoraciones, para que puedan taparse entre sí según
        // quién esté más adelante en el camino. Un nodo es un Canvas en World Space, y por defecto
        // todos quedan en orden 0 — o sea siempre por encima de cualquier decoración, aunque esta
        // esté claramente más cerca de la cámara.
        //
        // Se ordena por el ÁNGULO real sobre la esfera, no por el índice de nivel: entre capítulos
        // hay un hueco ('Chapter Gap') que el índice no refleja, así que el primer nodo de un
        // capítulo está mucho más lejos de lo que su número sugiere.
        var canvas = node.GetComponent<Canvas>();
        if (canvas) canvas.sortingOrder = DrawOrderAt(_restAngle[levelIndex]) + NODE_ORDER_BIAS;

        var view = node.GetComponentInChildren<LevelNodeView>(true);
        if (view && _levels.Count > 0)
        {
            int levelId = _levels[levelIndex].id;
            var state   = GetState(levelId, SaveManager.MaxUnlockedLevel);

            node.name = $"Node_{levelId:000}";
            view.Setup(levelId, state, SaveManager.GetLevelStars(levelId));

            // Los cilindros del canto no los toca LevelNodeView (es un archivo compartido con el
            // mapa real), así que su material lo cambia este componente aparte.
            var edges = node.GetComponentInChildren<CurvedMapNodeEdgeMaterials>(true);
            if (edges) edges.ApplyState(state);
        }
        else if (!view)
        {
            node.name = $"NodeProto_{levelIndex + 1}";
        }
    }

    // Rotación de reposo de un nivel: el vaivén lateral (eje Y) desvía el camino a los costados y
    // el avance del recorrido (eje X) lo sube hacia el horizonte. Al componerlas sobre un vector
    // unitario, el nodo queda siempre sobre la superficie.
    // Orden de dibujo que le corresponde a un punto del recorrido, a partir de su ángulo sobre la
    // esfera. Lo comparten nodos y decoraciones para que puedan taparse entre sí correctamente:
    // lo que está más adelante en el camino está más lejos, y se dibuja antes.
    //
    // ORDER_LAYERS son las capas que quedan libres dentro de cada paso angular, para el orden
    // interno de los grupos de decoración y para el bias del nodo.
    //
    // La resolución NO es arbitraria: Unity limita sortingOrder a ±32767, y el ángulo del último
    // nivel de tres capítulos de 60 ronda los 1450°. Antes esto era Round(ángulo * 4) * 8, que
    // llegaba a -46400: se saturaba, y a partir de ahí TODOS los nodos y decoraciones compartían
    // orden y se tapaban al azar. Un grado por paso con 16 capas da el mismo detalle y deja el
    // máximo en unos -23000.
    //
    // Si algún día se agregan capítulos o crece angleSpacing hay que bajar ORDER_LAYERS, por eso
    // el tope avisa en vez de saturar en silencio.
    const int   ORDER_RANGE      = 32767;
    const int   ORDER_LAYERS     = 16;
    const float ORDER_RESOLUTION = 1f;

    // Ángulo desde el que se cuenta el orden: el del primer nodo de la ventana visible.
    //
    // Sin esto el orden se medía desde el origen del mundo y crecía con cada nivel, hasta pasarse
    // del rango de sortingOrder (±32767) alrededor del nivel 250 — de ahí en adelante todo
    // compartía capa y se tapaba al azar. Contando desde la ventana, el rango que hace falta es
    // el de los treinta nodos que se ven a la vez, y el mundo puede tener los niveles que sea.
    //
    // Lo que importa es el orden RELATIVO entre lo que está en pantalla al mismo tiempo, y eso no
    // cambia al mover el ancla: se le resta lo mismo a todos.
    public static float DrawOrderAnchor { get; private set; }

    public static int DrawOrderAt(float restAngle) =>
        -Mathf.RoundToInt((restAngle - DrawOrderAnchor) * ORDER_RESOLUTION) * ORDER_LAYERS;

    // El nodo se dibuja en la mitad alta de su paso angular, por delante de las decoraciones que
    // caen en el MISMO paso — nada más. Entre pasos distintos manda la profundidad: una
    // decoración que va antes en el camino tapa al nodo que hay detrás, y así tiene que ser.
    //
    // Sin este bias empatan, porque el redondeo manda a la misma capa a un nodo y a una
    // decoración con 'at' casi entero, y el desempate lo termina decidiendo el orden de creación
    // — que con el pooling cambia en cada pasada.
    const int NODE_ORDER_BIAS = ORDER_LAYERS / 2;

    // Ángulo de reposo de un nivel. Lo usa el camino para anclar su textura al capítulo.
    static int IndexOfLevel(IReadOnlyList<LevelIndexEntry> levels, int id)
    {
        for (int i = 0; i < levels.Count; i++)
            if (levels[i].id == id) return i;
        return -1;
    }

    public float RestAngleAt(int levelIndex)
    {
        if (_restAngle == null || _restAngle.Length == 0) return 0f;
        return _restAngle[Mathf.Clamp(levelIndex, 0, _restAngle.Length - 1)];
    }

    public Quaternion RestRotation(int levelIndex)
    {
        levelIndex = Mathf.Clamp(levelIndex, 0, _restAngle.Length - 1);
        return Quaternion.Euler(_restAngle[levelIndex], 0f, 0f)
             * Quaternion.Euler(0f, _lateralDeg[levelIndex], 0f);
    }

    // Mismo criterio que LevelMapController.PlayCompletionSequence(): deja que el mapa asiente un
    // instante, revela el nodo recién completado, y recién ahí se desplaza hasta el siguiente —
    // que es el equivalente al PlayerCard deslizándose por el camino de perlas en el mapa real.
    IEnumerator PlayCompletion(LevelNodeView node, int stars, bool gold, float nextAngle, int nextIndex)
    {
        yield return new WaitForSeconds(0.4f);
        yield return node.PlayCompletionTransition(stars, gold);

        // El mundo gira hasta el nivel siguiente y la tarjeta se desliza hasta él, a la vez.
        if (dragRotate) dragRotate.AnimateTo(nextAngle);
        if (_playerCard)
        {
            _cardScrollAngle = _scrollAngle[Mathf.Clamp(nextIndex, 0, _scrollAngle.Length - 1)];
            _cardMove = StartCoroutine(MovePlayerCard(PlayerCardRotation(nextIndex)));
        }
    }

    // Los nodos giran con la esfera (son sus hijos), pero su ORIENTACIÓN se recalcula cada frame:
    // el que está llegando al frente se endereza y mira a la cámara (se lee bien, se ve "acá"),
    // y a medida que se aleja hacia el horizonte vuelve a quedar acostado sobre la superficie,
    // que es lo que da la sensación de que ya se está yendo.
    void LateUpdate()
    {
        // 'Ready' y no solo '_pool != null': el grupo se crea repartido en varios frames, y
        // LateUpdate corre durante esos frames. Sin esperar a que esté completo, se encontraría
        // slots todavía vacíos.
        if (!Ready || _pool == null || !_cam || !sphere || faceCameraRange <= 0f) return;

        // Qué niveles entran en pantalla depende del scroll, así que la ventana se revisa antes de
        // orientar nada. Si no cambió, esto no hace prácticamente trabajo.
        int beforeFirst = WindowFirst, beforeLast = WindowLast;
        UpdateWindow(dragRotate ? dragRotate.CurrentAngle : 0f);
        if (WindowFirst != beforeFirst || WindowLast != beforeLast) OnWindowChanged?.Invoke();

        Vector3 frontDir = (_cam.transform.position - sphere.position).normalized;

        for (int slot = 0; slot < _pool.Length; slot++)
        {
            var node = _pool[slot];
            if (!node || _poolLevel[slot] < 0) continue;

            // Distancia angular al punto más cercano a cámara — sin depender de cuánto giró la
            // esfera, sale de la posición actual del nodo, así no hay que rastrear el scroll.
            Vector3 outward = (node.position - sphere.position).normalized;
            float   away    = Vector3.Angle(outward, frontDir);

            float t = (1f - Mathf.Clamp01(away / faceCameraRange)) * faceCameraMax;

            // La altura acompaña al giro: un nodo plano acostado no necesita levantarse nada,
            // pero al enderezarse gira sobre su centro y su mitad de abajo se enterraría.
            node.localPosition = _poolRot[slot]
                               * (Vector3.back * (_baseRadius + NodeLift(t) / _sphereScale));

            Quaternion onSurface = sphere.rotation * _poolRot[slot];
            if (t <= 0f) { node.rotation = onSurface; continue; }

            // La cara visible del nodo es su -Z (ahí va el contenido), así que para encararlo a
            // cámara su +Z tiene que apuntar en sentido contrario.
            Vector3 toCam = _cam.transform.position - node.position;
            if (toCam.sqrMagnitude < 1e-6f) { node.rotation = onSurface; continue; }

            // El eje del meridiano (X de la esfera) se mantiene como el "a lo ancho" del nodo, y
            // la orientación se arma alrededor de él. Así el nodo solo puede inclinarse hacia
            // adelante y atrás, nunca torcerse de costado.
            Vector3 axis    = sphere.rotation * Vector3.right;
            Vector3 forward = Vector3.ProjectOnPlane(-toCam, axis).normalized;
            if (forward.sqrMagnitude < 1e-6f) { node.rotation = onSurface; continue; }

            Quaternion facing = Quaternion.LookRotation(forward, Vector3.Cross(forward, axis));
            node.rotation = Quaternion.Slerp(onSurface, facing, t);
        }

        // La card siempre de frente, sin mezclar con la superficie: es una tarjeta con el avatar
        // y un nombre, tiene que leerse, no acostarse sobre el agua como los nodos lejanos.
        if (_playerCard)
        {
            // La posición sale siempre de '_playerCardRot', que es lo único que anima
            // MovePlayerCard — así el deslizamiento de un nodo al otro no necesita tocar nada más.
            _playerCard.localPosition = _playerCardRot * (Vector3.back * _cardRadius);

            // Se apaga cuando su nivel queda lejos del scroll actual.
            //
            // La distancia se mide en el ESPACIO DEL SCROLL, no como ángulo en 3D. Con muchos
            // niveles el recorrido da más de una vuelta a la esfera, así que un ángulo de 3D se
            // repite: la card del capítulo 1 volvía a entrar en cuadro al llegar al capítulo 3.
            // El scroll, en cambio, crece sin repetirse.
            if (cullRange > 0.01f && dragRotate)
            {
                bool visible = Mathf.Abs(_cardScrollAngle - dragRotate.CurrentAngle) <= cullRange;
                if (_playerCard.gameObject.activeSelf != visible) _playerCard.gameObject.SetActive(visible);
                if (!visible) return;
            }

            Vector3 axis    = sphere.rotation * Vector3.right;
            Vector3 toCam   = _cam.transform.position - _playerCard.position;
            Vector3 forward = Vector3.ProjectOnPlane(-toCam, axis).normalized;

            _playerCard.rotation = forward.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(forward, Vector3.Cross(forward, axis))
                : sphere.rotation * _playerCardRot;
        }
    }

    // La tarjeta del avatar, apoyada sobre la esfera al lado del nivel actual. Va como hija de la
    // esfera (no del nodo) para que el balanceo del nodo al encararse no la arrastre.
    void CreatePlayerCard(int nodeIndex, float radius, float sphereScale)
    {
        var card = Instantiate(playerCardPrefab, sphere);
        // El nivel va en el nombre: si alguna vez aparece donde no corresponde, se lee de un
        // vistazo en la Hierarchy a qué nodo cree estar pegada.
        card.name = _levels != null && nodeIndex < _levels.Count
            ? $"PlayerCard_nivel_{_levels[nodeIndex].id}"
            : $"PlayerCard_indice_{nodeIndex}";

        // El prefab de la card es UI, y la UI necesita un Canvas para dibujarse. Acá no hay
        // ninguno, así que se le agrega uno propio en World Space — lo mismo que hace a mano el
        // envoltorio de los nodos. Si el prefab ya trae uno, se respeta el suyo.
        if (!card.GetComponent<Canvas>())
            card.gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        _cardRadius        = radius;
        _cardScrollAngle   = _scrollAngle[Mathf.Clamp(nodeIndex, 0, _scrollAngle.Length - 1)];
        _playerCardRot     = PlayerCardRotation(nodeIndex);
        card.localPosition = _playerCardRot * (Vector3.back * radius);
        card.localRotation = _playerCardRot;

        float sourceSize = card.rect.width > 0.001f ? card.rect.width : 1f;
        card.localScale   = Vector3.one * (playerCardSize * WorldScale / sourceSize / sphereScale);

        _playerCard = card;
    }

    // Dónde va la tarjeta respecto a un nodo. La separación sale de los tamaños reales, no a ojo
    // en grados: medio nodo más media tarjeta más el aire pedido, convertido a grados sobre la
    // esfera — así queda pegada aunque cambies 'Node Size' o el tamaño de la tarjeta.
    Quaternion PlayerCardRotation(int nodeIndex)
    {
        float radiusWorld = _baseRadius * _sphereScale;
        float apart       = ((nodeSize + playerCardSize) * 0.5f + playerCardGap) * WorldScale;
        float sideDeg     = apart / Mathf.Max(radiusWorld, 0.0001f) * Mathf.Rad2Deg;

        // Al lado CONTRARIO de hacia donde el zigzag desvió al nodo: así la tarjeta siempre cae
        // hacia el centro del camino y nunca se va contra el borde de la pantalla.
        if (_lateralDeg[nodeIndex] > 0.0001f) sideDeg = -sideDeg;
        if (playerCardFlip)                   sideDeg = -sideDeg;

        // El giro va DESPUÉS de la rotación del nodo, no antes: así es sobre el eje del propio
        // nodo y recorre siempre el mismo arco. Aplicado antes sería sobre el eje polar de la
        // esfera, que recorre mucha distancia cerca del ecuador y ninguna cerca del polo — por eso
        // la tarjeta se iba acercando hasta montarse encima del nodo a medida que subía el camino.
        return RestRotation(nodeIndex) * Quaternion.Euler(0f, sideDeg, 0f);
    }

    // Deslizamiento de la tarjeta de un nodo al siguiente — el equivalente al recorrido por el
    // camino de perlas del mapa real. Solo cambia la rotación de referencia; la posición la
    // recalcula LateUpdate a partir de ella.
    IEnumerator MovePlayerCard(Quaternion to)
    {
        Quaternion from = _playerCardRot;
        for (float t = 0f; t < 1f;)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(playerCardMoveDuration, 0.0001f);
            _playerCardRot = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }
        _playerCardRot = to;
        _cardMove = null;
    }

    // Cuánto sube el centro del nodo por encima de la superficie, en unidades de MUNDO.
    // Acostado como una moneda apoya su cara, así que sube medio espesor; enderezado hacia la
    // cámara gira sobre su centro y necesita medio diámetro. Entre medio, interpola.
    // La esfera placeholder no tiene "canto": su espesor es su propio diámetro.
    float NodeLift(float facing)
    {
        float w       = WorldScale;
        float resting = (_isFlat ? nodeThickness : nodeSize) * 0.5f * w;
        return Mathf.Lerp(resting, nodeSize * 0.5f * w, facing);
    }

    // Mismo flujo que LevelMapController.OnLevelSelected(): sin vidas no se entra, y si hay un
    // StartGamePanel asignado pasa por ahí; si no, navega directo para no dejar el mapa sin jugar.
    // El nodo bloqueado ni siquiera dispara el evento — eso ya lo filtra LevelNodeView.
    void OnLevelSelected(int levelId)
    {
        if (!SaveManager.HasLivesAvailable)
        {
            if (outOfLivesPanel) outOfLivesPanel.Open();
            else Debug.LogWarning("[WorldSphereNodePositioner] Falta asignar 'Out Of Lives Panel' en el Inspector.");
            return;
        }

        var level = LevelLoader.LoadById(levelId);
        if (level == null)
        {
            Debug.LogError($"[WorldSphereNodePositioner] No se encontró el nivel {levelId}");
            return;
        }

        if (startGamePanel)
        {
            startGamePanel.Show(level);
            return;
        }

        Debug.LogWarning("[WorldSphereNodePositioner] Falta asignar 'Start Game Panel' en el Inspector — se navega directo a Gameplay.");
        PlayerPrefs.SetInt("selected_level", levelId);
        SceneLoader.GoTo(SceneLoader.GAMEPLAY);
    }

    // Mismo criterio que LevelMapController.GetState() — no se puede reusar directo porque allá
    // es privado, así que se replica acá (misma lógica, una sola fuente de verdad: SaveManager).
    NodeState GetState(int levelId, int maxUnlocked)
    {
        if (levelId > maxUnlocked)  return NodeState.Locked;
        if (levelId == maxUnlocked) return NodeState.Available;
        return SaveManager.IsLevelGold(levelId) ? NodeState.CompleteFirstTry : NodeState.Completed;
    }

    // Radio (en unidades LOCALES del mesh) del punto MÁS ALTO de la esfera — o sea, sus vértices.
    // Hay que apoyarse ahí y no en el punto más hundido: el mesh tiene pocos polígonos, así que
    // entre vértice y vértice la superficie baja, y un nodo ancho y plano cruza varias caras. Si
    // se lo apoya en el valle, las crestas lo cortan por la mitad.
    float SurfaceRadius(Transform target)
    {
        var filter = target.GetComponent<MeshFilter>();
        if (!filter || !filter.sharedMesh) return 0.5f; // fallback: esfera unitaria de Unity

        var verts = filter.sharedMesh.vertices;
        float max = 0f;
        for (int i = 0; i < verts.Length; i++) max = Mathf.Max(max, verts[i].magnitude);

        return max > 0.0001f ? max : 0.5f;
    }

    Transform CreatePlaceholder()
    {
        // La escala la fija Start() (depende de la escala de la esfera padre), acá solo el mesh.
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(go.GetComponent<Collider>());

        // El Material por defecto de CreatePrimitive no funciona bien en URP (queda invisible o
        // rosado) — le ponemos un Unlit simple para asegurar que se vea.
        var renderer = go.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader)
        {
            var mat = new Material(shader);
            mat.color = Color.white;
            renderer.material = mat;
        }

        return go.transform;
    }

    void SetLabel(Transform node, int number)
    {
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(node, false);
        // Relativo al nodo (que ya mide 1 en local): -0.6 lo deja justo al frente de la superficie.
        labelGO.transform.localPosition = new Vector3(0f, 0f, -0.6f);
        labelGO.transform.localScale = Vector3.one * 0.3f;

        var tmp = labelGO.AddComponent<TextMeshPro>();
        tmp.text = number.ToString();
        tmp.fontSize = 12;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.black;
    }
}
