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

    [Header("Límites del recorrido")]
    [Tooltip("Tope manual, en unidades de mundo. Solo se usa si no hay terreno del cual sacar los extremos reales.")]
    [SerializeField] float fallbackMax = 200f;

    [Tooltip("Cuánto se puede pasar de los límites mientras el dedo está apoyado, antes de volver solo.")]
    [SerializeField] float overshoot = 3f;

    [SerializeField] Transform world;

    [Tooltip("Cuántos píxeles puede moverse el dedo y seguir contando como toque y no como arrastre.")]
    [SerializeField] float tapSlack = 12f;

    [Tooltip("Dónde queda un nivel al centrarlo, en unidades de mundo. Positivo lo deja más abajo en pantalla.")]
    [SerializeField] float centerBias = 0f;

    // Con esto puesto el mapa no responde al dedo. Lo usa la tarjeta del jugador mientras dura su
    // llegada: si el jugador pudiera arrastrar en ese momento, se perdería la animación que
    // justamente estamos esperando a que se vea.
    public bool Locked { get; set; }

    public float Offset { get; private set; }

    // Lo escucha WorldMapNodes. El toque se resuelve acá y no con el raycast de UI porque este
    // Image ocupa toda la pantalla sobre un Canvas Overlay, y ese gana siempre contra los Canvas
    // en espacio de mundo de los nodos: el click nunca les llegaría.
    public event System.Action<Vector2> OnTap;

    float _velocity;
    bool  _dragging;
    float _dragged;

    // Los topes no llevan ningún número a mano: son el punto en el que el borde del terreno toca
    // el borde de la pantalla. Se agrega un capítulo, se cambia el margen del suelo o se cambia el
    // encuadre de la cámara, y salen solos.
    //
    // No se puede resolver con una cuenta cerrada porque la curvatura hunde el terreno: el rayo
    // que sale por el borde superior nunca llega a cortar el suelo curvo, se le escapa por arriba.
    // Así que se busca el Offset con el que el borde del terreno cae justo en el borde de la
    // pantalla, partiendo el intervalo por la mitad. Treinta pasos dan precisión de sobra y
    // cuestan nada.
    float Min => _limits.min;
    float Max => _limits.max;

    (float min, float max) _limits;
    int _limitsFrame = -1;

    void RefreshLimits()
    {
        if (_limitsFrame == Time.frameCount) return;
        _limitsFrame = Time.frameCount;

        var cam = Camera.main;
        if (_ground == null) _ground = FindAnyObjectByType<WorldMapGround>();

        if (cam == null || _ground == null)
        {
            _limits = (0f, fallbackMax);
            return;
        }

        MeasureView(cam, out float near, out float far);

        float min = _ground.WorldStart - near;
        float max = _ground.WorldEnd   - far;

        // Un mundo más corto que la pantalla deja el máximo por debajo del mínimo. Ahí no hay nada
        // que arrastrar y el tope tiene que ser el mismo, no un rango dado vuelta.
        _limits = (min, Mathf.Max(min, max));
    }

    // Entre qué Z del suelo está lo que se ve: la primera que asoma por el borde de abajo y la
    // última que se alcanza a ver arriba.
    //
    // Se recorre la Z alejándose de la cámara en vez de resolver una ecuación porque la curvatura
    // rompe el supuesto de "más lejos = más arriba en pantalla": el terreno se hunde cada vez más
    // rápido, así que pasado cierto punto vuelve a BAJAR. Ese punto es el horizonte, y es
    // exactamente el tope que buscamos — más allá, seguir arrastrando no revela nada nuevo.
    void MeasureView(Camera cam, out float near, out float far)
    {
        float camZ = cam.transform.position.z;
        float step = SCAN_RANGE / SCAN_SAMPLES;

        near = camZ;
        far  = camZ;

        float best    = float.NegativeInfinity;
        float prevZ   = camZ;
        bool  entered = false;

        for (int i = 1; i <= SCAN_SAMPLES; i++)
        {
            float z = camZ + i * step;
            float y = ScreenYOf(cam, z);

            // El borde de abajo se afina: está cerca de la cámara, así que un error de unas
            // unidades ahí se ve como un hueco de agua vacía.
            if (!entered && y >= 0f)
            {
                near    = Refine(cam, prevZ, z, 0f);
                entered = true;
            }

            prevZ = z;
            if (!entered) continue;

            if (y >= 1f)        { far = z; return; }   // llegó al borde de arriba
            if (y > best)       { best = y; far = z; }
            else if (y < best)  return;                // ya está bajando: pasó el horizonte
        }
    }

    // Afina entre dos muestras dónde el suelo cruza esa altura de pantalla.
    static float Refine(Camera cam, float low, float high, float wanted)
    {
        for (int i = 0; i < 12; i++)
        {
            float mid = (low + high) * 0.5f;
            if (ScreenYOf(cam, mid) < wanted) low = mid;
            else                              high = mid;
        }
        return (low + high) * 0.5f;
    }

    // A qué altura de pantalla se ve un punto del suelo que está en esa Z, con la curvatura ya
    // aplicada — que es donde se lo ve de verdad, no donde dice su posición.
    static float ScreenYOf(Camera cam, float z)
    {
        Vector3 point = WorldMapCurve.Curve(new Vector3(0f, 0f, z), cam);
        Vector3 view  = cam.WorldToViewportPoint(point);

        // Detrás de la cámara los valores de viewport se dan vuelta; para este recorrido lo que
        // importa es que cuente como "todavía no entró por abajo".
        return view.z > 0f ? view.y : -1f;
    }

    const float SCAN_RANGE   = 600f;
    const int   SCAN_SAMPLES = 120;

    WorldMapGround _ground;

    void Reset() => world = transform.parent;

    // En Update y no en LateUpdate a propósito, aunque solo mueva el mundo.
    //
    // Los nodos no se doblan con el shader: su altura la recalcula WorldMapNodes en LateUpdate a
    // partir de la distancia a la cámara. Si el mundo se moviera también en LateUpdate, el orden
    // entre los dos scripts quedaría a suerte de Unity y los nodos podrían estar calculando su
    // altura con la posición del frame anterior. Unity sí garantiza que TODOS los Update van antes
    // que cualquier LateUpdate, así que moviendo acá el mundo siempre está quieto cuando los nodos
    // se colocan.
    void Update()
    {
        RefreshLimits();
        if (_dragging || Locked) return;

        // Fuera de los límites manda el rebote, no la inercia: si se dejara frenar sola, el mapa
        // se quedaría quieto más allá del final del capítulo.
        float clamped = Mathf.Clamp(Offset, Min, Max);
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
        if (Locked) return;

        _dragging = true;
        _velocity = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Locked) return;

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
        if (!Locked && _dragged <= tapSlack) OnTap?.Invoke(eventData.position);
    }

    // Lleva el mapa a una posición concreta. Lo usan los pines para volver al nodo actual.
    //
    // Corta la inercia: si no, el mapa seguiría deslizándose hacia donde venía y pelearía con el
    // salto.
    public void MoveTo(float offset)
    {
        RefreshLimits();
        _velocity = 0f;
        Offset    = Mathf.Clamp(offset, Min, Max);
        Apply();
    }

    // Dónde tiene que quedar el scroll para que un punto del mundo se vea centrado. Vive acá
    // porque es la vista la que decide dónde está su punto de referencia, no quien pide el viaje:
    // así el pin y la tarjeta del jugador no pueden discrepar.
    public float OffsetFor(float worldZ) => worldZ - centerBias;

    public void CenterOn(float worldZ) => MoveTo(OffsetFor(worldZ));

    void Move(float delta)
    {
        RefreshLimits();
        Offset = Mathf.Clamp(Offset + delta, Min - overshoot, Max + overshoot);
        Apply();
    }

    void Apply()
    {
        if (world) world.localPosition = new Vector3(world.localPosition.x, world.localPosition.y, -Offset);
    }
}
