using System.Collections.Generic;
using UnityEngine;

// Construye el mundo a los costados del camino: lee el JSON de cada capítulo
// (Resources/Chapters/Chapter_N.json) y planta ahí las decoraciones que indique.
//
// Las posiciones vienen ancladas a los niveles, no a la esfera, y se resuelven con la MISMA curva
// que dibuja el camino (WorldSpherePath). Por eso una decoración no puede quedar desalineada del
// camino aunque después se cambie la separación de nodos o el zigzag.
//
// Los sprites se orientan con el mismo criterio que la card del jugador: siempre de frente a la
// cámara y siempre derechos. Así se leen como recortes de pie desde cualquier punto del scroll y
// no se acuestan ni se tuercen al acercarse al horizonte.
public class WorldSphereDecorationPositioner : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("De acá sale el recorrido. Lo llama él mismo al terminar de armar los nodos.")]
    [SerializeField] WorldSphereNodePositioner positioner;
    [Tooltip("La esfera del mundo — las decoraciones cuelgan de ahí para girar con él.")]
    [SerializeField] Transform sphere;
    [Tooltip("Traduce los ids del JSON a sprites. Sin esto no se planta nada.")]
    [SerializeField] DecorationCatalog catalog;
    [Tooltip("Si se deja vacío usa Camera.main.")]
    [SerializeField] Camera worldCamera;

    [Header("Ajustes")]
    [Tooltip("Cuánto se hunden en el suelo, en unidades de MUNDO — un pelín evita que se vean flotando por el facetado del terreno.")]
    [SerializeField] float sink = 0.05f;
    [Tooltip("Cuánto se adelantan hacia la cámara respecto de donde se apoyan, en unidades de MUNDO. Es lo que evita que el suelo y el camino las recorten: un sprite parado es un plano vertical, y el terreno que curva hacia el horizonte queda por delante de su base.")]
    [SerializeField] float cameraOffset = 0.6f;
    [Tooltip("A cuántos grados del frente se apaga una decoración. Las que quedan detrás del horizonte no se ven pero igual se dibujan. 0 = no apagar ninguna.")]
    [SerializeField] float cullRange = 55f;

    [Header("Sombra de contacto")]
    [Tooltip("Mancha difusa apoyada en el suelo debajo de cada decoración. Es lo que la ancla al piso: sin esto, un sprite parado contra un fondo plano se lee como flotando.")]
    [SerializeField] bool  contactShadow = true;
    [SerializeField] Color shadowColor   = new Color(0f, 0.06f, 0.16f, 0.4f);
    [Tooltip("Ancho por defecto de la sombra, como fracción del alto de la decoración. El catálogo puede pisarlo pieza por pieza.")]
    [SerializeField] float shadowScale   = 0.8f;

    Camera     _cam;
    Material   _shadowMaterial;
    GameObject _shadowObject;
    Mesh       _shadowMesh;

    // Se guarda el punto de apoyo aparte de la posición final: la decoración se dibuja adelantada
    // hacia la cámara, pero su lugar en el mundo sigue siendo el de la superficie.
    struct Planted
    {
        public Transform        transform;
        public Vector3          anchor;    // en espacio local de la esfera
        public SpriteRenderer[] renderers;  // cacheados: buscarlos cada frame sería tirar rendimiento
        public int[]            baseOrders; // el 'Order in Layer' que traía cada pieza del prefab
        public string           id;          // para devolverla al depósito que le corresponde
        public Vector3          direction;   // dirección sobre la esfera, para rearmar su sombra
        public float            shadowWidth; // ancho ya resuelto, del catálogo o del alto
    }

    // Cuántos niveles de más se pueblan a cada lado de la ventana visible.
    const int WINDOW_MARGIN = 4;

    // Las decoraciones vivas, por capítulo y posición dentro de su JSON.
    readonly Dictionary<(int chapter, int placement), Planted> _active =
        new Dictionary<(int, int), Planted>();

    // Depósito por tipo de pieza: al salir de la ventana no se destruyen, se guardan apagadas para
    // reusarlas. Destruir e instanciar en cada paso del scroll generaría basura constante.
    readonly Dictionary<string, Stack<Transform>> _idle = new Dictionary<string, Stack<Transform>>();

    // Qué piezas están puestas ahora mismo. Sin esto, si una llegara a entrar dos veces al
    // depósito, dos decoraciones distintas se quedarían con el MISMO objeto y una de las dos
    // aparecería encima de la otra.
    readonly HashSet<Transform> _inUse = new HashSet<Transform>();

    // Alto de cada tipo de pieza, medido UNA vez sobre una recién creada y guardado.
    // No se puede medir en cada reuso: renderer.bounds está en espacio de mundo, así que incluye
    // la escala que la pieza ya tiene y la de la esfera de la que cuelga. Al dividir por esa
    // medida ya encogida, la escala se dispara — y cada reuso lo multiplica de nuevo.
    readonly Dictionary<string, float> _sizeCache = new Dictionary<string, float>();

    // El 'Order in Layer' con el que vino cada pieza del prefab, guardado la PRIMERA vez que se
    // crea. No se puede releer al reusarla: para entonces ya fue sobrescrito por el orden por
    // profundidad de la vuelta anterior, y el número se iría desviando en cada reuso.
    readonly Dictionary<Transform, (SpriteRenderer[] renderers, int[] baseOrders)> _cached =
        new Dictionary<Transform, (SpriteRenderer[], int[])>();

    (SpriteRenderer[] renderers, int[] baseOrders) RenderersOf(Transform decoration)
    {
        if (_cached.TryGetValue(decoration, out var cached)) return cached;

        var renderers  = decoration.GetComponentsInChildren<SpriteRenderer>(true);
        var baseOrders = new int[renderers.Length];
        for (int r = 0; r < renderers.Length; r++) baseOrders[r] = renderers[r].sortingOrder;

        cached = (renderers, baseOrders);
        _cached[decoration] = cached;
        return cached;
    }

    static readonly List<(int, int)> _expired = new List<(int, int)>();

    void OnEnable()
    {
        if (positioner) positioner.OnWindowChanged += Build;
    }

    void OnDisable()
    {
        if (positioner) positioner.OnWindowChanged -= Build;
    }

    // Guarda las que quedaron fuera de la ventana. El 'at' de una decoración está en niveles, así
    // que se compara contra el mismo rango que usan los nodos.
    void ReleaseOutside(int windowFirst, int windowLast)
    {
        var chapters = positioner.NodeChapters;
        _expired.Clear();

        foreach (var pair in _active)
        {
            var data = LoadChapter(pair.Key.chapter);
            if (data?.decorations == null || pair.Key.placement >= data.decorations.Length) { _expired.Add(pair.Key); continue; }

            // El 'at' es relativo al primer nivel del capítulo: hay que llevarlo a índice global.
            int start = 0;
            while (start < chapters.Count && chapters[start] != pair.Key.chapter) start++;
            float global = start + data.decorations[pair.Key.placement].at;

            // Un poco más tolerante que el alta, para que una decoración en el borde no entre y
            // salga en frames alternos.
            if (global < windowFirst - WINDOW_MARGIN - 1f || global > windowLast + WINDOW_MARGIN + 1f)
                _expired.Add(pair.Key);
        }

        foreach (var key in _expired)
        {
            var planted = _active[key];
            _active.Remove(key);
            if (!planted.transform) continue;

            planted.transform.gameObject.SetActive(false);
            if (!_inUse.Remove(planted.transform)) continue; // ya estaba guardada, no duplicarla

            if (!_idle.TryGetValue(planted.id, out var stack))
            {
                stack = new Stack<Transform>();
                _idle[planted.id] = stack;
            }
            stack.Push(planted.transform);
        }
    }

    // Lo llama WorldSphereNodePositioner cuando ya tiene el recorrido listo — no se puede hacer en
    // Start() propio porque Unity no garantiza el orden entre los dos.
    public void Build()
    {
        if (!positioner || !sphere)
        {
            Debug.LogWarning("[WorldSphereDecorationPositioner] Faltan 'Positioner' o 'Sphere' en el Inspector — no se plantan decoraciones.", this);
            return;
        }
        if (!catalog)
        {
            Debug.LogWarning("[WorldSphereDecorationPositioner] Falta asignar 'Catalog' en el Inspector — no se plantan decoraciones.", this);
            return;
        }

        _cam = worldCamera ? worldCamera : Camera.main;

        var chapters = positioner.NodeChapters;
        int count    = positioner.LevelCount;
        if (count == 0 || chapters == null) return;

        // Solo el tramo en pantalla, más margen. Sin esto, con capítulos largos habría cientos de
        // decoraciones instanciadas para mostrar una docena, y además darían la vuelta a la esfera.
        int windowFirst = Mathf.Max(0, positioner.WindowFirst - WINDOW_MARGIN);
        int windowLast  = Mathf.Min(count - 1, positioner.WindowLast + WINDOW_MARGIN);

        float radiusWorld = positioner.SurfaceRadiusLocal * positioner.SphereScale;
        float anglePerNode = positioner.AngleSpacing;

        // Un tramo por capítulo. El 'at' del JSON es relativo al PRIMER nivel de su capítulo, así
        // que cada capítulo se resuelve con su propio sub-recorrido.
        var shadowVerts = new List<Vector3>();
        var shadowUvs   = new List<Vector2>();
        var shadowNorms = new List<Vector3>();
        var shadowTris  = new List<int>();

        // Las que estaban y ya no entran en la ventana vuelven al depósito, para no instanciar y
        // destruir en cada paso del scroll.
        ReleaseOutside(windowFirst, windowLast);

        int from = windowFirst;
        for (int i = windowFirst + 1; i <= windowLast + 1; i++)
        {
            bool isBreak = i > windowLast || chapters[i] != chapters[from];
            if (!isBreak) continue;

            BuildChapter(chapters[from], from, i - 1, count, chapters, radiusWorld, anglePerNode,
                         shadowVerts, shadowUvs, shadowNorms, shadowTris);
            from = i;
        }

        // Las sombras se rearman con TODAS las activas, no solo con las que acaban de entrar: son
        // una sola malla, así que si se generara incremental le faltarían las de antes.
        if (contactShadow)
        {
            shadowVerts.Clear(); shadowUvs.Clear(); shadowNorms.Clear(); shadowTris.Clear();
            foreach (var planted in _active.Values)
                AppendShadow(planted, shadowVerts, shadowUvs, shadowNorms, shadowTris);
        }
        BuildShadowMesh(shadowVerts, shadowUvs, shadowNorms, shadowTris);
    }

    // Nodos a cada lado del punto: los que Catmull-Rom necesita para tener pendiente de entrada y
    // de salida.
    const int SPLINE_SPAN = 2;

    // Posición de un 'at' sobre el camino de SU capítulo, con los mismos nodos siempre.
    //
    // Antes esto se resolvía con los nodos de la ventana visible. Mientras la decoración estaba
    // bien adentro daba igual, pero en el borde el camino se extrapola en línea recta, y una
    // decoración plantada en ese momento quedaba corrida — normalmente encima del primer nodo del
    // capítulo, que es justo cuando el tramo recién asoma. Al seguir scrolleando y volver, la
    // misma decoración se recreaba con la ventana centrada y aparecía en su sitio: de ahí que se
    // "acomodaran solas".
    //
    // Tomando los vecinos por índice de capítulo, la posición ya no depende de cuánto scroll
    // había cuando se plantó.
    Vector3 SampleAlongChapter(float at, int chapterStart, int chapterEnd,
                               float anglePerNode, float offsetWorld, float radiusWorld)
    {
        int anchor = chapterStart + Mathf.FloorToInt(at);
        int from   = Mathf.Clamp(anchor - SPLINE_SPAN,     chapterStart, chapterEnd);
        int to     = Mathf.Clamp(anchor + SPLINE_SPAN + 1, chapterStart, chapterEnd);

        var dirs = new Vector3[to - from + 1];
        for (int j = 0; j < dirs.Length; j++)
            dirs[j] = (positioner.RestRotation(from + j) * Vector3.back).normalized;

        // 'at' es relativo al primer nivel del capítulo; el muestreo, al primero de este tramo.
        return WorldSpherePath.SampleOffset(dirs, at - (from - chapterStart),
                                            anglePerNode, offsetWorld, radiusWorld);
    }

    void BuildChapter(int chapter, int first, int last, int count, IReadOnlyList<int> chapters,
                      float radiusWorld, float anglePerNode,
                      List<Vector3> shadowVerts, List<Vector2> shadowUvs,
                      List<Vector3> shadowNorms, List<int> shadowTris)
    {
        var data = LoadChapter(chapter);
        if (data?.decorations == null || data.decorations.Length == 0) return;

        // El 'at' del JSON es relativo al PRIMER nivel del capítulo, no al primero de la ventana:
        // hay que restar ese desplazamiento para ubicarlo dentro del tramo que se está armando.
        int chapterStart = first;
        while (chapterStart > 0 && chapters[chapterStart - 1] == chapter) chapterStart--;
        float shift = first - chapterStart;

        // Fuera del tramo que se está calculando, la curva se extrapola en línea RECTA, sin el
        // zigzag. Eso solo es correcto en los extremos reales del capítulo, donde el camino
        // también sale recto. En el borde de la ventana, en cambio, la extrapolación se desvía
        // del camino y la decoración termina cayendo sobre él.
        bool atChapterStart = first == chapterStart;
        bool atChapterEnd   = last == count - 1 || chapters[last + 1] != chapter;

        float lowBound  = atChapterStart ? -WINDOW_MARGIN : -0.5f;
        float highBound = (last - first) + (atChapterEnd ? WINDOW_MARGIN : 0.5f);

        // Dónde termina el capítulo de verdad, no dónde termina la ventana. La posición de una
        // decoración se calcula con los nodos de su capítulo, nunca con los que haya en pantalla.
        int chapterEnd = last;
        while (chapterEnd + 1 < count && chapters[chapterEnd + 1] == chapter) chapterEnd++;

        // Semilla fija por capítulo: la variación de tamaño sale igual en cada partida. Un mundo
        // que cambia de forma cada vez que se abre el mapa se siente roto.
        var random = new System.Random(chapter * 7919);

        // Las medidas en unidades de MUNDO se reescalan con el tamaño del planeta — ver
        // 'Reference Radius' en el posicionador de nodos.
        float w           = positioner.WorldScale;
        float radiusLocal = positioner.SurfaceRadiusLocal - sink * w / Mathf.Max(positioner.SphereScale, 0.0001f);

        for (int p = 0; p < data.decorations.Length; p++)
        {
            var placement = data.decorations[p];

            // Fuera del tramo que se está armando, o ya puesta desde antes.
            //
            // El margen tiene que ser amplio a los dos lados: un capítulo puede tener decoraciones
            // con 'at' negativo, que adornan la entrada antes del primer nivel, y la ventana nunca
            // arranca por debajo de cero. Con un margen chico esas nunca llegaban a crearse.
            float local = placement.at - shift;
            if (local < lowBound || local > highBound) continue;

            var key = (chapter, p);
            if (_active.ContainsKey(key)) continue;

            var entry = catalog.Find(placement.id);
            if (entry == null)
            {
                Debug.LogWarning($"[WorldSphereDecorationPositioner] El capítulo {chapter} pide '{placement.id}', que no está en el catálogo — se saltea.", this);
                continue;
            }

            float signedOffset = (placement.side == "right" ? -placement.offset : placement.offset) * w;
            Vector3 direction  = SampleAlongChapter(placement.at, chapterStart, chapterEnd,
                                                    anglePerNode, signedOffset, radiusWorld);

            var decoration = Spawn(entry, placement, random);
            if (!decoration) continue;

            Vector3 anchor = direction * radiusLocal;
            decoration.SetParent(sphere, false);
            decoration.localPosition = anchor;
            var (renderers, baseOrders) = RenderersOf(decoration);

            // Orden de dibujo FIJO, calculado desde el ángulo sobre la esfera y no desde la
            // distancia a la cámara. Con la distancia, dos piezas a profundidad parecida se cruzan
            // al scrollear y el orden se invierte de golpe — eso es el salto que se ve. El avance
            // por el camino, en cambio, no cambia nunca.
            //
            // Se usa el ángulo y no el 'at' porque entre capítulos hay un hueco que el índice de
            // nivel no refleja; es la misma cuenta que usan los nodos, así que unos y otros se
            // ordenan entre sí correctamente.
            float angle = positioner.RestAngleAt(chapterStart) + placement.at * anglePerNode;
            int   order = WorldSphereNodePositioner.DrawOrderAt(angle);
            for (int r = 0; r < renderers.Length; r++)
                if (renderers[r]) renderers[r].sortingOrder = order + baseOrders[r];

            _active[key] = new Planted
            {
                transform  = decoration,
                anchor     = anchor,
                renderers  = renderers,
                baseOrders = baseOrders,
                id         = placement.id,
                direction  = direction,
                shadowWidth = (entry.shadowWidth > 0.0001f
                    ? entry.shadowWidth
                    : entry.height * placement.scale * shadowScale) * w,
            };

        }
    }

    Transform Spawn(DecorationCatalog.Entry entry, DecorationPlacement placement, System.Random random)
    {
        Transform tr = null;

        // Primero el depósito: si hay una pieza de este tipo apagada, se reusa en vez de crear otra.
        if (_idle.TryGetValue(placement.id, out var stack))
        {
            while (stack.Count > 0 && !tr)
            {
                var candidate = stack.Pop();
                if (candidate && !_inUse.Contains(candidate)) tr = candidate;
            }
        }

        if (!tr)
        {
            GameObject go;

            if (entry.prefab)
            {
                // Prefab + sprite es la combinación útil: un único prefab con la animación adentro,
                // reusado por todas las plantas, y cada entrada aportando solo su imagen.
                go = Instantiate(entry.prefab);
                if (entry.sprite)
                {
                    var target = go.GetComponentInChildren<SpriteRenderer>(true);
                    if (target) target.sprite = entry.sprite;
                    else Debug.LogWarning($"[WorldSphereDecorationPositioner] El prefab de '{entry.id}' no tiene ningún SpriteRenderer donde poner el sprite.", this);
                }
            }
            else if (entry.sprite)
            {
                go = new GameObject(placement.id);
                go.AddComponent<SpriteRenderer>().sprite = entry.sprite;
            }
            else
            {
                Debug.LogWarning($"[WorldSphereDecorationPositioner] La entrada '{entry.id}' del catálogo no tiene ni sprite ni prefab.", this);
                return null;
            }

            // Se mide acá, con la pieza recién instanciada: todavía está en escala 1 y sin padre,
            // que es la única situación en la que renderer.bounds coincide con su tamaño real.
            if (!_sizeCache.ContainsKey(placement.id))
                _sizeCache[placement.id] = SpriteHeight(go);

            tr = go.transform;
        }

        // Todo lo que sigue corre igual para una pieza nueva que para una reusada. Antes el reuso
        // salía antes de acá y se quedaba con el espejado y el tamaño de quien la usó la vez
        // anterior.
        _inUse.Add(tr);
        tr.gameObject.SetActive(true);
        tr.name = $"Deco_{placement.id}";

        var (renderers, _) = RenderersOf(tr);
        foreach (var renderer in renderers)
            if (renderer) renderer.flipX = placement.flip;

        // El tamaño se pide en unidades de MUNDO, pero el sprite mide lo que mide según sus pixels
        // por unidad — hay que convertir. Y como cuelga de la esfera, que tiene una escala enorme,
        // encima hay que dividir por ella.
        float variation  = 1f + ((float)random.NextDouble() * 2f - 1f) * entry.sizeVariation;
        float worldSize  = entry.height * placement.scale * variation * positioner.WorldScale;
        float spriteSize = _sizeCache.TryGetValue(placement.id, out var cachedSize) ? cachedSize : 1f;
        tr.localScale = Vector3.one * (worldSize / Mathf.Max(spriteSize, 0.0001f) / Mathf.Max(positioner.SphereScale, 0.0001f));

        return tr;
    }

    // Alto de la pieza tal como viene del prefab, antes de escalarla. En un grupo es el de TODA la
    // composición: si se midiera solo el primer SpriteRenderer que aparece, el 'Height' del catálogo
    // acabaría refiriéndose al alto de la roca o al de la planta según cuál viniera primero en la
    // jerarquía, que es un detalle invisible desde el JSON.
    // Las sombras se acumulan en UNA sola malla en vez de un objeto por decoración. No se mueven
    // nunca respecto de la esfera, así que no hay razón para que sean objetos separados: así son
    // un solo draw call en vez de uno por pieza.
    //
    // Además evita GameObject.CreatePrimitive, que siempre intenta agregar un MeshCollider — y en
    // un build para dispositivo esa clase la elimina el code stripping, lo que tira un error por
    // cada sombra.
    void AppendShadow(Planted planted,
                      List<Vector3> verts, List<Vector2> uvs, List<Vector3> norms, List<int> tris)
    {
        Vector3 direction = planted.direction;
        float   width     = planted.shadowWidth;

        float sphereScale = Mathf.Max(positioner.SphereScale, 0.0001f);
        float half        = width * 0.5f / sphereScale;

        // Apenas por encima de la superficie, lo justo para no pelear en profundidad con el suelo.
        Vector3 center = direction * (positioner.SurfaceRadiusLocal + 0.02f * positioner.WorldScale / sphereScale);

        // Marco plano apoyado en la superficie. La sombra es chica comparada con la esfera, así que
        // un cuadrado plano alcanza: no hace falta curvarlo.
        Quaternion rot = Quaternion.LookRotation(-direction, Vector3.up);
        Vector3 right  = rot * Vector3.right * half;
        Vector3 up     = rot * Vector3.up    * half;

        int baseIndex = verts.Count;

        verts.Add(center - right - up);
        verts.Add(center + right - up);
        verts.Add(center - right + up);
        verts.Add(center + right + up);

        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(0f, 1f));
        uvs.Add(new Vector2(1f, 1f));

        for (int i = 0; i < 4; i++) norms.Add(direction);

        // El material tiene el descarte de caras apagado, así que el sentido del triángulo da igual.
        tris.Add(baseIndex);     tris.Add(baseIndex + 2); tris.Add(baseIndex + 1);
        tris.Add(baseIndex + 2); tris.Add(baseIndex + 3); tris.Add(baseIndex + 1);
    }

    void BuildShadowMesh(List<Vector3> verts, List<Vector2> uvs, List<Vector3> norms, List<int> tris)
    {
        if (verts.Count == 0) return;

        var material = ShadowMaterial();
        if (!material) return;

        if (!_shadowMesh) _shadowMesh = new Mesh { name = "DecorationShadows", hideFlags = HideFlags.DontSave };
        var mesh = _shadowMesh;
        mesh.Clear();
        mesh.indexFormat = verts.Count > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        // Un solo objeto reusado: esto se rearma en cada corrimiento de la ventana, y crear uno
        // nuevo cada vez iría dejando objetos muertos colgando de la esfera.
        if (!_shadowObject)
        {
            _shadowObject = new GameObject("ContactShadows");
            _shadowObject.transform.SetParent(sphere, false);
            _shadowObject.AddComponent<MeshFilter>();

            var renderer = _shadowObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial    = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows    = false;
        }

        _shadowObject.GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    // Un único material y una única textura para todas las sombras — se generan la primera vez y
    // se reusan, así cincuenta decoraciones no son cincuenta materiales.
    Material ShadowMaterial()
    {
        if (_shadowMaterial) return _shadowMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader)
        {
            Debug.LogWarning("[WorldSphereDecorationPositioner] No se encontró el shader Unlit de URP — sin sombras de contacto.", this);
            return null;
        }

        _shadowMaterial = new Material(shader) { name = "DecorationShadow" };
        // Configuración de "Surface Type: Transparent" a mano, que es lo que haría el Inspector.
        _shadowMaterial.SetFloat("_Surface",  1f);
        _shadowMaterial.SetFloat("_Blend",    0f);
        _shadowMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _shadowMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _shadowMaterial.SetFloat("_ZWrite",   0f);
        _shadowMaterial.SetFloat("_Cull",     (float)UnityEngine.Rendering.CullMode.Off);
        _shadowMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        // Antes que el resto de los transparentes: como todas las sombras son una sola malla,
        // Unity las ordenaría por un único centro y taparían las decoraciones más lejanas.
        _shadowMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 50;

        var texture = ShadowTexture();
        _shadowMaterial.mainTexture = texture;
        _shadowMaterial.SetTexture("_BaseMap", texture);
        _shadowMaterial.SetColor("_BaseColor", shadowColor);
        _shadowMaterial.color = shadowColor;

        return _shadowMaterial;
    }

    // Círculo blanco que se desvanece hacia el borde. El color lo pone el material; acá solo
    // interesa la forma del degradado, para que la sombra no tenga canto duro.
    static Texture2D ShadowTexture()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "DecorationShadowTex",
            wrapMode = TextureWrapMode.Clamp,
        };

        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float alpha = 1f - Mathf.SmoothStep(0.35f, 1f, Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    static float SpriteHeight(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length == 0) return 1f;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        return bounds.size.y > 0.0001f ? bounds.size.y : 1f;
    }

    // La orientación se recalcula por frame: la esfera gira, así que la relación entre cada
    // decoración y la cámara cambia todo el tiempo.
    void LateUpdate()
    {
        if (_active.Count == 0 || !_cam) return;

        Vector3 camPos = _cam.transform.position;

        // Mismo criterio que la card del jugador: de frente a la cámara y siempre derecha.
        // El eje X de la esfera hace de "a lo ancho", así la decoración solo puede inclinarse
        // hacia adelante y atrás, nunca torcerse de costado ni acostarse al llegar al horizonte.
        Vector3 axis     = sphere.rotation * Vector3.right;
        Vector3 frontDir = (camPos - sphere.position).normalized;

        foreach (var planted in _active.Values)
        {
            var decoration = planted.transform;
            if (!decoration) continue;

            Vector3 anchor = sphere.TransformPoint(planted.anchor);

            // Las que quedaron detrás del horizonte no se ven: apagarlas las saca del render.
            if (cullRange > 0.01f)
            {
                bool visible = Vector3.Angle((anchor - sphere.position).normalized, frontDir) <= cullRange;
                if (decoration.gameObject.activeSelf != visible) decoration.gameObject.SetActive(visible);
                if (!visible) continue;
            }

            Vector3 toCam   = camPos - anchor;
            Vector3 forward = Vector3.ProjectOnPlane(-toCam, axis).normalized;
            if (forward.sqrMagnitude < 0.5f) continue;

            decoration.position = anchor + toCam.normalized * (cameraOffset * positioner.WorldScale);
            decoration.rotation = Quaternion.LookRotation(forward, Vector3.Cross(forward, axis));

        }
    }

    // Mismo patrón que la carga de niveles, pero en su propia carpeta: si el archivo de capítulo
    // viviera en Resources/Levels, los cargadores de niveles lo leerían como un nivel con id 0.
    static ChapterData LoadChapter(int chapter)
    {
        var json = Resources.Load<TextAsset>($"Chapters/Chapter_{chapter}");
        if (!json) return null;

        return JsonUtility.FromJson<ChapterData>(json.text);
    }
}
