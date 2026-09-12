using UnityEngine;
using UnityEngine.EventSystems;

// Prototipo del "mundo curvo" (experimento — LevelMapCurve.unity, no toca LevelMap real). El
// mundo es literalmente una esfera 3D (WorldSphere, hija de este mismo GameObject) — los nodos
// se ubican en posiciones FIJAS sobre su superficie (coordenadas esféricas), y el gesto de
// scroll ROTA todo este objeto (esfera + nodos juntos) sobre su eje X local, como si girases un
// globo terráqueo. La cámara queda fija — es la rotación del mundo, no un desplazamiento de
// cámara, lo que hace que los nodos se acerquen/alejen/achiquen por perspectiva real a medida
// que giran hacia los costados o se alejan del frente, igual que en Candy Crush.
public class LevelMapCurveController : MonoBehaviour, IDragHandler
{
    [Header("Rotación (drag)")]
    [Tooltip("Grados que rota el mundo por cada pixel de drag vertical.")]
    [SerializeField] float dragSpeedDeg = 0.15f;

    [Header("Nodos de prueba (placeholder, no LevelNodeView todavía)")]
    [SerializeField] GameObject nodePrefab;
    [SerializeField] int        nodeCount     = 15;
    [Tooltip("Tiene que coincidir con el radio VISUAL de WorldSphere (Scale/2 del prefab de esfera default de Unity, cuyo mesh tiene radio 0.5 — ej. Scale 16 = radio 8).")]
    [SerializeField] float      radius        = 8f;
    [Tooltip("Distancia (en unidades) entre nodos a lo largo del recorrido — el ángulo necesario para lograrla se calcula solo, según el Radius actual.")]
    [SerializeField] float      nodeSpacing   = 1.2f;
    [Tooltip("Desvío lateral MÁXIMO (en unidades, izquierda/derecha) — el ángulo necesario se calcula solo según el Radius actual, igual que Node Spacing.")]
    [SerializeField] float      windAmplitude = 1f;
    [SerializeField] float      windFrequency = 0.5f;

    float _currentRotationX;

    void Start()
    {
        if (!nodePrefab) { Debug.LogWarning("[LevelMapCurveController] Falta asignar 'Node Prefab' en el Inspector."); return; }

        // Distancias deseadas -> ángulos necesarios para lograrlas sobre este radio (arco =
        // radio * ángulo en radianes). Si el radio cambia, las distancias se mantienen solas.
        float angleStepDeg = (nodeSpacing / radius) * Mathf.Rad2Deg;
        float windAmplitudeDeg = (windAmplitude / radius) * Mathf.Rad2Deg;

        for (int i = 0; i < nodeCount; i++)
        {
            float latDeg = i * angleStepDeg;
            float lonDeg = Mathf.Sin(i * windFrequency) * windAmplitudeDeg;

            // Punto de partida: el "polo frontal" de la esfera (el más cercano a la cámara),
            // rotado por latitud (progreso del recorrido) y longitud (vaivén lateral). Así cada
            // nodo queda anclado a un punto fijo sobre la superficie de la esfera.
            Vector3 localPos = Quaternion.Euler(latDeg, lonDeg, 0f) * (Vector3.back * radius);

            var node = Instantiate(nodePrefab, transform);
            node.transform.localPosition = localPos;
            node.name = $"NodeProto_{i}";
        }
    }

    // Drag vertical gira el mundo entero (esfera + nodos, todos hijos de este transform) sobre
    // su eje X local — mismo gesto de siempre, pero ahora es una rotación real del mundo, no un
    // desplazamiento de cámara ni de posición.
    public void OnDrag(PointerEventData eventData)
    {
        _currentRotationX += eventData.delta.y * dragSpeedDeg;
        transform.localRotation = Quaternion.Euler(_currentRotationX, 0f, 0f);
    }
}
