using UnityEngine;

// Prototipo 2D del "mundo curvo" (experimento — LevelMapCurve.unity, no toca LevelMap real).
// Técnica real que usan estos mapas (Candy Crush y similares): NO hay cámara 3D ni esfera — es
// un ScrollRect 2D normal (igual que LevelMapController) donde cada nodo calcula SU PROPIA
// escala y desplazamiento horizontal según qué tan lejos está del centro visible del Viewport.
// A diferencia de Lens Distortion (que probamos antes y descartamos), esto no toca la cámara ni
// el post-proceso — es matemática por-nodo en LateUpdate, sobre RectTransforms comunes.
public class CurveMapEffect : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] RectTransform viewport; // define el "centro" de la curva
    [SerializeField] RectTransform content;  // contiene los nodos (se instancian acá)

    [Header("Camino de fondo (tiles repetidos — cada uno se curva por separado, así el camino se dobla de verdad en vez de moverse como un solo bloque rígido)")]
    [SerializeField] RectTransform roadTilePrefab;
    [SerializeField] int   roadTileCount  = 14;
    [SerializeField] float roadTileHeight = 220f;

    [Header("Nodos de prueba (placeholder, no LevelNodeView todavía)")]
    [SerializeField] RectTransform nodePrefab;
    [SerializeField] int   nodeCount    = 15;
    [SerializeField] float nodeSpacingY = 220f;
    [Tooltip("Zigzag izquierda/derecha ANTES de aplicar la curva.")]
    [SerializeField] float zigzagX      = 150f;

    [Header("Curva")]
    [Tooltip("Escala mínima que llegan a tener los nodos en el borde del viewport.")]
    [SerializeField] float edgeScale   = 0.65f;
    [Tooltip("Desplazamiento horizontal máximo (píxeles) para los nodos en el borde del viewport.")]
    [SerializeField] float curveAmount = 150f;
    [Tooltip("1 = curva pareja. Más alto = casi no se nota cerca del centro y se concentra en los bordes.")]
    [SerializeField] float curvePower  = 2f;

    RectTransform[] _nodes;
    Vector2[]       _baseAnchoredPos;

    void Start()
    {
        if (!viewport) { Debug.LogWarning("[CurveMapEffect] Falta asignar 'Viewport' en el Inspector."); return; }
        if (!content)  { Debug.LogWarning("[CurveMapEffect] Falta asignar 'Content' en el Inspector.");  return; }

        var spawned = new System.Collections.Generic.List<RectTransform>();

        // Camino de fondo en pedazos — CADA tile se curva por separado (por eso se dobla de
        // verdad, en vez de moverse como un solo bloque rígido).
        if (roadTilePrefab)
        {
            for (int i = 0; i < roadTileCount; i++)
            {
                var tile = Instantiate(roadTilePrefab, content);
                tile.anchoredPosition = new Vector2(0f, -i * roadTileHeight);
                tile.name = $"RoadTile_{i}";
                spawned.Add(tile);
            }
        }

        // Nodos de prueba, en zigzag.
        if (nodePrefab)
        {
            for (int i = 0; i < nodeCount; i++)
            {
                var node = Instantiate(nodePrefab, content);
                float x = (i % 2 == 0) ? -zigzagX : zigzagX;
                float y = -i * nodeSpacingY;
                node.anchoredPosition = new Vector2(x, y);
                node.name = $"NodeProto_{i}";
                spawned.Add(node);
            }
        }

        // Si no asignaste ningún prefab, toma lo que ya esté puesto a mano como hijo de Content.
        if (spawned.Count == 0)
        {
            for (int i = 0; i < content.childCount; i++)
                spawned.Add(content.GetChild(i) as RectTransform);
        }

        float maxY = 0f;
        foreach (var t in spawned) if (t) maxY = Mathf.Max(maxY, -t.anchoredPosition.y);
        content.sizeDelta = new Vector2(content.sizeDelta.x, maxY + 400f);

        _nodes = spawned.ToArray();
        _baseAnchoredPos = new Vector2[_nodes.Length];
        for (int i = 0; i < _nodes.Length; i++)
            _baseAnchoredPos[i] = _nodes[i] ? _nodes[i].anchoredPosition : Vector2.zero;
    }

    void LateUpdate()
    {
        if (_nodes == null) return;

        float halfHeight = viewport.rect.height * 0.5f;

        for (int i = 0; i < _nodes.Length; i++)
        {
            if (!_nodes[i]) continue;

            // Posición del nodo en el espacio local del Viewport — no importa cómo esté
            // configurado el scroll de Content, esto siempre da la distancia real al centro.
            Vector3 localInViewport = viewport.InverseTransformPoint(_nodes[i].position);
            float t = Mathf.Clamp(localInViewport.y / halfHeight, -1f, 1f);
            float absT = Mathf.Abs(t);

            float scale   = Mathf.Lerp(1f, edgeScale, Mathf.Pow(absT, curvePower));
            float xOffset = Mathf.Sign(t) * curveAmount * Mathf.Pow(absT, curvePower);

            _nodes[i].localScale = Vector3.one * scale;
            _nodes[i].anchoredPosition = _baseAnchoredPos[i] + new Vector2(xOffset, 0f);
        }
    }
}
