using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La mini-vista que enseña a cancelar un disparo dentro del TutorialPanel.
//
// Lo que hay que contar es el ARREPENTIMIENTO: la mano apunta, duda, vuelve sobre la burbuja y
// suelta sin disparar. Empezar ya con el dedo encima solo enseñaría a tocar la burbuja, que es
// el gesto del swap y no este.
//
// El aro es el protagonista, no un adorno: mientras se apunta va pegado a la burbuja y en blanco;
// al entrar en la zona de cancelación se ABRE hasta el radio real y se pone rojo. Esa es la señal
// que el jugador tiene que aprender a leer, así que acá se reproduce igual que en el cañón.
public class TutorialCancelDemo : MonoBehaviour
{
    [Tooltip("Sprites de burbuja de colores distintos, dos o más. Solo decoran el tablero: acá no estalla nada.")]
    [SerializeField] Sprite[] bubbleSprites;

    [Tooltip("La mano que apunta. La misma que usan los otros tutoriales, para que el gesto se reconozca.")]
    [SerializeField] Sprite handSprite;

    [Tooltip("El punto de la línea de trayectoria. Vacío usa la primera burbuja en chiquito.")]
    [SerializeField] Sprite dotSprite;

    [SerializeField] Color dotColor = new(1f, 1f, 1f, 0.85f);

    [Header("El aro")]
    [Tooltip("De qué color va mientras se apunta. El mismo que usa el cañón.")]
    [SerializeField] Color aimColor = new(1f, 1f, 1f, 0.85f);

    [Tooltip("Y de cuál al entrar en la zona de cancelación. Rojo en el juego: es un aviso, no un adorno.")]
    [SerializeField] Color cancelColor = new(1f, 0.45f, 0.4f, 1f);

    [Tooltip("Cuánto mide el aro mientras se apunta, en veces la burbuja. En 1 queda justo sobre su borde.")]
    [Range(0.8f, 2f)]
    [SerializeField] float aimRingSize = 1.15f;

    [Tooltip("Hasta dónde se abre al entrar en la zona de cancelación, en veces la burbuja. En el juego son 130 sobre un diámetro de 92, o sea algo más de un dedo alrededor.")]
    [Range(1f, 4f)]
    [SerializeField] float cancelRingSize = 2.8f;

    [Range(0.02f, 0.4f)]
    [SerializeField] float ringEdge = 0.07f;

    [Header("Proporciones")]
    [Range(0.05f, 0.3f)] [SerializeField] float bubbleSize = 0.1f;

    [Tooltip("Proporción ancho/alto de la composición. Acota el área usada dentro del hueco para que la demostración no se estire en pantallas altas.")]
    [Range(0.4f, 1.6f)] [SerializeField] float aspect = 0.72f;

    [Range(4, 24)]      [SerializeField] int   dots    = 10;
    [Range(0.1f, 0.6f)] [SerializeField] float dotSize = 0.2f;

    [Tooltip("Cuánto se desvía la mira mientras apunta, en grados.")]
    [Range(0f, 60f)]    [SerializeField] float aimSweep = 26f;

    [Range(0.1f, 0.9f)] [SerializeField] float handDistance = 0.42f;

    [Header("Tiempos (segundos)")]
    [SerializeField] float holdTime   = 0.5f;   // el tablero quieto, antes de empezar
    [SerializeField] float aimTime    = 0.7f;   // la mano apunta
    [SerializeField] float doubtTime  = 0.25f;  // se queda quieta un momento: es la duda
    [SerializeField] float returnTime = 0.5f;   // y vuelve sobre la burbuja
    [SerializeField] float armedTime  = 0.6f;   // el aro abierto y rojo, antes de soltar
    [SerializeField] float releaseTime = 0.2f;  // el tirón al soltar
    [SerializeField] float restTime   = 0.8f;   // y se espera antes de repetir

    // Celdas de verdad del tablero: la fila queda donde quedaría jugando. Acá no se dispara, así
    // que no hace falta un hueco de destino — solo algo contra lo que apuntar.
    static readonly Vector2Int[] ROW_CELLS = { new(1, 0), new(2, 0), new(3, 0), new(4, 0) };
    static readonly Vector2Int   ANCHOR    = new(2, 1);

    readonly List<Image> _row   = new();
    readonly List<Image> _trail = new();

