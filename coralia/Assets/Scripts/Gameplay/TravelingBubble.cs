using UnityEngine;
using UnityEngine.UI;

// Una burbuja que sale del impacto y viaja hasta otra celda del tablero.
//
// Existe por la Burbuja Madre: las dos hijas que no revientan donde pegó el disparo van a buscar
// burbujas que pueden estar en la otra punta. Sin verlas ir, dos burbujas lejanas desaparecen a
// la vez que la del impacto y se lee como un fallo del juego, no como un poder.
//
// El viaje son TRES tiempos y no uno, porque cada uno cuenta algo distinto:
//
//   1. el remolino — sale de la burbuja que estalló y se coloca mirando a su objetivo
//   2. la pausa    — el instante en que se la ve apuntada, que es lo que hace legible lo que va a pasar
//   3. la carrera  — recta y con ease, hasta la burbuja que le toca
//
// Mezclados en un solo movimiento curvo todo ocurría a la vez y no se entendía ninguno de los
// tres. Separados, la secuencia se lee aunque dure medio segundo.
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

        [Tooltip("Lo que dura el remolino de salida. La hija gira sobre el sitio y acaba encarada a su objetivo.")]
        [Range(0.05f, 1.5f)]
        public float spiralTime = 0.45f;

        [Tooltip("Vueltas de ese remolino.")]
        [Range(0.25f, 5f)]
        public float spiralTurns = 1.5f;

        [Tooltip("Lo lejos del centro que acaba el remolino, en burbujas. Es también lo que la hija se adelanta hacia su objetivo antes de salir corriendo.")]
        [Range(0.1f, 3f)]
        public float spiralRadius = 0.9f;

        [Tooltip("El instante quieta entre el remolino y la carrera. Corto: es una respiración antes de salir disparada y lo que deja ver hacia dónde apunta.")]
        [Range(0f, 0.8f)]
        public float pause = 0.12f;

        [Tooltip("Lo que dura la carrera hasta el objetivo. Es también lo que esa burbuja espera antes de reventar: lo que se ve y lo que pasa van juntos.")]
        [Range(0.1f, 2f)]
        public float duration = 0.4f;

        [Tooltip("Qué mide la hija mientras viaja, respecto a una burbuja del tablero.")]
        [Range(0.2f, 1f)]
        public float size = 0.55f;

        // Lo que tarda desde que estalla el impacto hasta que la hija llega. Quien lance el viaje
        // lo necesita para retrasar el pop de la burbuja de destino, y vive acá para que no haya
        // dos sitios sumando los mismos tramos.
        public float Total => delay + spiralTime + pause + duration;
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

    Vector2 _from, _to, _launch;
    float   _aim;      // ángulo hacia el objetivo
    float   _spin;     // hacia qué lado da las vueltas
    float   _elapsed;

    void Init(Vector2 from, Vector2 to, Settings s, int index)
    {
        _rt       = (RectTransform)transform;
        _image    = GetComponent<Image>();
        _from     = from;
        _to       = to;
        _settings = s;

        // Las dos hijas giran hacia lados contrarios. Girando igual salen en paralelo y parecen
        // una sola cosa partida en dos en vez de dos que van a sitios distintos.
        _spin = index % 2 == 0 ? 1f : -1f;

        Vector2 path = to - from;
        _aim = Mathf.Atan2(path.y, path.x);

        // Donde acaba el remolino: adelantada hacia su objetivo. De ahí arranca la carrera, así
        // que la recta final sale de un punto que ya estaba encarado.
        _launch = from + new Vector2(Mathf.Cos(_aim), Mathf.Sin(_aim)) * s.spiralRadius * HexGridMath.BubbleDiameter;

        Show(false);
    }

    void Update()
    {
        _elapsed += Time.deltaTime;

        float t = _elapsed - _settings.delay;

        // 1. Esperando a que reviente la burbuja del impacto.
        if (t < 0f) return;

        Show(true);

        // 2. El remolino. El ángulo TERMINA mirando al objetivo y empieza las vueltas que haga
        // falta por detrás, así que el giro no es un adorno: es la hija colocándose.
        if (t < _settings.spiralTime)
        {
            float p = Ease(t / _settings.spiralTime);

            float from  = _aim - _settings.spiralTurns * Mathf.PI * 2f * _spin;
            float angle = Mathf.Lerp(from, _aim, p);

            // El radio se abre desde CERO: naciendo ya desplazada, la hija aparece separada de la
            // burbuja que la soltó y se pierde de dónde salió.
            float radius = p * _settings.spiralRadius * HexGridMath.BubbleDiameter;

            Place(_from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            return;
        }

        t -= _settings.spiralTime;

        // 3. La respiración. Quieta y ya encarada, que es lo que anuncia lo que viene.
        if (t < _settings.pause) { Place(_launch); return; }

        t -= _settings.pause;

        // 4. La carrera. Recta y con ease: arranca de golpe y frena al llegar.
        float run = Mathf.Clamp01(t / _settings.duration);
        Place(Vector2.Lerp(_launch, _to, Ease(run)));

        // Se apaga justo al final, no durante: tiene que llegar entera para que el pop de la
        // burbuja de destino se lea como consecuencia suya.
        if (run > 0.85f)
        {
            var c = _image.color;
            _image.color = new Color(c.r, c.g, c.b, Mathf.InverseLerp(1f, 0.85f, run));
        }

        if (run >= 1f) Destroy(gameObject);
    }

    // Mira hacia donde se mueve de verdad, remolino incluido: calculado sobre la recta final, la
    // hija apuntaría al objetivo mientras todavía está dando vueltas.
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
