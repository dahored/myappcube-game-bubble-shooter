using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Gira la esfera del mundo (World3D/Sphere) sobre su eje horizontal (X) al arrastrar el dedo,
// con inercia (al soltar sigue girando un poco y frena solo). Va sobre un Image invisible
// full-screen (DragArea) que recibe el drag y lo reenvía acá.
//
// Reglas fijas (mismo criterio que CurvedMapScrollDrag del sistema del shader):
// - El ángulo nunca baja de 0 — eso es el nodo 1, no hay nada "antes".
// - El límite máximo lo calcula solo WorldSphereNodePositioner, según cuántos nodos haya.
[RequireComponent(typeof(Transform))]
public class WorldSphereDragRotate : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Tooltip("La esfera que gira (World3D/Sphere).")]
    [SerializeField] Transform sphere;
    [Tooltip("Grados que gira la esfera por cada pixel de drag.")]
    [SerializeField] float dragSpeed = 0.1f;
    [Tooltip("De dónde saca el límite máximo del ángulo (se calcula solo, según sus nodos).")]
    [SerializeField] WorldSphereNodePositioner nodePositioner;
    [Tooltip("Igual que 'Deceleration Rate' de un ScrollRect normal — más bajo frena antes.")]
    [SerializeField] float decelerationRate = 0.5f;

    const float ANGLE_MIN = 0f; // nunca negativo — nodo 1 es el principio, no hay nada antes

    float _currentAngle;
    float _velocity; // grados por segundo, suavizada

    // Se reusa entre toques para no generar basura en cada tap.
    static readonly List<RaycastResult> _raycastHits = new List<RaycastResult>();

    // Ángulo de giro actual, para que otros (los pines) puedan saber a qué altura del mapa estamos.
    public float CurrentAngle => _currentAngle;

    Coroutine _autoScroll;

    // Salta de una al ángulo pedido, sin animación — para posicionar el mapa al abrirlo.
    public void JumpTo(float angle)
    {
        StopAutoScroll();
        _velocity = 0f;
        ApplyAngleDelta(angle - _currentAngle);
    }

    // Se desplaza suave hasta el ángulo pedido — para ir al nodo siguiente después de ganar.
    public void AnimateTo(float angle, float duration = 0.7f)
    {
        StopAutoScroll();
        _autoScroll = StartCoroutine(AutoScroll(angle, duration));
    }

    IEnumerator AutoScroll(float target, float duration)
    {
        _velocity = 0f;
        float from = _currentAngle;

        for (float t = 0f; t < 1f;)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(duration, 0.0001f);
            // SmoothStep: arranca y frena despacio, sin el tirón de un movimiento lineal.
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            ApplyAngleDelta(Mathf.Lerp(from, target, p) - _currentAngle);
            yield return null;
        }
        _autoScroll = null;
    }

    void StopAutoScroll()
    {
        if (_autoScroll == null) return;
        StopCoroutine(_autoScroll);
        _autoScroll = null;
    }

    // Si el usuario toca el mapa mientras se está desplazando solo, manda él.
    public void OnBeginDrag(PointerEventData eventData)
    {
        StopAutoScroll();
        _velocity = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

        // Arrastrar hacia ABAJO (delta.y negativo) avanza el scroll: el dedo tira del mundo hacia
        // la cámara y va bajando los nodos siguientes desde el horizonte, igual que arrastrar
        // una lista hacia abajo trae lo que estaba más arriba.
        float deltaAngle = -eventData.delta.y * dragSpeed;
        ApplyAngleDelta(deltaAngle);

        // Promedio suavizado en vez de la lectura instantánea (delta/dt salta mucho de frame a
        // frame, sobre todo con mouse) — así el impulso al soltar sale parejo, no disparado.
        float instantVelocity = deltaAngle / dt;
        _velocity = Mathf.Lerp(_velocity, instantVelocity, 0.5f);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // El impulso queda guardado en _velocity — Update() lo sigue aplicando con frenado.
    }

    // Este objeto es un Image invisible a pantalla completa en el Canvas de Overlay, o sea que
    // está por encima del mundo 3D y se queda con TODOS los toques — los nodos, que viven en
    // Canvases en World Space colgados de la esfera, nunca los reciben. Así que cuando el toque
    // fue un tap y no un arrastre, se busca a mano qué había debajo y se le reenvía.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return; // fue un arrastre del mundo, no un tap sobre un nodo

        _raycastHits.Clear();
        EventSystem.current.RaycastAll(eventData, _raycastHits);

        foreach (var hit in _raycastHits)
        {
            if (!hit.gameObject || hit.gameObject == gameObject) continue; // saltar el propio DragArea

            // El nodo puede estar varios niveles abajo (el número, las estrellas), así que se
            // sube por la jerarquía hasta encontrar el LevelNodeView.
            var view = hit.gameObject.GetComponentInParent<LevelNodeView>();
            if (!view) continue;

            // Se dispara su propio Button en vez de llamar al evento directo: ahí adentro está el
            // filtro de nivel bloqueado y la vibración, y no hay que duplicar ese criterio acá.
            var button = view.GetComponent<Button>();
            if (button && button.IsInteractable()) button.onClick.Invoke();
            return;
        }
    }

    void Update()
    {
        if (Mathf.Abs(_velocity) < 0.01f) { _velocity = 0f; return; }
        ApplyAngleDelta(_velocity * Time.unscaledDeltaTime);
        _velocity *= Mathf.Pow(decelerationRate, Time.unscaledDeltaTime);
    }

    void ApplyAngleDelta(float delta)
    {
        if (!sphere) return;
        float angleMax = nodePositioner ? nodePositioner.AngleMax : 0f;
        _currentAngle = Mathf.Clamp(_currentAngle + delta, ANGLE_MIN, angleMax);
        // Signo negativo a propósito: avanzar el scroll (ángulo creciendo desde 0) tiene que
        // BAJAR los nodos desde el horizonte hacia la cámara — el nodo 1 es el ancla en 0 y los
        // siguientes van llegando. Con el signo al revés el mundo se alejaría en vez de acercarse.
        sphere.localRotation = Quaternion.Euler(-_currentAngle, 0f, 0f);
    }
}
