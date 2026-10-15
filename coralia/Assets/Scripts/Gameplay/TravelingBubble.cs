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

        [Tooltip("Cuánto espera la PRIMERA antes de salir, desde que estalla la burbuja del impacto. Es el hueco donde se ve la división: sin él las hijas salen dentro de la propia explosión y no se llega a entender que salieron de ahí.")]
        [Range(0f, 1f)]
        public float delay = 0.18f;

        [Tooltip("Lo que cada hija espera respecto a la anterior. Saliendo a la vez llegan a la vez, y dos estallidos en el mismo instante se oyen como uno solo mal grabado — además de que una tapa a la otra al mirar.")]
        [Range(0f, 0.6f)]
        public float stagger = 0.14f;

        [Tooltip("Lo que dura el viaje entero. Es también lo que esa burbuja espera antes de reventar: lo que se ve y lo que pasa van juntos.")]
        [Range(0.2f, 3f)]
        public float duration = 0.85f;

        [Tooltip("Qué parte del CAMINO recorre antes de ponerse a rizar, de 0 a 1 — en 0.45 riza pasada casi la mitad. Es distancia y no tiempo: hasta ahí va disparada y frenando, así que llega antes de lo que el número sugiere.\n\nPronto se lee como 'arrancó dando una vuelta'; pasada la mitad, como que se lo pensó dos veces.")]
        [Range(0f, 0.7f)]
        public float loopAt = 0.45f;

        [Tooltip("Qué parte del TIEMPO del viaje se va en el rizo. Mientras lo da casi no avanza, así que subirlo es que la vuelta se dé más despacio, no que el viaje dure más.")]
        [Range(0.1f, 0.7f)]
        public float loopSpan = 0.4f;

        [Tooltip("Lo grande que es el rizo, en burbujas.")]
        [Range(0.2f, 3f)]
        public float loopSize = 0.95f;

        [Tooltip("El sonido de la salida: lo que se oye cuando una hija se desprende y arranca. Suena una vez por hija, que es legible porque salen escalonadas.")]
        public AudioClip travelClip;

        [Tooltip("El sonido de la LLEGADA: lo que se oye cuando una hija alcanza su objetivo y lo revienta, y también cuando se deshace en el agua sin haber encontrado ninguno.\n\nAparte del 'Burst Clip' del poder a propósito: el estallido del impacto y el de una hija son dos cosas distintas, y con el mismo sonido el segundo se confunde con un eco del primero. Vacío usa el del poder.")]
        public AudioClip arriveClip;

        [Tooltip("Qué mide mientras viaja, respecto a una burbuja del tablero.")]
        [Range(0.2f, 1.5f)]
        public float size = 0.55f;

        [Tooltip("Cuánto se QUEDA PEGADA al llegar, antes de empezar a irse. En cero desaparece al tocar, que es lo que quiere una hija que revienta; una gota de tinta en cambio tiene que verse caer ENCIMA y quedarse ahí un momento.")]
        [Range(0f, 3f)]
        public float stickTime;

        [Tooltip("Y lo que tarda en desvanecerse después de ese momento pegada. Es cuando la burbuja de debajo cambia de color: lo que se va y lo que aparece son el mismo gesto.")]
        [Range(0f, 2f)]
        public float stickFade;

        // Desde que estalla el impacto hasta que llega la hija número 'index'. Quien lance el
        // viaje lo necesita para retrasar el pop de la burbuja de destino, y vive acá para que no
        // haya dos sitios sumando los mismos tramos.
        public float TotalFor(int index) => delay + index * stagger + duration;

        // Y cuándo empieza a desvanecerse, que es cuando lo de debajo tiene que cambiar.
        public float FadeStartFor(int index) => TotalFor(index) + stickTime;

        // La misma configuración con otro escalonado. La pide el Agua Clara, que manda una
        // burbuja por cada una que va a convertir: con veinte en el tablero, el 'stagger' puesto
        // a mano las repartiría en cinco segundos. Comprimiéndolo, el poder dura lo mismo con
        // tres que con veinte — igual que la cadena de pops.
        public Settings WithStagger(float value) => new()
        {
            enabled    = enabled,
            delay      = delay,
            stagger    = value,
            duration   = duration,
            loopAt     = loopAt,
            loopSpan   = loopSpan,
            loopSize   = loopSize,
            travelClip = travelClip,
            arriveClip = arriveClip,
            size       = size,
        };
    }

    // from/to en coordenadas del TABLERO, no del canvas: así el viaje acompaña al tablero si este
    // se desplaza por el scroll mientras la hija está en el aire.
    // Devuelve la Image de lo que sale volando, para que quien la mande pueda teñirla: la gota de
    // la Tinta va del color que reparte, y ese color lo sabe quien dispara, no esto.
    //
    // 'extraDelay' es una espera añadida a su turno. La usa la Tinta, que reparte DESPUÉS de que
    // el racimo del impacto termine de reventar.
    //
    // 'stickTo' es a qué se pega al aterrizar. Fijar la posición no basta: el tablero se desplaza
    // cuando se retira del cañón, y la burbuja de debajo puede caerse — en los dos casos la gota
    // se quedaba flotando donde ya no había nada (reportado por Diego). Colgando de ella, va
    // donde vaya, y si revienta se va con ella.
    public static Image Send(RectTransform gridContainer, Vector2 from, Vector2 to, Sprite sprite,
                             Settings s, int index, float extraDelay = 0f, RectTransform stickTo = null)
    {
        if (gridContainer == null || s == null || !s.enabled || sprite == null) return null;

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

        go.AddComponent<TravelingBubble>().Init(from, to, s, index, extraDelay, stickTo);

        return image;
    }

    RectTransform _rt;
    Image         _image;
    Settings      _settings;

    Vector2 _from, _to;
    Vector2 _along, _side;   // el marco del camino: hacia dónde va y qué es "a un lado"
    float   _spin;           // hacia qué lado se abre el rizo
    float   _elapsed;
    bool    _started;        // si ya arrancó, para sonar en el primer fotograma de vuelo y no en todos
    bool    _landed;         // si ya se posó: a partir de ahí no se mueve más
    RectTransform _stickTo;  // y de qué cuelga desde entonces
    float   _wait;           // lo que le toca esperar a ESTA, con su turno incluido

    // Qué parte del CAMINO se gasta mientras riza. Es poco a propósito: con avance normal la
    // vuelta se estira y sale una ondulación en vez de un bucle cerrado.
    const float DURING_LOOP = 0.12f;

    float _loopFrom, _loopTo;    // el rizo, en camino recorrido
    float _timeBefore, _timeLoop; // y en tiempo: cuándo empieza y cuánto dura

    void Init(Vector2 from, Vector2 to, Settings s, int index, float extraDelay = 0f, RectTransform stickTo = null)
    {
        _rt       = (RectTransform)transform;
        _image    = GetComponent<Image>();
        _from     = from;
        _to       = to;
        _settings = s;

        // Las dos hijas rizan hacia lados contrarios. Rizando igual salen en paralelo y parecen
        // una sola cosa partida en dos en vez de dos que van a sitios distintos.
        _spin = index % 2 == 0 ? 1f : -1f;

        // Su turno: cada una sale un poco después que la anterior, más lo que quien la mande
        // haya querido esperar antes de empezar el reparto entero.
        _wait = extraDelay + s.delay + index * s.stagger;

        _stickTo = stickTo;
        _along   = (to - from).normalized;
        _side  = new Vector2(-_along.y, _along.x) * _spin;

        // El reparto del viaje, resuelto una sola vez: no depende del instante y lo necesitan
        // tanto Advance (cuánto llevo recorrido) como At (si estoy rizando ahora).
        _loopFrom = Mathf.Clamp(s.loopAt, 0f, 1f - DURING_LOOP);   // camino hecho antes del rizo
        _loopTo   = _loopFrom + DURING_LOOP;                        // y con el rizo ya dado

        // Sin rizo no hay parada en medio: el tiempo entero es de los dos tramos rectos, que así
        // se funden en uno solo y la gota cae de un tirón.
        _timeLoop  = s.loopSize > 0f ? Mathf.Clamp01(s.loopSpan) : 0f;

        // Lo que el rizo no usa se reparte entre los dos tramos rectos EN PROPORCIÓN A SU LARGO,
        // no a partes iguales: si no, el tramo corto se recorrería despacio y el largo volando,
        // y el viaje cambiaría de ritmo sin que nada lo justifique.
        float straight = _loopFrom + (1f - _loopTo);
        _timeBefore = straight > 0f ? (1f - _timeLoop) * _loopFrom / straight : (1f - _timeLoop) * 0.5f;

        Show(false);
    }

    void Update()
    {
        _elapsed += Time.deltaTime;

        float t = _elapsed - _wait;
        if (t < 0f) return;

        Show(true);

        if (!_started)
        {
            _started = true;
            AudioManager.Instance?.PlaySfx(_settings.travelClip);
        }

        // Lo que se queda pegada al llegar, y lo que tarda en irse después. Cero en las dos es el
        // comportamiento de siempre: llega y desaparece.
        float stick = Mathf.Max(0f, _settings.stickTime);
        float fade  = Mathf.Max(0f, _settings.stickFade);

        if (t < _settings.duration)
        {
            Place(At(Mathf.Clamp01(t / _settings.duration)));

            // Sin pegado, se apaga en el último tramo del vuelo: tiene que llegar entera para que
            // el pop de la burbuja de destino se lea como consecuencia suya.
            if (stick <= 0f && fade <= 0f)
            {
                float p = Mathf.Clamp01(t / _settings.duration);
                var   c = _image.color;

                if (p > 0.9f) _image.color = new Color(c.r, c.g, c.b, Mathf.InverseLerp(1f, 0.9f, p));
            }

            return;
        }

        // Ya llegó. Sin pegado ni desvanecido, se va como siempre.
        if (stick <= 0f && fade <= 0f) { Place(At(1f)); Destroy(gameObject); return; }

        // Con pegado, se queda QUIETA del todo: la posición se fija una vez y no se vuelve a
        // tocar, y se endereza. Place la deja girada hacia donde venía, y una gota posada encima
        // de una burbuja tiene que estar vertical — ese resto de giro es el movimiento que se
        // seguía notando mientras debería estar parada (pedido de Diego).
        if (!_landed)
        {
            _landed = true;
            _rt.localEulerAngles = Vector3.zero;

            // Pegada a lo que alcanzó: a partir de acá su posición es la de esa burbuja, no una
            // coordenada del tablero. Centrada sobre ella y sin volver a tocarla.
            if (_stickTo != null)
            {
                _rt.SetParent(_stickTo, worldPositionStays: false);
                _rt.anchorMin = _rt.anchorMax = _rt.pivot = new Vector2(0.5f, 0.5f);
                _rt.anchoredPosition = Vector2.zero;
                _rt.SetAsLastSibling();
            }
            else
            {
                _rt.anchoredPosition = At(1f);
            }
        }

        // Si la burbuja de debajo desapareció —la reventó una cadena, se cayó— la gota se va con
        // ella: una mancha flotando sobre el agua no tiene sentido.
        if (_stickTo == null && transform.parent == null) { Destroy(gameObject); return; }

        float after = t - _settings.duration;

        // Quieta encima de lo que alcanzó: es el momento en que se LEE que cayó ahí.
        if (after < stick) return;

        if (fade <= 0f) { Destroy(gameObject); return; }

        // Y se va. Mientras se va, lo de debajo cambia: los dos gestos son el mismo.
        float gone = Mathf.Clamp01((after - stick) / fade);
        var   tint = _image.color;

        _image.color = new Color(tint.r, tint.g, tint.b, 1f - gone);

        if (gone >= 1f) Destroy(gameObject);
    }

    // Dónde está la hija en el instante t del viaje.
    Vector2 At(float t)
    {
        Vector2 at = Vector2.Lerp(_from, _to, Advance(t));

        // Sin rizo, línea recta: lo pide una gota que cae: un bucle en el aire la convertiría en
        // otra cosa. El reparto de tiempos de Advance sigue valiendo, solo que sin la vuelta.
        if (_settings.loopSize <= 0f) return at;

        float loop = Mathf.InverseLerp(_timeBefore, _timeBefore + _timeLoop, t);
        if (loop <= 0f || loop >= 1f) return at;

        // El rizo es un círculo apoyado EN el camino: su centro está a un radio hacia el lado, y
        // la vuelta empieza y acaba justo sobre la línea. Así entra y sale del trazo sin salto,
        // que es lo que lo hace parecer un bucle del propio camino y no un adorno encima.
        float   radius = _settings.loopSize * HexGridMath.BubbleDiameter;
        float   turn   = loop * Mathf.PI * 2f;
        Vector2 center = at + _side * radius;

        return center + _along * Mathf.Sin(turn) * radius - _side * Mathf.Cos(turn) * radius;
    }

    // Cuánto lleva recorrido del camino en el instante t, y con qué ritmo (pedido de Diego):
    //
    //   sale DISPARADA y va frenando hasta el rizo  → arranca con ganas y se pone a tiro
    //   da la vuelta despacio y pareja              → el rizo es lo que hay que mirar
    //   sale despacio y ACELERA hasta el final      → termina como un impacto, no como un roce
    //
    // Es lo contrario del smoothstep que había en cada tramo: aquel arrancaba lento y terminaba
    // lento tres veces seguidas, y el viaje entero se leía como un empujón flojo repetido.
    //
    // La velocidad casa en las dos costuras, que es lo que lo hace un solo gesto: el primer tramo
    // llega al rizo ya frenado y el último sale de él igual de lento.
    float Advance(float t)
    {
        if (t <= _timeBefore)
        {
            float x = t / Mathf.Max(0.0001f, _timeBefore);
            return EaseOut(x) * _loopFrom;
        }

        if (t < _timeBefore + _timeLoop)
        {
            float x = (t - _timeBefore) / Mathf.Max(0.0001f, _timeLoop);
            return Mathf.Lerp(_loopFrom, _loopTo, x);   // pareja: el giro ya pone el movimiento
        }

        float after = (t - _timeBefore - _timeLoop) / Mathf.Max(0.0001f, 1f - _timeBefore - _timeLoop);

        return Mathf.Lerp(_loopTo, 1f, EaseIn(after));
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

    // Rápido al principio, frenando al final.
    static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    // Y al revés: lento al principio, disparado al final.
    static float EaseIn(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t;
    }
}
