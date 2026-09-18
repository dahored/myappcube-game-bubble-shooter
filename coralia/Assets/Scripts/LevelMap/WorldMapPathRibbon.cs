using System.Collections.Generic;
using UnityEngine;

// Dibuja el camino: una cinta de malla que sigue el trazado de WorldMapPath.
//
// Malla y no sprite ni LineRenderer por dos razones. Una, el camino se dobla con el mundo, y eso
// solo funciona si tiene vértices repartidos a lo largo — un sprite estirado tiene cuatro. Dos,
// un LineRenderer se orienta siempre hacia la cámara, y este camino está apoyado en el suelo.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WorldMapPathRibbon : MonoBehaviour, IWorldMapRebuildable
{
    [Tooltip("Cuánto camino sobra ANTES del primer nivel, medido en niveles. Le da aire a la punta en vez de que nazca pegada al nodo 1.")]
    [SerializeField] float leadIn = 1.2f;

    [Tooltip("Lo mismo después del último nivel.")]
    [SerializeField] float leadOut = 1.2f;

    [Tooltip("Ancho de la cinta, en unidades de mundo.")]
    [SerializeField] float width = 1.6f;

    [Tooltip("Cuántos cortes por nivel. Más alto es más suave en las curvas del zigzag.")]
    [SerializeField] int stepsPerLevel = 6;

    [Tooltip("Cuánto se levanta del suelo. Sin esto los dos planos pelean por el mismo píxel y el camino parpadea.")]
    [SerializeField] float height = 0.02f;

    [Tooltip("Cada cuántos niveles se repite la textura a lo largo.")]
    [SerializeField] float tiling = 2f;

    [Tooltip("Cuántos triángulos tiene cada punta redondeada. 0 las deja cortadas en recto.")]
    [Range(0, 16)]
    [SerializeField] int endSegments = 8;

    WorldMapDefinition _definition;
    Mesh _mesh;

    WorldMapPath.Layout layout => _definition.Layout;

    void OnEnable()   => Rebuild();
    void OnValidate() => Rebuild();

    public void Rebuild()
    {
        _definition = WorldMapDefinition.For(this);
        if (_definition == null) return;

        int   levels = _definition.Levels;
        float first  = -leadIn;
        float last   = (levels - 1) + leadOut;
        int   steps  = Mathf.Max(1, Mathf.RoundToInt((last - first) * Mathf.Max(1, stepsPerLevel)));

        var vertices = new List<Vector3>();
        var uvs      = new List<Vector2>();
        var indices  = new List<int>();

        for (int i = 0; i <= steps; i++)
        {
            float f = first + i / (float)Mathf.Max(1, stepsPerLevel);
            WorldMapPath.SampleFrame(f, layout, out var point, out var forward);

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized * (width * 0.5f);
            point.y += height;

            float along = f / Mathf.Max(0.01f, tiling);

            vertices.Add(point - right); uvs.Add(new Vector2(0f, along));
            vertices.Add(point + right); uvs.Add(new Vector2(1f, along));

            if (i == 0) continue;

            int a = (i - 1) * 2;
            indices.Add(a);     indices.Add(a + 2); indices.Add(a + 1);
            indices.Add(a + 1); indices.Add(a + 2); indices.Add(a + 3);
        }

        AppendCap(vertices, uvs, indices, first, true);
        AppendCap(vertices, uvs, indices, last,  false);

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "WorldMapPathRibbon" };
            _mesh.hideFlags = HideFlags.DontSave;
        }

        _mesh.Clear();
        _mesh.indexFormat = vertices.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        _mesh.SetVertices(vertices);
        _mesh.SetUVs(0, uvs);
        _mesh.SetTriangles(indices, 0);
        _mesh.RecalculateBounds();

        var bounds = _mesh.bounds;
        bounds.Expand(new Vector3(0f, _definition.Length, 0f));
        _mesh.bounds = bounds;

        GetComponent<MeshFilter>().sharedMesh = _mesh;
    }

    // Una punta redondeada es medio disco pegado al extremo: un vértice en el centro y un
    // abanico de triángulos hasta el borde. Sin esto la cinta termina en un corte recto, que se
    // lee como un camino partido en vez de uno que empieza.
    //
    // El semicírculo va hacia AFUERA del recorrido — hacia atrás en el arranque y hacia adelante
    // en el final — así la tapa sobresale en vez de comerse el último tramo.
    void AppendCap(List<Vector3> vertices, List<Vector2> uvs, List<int> indices, float f, bool start)
    {
        if (endSegments <= 0) return;

        WorldMapPath.SampleFrame(f, layout, out var center, out var forward);
        center.y += height;

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 out_  = start ? -forward : forward;
        float   radius = width * 0.5f;

        // Las UV de la tapa se sacan proyectando cada vértice sobre los mismos ejes que la cinta,
        // no repartiendo el abanico de 0 a 1. Si se reparten, la textura sale en rayos desde el
        // centro y la punta se ve como un abanico pintado en vez de la continuación del camino.
        float along   = f / Mathf.Max(0.01f, tiling);
        float perUnit = 1f / Mathf.Max(0.01f, tiling * layout.spacing); // cuánto avanza la V por unidad de mundo

        int centerIndex = vertices.Count;
        vertices.Add(center);
        uvs.Add(new Vector2(0.5f, along));

        for (int i = 0; i <= endSegments; i++)
        {
            float angle = Mathf.PI * i / endSegments;

            // Arranca en un borde de la cinta, pasa por la punta y termina en el otro borde.
            Vector3 dir    = right * Mathf.Cos(angle) + out_ * Mathf.Sin(angle);
            Vector3 offset = dir * radius;

            vertices.Add(center + offset);
            uvs.Add(new Vector2(
                0.5f + Vector3.Dot(offset, right)   / width,
                along + Vector3.Dot(offset, forward) * perUnit));

            if (i == 0) continue;

            int edge = centerIndex + i;

            // El orden se invierte entre las dos puntas: el abanico gira al revés en cada una y
            // con un solo orden, una de las dos quedaría mirando hacia abajo y no se vería.
            if (start) { indices.Add(centerIndex); indices.Add(edge);        indices.Add(edge + 1); }
            else       { indices.Add(centerIndex); indices.Add(edge + 1);    indices.Add(edge); }
        }
    }
}
