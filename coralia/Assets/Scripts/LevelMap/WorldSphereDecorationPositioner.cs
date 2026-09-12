using System.Collections.Generic;
using UnityEngine;

// Construye el mundo a los costados del camino: lee el JSON de cada capítulo
// (Resources/Chapters/Chapter_N.json) y planta ahí las decoraciones que indique.
//
// Las posiciones vienen ancladas a los niveles, no a la esfera, y se resuelven con la MISMA curva
// que dibuja el camino (WorldSpherePath). Por eso una decoración no puede quedar desalineada del
// camino aunque después se cambie la separación de nodos o el zigzag.
//
// Los sprites se paran sobre la superficie y giran sobre su eje vertical para mirar a la cámara —
// un billboard cilíndrico. Así se leen como recortes de pie desde cualquier punto del scroll, sin
// necesidad de dibujarlos desde varios ángulos.
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
    [Tooltip("Inclinación máxima al azar respecto a la vertical, en grados. Un poco de desprolijidad hace que no parezcan puestas con regla.")]
    [Range(0f, 20f)]
    [SerializeField] float tiltVariation = 6f;

    Camera _cam;

    // La inclinación se guarda por decoración y se sortea una sola vez: si se recalculara por
    // frame, temblarían.
    struct Planted
    {
        public Transform transform;
        public float     tilt;
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
        int from = 0;
        for (int i = 1; i <= chapters.Count; i++)
        {
            bool isBreak = i == chapters.Count || chapters[i] != chapters[from];
            if (!isBreak) continue;

            BuildChapter(chapters[from], rotations, from, i - 1, radiusWorld, anglePerNode);
            from = i;
        }
    }

    void BuildChapter(int chapter, IReadOnlyList<Quaternion> rotations, int first, int last,
                      float radiusWorld, float anglePerNode)
    {
        var data = LoadChapter(chapter);
        if (data?.decorations == null || data.decorations.Length == 0) return;

        var nodeDirs = new Vector3[last - first + 1];
        for (int j = 0; j < nodeDirs.Length; j++)
            nodeDirs[j] = (rotations[first + j] * Vector3.back).normalized;

        // Semilla fija por capítulo: las variaciones al azar (tamaño, inclinación) salen iguales
        // en cada partida. Un mundo que cambia de forma cada vez que se abre el mapa se siente roto.
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

            decoration.SetParent(sphere, false);
            decoration.localPosition = direction * radiusLocal;

            float tilt = ((float)random.NextDouble() * 2f - 1f) * tiltVariation;
            _decorations.Add(new Planted { transform = decoration, tilt = tilt });
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

    static float SpriteHeight(GameObject go)
    {
        var renderer = go.GetComponentInChildren<SpriteRenderer>();
        return renderer && renderer.sprite ? renderer.sprite.bounds.size.y : 1f;
    }

    // El billboard se recalcula por frame: la esfera gira, así que la relación entre cada
    // decoración y la cámara cambia todo el tiempo.
    void LateUpdate()
    {
        if (_decorations.Count == 0 || !_cam) return;

        Vector3 camPos = _cam.transform.position;

        foreach (var planted in _decorations)
        {
            var decoration = planted.transform;
            if (!decoration) continue;

            // "Arriba" es la normal de la superficie: la decoración queda parada sobre el planeta,
            // no alineada con el eje Y del mundo.
            Vector3 up    = (decoration.position - sphere.position).normalized;
            Vector3 toCam = camPos - decoration.position;

            // Solo gira sobre ese eje vertical. Si mirara de lleno a la cámara se acostaría al
            // acercarse al horizonte, y dejaría de parecer que está plantada en el suelo.
            Vector3 forward = Vector3.ProjectOnPlane(toCam, up).normalized;
            if (forward.sqrMagnitude < 0.5f) continue;

            // El giro extra sobre su propio eje de vista es la inclinación: nada crece perfectamente
            // derecho, y con todas a plomo el mundo se ve puesto con regla.
            decoration.rotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(0f, 0f, planted.tilt);
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
