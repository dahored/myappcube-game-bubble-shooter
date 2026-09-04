using UnityEngine;
using UnityEngine.EventSystems;

// Vive en AimArea (la zona inferior de pantalla) y reenvía el drag a CannonController —
// están en GameObjects distintos porque AimArea es la zona de input, no el cañón visual.
public class AimInputRelay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] CannonController cannon;

    // Fuerza a AimArea a ser siempre el primer hermano de su padre (menor prioridad de
    // raycast que cualquier otro sibling, ej. GridContainer) — sin esto, si en el Editor
    // AimArea queda ordenada DESPUÉS del grid, se dibuja/raycastea encima de las burbujas y
    // les roba el tap antes de que BubbleView.OnPointerClick llegue a dispararse (bug real,
    // reportado por Diego: tocar una burbuja no hacía nada).
    void Awake() => transform.SetAsFirstSibling();

    public void OnBeginDrag(PointerEventData e) => cannon.OnAimBegin(e.position);
    public void OnDrag(PointerEventData e)      => cannon.OnAimDrag(e.position);
    public void OnEndDrag(PointerEventData e)   => cannon.OnAimEnd(e.position);

    // Tap sin drag sobre cualquier punto de AimArea (ej. un espacio vacío entre 2 burbujas,
    // no una burbuja exacta — eso ya lo cubre BubbleView.OnTapped) — apunta y dispara de una
    // hacia ahí, igual que en otros bubble shooters (pedido de Diego). El EventSystem nunca
    // dispara esto Y los handlers de drag a la vez para el mismo gesto: si el dedo se mueve
    // más que el umbral de drag, esto no se llama; si se queda quieto (tap real), sí.
    public void OnPointerClick(PointerEventData e) => cannon.OnTapShoot(e.position);
}
