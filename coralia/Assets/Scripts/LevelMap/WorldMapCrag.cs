using System.Collections.Generic;
using UnityEngine;

// Las rocas que cubren la fosa entre un capítulo y el siguiente.
//
// La pared de malla resuelve la profundidad —el relieve, la grieta, la sombra del filo— pero no
// el dibujo: una malla barrida no tiene el detalle de un sprite pintado a mano. Así que encima
// se planta arte de verdad, hundido hasta que solo asome lo que debe asomar.
//
// Va en CAPAS y no en una sola hilera porque una pieza sola tendría que resolver a la vez el
// filo de acá, el de enfrente y lo que se ve al fondo, y son tres cosas que se miran desde
// ángulos distintos. Separadas, cada una puede usar el prefab que le convenga y quedar a su
// propia altura sin estropear a las otras.
//
// Va aparte de WorldMapDecorations aunque se parezcan: las decoraciones se colocan una a una
// desde el JSON del capítulo y se apoyan en el camino; estas se reparten solas a lo ancho de un
// filo que el suelo ya sabe dónde está.
[ExecuteAlways]
public class WorldMapCrag : MonoBehaviour, IWorldMapRebuildable
{
    [System.Serializable]
    public class Layer
    {
        [Tooltip("Solo para reconocerla en esta lista. No hace nada.")]
        public string name = "Capa";

        public bool enabled = true;

        [Tooltip("La pieza que se repite a lo ancho. Se mide sola: lo que diga 'Height' es lo que va a medir, sea cual sea el tamaño con el que esté hecho el prefab.")]
        public GameObject prefab;

        [Tooltip("Dónde se planta, cruzando la fosa. En 0 va en el filo de acá, el que se deja atrás; en 1 en el de enfrente, el que se ve de cara. Valores intermedios la dejan flotando dentro del hueco, que es donde suele ir la capa de fondo.")]
        [Range(0f, 1f)]
        public float across;

        [Tooltip("Cuánto se corre desde ahí. Positivo la aleja de la cámara. Es el ajuste fino, para separar dos capas que caen en el mismo sitio.")]
        public float zOffset;

        [Tooltip("Cuánto mide de alto la pieza entera, en unidades de mundo.")]
        public float height = 6f;

        [Tooltip("Cuánto asoma por encima de la arena. Lo demás cuelga hacia la fosa. En 0 queda a ras y solo tapa la pared; en negativo se hunde entera.")]
        public float rise = 0.3f;

        [Tooltip("Cuánto se inclina hacia la cámara, en grados. En 0 queda de pie.")]
        public float tilt;

        [Tooltip("Cuánto se solapan dos piezas seguidas, en tanto por uno de su ancho. Sin solape se ve la costura entre una y la siguiente.")]
        [Range(0f, 0.5f)]
        public float overlap = 0.12f;

        [Tooltip("Si la hilera sigue la ondulación del filo más cercano. En la capa de fondo suele estorbar: ahí no hay filo que seguir.")]
        public bool followEdge = true;

        [Tooltip("Cuánto se adelanta o se atrasa en el orden de dibujo, por encima de lo que le toca por su distancia. Sirve para forzar que una capa quede detrás de otra cuando las dos caen casi a la misma profundidad.")]
        public int sortingBias;
    }

    [Tooltip("Las capas de roca, de la más cercana a la más lejana. Cada una con su prefab y sus medidas: dos para los filos, que es lo que conserva las formas del borde, y una de fondo.")]
    [SerializeField] Layer[] layers =
    {
        new() { name = "Filo de acá",     across = 0f,   rise =  0.3f },
        new() { name = "Filo de enfrente", across = 1f,   rise =  0.3f },
        new() { name = "Fondo",            across = 0.5f, rise = -1f, followEdge = false, sortingBias = -50 },
    };

    [Tooltip("El material de los sprites del mundo, el que conoce la curva. Sin él las rocas salen rectas mientras el suelo se hunde. Es el mismo que usa WorldMapDecorations.")]
    [SerializeField] Material spriteMaterial;

    [Tooltip("A qué distancia deja de dibujarse. Igual que en las decoraciones: lo que está más lejos no se ve pero sigue costando.")]
    [SerializeField] float cullRange = 90f;

    readonly List<Transform> _placed = new();
    readonly List<Vector3>   _flat   = new();

    WorldMapDefinition _definition;
    WorldMapGround     _ground;
    Camera             _camera;
    bool               _needsRebuild = true;

    void OnEnable()   => _needsRebuild = true;
    void OnValidate() => _needsRebuild = true;

    // Igual que el suelo y las decoraciones: se anota y se hace en LateUpdate. Dentro de
    // OnValidate, Unity ignora DestroyImmediate y las piezas viejas se quedarían.
    public void Rebuild() => _needsRebuild = true;

