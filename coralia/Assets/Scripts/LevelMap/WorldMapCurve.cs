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
    [Tooltip("Apagado, el mundo se ve plano. No borra los valores de abajo: quedan guardados para cuando se vuelva a encender.")]
    [SerializeField] bool curved = true;

    [Tooltip("Cuánto se hunde el terreno a medida que se aleja. Es lo que dibuja el horizonte.")]
    [SerializeField] float depth = 0.012f;

    [Tooltip("Cuánto caen los costados respecto del centro. Da la sensación de estar sobre algo redondo.")]
    [SerializeField] float side = 0.01f;

    [Tooltip("Hasta esta distancia de la cámara el terreno queda plano. Protege la zona donde está el nodo actual.")]
    [SerializeField] float flatUntil = 4f;

    [Tooltip("El eje X sobre el que no hay caída lateral. 0 = el centro del mundo.")]
    [SerializeField] float centerX;

    // Lo escribe WorldMapCameraFit cuando la cámara cambia de sitio según la pantalla: esto se
    // mide DESDE la cámara, así que moverla sin ajustarlo corre la zona plana sola.
    public float FlatUntil { get => flatUntil; set => flatUntil = value; }

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

    // Cuánto baja el terreno por unidad que se aleja, en ese punto, expresado en grados.
    //
    // Es la derivada de la caída: la curva es cuadrática, así que la pendiente crece con la
    // distancia. Lo necesitan los nodos, que se apoyan sobre el suelo: con una inclinación fija
    // quedan clavados en un ángulo y a lo lejos se ven torcidos contra la pendiente.
    //
    // Solo la caída hacia el fondo. La lateral también inclina, pero con el zigzag y los desvíos
    // que maneja este mapa no llega a un par de grados.
    public static float Pitch(Vector3 positionWS, Camera camera)
    {
        if (camera == null) return 0f;

        float depth = Mathf.Max(0f, (positionWS.z - camera.transform.position.z) - _start);
        return Mathf.Atan(2f * _depth * depth) * Mathf.Rad2Deg;
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
        // Apagar la curva es mandar ceros, no saltarse el Apply: los parámetros son globales de
        // shader y se quedan con el último valor puesto — incluido el de otra escena.
        //
        // Se apagan las dos copias a la vez, la del shader y la de C#. Si solo se apagara una, el
        // suelo se vería plano y los nodos seguirían hundiéndose, o al revés.
        float wantDepth = curved ? depth : 0f;
        float wantSide  = curved ? side  : 0f;

        _depth = wantDepth; _side = wantSide; _start = flatUntil; _centerX = centerX;

        Shader.SetGlobalFloat(DepthId,   wantDepth);
        Shader.SetGlobalFloat(SideId,    wantSide);
        Shader.SetGlobalFloat(StartId,   flatUntil);
        Shader.SetGlobalFloat(CenterXId, centerX);
    }
}
