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

    Camera   _cam;
    Material _shadowMaterial;

    // Se guarda el punto de apoyo aparte de la posición final: la decoración se dibuja adelantada
    // hacia la cámara, pero su lugar en el mundo sigue siendo el de la superficie.
    struct Planted
    {
        public Transform transform;
        public Vector3   anchor;   // en espacio local de la esfera
    }

    readonly List<Planted> _decorations = new List<Planted>();

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

        var rotations = positioner.NodeRotations;
        var chapters  = positioner.NodeChapters;
        if (rotations == null || rotations.Count == 0 || chapters == null) return;

        float radiusWorld = positioner.SurfaceRadiusLocal * positioner.SphereScale;
        float anglePerNode = positioner.AngleSpacing;

        // Un tramo por capítulo. El 'at' del JSON es relativo al PRIMER nivel de su capítulo, así
        // que cada capítulo se resuelve con su propio sub-recorrido.
        var shadowVerts = new List<Vector3>();
        var shadowUvs   = new List<Vector2>();
        var shadowNorms = new List<Vector3>();
        var shadowTris  = new List<int>();

        int from = 0;
        for (int i = 1; i <= chapters.Count; i++)
        {
            bool isBreak = i == chapters.Count || chapters[i] != chapters[from];
            if (!isBreak) continue;

            BuildChapter(chapters[from], rotations, from, i - 1, radiusWorld, anglePerNode,
                         shadowVerts, shadowUvs, shadowNorms, shadowTris);
            from = i;
        }

        BuildShadowMesh(shadowVerts, shadowUvs, shadowNorms, shadowTris);
    }

    void BuildChapter(int chapter, IReadOnlyList<Quaternion> rotations, int first, int last,
                      float radiusWorld, float anglePerNode,
                      List<Vector3> shadowVerts, List<Vector2> shadowUvs,
                      List<Vector3> shadowNorms, List<int> shadowTris)
    {
        var data = LoadChapter(chapter);
        if (data?.decorations == null || data.decorations.Length == 0) return;

        var nodeDirs = new Vector3[last - first + 1];
        for (int j = 0; j < nodeDirs.Length; j++)
            nodeDirs[j] = (rotations[first + j] * Vector3.back).normalized;

        // Semilla fija por capítulo: la variación de tamaño sale igual en cada partida. Un mundo
        // que cambia de forma cada vez que se abre el mapa se siente roto.
        var random = new System.Random(chapter * 7919);

        float radiusLocal = positioner.SurfaceRadiusLocal - sink / Mathf.Max(positioner.SphereScale, 0.0001f);

        foreach (var placement in data.decorations)
        {
            var entry = catalog.Find(placement.id);
            if (entry == null)
            {
                Debug.LogWarning($"[WorldSphereDecorationPositioner] El capítulo {chapter} pide '{placement.id}', que no está en el catálogo — se saltea.", this);
                continue;
            }

            float signedOffset = placement.side == "right" ? -placement.offset : placement.offset;
            Vector3 direction  = WorldSpherePath.SampleOffset(nodeDirs, placement.at, anglePerNode, signedOffset, radiusWorld);

            var decoration = Spawn(entry, placement, random);
            if (!decoration) continue;

            Vector3 anchor = direction * radiusLocal;
            decoration.SetParent(sphere, false);
            decoration.localPosition = anchor;
            _decorations.Add(new Planted { transform = decoration, anchor = anchor });

            if (contactShadow) AppendShadow(entry, placement, direction, shadowVerts, shadowUvs, shadowNorms, shadowTris);
        }
    }

    Transform Spawn(DecorationCatalog.Entry entry, DecorationPlacement placement, System.Random random)
    {
        GameObject go;

        if (entry.prefab)
        {
            go = Instantiate(entry.prefab);
        }
        else if (entry.sprite)
        {
            go = new GameObject(placement.id);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = entry.sprite;
            renderer.flipX  = placement.flip;
        }
        else
        {
            Debug.LogWarning($"[WorldSphereDecorationPositioner] La entrada '{entry.id}' del catálogo no tiene ni sprite ni prefab.", this);
            return null;
        }

        go.name = $"Deco_{placement.id}";

        // El tamaño se pide en unidades de MUNDO, pero el sprite mide lo que mide según sus pixels
        // por unidad — hay que convertir. Y como cuelga de la esfera, que tiene una escala enorme,
        // encima hay que dividir por ella.
        float variation = 1f + ((float)random.NextDouble() * 2f - 1f) * entry.sizeVariation;
        float worldSize = entry.height * placement.scale * variation;
        float spriteSize = SpriteHeight(go);
        float scale = worldSize / Mathf.Max(spriteSize, 0.0001f) / Mathf.Max(positioner.SphereScale, 0.0001f);

        go.transform.localScale = Vector3.one * scale;
        return go.transform;
    }

    // Las sombras se acumulan en UNA sola malla en vez de un objeto por decoración. No se mueven
    // nunca respecto de la esfera, así que no hay razón para que sean objetos separados: así son
    // un solo draw call en vez de uno por pieza.
    //
    // Además evita GameObject.CreatePrimitive, que siempre intenta agregar un MeshCollider — y en
    // un build para dispositivo esa clase la elimina el code stripping, lo que tira un error por
    // cada sombra.
    void AppendShadow(DecorationCatalog.Entry entry, DecorationPlacement placement, Vector3 direction,
                      List<Vector3> verts, List<Vector2> uvs, List<Vector3> norms, List<int> tris)
    {
        float width = entry.shadowWidth > 0.0001f
            ? entry.shadowWidth
            : entry.height * placement.scale * shadowScale;

        float sphereScale = Mathf.Max(positioner.SphereScale, 0.0001f);
        float half        = width * 0.5f / sphereScale;

        // Apenas por encima de la superficie, lo justo para no pelear en profundidad con el suelo.
        Vector3 center = direction * (positioner.SurfaceRadiusLocal + 0.02f / sphereScale);

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

        var mesh = new Mesh { name = "DecorationShadows", hideFlags = HideFlags.DontSave };
        mesh.indexFormat = verts.Count > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        var go = new GameObject("ContactShadows");
        go.transform.SetParent(sphere, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows    = false;
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
        // Antes que el resto de los transparentes, no junto con ellos. Unity los ordena por la
        // distancia del CENTRO de cada objeto, y como todas las sombras son una sola malla, ese
        // centro cae en medio del capítulo: sin esto, las sombras se dibujan por encima de las
        // decoraciones que quedaron más lejos que ese punto.
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
        var renderer = go.GetComponentInChildren<SpriteRenderer>();
        return renderer && renderer.sprite ? renderer.sprite.bounds.size.y : 1f;
    }

    // La orientación se recalcula por frame: la esfera gira, así que la relación entre cada
    // decoración y la cámara cambia todo el tiempo.
    void LateUpdate()
    {
        if (_decorations.Count == 0 || !_cam) return;

        Vector3 camPos = _cam.transform.position;

        // Mismo criterio que la card del jugador: de frente a la cámara y siempre derecha.
        // El eje X de la esfera hace de "a lo ancho", así la decoración solo puede inclinarse
        // hacia adelante y atrás, nunca torcerse de costado ni acostarse al llegar al horizonte.
        Vector3 axis     = sphere.rotation * Vector3.right;
        Vector3 frontDir = (camPos - sphere.position).normalized;

        foreach (var planted in _decorations)
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

            decoration.position = anchor + toCam.normalized * cameraOffset;
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
