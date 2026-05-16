using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneTransition : MonoBehaviour
{
    static SceneTransition _instance;
    Image _overlay;

    const float DURATION = 0.35f;
    public static bool Enabled = false; // cambiar a true cuando esté listo

    static SceneTransition Instance
    {
        get
        {
            if (_instance == null) CreateInstance();
            return _instance;
        }
    }

    static void CreateInstance()
    {
        var root = new GameObject("SceneTransition");
        DontDestroyOnLoad(root);
        _instance = root.AddComponent<SceneTransition>();

        var canvasGO = new GameObject("Canvas");
        canvasGO.transform.SetParent(root.transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasGO.AddComponent<CanvasScaler>();

        var imgGO = new GameObject("Overlay");
        imgGO.transform.SetParent(canvasGO.transform, false);
        _instance._overlay = imgGO.AddComponent<Image>();
        _instance._overlay.color = new Color(0, 0, 0, 0);
        _instance._overlay.raycastTarget = false;
        var rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public static void GoTo(string sceneName)
    {
        if (!Enabled) { SceneManager.LoadScene(sceneName); return; }
        Instance.StartCoroutine(Instance.Transition(sceneName));
    }

    public static void FadeOutThen(System.Action onComplete)
    {
        if (!Enabled) { onComplete?.Invoke(); return; }
        Instance.StartCoroutine(Instance.FadeAndCallback(onComplete));
    }

    public static void FadeIn()
    {
        if (!Enabled) return;
        Instance.StartCoroutine(Instance.Fade(1f, 0f));
    }

    IEnumerator Transition(string sceneName)
    {
        yield return Fade(0f, 1f);
        SceneManager.LoadScene(sceneName);
        yield return null; // espera un frame para que cargue
        yield return Fade(1f, 0f);
    }

    IEnumerator FadeAndCallback(System.Action onComplete)
    {
        yield return Fade(0f, 1f);
        onComplete?.Invoke();
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        Color c = _overlay.color;
        while (t < DURATION)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, t / DURATION);
            _overlay.color = c;
            yield return null;
        }
        c.a = to;
        _overlay.color = c;
    }
}
