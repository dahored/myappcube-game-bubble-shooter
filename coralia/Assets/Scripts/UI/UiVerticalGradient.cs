using UnityEngine;
using UnityEngine.UI;

// Degradado vertical sobre cualquier Image, sin sprite.
//
// La UI de Unity no tiene degradados, pero sí color POR VÉRTICE: esto engancha la malla que ya
// genera el Image y la pinta. El resultado es el mismo que una textura degradada, pero sin
// textura: no ocupa memoria, no hay que exportar nada y se adapta a cualquier tamaño sin
// estirarse ni perder nitidez.
//
// La malla se SUBDIVIDE en franjas horizontales antes de pintarla. Un Image sin sprite trae solo
// cuatro vértices, uno por esquina, y el color entre ellos se interpola en línea recta: por mucho
// que se calcule una curva, el degradado siempre saldría parejo de arriba abajo. Con franjas, el
// color se evalúa en cada altura y entonces sí puede quedarse sólido en un tramo y desvanecerse
// en otro.
//
// Multiplica el color que ya traía cada vértice en vez de reemplazarlo, así el color del propio
// Image y el alfa de un CanvasGroup por encima siguen funcionando.
//
// Solo para Image y otros Graphic normales. TMP_Text genera su malla por su cuenta y no pasa por
// acá; para un texto con degradado se usa el Color Gradient que TMP ya trae.
[RequireComponent(typeof(Graphic))]
public class UiVerticalGradient : BaseMeshEffect
{
    [SerializeField] Color top    = new(0f, 0f, 0f, 0.85f);
    [SerializeField] Color bottom = new(0f, 0f, 0f, 0f);

    [Tooltip("Entre qué alturas ocurre el cambio, de abajo (0) a arriba (1). Por debajo de X todo es el color de abajo y por encima de Y todo es el de arriba. (0, 1) es un degradado parejo; (0, 0.5) deja la mitad de arriba sólida y desvanece solo la de abajo.")]
    [SerializeField] Vector2 fadeRange = new(0f, 1f);

    [Tooltip("En cuántas franjas se parte la malla. Más franjas es más suave donde el color cambia rápido; por debajo de 2 no se subdivide y el degradado vuelve a ser recto.")]
    [Range(1, 64)]
    [SerializeField] int steps = 16;

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive() || mesh.currentVertCount == 0) return;

        // Subdividir supone reconstruir la malla desde cero, y eso solo es seguro con el quad
        // simple de un Image normal. Un Sliced o un Filled generan sus propios trozos y rehacerlos
        // los rompería, así que ahí se cae al tinte por vértice de toda la vida.
        if (steps > 1 && mesh.currentVertCount == 4) Subdivide(mesh);
        else                                         TintInPlace(mesh);
    }

    void Subdivide(VertexHelper mesh)
    {
        var corners = new UIVertex[4];
        for (int i = 0; i < 4; i++) mesh.PopulateUIVertex(ref corners[i], i);

        // Las esquinas se identifican por su posición y no por el orden en que vengan: así no
        // depende de un detalle interno de cómo Image arma su quad.
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var corner in corners)
        {
            minX = Mathf.Min(minX, corner.position.x); maxX = Mathf.Max(maxX, corner.position.x);
            minY = Mathf.Min(minY, corner.position.y); maxY = Mathf.Max(maxY, corner.position.y);
        }

        UIVertex bottomLeft = Corner(corners, minX, minY), topLeft  = Corner(corners, minX, maxY);
        UIVertex bottomRight= Corner(corners, maxX, minY), topRight = Corner(corners, maxX, maxY);

        mesh.Clear();

        for (int row = 0; row <= steps; row++)
        {
            float f    = row / (float)steps;
            Color tint = Color.Lerp(bottom, top, Blend(f));

            mesh.AddVert(Multiply(Lerp(bottomLeft,  topLeft,  f), tint));
            mesh.AddVert(Multiply(Lerp(bottomRight, topRight, f), tint));
        }

        for (int row = 0; row < steps; row++)
        {
            int at = row * 2;
            mesh.AddTriangle(at, at + 2, at + 3);
            mesh.AddTriangle(at, at + 3, at + 1);
        }
    }

    void TintInPlace(VertexHelper mesh)
    {
        var   vertex = new UIVertex();
        float min = float.MaxValue, max = float.MinValue;

        for (int i = 0; i < mesh.currentVertCount; i++)
        {
            mesh.PopulateUIVertex(ref vertex, i);
            min = Mathf.Min(min, vertex.position.y);
            max = Mathf.Max(max, vertex.position.y);
        }

        float height = Mathf.Max(0.0001f, max - min);

        for (int i = 0; i < mesh.currentVertCount; i++)
        {
            mesh.PopulateUIVertex(ref vertex, i);

            float f = (vertex.position.y - min) / height;
            mesh.SetUIVertex(Multiply(vertex, Color.Lerp(bottom, top, Blend(f))), i);
        }
    }

    // Cuánto pesa el color de arriba a esa altura. Fuera del rango se queda plano, que es lo que
    // permite tener un tramo sólido en vez de un degradado que arranca en el borde.
    float Blend(float f)
    {
        float from = Mathf.Min(fadeRange.x, fadeRange.y);
        float to   = Mathf.Max(fadeRange.x, fadeRange.y);

        return to - from < 0.0001f ? (f >= to ? 1f : 0f) : Mathf.Clamp01((f - from) / (to - from));
    }

    static UIVertex Corner(UIVertex[] corners, float x, float y)
    {
        foreach (var corner in corners)
            if (Mathf.Approximately(corner.position.x, x) && Mathf.Approximately(corner.position.y, y))
                return corner;

        return corners[0];
    }

    static UIVertex Lerp(UIVertex a, UIVertex b, float t) => new()
    {
        position = Vector3.Lerp(a.position, b.position, t),
        normal   = a.normal,
        tangent  = a.tangent,
        uv0      = Vector2.Lerp(a.uv0, b.uv0, t),
        uv1      = Vector2.Lerp(a.uv1, b.uv1, t),
        color    = Color32.Lerp(a.color, b.color, t),
    };

    static UIVertex Multiply(UIVertex vertex, Color tint)
    {
        Color had = vertex.color;
        vertex.color = new Color(had.r * tint.r, had.g * tint.g, had.b * tint.b, had.a * tint.a);
        return vertex;
    }
}
