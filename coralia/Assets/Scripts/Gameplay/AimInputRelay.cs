using UnityEngine;
using UnityEngine.EventSystems;

// Vive en AimArea (la zona inferior de pantalla) y reenvía el drag a CannonController —
// están en GameObjects distintos porque AimArea es la zona de input, no el cañón visual.
public class AimInputRelay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] CannonController cannon;

    public void OnBeginDrag(PointerEventData e) => cannon.OnAimBegin(e.position);
    public void OnDrag(PointerEventData e)      => cannon.OnAimDrag(e.position);
    public void OnEndDrag(PointerEventData e)   => cannon.OnAimEnd(e.position);
}
