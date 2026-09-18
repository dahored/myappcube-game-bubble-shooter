using UnityEngine;

// Manda los parámetros de curvatura a TODOS los materiales del mapa de una sola vez.
//
// Son variables globales de shader, no propiedades por material, a propósito: el suelo, el
// camino, los nodos y las decoraciones tienen que doblarse con exactamente los mismos números.
// Si cada material llevara su copia, ajustar la curva obligaría a tocar veinte materiales y
// bastaría olvidar uno para que un adorno quedara flotando por encima del terreno.
//
// La matemática vive en Shaders/CurvedWorld.hlsl. Acá solo se eligen los valores.
[ExecuteAlways]
public class WorldMapCurve : MonoBehaviour
{
    [Tooltip("Cuánto se hunde el terreno a medida que se aleja. Es lo que dibuja el horizonte.")]
    [SerializeField] float depth = 0.012f;

    [Tooltip("Cuánto caen los costados respecto del centro. Da la sensación de estar sobre algo redondo.")]
    [SerializeField] float side = 0.01f;

    [Tooltip("Hasta esta distancia de la cámara el terreno queda plano. Protege la zona donde está el nodo actual.")]
    [SerializeField] float flatUntil = 4f;

    [Tooltip("El eje X sobre el que no hay caída lateral. 0 = el centro del mundo.")]
    [SerializeField] float centerX;

    // Los mismos valores que se le pasan al shader, guardados acá para poder repetir la cuenta
    // en C#. Hace falta porque el shader dobla los vértices al DIBUJAR: el transform de un nodo
    // sigue estando en el plano, así que para saber dónde se lo ve hay que aplicar la curva
    // aparte. Es la única duplicación de esta matemática y vive pegada a los valores justamente
    // para que no se separen sin que se note.
    static float _depth, _side, _start, _centerX;

    public static Vector3 Curve(Vector3 positionWS, Camera camera)
    {
        if (camera == null) return positionWS;

        float depth = Mathf.Max(0f, (positionWS.z - camera.transform.position.z) - _start);
        float side  = positionWS.x - _centerX;

        positionWS.y -= _depth * depth * depth + _side * side * side;
        return positionWS;
    }

    static readonly int DepthId   = Shader.PropertyToID("_CurveDepth");
    static readonly int SideId    = Shader.PropertyToID("_CurveSide");
    static readonly int StartId   = Shader.PropertyToID("_CurveStart");
    static readonly int CenterXId = Shader.PropertyToID("_CurveCenterX");

    // En Update y no solo en Awake para poder mover los sliders en Play y ver el efecto al
    // instante — calibrar una curva a ojo sin verla moverse es imposible.
    void Update() => Apply();

    void OnValidate() => Apply();

    void Apply()
    {
        _depth = depth; _side = side; _start = flatUntil; _centerX = centerX;

        Shader.SetGlobalFloat(DepthId,   depth);
        Shader.SetGlobalFloat(SideId,    side);
        Shader.SetGlobalFloat(StartId,   flatUntil);
        Shader.SetGlobalFloat(CenterXId, centerX);
    }
}
