using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaPanel : MonoBehaviour
{
    void Awake() => Apply();

    void Apply()
    {
        var safe = Screen.safeArea;
        var rt   = GetComponent<RectTransform>();

        Vector2 anchorMin = safe.position;
        Vector2 anchorMax = safe.position + safe.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rt.anchorMin = new Vector2(Mathf.Clamp01(anchorMin.x), Mathf.Clamp01(anchorMin.y));
        rt.anchorMax = new Vector2(Mathf.Clamp01(anchorMax.x), Mathf.Clamp01(anchorMax.y));
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;
    }
}
