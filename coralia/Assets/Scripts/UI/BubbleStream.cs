using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Burbujas que salen de un punto fijo de la pantalla y suben describiendo un cuarto de círculo,
// hasta apagarse. Pensado para los adornos de las esquinas del mapa: el adorno no se mueve y las
// burbujas le dan vida sin competir con el juego.
//
// Va en UI y no en el mundo a propósito. Estas burbujas acompañan al MARCO de la pantalla, no al
// arrecife: tienen que quedarse en su esquina mientras el mapa se desplaza por debajo, y si
// vivieran en el mundo se irían con él.
//
// El recorrido es un cuarto de círculo y no una recta porque una burbuja que sube a plomo se lee
// como un objeto cayendo al revés. La curva sugiere que hay una corriente empujándola.
//
// Las burbujas se reciclan en vez de crearse y destruirse. Es un emisor que no para nunca: a un
// ritmo de tres por segundo, una partida de diez minutos serían casi dos mil objetos creados y
// tirados, o sea basura constante para el recolector y microcortes en el dispositivo.
public class BubbleStream : MonoBehaviour
{
    [Tooltip("De cuáles se elige al azar. Con una sola sirve, pero varias hacen que no se note el patrón.")]
    [SerializeField] Sprite[] sprites;

    [Tooltip("Hacia dónde se curva el recorrido: a la derecha o a la izquierda del punto de salida. En la esquina izquierda de la pantalla se quiere hacia adentro, y al revés en la derecha.")]
    [SerializeField] bool curveRight = true;

    [Header("Recorrido")]
    [Tooltip("Radio del cuarto de círculo, en píxeles. Es también cuánto sube y cuánto se corre de lado: la burbuja termina a esa distancia en las dos direcciones.")]
    [SerializeField] float radius = 320f;

    [Tooltip("Cuánto varía ese radio entre una burbuja y otra, en fracción. Sin esto todas siguen exactamente la misma línea y se ve el truco.")]
    [Range(0f, 0.8f)]
    [SerializeField] float radiusVariation = 0.35f;

    [Tooltip("Cuánto se reparte el punto de salida alrededor del origen, en píxeles.")]
    [SerializeField] Vector2 spread = new(40f, 25f);

    [Tooltip("Cuánto se bambolea de lado mientras sube, en píxeles.")]
    [SerializeField] float wobble = 12f;

    [Header("Ritmo")]
    [Tooltip("Segundos entre una burbuja y la siguiente: se sortea dentro de este rango.")]
    [SerializeField] Vector2 interval = new(0.35f, 1.2f);

    [Tooltip("Cuánto tarda cada una en hacer todo el recorrido.")]
    [SerializeField] Vector2 duration = new(3.5f, 6f);

    [SerializeField] Vector2 size = new(14f, 38f);

    [Tooltip("Tope de burbujas a la vez. Es una red de seguridad: con intervalos cortos y recorridos largos se acumulan más de las que uno cree.")]
    [SerializeField] int maxAlive = 14;

    [Range(0f, 1f)]
    [SerializeField] float maxAlpha = 0.75f;

    // Lo que hace falta saber de una burbuja viva. Struct y no una clase por burbuja: son datos,
    // no comportamiento, y así no hay un MonoBehaviour por cada una.
    struct Live
    {
        public RectTransform rect;
        public Image         image;
        public Vector2       origin;
        public float         radius;
        public float         duration;
        public float         time;
        public float         phase;   // para que el bamboleo no vaya sincronizado entre todas
    }

    readonly List<Live> _alive = new();
    readonly Stack<Live> _idle = new();

    float _nextIn;

    void OnEnable()  => _nextIn = Random.Range(interval.x, interval.y);

    void Update()
    {
        _nextIn -= Time.deltaTime;

        if (_nextIn <= 0f)
        {
            _nextIn = Random.Range(interval.x, Mathf.Max(interval.x, interval.y));
            if (_alive.Count < maxAlive) Emit();
        }

        // Hacia atrás porque se quitan elementos en el camino.
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            var bubble = _alive[i];
            bubble.time += Time.deltaTime;

            if (bubble.time >= bubble.duration)
            {
                bubble.rect.gameObject.SetActive(false);
                _idle.Push(bubble);
                _alive.RemoveAt(i);
                continue;
            }

            Move(bubble);
            _alive[i] = bubble;
        }
    }

    void Move(Live bubble)
    {
        float p = bubble.time / bubble.duration;

        // El cuarto de círculo. El centro del arco queda al lado del punto de salida, así que a
        // 180 grados la burbuja está justo ahí y a 90 ya subió y se corrió lo mismo hacia el lado.
        // Sale hacia arriba y termina yéndose de costado, que es el gesto de una corriente.
        float angle = Mathf.Lerp(180f, 90f, p) * Mathf.Deg2Rad;
        float side   = curveRight ? 1f : -1f;

        Vector2 offset = new(side * bubble.radius * (1f + Mathf.Cos(angle)),
                                    bubble.radius * Mathf.Sin(angle));

        // El bamboleo va perpendicular al recorrido, no en X fija: sobre el tramo final, donde la
        // burbuja ya viaja de lado, un vaivén horizontal se confundiría con el propio avance.
        offset += new Vector2(Mathf.Sin(angle), side * Mathf.Cos(angle))
                  * (Mathf.Sin(p * Mathf.PI * 3f + bubble.phase) * wobble);

        bubble.rect.anchoredPosition = bubble.origin + offset;

        // Entra rápido y se apaga despacio: aparecer de golpe delata dónde nace, y desaparecer de
        // golpe delata dónde muere.
        float alpha = maxAlpha * Mathf.Min(1f, p / 0.12f) * (1f - Mathf.SmoothStep(0.6f, 1f, p));

        var color = bubble.image.color;
        bubble.image.color = new Color(color.r, color.g, color.b, alpha);
    }

    void Emit()
    {
        if (sprites == null || sprites.Length == 0) return;

        var bubble = _idle.Count > 0 ? _idle.Pop() : Create();
        float side = Random.Range(size.x, size.y);

        bubble.image.sprite   = sprites[Random.Range(0, sprites.Length)];
        bubble.rect.sizeDelta = Vector2.one * side;
        bubble.origin         = new Vector2(Random.Range(-spread.x, spread.x),
                                            Random.Range(-spread.y, spread.y));
        bubble.radius   = radius * (1f + Random.Range(-radiusVariation, radiusVariation));
        bubble.duration = Mathf.Max(0.1f, Random.Range(duration.x, duration.y));
        bubble.time     = 0f;
        bubble.phase    = Random.Range(0f, Mathf.PI * 2f);

        bubble.rect.gameObject.SetActive(true);
        Move(bubble);

        _alive.Add(bubble);
    }

    Live Create()
    {
        var go = new GameObject("Bubble", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;

        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot     = new Vector2(0.5f, 0.5f);

        var image = go.GetComponent<Image>();
        image.raycastTarget = false; // nunca deben robarle el toque al mapa que tienen debajo

        return new Live { rect = rect, image = image };
    }
}
