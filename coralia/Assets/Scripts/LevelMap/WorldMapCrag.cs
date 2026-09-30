using System.Collections.Generic;
using UnityEngine;

// Las rocas que se apoyan en el filo de la fosa.
//
// La pared de la fosa es geometría con la luz pintada por vértice, y eso resuelve la profundidad
// pero no el detalle: una malla barrida no tiene el dibujo que tiene un sprite pintado a mano.
// Así que el filo se cubre con piezas de arte de verdad, hundidas hasta que solo asome lo que
// debe asomar.
//
// Va aparte de WorldMapDecorations aunque se parezcan: las decoraciones se colocan una a una
// desde el JSON del capítulo y se apoyan en el camino, y estas se reparten solas a lo ancho de
// un filo que el suelo ya sabe dónde está.
[ExecuteAlways]
public class WorldMapCrag : MonoBehaviour, IWorldMapRebuildable
{
    [Tooltip("La pieza de roca que se repite a lo largo del filo. Se mide sola: lo que diga 'Height' es lo que va a medir de alto, sea cual sea el tamaño con el que esté hecho el prefab.")]
    [SerializeField] GameObject prefab;

    [Tooltip("El material de los sprites del mundo, el que conoce la curva. Sin él las rocas salen rectas mientras el suelo se hunde. Es el mismo que usa WorldMapDecorations.")]
    [SerializeField] Material spriteMaterial;

    [Tooltip("Cuánto mide de alto la pieza entera, en unidades de mundo. Casi todo queda por debajo de la arena: lo que se ve es solo lo que diga 'Rise'.")]
    [SerializeField] float height = 6f;

    [Tooltip("Cuánto asoma la roca por encima de la arena. Es el único trozo que se ve del derecho; el resto baja hacia la fosa. En 0 la roca queda a ras y solo tapa la pared.")]
    [SerializeField] float rise = 0.3f;

    [Tooltip("Cuánto se corre la hilera de ACÁ, la del filo que se deja atrás. Positivo la mete dentro del hueco, negativo la trae sobre la arena. Va aparte de la de enfrente porque las dos no se ven igual: a esta se la mira desde arriba y a la otra de frente.")]
    [SerializeField] float nearZOffset;

    [Tooltip("Cuánto se corre la hilera de ENFRENTE, la del filo al que se va. Positivo la aleja hacia su isla, negativo la mete dentro del hueco.")]
    [SerializeField] float farZOffset;

    [Tooltip("Cuánto se solapan dos piezas seguidas, en tanto por uno de su ancho. Sin solape se ve la costura entre una y la siguiente.")]
    [Range(0f, 0.5f)]
    [SerializeField] float overlap = 0.12f;

    [Tooltip("Si la hilera sigue la ondulación del filo. Apagado la cruza en línea recta, que se nota porque el suelo de al lado sí ondula.")]
    [SerializeField] bool followEdge = true;

    [Tooltip("En el filo de acá, el que da al capítulo que se deja atrás.")]
    [SerializeField] bool nearEdge = true;

    [Tooltip("En el filo de enfrente, el que da al capítulo al que se va. Es la pared que se ve de frente desde la cámara, así que suele ser la que más importa.")]
    [SerializeField] bool farEdge = true;

    [Tooltip("Cuánto se inclina la pieza hacia la cámara, en grados. En 0 queda de pie.")]
    [SerializeField] float tilt;

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
        if (_definition == null) return;

        if (prefab == null)
        {
            Debug.LogWarning("[WorldMapCrag] Falta el prefab de roca: el filo de la fosa se queda sin cubrir.", this);
            return;
        }

        if (_ground == null) _ground = FindAnyObjectByType<WorldMapGround>();
        if (_ground == null)
        {
            Debug.LogWarning("[WorldMapCrag] No hay WorldMapGround en la escena: sin él no se sabe dónde está el filo.", this);
            return;
        }

        // Se mide una sola vez, no por hilera: instanciar para medir y tirar la copia es barato
        // pero ensucia la escena en el editor, y el prefab no cambia entre una hilera y otra.
        var sample = Instantiate(prefab);
        var bounds = Measure(sample);
        DestroyInstance(sample);

        if (bounds.size.y < 0.001f || bounds.size.x < 0.001f)
        {
            Debug.LogWarning($"[WorldMapCrag] '{prefab.name}' no tiene ningún Renderer con tamaño: no se puede saber cuánto mide para repartirlo.", this);
            return;
        }

        var spans = _definition.VisibleSpans();

        for (int i = 0; i + 1 < spans.Count; i++)
        {
            if (nearEdge) Line(_ground.IslandEnd(spans[i]),       nearZOffset, bounds);
            if (farEdge)  Line(_ground.IslandStart(spans[i + 1]), farZOffset,  bounds);
        }
    }

    // Una hilera de piezas cruzando el mapa de lado a lado. El desplazamiento se aplica tal cual
    // y sin darle vuelta al signo según el filo: los dos números apuntan al mismo lado, así que
    // subirlos mueve las dos hileras en la misma dirección y se pueden pensar juntos.
    void Line(float edgeZ, float offset, Bounds bounds)
    {
        float width = _ground.Width;
        float scale = height / bounds.size.y;
        float step  = bounds.size.x * scale * (1f - overlap);
        int   count = Mathf.Max(1, Mathf.CeilToInt(width / step) + 1);

        // Lo que sobresale del pivote por arriba. Restarlo deja el TOPE de la roca a ras de la
        // arena, que es lo que hace que 'Rise' se lea como "cuánto asoma" y no como una altura
        // suelta que hay que buscar a tientas.
        float top = bounds.max.y * scale;

        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) * 0.5f) * step;
            float z = edgeZ + offset;

            if (followEdge) z += _ground.EdgeAt(x, edgeZ);

            Place(new Vector3(x, rise - top, z), scale);
        }
    }

    void Place(Vector3 flat, float scale)
    {
        var instance = Instantiate(prefab);
        var tr       = instance.transform;

        tr.SetParent(transform, false);
        tr.localScale    = Vector3.one * scale;
        tr.localPosition = flat;
        tr.localRotation = Quaternion.Euler(tilt, 0f, 0f);

        int order = _definition.SortingOrder(flat.z);

        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sortingOrder += order;

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

            // La curva la aplica la cámara, no el shader: estas piezas son sprites sueltos y hay
            // que moverlos a mano igual que las decoraciones, o se quedarían rectos mientras el
            // suelo al que se apoyan se hunde hacia el horizonte.
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