    RectTransform _area;
    Vector2       _aimTo;
    Image         _shot;
    Image         _ring;
    Image         _hand;
    Coroutine     _loop;
    float         _builtFor;

    void OnEnable()
    {
        if (!Ready()) return;

        _loop = StartCoroutine(Run());
    }

    void OnDisable()
    {
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
    }

    bool Ready()
    {
        if (bubbleSprites != null && bubbleSprites.Length >= 2 && handSprite != null) return true;

        Debug.LogWarning($"[TutorialCancelDemo] '{name}' necesita dos sprites de burbuja y uno de mano: sin eso no hay nada que mostrar.", this);
        return false;
    }

    // --- Armado ---

    IEnumerator Run()
    {
        _area = (RectTransform)transform;

        _area.anchorMin = Vector2.zero;
        _area.anchorMax = Vector2.one;
        _area.offsetMin = Vector2.zero;
        _area.offsetMax = Vector2.zero;

        const float TIMEOUT = 1f;

        for (float t = 0f; _area.rect.height < 1f && t < TIMEOUT; t += Time.unscaledDeltaTime)
            yield return null;

        if (_area.rect.height < 1f)
        {
            Debug.LogWarning($"[TutorialCancelDemo] '{name}' quedó sin alto: el layout del panel no le dio sitio.", this);
            yield break;
        }

        Build();

        yield return Loop();
    }

