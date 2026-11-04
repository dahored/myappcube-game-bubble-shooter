using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Un hueco del hub de objetivos: un icono y cuánto falta.
//
// No sabe QUÉ está contando. Le dan un sprite y un número, y los dibuja — da igual que sean
// criaturas por rescatar, burbujas por reventar o burbujas verdes por cazar (GDD §2.2). Esa
// ignorancia es lo que permite que los cinco tipos de objetivo compartan el mismo hub sin que
// este archivo crezca con un caso por tipo.
public class ObjectiveItemView : MonoBehaviour
{
    [SerializeField] Image    icon;
    [SerializeField] TMP_Text total;

    [Tooltip("Lo que SUSTITUYE al número cuando el objetivo está cumplido — el tilde verde. Mientras se ve, el número se apaga: un cero y un tilde a la vez dicen lo mismo dos veces.\n\nOpcional: sin él solo queda el cero.")]
    [SerializeField] GameObject doneState;

    [Tooltip("Lo apagado que queda el icono una vez cumplido. En UNO se queda igual, que es lo que conviene si el tilde ya lo tapa a medias.\n\nEn CERO usa uno: un campo nuevo llega en cero a lo que ya estaba guardado, y eso dejaría el icono invisible justo al cumplirlo.")]
    [Range(0f, 1f)]
    [SerializeField] float doneTint = 1f;

    void Awake()
    {
        if (icon  == null) Debug.LogWarning($"[ObjectiveItemView] Falta asignar 'Icon' en {name}: el objetivo va a salir sin dibujo.", this);
        if (total == null) Debug.LogWarning($"[ObjectiveItemView] Falta asignar 'Total' en {name}: el objetivo va a salir sin número.", this);
    }

    // 'glow' es el brillo del cristal, que va DELANTE del icono. Solo lo lleva lo que se dibuja
    // como una burbuja: una criatura en el hub sale suelta, sin burbuja alrededor y sin brillo.
    public void Show(Sprite sprite, Sprite glow, int remaining)
    {
        if (icon != null && sprite != null) icon.sprite = sprite;

        Dress(glow);

        SetRemaining(remaining);
    }

    Image _glow;

    // El brillo se crea acá y no se pide en el prefab: es la misma decisión que toma la burbuja
    // con sus capas (BubbleView.BuildLayers). Pedirlo en el Inspector obligaría a montarlo en
    // cada hueco a mano, y el que se olvidara saldría plano sin que nada avise.
    void Dress(Sprite glow)
    {
        if (glow == null)
        {
            if (_glow != null) _glow.enabled = false;
            return;
        }

        if (_glow == null)
        {
            var go = new GameObject("Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            {
                hideFlags = HideFlags.DontSave,
            };

            var rt = (RectTransform)go.transform;

            // Cuelga del ICONO y no del hueco: así hereda su tamaño y su sitio, que es lo que el
            // Inspector ya ajustó. Colgado del hueco taparía también el número.
            rt.SetParent(icon != null ? icon.transform : transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            _glow = go.GetComponent<Image>();
            _glow.raycastTarget = false;
        }

        _glow.sprite  = glow;
        _glow.enabled = true;

        // Siempre el último, que es lo que lo deja DELANTE del icono.
        _glow.transform.SetAsLastSibling();
    }

    // El golpe de recibir algo. Va aparte del número: lo que se nota no es que ponga un dígito
    // distinto —eso de reojo no se ve— sino que el hueco acusa el golpe cuando le llega la pieza.
    public void Bump()
    {
        if (!isActiveAndEnabled) return;

        if (_bump != null) StopCoroutine(_bump);

        _bump = StartCoroutine(BumpRoutine());
    }

    Coroutine _bump;

    System.Collections.IEnumerator BumpRoutine()
    {
        const float TIME = 0.22f;
        const float PEAK = 1.3f;

        var rt = (RectTransform)transform;

        for (float t = 0f; t < TIME; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / TIME);

            rt.localScale = Vector3.one * (1f + (PEAK - 1f) * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }

        rt.localScale = Vector3.one;
        _bump = null;
    }

    public void SetRemaining(int remaining)
    {
        bool done = remaining <= 0;

        if (total != null)
        {
            total.text = Mathf.Max(0, remaining).ToString();

            // Con tilde, el número se va: los dos a la vez dicen lo mismo dos veces. Sin tilde se
            // queda el cero, porque un hueco vacío donde había una cuenta se lee como que algo
            // falló y el cero dice "ya está".
            bool showNumber = !done || doneState == null;

            if (total.gameObject.activeSelf != showNumber) total.gameObject.SetActive(showNumber);
        }

        if (icon != null)
        {
            var c     = icon.color;
            float lit = doneTint > 0f ? doneTint : 1f;

            icon.color = new Color(c.r, c.g, c.b, done ? lit : 1f);

            // El brillo se apaga CON el icono: quedándose entero sobre un icono a medias, lo
            // cumplido se leería como un reflejo flotando solo.
            if (_glow != null) _glow.color = icon.color;
        }

        if (doneState != null && doneState.activeSelf != done) doneState.SetActive(done);
    }
}
