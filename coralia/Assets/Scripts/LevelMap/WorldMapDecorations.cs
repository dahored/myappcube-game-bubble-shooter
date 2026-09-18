using System.Collections.Generic;
using UnityEngine;

// Planta las decoraciones a los costados del camino, leyendo los Chapter_N.json que ya existen.
//
// Los datos no cambian ni una línea al pasar del mapa esférico a este: las posiciones están
// ancladas a los NIVELES (`at` en índice de nivel, `side`, `offset` en unidades de mundo), no a
// la geometría del mundo. Por eso el mismo archivo sirve para una esfera y para un plano.
//
// Instancia todo de una vez y no con pool como los nodos. Son cientos de objetos, pero cada uno
// es un sprite quieto: lo caro no es tenerlos, es dibujarlos, y de eso se encarga el culling —
// lo que queda fuera del alcance se apaga y sale del render.
[ExecuteAlways]
public class WorldMapDecorations : MonoBehaviour, IWorldMapRebuildable
{
    [SerializeField] DecorationCatalog catalog;

    [Tooltip("Multiplica el tamaño de TODAS las decoraciones. Para ajustar el conjunto sin tocar el catálogo ni los JSON.")]
    [SerializeField] float sizeMultiplier = 1f;

    [Tooltip("Cuánto se hunden en el suelo, para que la base no corte recta contra él.")]
    [SerializeField] float sink = 0.05f;

    [Tooltip("Apartar del camino siguiendo su perpendicular. Apagado aparta solo en X, y así cada pieza queda a la misma profundidad que el nodo de su índice.")]
    [SerializeField] bool perpendicularOffset = true;

    [Tooltip("Cuántos escalones de orden de dibujado por unidad de mundo. Más alto separa mejor piezas cercanas, pero el orden tiene tope (±32767) y un mundo largo lo puede pasar.")]
    [SerializeField] float orderPerUnit = 4f;

    [Tooltip("Cuánto se inclinan hacia atrás, girando sobre su base. 0 = paradas de frente.")]
    [Range(0f, 80f)]
    [SerializeField] float tilt = 12f;

    [Tooltip("Hasta qué distancia de la cámara se dibujan. Más allá se apagan.")]
    [SerializeField] float cullRange = 90f;

    [Tooltip("Semilla del tamaño al azar. Cambiarla revuelve qué pieza sale más grande o más chica.")]
    [SerializeField] int seed = 1;

    WorldMapDefinition _definition;

    // El alto real de cada pieza, medido una vez. No se puede volver a medir después: los bounds
    // de un renderer están en espacio de MUNDO, así que ya incluyen la escala que le pusimos.
    readonly Dictionary<string, float> _sourceHeight = new();

    readonly List<Transform> _planted = new();
    readonly List<float>     _plantedZ = new();

    bool _needsRebuild = true;

    void OnEnable()   => _needsRebuild = true;
    void OnValidate() => _needsRebuild = true;

    // Se anota y se hace después, nunca dentro de OnValidate: ahí Unity IGNORA DestroyImmediate,
    // así que Clear() no borraba nada y cada cambio en el Inspector plantaba otra copia encima
    // de la anterior.
    public void Rebuild() => _needsRebuild = true;

    void Build()
    {
        _definition = WorldMapDefinition.For(this);
        if (_definition == null || catalog == null) return;

        Clear();
        _sourceHeight.Clear();

        var rng = new System.Random(seed);

        // Un capítulo por vez: los `at` de cada archivo arrancan en 0, así que hay que correrlos
        // según dónde empieza ese capítulo dentro del mundo.
        foreach (var (chapter, firstLevel, count) in Chapters())
        {
            var data = LoadChapter(chapter);
            if (data?.decorations == null) continue;

            foreach (var placement in data.decorations)
                Plant(placement, firstLevel, count, rng);
        }
    }

    // Qué capítulos hay y en qué nivel arranca cada uno, sacado del índice: así agregar niveles a
    // un capítulo corre el siguiente solo, sin tener que tocar ningún número acá.
    IEnumerable<(int chapter, int firstLevel, int count)> Chapters()
    {
        var entries = LevelLoader.Entries;

        // Se corta en _definition.Levels y no en entries.Count: con 'Levels Override' puesto el
        // mundo tiene menos niveles de los que hay en disco, y sin esto el 'end' de un capítulo
        // se calcularía con su longitud real y caería fuera del mapa que se está probando.
        int total = Mathf.Min(entries.Count, _definition.Levels);
        if (total == 0) yield break;

        int start = 0;

        for (int i = 1; i <= total; i++)
        {
            bool last = i == total;
            if (!last && entries[i].chapter == entries[start].chapter) continue;

            yield return (entries[start].chapter, start, i - start);
            start = i;
        }
    }

