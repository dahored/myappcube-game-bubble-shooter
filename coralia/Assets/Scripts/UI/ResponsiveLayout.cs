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

    void Awake()
    {
        bool isTablet = (float)Screen.width / Screen.height >= 0.6f;
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