using UnityEngine;
using UnityEngine.UI;

// El estallido de la Bomba de Coral: un destello que lava la pantalla entera y una onda que se
// abre desde el punto del impacto.
//
// Son dos cosas distintas a propósito. El destello dice "pasó algo grande" sin importar dónde
// estuviera mirando el jugador; la onda dice DÓNDE y HASTA DÓNDE. Con solo el destello, la
// explosión no tiene sitio; con solo la onda, no tiene fuerza.
//
// Mismo patrón que Sparkle y PopParticle: cada pieza se anima sola y se destruye al terminar. Si
// dependiera de una corrutina de quien la lanzó, al acabar el nivel —que es cuando el tablero se
// limpia y todo se destruye— el destello se quedaría congelado tapando la pantalla.
//
// Son Images de UI y no un ParticleSystem por lo mismo que Sparkle: el canvas de Gameplay es
// Screen Space - Overlay y ahí las partículas se dibujarían detrás del fondo.
public class BombBlast : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Header("Carga")]
        [Tooltip("Cuánto tarda la bomba en estallar desde que se pega al tablero. Mientras tanto se va encendiendo. Es la pausa que convierte el disparo en un acontecimiento: sin ella la bomba cae y revienta en el mismo instante, y no se llega a ver que fue ELLA la que explotó. También es el hueco donde cabe el silencio inicial del sonido.")]
        [Range(0f, 4f)]
        public float chargeTime = 1.2f;

        [Tooltip("Color de la luz que la bomba va acumulando.")]
        public Color chargeColor = new(1f, 0.92f, 0.6f, 0.95f);

        [Tooltip("Diámetro que alcanza esa luz justo antes de estallar, en píxeles del tablero (una burbuja mide 92).")]
        public float chargeSize = 300f;

        [Tooltip("Cuántos latidos por segundo da la luz al final de la carga. Acelera a medida que se acerca el estallido, que es lo que lo hace sentir inminente en vez de ser un brillo que crece y ya.")]
        [Range(0f, 20f)]
        public float chargePulse = 9f;

        [Header("Destello de pantalla")]
        [Tooltip("Color de la luz. El alfa manda: por encima de 0,7 lava el tablero y deja de verse qué explotó.")]
        public Color flashColor = new(1f, 0.97f, 0.85f, 0.6f);

        [Tooltip("Cuánto más grande que la pantalla es el resplandor, contado sobre su diagonal. Por debajo de 1 la luz no llega a las esquinas y se ve el borde del halo; de 2 para arriba queda casi plano y vuelve a parecer una capa de pintura.")]
        [Range(0.8f, 3f)]
        public float flashSpread = 1.5f;

        [Tooltip("Cuánto tarda en encenderse. Muy corto a propósito: un destello que sube despacio se lee como que la pantalla se está poniendo blanca, no como un fogonazo.")]
        public float flashRise = 0.05f;

        [Tooltip("Y cuánto en apagarse.")]
        public float flashFall = 0.45f;

        [Header("Onda expansiva")]
        public Color waveColor = new(1f, 1f, 1f, 0.9f);

        [Tooltip("Diámetro con el que nace la onda, en píxeles del tablero (una burbuja mide 92).")]
        public float waveFrom = 70f;

        [Tooltip("Y hasta dónde se abre.")]
        public float waveTo = 1000f;

        public float waveDuration = 0.55f;

        [Tooltip("Grosor del filo de la onda: bajo da un aro nítido, alto un frente difuso.")]
        [Range(0.02f, 0.4f)]
        public float waveEdge = 0.07f;

        [Tooltip("Cuántos aros encadenados. Dos o tres se leen como una sacudida del agua; uno solo, como un círculo que crece.")]
        [Range(1, 4)]
        public int waveCount = 2;

        [Tooltip("Separación entre un aro y el siguiente.")]
        public float waveStagger = 0.08f;
    }

    // La luz que la bomba acumula antes de estallar. Se monta al lado de la burbuja y se apaga
    // sola al cumplirse el tiempo, así que quien la lanza solo tiene que esperar 'chargeTime' y
    // llamar a Play — si la bomba se destruyera antes, la luz se va con ella.
    public static void Charge(RectTransform bomb, Settings s)
    {
        if (bomb == null || bomb.parent == null || s == null || s.chargeTime <= 0f) return;

        var rt = NewPiece("BombCharge", (RectTransform)bomb.parent, SparkleTextures.Glow, s.chargeColor);

        rt.anchorMin = bomb.anchorMin;
        rt.anchorMax = bomb.anchorMax;
        rt.pivot     = bomb.pivot;
        rt.anchoredPosition = bomb.anchoredPosition;
        rt.sizeDelta        = Vector2.one * Mathf.Max(1f, s.chargeSize);

        // Detrás de la burbuja: es la bomba la que se enciende, y un halo por delante la taparía.
        rt.SetSiblingIndex(bomb.GetSiblingIndex());

        rt.GetComponent<BombBlast>().InitCharge(s.chargeTime, s.chargePulse);
    }

    // localPos: el punto del impacto en el espacio del tablero.
    public static void Play(RectTransform gridContainer, Vector2 localPos, Settings s)
    {
        if (gridContainer == null || s == null) return;

        Flash(gridContainer, localPos, s);

        for (int i = 0; i < s.waveCount; i++)
            Wave(gridContainer, localPos, s, i * s.waveStagger);
    }

    // El destello cuelga del CANVAS y no del tablero: tiene que cubrir la pantalla entera, y el
    // tablero se desplaza con el scroll y queda recortado por su propia área.
    //
    // Es un resplandor RADIAL centrado en la explosión y escalado hasta pasarse de la pantalla, no
    // un rectángulo de color uniforme. Un rectángulo plano se lee como una capa de pintura encima
    // —de hecho, sobre un fondo claro parece que oscurece— mientras que una luz que cae desde un
    // punto se lee como luz. Así ilumina la pantalla ENTERA y además se ve de dónde sale.
    static void Flash(RectTransform gridContainer, Vector2 localPos, Settings s)
    {
        if (s.flashColor.a <= 0f) return;

        var canvas = gridContainer.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var canvasRT = canvas.rootCanvas.transform as RectTransform;
        if (canvasRT == null) return;

        var rt = NewPiece("BombFlash", canvasRT, SparkleTextures.Glow, s.flashColor);

        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = ToCanvasLocal(canvas, canvasRT, gridContainer, localPos);

        // Sobre la DIAGONAL, no sobre el ancho: centrado en cualquier punto, la esquina más lejana
        // está a una diagonal de distancia, y midiendo por el ancho la luz no llegaría hasta ahí.
        float diagonal = new Vector2(canvasRT.rect.width, canvasRT.rect.height).magnitude;
        rt.sizeDelta   = Vector2.one * diagonal * 2f * Mathf.Max(0.1f, s.flashSpread);

        // Por encima de todo lo demás del canvas, incluidos los paneles: es un fogonazo, no una
        // capa del tablero.
        rt.SetAsLastSibling();

        rt.GetComponent<BombBlast>().InitFlash(s.flashRise, s.flashFall);
    }

    // Del espacio del tablero al del canvas. No se puede copiar la posición tal cual: el tablero
    // tiene su propia escala y su propio desplazamiento por el scroll.
    static Vector2 ToCanvasLocal(Canvas canvas, RectTransform canvasRT, RectTransform from, Vector2 localPos)
    {
        var     cam    = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, from.TransformPoint(localPos));

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screen, cam, out var local);
        return local;
    }

    static void Wave(RectTransform gridContainer, Vector2 localPos, Settings s, float delay)
    {
        var rt = NewPiece("BombWave", gridContainer, SparkleTextures.Ring(s.waveEdge), s.waveColor);

        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = localPos;
        rt.sizeDelta        = Vector2.one * s.waveFrom;

        // Delante de las burbujas: la onda pasa POR ENCIMA del tablero, como la sacudida del agua.
        rt.SetAsLastSibling();

        rt.GetComponent<BombBlast>().InitWave(s.waveFrom, s.waveTo, s.waveDuration, delay);
    }

    static RectTransform NewPiece(string label, RectTransform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
        {
            // Son temporales y se destruyen solas, pero si el editor guarda la escena mientras una
            // está viva, sin esto queda serializada dentro del .unity.
            hideFlags = HideFlags.DontSave,
        };

        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;

        var image = go.GetComponent<Image>();
        image.sprite        = sprite;
        image.raycastTarget = false;    // no puede comerse el toque del jugador a mitad de partida

        // El color va ENTERO, no apagado. Ponerlo en alfa 0 acá dejaba al componente guardando ese
        // cero como su alfa base, y como cada frame multiplica por él, la pieza no se encendía
        // nunca. Quien arranca en transparente es Awake, que primero guarda el color de verdad.
        image.color = color;

        go.AddComponent<BombBlast>();
        return rt;
    }

    RectTransform _rt;
    Image         _image;
    Color         _base;
    float         _t, _delay;

    // Qué clase de pieza es. Cada una se anima distinto y se apaga por su cuenta.
    enum Kind { Flash, Wave, Charge }
    Kind _kind;

    // Destello: solo alfa.
    float _rise, _fall;

    // Onda: tamaño y alfa.
    float _from, _to, _life;

    // Carga: cuánto dura y a qué ritmo late al final.
    float _pulse;

    void Awake()
    {
        _rt    = (RectTransform)transform;
        _image = GetComponent<Image>();
        _base  = _image.color;

        // Invisible hasta que empiece a animarse. Awake corre dentro del AddComponent, o sea antes
        // de que se dibuje un solo frame, así que no se alcanza a ver puesta.
        SetAlpha(0f);
    }

    void InitFlash(float rise, float fall)
    {
        _kind = Kind.Flash;
        _rise = Mathf.Max(0.01f, rise);
        _fall = Mathf.Max(0.01f, fall);
    }

    void InitWave(float from, float to, float life, float delay)
    {
        _kind  = Kind.Wave;
        _from  = from;
        _to    = to;
        _life  = Mathf.Max(0.01f, life);
        _delay = delay;
    }

    void InitCharge(float life, float pulse)
    {
        _kind  = Kind.Charge;
        _life  = Mathf.Max(0.01f, life);
        _pulse = pulse;
    }

    void Update()
    {
        if (_delay > 0f) { _delay -= Time.deltaTime; return; }

        _t += Time.deltaTime;

        switch (_kind)
        {
            case Kind.Flash:  UpdateFlash();  break;
            case Kind.Wave:   UpdateWave();   break;
            case Kind.Charge: UpdateCharge(); break;
        }
    }

    // La luz crece y late cada vez más rápido. El crecimiento al cuadrado hace que casi todo pase
    // sobre el final: así una carga larga no es un halo enorme esperando dos segundos, sino una
    // bomba que parece tranquila y se enciende de golpe justo antes de reventar.
    void UpdateCharge()
    {
        if (_t >= _life) { Destroy(gameObject); return; }

        float p = _t / _life;

        _rt.localScale = Vector3.one * (0.25f + 0.75f * p * p);

        // La frecuencia sube con p, y la fase se integra (p*p) para que el latido ACELERE en vez
        // de saltar de ritmo a cada frame.
        float beat = _pulse > 0f ? 0.72f + 0.28f * Mathf.Sin(p * p * _pulse * Mathf.PI * 2f) : 1f;

        SetAlpha(p * beat);
    }

    void UpdateFlash()
    {
        if (_t >= _rise + _fall) { Destroy(gameObject); return; }

        float alpha = _t < _rise ? _t / _rise : 1f - (_t - _rise) / _fall;

        SetAlpha(alpha);
    }

    void UpdateWave()
    {
        if (_t >= _life) { Destroy(gameObject); return; }

        float p = _t / _life;

        // Arranca rápido y frena, que es como se abre un frente de onda — a velocidad constante
        // parece un círculo dibujándose, no algo empujado por una explosión.
        float eased = 1f - (1f - p) * (1f - p);

        _rt.sizeDelta = Vector2.one * Mathf.Lerp(_from, _to, eased);

        // Se desvanece por el final y no linealmente: así el aro se ve nítido mientras crece y se
        // disuelve al perder fuerza, en vez de ir apagándose desde el primer frame.
        SetAlpha(1f - p * p);
    }

    void SetAlpha(float alpha) =>
        _image.color = new Color(_base.r, _base.g, _base.b, _base.a * Mathf.Clamp01(alpha));
}