    void Build()
    {
        Clear();

        _definition = WorldMapDefinition.For(this);
        if (_definition == null || layers == null) return;

        if (_ground == null) _ground = FindAnyObjectByType<WorldMapGround>();
        if (_ground == null)
        {
            Debug.LogWarning("[WorldMapCrag] No hay WorldMapGround en la escena: sin él no se sabe dónde está el filo de la fosa.", this);
            return;
        }

        var spans = _definition.VisibleSpans();

        foreach (var layer in layers)
        {
            if (layer == null || !layer.enabled) continue;

            if (layer.prefab == null)
            {
                Debug.LogWarning($"[WorldMapCrag] La capa '{layer.name}' está activa pero no tiene prefab.", this);
                continue;
            }

            // Se mide una sola vez por capa y no por fosa: instanciar para medir y tirar la copia
            // ensucia la escena en el editor, y el prefab no cambia de un hueco al siguiente.
            var sample = Instantiate(layer.prefab);
            var bounds = Measure(sample);
            DestroyInstance(sample);

            if (bounds.size.x < 0.001f || bounds.size.y < 0.001f)
            {
                Debug.LogWarning($"[WorldMapCrag] '{layer.prefab.name}' no tiene ningún Renderer con tamaño: no se puede saber cuánto mide para repartirlo.", this);
                continue;
            }

            for (int i = 0; i + 1 < spans.Count; i++)
                Line(layer, bounds, _ground.IslandEnd(spans[i]), _ground.IslandStart(spans[i + 1]));
        }
    }

    // Una hilera de piezas cruzando el mapa de lado a lado.
    void Line(Layer layer, Bounds bounds, float nearZ, float farZ)
    {
        float scale = layer.height / bounds.size.y;
        float step  = bounds.size.x * scale * (1f - layer.overlap);
        int   count = Mathf.Max(1, Mathf.CeilToInt(_ground.Width / step) + 1);

        float baseZ = Mathf.Lerp(nearZ, farZ, layer.across) + layer.zOffset;

        // La ondulación es la del filo que tenga más cerca: son dos filos distintos y cada uno
        // ondula a su manera, así que una capa a medio camino tiene que elegir uno.
        float edge = layer.across < 0.5f ? nearZ : farZ;

        // Lo que sobresale del pivote por arriba. Restarlo deja el TOPE de la pieza a ras de la
        // arena, y con eso 'Rise' se lee como "cuánto asoma" en vez de como una altura suelta que
        // hay que buscar a tientas.
        float top = bounds.max.y * scale;

        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) * 0.5f) * step;
            float z = baseZ + (layer.followEdge ? _ground.EdgeAt(x, edge) : 0f);

            Place(layer, new Vector3(x, layer.rise - top, z), scale);
        }
    }

    void Place(Layer layer, Vector3 flat, float scale)
    {
        var instance = Instantiate(layer.prefab);
        var tr       = instance.transform;

        // No se guarda con la escena: se regenera sola en cada OnEnable, y sin esto cada guardado
        // dejaría dentro del .unity cientos de objetos que el componente vuelve a crear igual.
        // Sobreviven a una recompilación de scripts aunque la lista no, pero Clear() recorre los
        // hijos del transform y no la lista, así que los readopta y los borra igual.
        instance.hideFlags = HideFlags.DontSave;

        tr.SetParent(transform, false);
        tr.localScale    = Vector3.one * scale;
        tr.localPosition = flat;
        tr.localRotation = Quaternion.Euler(layer.tilt, 0f, 0f);

        int order = _definition.SortingOrder(flat.z) + layer.sortingBias;

        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sortingOrder += order;

            // Un único material para todas las piezas: SpriteRenderer le pasa la textura de su
            // sprite por _MainTex, así que el material no necesita saber cuál es.
            if (spriteMaterial && renderer is SpriteRenderer)
                renderer.sharedMaterial = spriteMaterial;
        }

        _placed.Add(tr);
        _flat.Add(flat);
    }

    static Bounds Measure(GameObject instance)
    {
        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        // El prefab se instancia en el origen y sin escalar, así que los bounds de mundo ya son
        // los del propio prefab.
        return bounds;
    }

    void LateUpdate()
    {
        if (_needsRebuild)
        {
            _needsRebuild = false;
            Build();
        }

        if (_camera == null) _camera = Camera.main;
        if (_camera == null || _placed.Count == 0) return;

        float cameraZ = _camera.transform.position.z - transform.position.z;

        for (int i = 0; i < _placed.Count; i++)
        {
            if (!_placed[i]) continue;

            float distance = _flat[i].z - cameraZ;
            bool  visible  = distance > -cullRange * 0.25f && distance < cullRange;

            if (_placed[i].gameObject.activeSelf != visible)
                _placed[i].gameObject.SetActive(visible);

            // La curva se aplica a mano, por frame: son sprites sueltos y si no se moviesen
            // quedarían rectos mientras el suelo al que se apoyan se hunde hacia el horizonte.
            if (visible)
            {
                Vector3 curved = WorldMapCurve.Curve(transform.TransformPoint(_flat[i]), _camera);
                _placed[i].localPosition = transform.InverseTransformPoint(curved);
            }
        }
    }

    void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyInstance(transform.GetChild(i).gameObject);

        _placed.Clear();
        _flat.Clear();
    }

    static void DestroyInstance(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else                       DestroyImmediate(go);
    }
}
