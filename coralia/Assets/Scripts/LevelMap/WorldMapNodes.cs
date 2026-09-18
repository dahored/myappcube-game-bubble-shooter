using System.Collections.Generic;
using UnityEngine;

// Coloca los nodos de nivel sobre el camino, reciclando un puñado fijo de objetos.
//
// No instancia uno por nivel: con 180 niveles —y la idea de llegar a 500 por mundo— eso sería
// medio millar de objetos vivos para mostrar una docena. En su lugar hay un pool del tamaño de
// lo que se ve, y al scrollear los que salen por un extremo se reasignan al nivel que entra por
// el otro.
//
// El slot de cada nivel sale de 'índice % tamaño del pool'. Es un buffer circular: dos niveles
// que compartan slot nunca están visibles a la vez, porque el pool es más grande que la ventana.
public class WorldMapNodes : MonoBehaviour, IWorldMapRebuildable
{
    [Tooltip("El prefab del nodo. Puede traer el LevelNodeView en la raíz o adentro — los nodos de mundo lo llevan dentro de un Canvas propio.")]
    [SerializeField] GameObject nodePrefab;
    [SerializeField] WorldMapScroll scroll;

    [Header("Al tocar un nodo")]
    [Tooltip("Pantalla previa al nivel. Sin esto se navega directo a Gameplay.")]
    [SerializeField] StartGamePanel startGamePanel;

    [Tooltip("Se abre cuando no quedan vidas, en vez de entrar al nivel.")]
    [SerializeField] OutOfLivesPanel outOfLivesPanel;

    [Tooltip("Alto del nodo en unidades de mundo. El prefab viene medido en píxeles de UI, así que se reescala al instanciarlo.")]
    [SerializeField] float nodeSize = 2.2f;

    [Tooltip("Cuánto se levantan del suelo. Tiene que ser MAYOR que el Height de la cinta, o el camino se dibuja encima y les tapa la mitad de abajo.")]
    [SerializeField] float height = 0.06f;

    [Tooltip("90 = acostado sobre el camino. 0 = parado de frente a la cámara. En el medio se inclina hacia el jugador para que el número se lea mejor.")]
    [Range(0f, 90f)]
    [SerializeField] float tilt = 90f;

    [Header("Ventana visible, en unidades de mundo desde la cámara")]
    [Tooltip("Cuánto por detrás de la cámara se siguen manteniendo nodos. Evita que el de abajo desaparezca al borde.")]
    [SerializeField] float behind = 12f;

    [Tooltip("Hasta dónde hacia el horizonte hay nodos.")]
    [SerializeField] float ahead = 80f;

    [Tooltip("Slots de más en el pool. Amortiguan el frame en que la ventana crece por un salto de scroll.")]
    [SerializeField] int spare = 4;

    public event System.Action<int> OnNodeClicked;

    WorldMapDefinition _definition;
    Transform[]        _pool;        // lo que se mueve
    LevelNodeView[]    _views;       // lo que se configura: puede estar en un hijo del prefab
    CurvedMapNodeEdgeMaterials[] _edges;   // el canto de moneda, que cambia de material según el estado
    NodeState[]        _states;      // para no abrir un nivel bloqueado desde el hit test
    int[]              _ids;         // el id real del nivel de cada slot
    int[]              _slotLevel;   // qué índice de nivel tiene cada slot, -1 si está libre
    int                _first = -1, _last = -2;
    bool               _needsRebuild;

    // En Start y no en OnEnable: instanciar y activar objetos durante OnEnable dispara
    // "SendMessage cannot be called during Awake, CheckConsistency, or OnValidate" por cada
    // hijo del prefab. Un frame más tarde es perfectamente válido.
    void Start()
    {
        Build();
        if (scroll) scroll.OnTap += OnTap;
    }

    void OnDestroy()
    {
        if (scroll) scroll.OnTap -= OnTap;
    }

