using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Mueve el parámetro _Scroll del shader CurvedMapPreview con el drag del dedo, con inercia (al
// soltar sigue avanzando un poco y frena solo) — mismo concepto que "Deceleration Rate" del
// ScrollRect normal. Va sobre el mismo RawImage (ya tiene Raycast Target activo).
//
// Reglas fijas (pedidas explícitamente, no tocar sin confirmar):
// - Scroll NUNCA negativo. Arranca en 0, que es nivel 1 pegado al pie — no hay nada "antes".
// - Arrastrar hacia ABAJO avanza el Scroll (revela nivel 2, 3, 4...).
// - El límite máximo NO se configura a mano acá — se lee de CurvedMapNodePositioner.ScrollMax,
//   que lo calcula solo según cuántos nodos haya.
[RequireComponent(typeof(RawImage))]
public class CurvedMapScrollDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("Cuánto avanza 'Scroll' (en alturas de pantalla) por cada pixel de drag.")]
    [SerializeField] float dragSpeed = 0.003f;
    [Tooltip("De dónde saca el límite máximo de Scroll (se calcula solo, según sus nodos).")]
    [SerializeField] CurvedMapNodePositioner nodePositioner;
    [Tooltip("Igual que 'Deceleration Rate' de un ScrollRect normal — más bajo frena antes.")]
    [SerializeField] float decelerationRate = 0.5f;

    const float SCROLL_MIN = 0f; // nunca negativo — nivel 1 es el principio, no hay nada antes

    RawImage _rawImage;
    Material _material; // instancia propia (.material, no .sharedMaterial)
    float _velocity;    // unidades de Scroll por segundo, suavizada

    void Awake()
    {
        _rawImage = GetComponent<RawImage>();
        _material = _rawImage.material;
        _material.SetFloat("_Scroll", SCROLL_MIN); // nivel 1 al pie, arranque natural
    }

    public void OnBeginDrag(PointerEventData eventData) => _velocity = 0f;

    public void OnDrag(PointerEventData eventData)
    {
        if (!_material) return;
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        // Arrastrar hacia ABAJO (delta.y negativo, convención de pantalla de Unity) avanza el
        // Scroll (positivo) — revela nivel 2, 3, 4...
        float delta = -eventData.delta.y * dragSpeed;
        ApplyScrollDelta(delta);

        // Promedio suavizado en vez de la lectura instantánea (delta/dt salta mucho de frame a
        // frame, sobre todo con mouse) — así el impulso al soltar sale parejo, no disparado.
        float instantVelocity = delta / dt;
        _velocity = Mathf.Lerp(_velocity, instantVelocity, 0.5f);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // El impulso queda guardado en _velocity — Update() lo sigue aplicando con frenado.
    }

    void Update()
    {
        if (Mathf.Abs(_velocity) < 0.01f) { _velocity = 0f; return; }
        ApplyScrollDelta(_velocity * Time.unscaledDeltaTime);
        _velocity *= Mathf.Pow(decelerationRate, Time.unscaledDeltaTime);
    }

    void ApplyScrollDelta(float delta)
    {
        float scrollMax = nodePositioner ? nodePositioner.ScrollMax : 0f;
        float scroll = _material.GetFloat("_Scroll");
        scroll = Mathf.Clamp(scroll + delta, SCROLL_MIN, scrollMax);
        _material.SetFloat("_Scroll", scroll);
    }
}
