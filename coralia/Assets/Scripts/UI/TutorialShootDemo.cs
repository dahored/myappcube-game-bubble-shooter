using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La mini-vista que enseña a disparar dentro del TutorialPanel: un tablero diminuto donde una
// mano apunta, suelta, la burbuja sube y tres del mismo color estallan. En bucle.
//
// Se dibuja por código y no se compone en la escena porque lo que hay que mantener en pie no es
// un dibujo sino una COREOGRAFÍA: la mano tiene que acabar donde acaba la línea, la línea donde
// aterriza la burbuja y la burbuja donde están las que estallan. Armado a mano son cinco objetos
// que hay que volver a alinear cada vez que se cambie el tamaño del panel; acá todo sale de una
// cuenta y encaja siempre.
//
// Lo único que se asigna son los sprites. El resto —posiciones, tamaños, tiempos— es relativo al
// hueco que le toque, así que la misma pieza sirve en un panel grande y en uno chico.
public class TutorialShootDemo : MonoBehaviour
{
    [Tooltip("Dos sprites de burbuja de colores distintos. El primero es el color que hace match; el segundo, el que acompaña para que se note la diferencia.")]
    [SerializeField] Sprite[] bubbleSprites;

    [Tooltip("La mano que apunta. La misma que usa ShootHintView dentro del gameplay: conviene que sea la misma para que el gesto se reconozca.")]
    [SerializeField] Sprite handSprite;

    [Tooltip("El punto de la línea de trayectoria. Vacío usa la primera burbuja en chiquito, que es lo que hace la línea del juego de verdad.")]
    [SerializeField] Sprite dotSprite;

    [Header("Proporciones")]
    [Tooltip("Cuánto mide una burbuja, en tanto por uno del alto del hueco.")]
    [Range(0.08f, 0.3f)]
    [SerializeField] float bubbleSize = 0.16f;

    [Tooltip("Cuántos puntos tiene la línea de trayectoria.")]
    [Range(4, 20)]
    [SerializeField] int dots = 10;

    [Header("Tiempos (segundos)")]
    [SerializeField] float holdTime    = 0.5f;   // el tablero quieto, antes de empezar
    [SerializeField] float dragTime    = 0.7f;   // la mano va del cañón al punto de mira
    [SerializeField] float releaseTime = 0.15f;  // el tirón al soltar
    [SerializeField] float flyTime     = 0.45f;  // la burbuja sube
    [SerializeField] float popTime     = 0.4f;   // las tres estallan
    [SerializeField] float restTime    = 0.7f;   // y se espera antes de repetir

    // El color que hace match va en los índices 1 y 2 de la fila: así el hueco de abajo toca a
    // los dos y el disparo completa el trío sin tener que explicar la geometría hexagonal.
    static readonly int[] ROW = { 1, 0, 0, 1 };

    readonly List<Image> _row  = new();
    readonly List<Image> _trail = new();

    RectTransform _area;
    Image         _shot;
    Image         _hand;
    Coroutine     _loop;
    float         _builtFor;   // el alto con el que se armó, para rehacerlo si el hueco cambia

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

