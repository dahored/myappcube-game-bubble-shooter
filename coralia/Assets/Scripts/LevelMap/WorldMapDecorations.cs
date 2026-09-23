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

    [Tooltip("El material curvo que se le pone a cada sprite. Sin esto las decoraciones se quedan rectas mientras el resto del mapa se dobla. Uno solo sirve para todas: cada sprite le pasa su propia textura.")]
    [SerializeField] Material spriteMaterial;

    [Tooltip("Multiplica el tamaño de TODAS las decoraciones. Para ajustar el conjunto sin tocar el catálogo ni los JSON.")]
    [SerializeField] float sizeMultiplier = 1f;

    [Tooltip("Cuánto se hunden en el suelo de CERCA, donde el terreno se mira desde arriba. En negativo las levanta. Acá suele hacer falta poco —o negativo—: el suelo es opaco y escribe profundidad, así que la parte inclinada hacia atrás se mete detrás de él y Unity la corta.")]
    [SerializeField] float sink = 0.05f;

    [Tooltip("Lo mismo, pero donde el suelo ya se ve de canto. Ahí el corte no ocurre (no hay suelo delante) y en cambio cualquier altura de más se lee como un hueco, así que casi siempre quiere ser MAYOR que el de cerca. Se pasa de uno al otro solo, según lo inclinado que esté el terreno.")]
    [SerializeField] float sinkAtHorizon = 0.2f;

    [Tooltip("Apartar del camino siguiendo su perpendicular. Apagado aparta solo en X, y así cada pieza queda a la misma profundidad que el nodo de su índice.")]
    [SerializeField] bool perpendicularOffset = true;

    [Tooltip("Cuánto se inclinan hacia atrás. Girar no las despega del suelo: lo que la inclinación levanta se compensa solo, así que se puede subir sin que queden flotando.")]
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

    // Dónde iría cada pieza si el mundo fuera plano. Es la posición de referencia: la de verdad
    // se recalcula por frame aplicándole la curva.
    readonly List<Vector3>   _plantedFlat = new();

    // Cuánto sobresale cada pieza por DEBAJO de su pivote, ya escalado. Es lo que la inclinación
    // levanta del suelo, y hay que saberlo por pieza porque depende de su tamaño — ver Place().
    readonly List<float>     _belowPivot  = new();

    Camera _camera;

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
        foreach (var span in _definition.VisibleSpans())
        {
            var data = LoadChapter(span.number);
            if (data?.decorations == null) continue;

            foreach (var placement in data.decorations)
                Plant(placement, span.start, span.count, rng);
        }
    }

    // Qué capítulos hay y en qué nivel arranca cada uno, sacado del índice: así agregar niveles a
    // un capítulo corre el siguiente solo, sin tener que tocar ningún número acá.
    void Plant(DecorationPlacement placement, float chapterStart, int levelsInChapter, System.Random rng)
    {
        var entry = catalog.Find(placement.id);
        if (entry == null)
        {
            Debug.LogWarning($"[WorldMapDecorations] '{placement.id}' no está en el catálogo.", this);
            return;
        }

        var instance = Build(entry, placement);
        if (instance == null) return;

        float at     = chapterStart + placement.IndexIn(levelsInChapter);
        float offset = placement.side == "right" ? placement.offset : -placement.offset;

        // El hundido NO se aplica acá: depende de lo inclinado que se vea el suelo bajo la pieza,
        // o sea de dónde esté la cámara. Se resuelve por frame en Place().
        Vector3 ground = WorldMapPath.SampleOffset(at, _definition.Layout, offset, perpendicularOffset);

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

        // El orden lo decide el mundo, para que nodos y decoraciones compartan la misma escala.
        //
        // Se SUMA al orden que traiga cada renderer en vez de pisarlo: dentro de un grupo, las
        // plantas y las rocas ya vienen ordenadas entre sí desde el prefab, y pisarlas aplanaría
        // esa composición.
        int order = _definition.SortingOrder(ground.z);
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sortingOrder += order;

            // Un único material para todas las piezas: SpriteRenderer le pasa la textura de su
            // sprite por _MainTex, así que el material no necesita saber cuál es. Sin esto los
            // sprites usan el material por defecto de Unity, que no conoce la curva y los deja
            // rectos mientras el suelo se hunde.
            if (spriteMaterial && renderer is SpriteRenderer)
                renderer.sharedMaterial = spriteMaterial;
        }

        // Se mide ANTES de inclinarla: con la rotación puesta, los bounds ya vendrían girados y
        // el número saldría del arco en vez del sprite.
        _belowPivot.Add(BelowPivot(instance));

        tr.localRotation = Quaternion.Euler(tilt, 0f, 0f);

        _planted.Add(tr);
        _plantedFlat.Add(ground);
    }

    // Alto de la pieza tal como viene del prefab. En un grupo es el de TODA la composición: si se
    // midiera solo el primer sprite, el 'height' del catálogo pasaría a significar "el alto de una
    // de las plantas" en vez de "el alto del conjunto".
    // De 0 a 1 según lo de canto que se vea el suelo bajo un punto: 0 en el tramo plano de
    // adelante, 1 en lo más lejos que llega a dibujarse una decoración.
    //
    // Se normaliza contra la pendiente que hay EN EL BORDE DEL ALCANCE y no contra 90 grados
    // porque el terreno nunca llega a verse del todo de canto dentro de lo que se dibuja: con la
    // curvatura y el alcance actuales se queda en unos 63. Dividiendo por 90, el campo del
    // horizonte nunca se alcanzaría y habría que pedir de más para conseguir lo que uno quiere.
    // Así vale exactamente lo que dice.
    float EdgeOn(Vector3 positionWS)
    {
        float here = WorldMapCurve.Pitch(positionWS, _camera);
        float max  = WorldMapCurve.Pitch(
            new Vector3(0f, 0f, _camera.transform.position.z + cullRange), _camera);

        return max > 1f ? Mathf.Clamp01(here / max) : 0f;
    }

    // Cuánto queda por debajo del pivote, en unidades de mundo y con la escala ya aplicada. No se
    // puede cachear por id como el alto: dos copias de la misma pieza pueden tener escalas
    // distintas (el 'scale' del JSON y la variación al azar).
    static float BelowPivot(GameObject instance)
    {
        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return 0f;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        return Mathf.Max(0f, instance.transform.position.y - bounds.min.y);
    }

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
        _belowPivot.Clear();
        _plantedFlat.Clear();
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

        if (_camera == null) _camera = Camera.main;
        if (_camera == null || _planted.Count == 0) return;

        float cameraZ = _camera.transform.position.z - transform.position.z;

        for (int i = 0; i < _planted.Count; i++)
        {
            if (!_planted[i]) continue;

            float distance = _plantedFlat[i].z - cameraZ;
            bool  visible  = distance > -cullRange * 0.25f && distance < cullRange;

            if (_planted[i].gameObject.activeSelf != visible)
                _planted[i].gameObject.SetActive(visible);

            if (visible) Place(i);
        }
    }

    void Place(int i)
    {
        Vector3 world  = transform.TransformPoint(_plantedFlat[i]);
        Vector3 curved = WorldMapCurve.Curve(world, _camera);

        // La inclinación gira la pieza sobre su PIVOTE, y el pivote no es el borde de abajo: hay
        // un trozo de sprite por debajo (la base pintada de la roca). Al girar, ese trozo describe
        // un arco y sube: a 35° levanta un 17% de su largo, a 60° un 50%. La pieza queda apoyada
        // en el pivote pero su base dibujada flota.
        //
        // De cerca no se ve porque se mira el suelo desde arriba y unos centímetros de altura
        // quedan tapados por la propia pieza. Hacia el horizonte el suelo se ve casi de canto, y
        // esos mismos centímetros se convierten en un hueco bien visible — que es lo que parecía
        // un problema de 'sink' pero no lo era: el sink es constante y el hueco aparece solo lejos.
        curved.y -= _belowPivot[i] * (1f - Mathf.Cos(tilt * Mathf.Deg2Rad));

        // Y el hundido, que no puede ser uno solo para todo el mapa porque el problema cambia de
        // signo con la distancia:
        //
        //   De cerca  el suelo se mira desde arriba y es opaco: la parte de la pieza inclinada
        //             hacia atrás queda DETRÁS de la superficie y Unity la corta. Hay que
        //             levantarla (hundido chico, o negativo).
        //   De lejos  el suelo se ve de canto y ya no hay nada delante que corte; lo que se nota
        //             es al revés, cualquier altura de más se lee como un hueco bajo la pieza.
        //
        // Se interpola con la PENDIENTE del suelo y no con la distancia: la pendiente ya es la
        // medida de "qué tan de canto se ve el terreno acá", que es justo lo que decide cuál de
        // los dos problemas manda. Y no hay que calibrar ninguna distancia a mano: si cambia la
        // curvatura o el encuadre, la transición se corre sola.
        curved.y -= Mathf.Lerp(sink, sinkAtHorizon, EdgeOn(world));

        _planted[i].localPosition = transform.InverseTransformPoint(curved);
    }

    // La curva se la aplica C# a la pieza entera, no el shader, aunque el material sepa hacerlo.
    //
    // El motivo es el descarte por cuadro de Unity: decide si dibujar un objeto mirando sus
    // límites SIN curvar, y el shader recién los mueve al dibujar. Pasado el horizonte, la pieza
    // plana queda por encima del borde superior y Unity la descarta, mientras que su versión
    // hundida sí estaría en pantalla. Por eso desaparecían justo al llegar al horizonte.
    //
    // Moviendo el Transform, los límites acompañan y el descarte vuelve a ser correcto. Y no se
    // pierde nada visualmente: un sprite tiene cuatro vértices casi a la misma profundidad, así
    // que la curva nunca lo dobló — solo lo bajaba. Ver Place().

    static ChapterData LoadChapter(int chapter)
    {
        var json = Resources.Load<TextAsset>($"Chapters/Chapter_{chapter}");
        return json ? JsonUtility.FromJson<ChapterData>(json.text) : null;
    }
}