    void Plant(DecorationPlacement placement, int firstLevel, int levelsInChapter, System.Random rng)
    {
        var entry = catalog.Find(placement.id);
        if (entry == null)
        {
            Debug.LogWarning($"[WorldMapDecorations] '{placement.id}' no está en el catálogo.", this);
            return;
        }

        var instance = Build(entry, placement);
        if (instance == null) return;

        float at     = firstLevel + placement.IndexIn(levelsInChapter);
        float offset = placement.side == "right" ? placement.offset : -placement.offset;

        Vector3 ground = WorldMapPath.SampleOffset(at, _definition.Layout, offset, perpendicularOffset);
        ground.y -= sink;

        var tr = instance.transform;
        tr.SetParent(transform, false);

        // 'height' del catálogo es el alto que se QUIERE en unidades de mundo, no un
        // multiplicador: hay que dividir por lo que mide la pieza de verdad. Sin esto un grupo de
        // 2.5 salía dos veces y media su tamaño original en vez de medir 2,5 unidades.
        float variation = 1f + ((float)rng.NextDouble() * 2f - 1f) * entry.sizeVariation;
        float wanted    = entry.height * Mathf.Max(0.01f, placement.scale) * variation
                                       * Mathf.Max(0.01f, sizeMultiplier);
        float scale     = wanted / SourceHeight(placement.id, instance);

        tr.localScale    = new Vector3(placement.flip ? -scale : scale, scale, scale);

        // Los sprites del catálogo traen el pivot en su base (alignment Custom, y ≈ 0.25), así
        // que el transform ES el punto de apoyo: se coloca en el suelo y la inclinación gira
        // sobre él sola, sin compensar nada.
        tr.localPosition = ground;
        tr.localRotation = Quaternion.Euler(tilt, 0f, 0f);

        // Orden de dibujado por profundidad: lo que está más cerca de la cámara se pinta encima.
        // Sin esto Unity ordena los transparentes por distancia al CENTRO de sus bounds, y una
        // roca alta y lejana puede ganarle a un coral bajo y cercano.
        //
        // Se SUMA al orden que traiga cada renderer en vez de pisarlo: dentro de un grupo, las
        // plantas y las rocas ya vienen ordenadas entre sí desde el prefab, y pisarlas aplanaría
        // esa composición.
        int order = Mathf.Clamp(Mathf.RoundToInt(-ground.z * orderPerUnit), -20000, 20000);
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            renderer.sortingOrder += order;

        _planted.Add(tr);
        _plantedZ.Add(ground.z);
    }

    // Alto de la pieza tal como viene del prefab. En un grupo es el de TODA la composición: si se
    // midiera solo el primer sprite, el 'height' del catálogo pasaría a significar "el alto de una
    // de las plantas" en vez de "el alto del conjunto".
    float SourceHeight(string id, GameObject instance)
    {
        if (_sourceHeight.TryGetValue(id, out var cached)) return cached;

        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        float height = 1f;

        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y > 0.0001f) height = bounds.size.y;
        }

        _sourceHeight[id] = height;
        return height;
    }

    GameObject Build(DecorationCatalog.Entry entry, DecorationPlacement placement)
    {
        if (entry.prefab)
        {
            // Prefab + sprite es la combinación útil: un prefab con su animación adentro, reusado
            // por todas las piezas, y cada entrada del catálogo aportando solo su imagen.
            var go = Instantiate(entry.prefab);
            if (entry.sprite)
            {
                var target = go.GetComponentInChildren<SpriteRenderer>(true);
                if (target) target.sprite = entry.sprite;
            }
            return go;
        }

        if (entry.sprite)
        {
            var go = new GameObject(placement.id);
            go.AddComponent<SpriteRenderer>().sprite = entry.sprite;
            return go;
        }

        Debug.LogWarning($"[WorldMapDecorations] '{entry.id}' no tiene ni sprite ni prefab.", this);
        return null;
    }

    void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }

        _planted.Clear();
        _plantedZ.Clear();
    }

    // Apaga lo que queda lejos. Es lo único que corre por frame: comparar una Z contra un número
    // sale casi gratis, y sin esto se dibujarían las doscientas ochenta decoraciones del capítulo
    // aunque solo se vean diez.
    void LateUpdate()
    {
        if (_needsRebuild)
        {
            _needsRebuild = false;
            Build();
        }

        var camera = Camera.main;
        if (camera == null || _planted.Count == 0) return;

        float cameraZ = camera.transform.position.z - transform.position.z;

        for (int i = 0; i < _planted.Count; i++)
        {
            if (!_planted[i]) continue;

            float distance = _plantedZ[i] - cameraZ;
            bool  visible  = distance > -cullRange * 0.25f && distance < cullRange;

            if (_planted[i].gameObject.activeSelf != visible)
                _planted[i].gameObject.SetActive(visible);
        }
    }

    static ChapterData LoadChapter(int chapter)
    {
        var json = Resources.Load<TextAsset>($"Chapters/Chapter_{chapter}");
        return json ? JsonUtility.FromJson<ChapterData>(json.text) : null;
    }
}
