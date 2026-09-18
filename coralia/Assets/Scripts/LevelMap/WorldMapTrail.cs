using System.Collections.Generic;
using UnityEngine;

// La banda que une un nodo con el siguiente, al estilo del camino de Candy Crush: una cinta
// angosta que corre por el centro del camino y se corta en cada nodo.
//
// Es UNA sola malla con todos los tramos, no un objeto por tramo: son puntos que nunca se mueven
// y con 180 niveles serían 179 draw calls para dibujar una línea.
//
// Va aparte de WorldMapPathRibbon aunque sigan el mismo trazado porque son dos cosas distintas:
// el camino es el terreno y esta banda es la señalización encima. Se cortan en sitios distintos
// y llevan material distinto.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WorldMapTrail : MonoBehaviour, IWorldMapRebuildable
{
    [Tooltip("Ancho de la banda, en unidades de mundo. Bastante más angosta que el camino.")]
    [SerializeField] float width = 0.5f;

    [Tooltip("Cuánto se levanta del suelo. Entre el camino y los nodos, para que la cinta no la tape y ella no tape los nodos.")]
    [SerializeField] float height = 0.04f;

    [Tooltip("Cuánto se corta antes y después de cada nodo, medido en niveles. En 0 llega hasta el centro del nodo y queda tapada por el disco.")]
    [Range(0f, 0.5f)]
    [SerializeField] float nodeGap = 0.12f;

    [Tooltip("Cuántos cortes por tramo. Más alto es más suave en las curvas del zigzag.")]
    [SerializeField] int steps = 8;

    [Header("Volumen")]
    [Tooltip("Cuánto del ancho se va en el canto redondeado. 0 es un rectángulo de cantos vivos; 1 es un cilindro. En el medio, una losa con el borde matado.")]
    [Range(0f, 1f)]
    [SerializeField] float bevel = 0.3f;

    [Tooltip("Cuántos puntos tiene cada canto. Más alto lo redondea más fino.")]
    [Range(1, 8)]
    [SerializeField] int bevelSteps = 3;

    [Tooltip("Cuánto se levanta el centro respecto de los bordes. Es el bombeado de la banda.")]
    [SerializeField] float thickness = 0.12f;

    [Tooltip("Cuánto se oscurecen los bordes. Como el shader es unlit no hay luz que dé el relieve: lo da este degradado.")]
    [Range(0f, 1f)]
    [SerializeField] float edgeShade = 0.35f;

    // La sección transversal, calculada una vez por reconstrucción: u de -1 a 1 a lo ancho, y
    // cuánto se levanta cada punto (1 = cara de arriba, 0 = apoyado en el suelo).
    float[] _profileU = { -1f, 1f };
    float[] _profileY = {  0f, 0f };

    WorldMapDefinition _definition;
    Mesh _mesh;
    bool _needsRebuild = true;

    void OnEnable()   => _needsRebuild = true;
    void OnValidate() => _needsRebuild = true;

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

        BuildProfile();

        var layout = _definition.Layout;
        int levels = _definition.Levels;

        var vertices = new List<Vector3>();
        var uvs      = new List<Vector2>();
        var colors   = new List<Color>();
        var indices  = new List<int>();

        // Un tramo por hueco entre nodos. Cada uno arranca y termina por separado: si fuera una
        // cinta continua con agujeros, los bordes de cada corte compartirían vértices con el
        // tramo siguiente y los triángulos cruzarían el nodo.
        for (int i = 0; i < levels - 1; i++)
            AppendSegment(vertices, uvs, colors, indices, i + nodeGap, i + 1 - nodeGap, layout);

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "WorldMapTrail" };
            _mesh.hideFlags = HideFlags.DontSave;
        }

        _mesh.Clear();
        _mesh.indexFormat = vertices.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        _mesh.SetVertices(vertices);
        _mesh.SetUVs(0, uvs);
        _mesh.SetColors(colors);
        _mesh.SetTriangles(indices, 0);
        _mesh.RecalculateBounds();

        // Mismo motivo que el suelo y el camino: los bounds salen de los vértices planos, pero el
        // shader los hunde al dibujar. Sin agrandarlos, la banda desaparece al mirar lejos.
        var bounds = _mesh.bounds;
        bounds.Expand(new Vector3(0f, _definition.Length, 0f));
        _mesh.bounds = bounds;

        GetComponent<MeshFilter>().sharedMesh = _mesh;
    }

    // Perfil de losa: plano arriba y un cuarto de círculo en cada canto. Los puntos se reparten
    // SOLO en los cantos — la cara de arriba no necesita ninguno en el medio, es recta, y gastar
    // vértices ahí dejaría los cantos angulosos justo donde se nota.
    void BuildProfile()
    {
        float b = Mathf.Clamp01(bevel);

        if (b < 0.001f)
        {
            // Cantos vivos: la cara de arriba y una pared vertical a cada lado.
            _profileU = new[] { -1f, -1f, 1f, 1f };
            _profileY = new[] {  0f,  1f, 1f, 0f };
            return;
        }

        int steps = Mathf.Max(1, bevelSteps);
        float flat = 1f - b;

        var us = new List<float>();
        var ys = new List<float>();

        // Canto izquierdo, de afuera hacia adentro.
        for (int i = 0; i <= steps; i++)
        {
            float k = i / (float)steps;
            us.Add(-1f + b * k);
            ys.Add(Mathf.Sqrt(Mathf.Max(0f, 1f - (1f - k) * (1f - k))));
        }

        // Canto derecho, en espejo. La cara de arriba queda cubierta por el tramo entre el último
        // punto de un canto y el primero del otro.
        for (int i = steps; i >= 0; i--)
        {
            float k = i / (float)steps;
            us.Add(1f - b * k);
            ys.Add(Mathf.Sqrt(Mathf.Max(0f, 1f - (1f - k) * (1f - k))));
        }

        _profileU = us.ToArray();
        _profileY = ys.ToArray();
    }

    void AppendSegment(List<Vector3> vertices, List<Vector2> uvs, List<Color> colors, List<int> indices,
                       float from, float to, WorldMapPath.Layout layout)
    {
        if (to <= from) return;

        int across = _profileU.Length - 1;
        int cuts   = Mathf.Max(1, steps);
        int start  = vertices.Count;

        // Cuánto tarda la banda en abrirse a su ancho completo, medido en niveles: media anchura.
        // Así la punta se redondea con el mismo radio que tiene de ancho y queda una pastilla, en
        // vez de un corte recto o una punta afilada.
        float cap = width * 0.5f / Mathf.Max(0.01f, layout.spacing);

        for (int i = 0; i <= cuts; i++)
        {
            float f = Mathf.Lerp(from, to, i / (float)cuts);
            WorldMapPath.SampleFrame(f, layout, out var point, out var forward);

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            point.y += height;

            // Redondeo de las puntas: a 'cap' del extremo la banda tiene su ancho completo, y de
            // ahí hasta el borde se cierra siguiendo un cuarto de círculo.
            float toEnd = Mathf.Min(f - from, to - f);
            float taper = cap > 0.0001f && toEnd < cap
                ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(1f - toEnd / cap, 2f)))
                : 1f;

            for (int j = 0; j < _profileU.Length; j++)
            {
                float u     = _profileU[j];
                float raise = _profileY[j];

                vertices.Add(point + right * (u * width * 0.5f * taper) + Vector3.up * (raise * thickness * taper));
                uvs.Add(new Vector2((u + 1f) * 0.5f, i / (float)cuts));

                // El sombreado sigue el perfil: la cara de arriba queda pareja y solo se oscurece
                // el canto. Si siguiera la posición, la losa se vería degradada de lado a lado y
                // perdería la cara plana.
                float shade = 1f - edgeShade * (1f - raise);
                colors.Add(new Color(shade, shade, shade, 1f));
            }

            if (i == 0) continue;

            int row  = start + i * (across + 1);
            int prev = row - (across + 1);

            for (int j = 0; j < across; j++)
            {
                indices.Add(prev + j);     indices.Add(row + j);      indices.Add(prev + j + 1);
                indices.Add(prev + j + 1); indices.Add(row + j);      indices.Add(row + j + 1);
            }
        }
    }
}
