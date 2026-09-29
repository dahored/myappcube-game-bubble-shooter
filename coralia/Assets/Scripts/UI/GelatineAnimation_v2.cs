using UnityEngine;

// Variante de GelatineAnimation pensada para elementos interactivos (botones) además de
// decorativos — mismo wobble de squash & stretch en loop continuo (con balanceo ondulatorio
// opcional), pero se pausa sola mientras ButtonPop (pop de click) o PopInView (pop de
// aparición) están animando este mismo transform, así ninguno pelea por escribir
// transform.localScale el mismo frame. Asignable a cualquier elemento con un RectTransform —
// Image, Button, Text, lo que sea.
public class GelatineAnimation_v2 : MonoBehaviour
{
    [Header("Squash & stretch")]
    [SerializeField] float duration    = 3f; // segundos por ciclo completo
    [SerializeField] float startDelay;       // offset inicial — variar entre varios elementos para que no se muevan todos igual
    [Tooltip("Cuánto se expande/achica hacia los lados (eje X). Independiente de Squash Amount Y.")]
    [SerializeField, Range(0f, 0.5f)] float squashAmountX = 0.03f;
    [Tooltip("Cuánto se expande/achica hacia arriba/abajo (eje Y). Independiente de Squash Amount X.")]
    [SerializeField, Range(0f, 0.5f)] float squashAmountY = 0.03f;

    [Header("Balanceo ondulatorio (opcional)")]
    [SerializeField] bool  enableSway   = false;
    [SerializeField] float swayAngle    = 6f;
    [SerializeField] float swayDuration = 4f;

    // Opcionales — si este mismo objeto tiene ButtonPop y/o PopInView, se detectan solos en
    // Awake. No hace falta asignarlos a mano ni que el elemento sea necesariamente un botón.
    ButtonPop  _buttonPop;
    PopInView  _popInView;

    (float t, float x, float y)[] _keyframes;
    Vector3 _baseScale;
    float   _t;
    float   _swayT;

    void Awake() => Initialize();

    // Aparte de Awake porque este componente viaja en prefabs que instancian herramientas de
    // editor: con 'Reload Scene' desactivado, un objeto creado en modo edición entra a Play sin
    // que su Awake haya corrido, y Update se encontraba los keyframes en null.
    void Initialize()
    {
        _buttonPop = GetComponent<ButtonPop>();
        _popInView = GetComponent<PopInView>();
        _baseScale = transform.localScale;
        _t         = startDelay / duration;
        _swayT     = startDelay / swayDuration;

        // X e Y ya no comparten un solo valor con signo invertido — cada eje tiene su propia
        // amplitud, así se puede pedir ej. "casi nada de lado, bastante hacia arriba" sin que
        // el otro eje quede atado a lo mismo (eso era lo que se sentía "raro" antes).
        _keyframes = new (float, float, float)[]
        {
            (0f,    1f,                        1f),
            (0.25f, 1f - squashAmountX,         1f + squashAmountY),
            (0.5f,  1f + squashAmountX,         1f - squashAmountY),
            (0.75f, 1f - squashAmountX * 0.5f,  1f + squashAmountY * 0.5f),
            (1f,    1f,                        1f),
        };
    }

    void Update()
    {
        if (_keyframes == null) Initialize();

        // ButtonPop (click) o PopInView (aparición) ya están animando este mismo transform —
        // nos hacemos a un lado sin avanzar el reloj, así al retomar el wobble sigue justo
        // donde se había quedado, sin salto ni desincronización.
        if (_buttonPop != null && _buttonPop.IsPlaying) return;
        if (_popInView != null && _popInView.IsPlaying) return;

        _t = (_t + Time.deltaTime / duration) % 1f;
        var (x, y) = Evaluate(_t);
        transform.localScale = new Vector3(_baseScale.x * x, _baseScale.y * y, _baseScale.z);

        if (enableSway)
        {
            _swayT += Time.deltaTime / swayDuration;
            float angle = Mathf.Sin(_swayT * Mathf.PI * 2f) * swayAngle;
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    (float x, float y) Evaluate(float t)
    {
        for (int i = 0; i < _keyframes.Length - 1; i++)
        {
            var (t0, x0, y0) = _keyframes[i];
            var (t1, x1, y1) = _keyframes[i + 1];
            if (t < t0 || t > t1) continue;
            float p = (t - t0) / (t1 - t0);
            return (Mathf.SmoothStep(x0, x1, p), Mathf.SmoothStep(y0, y1, p));
        }
        return (1f, 1f);
    }
}
