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

    [Tooltip("Cuánto sobra de suelo DESPUÉS de donde termina el camino, en las puntas interiores de cada isla. Se mide contra el camino y no contra el último nodo, así que alargar el camino agranda la isla sola en vez de salirse de ella.")]
    [SerializeField] float endMargin = 12f;

    [Tooltip("Cuánto sobra antes de lo primero que se ve del mundo. Va aparte del margen interior porque ahí no hay isla vecina que separar, solo agua de más.")]
    [SerializeField] float outerStartMargin = 2f;

    [Tooltip("Cuánto sobra después de lo último que se ve del mundo. Separado del de arriba porque no cumplen la misma función: este decide cuánto se alcanza a ver al final, y el otro solo cuánta agua queda antes del primer nodo.")]
    [SerializeField] float outerEndMargin = 2f;

    [Tooltip("Cuántos cortes por unidad de mundo. Más alto es más suave y más caro.")]
    [SerializeField] float density = 0.5f;

    [Tooltip("Cada cuántas unidades de mundo se repite la textura.")]
    [SerializeField] float tiling = 8f;

    [Header("Fosa entre capítulos")]
    [Tooltip("Cuánto se hunde el suelo entre un capítulo y el siguiente. En 0 no se genera fosa y las islas quedan como antes.")]
    [SerializeField] float chasmDepth = 0f;

    [Tooltip("Qué tan abrupta es la caída. En 1 baja y sube parejo; más alto deja las paredes verticales y un fondo plano, que se lee más como una grieta que como un valle.")]
    [Range(1f, 6f)]
    [SerializeField] float chasmSteepness = 2.5f;

    [Tooltip("El color del FONDO de la fosa. Se mezcla con el del borde según lo hondo que esté cada punto, así que la oscuridad entra de forma gradual y no hay ningún corte donde empieza la bajada. Un azul frío y oscuro se lee como agua profunda; un gris pardo, como roca.")]
    [SerializeField] Color chasmShade = new(0.28f, 0.34f, 0.46f, 1f);

    [Tooltip("Material del fondo de la fosa. Vacío usa el mismo del suelo, que suele bastar: lo que vende la profundidad es la sombra de arriba, no la textura. Ponlo solo si quieres roca en vez de arena.")]
    [SerializeField] Material chasmMaterial;

    [Tooltip("Cuánto se ondula el filo entre la isla y la fosa, en unidades de mundo. En 0 el corte es una recta atravesando el mapa, que se lee como cortado a cuchillo en vez de como roca.")]
    [SerializeField] float edgeWave = 0.8f;

    [Tooltip("Cuántas ondulaciones caben a lo ancho. Pocas dan entrantes grandes tipo acantilado; muchas, un filo dentado.")]
    [Range(0.5f, 8f)]
    [SerializeField] float edgeWaveScale = 2.5f;

    [Tooltip("En cuántos escalones se parte el filo. En 0 el borde es una curva continua, que se lee como duna. Con escalones el filo avanza a saltos y queda partido en bloques con caras planas, como roca.")]
    [Range(0, 8)]
    [SerializeField] int edgeFacets = 4;

    [Tooltip("Cuántas veces más cortes a lo ancho que los que pide 'Density'. El detalle del filo no puede ser más fino que sus columnas: con pocas, cualquier forma que se le pida sale redondeada a la fuerza.")]
    [Range(1, 8)]
    [SerializeField] int edgeDetail = 3;

    [Tooltip("Cuánto se sombrean las paredes de la fosa según hacia dónde miran. El shader del mundo es unlit, así que sin esto una pared vertical y el fondo plano salen del MISMO color y la fosa se lee como una mancha oscura en vez de un pozo. Esto le pinta la luz encima, por vértice.")]
    [Range(0f, 1f)]
    [SerializeField] float chasmRelief = 0.75f;

    [Header("Suelo por capítulo")]
    [Tooltip("Qué material le toca a cada 'ground' de los Chapter_N.json. Un capítulo sin 'ground' —o con uno que no esté acá— usa el material de este mismo objeto.")]
    [SerializeField] GroundMaterial[] materials;

    const string ISLAND_PREFIX = "Island ";
    const string CHASM_PREFIX  = "Chasm ";

    WorldMapDefinition  _definition;
    WorldMapPathRibbon  _path;

    // Hasta dónde llega el camino de ese capítulo, en índice de mundo. Sin cinta en la escena se
    // cae al propio nodo, que es lo que había antes de que el margen mirara el camino.
    float PathStart(WorldMapDefinition.Span span)
    {
        if (_path == null) _path = FindAnyObjectByType<WorldMapPathRibbon>();
        return span.start - (_path != null ? _path.LeadIn : 0f);
    }

    float PathEnd(WorldMapDefinition.Span span)
    {
        if (_path == null) _path = FindAnyObjectByType<WorldMapPathRibbon>();
        return span.End + (_path != null ? _path.LeadOut : 0f);
    }

    // Las islas vivas, en el mismo orden que VisibleSpans(). Se reciclan entre reconstrucciones:
    // al cruzar de capítulo cambia CUÁL se dibuja, no cuántas, así que crear y destruir objetos
    // en cada cruce sería basura para nada.
    readonly List<MeshFilter> _islands = new();
    readonly List<MeshFilter> _chasms  = new();

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
    float ContentStart(WorldMapDefinition.Span span) =>
        Mathf.Min(span.start + DecorationRange(span).min, PathStart(span));

    float ContentEnd(WorldMapDefinition.Span span) =>
        Mathf.Max(span.start + DecorationRange(span).max, PathEnd(span));

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

                // Solo el marco y las posiciones crudas pueden salirse del rango de los nodos. Las
                // piezas del cuerpo se reparten sobre los nodos que EXISTEN (ver
                // WorldMapDecorations.PlantChapter), así que su 'node' no dice dónde van a caer:
                // mirarlo estiraría el suelo para cubrir piezas que no se plantan, que es lo que
                // pasaba en un capítulo con menos niveles creados que definiciones.
                if (placement.node > 0 && !placement.start && !placement.end) continue;

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

        BuildChasms(spans, width);
    }

    // La fosa que separa un capítulo del siguiente.
    //
    // Es geometría y no un hueco a secas porque un hueco no se lee como profundidad: por debajo
    // del suelo está el telón del horizonte, agua clara con rayos de sol, y eso dice "lejos", no
    // "hondo". Bajando el terreno de verdad, la caída se ve.
    //
    // Los bordes de la fosa arrancan y terminan en y=0, o sea a la misma altura que las islas
    // vecinas, así que no hay costura entre una cosa y la otra por más que se cambie la
    // profundidad.
    void BuildChasms(List<WorldMapDefinition.Span> spans, float width)
    {
        int built = 0;

        _warnedThisPass = false;

        if (chasmDepth > 0f)
            for (int i = 0; i + 1 < spans.Count; i++)
            {
                float spacing = _definition.Layout.spacing;
                float from    = (PathEnd(spans[i])       + endMargin) * spacing;
                float to      = (PathStart(spans[i + 1]) - endMargin) * spacing;

                // Con el margen interior grande, las islas se solapan y no hay sitio donde cavar.
                //
                // Callarse acá deja el mapa como si la fosa nunca se hubiera pedido: sin hueco,
                // sin error y sin ninguna pista de qué número lo impide. Como el margen se mide
                // contra la punta del camino, alargar el camino también consume la separación,
                // y eso no se adivina mirando el Inspector.
                if (to - from < 0.01f)
                {
                    float over = (from - to) / spacing;

                    Warn($"La fosa entre el capítulo {spans[i].number} y el {spans[i + 1].number} " +
                         $"no cabe: las islas se solapan {over:0.##} niveles. Sube 'Chapter Gap' a " +
                         $"{_definition.ChapterGap + over + 0.5f:0.##} en WorldMapDefinition, o baja " +
                         "'End Margin' acá o 'Lead In'/'Lead Out' en WorldMapPathRibbon.");
                    continue;
                }

                BuildChasm(Chasm(built++), from, to, width);
            }

        for (int i = built; i < _chasms.Count; i++)
            if (_chasms[i]) _chasms[i].gameObject.SetActive(false);

        // Al quedar arreglado se olvida el aviso, para que vuelva a salir si se rompe otra vez
        // con los mismos números.
        if (!_warnedThisPass) _lastWarning = null;
    }

    // El mapa se reconstruye a cada cambio del Inspector, así que el mismo aviso saldría decenas
    // de veces seguidas y enterraría la consola. Solo se imprime cuando cambia.
    string _lastWarning;
    bool   _warnedThisPass;

    void Warn(string message)
    {
        _warnedThisPass = true;

        if (message == _lastWarning) return;

        _lastWarning = message;
        Debug.LogWarning(message, this);
    }

    void BuildChasm(MeshFilter filter, float from, float to, float width)
    {
        var vertices = new List<Vector3>();
        var uvs      = new List<Vector2>();
        var colors   = new List<Color>();
        var indices  = new List<int>();

        float length = to - from;
        int   cols   = Columns(width);

        // La caída pide cortes aunque la fosa sea angosta: lo que hay que describir no es el
        // largo sino la bajada, así que la profundidad cuenta igual que el largo.
        int   rows   = Mathf.Max(8, Mathf.RoundToInt((length + chasmDepth) * density * 2f));

        // El perfil se calcula una vez por fila, antes de generar los vértices: hace falta la
        // fila anterior para ir sumando la distancia recorrida SOBRE la superficie.
        var profile = new float[rows + 1];   // altura de cada fila

        for (int z = 0; z <= rows; z++)
        {
            float t = z / (float)rows;

            // Seno elevado: en 1 es un valle suave y a más exponente las paredes se enderezan y
            // el fondo se aplana, que es lo que lo convierte en grieta.
            //
            // El Max no sobra: Sin(PI) no da cero exacto sino -8,7e-8 por redondeo, y elevar un
            // negativo a una fracción da NaN. Un solo vértice con NaN envenena los bounds de la
            // malla entera y Unity la descarta por "demasiado grande o lejos del origen".
            profile[z] = -chasmDepth * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 1f / chasmSteepness);
        }

        // Los filos ondulan, así que cada columna cruza la fosa en un tramo de largo distinto y
        // recorre su pendiente en distinta medida. Por eso el avance de la textura se acumula
        // POR COLUMNA, en una pasada aparte: los vértices se generan por filas y ahí ya no se
        // tiene a mano el de la columna anterior.
        var edgeFrom = new float[cols + 1];
        var edgeTo   = new float[cols + 1];
        var walked   = new float[cols + 1][];

        for (int x = 0; x <= cols; x++)
        {
            float px = (x / (float)cols - 0.5f) * width;

            edgeFrom[x] = from + EdgeOffset(px, from);
            edgeTo[x]   = to   + EdgeOffset(px, to);
            walked[x]   = new float[rows + 1];

            float dz = (edgeTo[x] - edgeFrom[x]) / rows;

            for (int z = 1; z <= rows; z++)
            {
                float dy = profile[z] - profile[z - 1];
                walked[x][z] = walked[x][z - 1] + Mathf.Sqrt(dz * dz + dy * dy);
            }
        }

        for (int z = 0; z <= rows; z++)
        for (int x = 0; x <= cols; x++)
        {
            float px = (x / (float)cols - 0.5f) * width;
            float pz = Mathf.Lerp(edgeFrom[x], edgeTo[x], z / (float)rows);

            vertices.Add(new Vector3(px, profile[z], pz));

            // La textura se mide por lo que se RECORRE sobre la pendiente, no por lo que se avanza
            // en Z. Una pared casi vertical baja mucho y avanza poco, así que midiendo en Z le
            // tocaba un trozo minúsculo de textura estirado sobre una superficie grande.
            uvs.Add(new Vector2(px / tiling, (edgeFrom[x] + walked[x][z]) / tiling));

            // La sombra va POR VÉRTICE y según la profundidad de ese punto: en el borde queda en
            // blanco —o sea, igual que la isla vecina— y se va al color del fondo a medida que
            // baja. Con un material oscuro plano habría un parche con un corte duro justo donde
            // empieza la bajada, y eso se lee como una mancha, no como un pozo.
            var tint = Color.Lerp(Color.white, chasmShade, chasmDepth > 0f ? -profile[z] / chasmDepth : 0f);

            // Y encima la luz, también por vértice. La pared que baja alejándose queda de espaldas
            // y se apaga; la de enfrente, que sube hacia la cámara, se ilumina. Ese contraste
            // entre las dos caras es lo que hace ver un pozo — el degradado de profundidad solo,
            // sin él, es una mancha.
            float lit = Relief(x, z, profile, edgeFrom, edgeTo, rows);

            colors.Add(new Color(tint.r * lit, tint.g * lit, tint.b * lit, tint.a));
        }

        for (int z = 0; z < rows; z++)
        for (int x = 0; x < cols; x++)
        {
            int a = z * (cols + 1) + x;
            int b = a + cols + 1;

            indices.Add(a);     indices.Add(b); indices.Add(a + 1);
            indices.Add(a + 1); indices.Add(b); indices.Add(b + 1);
        }

        var mesh = filter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh { name = "WorldMapChasm", hideFlags = HideFlags.DontSave };
            filter.sharedMesh = mesh;
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();

        // Igual que las islas: el shader hunde los vértices al dibujar y Unity descarta por los
        // bounds SIN curvar, así que hay que agrandarlos o la fosa desaparece mirando al horizonte.
        var bounds = mesh.bounds;
        bounds.Expand(new Vector3(0f, _definition.Length * _definition.Layout.spacing, 0f));
        mesh.bounds = bounds;

        var renderer = filter.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = chasmMaterial != null ? chasmMaterial : GetComponent<MeshRenderer>().sharedMaterial;
        renderer.sortingOrder   = WorldMapDefinition.ORDER_GROUND;
    }

    // Los objetos DontSave sobreviven a una recompilación de scripts, pero la lista no: sin esto,
    // cada recompilación en el editor construiría islas nuevas encima de las que ya estaban.
    void Adopt()
    {
        if (_islands.Count > 0 || _chasms.Count > 0) return;

        foreach (Transform child in transform)
        {
            if (!child.TryGetComponent<MeshFilter>(out var filter)) continue;

            if (child.name.StartsWith(ISLAND_PREFIX)) _islands.Add(filter);
            else if (child.name.StartsWith(CHASM_PREFIX)) _chasms.Add(filter);
        }
    }

    MeshFilter Chasm(int index) => Child(_chasms, CHASM_PREFIX, index);
    MeshFilter Island(int index)  => Child(_islands, ISLAND_PREFIX, index);

    MeshFilter Child(List<MeshFilter> pool, string prefix, int index)
    {
        while (pool.Count <= index) pool.Add(null);

        if (pool[index] == null)
        {
            var go = new GameObject(prefix + index, typeof(MeshFilter), typeof(MeshRenderer));

            // DontSave porque se regenera sola en cada OnEnable: sin esto, cada guardado de la
            // escena dejaría las piezas de ese momento adentro del .unity y al abrirla aparecerían
            // duplicadas junto a las recién construidas.
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);

            pool[index] = go.GetComponent<MeshFilter>();
        }

        pool[index].gameObject.SetActive(true);
        return pool[index];
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

    // Cuánto se adelanta o se atrasa el filo en esa columna.
    //
    // La misma función la usan la isla y la fosa que tiene enfrente, con la misma semilla —la Z
    // del filo—, así que los dos bordes ondulan idéntico y encajan sin hueco ni solape por más
    // que se cambie la amplitud.
    //
    // La luz falsa que se hornea en el color de los vértices: desde arriba y algo inclinada hacia
    // la cámara. Dividida por su propia componente vertical, una superficie llana da exactamente
    // 1, o sea que el fondo de la fosa sale del mismo tono que la isla y solo cambian las paredes.
    static readonly Vector3 LIGHT = new(0f, 0.6f, -0.8f);

    // La pared de enfrente puede pasarse de 1 y quedar MÁS clara que el suelo llano. Es a
    // propósito: es la cara que mira a la cámara y recibe la luz de lleno, y ese brillo contra
    // la pared de acá, que está a oscuras, es lo que separa las dos caras del pozo.
    const float MAX_LIT = 1.35f;

    float Relief(int x, int z, float[] profile, float[] edgeFrom, float[] edgeTo, int rows)
    {
        if (chasmRelief <= 0f) return 1f;

        int   a  = Mathf.Max(0,    z - 1);
        int   b  = Mathf.Min(rows, z + 1);
        float dy = profile[b] - profile[a];
        float dz = (edgeTo[x] - edgeFrom[x]) / rows * (b - a);

        // La normal de una pendiente y = f(z) es (0, 1, -f'), y f' es dy/dz. Sin dividir queda
        // (0, dz, -dy), que apunta igual; normalizar se encarga del resto.
        var n = new Vector3(0f, dz, -dy).normalized;

        float lit = Mathf.Clamp(Vector3.Dot(n, LIGHT) / LIGHT.y, 0f, MAX_LIT);

        return Mathf.Lerp(1f, lit, chasmRelief);
    }

    // Ruido de valor en vez de senos: sumar dos senos sigue teniendo un ritmo, y un ritmo se ve
    // como adorno. El ruido no repite, así que cada entrante sale de un tamaño distinto.
    //
    // Y encima escalones: una curva continua, por irregular que sea, se lee como arena movida.
    // La roca se reconoce por las CARAS PLANAS y los saltos entre una y otra, y eso es lo que
    // hace redondear la amplitud a unos pocos valores.
    float EdgeOffset(float px, float seed)
    {
        if (edgeWave <= 0f) return 0f;

        float s = seed * 0.613f;
        float x = px * edgeWaveScale * 0.35f;

        // Tres octavas: la primera pone los entrantes grandes y las otras le muerden el borde.
        float n = (Noise(x,          s)
                + (Noise(x * 2.17f,  s + 31.7f) - 0.5f) * 0.5f
                + (Noise(x * 4.63f,  s + 71.3f) - 0.5f) * 0.25f) - 0.5f;

        n /= 0.875f;   // las tres octavas juntas no llegan a ±0,5; esto devuelve el rango entero

        if (edgeFacets > 0) n = Mathf.Round(n * edgeFacets) / edgeFacets;

        return n * 2f * edgeWave;
    }

    // Ruido de valor de una dimensión, entre 0 y 1. Determinista: el mismo px da el mismo valor
    // siempre, que es lo que permite que la isla y la fosa de enfrente calculen su filo por
    // separado y encajen igual.
    static float Noise(float x, float seed)
    {
        float i = Mathf.Floor(x);
        float f = x - i;

        f = f * f * (3f - 2f * f);   // suaviza el paso de un entero al siguiente

        return Mathf.Lerp(Hash(i, seed), Hash(i + 1f, seed), f);
    }

    static float Hash(float i, float seed)
    {
        float v = Mathf.Sin(i * 12.9898f + seed * 78.233f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    // Las columnas las comparten la isla y la fosa: si no cayeran en los mismos px, sus filos se
    // calcularían en sitios distintos y quedaría un hueco entre una cosa y la otra.
    int Columns(float width) => Mathf.Max(1, Mathf.RoundToInt(width * density * edgeDetail));

    void AppendIsland(List<Vector3> vertices, List<Vector2> uvs, List<int> indices,
                      WorldMapDefinition.Span span, float width, bool worldStart, bool worldEnd)
    {
        float spacing = _definition.Layout.spacing;
        float from    = (worldStart ? ContentStart(span) - outerStartMargin : PathStart(span) - endMargin) * spacing;
        float to      = (worldEnd   ? ContentEnd(span)   + outerEndMargin   : PathEnd(span)   + endMargin) * spacing;
        float length  = to - from;

        int cols = Columns(width);
        int rows = Mathf.Max(1, Mathf.RoundToInt(length * density));
        int start = vertices.Count;

        for (int z = 0; z <= rows; z++)
        for (int x = 0; x <= cols; x++)
        {
            float px = (x / (float)cols - 0.5f) * width;

            // Solo ondulan los filos INTERIORES, los que dan a una fosa. Los extremos del mundo se
            // dejan rectos: ahí no hay nada enfrente con lo que encajar y la ondulación solo se
            // vería como un borde mal cortado.
            float a  = worldStart ? from : from + EdgeOffset(px, from);
            float b  = worldEnd   ? to   : to   + EdgeOffset(px, to);
            float pz = Mathf.Lerp(a, b, z / (float)rows);

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
