using UnityEngine;
using UnityEngine.UI;

// Aplica el padding del notch/Dynamic Island al VerticalLayoutGroup del TopPanel.
// TopPanel debe ser hijo directo del Canvas (NO dentro de SafeArea).
public class TopPanelController : MonoBehaviour
{
    [SerializeField] float defaultTopPadding = 20f;

    // Para ScrollPinController: height real del panel después del layout
    public float PanelHeight => GetComponent<RectTransform>().rect.height;

    void Start()
    {
        Canvas canvas  = GetComponentInParent<Canvas>().rootCanvas;
        float  canvasH = canvas.GetComponent<RectTransform>().rect.height;
        float  insetPx = Screen.height - Screen.safeArea.yMax;
        float  inset   = canvasH > 0 ? insetPx * canvasH / Screen.height : insetPx;
        float  padding = Mathf.Max(inset, defaultTopPadding);

        var vlg = GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.padding.top = Mathf.RoundToInt(padding);
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        }
    }
}
