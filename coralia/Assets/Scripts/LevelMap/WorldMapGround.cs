using UnityEngine;

// El suelo del mapa: un plano en XZ generado por código, subdividido.
//
// Subdividido porque la curvatura mueve VÉRTICES. Un quad de cuatro esquinas no se dobla: se
// inclina. Para que el terreno se hunda parejo hacia el horizonte hace falta que tenga vértices
// repartidos a lo largo, y cuanto más lejos llega, más necesita.
//
// Generado por código y no como asset para poder cambiar largo y densidad desde el Inspector sin
// reexportar nada — la densidad es el primer número que hay que buscar a ojo en el dispositivo.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WorldMapGround : MonoBehaviour, IWorldMapRebuildable
{
    [Tooltip("Cuánto sobra de suelo a cada lado del zigzag.")]
    [SerializeField] float sideMargin = 10f;

    [Tooltip("Cuánto sobra de suelo antes del primer nivel y después del último.")]
    [SerializeField] float endMargin = 12f;

    [Tooltip("Cuántos cortes por unidad de mundo. Más alto es más suave y más caro.")]
    [SerializeField] float density = 0.5f;

    [Tooltip("Cada cuántas unidades de mundo se repite la textura.")]
    [SerializeField] float tiling = 8f;

    WorldMapDefinition _definition;
    Mesh _mesh;

    void OnEnable()  => Rebuild();
    void OnValidate() => Rebuild();

    public void Rebuild()
    {
        _definition = WorldMapDefinition.For(this);
        if (_definition == null) return;

        // El tamaño sale del recorrido, no de dos números escritos a mano: al cambiar el zigzag
        // o la separación entre niveles el suelo acompaña solo, sin quedar corto ni sobrar.
        float width  = _definition.Layout.zigzag * 2f + sideMargin * 2f;
        float length = _definition.Length + endMargin * 2f;

        int cols = Mathf.Max(1, Mathf.RoundToInt(width  * density));
        int rows = Mathf.Max(1, Mathf.RoundToInt(length * density));

        var vertices = new Vector3[(cols + 1) * (rows + 1)];
        var uvs      = new Vector2[vertices.Length];
        var indices  = new int[cols * rows * 6];

        for (int z = 0, v = 0; z <= rows; z++)
        for (int x = 0; x <= cols; x++, v++)
        {
            float px = (x / (float)cols - 0.5f) * width;

            // Arranca antes del primer nivel: el camino tiene que nacer con suelo por detrás,
            // no en el borde mismo del mundo.
            float pz =  z / (float)rows * length - endMargin;

            vertices[v] = new Vector3(px, 0f, pz);
            uvs[v]      = new Vector2(px / tiling, pz / tiling);
        }

        for (int z = 0, i = 0; z < rows; z++)
        for (int x = 0; x < cols; x++)
        {
            int a = z * (cols + 1) + x;
            int b = a + cols + 1;

            indices[i++] = a;     indices[i++] = b;     indices[i++] = a + 1;
            indices[i++] = a + 1; indices[i++] = b;     indices[i++] = b + 1;
        }

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "WorldMapGround" };
            _mesh.hideFlags = HideFlags.DontSave; // generado: no tiene por qué ensuciar la escena
        }

        _mesh.Clear();

        // Un plano largo pasa de 65k vértices enseguida con densidad alta; sin esto Unity lo
        // corta en silencio y el suelo aparece a la mitad.
        _mesh.indexFormat = vertices.Length > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        _mesh.vertices  = vertices;
        _mesh.uv        = uvs;
        _mesh.triangles = indices;
        _mesh.RecalculateBounds();

        // Los bounds los calcula Unity sobre los vértices PLANOS, pero el shader los hunde en Y
        // al dibujar. Sin agrandarlos, el suelo desaparece al mirar hacia el horizonte porque
        // Unity cree que quedó fuera de cuadro.
        var bounds = _mesh.bounds;
        bounds.Expand(new Vector3(0f, length, 0f));
        _mesh.bounds = bounds;

        GetComponent<MeshFilter>().sharedMesh = _mesh;
    }
}
