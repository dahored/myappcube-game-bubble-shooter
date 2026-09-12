using UnityEngine;
using UnityEngine.EventSystems;

// Reenvía el drag desde un Image full-screen (necesita RectTransform + Raycast Target para que
// el EventSystem lo detecte) hacia LevelMapCurveController, que vive en un GameObject sin UI
// (Transform normal, ej. "CurveWorld") — mismo motivo que AimInputRelay/DragToAimRelay del cañón.
public class CurveDragRelay : MonoBehaviour, IDragHandler
{
    [SerializeField] LevelMapCurveController target;

    public void OnDrag(PointerEventData eventData) => target?.OnDrag(eventData);
}
