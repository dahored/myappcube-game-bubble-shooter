using UnityEngine;
using UnityEngine.UI;

// El brillo que acompaña la aparición de una burbuja: un resplandor suave que florece detrás y un
// puñado de chispas que salen girando y titilan.
//
// Las texturas se GENERAN por código, no son arte importado. La razón es que lo que hace que algo
// se vea mágico no es la forma sino el degradado: un sprite vectorial de rayitas queda plano por
// más que se anime. Acá el resplandor cae suave desde el centro y cada chispa tiene sus cuatro
// brazos con caída gaussiana, que es de donde sale el brillo.
//
// Son Images de UI y no un ParticleSystem porque el canvas de Gameplay es Screen Space - Overlay:
// ahí las partículas se dibujarían DETRÁS del fondo, o sea invisibles. Pasar el canvas a Screen
// Space - Camera resolvería eso pero mueve el sorting de toda la escena.
//
// Cada pieza tiene vida propia, igual que PopParticle: si quien la lanzó se destruye o corta su
// corrutina, la chispa se apagaría congelada en pantalla en vez de terminar.
public class Sparkle : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Header("Resplandor")]
        public Color glowColor    = new(1f, 0.93f, 0.72f, 0.85f);
        public float glowSize     = 300f;
        public float glowFrom     = 0.35f;
        public float glowTo       = 1.6f;
        public float glowDuration = 0.5f;

        [Header("Chispas")]
        public int   moteCount    = 10;
        public Color moteColor    = new(1f, 1f, 1f, 0.95f);
        public float moteSize     = 64f;

        [Tooltip("A qué distancia del centro nacen las chispas.")]
        public float spawnRadius  = 34f;

        [Tooltip("Cuánto se alejan mientras titilan.")]
        public float travel       = 95f;

        public float moteDuration = 0.55f;

        [Tooltip("Cuánto se reparte la salida de las chispas. En 0 salen todas juntas, que se lee como un golpe en vez de un chisporroteo.")]
        public float stagger      = 0.22f;
    }

    RectTransform _rt;
    Image         _image;
    Color         _baseColor;
    Vector2       _from, _to;
    float         _peakScale, _lifetime, _delay, _spin, _t;
    bool          _twinkle;

    // Lanza el brillo entero al lado de `beside`, con su mismo anclaje y justo DETRÁS en la
    // jerarquía: enmarca a la burbuja en vez de taparla.
    public static void Burst(RectTransform beside, Settings s)
    {
        if (beside == null || beside.parent == null || s == null) return;

        // El resplandor no titila ni se mueve: se abre en el sitio. Su escala la maneja SetGlow
        // aparte, porque va de un tamaño a otro en vez de subir y bajar como las chispas.
        var glow = Piece(beside, SparkleTextures.Glow, s.glowSize, s.glowColor,
                         Vector2.zero, Vector2.zero, 1f, s.glowDuration, 0f, 0f, twinkle: false);
        if (glow != null) glow.SetGlow(s.glowFrom, s.glowTo);

        for (int i = 0; i < s.moteCount; i++)
        {
            // Ángulos repartidos con una pizca de azar: parejitos se ven como un reloj, y del todo
            // al azar se amontonan de un lado.
            float angle = 360f / Mathf.Max(1, s.moteCount) * i + Random.Range(-16f, 16f);
            Vector2 dir = new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            Piece(beside, SparkleTextures.Mote, s.moteSize * Random.Range(0.65f, 1.2f), s.moteColor,
                  dir * s.spawnRadius,
                  dir * (s.spawnRadius + s.travel * Random.Range(0.7f, 1.3f)),
                  1f,
                  s.moteDuration * Random.Range(0.8f, 1.25f),
                  Random.Range(0f, s.stagger),
                  Random.Range(-90f, 90f),
                  twinkle: true);
        }
    }

    static Sparkle Piece(RectTransform beside, Sprite sprite, float size, Color color,
                         Vector2 from, Vector2 to, float peakScale,
                         float lifetime, float delay, float spin, bool twinkle)
    {
        if (sprite == null) return null;

        var go = new GameObject("Sparkle", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;

        rt.SetParent(beside.parent, false);
        rt.SetSiblingIndex(beside.GetSiblingIndex());

        rt.anchorMin = beside.anchorMin;
        rt.anchorMax = beside.anchorMax;
        rt.pivot     = beside.pivot;
        rt.sizeDelta = Vector2.one * size;

        var image = go.GetComponent<Image>();
        image.sprite        = sprite;
        image.color         = color;
        image.raycastTarget = false;

        var piece = go.AddComponent<Sparkle>();
        piece.Init(beside.anchoredPosition + from, beside.anchoredPosition + to,
                   peakScale, lifetime, delay, spin, twinkle);
        return piece;
    }

    void Init(Vector2 from, Vector2 to, float peakScale, float lifetime, float delay, float spin, bool twinkle)
    {
        _rt        = (RectTransform)transform;
        _image     = GetComponent<Image>();
        _baseColor = _image.color;
        _from      = from;
        _to        = to;
        _peakScale = peakScale;
        _lifetime  = Mathf.Max(0.01f, lifetime);
        _delay     = delay;
        _spin      = spin;
        _twinkle   = twinkle;

        // Invisible mientras espera su turno: aparecer ya puesto y quedarse quieto delata el truco.
        _rt.anchoredPosition = from;
        _rt.localScale       = Vector3.zero;
        _image.color         = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0f);
    }

    float _glowFrom = -1f, _glowTo;
    void SetGlow(float from, float to) { _glowFrom = from; _glowTo = to; }

    void Update()
    {
        if (_delay > 0f) { _delay -= Time.deltaTime; return; }

        _t += Time.deltaTime;
        if (_t >= _lifetime) { Destroy(gameObject); return; }

        float p     = _t / _lifetime;
        float eased = 1f - (1f - p) * (1f - p); // rápido al principio y frena: así se lee un estallido

        _rt.anchoredPosition = Vector2.LerpUnclamped(_from, _to, eased);
        _rt.localRotation    = Quaternion.Euler(0f, 0f, _spin * eased);

        // La chispa titila —crece y se apaga en el mismo gesto— mientras el resplandor solo se
        // abre y se desvanece.
        float scale = _glowFrom >= 0f
            ? Mathf.Lerp(_glowFrom, _glowTo, eased)
            : _peakScale * Mathf.Sin(p * Mathf.PI);

        float alpha = _twinkle
            ? Mathf.Sin(p * Mathf.PI)
            : (p < 0.12f ? p / 0.12f : 1f - (p - 0.12f) / 0.88f);

        _rt.localScale = Vector3.one * scale;
        _image.color   = new Color(_baseColor.r, _baseColor.g, _baseColor.b, _baseColor.a * alpha);
    }
}
