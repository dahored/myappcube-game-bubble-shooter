using UnityEngine;

// Recorrido en ocho para criaturas que nadan. Para peces y similares, donde flotar arriba y abajo
// no alcanza: un pez quieto que solo sube y baja se lee como colgado de un hilo.
//
// La figura es una lemniscata — x = ancho * sin(t), y = alto * sin(2t) —, o sea el mismo seno en
// los dos ejes pero el vertical al doble de velocidad. Se eligió por tres cosas que salen gratis:
// el trazo nunca es recto, se cierra sobre sí mismo sin saltos, y el cambio de sentido ocurre en
// los extremos, que es donde un pez de verdad gira.
//
// Sirve en UI y en el mundo, igual que FloatAnimation. La única diferencia es la unidad: un
// RectTransform se mueve en píxeles del lienzo y un Transform en unidades de mundo.
//
// NO se combina con FloatAnimation: los dos escriben la posición y se pisarían. Este ya incluye
// el vaivén vertical.
public class SwimPath : MonoBehaviour
{
    [Header("Recorrido")]
    [Tooltip("Cuánto se aleja del centro a cada lado.")]
    [SerializeField] float width = 60f;

    [Tooltip("Cuánto sube y baja. Bastante menor que el ancho: un ocho demasiado alto se lee como un garabato en vez de como algo nadando.")]
    [SerializeField] float height = 18f;

    [Tooltip("Segundos en dar la vuelta completa al ocho.")]
    [SerializeField] float period = 12f;

    [Tooltip("Cuánto vale el recorrido cuando la criatura NO es de UI. Las unidades de mundo son mucho más grandes que los píxeles.")]
    [SerializeField] float worldScale = 0.01f;

    [Header("Variación entre copias")]
    [SerializeField] bool randomPhase = true;

    [Tooltip("Cuánto varía la velocidad entre una copia y otra, en fracción. Sin esto, varias criaturas recorren su ocho al mismo ritmo y se ven coreografiadas.")]
    [SerializeField, Range(0f, 0.5f)] float periodVariation = 0.25f;

    [Tooltip("Cuánto varía el tamaño del recorrido entre copias.")]
    [SerializeField, Range(0f, 0.5f)] float sizeVariation = 0.2f;

    [Header("Orientación")]
    [Tooltip("Da la vuelta a la criatura para que mire hacia donde nada. Sin esto, media vuelta del ocho la hace ir de espaldas.")]
    [SerializeField] bool faceDirection = true;

    [Tooltip("Marcar si el dibujo mira a la IZQUIERDA en su sprite original.")]
    [SerializeField] bool artFacesLeft;

    [Tooltip("Cuánto inclina el morro al subir o bajar, en grados. 0 lo deja siempre horizontal.")]
    [SerializeField, Range(0f, 45f)] float pitchWithClimb = 12f;

    RectTransform  _rect;     // nulo si no es de UI
    SpriteRenderer _renderer; // nulo si es de UI
    Vector3        _origin;
    Vector3        _baseScale;

    float _width, _height, _period, _phase;

    void Start()
    {
        _rect      = transform as RectTransform;
        _renderer  = GetComponent<SpriteRenderer>();
        _origin    = _rect ? (Vector3)_rect.anchoredPosition : transform.localPosition;
        _baseScale = transform.localScale;

        // Se sortea una vez y se guarda. Leyendo los valores del Inspector en cada frame, el azar
        // volvería a tirarse siempre y el recorrido iría a saltos en vez de ser una curva.
        float unit = _rect ? 1f : worldScale;
        _width  = width  * unit * (1f + Random.Range(-sizeVariation, sizeVariation));
        _height = height * unit * (1f + Random.Range(-sizeVariation, sizeVariation));
        _period = Mathf.Max(0.1f, period * (1f + Random.Range(-periodVariation, periodVariation)));
        _phase  = randomPhase ? Random.value * Mathf.PI * 2f : 0f;
    }

    void Update()
    {
        float t = Time.time / _period * Mathf.PI * 2f + _phase;

        // El vertical va al DOBLE de velocidad que el horizontal: eso es lo que convierte una
        // elipse en un ocho, con su cruce en el centro.
        Vector3 offset = new(_width * Mathf.Sin(t), _height * Mathf.Sin(t * 2f), 0f);

        if (_rect) _rect.anchoredPosition = (Vector2)(_origin + offset);
        else       transform.localPosition = _origin + offset;

        if (!faceDirection) return;

        // La derivada del recorrido, que es hacia dónde va en este instante. Se usa el signo del
        // coseno y no la diferencia con el frame anterior: así el giro ocurre exacto en el
        // extremo del ocho, sin depender de los fotogramas por segundo.
        bool goingLeft = Mathf.Cos(t) < 0f;
        Face(goingLeft != artFacesLeft, Mathf.Cos(t * 2f) * 2f * _height);
    }

    // El giro va en el SpriteRenderer cuando lo hay, y solo cae a la escala si no existe. Escribir
    // la escala pelearía con GelatineAnimation, que anima ese mismo valor en muchas piezas.
    void Face(bool flip, float climb)
    {
        if (_renderer) _renderer.flipX = flip;
        else transform.localScale = new Vector3(Mathf.Abs(_baseScale.x) * (flip ? -1f : 1f),
                                                _baseScale.y, _baseScale.z);

        if (pitchWithClimb <= 0f) return;

        // Inclinado según lo que sube o baja, y al revés cuando va hacia el otro lado: si no, al
        // dar la vuelta el morro apuntaría hacia donde tiene la cola.
        float amount = Mathf.Clamp(climb / Mathf.Max(0.0001f, _height * 2f), -1f, 1f);
        transform.localRotation = Quaternion.Euler(0f, 0f, amount * pitchWithClimb * (flip ? -1f : 1f));
    }
}
