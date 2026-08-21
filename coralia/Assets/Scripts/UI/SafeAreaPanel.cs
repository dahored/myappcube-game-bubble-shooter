using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaPanel : MonoBehaviour
{
    void Awake() => Apply();

    // Reintento un frame después: cuando la escena se carga como parte de una transición
    // (en vez de ser la primera escena en arrancar), Screen.safeArea a veces todavía no
    // está actualizado en Awake() y devuelve un valor viejo/incorrecto — este segundo
    // Apply() se autocorrige. No hace nada raro si el primer cálculo ya estaba bien.
    IEnumerator Start()
    {
        yield return null;
        Apply();
    }

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
