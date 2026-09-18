using UnityEngine;
using UnityEngine.EventSystems;

// El scroll del mapa: el mundo se mueve en Z y la cámara no se mueve nunca.
//
// Al revés de lo que parece natural, pero es lo que hace que la curvatura funcione: el shader
// dobla cada vértice según su distancia A LA CÁMARA, así que si la cámara viajara, el horizonte
// viajaría con ella y el terreno nunca terminaría de enderezarse al acercarse.
//
// Va sobre un Image invisible a pantalla completa. Recibe el arrastre él mismo en vez de que se
// lo reenvíe otro script: es un solo componente que leer cuando algo del scroll se comporte raro.
public class WorldMapScroll : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerClickHandler
{
    [Tooltip("Cuántas unidades de mundo avanza el mapa por cada unidad de pantalla arrastrada.")]
    [SerializeField] float sensitivity = 0.02f;

    [Tooltip("Qué tan rápido frena al soltar. Más alto frena antes.")]
    [SerializeField] float friction = 6f;

    [Tooltip("Por debajo de esta velocidad se considera detenido y deja de moverse.")]
    [SerializeField] float stopBelow = 0.05f;

    [Header("Límites del recorrido, en unidades de mundo")]
    [SerializeField] float min;
    [SerializeField] float max = 200f;

    [Tooltip("Cuánto se puede pasar de los límites mientras el dedo está apoyado, antes de volver solo.")]
    [SerializeField] float overshoot = 3f;

    [SerializeField] Transform world;

    [Tooltip("Cuántos píxeles puede moverse el dedo y seguir contando como toque y no como arrastre.")]
    [SerializeField] float tapSlack = 12f;

    public float Offset { get; private set; }

    // Lo escucha WorldMapNodes. El toque se resuelve acá y no con el raycast de UI porque este
    // Image ocupa toda la pantalla sobre un Canvas Overlay, y ese gana siempre contra los Canvas
    // en espacio de mundo de los nodos: el click nunca les llegaría.
    public event System.Action<Vector2> OnTap;

    float _velocity;
    bool  _dragging;
    float _dragged;

    void Reset() => world = transform.parent;

    void LateUpdate()
    {
        if (_dragging) return;

        // Fuera de los límites manda el rebote, no la inercia: si se dejara frenar sola, el mapa
        // se quedaría quieto más allá del final del capítulo.
        float clamped = Mathf.Clamp(Offset, min, max);
        if (!Mathf.Approximately(clamped, Offset))
        {
            Offset    = Mathf.Lerp(Offset, clamped, 1f - Mathf.Exp(-friction * 2f * Time.deltaTime));
            _velocity = 0f;
            Apply();
            return;
        }

        if (Mathf.Abs(_velocity) < stopBelow) return;

        // Exponencial y no lineal: así el frenado se siente igual corra el juego a 30 o a 120 fps.
        _velocity *= Mathf.Exp(-friction * Time.deltaTime);
        Move(_velocity * Time.deltaTime);
    }

    // El contador se reinicia al APOYAR el dedo y no al empezar a arrastrar: un toque limpio
    // nunca dispara OnBeginDrag, así que si se reiniciara ahí conservaría la distancia del
    // arrastre anterior y el toque se descartaría por "te moviste demasiado".
    public void OnPointerDown(PointerEventData eventData) => _dragged = 0f;

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragging = true;
        _velocity = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        _dragged += eventData.delta.magnitude;

        float delta = -eventData.delta.y * sensitivity;
        Move(delta);

        // La velocidad sale del movimiento real de este frame y no de eventData.delta acumulado:
        // al soltar tras una pausa con el dedo quieto, el acumulado lanzaría el mapa igual.
        if (Time.deltaTime > 0f) _velocity = delta / Time.deltaTime;
    }

    public void OnEndDrag(PointerEventData eventData) => _dragging = false;

    // Un toque es un arrastre que casi no se movió. Sin el margen, cualquier temblor del dedo
    // sobre un nodo lo convertiría en scroll y el nivel no abriría nunca.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_dragged <= tapSlack) OnTap?.Invoke(eventData.position);
    }

    void Move(float delta)
    {
        Offset = Mathf.Clamp(Offset + delta, min - overshoot, max + overshoot);
        Apply();
    }

    void Apply()
    {
        if (world) world.localPosition = new Vector3(world.localPosition.x, world.localPosition.y, -Offset);
    }
}
