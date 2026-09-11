using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Versión reutilizable de CurvedMapNodePositioner — posiciona CUALQUIER grupo de elementos
// (nodos, decoraciones a los costados, lo que sea) sobre la misma curva que dibuja
// CurvedMapPreview.shader. Se puede usar varias veces en la escena: uno para los nodos, uno
// para las decoraciones de la izquierda, uno para las de la derecha, etc. — cada instancia lee
// los parámetros del mismo Material, así todo queda sincronizado con el scroll.
public class CurvedMapElementPositioner : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El RawImage que tiene el Material de CurvedMapPreview.")]
    [SerializeField] RawImage worldPreview;
    [Tooltip("El RectTransform del Canvas — para convertir 0..1 normalizado a posición real.")]
    [SerializeField] RectTransform canvasRect;

    [Header("Elementos")]
    [Tooltip("Si lo asignás, este componente instancia 'Spawn Count' copias solo. Si lo dejás vacío, toma los hijos que YA tenga este GameObject (para decoraciones puestas a mano en el Editor).")]
    [SerializeField] RectTransform elementPrefab;
    [SerializeField] int   spawnCount   = 0;
    [Tooltip("Distancia entre elementos consecutivos, en 'alturas de pantalla' (misma unidad que Scroll/Map Scale del shader).")]
    [SerializeField] float depthSpacing = 1f;
    [Tooltip("Profundidad del primer elemento — para desfasar decoraciones respecto a los nodos.")]
    [SerializeField] float depthOffset  = 0f;
    [Tooltip("0 = borde izquierdo del camino, 0.5 = centro, 1 = borde derecho.")]
    [SerializeField] float lateralBase    = 0.5f;
    [Tooltip("Vaivén alrededor de 'Lateral Base' (0 = fijo a un lado, sin vaivén — típico para decoraciones).")]
    [SerializeField] float lateralWander  = 0f;
    [Tooltip("Grados máx. de inclinación hacia atrás cerca del horizonte — casi no se nota cerca del pie, crece fuerte recién cerca del final.")]
    [SerializeField] float tiltNearHorizonMax = 60f;
    [Tooltip("Qué tan cerca del borde (en 0..1 de pantalla) empieza el desvanecido — evita que aparezcan/desaparezcan de golpe.")]
    [SerializeField] float edgeFadeMargin = 0.05f;
    [Tooltip("Igual que 'Edge Fade Margin' pero para el corte del horizonte (en radianes, valores chicos).")]
    [SerializeField] float horizonFadeMargin = 0.05f;

    RectTransform[] _elements;
    CanvasGroup[]   _canvasGroups;
    float[] _depths;
    float[] _lateral01;
    float[] _lastTh; // para reordenar por profundidad real cada frame
    int[]   _sortIndices;
    Material _material;

    void Start()
    {
        if (!worldPreview) { Debug.LogWarning("[CurvedMapElementPositioner] Falta asignar 'World Preview' en el Inspector."); return; }
        if (!canvasRect)   { Debug.LogWarning("[CurvedMapElementPositioner] Falta asignar 'Canvas Rect' en el Inspector.");  return; }

        _material = worldPreview.material;

        var list = new List<RectTransform>();

        if (elementPrefab && spawnCount > 0)
        {
            for (int i = 0; i < spawnCount; i++)
            {
                var el = Instantiate(elementPrefab, transform);
                el.name = $"Element_{i}";
                list.Add(el);
            }
        }
        else
        {
            for (int i = 0; i < transform.childCount; i++)
                list.Add(transform.GetChild(i) as RectTransform);
        }

        // El primer elemento arranca pegado al "pie" con Scroll=0 (nunca negativo), los
        // siguientes necesitan Scroll cada vez más alto — mismo criterio que
        // CurvedMapNodePositioner. 'Depth Offset' sirve para desfasar (ej. decoraciones entre nodos).
        float footY = _material.GetFloat("_FootY");
        _elements = list.ToArray();
        _canvasGroups = new CanvasGroup[_elements.Length];
        _depths = new float[_elements.Length];
        _lateral01 = new float[_elements.Length];
        _lastTh = new float[_elements.Length];
        _sortIndices = new int[_elements.Length];
        for (int i = 0; i < _elements.Length; i++)
        {
            _depths[i] = footY - depthOffset - i * depthSpacing;
            _lateral01[i] = Mathf.Clamp01(lateralBase + Mathf.Sin(i * 0.9f) * lateralWander);
            if (_elements[i])
            {
                _canvasGroups[i] = _elements[i].GetComponent<CanvasGroup>();
                if (!_canvasGroups[i]) _canvasGroups[i] = _elements[i].gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    void LateUpdate()
    {
        if (_elements == null || _material == null) return;

        float scroll   = _material.GetFloat("_Scroll");
        float radius   = _material.GetFloat("_Radius");
        float horizonY = _material.GetFloat("_HorizonY");
        float footY    = _material.GetFloat("_FootY");
        float drop     = _material.GetFloat("_Drop");
        float xStretch = _material.GetFloat("_XStretch");

        float span = footY - horizonY;
        Vector2 size = canvasRect.rect.size;

        for (int i = 0; i < _elements.Length; i++)
        {
            var el = _elements[i];
            if (!el) continue;

            // Misma fórmula que usa el shader (sourceY = footY - vDepth - scroll), despejada
            // al revés: acá "depth" del elemento hace del rol de sourceY fijo en el atlas.
            float vDepth = footY - _depths[i] - scroll;
            float th = vDepth / radius;
            _lastTh[i] = th; // para el reordenamiento de más abajo

            // Cerca del límite del horizonte (±90°) ya empieza a desvanecerse, en vez de
            // desaparecer de golpe al cruzarlo.
            float alpha = Mathf.Clamp01((Mathf.PI * 0.5f - Mathf.Abs(th)) / horizonFadeMargin);

            if (alpha <= 0.001f)
            {
                el.gameObject.SetActive(false);
                continue;
            }

            float cosTh = Mathf.Cos(th);
            float sinTh = Mathf.Sin(th);

            float syLayer = footY - radius * sinTh;
            float stretch = 1f + xStretch * cosTh;
            float screenX01 = 0.5f + (_lateral01[i] - 0.5f) * stretch;

            float xn = (screenX01 - 0.5f) * 2f;
            float off = drop * xn * xn;
            float sc = (span - off) / span;

            float screenY01 = (syLayer < footY) ? footY - sc * (footY - syLayer) : syLayer;

            // 100% visible mientras esté DENTRO de la pantalla (0..1) — el desvanecido recién
            // arranca una vez que ya cruzó el borde real, no antes.
            float pastBottom = Mathf.Max(0f, screenY01 - 1f);
            float pastTop    = Mathf.Max(0f, -screenY01);
            float pastRight  = Mathf.Max(0f, screenX01 - 1f);
            float pastLeft   = Mathf.Max(0f, -screenX01);
            alpha *= Mathf.Clamp01(1f - pastBottom / edgeFadeMargin);
            alpha *= Mathf.Clamp01(1f - pastTop / edgeFadeMargin);
            alpha *= Mathf.Clamp01(1f - pastRight / edgeFadeMargin);
            alpha *= Mathf.Clamp01(1f - pastLeft / edgeFadeMargin);

            if (alpha <= 0.001f)
            {
                el.gameObject.SetActive(false);
                continue;
            }

            el.gameObject.SetActive(true);
            if (_canvasGroups[i]) _canvasGroups[i].alpha = alpha;

            // Posición en MUNDO (no anchoredPosition) — así da igual si el padre real del
            // elemento (ej. "DecorationsLeft") no es el Canvas directo y tiene su propio
            // offset/tamaño: Transform.position siempre es absoluto, sin sumar desplazamientos.
            float px = (screenX01 - 0.5f) * size.x;
            float py = (0.5f - screenY01) * size.y;
            el.position = canvasRect.TransformPoint(new Vector3(px, py, 0f));
            el.localScale = Vector3.one; // sin achicarse, igual que los nodos

            // Paradas casi todo el recorrido — la inclinación hacia atrás recién se nota cerca
            // del horizonte (1-cosTh ≈ 0 cerca del pie, crece fuerte cuando th se acerca a 90°).
            float tiltX = -Mathf.Sign(th) * (1f - cosTh) * (1f - cosTh) * tiltNearHorizonMax;
            el.localRotation = Quaternion.Euler(tiltX, 0f, 0f);
        }

        ReorderByDepth();
    }

    // El orden de dibujo en UI lo da el sibling index, no la posición en pantalla — sin esto,
    // el que haya quedado último en la Hierarchy siempre se dibuja encima, sin importar cuál
    // está realmente más cerca de cámara (th más chico) en cada momento del scroll.
    void ReorderByDepth()
    {
        for (int i = 0; i < _sortIndices.Length; i++) _sortIndices[i] = i;

        // th más grande (más lejos, cerca del horizonte) primero -> queda ATRÁS (sibling index
        // chico). th más chico (más cerca del pie) al final -> queda ADELANTE (se dibuja encima).
        System.Array.Sort(_sortIndices, (a, b) => _lastTh[b].CompareTo(_lastTh[a]));

        for (int order = 0; order < _sortIndices.Length; order++)
        {
            var el = _elements[_sortIndices[order]];
            if (el) el.SetSiblingIndex(order);
        }
    }
}