    // Qué nodo se tocó, resuelto en pantalla: se proyecta cada nodo visible y gana el más cercano
    // al dedo dentro de su propio radio. Se proyecta la posición CURVADA, no la del transform,
    // porque el shader dibuja los nodos en otro lado del que dice su Transform.
    void OnTap(Vector2 screenPosition)
    {
        var camera = Camera.main;
        if (_pool == null || camera == null) return;

        int   bestSlot     = -1;
        float bestDistance = float.MaxValue;

        for (int slot = 0; slot < _pool.Length; slot++)
        {
            if (_slotLevel[slot] < 0 || _states[slot] == NodeState.Locked) continue;

            Vector3 world = WorldMapCurve.Curve(_pool[slot].position, camera);
            Vector3 point = camera.WorldToScreenPoint(world);
            if (point.z <= 0f) continue; // detrás de la cámara

            // El radio en píxeles se mide proyectando un punto al costado: así un nodo lejano
            // pide precisión y uno cercano perdona, igual que se ve.
            Vector3 edge   = camera.WorldToScreenPoint(world + camera.transform.right * (nodeSize * 0.5f));
            float   radius = Mathf.Max(12f, Vector2.Distance(point, edge));

            float distance = Vector2.Distance(screenPosition, point);
            if (distance > radius || distance >= bestDistance) continue;

            bestDistance = distance;
            bestSlot     = slot;
        }

        if (bestSlot >= 0) OnLevelSelected(_ids[bestSlot]);
    }

    // Rebuild se pide desde OnValidate de WorldMapDefinition, y ahí Unity no deja instanciar ni
    // activar objetos — avisa con "SendMessage cannot be called during Awake, CheckConsistency,
    // or OnValidate" por cada hijo del prefab. Se anota y se hace un frame después.
    public void Rebuild() => _needsRebuild = true;

