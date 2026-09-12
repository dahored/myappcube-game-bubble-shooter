using UnityEngine;
using UnityEngine.Rendering;

// Reemplaza el mesh del Sphere que trae Unity (24 gajos x 16 anillos) por una esfera UV generada
// con la resolución que se pida. A la escala del mundo del mapa, la esfera de Unity se ve
// facetada: el horizonte queda con tramos rectos y se notan los bloques en el agua.
//
// Mantiene el mismo radio 0.5 y el mismo mapeo de UV que el mesh original, así que todo lo que ya
// está posicionado sobre la esfera (nodos, textura) sigue calzando igual — solo cambia el detalle.
//
// Corre también en el editor, así se ve el resultado sin darle Play.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class SmoothSphereMesh : MonoBehaviour
{
    [Tooltip("Gajos alrededor del ecuador. Más = horizonte más redondo.")]
    [Range(8, 256)]
    [SerializeField] int segments = 96;
    [Tooltip("Anillos de polo a polo.")]
    [Range(4, 128)]
    [SerializeField] int rings    = 48;

    Mesh _mesh;
    bool _dirty = true;

    void OnEnable()   => _dirty = true;
    void OnValidate() => _dirty = true;

    void Update()
    {
        if (!_dirty) return;
        _dirty = false;
        Rebuild();
    }

    void Rebuild()
    {
        var filter = GetComponent<MeshFilter>();
        if (!filter) return;

        // Se reusa siempre la misma instancia en vez de crear una nueva: si no, cada cambio en el
        // Inspector dejaría un mesh huérfano en memoria. 'DontSave' evita además que se guarde
        // dentro de la escena, ya que se regenera solo al cargar.
        if (!_mesh)
        {
            _mesh = new Mesh { name = "SmoothSphere", hideFlags = HideFlags.DontSave };
        }
        _mesh.Clear();

        int cols = segments + 1; // el gajo del final repite el del principio, para cerrar la UV
        int rows = rings + 1;

        var verts = new Vector3[cols * rows];
        var norms = new Vector3[cols * rows];
        var uvs   = new Vector2[cols * rows];

        for (int y = 0; y < rows; y++)
        {
            float v      = (float)y / rings;
            float phi    = v * Mathf.PI; // 0 = polo norte, PI = polo sur
            float sinPhi = Mathf.Sin(phi);
            float cosPhi = Mathf.Cos(phi);

            for (int x = 0; x < cols; x++)
            {
                float u     = (float)x / segments;
                float theta = u * Mathf.PI * 2f;

                var normal = new Vector3(sinPhi * Mathf.Sin(theta), cosPhi, sinPhi * Mathf.Cos(theta));
                int i = y * cols + x;

                norms[i] = normal;
                verts[i] = normal * 0.5f; // mismo radio que el mesh de Unity
                uvs[i]   = new Vector2(u, 1f - v);
            }
        }

        var tris = new int[segments * rings * 6];
        int t = 0;
        for (int y = 0; y < rings; y++)
        {
            for (int x = 0; x < segments; x++)
            {
                int a = y * cols + x;
                int b = a + 1;
                int c = a + cols;
                int d = c + 1;

                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }
        }

        // Con muchas subdivisiones se pasa el límite de 65535 vértices de los índices de 16 bits.
        _mesh.indexFormat = verts.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        _mesh.vertices    = verts;
        _mesh.normals     = norms;
        _mesh.uv          = uvs;
        _mesh.triangles   = tris;
        _mesh.RecalculateBounds();

        filter.sharedMesh = _mesh;
    }
}
