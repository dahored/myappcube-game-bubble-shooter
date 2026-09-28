using UnityEngine;
using UnityEngine.UI;

// Degradado vertical sobre cualquier Image, sin sprite.
//
// La UI de Unity no tiene degradados, pero sí color POR VÉRTICE: esto engancha la malla que ya
// genera el Image y pinta los vértices de arriba de un color y los de abajo de otro. El resultado
// es el mismo que una textura degradada, pero sin textura: no ocupa memoria, no hay que exportar
// nada y se adapta a cualquier tamaño sin estirarse ni perder nitidez.
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

    [Tooltip("Dónde queda el corte entre los dos colores, de abajo (0) a arriba (1). En 0,5 el degradado es parejo.")]
    [Range(0f, 1f)]
    [SerializeField] float middle = 0.5f;

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive() || mesh.currentVertCount == 0) return;

        // Los límites salen de la malla y no del RectTransform: así vale igual para un Image
        // simple de cuatro vértices que para uno en Sliced, que genera nueve trozos.
        var  vertex = new UIVertex();
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

            float t = Mathf.Clamp01((vertex.position.y - min) / height);
            t = middle <= 0f ? 1f
              : middle >= 1f ? 0f
              : t < middle ? Mathf.InverseLerp(0f, middle, t) * 0.5f
                           : 0.5f + Mathf.InverseLerp(middle, 1f, t) * 0.5f;

            Color tint = Color.Lerp(bottom, top, t);
            Color had  = vertex.color;

            vertex.color = new Color(had.r * tint.r, had.g * tint.g, had.b * tint.b, had.a * tint.a);
            mesh.SetUIVertex(vertex, i);
        }
    }
}
