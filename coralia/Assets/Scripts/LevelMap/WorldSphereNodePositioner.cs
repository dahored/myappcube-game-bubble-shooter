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

    // Geometría del recorrido, para que otros puedan dibujar sobre él (ej. el camino).
    // Las rotaciones son las de REPOSO de cada nodo, en el espacio local de la esfera.
    public IReadOnlyList<Quaternion> NodeRotations     => _restRotations;
    public IReadOnlyList<int>        NodeChapters      => _chapters;
    public float                     SurfaceRadiusLocal => _baseRadius;
    public float                     SphereScale        => _sphereScale;

    Transform[] _nodes;
    Quaternion[] _restRotations;
    Camera      _cam;
    float       _baseRadius;   // radio de apoyo, en unidades locales de la esfera
    float       _sphereScale;
    bool        _isFlat;       // el prefab es de UI (plano) y no una primitiva con volumen
    float[]       _lateralDeg;  // desvío del zigzag de cada nodo, para saber de qué lado va la card
    int[]         _chapters;    // capítulo de cada nodo, para cortar el camino entre capítulos
    RectTransform _playerCard;
    Quaternion    _playerCardRot;
    float         _cardRadius;
    Coroutine     _cardMove;

    void Start()
    {
        if (!sphere) { Debug.LogWarning("[WorldSphereNodePositioner] Falta asignar 'Sphere' en el Inspector."); return; }

        var cam = worldCamera ? worldCamera : Camera.main;
        if (!cam) { Debug.LogWarning("[WorldSphereNodePositioner] No hay cámara (ni 'World Camera' asignada ni Camera.main)."); return; }
        _cam = cam;

        var levels = LoadAllLevels();
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

        _nodes      = new Transform[count];
        _restRotations = new Quaternion[count];
        _lateralDeg    = new float[count];
        _chapters      = new int[count];

        // El "frente" NO es el -Z local de la esfera: es el punto de la superficie más cercano a
        // la cámara, que depende de dónde esté puesta la esfera. Con la esfera abajo y adelante,
        // ese punto queda decenas de grados por encima del -Z — si no se tiene en cuenta, el
        // nodo 1 termina detrás de la cámara y el orden se ve al revés.
        Vector3 toCamera  = cam.transform.position - sphere.position;
        float frontAngle  = Mathf.Atan2(toCamera.y, -toCamera.z) * Mathf.Rad2Deg;

        // Todo sale del Scale real de la esfera, no de un campo a mano — así no puede quedar
        // desincronizado si se cambia la escala.
        float sphereScale = Mathf.Max(sphere.localScale.x, 0.0001f);

        _baseRadius  = SurfaceRadius(sphere);
        _sphereScale = sphereScale;
        _isFlat      = nodePrefab is RectTransform;

        float radius = _baseRadius + NodeLift(1f) / sphereScale;

        int   maxUnlocked = SaveManager.MaxUnlockedLevel;
        float offset      = bottomPadding; // ángulo acumulado desde el frente

        // Si GameplayController acaba de avanzar el progreso (issue #55), ese nivel arranca
        // invisible y se revela con el pop + las estrellas en cadena, igual que en el mapa real.
        // La bandera se CONSUME al leerla, así que solo la puede leer un mapa — y en esta escena
        // LevelMapController está desactivado.
        int           justAdvancedFrom   = SaveManager.ConsumeJustAdvancedFromLevel();
        LevelNodeView justCompletedNode  = null;
        int           justCompletedStars = 0;
        bool          justCompletedGold  = false;
        int           justCompletedIndex = -1;
        int           currentIndex       = -1; // el nivel disponible, donde arranca el mapa

        // Cuánto hay que girar para dejar cada nodo en la posición de arranque del nodo 1.
        var scrollAngles = new float[count];

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

            // Nivel 1 (i=0) justo arriba del frente (por 'Bottom Padding'), y de ahí subiendo
            // hacia el horizonte: el menor abajo, el mayor arriba. Ese es su ángulo EN REPOSO.
            float restAngle     = frontAngle + offset;
            scrollAngles[i]     = offset - bottomPadding;

            Transform node = nodePrefab ? Instantiate(nodePrefab, sphere) : CreatePlaceholder();
            node.SetParent(sphere, false);
            node.name = levels.Count > 0 ? $"Node_{levels[i].id:000}" : $"NodeProto_{i + 1}";

            // Dos rotaciones encadenadas sobre la esfera: el vaivén lateral (eje Y) desvía el
            // camino a los costados, y el avance del recorrido (eje X) lo sube hacia el horizonte.
            // Al componerlas sobre un vector unitario, el nodo queda siempre sobre la superficie
            // — no hay forma de que el zigzag lo despegue o lo hunda.
            float lateral = zigzagPeriod > 0.01f
                ? Mathf.Sin(i / zigzagPeriod * Mathf.PI * 2f) * zigzagAmount
                : 0f;
            Quaternion rot = Quaternion.Euler(restAngle, 0f, 0f) * Quaternion.Euler(0f, lateral, 0f);

            // Como el nodo es HIJO de la esfera (que tiene Scale grande), hay que dividir tanto
            // el radio como la escala — si no, Unity multiplica todo por la escala del padre.
            node.localPosition = rot * (Vector3.back * radius);
            node.localRotation = rot; // parado sobre la superficie
            _restRotations[i]  = rot;
            _lateralDeg[i]     = lateral;
            _chapters[i]       = levels.Count > 0 ? levels[i].chapter : 1;

            // 'Node Size' es siempre el diámetro en unidades de MUNDO. Un prefab de UI mide
            // cientos (píxeles) en su propio espacio y una primitiva mide 1, así que hay que
            // normalizar por su tamaño propio antes de dividir por la escala de la esfera.
            float sourceSize = node is RectTransform rt && rt.rect.width > 0.001f ? rt.rect.width : 1f;
            node.localScale  = Vector3.one * (nodeSize / sourceSize / sphereScale);

            _nodes[i]      = node;

            // El prefab real trae LevelNodeView adentro (colgando del Canvas envoltorio), y se
            // configura igual que en LevelMapController. Si no lo tiene, es un placeholder de
            // prueba y se le pone el número a mano.
            var view = node.GetComponentInChildren<LevelNodeView>(true);
            if (view && levels.Count > 0)
            {
                int levelId = levels[i].id;
                var state   = GetState(levelId, maxUnlocked);
                view.Setup(levelId, state, SaveManager.GetLevelStars(levelId));
                view.OnClicked += OnLevelSelected;

                // Los cilindros del canto no los toca LevelNodeView (es un archivo compartido con
                // el mapa real), así que su material lo cambia este componente aparte.
                var edges = node.GetComponentInChildren<CurvedMapNodeEdgeMaterials>(true);
                if (edges) edges.ApplyState(state);

                if (state == NodeState.Available) currentIndex = i;

                if (levelId == justAdvancedFrom)
                {
                    justCompletedNode  = view;
                    justCompletedStars = SaveManager.GetLevelStars(levelId);
                    justCompletedGold  = state == NodeState.CompleteFirstTry;
                    justCompletedIndex = i;
                    view.HideForReveal(); // invisible hasta que la corrutina lo revele
                }
            }
            else if (!view)
            {
                SetLabel(node, i + 1);
            }
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

        // El camino y las decoraciones se construyen recién acá, cuando el recorrido de todos los
        // nodos ya está calculado — los dos se apoyan sobre él.
        if (pathRibbon)  pathRibbon.Build();
        if (decorations) decorations.Build();

        // Si no hay ninguno disponible (capítulos terminados), se queda en el último.
        if (currentIndex < 0) currentIndex = count - 1;

        // Si se vuelve de ganar, la tarjeta aparece todavía al lado del nivel que se acaba de
        // pasar, y se desliza al siguiente una vez terminada la revelación del nodo.
        if (playerCardPrefab)
            CreatePlayerCard(justCompletedIndex >= 0 ? justCompletedIndex : currentIndex, radius, sphereScale);

        float currentAngle = Mathf.Clamp(scrollAngles[currentIndex], 0f, AngleMax);
        CurrentNodeAngle   = currentAngle;

        if (!dragRotate)
        {
            Debug.LogWarning("[WorldSphereNodePositioner] Falta asignar 'Drag Rotate' en el Inspector — el mapa va a abrir en el nodo 1 en vez del actual.");
            return;
        }

        if (justCompletedIndex >= 0)
        {
            // Se vuelve de ganar: el mapa abre donde quedó el nivel recién pasado, y una vez
            // revelado se desplaza solo hasta el siguiente.
            dragRotate.JumpTo(Mathf.Clamp(scrollAngles[justCompletedIndex], 0f, AngleMax));
            StartCoroutine(PlayCompletion(justCompletedNode, justCompletedStars, justCompletedGold, currentAngle, currentIndex));
        }
        else
        {
            // Entrada normal al mapa: directo al nivel disponible.
            dragRotate.JumpTo(currentAngle);
        }
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
        if (_playerCard) _cardMove = StartCoroutine(MovePlayerCard(PlayerCardRotation(nextIndex)));
    }

    // Los nodos giran con la esfera (son sus hijos), pero su ORIENTACIÓN se recalcula cada frame:
    // el que está llegando al frente se endereza y mira a la cámara (se lee bien, se ve "acá"),
    // y a medida que se aleja hacia el horizonte vuelve a quedar acostado sobre la superficie,
    // que es lo que da la sensación de que ya se está yendo.
    void LateUpdate()
    {
        if (_nodes == null || !_cam || !sphere || faceCameraRange <= 0f) return;

        Vector3 frontDir = (_cam.transform.position - sphere.position).normalized;

        for (int i = 0; i < _nodes.Length; i++)
        {
            var node = _nodes[i];
            if (!node) continue;

            // Distancia angular al punto más cercano a cámara — sin depender de cuánto giró la
            // esfera, sale de la posición actual del nodo, así no hay que rastrear el scroll.
            Vector3 outward = (node.position - sphere.position).normalized;
            float   away    = Vector3.Angle(outward, frontDir);
            float   t       = (1f - Mathf.Clamp01(away / faceCameraRange)) * faceCameraMax;

            // La altura acompaña al giro: un nodo plano acostado no necesita levantarse nada,
            // pero al enderezarse gira sobre su centro y su mitad de abajo se enterraría.
            node.localPosition = _restRotations[i]
                               * (Vector3.back * (_baseRadius + NodeLift(t) / _sphereScale));

            Quaternion onSurface = sphere.rotation * _restRotations[i];
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
        card.name = "PlayerCard";

        // El prefab de la card es UI, y la UI necesita un Canvas para dibujarse. Acá no hay
        // ninguno, así que se le agrega uno propio en World Space — lo mismo que hace a mano el
        // envoltorio de los nodos. Si el prefab ya trae uno, se respeta el suyo.
        if (!card.GetComponent<Canvas>())
            card.gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        _cardRadius        = radius;
        _playerCardRot     = PlayerCardRotation(nodeIndex);
        card.localPosition = _playerCardRot * (Vector3.back * radius);
        card.localRotation = _playerCardRot;

        float sourceSize = card.rect.width > 0.001f ? card.rect.width : 1f;
        card.localScale   = Vector3.one * (playerCardSize / sourceSize / sphereScale);

        _playerCard = card;
    }

    // Dónde va la tarjeta respecto a un nodo. La separación sale de los tamaños reales, no a ojo
    // en grados: medio nodo más media tarjeta más el aire pedido, convertido a grados sobre la
    // esfera — así queda pegada aunque cambies 'Node Size' o el tamaño de la tarjeta.
    Quaternion PlayerCardRotation(int nodeIndex)
    {
        float radiusWorld = _baseRadius * _sphereScale;
        float apart       = (nodeSize + playerCardSize) * 0.5f + playerCardGap;
        float sideDeg     = apart / Mathf.Max(radiusWorld, 0.0001f) * Mathf.Rad2Deg;

        // Al lado CONTRARIO de hacia donde el zigzag desvió al nodo: así la tarjeta siempre cae
        // hacia el centro del camino y nunca se va contra el borde de la pantalla.
        if (_lateralDeg[nodeIndex] > 0.0001f) sideDeg = -sideDeg;
        if (playerCardFlip)                   sideDeg = -sideDeg;

        // El giro va DESPUÉS de la rotación del nodo, no antes: así es sobre el eje del propio
        // nodo y recorre siempre el mismo arco. Aplicado antes sería sobre el eje polar de la
        // esfera, que recorre mucha distancia cerca del ecuador y ninguna cerca del polo — por eso
        // la tarjeta se iba acercando hasta montarse encima del nodo a medida que subía el camino.
        return _restRotations[nodeIndex] * Quaternion.Euler(0f, sideDeg, 0f);
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
        float resting = _isFlat ? nodeThickness * 0.5f : nodeSize * 0.5f;
        return Mathf.Lerp(resting, nodeSize * 0.5f, facing);
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

    // Mismo patrón que LevelMapController.LoadAllLevels(): carga todos los JSON de niveles
    // (Resources/Levels/Chapter_N/*.json vía LevelData) y descarta los de prueba (ids >= 900,
    // ej. 999_test_rescue.json). Ordenados por capítulo y después por id, que es el orden en el
    // que se recorren en el mapa.
    List<LevelData> LoadAllLevels()
    {
        var result = new List<LevelData>();
        foreach (var json in Resources.LoadAll<TextAsset>("Levels"))
        {
            var lvl = JsonUtility.FromJson<LevelData>(json.text);
            if (lvl != null && lvl.id < 900) result.Add(lvl);
        }
        result.Sort((a, b) => a.chapter != b.chapter ? a.chapter.CompareTo(b.chapter) : a.id.CompareTo(b.id));
        return result;
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