    void Build()
    {
        _definition = WorldMapDefinition.For(this);
        if (_definition == null || nodePrefab == null || !Application.isPlaying) return;

        Release();

        float spacing = Mathf.Max(0.01f, _definition.Layout.spacing);
        int   size    = Mathf.Min(_definition.Levels,
                                  Mathf.CeilToInt((behind + ahead) / spacing) + Mathf.Max(0, spare));

        // El prefab es un Canvas en espacio de mundo: su RectTransform mide en píxeles de UI,
        // así que instanciarlo tal cual da un nodo de 200 unidades de mundo. Se convierte una vez
        // acá, con el alto que trae el prefab como referencia.
        float sourceSize = 1f;
        if (nodePrefab.transform is RectTransform rect && rect.rect.height > 0.01f)
            sourceSize = rect.rect.height;
        float scale = nodeSize / sourceSize;

        _pool      = new Transform[Mathf.Max(1, size)];
        _views     = new LevelNodeView[_pool.Length];
        _edges     = new CurvedMapNodeEdgeMaterials[_pool.Length];
        _states    = new NodeState[_pool.Length];
        _ids       = new int[_pool.Length];
        _slotLevel = new int[_pool.Length];

        for (int i = 0; i < _pool.Length; i++)
        {
            var instance = Instantiate(nodePrefab, transform);
            instance.SetActive(false);

            instance.transform.localScale = Vector3.one * scale;

            // El prefab trae un Canvas en espacio de mundo sin cámara de eventos. Antes la
            // heredaba de un Canvas padre que vivía en la escena; ahora cada nodo es su propia
            // raíz, y un Canvas world space sin cámara no recibe clicks.
            var canvas = instance.GetComponent<Canvas>();
            if (canvas && canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
                canvas.worldCamera = Camera.main;

            _pool[i]  = instance.transform;
            _views[i] = instance.GetComponentInChildren<LevelNodeView>(true);
            _edges[i] = instance.GetComponentInChildren<CurvedMapNodeEdgeMaterials>(true);

            if (_views[i] == null)
                Debug.LogWarning($"[WorldMapNodes] '{nodePrefab.name}' no tiene ningún LevelNodeView " +
                                 "ni en su raíz ni adentro: los nodos se van a ver pero sin número ni estrellas.");
            // No se engancha LevelNodeView.OnClicked: ese botón nunca recibe el puntero, porque
            // el DragArea lo intercepta. El toque llega por WorldMapScroll.OnTap.

            _slotLevel[i] = -1;
        }

        _first = -1; _last = -2;
        UpdateWindow();
    }

    void Release()
    {
        if (_pool == null) return;
        foreach (var node in _pool)
            if (node) Destroy(node.gameObject);
        _views  = null;
        _edges  = null;
        _states = null;
        _ids    = null;
        _pool = null;
    }

    void LateUpdate()
    {
        if (_needsRebuild)
        {
            _needsRebuild = false;
            Build();
        }

        if (_pool != null) UpdateWindow();
    }

    void UpdateWindow()
    {
        float spacing = Mathf.Max(0.01f, _definition.Layout.spacing);
        float offset  = scroll ? scroll.Offset : 0f;

        // El nivel i está en z = i * spacing dentro del mundo, y el mundo está corrido -offset.
        // Así que lo que la cámara tiene delante va de (offset - behind) a (offset + ahead).
        int first = Mathf.Max(0, Mathf.FloorToInt((offset - behind) / spacing));
        int last  = Mathf.Min(_definition.Levels - 1, Mathf.CeilToInt((offset + ahead) / spacing));

        if (first == _first && last == _last) return;

        // Primero se sueltan los que salieron: si se ligaran los nuevos antes, un slot reusado
        // se apagaría justo después de haberlo puesto.
        for (int slot = 0; slot < _pool.Length; slot++)
        {
            int held = _slotLevel[slot];
            if (held < 0 || (held >= first && held <= last)) continue;

            _slotLevel[slot] = -1;
            _pool[slot].gameObject.SetActive(false);
        }

        for (int i = first; i <= last; i++) Bind(i);

        _first = first; _last = last;
    }

    // Mismo flujo que LevelMapController.OnLevelSelected(): sin vidas no se entra, y si hay un
    // StartGamePanel asignado se pasa por ahí. El nodo bloqueado ni siquiera dispara el evento —
    // eso ya lo filtra LevelNodeView.
    void OnLevelSelected(int levelId)
    {
        OnNodeClicked?.Invoke(levelId);

        if (!SaveManager.HasLivesAvailable)
        {
            if (outOfLivesPanel) outOfLivesPanel.Open();
            else Debug.LogWarning("[WorldMapNodes] Falta asignar 'Out Of Lives Panel' en el Inspector.");
            return;
        }

        var level = LevelLoader.LoadById(levelId);
        if (level == null)
        {
            Debug.LogError($"[WorldMapNodes] No se encontró el nivel {levelId}.");
            return;
        }

        if (startGamePanel)
        {
            startGamePanel.Show(level);
            return;
        }

        Debug.LogWarning("[WorldMapNodes] Falta asignar 'Start Game Panel' en el Inspector — se navega directo a Gameplay.");
        PlayerPrefs.SetInt("selected_level", levelId);
        SceneLoader.GoTo(SceneLoader.GAMEPLAY);
    }

    void Bind(int index)
    {
        int slot = index % _pool.Length;
        if (_slotLevel[slot] == index) return;   // ya está puesto, no hay nada que rehacer

        var entries = LevelLoader.Entries;
        if (index >= entries.Count) return;

        var node = _pool[slot];
        Vector3 position = WorldMapPath.Sample(index, _definition.Layout);
        position.y += height;
        node.localPosition = position;

        // Solo se inclina en X. Girarlo también en Y para que siga la curva del camino haría
        // que el número quedara torcido en cada ese, y el número es lo que hay que leer.
        node.localRotation = Quaternion.Euler(tilt, 0f, 0f);

        // Mismo criterio que LevelMapController.GetState(): no se puede reusar directo porque
        // allá es un método privado de un MonoBehaviour de otra escena.
        int id          = entries[index].id;
        int maxUnlocked = SaveManager.MaxUnlockedLevel;

        NodeState state = id > maxUnlocked  ? NodeState.Locked
                        : id == maxUnlocked ? NodeState.Available
                        : SaveManager.IsLevelGold(id) ? NodeState.CompleteFirstTry
                                                      : NodeState.Completed;

        // Activar ANTES de configurar: Setup arranca una corrutina, y Unity no la deja correr
        // sobre un objeto apagado.
        node.gameObject.SetActive(true);
        if (_views[slot]) _views[slot].Setup(id, state, SaveManager.GetLevelStars(id));

        // El canto de moneda va aparte: LevelNodeView se comparte con el mapa plano y no sabe
        // nada de cilindros, así que el material dorado lo aplica este otro componente.
        if (_edges[slot]) _edges[slot].ApplyState(state);

        _states[slot] = state;
        _ids[slot]    = id;
        _slotLevel[slot] = index;
    }
}
