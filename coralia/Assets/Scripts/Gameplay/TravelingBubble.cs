using UnityEngine;
using UnityEngine.UI;

// Una burbuja que sale del impacto y viaja hasta otra celda del tablero.
//
// Existe por la Burbuja Madre: las dos hijas que no revientan donde pegó el disparo van a buscar
// burbujas que pueden estar en la otra punta. Sin verlas ir, dos burbujas lejanas desaparecen a
// la vez que la del impacto y se lee como un fallo del juego, no como un poder.
//
// El camino es una CURVA y no una recta. Una recta entre dos celdas es el trazo de un proyectil;
// un arco se lee como algo vivo que va nadando a buscar, que es lo que la Madre suelta.
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

        [Tooltip("Cuánto tarda el viaje. Es también lo que espera cada objetivo antes de reventar: lo que se ve y lo que pasa van juntos.")]
        [Range(0.1f, 2.5f)]
        public float duration = 0.7f;

        [Tooltip("Vueltas que da sobre sí misma al salir. El remolino se abre al principio y se deshace según coge camino, así que se ve de dónde salió sin que el viaje entero parezca un tirabuzón.")]
        [Range(0f, 5f)]
        public float spiralTurns = 2.2f;

        [Tooltip("Lo ancho que llega a ser ese remolino, en burbujas.")]
        [Range(0f, 3f)]
        public float spiralRadius = 1.1f;

        [Tooltip("Cuánto se arquea el camino, en tanto por uno de la distancia. En cero va derecho y parece un disparo.")]
        [Range(0f, 0.8f)]
        public float arc = 0.3f;

        [Tooltip("Qué mide la hija mientras viaja, respecto a una burbuja del tablero.")]
        [Range(0.2f, 1f)]
        public float size = 0.55f;
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
    Vector2       _from, _to, _control;
    Settings      _settings;
    float         _elapsed;
    float         _spin;    // hacia qué lado da las vueltas
    Vector2       _last;    // dónde estaba el frame anterior, para saber hacia dónde mira

    void Init(Vector2 from, Vector2 to, Settings s, int index)
    {
        _rt       = (RectTransform)transform;
        _image    = GetComponent<Image>();
        _from     = from;
        _to       = to;
        _settings = s;
        _spin     = index % 2 == 0 ? 1f : -1f;
        _last     = from;

        // Escondida hasta que le toque salir: durante la espera la escena es el estallido de la
        // burbuja del impacto, y una hija quieta encima lo ensucia.
        var c = _image.color;
        _image.color = new Color(c.r, c.g, c.b, 0f);

        // El punto de control del arco sale PERPENDICULAR al camino, y hacia un lado distinto
        // según el índice: con las dos hijas curvándose igual, salen en paralelo y parecen una
        // sola cosa partida en dos. Abriéndose hacia lados contrarios se ve que van a sitios
        // distintos desde el primer fotograma.
        Vector2 mid  = (from + to) * 0.5f;
        Vector2 path = to - from;
        var perpendicular = new Vector2(-path.y, path.x).normalized;

        _control = mid + perpendicular * path.magnitude * s.arc * _spin;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;

        if (_elapsed < _settings.delay) return;

        float t = Mathf.Clamp01((_elapsed - _settings.delay) / _settings.duration);

        // El AVANCE va con ease in-out: sale despacio, coge velocidad y frena al llegar. Es lo
        // que deja ver el remolino — arrancando a velocidad de crucero, las vueltas quedan
        // estiradas por el camino y no se leen como vueltas.
        float move = t * t * (3f - 2f * t);
        float u    = 1f - move;

        // Bézier cuadrática: el arco entero con tres puntos y sin curva que configurar.
        Vector2 at = u * u * _from + 2f * u * move * _control + move * move * _to;

        // Y encima, un remolino. Va SUMADO al camino en vez de ser una fase aparte: en dos fases
        // la burbuja gira quieta y luego arranca, y el tirón entre una y otra se ve.
        //
        // Se abre desde CERO, no desde su ancho máximo: naciendo ya desplazada, la hija aparece
        // separada de la burbuja que la soltó y se pierde de dónde salió. La campana sube rápido
        // —pico alrededor de un quinto del viaje— y se deshace despacio.
        float bell  = Mathf.Sin(Mathf.Pow(t, 0.45f) * Mathf.PI);
        float swirl = bell * _settings.spiralRadius * HexGridMath.BubbleDiameter;

        if (swirl > 0.01f)
        {
            // El ángulo va con t LINEAL, no con el avance: girando también con ease, el remolino
            // se abriría y cerraría al ritmo del viaje y dejaría de leerse como un giro.
            float angle = t * _settings.spiralTurns * 360f * Mathf.Deg2Rad * _spin;
            at += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * swirl;
        }

        _rt.anchoredPosition = at;

        var color = _image.color;
        _image.color = new Color(color.r, color.g, color.b, 1f);

        // Mira hacia donde va de verdad, remolino incluido: calculada sobre la curva a secas,
        // la hija apuntaría recta mientras da vueltas, que es lo que delata que el giro está
        // pegado encima en vez de ser su camino.
        Vector2 heading = at - _last;
        _last = at;

        if (heading.sqrMagnitude > 0.01f)
            _rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg - 90f);

        // Se apaga justo al final, no durante: tiene que llegar entera para que el pop de la
        // burbuja de destino se lea como consecuencia suya.
        if (t > 0.85f)
        {
            var c = _image.color;
            _image.color = new Color(c.r, c.g, c.b, Mathf.InverseLerp(1f, 0.85f, t));
        }

        if (t >= 1f) Destroy(gameObject);
    }
}
