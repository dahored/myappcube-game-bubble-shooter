using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// El DISPARO de una demostración de tutorial: la burbuja esperando en el cañón, la mano que
// apunta y la línea de puntos que sale de ella.
//
// Existe porque es lo que convierte una demostración en una instrucción. Sin disparo, el tablero
// se mueve solo y lo que el jugador ve es un efecto; con él, ve el gesto que tiene que hacer —y
// todos los tutoriales del juego lo enseñan así (pedido de Diego).
//
// Va aparte en vez de repetirse en cada demo por el mismo motivo que BubbleGlow: son muchos
// sitios donde la misma coreografía se puede separar de sí misma, y la que se quede atrás enseña
// un gesto que ya no es el del juego.
//
// Se guarda como campo [SerializeField] de la demostración, así que sus ajustes salen en el
// Inspector agrupados bajo un solo desplegable.
[System.Serializable]
public class TutorialShot
{
    [Tooltip("La mano que apunta. La misma que usa ShootHintView dentro del gameplay: conviene que sea la misma para que el gesto se reconozca de un sitio a otro.")]
    public Sprite hand;

    [Tooltip("El punto de la línea de trayectoria. Vacío usa la burbuja del disparo en chiquito, que es lo que hace la línea del juego de verdad.")]
    public Sprite dot;

    [Tooltip("Color de los puntos de la mira.")]
    public Color dotColor = new(1f, 1f, 1f, 0.85f);

    [Range(4, 24)]
    [Tooltip("Cuántos puntos tiene la línea.")]
    public int dots = 10;

    [Range(0.1f, 0.6f)]
    [Tooltip("Tamaño de cada punto, en tanto por uno de la burbuja.")]
    public float dotSize = 0.2f;

    [Range(0f, 60f)]
    [Tooltip("Cuánto se desvía la mira al empezar a apuntar, en grados. Es lo que convierte el gesto en APUNTAR: la línea entra torcida y se va asentando sobre el objetivo. En cero, la mano va derecho del cañón al destino y se lee como si arrastrara la burbuja hasta ahí.")]
    public float sweep = 30f;

    [Range(0.1f, 0.9f)]
    [Tooltip("A qué altura de la línea se dibuja la mano, en tanto por uno de la distancia al objetivo.")]
    public float handDistance = 0.42f;

    [Header("Tiempos (segundos)")]
    public float aimTime     = 0.7f;    // la mano va del cañón al punto de mira
    public float releaseTime = 0.15f;   // el tirón al soltar
    public float flyTime     = 0.45f;   // la burbuja sube

    readonly List<Image> _trail = new();

    RectTransform _area;
    Image         _bubble;
    Image         _hand;
    Vector2       _aimTo;     // hacia dónde apunta la mira ahora: la línea sale del cañón hasta acá
    float         _size;

    // La burbuja que viaja. La demostración la necesita para reventarla junto al racimo que
    // completa: el disparo se va CON ellas, no se queda pegado.
    public Image Bubble => _bubble;

    public bool Ready => hand != null;

    // Se llama desde el Build de la demostración y DESPUÉS de las burbujas del tablero: en UGUI
    // el orden de dibujo es el de la jerarquía, así que lo que se crea al final pasa por delante.
    //
    // Los puntos van primero entre los suyos, para que la línea quede por debajo de la burbuja.
    public void Build(RectTransform area, float bubbleSize)
    {
        _area = area;
        _size = bubbleSize;
        _trail.Clear();

        for (int i = 0; i < dots; i++)
        {
            var point = Spawn("Dot", dot, bubbleSize * dotSize);
            point.color = dotColor;
            _trail.Add(point);
        }

        _bubble = BubbleGlow.AddTo(Spawn("Shot", null, bubbleSize));
        _hand   = Spawn("Hand", hand, bubbleSize * 1.5f);
    }

