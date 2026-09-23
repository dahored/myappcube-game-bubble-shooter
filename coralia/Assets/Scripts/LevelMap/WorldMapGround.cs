using System.Collections.Generic;
using UnityEngine;

// El suelo del mapa: una isla por capítulo, cada una un plano en XZ generado por código.
//
// Subdividido porque la curvatura mueve VÉRTICES. Un quad de cuatro esquinas no se dobla: se
// inclina. Para que el terreno se hunda parejo hacia el horizonte hace falta que tenga vértices
// repartidos a lo largo, y cuanto más lejos llega, más necesita.
//
// Generado por código y no como asset para poder cambiar largo y densidad desde el Inspector sin
// reexportar nada — la densidad es el primer número que hay que buscar a ojo en el dispositivo.
//
// Cada isla es un GameObject aparte y no submallas de una sola: así el material sale del capítulo
// y no de una posición en una lista. La ventana de capítulos visibles ROTA al scrollear, y con
// submallas el orden de los materiales tendría que rotar con ella sin equivocarse nunca.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WorldMapGround : MonoBehaviour, IWorldMapRebuildable
{
    [System.Serializable]
    public class GroundMaterial
    {
        [Tooltip("Cómo se lo nombra en el JSON del capítulo, en el campo 'ground'.")]
        public string   id;
        public Material material;
    }

    [Tooltip("Cuánto sobra de suelo a cada lado del zigzag.")]
    [SerializeField] float sideMargin = 10f;

    [Tooltip("Cuánto sobra de suelo en las puntas INTERIORES de cada isla, las que dan al capítulo vecino. Es lo que decide la separación entre capítulos, junto con 'Chapter Gap'.")]
    [SerializeField] float endMargin = 12f;

    [Tooltip("Cuánto sobra antes de lo primero que se ve del mundo. Va aparte del margen interior porque ahí no hay isla vecina que separar, solo agua de más.")]
    [SerializeField] float outerStartMargin = 2f;

    [Tooltip("Cuánto sobra después de lo último que se ve del mundo. Separado del de arriba porque no cumplen la misma función: este decide cuánto se alcanza a ver al final, y el otro solo cuánta agua queda antes del primer nodo.")]
    [SerializeField] float outerEndMargin = 2f;

    [Tooltip("Cuántos cortes por unidad de mundo. Más alto es más suave y más caro.")]
    [SerializeField] float density = 0.5f;

    [Tooltip("Cada cuántas unidades de mundo se repite la textura.")]
    [SerializeField] float tiling = 8f;

    [Header("Suelo por capítulo")]
    [Tooltip("Qué material le toca a cada 'ground' de los Chapter_N.json. Un capítulo sin 'ground' —o con uno que no esté acá— usa el material de este mismo objeto.")]
    [SerializeField] GroundMaterial[] materials;

    const string ISLAND_PREFIX = "Island ";

    WorldMapDefinition _definition;

    // Las islas vivas, en el mismo orden que VisibleSpans(). Se reciclan entre reconstrucciones:
    // al cruzar de capítulo cambia CUÁL se dibuja, no cuántas, así que crear y destruir objetos
    // en cada cruce sería basura para nada.
    readonly List<MeshFilter> _islands = new();

    // Leer y parsear el JSON de un capítulo por cada consulta sería caro: WorldStart y WorldEnd
    // los pide el scroll para calcular sus topes, o sea varias veces por frame.
    readonly Dictionary<int, (float min, float max)> _decorationRange = new();

    bool _needsRebuild = true;

    // Los extremos del terreno en unidades de mundo, con el margen de cada punta ya incluido.
    // Los lee el scroll para saber hasta dónde dejar arrastrar, así que salen de TODOS los
    // capítulos y no solo de los que están construidos ahora mismo.
    public float WorldStart
    {
        get
        {
            var map = _definition != null ? _definition : WorldMapDefinition.For(this);
            if (map == null) return 0f;

            var spans = map.Spans();
            return spans.Count == 0 ? 0f : (ContentStart(spans[0]) - outerStartMargin) * map.Layout.spacing;
        }
    }

    public float WorldEnd
    {
        get
        {
            var map = _definition != null ? _definition : WorldMapDefinition.For(this);
            if (map == null) return 0f;

            var spans = map.Spans();
            return spans.Count == 0 ? 0f : (ContentEnd(spans[^1]) + outerEndMargin) * map.Layout.spacing;
        }
    }

    // Hasta dónde llega lo que de verdad se VE de un capítulo, en índice de mundo: el primer y el
    // último nodo, o las decoraciones que se salgan más allá de ellos.
    //
    // Los márgenes exteriores se miden contra esto y no contra los nodos, porque el marco de
    // cierre se coloca DESPUÉS del último nodo (ver ChapterData.IndexIn: 'end' cae medio nodo o un
    // nodo más allá). Midiendo desde el nodo, el margen tenía que cubrir el cierre Y el agua que
    // se quiere ver detrás, así que para que el último nodo siguiera siendo alcanzable había que
    // subirlo hasta dejar varios nodos de agua vacía después del cierre — que es exactamente lo
    // que se veía suelto al final del mundo.
    //
    // Así el margen significa lo que uno espera: cuánto sobra DESPUÉS de la última pieza. Y un
    // capítulo nuevo con otro cierre se acomoda solo.
    float ContentStart(WorldMapDefinition.Span span) => span.start + DecorationRange(span).min;
    float ContentEnd(WorldMapDefinition.Span span)   => span.start + DecorationRange(span).max;

    // Lo que ocupan las decoraciones del capítulo, en índice DENTRO del capítulo. Arranca en el
    // rango de los nodos (0 .. último) y se ensancha con lo que sobresalga: un capítulo sin
    // decoraciones queda exactamente igual que antes.
    (float min, float max) DecorationRange(WorldMapDefinition.Span span)
    {
        if (_decorationRange.TryGetValue(span.number, out var cached)) return cached;

        float last  = Mathf.Max(0, span.count - 1);
        var   range = (min: 0f, max: last);

        var json = Resources.Load<TextAsset>($"Chapters/Chapter_{span.number}");
        var data = json ? JsonUtility.FromJson<ChapterData>(json.text) : null;

        if (data?.decorations != null)
            foreach (var placement in data.decorations)
            {
                if (placement == null) continue;

                float at = placement.IndexIn(span.count);
                if (at < range.min) range.min = at;
                if (at > range.max) range.max = at;
            }

        _decorationRange[span.number] = range;
        return range;
    }

    void OnEnable()   => _needsRebuild = true;
    void OnValidate() => _needsRebuild = true;

    // Igual que las decoraciones: nunca dentro de OnValidate. Ahí Unity ignora DestroyImmediate y
    // se queja si se activan objetos, y ahora esto crea GameObjects, no solo una malla.
    public void Rebuild() => _needsRebuild = true;

    void LateUpdate()
    {
        if (!_needsRebuild) return;

        _needsRebuild = false;
        Build();
    }

    void Build()
    {
        _definition = WorldMapDefinition.For(this);
        if (_definition == null) return;

        _decorationRange.Clear(); // por si se editó un Chapter_N.json con el editor abierto
        Adopt();

        // La geometría vive en las islas; este objeto queda solo como contenedor y como fuente
        // del material por defecto.
        GetComponent<MeshFilter>().sharedMesh = null;

        // El ancho sale del recorrido: al cambiar el zigzag el suelo acompaña solo, sin quedar
        // corto ni sobrar.
        float width = _definition.Layout.zigzag * 2f + sideMargin * 2f;

        var all     = _definition.Spans();
        int firstNumber = all.Count > 0 ? all[0].number  : int.MinValue;
        int lastNumber  = all.Count > 0 ? all[^1].number : int.MaxValue;

        var spans = _definition.VisibleSpans();

        for (int i = 0; i < spans.Count; i++)
            BuildIsland(Island(i), spans[i], width,
                        spans[i].number == firstNumber, spans[i].number == lastNumber);

        // Las que sobran se apagan en vez de destruirse: la ventana vuelve a crecer al scrollear
        // hacia el otro lado.
        for (int i = spans.Count; i < _islands.Count; i++)
            if (_islands[i]) _islands[i].gameObject.SetActive(false);
    }

    // Los objetos DontSave sobreviven a una recompilación de scripts, pero la lista no: sin esto,
    // cada recompilación en el editor construiría islas nuevas encima de las que ya estaban.
    void Adopt()
    {
        if (_islands.Count > 0) return;

        foreach (Transform child in transform)
            if (child.name.StartsWith(ISLAND_PREFIX) && child.TryGetComponent<MeshFilter>(out var filter))
                _islands.Add(filter);
    }

    MeshFilter Island(int index)
    {
        while (_islands.Count <= index) _islands.Add(null);

        if (_islands[index] == null)
        {
            var go = new GameObject(ISLAND_PREFIX + index, typeof(MeshFilter), typeof(MeshRenderer));

            // DontSave porque se regenera sola en cada OnEnable: sin esto, cada guardado de la
            // escena dejaría las islas de ese momento adentro del .unity y al abrirla aparecerían
            // duplicadas junto a las recién construidas.
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);

            _islands[index] = go.GetComponent<MeshFilter>();
        }

        _islands[index].gameObject.SetActive(true);
        return _islands[index];
    }

    void BuildIsland(MeshFilter filter, WorldMapDefinition.Span span, float width,
                     bool worldStart, bool worldEnd)
    {
        filter.gameObject.name = $"{ISLAND_PREFIX}{span.number}";

        var vertices = new List<Vector3>();
        var uvs      = new List<Vector2>();
        var indices  = new List<int>();

        AppendIsland(vertices, uvs, indices, span, width, worldStart, worldEnd);

        var mesh = filter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh { name = "WorldMapGround", hideFlags = HideFlags.DontSave };
            filter.sharedMesh = mesh;
        }

        mesh.Clear();

        // Un capítulo largo pasa de 65k vértices enseguida con densidad alta; sin esto Unity lo
        // corta en silencio y el suelo aparece a la mitad.
        mesh.indexFormat = vertices.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();

        // Los bounds los calcula Unity sobre los vértices PLANOS, pero el shader los hunde en Y
        // al dibujar. Sin agrandarlos, el suelo desaparece al mirar hacia el horizonte porque
        // Unity cree que quedó fuera de cuadro.
        var bounds = mesh.bounds;
        bounds.Expand(new Vector3(0f, _definition.Length * _definition.Layout.spacing, 0f));
        mesh.bounds = bounds;

        var renderer = filter.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = MaterialFor(span.number);
        renderer.sortingOrder   = WorldMapDefinition.ORDER_GROUND;
    }

    // El material sale del JSON del capítulo. Sin 'ground' —o con uno que no esté en la lista—
    // cae al de este objeto, que es el que ya está puesto hoy: agregar el campo a un capítulo no
    // obliga a agregárselo a todos.
    Material MaterialFor(int chapter)
    {
        var fallback = GetComponent<MeshRenderer>().sharedMaterial;

        var json = Resources.Load<TextAsset>($"Chapters/Chapter_{chapter}");
        string id = json ? JsonUtility.FromJson<ChapterData>(json.text)?.ground : null;
        if (string.IsNullOrEmpty(id) || materials == null) return fallback;

        foreach (var option in materials)
            if (option != null && option.id == id)
                return option.material != null ? option.material : fallback;

        Debug.LogWarning($"[WorldMapGround] El capítulo {chapter} pide el suelo '{id}' y no está " +
                         "en la lista de materiales — va con el de siempre.", this);
        return fallback;
    }

    void AppendIsland(List<Vector3> vertices, List<Vector2> uvs, List<int> indices,
                      WorldMapDefinition.Span span, float width, bool worldStart, bool worldEnd)
    {
        float spacing = _definition.Layout.spacing;
        float from    = (worldStart ? ContentStart(span) - outerStartMargin : span.start - endMargin) * spacing;
        float to      = (worldEnd   ? ContentEnd(span)   + outerEndMargin   : span.End   + endMargin) * spacing;
        float length  = to - from;

        int cols = Mathf.Max(1, Mathf.RoundToInt(width  * density));
        int rows = Mathf.Max(1, Mathf.RoundToInt(length * density));
        int start = vertices.Count;

        for (int z = 0; z <= rows; z++)
        for (int x = 0; x <= cols; x++)
        {
            float px = (x / (float)cols - 0.5f) * width;
            float pz = from + z / (float)rows * length;

            vertices.Add(new Vector3(px, 0f, pz));

            // La textura se mide en MUNDO y no de 0 a 1 por isla: si no, un capítulo corto y uno
            // largo mostrarían la misma textura estirada distinto.
            uvs.Add(new Vector2(px / tiling, pz / tiling));
        }

        for (int z = 0; z < rows; z++)
        for (int x = 0; x < cols; x++)
        {
            int a = start + z * (cols + 1) + x;
            int b = a + cols + 1;

            indices.Add(a);     indices.Add(b); indices.Add(a + 1);
            indices.Add(a + 1); indices.Add(b); indices.Add(b + 1);
        }
    }
}