    void Build()
    {
        _builtFor = _area.rect.height;

        for (int i = _area.childCount - 1; i >= 0; i--) Discard(_area.GetChild(i).gameObject);
        _row.Clear();
        _trail.Clear();

        float size = Size();

        for (int i = 0; i < dots; i++)
        {
            var dot = Spawn("Dot", dotSprite != null ? dotSprite : bubbleSprites[0], size * dotSize);
            dot.color = dotColor;
            _trail.Add(dot);
        }

        for (int i = 0; i < ROW_CELLS.Length; i++)
        {
            var bubble = Spawn($"Bubble{i}", bubbleSprites[i % bubbleSprites.Length], size);
            Place(bubble, CellPos(ROW_CELLS[i]));
            _row.Add(bubble);
        }

        // El aro va DEBAJO de la burbuja: es el halo que la rodea, no una capa encima. El mismo
        // anillo generado por código que usa el cañón, para que la señal sea idéntica.
        _ring = Spawn("Ring", SparkleTextures.Ring(ringEdge), size * aimRingSize);
        _shot = Spawn("Shot", bubbleSprites[0], size);
        _hand = Spawn("Hand", handSprite, size * 1.5f);
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

    static void Place(Graphic graphic, Vector2 at) => ((RectTransform)graphic.transform).anchoredPosition = at;

    static void SetSize(Graphic graphic, float size) => ((RectTransform)graphic.transform).sizeDelta = Vector2.one * size;

    static void Discard(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else                       DestroyImmediate(go);
    }

    // --- Geometría, toda relativa al hueco que haya ---

    // Todas las medidas salen de un área de PROPORCIÓN FIJA centrada en el hueco, no del hueco
    // entero: en una pantalla alta la composición se repartía por todo el alto y la acción quedaba
    // perdida en el medio con los bordes vacíos.
    float Height() => Mathf.Min(_area.rect.height, _area.rect.width / Mathf.Max(0.1f, aspect));
    float Width()  => Mathf.Min(_area.rect.width, Height() * aspect);
    float Size()   => Height() * bubbleSize;

    float Scale => Size() / HexGridMath.BubbleDiameter;

    Vector2 CellPos(Vector2Int cell) =>
        (HexGridMath.CellToLocalPos(cell) - HexGridMath.CellToLocalPos(ANCHOR)) * Scale + AnchorPos();

    Vector2 AnchorPos() => new(0f, Height() * 0.14f);

    Vector2 CannonPos() => new(0f, -Height() * 0.32f);

    // --- La coreografía ---

    IEnumerator Loop()
    {
        while (true)
        {
            if (!Mathf.Approximately(_builtFor, _area.rect.height) && _area.rect.height > 1f) Build();

            Reset();
            yield return new WaitForSecondsRealtime(holdTime);

            yield return Aim();
            yield return new WaitForSecondsRealtime(doubtTime);
            yield return ComeBack();
            yield return new WaitForSecondsRealtime(armedTime);
            yield return Release();

            yield return new WaitForSecondsRealtime(restTime);
        }
    }

    void Reset()
    {
        float size = Size();

        _shot.enabled = true;
        _shot.color   = Color.white;
        _shot.transform.localScale = Vector3.one;
        Place(_shot, CannonPos());

        // El aro arranca apagado y pegado a la burbuja: aparece al empezar a apuntar, igual que
        // en el juego.
        _ring.enabled = true;
        _ring.color   = Transparent(aimColor);
        SetSize(_ring, size * aimRingSize);
        Place(_ring, CannonPos());

        _hand.enabled = true;
        _hand.transform.localScale = Vector3.one;
        Place(_hand, HandAtCannon());

        _aimTo = AimPointAt(StartAngle());
        ShowTrail(0f);
    }

    static Color Transparent(Color c) => new(c.r, c.g, c.b, 0f);

    // --- La mira ---

    float TargetAngle()
    {
        Vector2 d = AnchorPos() - CannonPos();
        return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
    }

    float StartAngle() => TargetAngle() + aimSweep;

    Vector2 AimPointAt(float degrees)
    {
        float   rad = degrees * Mathf.Deg2Rad;
        Vector2 dir = new(Mathf.Sin(rad), Mathf.Cos(rad));

        return CannonPos() + dir * Vector2.Distance(CannonPos(), AnchorPos());
    }

    Vector2 HandAtCannon() => CannonPos() + new Vector2(Size() * 0.45f, -Size() * 0.65f);

    Vector2 HandOnAim() =>
        Vector2.Lerp(CannonPos(), _aimTo, handDistance) + new Vector2(Size() * 0.6f, -Size() * 0.35f);

    IEnumerator Aim()
    {
        Vector2 handFrom = HandAtCannon();
        float   from     = StartAngle();
        float   to       = TargetAngle();

        for (float t = 0f; t < aimTime; t += Time.unscaledDeltaTime)
        {
            float p = Smooth(t / aimTime);

            _aimTo = AimPointAt(Mathf.Lerp(from, to, p));
            Place(_hand, Vector2.Lerp(handFrom, HandOnAim(), p));

            // El aro se enciende al empezar a apuntar y se queda pegado a la burbuja.
            _ring.color = new Color(aimColor.r, aimColor.g, aimColor.b, aimColor.a * Mathf.Clamp01(p * 3f));

            ShowTrail(1f);
            yield return null;
        }

        _aimTo = AnchorPos();
        Place(_hand, HandOnAim());
        _ring.color = aimColor;
        ShowTrail(1f);
    }

    // El arrepentimiento: la mano vuelve sobre la burbuja y el aro se abre y se vuelve rojo. La
    // línea se apaga a la vez — es lo que dice que ya no va a salir ningún disparo.
    IEnumerator ComeBack()
    {
        Vector2 handFrom = HandOnAim();
        Vector2 handTo   = HandAtCannon();
        float   small    = Size() * aimRingSize;
        float   big      = Size() * cancelRingSize;

        for (float t = 0f; t < returnTime; t += Time.unscaledDeltaTime)
        {
            float p = Smooth(t / returnTime);

            Place(_hand, Vector2.Lerp(handFrom, handTo, p));
            SetSize(_ring, Mathf.Lerp(small, big, p));
            _ring.color = Color.Lerp(aimColor, cancelColor, p);
            ShowTrail(1f - p);
            yield return null;
        }

        Place(_hand, handTo);
        SetSize(_ring, big);
        _ring.color = cancelColor;
        ShowTrail(0f);
    }

    // Suelta: la mano se va y el aro se apaga. La burbuja se queda donde estaba, que es el punto.
    IEnumerator Release()
    {
        Color from = _ring.color;

        for (float t = 0f; t < releaseTime; t += Time.unscaledDeltaTime)
        {
            float p = t / releaseTime;

            _hand.transform.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(p * Mathf.PI));
            _ring.color = Color.Lerp(from, Transparent(from), p);
            yield return null;
        }

        _hand.enabled = false;
        _ring.color   = Transparent(from);
    }

    void ShowTrail(float progress)
    {
        Vector2 from = CannonPos();
        int     show = Mathf.RoundToInt(Mathf.Clamp01(progress) * _trail.Count);

        for (int i = 0; i < _trail.Count; i++)
        {
            _trail[i].enabled = i < show;

            if (i < show) Place(_trail[i], Vector2.Lerp(from, _aimTo, (i + 1f) / _trail.Count));
        }
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
