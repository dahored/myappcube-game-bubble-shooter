using UnityEngine;

public class ResponsiveLayout : MonoBehaviour
{
    [System.Serializable]
    public struct LayoutPreset
    {
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
    }

    [Header("Targets")]
    [SerializeField] RectTransform[] targets;

    [Header("Phone (aspect < 0.6)")]
    [SerializeField] LayoutPreset[] phonePresets;

    [Header("Tablet (aspect >= 0.6)")]
    [SerializeField] LayoutPreset[] tabletPresets;

    System.Collections.IEnumerator Start()
    {
        yield return null; // wait one frame — Device Simulator screen size settles after scene activation
        float ratio = (float)Screen.width / Screen.height;
        bool isTablet = ratio >= 0.6f;
        var presets = isTablet ? tabletPresets : phonePresets;

        for (int i = 0; i < targets.Length; i++)
        {
            if (i >= presets.Length) break;
            targets[i].anchoredPosition = presets[i].anchoredPosition;
            if (presets[i].sizeDelta != Vector2.zero)
                targets[i].sizeDelta = presets[i].sizeDelta;
        }
    }
}