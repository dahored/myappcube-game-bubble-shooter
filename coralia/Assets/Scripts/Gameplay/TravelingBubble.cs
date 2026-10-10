using UnityEngine;
using UnityEngine.UI;

// Una burbuja que sale del impacto y viaja hasta otra celda del tablero.
//
// Existe por la Burbuja Madre: las dos hijas que no revientan donde pegó el disparo van a buscar
// burbujas que pueden estar en la otra punta. Sin verlas ir, dos burbujas lejanas desaparecen a
// la vez que la del impacto y se lee como un fallo del juego, no como un poder.
//
// El camino es una recta con UN RIZO cerca de la salida: sale, da la vuelta sobre sí misma y
// sigue. Es un solo movimiento continuo y no tres tiempos encadenados —girar, parar, correr—,
// porque el tirón entre fase y fase se ve, y lo que tiene que parecer es algo vivo que arranca.
//
// El rizo se cierra de verdad, cruzando su propio trazo, porque mientras lo da avanza menos de lo
// que mide la vuelta. Si avanzara más, saldría una ondulación y no un bucle.
public class TravelingBubble : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("Marcado: las burbujas que el poder alcanza lejos no estallan solas — sale una hija desde el impacto y revienta al llegar. Para un poder que elige objetivos repartidos por el tablero.")]
        public bool enabled;

        [Tooltip("Cuánto espera antes de salir, desde que estalla la burbuja del impacto. Es el hueco donde se ve la división: sin él las hijas salen dentro de la propia explosión y no se llega a entender que salieron de ahí.")]
        [Range(0f, 1f)]
        public float delay = 0.18f;

        [Tooltip("Lo que dura el viaje entero. Es también lo que esa burbuja espera antes de reventar: lo que se ve y lo que pasa van juntos.")]
        [Range(0.2f, 3f)]
        public float duration = 0.85f;

        [Tooltip("En qué momento del viaje empieza el rizo, de 0 a 1. Pronto es lo que se lee como 'arrancó dando una vuelta'; pasada la mitad parece que se lo pensó dos veces.")]
        [Range(0f, 0.7f)]
        public float loopAt = 0.12f;

        [Tooltip("Qué parte del viaje ocupa el rizo. Mientras lo da casi no avanza, así que esto es sobre todo cuánto se para a girar.")]
        [Range(0.1f, 0.7f)]
        public float loopSpan = 0.34f;

        [Tooltip("Lo grande que es el rizo, en burbujas.")]
        [Range(0.2f, 3f)]
        public float loopSize = 0.95f;

        [Tooltip("El sonido de la salida: lo que se oye cuando las hijas se desprenden y arrancan. Suena UNA vez aunque salgan varias — una por hija se oiría como un eco, porque salen todas en el mismo instante.")]
        public AudioClip travelClip;

        [Tooltip("El sonido de la LLEGADA: lo que se oye cuando una hija alcanza su objetivo y lo revienta, y también cuando se deshace en el agua sin haber encontrado ninguno.\n\nAparte del 'Burst Clip' del poder a propósito: el estallido del impacto y el de una hija son dos cosas distintas, y con el mismo sonido el segundo se confunde con un eco del primero. Vacío usa el del poder.")]
        public AudioClip arriveClip;

        [Tooltip("Qué mide la hija mientras viaja, respecto a una burbuja del tablero.")]
        [Range(0.2f, 1f)]
        public float size = 0.55f;

        // Desde que estalla el impacto hasta que la hija llega. Quien lance el viaje lo necesita
        // para retrasar el pop de la burbuja de destino, y vive acá para que no haya dos sitios
        // sumando los mismos tramos.
        public float Total => delay + duration;
    }

    // from/to en coordenadas del TABLERO, no del canvas: así el viaje acompaña al tablero si este
    // se desplaza por el scroll mientras la hija está en el aire.
    public static void Send(RectTransform gridContainer, Vector2 from, Vector2 to, Sprite sprite, Settings s, int index)
    {
        if (gridContainer == null || s == null || !s.enabled || sprite == null) return;

        var go = new GameObject("TravelingBubble", typeof(RectTransform), typeof(Image))
        {
            // Temporal y se destruye sola, pero si el editor guarda la escena mientras vive, sin
            // esto queda serializada dentro del .unity.
            hideFlags = HideFlags.DontSave,
        };

        var rt = (RectTransform)go.transform;
        rt.SetParent(gridContainer, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * (HexGridMath.BubbleDiameter * s.size);
        rt.anchoredPosition = from;
        rt.SetAsLastSibling();   // por encima del tablero: va nadando por delante, no entre medias

        var image = go.GetComponent<Image>();
        image.sprite         = sprite;
        image.raycastTarget  = false;
        image.preserveAspect = true;

        go.AddComponent<TravelingBubble>().Init(from, to, s, index);
    }

    RectTransform _rt;
    Image         _image;
    Settings      _settings;

    Vector2 _from, _to;
    Vector2 _along, _side;   // el marco del camino: hacia dónde va y qué es "a un lado"
    float   _spin;           // hacia qué lado se abre el rizo
    float   _elapsed;
    bool    _started;        // si ya arrancó, para sonar en el primer fotograma de vuelo y no en todos
    bool    _leads;          // si es la que lleva el sonido de la salida

    void Init(Vector2 from, Vector2 to, Settings s, int index)
    {
        _rt       = (RectTransform)transform;
        _image    = GetComponent<Image>();
        _from     = from;
        _to       = to;
        _settings = s;

        // Las dos hijas rizan hacia lados contrarios. Rizando igual salen en paralelo y parecen
        // una sola cosa partida en dos en vez de dos que van a sitios distintos.
        _spin = index % 2 == 0 ? 1f : -1f;

        // Solo la primera suena. Todas salen en el mismo instante, así que un clip por hija se
        // oye como un eco del mismo sonido en vez de como dos burbujas.
        _leads = index == 0;

        _along = (to - from).normalized;
        _side  = new Vector2(-_along.y, _along.x) * _spin;

        Show(false);
    }

    void Update()
    {
        _elapsed += Time.deltaTime;

        float t = _elapsed - _settings.delay;
        if (t < 0f) return;

        Show(true);

        if (!_started)
        {
            _started = true;
            if (_leads) AudioManager.Instance?.PlaySfx(_settings.travelClip);
        }

        t = Mathf.Clamp01(t / _settings.duration);

        Place(At(t));

        // Se apaga justo al final, no durante: tiene que llegar entera para que el pop de la
        // burbuja de destino se lea como consecuencia suya.
        if (t > 0.9f)
        {
            var c = _image.color;
            _image.color = new Color(c.r, c.g, c.b, Mathf.InverseLerp(1f, 0.9f, t));
        }

        if (t >= 1f) Destroy(gameObject);
    }

    // Dónde está la hija en el instante t del viaje.
    Vector2 At(float t)
    {
        Vector2 at = Vector2.Lerp(_from, _to, Advance(t));

        float loop = Mathf.InverseLerp(_settings.loopAt, _settings.loopAt + _settings.loopSpan, t);
        if (loop <= 0f || loop >= 1f) return at;

        // El rizo es un círculo apoyado EN el camino: su centro está a un radio hacia el lado, y
        // la vuelta empieza y acaba justo sobre la línea. Así entra y sale del trazo sin salto,
        // que es lo que lo hace parecer un bucle del propio camino y no un adorno encima.
        float   radius = _settings.loopSize * HexGridMath.BubbleDiameter;
        float   turn   = loop * Mathf.PI * 2f;
        Vector2 center = at + _side * radius;

        return center + _along * Mathf.Sin(turn) * radius - _side * Mathf.Cos(turn) * radius;
    }

    // Cuánto ha avanzado por la recta. Se FRENA durante el rizo: con avance constante la vuelta
    // se estira y sale una ondulación en vez de un bucle cerrado. El resto del camino se reparte
    // lo que el rizo no usa, con ease a la entrada y a la salida.
    float Advance(float t)
    {
        const float DURING_LOOP = 0.12f;   // qué parte del recorrido se gasta mientras riza

        float a = _settings.loopAt;
        float b = Mathf.Min(1f, a + _settings.loopSpan);

        if (t <= a) return Ease(t / Mathf.Max(0.0001f, a)) * a * (1f - DURING_LOOP);

        float before = a * (1f - DURING_LOOP);

        if (t < b) return before + (t - a) / (b - a) * DURING_LOOP;

        float after = before + DURING_LOOP;

        return Mathf.Lerp(after, 1f, Ease((t - b) / Mathf.Max(0.0001f, 1f - b)));
    }

    // Mira hacia donde se mueve de verdad, rizo incluido: calculado sobre la recta, la hija
    // apuntaría al objetivo mientras todavía está dando la vuelta.
    void Place(Vector2 at)
    {
        Vector2 heading = at - _rt.anchoredPosition;

        _rt.anchoredPosition = at;

        if (heading.sqrMagnitude > 0.01f)
            _rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg - 90f);
    }

    void Show(bool on)
    {
        var c = _image.color;
        _image.color = new Color(c.r, c.g, c.b, on ? 1f : 0f);
    }

    static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
