using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Posiciona los nodos reales sobre la MISMA curva que dibuja CurvedMapPreview.shader — es la
// transformación inversa del shader, pero "al derecho": de la posición lógica de cada nodo en
// el capítulo (profundidad a lo largo del camino + offset lateral) a su posición/escala en
// pantalla. Lee los parámetros directo del Material del shader, así quedan siempre sincronizados
// (si cambiás Radius/Drop/etc. en el Material, los nodos se acomodan solos).
public class CurvedMapNodePositioner : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El mismo RawImage que tiene el Material de CurvedMapPreview — de ahí se leen los parámetros de la curva.")]
    [SerializeField] RawImage worldPreview;
    [Tooltip("El RectTransform del Canvas (o un contenedor full-screen) — para convertir 0..1 normalizado a posición real.")]
    [SerializeField] RectTransform canvasRect;

    [Header("Datos reales")]
    [Tooltip("Qué capítulo cargar desde Resources/Levels/Chapter_N (JSON reales, vía LevelData) — reemplaza a 'Node Count' cuando encuentra niveles.")]
    [SerializeField] int chapterToLoad = 1;

    [Header("Nodos de prueba (placeholder, no LevelNodeView todavía)")]
    [SerializeField] RectTransform nodePrefab;
    [SerializeField] Transform     nodeParent;
    [Tooltip("Se usa solo si no se pudo cargar ningún nivel real del capítulo (fallback).")]
    [SerializeField] int   nodeCount     = 10;
    [Tooltip("Distancia entre nodos, en 'alturas de pantalla' (misma unidad que Scroll/MapScale).")]
    [SerializeField] float depthSpacing  = 1f;
    [Tooltip("Vaivén lateral (0..0.5, 0 = sin zigzag) alrededor del centro del camino.")]
    [SerializeField] float lateralWander = 0.15f;
    [Tooltip("Margen (en 'alturas de pantalla') para que el último nodo no quede pegado al horizonte cuando se llega al final del scroll.")]
    [SerializeField] float topPadding = 0.25f;
    [Tooltip("Grados máx. de inclinación lateral (eje Z) cuando el nodo está en el borde del camino.")]
    [SerializeField] float tiltSideMax = 20f;
    [Tooltip("Margen (en 'alturas de pantalla') para que el nodo 1 no quede pegado al borde de abajo — lo separa un poco del pie geométrico.")]
    [SerializeField] float bottomPadding = 0.25f;

    // Cuánto hay que avanzar el Scroll (siempre positivo) para llegar al último nodo — el
    // Scroll Max real del capítulo. Lo calcula este script solo, según Node Count/Depth
    // Spacing — no hay ningún valor manual de "largo máximo" en ningún lado.
    public float ScrollMax { get; private set; }

    RectTransform[] _nodes;
    float[] _depths;
    float[] _lateral01;
    Material _material;

    void Start()
    {
        if (!worldPreview) { Debug.LogWarning("[CurvedMapNodePositioner] Falta asignar 'World Preview' en el Inspector."); return; }
        if (!canvasRect)   { Debug.LogWarning("[CurvedMapNodePositioner] Falta asignar 'Canvas Rect' en el Inspector."); return; }
        if (!nodePrefab)   { Debug.LogWarning("[CurvedMapNodePositioner] Falta asignar 'Node Prefab' en el Inspector."); return; }

        _material = worldPreview.material;

        var parent = nodeParent ? nodeParent : transform;
        var levels = LoadChapterLevels(chapterToLoad);
        int count = levels.Count > 0 ? levels.Count : nodeCount;
        if (levels.Count == 0)
            Debug.LogWarning($"[CurvedMapNodePositioner] No se encontraron niveles del capítulo {chapterToLoad} en Resources/Levels — usando {nodeCount} nodos placeholder.");

        _nodes = new RectTransform[count];
        _depths = new float[count];
        _lateral01 = new float[count];

        // Nivel 1 (i=0) pegado al "pie" con Scroll=0, niveles siguientes cada vez más arriba
        // (hacia el horizonte) — 1 abajo, ascendiendo. Scroll nunca baja de 0: arrastrar hacia
        // abajo AVANZA el Scroll y va trayendo los siguientes niveles uno por uno.
        float footY = _material.GetFloat("_FootY");
        for (int i = 0; i < count; i++)
        {
            int levelNumber = levels.Count > 0 ? levels[i].id : i + 1;
            _nodes[i] = Instantiate(nodePrefab, parent);
            _nodes[i].name = levels.Count > 0 ? $"Node_{levelNumber:000}" : $"NodeProto_{i}";
            _depths[i] = footY - bottomPadding - i * depthSpacing;
            _lateral01[i] = 0.5f + Mathf.Sin(i * 0.9f) * lateralWander;

            // Si el prefab es el LevelNodeView real, se configura con Setup() (mismo criterio
            // que LevelMapController) — si no, es un placeholder de prueba, le pone el número
            // encima a mano.
            var nodeView = _nodes[i].GetComponent<LevelNodeView>();
            if (nodeView)
            {
                var state = GetState(levelNumber, SaveManager.MaxUnlockedLevel);
                nodeView.Setup(levelNumber, state, SaveManager.GetLevelStars(levelNumber));
            }
            else
            {
                AddNumberLabel(_nodes[i], levelNumber);
            }
        }

        // El último nodo, al llegar al scroll máximo, queda a 'Top Padding' del HORIZONTE de
        // verdad (radius * 90°) — no de 0/el pie, que es un punto sin relación con "arriba".
        float radius = _material.GetFloat("_Radius");
        float horizonLimit = radius * Mathf.PI * 0.5f;
        ScrollMax = Mathf.Max(0f, bottomPadding + (count - 1) * depthSpacing - (horizonLimit - topPadding));
        // El fondo (shader) también se auto-ajusta — tiene que cubrir el recorrido completo
        // MÁS el rango geométrico base de la curva (FootY + Radius + el padding), no solo
        // cuánto se scrollea.
        _material.SetFloat("_MapScale", ScrollMax + footY + radius + bottomPadding);
        _material.SetFloat("_ScrollMax", ScrollMax + bottomPadding);
    }

    // Mismo criterio que LevelMapController.GetState() — no se puede reusar directo porque es
    // privado ahí, así que se replica acá (misma lógica, una sola fuente de verdad: SaveManager).
    NodeState GetState(int levelId, int maxUnlocked)
    {
        if (levelId > maxUnlocked)  return NodeState.Locked;
        if (levelId == maxUnlocked) return NodeState.Available;
        return SaveManager.IsLevelGold(levelId) ? NodeState.CompleteFirstTry : NodeState.Completed;
    }

    // Mismo patrón que LevelMapController.LoadAllLevels() — carga todos los JSON de niveles
    // (Resources/Levels/Chapter_N/*.json vía LevelData), filtra por capítulo y descarta niveles
    // de prueba (ids >= 900, ej. 999_test_rescue.json), ordenados por id.
    List<LevelData> LoadChapterLevels(int chapter)
    {
        var result = new List<LevelData>();
        var jsons = Resources.LoadAll<TextAsset>("Levels");
        foreach (var json in jsons)
        {
            var lvl = JsonUtility.FromJson<LevelData>(json.text);
            if (lvl != null && lvl.chapter == chapter && lvl.id < 900) result.Add(lvl);
        }
        result.Sort((a, b) => a.id.CompareTo(b.id));
        return result;
    }

    // Placeholder de debug: número grande centrado sobre el nodo, para verificar de un vistazo
    // el orden en el que aparecen (no es LevelNodeView todavía, eso viene después).
    void AddNumberLabel(RectTransform node, int number)
    {
        var labelGO = new GameObject("Label", typeof(RectTransform));
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.SetParent(node, false);
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        var text = labelGO.AddComponent<TextMeshProUGUI>();
        text.text = number.ToString();
        text.fontSize = 48;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.black;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
    }

    void LateUpdate()
    {
        if (_nodes == null || _material == null) return;

        float scroll   = _material.GetFloat("_Scroll");
        float radius   = _material.GetFloat("_Radius");
        float horizonY = _material.GetFloat("_HorizonY");
        float footY    = _material.GetFloat("_FootY");
        float drop     = _material.GetFloat("_Drop");
        float xStretch = _material.GetFloat("_XStretch");

        float span = footY - horizonY;
        Vector2 size = canvasRect.rect.size;

        for (int i = 0; i < _nodes.Length; i++)
        {
            var node = _nodes[i];
            if (!node) continue;

            // Misma fórmula que usa el shader (sourceY = footY - vDepth - scroll), despejada
            // al revés: acá "depth" del nodo hace del rol de sourceY fijo en el atlas.
            float vDepth = footY - _depths[i] - scroll;
            float th = vDepth / radius;

            // Más allá del horizonte (o detrás de cámara) -> no se dibuja.
            if (th <= -Mathf.PI * 0.5f || th >= Mathf.PI * 0.5f)
            {
                node.gameObject.SetActive(false);
                continue;
            }

            float cosTh = Mathf.Cos(th);
            float sinTh = Mathf.Sin(th);

            // Paso 1 (al derecho): profundidad -> "layer space" + estiramiento horizontal.
            float syLayer = footY - radius * sinTh;
            float stretch = 1f + xStretch * cosTh;
            float screenX01 = 0.5f + (_lateral01[i] - 0.5f) * stretch;

            // Paso 2 (al derecho): arco esférico, según la columna X donde cayó el nodo.
            float xn = (screenX01 - 0.5f) * 2f;
            float off = drop * xn * xn;
            float sc = (span - off) / span;

            float screenY01 = (syLayer < footY) ? footY - sc * (footY - syLayer) : syLayer;

            if (screenX01 < -0.05f || screenX01 > 1.05f || screenY01 < -0.05f || screenY01 > 1.05f)
            {
                node.gameObject.SetActive(false);
                continue;
            }

            node.gameObject.SetActive(true);

            // Posición en MUNDO (no anchoredPosition) — así da igual si el padre real del nodo
            // no es el Canvas directo: Transform.position siempre es absoluto, sin sumar
            // desplazamientos del padre (mismo fix que CurvedMapElementPositioner).
            float px = (screenX01 - 0.5f) * size.x;
            float py = (0.5f - screenY01) * size.y;
            node.position = canvasRect.TransformPoint(new Vector3(px, py, 0f));

            // Sin achique de tamaño — en cambio, se inclina como si estuviera parado sobre la
            // superficie curva de la esfera (eje X = a lo largo del camino, eje Z = de lado).
            node.localScale = Vector3.one;
            float tiltX = -th * Mathf.Rad2Deg;
            float tiltZ = -xn * tiltSideMax;
            node.localRotation = Quaternion.Euler(tiltX, 0f, tiltZ);
        }
    }
}
