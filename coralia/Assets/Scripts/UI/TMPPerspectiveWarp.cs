using TMPro;
using UnityEngine;

// Warp de 4 esquinas para un TMP_Text — cada esquina se mueve independiente en X/Y, para
// calzar el texto sobre una imagen con perspectiva (ej. el cartel de madera del HUD de
// disparos). RectTransform no puede lograr esto solo con rotación: el Canvas Screen Space -
// Overlay es ortográfico, no hay trapecio real sin tocar los vértices de la malla a mano.
//
// [ExecuteAlways]: se ve en tiempo real en el Editor (sin necesidad de Play) para poder
// ajustar los 4 offsets a ojo hasta que calce con el cartel (pedido de Diego).
[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class TMPPerspectiveWarp : MonoBehaviour
{
    [Header("Offset de cada esquina (unidades locales del texto)")]
    [SerializeField] Vector2 topLeft;
    [SerializeField] Vector2 topRight;
    [SerializeField] Vector2 bottomLeft;
    [SerializeField] Vector2 bottomRight;

    TMP_Text _text;
    string   _lastText;

    void Awake() => _text = GetComponent<TMP_Text>();

    void OnEnable()
    {
        if (!_text) _text = GetComponent<TMP_Text>();
        ApplyWarp();
    }

    // Cambiar cualquier campo en el Inspector dispara esto — así se ve el resultado en vivo
    // en el Editor, sin entrar a Play mode.
    void OnValidate()
    {
        if (!_text) _text = GetComponent<TMP_Text>();
        if (!_text) return;
        ApplyWarp();
    }

    void LateUpdate()
    {
        if (!_text) return;
        // En Play mode el texto cambia por código (ShotsLabel), no por el Inspector — hay que
        // reaplicar el warp cada vez que cambia, si no queda "pegado" a la forma del texto
        // anterior (distinta cantidad de dígitos = distinto ancho de bloque).
        if (_text.text == _lastText) return;
        _lastText = _text.text;
        ApplyWarp();
    }

    void ApplyWarp()
    {
        _text.ForceMeshUpdate();
        var textInfo = _text.textInfo;
        int count = textInfo.characterCount;
        if (count == 0) return;

        // Bounds de TODO el bloque de texto (no por carácter) — el warp se calcula una vez
        // sobre el bloque completo, así "99" queda como un solo trapecio, no dos por separado.
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < count; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;
            int vi = textInfo.characterInfo[i].vertexIndex;
            var verts = textInfo.meshInfo[textInfo.characterInfo[i].materialReferenceIndex].vertices;
            for (int k = 0; k < 4; k++)
            {
                minX = Mathf.Min(minX, verts[vi + k].x); maxX = Mathf.Max(maxX, verts[vi + k].x);
                minY = Mathf.Min(minY, verts[vi + k].y); maxY = Mathf.Max(maxY, verts[vi + k].y);
            }
        }
        if (minX > maxX) return; // nada visible
        float width  = Mathf.Max(0.0001f, maxX - minX);
        float height = Mathf.Max(0.0001f, maxY - minY);

        for (int i = 0; i < count; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;
            int mat = textInfo.characterInfo[i].materialReferenceIndex;
            int vi  = textInfo.characterInfo[i].vertexIndex;
            var verts = textInfo.meshInfo[mat].vertices;

            for (int k = 0; k < 4; k++)
            {
                Vector3 v = verts[vi + k];
                float t = (v.x - minX) / width;  // 0 = borde izquierdo, 1 = borde derecho
                float u = (v.y - minY) / height; // 0 = borde inferior, 1 = borde superior

                // Interpolación bilineal entre los 4 offsets de esquina — cada vértice recibe
                // la mezcla que le toca según dónde cae dentro del bloque completo.
                Vector2 offset = Vector2.Lerp(
                    Vector2.Lerp(bottomLeft, bottomRight, t),
                    Vector2.Lerp(topLeft, topRight, t),
                    u);

                verts[vi + k] = v + (Vector3)offset;
            }

            textInfo.meshInfo[mat].vertices = verts;
        }

        for (int m = 0; m < textInfo.meshInfo.Length; m++)
        {
            textInfo.meshInfo[m].mesh.vertices = textInfo.meshInfo[m].vertices;
            _text.UpdateGeometry(textInfo.meshInfo[m].mesh, m);
        }
    }
}