    Image Spawn(string label, Sprite sprite, float size)
    {
        var image = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                        .GetComponent<Image>();
        var rt = (RectTransform)image.transform;

        rt.SetParent(_area, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * size;

        image.sprite         = sprite;
        image.raycastTarget  = false;   // es una demostración: nada acá se toca
        image.preserveAspect = true;

        return image;
    }

    static void Place(Graphic graphic, Vector2 at)
    {
        if (graphic != null) ((RectTransform)graphic.transform).anchoredPosition = at;
    }

    // Deja la burbuja cargada en el cañón, de ese color, con la mano encima de ella.
    //
    // La mano arranca SOBRE la burbuja y no en un sitio cualquiera: el gesto empieza tocándola, y
    // si apareciera ya a medio camino no se vería de dónde sale.
    public void Arm(Sprite bubbleSprite, Vector2 cannon, Vector2 target)
    {
        if (_bubble == null) return;

        _bubble.sprite              = bubbleSprite;

        // Sin punto propio, la línea se dibuja con la burbuja del disparo en chiquito — que es
        // lo que hace la mira del juego cuando no tiene el suyo. Se resuelve acá y no en Build
        // porque hasta este momento no se sabe de qué color va el disparo.
        //
        // Antes se pasaba 'dot' tal cual: vacío, la Image se quedaba sin sprite y la línea
        // sencillamente no se dibujaba, aunque el tooltip prometiera el respaldo.
        if (dot == null)
            foreach (var point in _trail)
                if (point != null) point.sprite = bubbleSprite;
        // El OBJETO y no solo la Image: BubbleGlow le cuelga el brillo como hijo, y apagando
        // únicamente el componente el aro se quedaba flotando en el cañón entre disparo y
        // disparo.
        _bubble.gameObject.SetActive(true);
        _bubble.color               = Color.white;
        _bubble.transform.localScale = Vector3.one;
        ((RectTransform)_bubble.transform).sizeDelta = Vector2.one * _size;
        Place(_bubble, cannon);

        if (_hand != null)
        {
            _hand.enabled              = true;
            _hand.transform.localScale = Vector3.one;
            Place(_hand, HandAtCannon(cannon));
        }

        _aimTo = AimPointAt(cannon, target, StartAngle(cannon, target));
        ShowTrail(cannon, 0f);
    }

    // --- La mira ---
    //
    // El dedo no ARRASTRA la burbuja hasta el hueco: elige una DIRECCIÓN, y la línea sale del
    // cañón hacia ahí. Por eso entra desviada y se va asentando sobre el objetivo, en vez de que
    // la mano viaje derecho del cañón al destino — eso se lee como arrastrar, que no es el gesto.

    static float AngleTo(Vector2 cannon, Vector2 target)
    {
        Vector2 d = target - cannon;
        return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
    }

    float StartAngle(Vector2 cannon, Vector2 target) => AngleTo(cannon, target) + sweep;

    // Hasta dónde llega la mira con ese ángulo: siempre la misma distancia, así la línea no se
    // estira ni se encoge mientras barre.
    static Vector2 AimPointAt(Vector2 cannon, Vector2 target, float degrees)
    {
        float   rad = degrees * Mathf.Deg2Rad;
        Vector2 dir = new(Mathf.Sin(rad), Mathf.Cos(rad));

        return cannon + dir * Vector2.Distance(cannon, target);
    }

    Vector2 HandAtCannon(Vector2 cannon) => cannon + new Vector2(_size * 0.4f, -_size * 0.6f);

    // La mano va SOBRE la línea, corrida a un lado para no taparla.
    Vector2 HandOnAim(Vector2 cannon) =>
        Vector2.Lerp(cannon, _aimTo, handDistance) + new Vector2(_size * 0.6f, -_size * 0.35f);

    // Cuánta línea se ve, de 0 a 1. Completa desde el primer frame mientras se apunta —como en el
    // juego—: lo que cambia es hacia dónde apunta, no cuánto mide.
    void ShowTrail(Vector2 cannon, float amount)
    {
        for (int i = 0; i < _trail.Count; i++)
        {
            float at = (i + 1) / (float)(_trail.Count + 1);

            _trail[i].enabled = at <= amount;

            if (_trail[i].enabled) Place(_trail[i], Vector2.Lerp(cannon, _aimTo, at));
        }
    }

    public IEnumerator Aim(Vector2 cannon, Vector2 target)
    {
        Vector2 handFrom = HandAtCannon(cannon);
        float   from     = StartAngle(cannon, target);
        float   to       = AngleTo(cannon, target);

        for (float t = 0f; t < aimTime; t += Time.unscaledDeltaTime)
        {
            float p = Smooth(t / aimTime);

            _aimTo = AimPointAt(cannon, target, Mathf.Lerp(from, to, p));
            Place(_hand, Vector2.Lerp(handFrom, HandOnAim(cannon), p));
            ShowTrail(cannon, 1f);
            yield return null;
        }

        _aimTo = target;
        Place(_hand, HandOnAim(cannon));
        ShowTrail(cannon, 1f);
    }

    public IEnumerator Release()
    {
        for (float t = 0f; t < releaseTime; t += Time.unscaledDeltaTime)
        {
            float p = t / releaseTime;

            if (_hand != null) _hand.transform.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }

        if (_hand != null) _hand.enabled = false;
    }

    public IEnumerator Fly(Vector2 cannon, Vector2 target)
    {
        for (float t = 0f; t < flyTime; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / flyTime);

            Place(_bubble, Vector2.Lerp(cannon, target, p));
            ShowTrail(cannon, 1f - p);   // la línea se consume por detrás de la burbuja
            yield return null;
        }

        Place(_bubble, target);
        ShowTrail(cannon, 0f);
    }

    // Lo deja todo apagado: entre un disparo y el siguiente no debe quedar ni mano ni línea.
    public void Hide()
    {
        if (_bubble != null) _bubble.gameObject.SetActive(false);
        if (_hand != null)   _hand.enabled   = false;

        foreach (var point in _trail)
            if (point != null) point.enabled = false;
    }

    static float Smooth(float p) => p * p * (3f - 2f * p);
}
