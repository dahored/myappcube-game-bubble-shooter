using UnityEngine;
using UnityEngine.EventSystems;

// Reenvía SOLO drag a CannonController — sin click (el Button.onClick propio de este mismo
// objeto ya cubre eso, ej. CurrentBubble/SwapCurrentAndNext) y sin forzar sibling order (a
// diferencia de AimInputRelay, que sí lo hace porque AimArea necesita quedar siempre atrás;
// acá el objeto necesita seguir renderizando arriba, tocarlo cambiaría el orden visual).
//
// Mismo problema que BubbleView: el sistema de eventos de Unity solo sube por los ANCESTROS
// del objeto tocado buscando quién maneja el drag, nunca cruza a un hermano — como AimArea es
// hermano de los elementos del cañón (no ancestro), si el gesto de apuntado arranca encima de
// CurrentBubble/CannonBase/etc. nunca llegaba a AimInputRelay (reportado por Diego).
public class DragToAimRelay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] CannonController cannon;

    public void OnBeginDrag(PointerEventData e) => cannon.OnAimBegin(e.position);
    public void OnDrag(PointerEventData e)      => cannon.OnAimDrag(e.position);
    public void OnEndDrag(PointerEventData e)   => cannon.OnAimEnd(e.position);
}
