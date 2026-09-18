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

        var vertices = new List<Vector3>();
        var uvs      = new List<Vector2>();
        var indices  = new List<int>();

        // Un camino por capítulo: cada isla tiene el suyo y entre una y otra no hay nada.
        foreach (var span in _definition.VisibleSpans())
            AppendPath(vertices, uvs, indices, span.start - leadIn, span.End + leadOut);

        Finish(vertices, uvs, indices);
    }

    // La cinta entera, puntas incluidas: una sucesión de filas de dos vértices que se van
    // cosiendo entre sí. Las puntas no son un abanico aparte, son filas más angostas.
    //
    // Antes la punta era medio disco con un vértice en el centro, y con la curvatura encendida
    // eso abría una rendija: el borde de la cinta es UN segmento recto entre sus dos esquinas,
    // mientras que el borde del abanico pasa por el vértice del centro. La caída lateral es
    // cuadrática, así que el centro no cae lo mismo que el promedio de las esquinas y los dos
    // bordes dejaban de coincidir. Se veía el suelo entre medio.
    void AppendPath(List<Vector3> vertices, List<Vector2> uvs, List<int> indices, float first, float last)
    {
        int start = vertices.Count;
        int rows  = 0;

        // Punta de entrada: de la cúspide hacia la cinta.
        for (int i = endSegments; i >= 1; i--)
            AppendCapRow(vertices, uvs, indices, start, ref rows, first, -1f, i);

        int steps = Mathf.Max(1, Mathf.RoundToInt((last - first) * Mathf.Max(1, stepsPerLevel)));
        for (int i = 0; i <= steps; i++)
        {
            // Interpolado y no sumando 1/stepsPerLevel: así la última fila cae EXACTAMENTE en
            // 'last' aunque el tramo no dé un número redondo de cortes.
            float f = Mathf.Lerp(first, last, i / (float)steps);
            WorldMapPath.SampleFrame(f, layout, out var point, out var forward);

            AppendRow(vertices, uvs, indices, start, ref rows,
                      point, Vector3.Cross(Vector3.up, forward).normalized,
                      width * 0.5f, f / Mathf.Max(0.01f, tiling));
        }

        for (int i = 1; i <= endSegments; i++)
            AppendCapRow(vertices, uvs, indices, start, ref rows, last, 1f, i);
    }

    // Una fila de la punta redondeada: se aleja del extremo siguiendo un cuarto de círculo y al
    // mismo tiempo se angosta, así que el conjunto dibuja el medio disco de siempre — pero cosido
    // a la cinta y con la misma densidad de vértices, que es lo que le permite seguir la curva.
    void AppendCapRow(List<Vector3> vertices, List<Vector2> uvs, List<int> indices,
                      int start, ref int rows, float f, float direction, int step)
    {
        if (endSegments <= 0) return;

        WorldMapPath.SampleFrame(f, layout, out var center, out var forward);

        float angle  = Mathf.PI * 0.5f * step / endSegments;
        float radius = width * 0.5f;
        float reach  = radius * Mathf.Sin(angle) * direction;

        // Cuánto avanza la V por unidad de mundo: la punta se sale del recorrido, así que su
        // coordenada no puede salir del índice de nivel como la del cuerpo.
        float perUnit = 1f / Mathf.Max(0.01f, tiling * layout.spacing);

        AppendRow(vertices, uvs, indices, start, ref rows,
                  center + forward * reach,
                  Vector3.Cross(Vector3.up, forward).normalized,
                  radius * Mathf.Cos(angle),
                  f / Mathf.Max(0.01f, tiling) + reach * perUnit);
    }

    void AppendRow(List<Vector3> vertices, List<Vector2> uvs, List<int> indices,
                   int start, ref int rows, Vector3 point, Vector3 right, float halfWidth, float along)
    {
        point.y += height;

        // La U se saca del ancho REAL de la fila, no de 0 a 1: en las puntas la fila es más
        // angosta, y estirar la textura de borde a borde ahí haría que el dibujo se apretara
        // justo donde más se mira.
        float u = halfWidth / Mathf.Max(0.01f, width);

        vertices.Add(point - right * halfWidth); uvs.Add(new Vector2(0.5f - u, along));
        vertices.Add(point + right * halfWidth); uvs.Add(new Vector2(0.5f + u, along));

        rows++;
        if (rows < 2) return;

        int a = start + (rows - 2) * 2;
        indices.Add(a);     indices.Add(a + 2); indices.Add(a + 1);
        indices.Add(a + 1); indices.Add(a + 2); indices.Add(a + 3);
    }

    void Finish(List<Vector3> vertices, List<Vector2> uvs, List<int> indices)
    {

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
        bounds.Expand(new Vector3(0f, _definition.Length * layout.spacing, 0f));
        _mesh.bounds = bounds;

        GetComponent<MeshFilter>().sharedMesh   = _mesh;
        GetComponent<MeshRenderer>().sortingOrder = WorldMapDefinition.ORDER_PATH;
    }
}