        Debug.LogWarning($"[TutorialShootDemo] '{name}' necesita dos sprites de burbuja y uno de mano: sin eso no hay nada que mostrar.", this);
        return false;
    }

    // --- Armado ---

    // Esperar al layout antes de medir: si el hueco lo reparte un VerticalLayoutGroup —como en
    // TutorialPanel— su tamaño todavía no existe cuando el objeto se activa, y armar ahí deja
    // todo apilado en un punto con medidas de cero.
    IEnumerator Run()
    {
        _area = (RectTransform)transform;

        // Ocupar todo el hueco que le toque. El prefab trae el tamaño con el que se guardó, y
        // todas las medidas de la demostración salen del alto del área: sin esto, la coreografía
        // se dibujaría a la escala del prefab y no a la del panel que la está mostrando.
        _area.anchorMin = Vector2.zero;
        _area.anchorMax = Vector2.one;
        _area.offsetMin = Vector2.zero;
        _area.offsetMax = Vector2.zero;

        const float TIMEOUT = 1f;

        for (float t = 0f; _area.rect.height < 1f && t < TIMEOUT; t += Time.unscaledDeltaTime)
            yield return null;

        if (_area.rect.height < 1f)
        {
            Debug.LogWarning($"[TutorialShootDemo] '{name}' quedó sin alto: el layout del panel no le dio sitio y no hay dónde dibujar la demostración.", this);
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

        // Primero la línea, para que quede DETRÁS de todo lo demás: en UGUI el orden de dibujo es
        // el orden en la jerarquía, no una propiedad que haya que recordar poner.
        for (int i = 0; i < dots; i++)
        {
            var dot = Spawn("Dot", dotSprite != null ? dotSprite : bubbleSprites[0], size * 0.22f);
            dot.color = new Color(1f, 1f, 1f, 0.55f);
            _trail.Add(dot);
        }

        for (int i = 0; i < ROW.Length; i++)
        {
            var bubble = Spawn($"Bubble{i}", bubbleSprites[ROW[i]], size);
            Place(bubble, RowPos(i));
            _row.Add(bubble);
        }

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

        image.sprite               = sprite;
        image.raycastTarget        = false;   // es una demostración: nada acá se toca
        image.preserveAspect       = true;

        return image;
    }

    static void Place(Graphic graphic, Vector2 at) => ((RectTransform)graphic.transform).anchoredPosition = at;

    static void Discard(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else                       DestroyImmediate(go);
    }

    // --- Geometría, toda relativa al hueco que haya ---

    float Size()   => _area.rect.height * bubbleSize;
    float Width()  => _area.rect.width;
    float Height() => _area.rect.height;

    Vector2 RowPos(int i)
    {
        float size = Size();

        return new Vector2((i - (ROW.Length - 1) * 0.5f) * size, Height() * 0.26f);
    }

    // El hueco donde cae el disparo: justo debajo y en medio de las dos del color que matchea,
    // que es donde toca a las dos a la vez. 0,87 es el alto de una fila hexagonal.
    Vector2 TargetPos() => (RowPos(1) + RowPos(2)) * 0.5f - Vector2.up * (Size() * 0.87f);

    Vector2 CannonPos() => new(0f, -Height() * 0.34f);

    // --- La coreografía ---

    IEnumerator Loop()
    {
        while (true)
        {
            // El panel puede cambiar de alto (otro idioma con un texto más largo lo estira), y
            // todas las medidas salen de ahí. Se rehace entre repeticiones, nunca a mitad de una.
            if (!Mathf.Approximately(_builtFor, _area.rect.height) && _area.rect.height > 1f) Build();

            Reset();
            yield return new WaitForSecondsRealtime(holdTime);

            yield return Aim();
            yield return Release();
            yield return Fly();
            yield return Pop();

            yield return new WaitForSecondsRealtime(restTime);
        }
    }

    void Reset()
    {
        float size = Size();

        foreach (var bubble in _row)
        {
            bubble.color = Color.white;
            bubble.transform.localScale = Vector3.one;
        }

        // El color también, no solo el enabled: Pop() la deja en alfa 0, así que sin esto la
        // burbuja vuelve al cañón de la segunda vuelta en adelante pero transparente.
        _shot.enabled = true;
        _shot.color   = Color.white;
        _shot.transform.localScale = Vector3.one;
        Place(_shot, CannonPos());

        // La mano arranca SOBRE la burbuja del cañón, no en un sitio cualquiera: el gesto que se
        // enseña empieza tocando la burbuja, y si la mano apareciera ya a medio camino el
        // jugador no vería de dónde sale.
        _hand.enabled = true;
        Place(_hand, CannonPos() + new Vector2(size * 0.35f, -size * 0.55f));
        _hand.transform.localScale = Vector3.one;

        ShowTrail(0f);
    }

    IEnumerator Aim()
    {
        Vector2 from = CannonPos() + new Vector2(Size() * 0.35f, -Size() * 0.55f);
        Vector2 to   = TargetPos() + new Vector2(Size() * 0.35f, -Size() * 0.55f);

        for (float t = 0f; t < dragTime; t += Time.unscaledDeltaTime)
        {
            float p = Smooth(t / dragTime);

            Place(_hand, Vector2.Lerp(from, to, p));
            ShowTrail(p);   // la línea crece con el dedo: es la relación que hay que enseñar
            yield return null;
        }

        ShowTrail(1f);
    }

    IEnumerator Release()
    {
        for (float t = 0f; t < releaseTime; t += Time.unscaledDeltaTime)
        {
            float p = t / releaseTime;

            _hand.transform.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }

        _hand.enabled = false;
    }

    IEnumerator Fly()
    {
        Vector2 from = CannonPos();
        Vector2 to   = TargetPos();

        for (float t = 0f; t < flyTime; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / flyTime);

            Place(_shot, Vector2.Lerp(from, to, p));
            ShowTrail(1f - p);   // la línea se consume por detrás de la burbuja
            yield return null;
        }

        Place(_shot, to);
        ShowTrail(0f);
    }

    IEnumerator Pop()
    {
        // Las dos de la fila MÁS la que acaba de llegar: el trío que estalla es lo que hay que
        // ver, así que las tres se van juntas y las otras dos se quedan.
        var popping = new List<Graphic> { _row[1], _row[2], _shot };

        for (float t = 0f; t < popTime; t += Time.unscaledDeltaTime)
        {
            float p = t / popTime;

            foreach (var g in popping)
            {
                g.transform.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(p * Mathf.PI) - p * 0.5f);
                g.color = new Color(1f, 1f, 1f, 1f - p);
            }

            yield return null;
        }

        foreach (var g in popping) g.color = new Color(1f, 1f, 1f, 0f);
        _shot.enabled = false;
    }

    // Cuántos puntos de la línea se ven, de 0 a 1.
    void ShowTrail(float progress)
    {
        Vector2 from = CannonPos();
        Vector2 to   = TargetPos();
        int     show = Mathf.RoundToInt(Mathf.Clamp01(progress) * _trail.Count);

        for (int i = 0; i < _trail.Count; i++)
        {
            _trail[i].enabled = i < show;

            if (i < show) Place(_trail[i], Vector2.Lerp(from, to, (i + 1f) / _trail.Count));
        }
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
