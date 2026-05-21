using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneTransition : MonoBehaviour
{
    static SceneTransition _instance;
    static Sprite[] _bubbleSprites;
    static AudioClip _bubbleSound;
    Image _overlay;
    RectTransform _layerBack;
    RectTransform _layerFront;
    AudioSource _audioSource;

    const float FADE_IN_DURATION  = 0.3f;
    const float ANIM_DURATION     = 2.5f;
    const float FADE_OUT_DURATION = 0.3f;
    const int   BUBBLE_COUNT_BACK  = 60;
    const int   BUBBLE_COUNT_FRONT = 50;
    const float SIZE_BACK_MIN  = 150f;
    const float SIZE_BACK_MAX  = 250f;
    const float SIZE_FRONT_MIN = 200f;
    const float SIZE_FRONT_MAX = 300f;
    const float SPEED_BACK     = 1.4f; // multiplicador — capa lejana, más lenta
    const float SPEED_FRONT    = 2.0f; // multiplicador — capa cercana, más rápida
    const float BUBBLE_SCREENS = 2f;   // alto del container en pantallas (densidad de burbujas)
    const float BUBBLE_TRAVEL  = 2f;   // cuántas pantallas sube el container — si < BUBBLE_SCREENS quedan burbujas visibles al final
    const float BG_FADE_AT     = 1f;   // a qué pantalla empieza a desaparecer el fondo azul

    static readonly Color BG_COLOR = new Color(0x17 / 255f, 0x7B / 255f, 0xFF / 255f, 0f);

    public static bool Enabled = false;

    public static void SetBubbleSprites(Sprite[] sprites) => _bubbleSprites = sprites;
    public static void SetBubbleSound(AudioClip clip)    => _bubbleSound  = clip;

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
        canvasGO.AddComponent<GraphicRaycaster>();

        var imgGO = new GameObject("Overlay");
        imgGO.transform.SetParent(canvasGO.transform, false);
        _instance._overlay = imgGO.AddComponent<Image>();
        _instance._overlay.color = BG_COLOR;
        _instance._overlay.raycastTarget = true;
        var ort = imgGO.GetComponent<RectTransform>();
        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        ort.offsetMin = ort.offsetMax = Vector2.zero;

        _instance._layerBack   = CreateLayer(canvasGO.transform, "BubblesBack");
        _instance._layerFront  = CreateLayer(canvasGO.transform, "BubblesFront");
        _instance._audioSource = root.AddComponent<AudioSource>();
        _instance._audioSource.playOnAwake = false;
    }

    static RectTransform CreateLayer(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        return rt;
    }

    public static void GoTo(string sceneName)
    {
        if (!Enabled) { SceneManager.LoadScene(sceneName); return; }
        Instance.StartCoroutine(Instance.Transition(sceneName));
    }

    public static void FadeOutThen(System.Action onActivate)
    {
        if (!Enabled) { onActivate?.Invoke(); return; }
        Instance.StartCoroutine(Instance.FadeAndCallback(onActivate));
    }

    public static void FadeIn()
    {
        if (!Enabled) return;
        Instance.StartCoroutine(Instance.Fade(1f, 0f, FADE_OUT_DURATION));
    }

    IEnumerator Transition(string sceneName)
    {
        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        SetupBubbles();
        yield return Fade(0f, 1f, FADE_IN_DURATION);
        yield return MoveLayers(() =>
        {
            op.allowSceneActivation = true;
            StartCoroutine(WaitThenFade(op));
        });
        ClearBubbles();
    }

    IEnumerator FadeAndCallback(System.Action onActivate)
    {
        SetupBubbles();
        yield return Fade(0f, 1f, FADE_IN_DURATION);
        yield return MoveLayers(() => StartCoroutine(WaitThenActivate(onActivate)));
        ClearBubbles();
    }

    IEnumerator WaitThenFade(AsyncOperation op)
    {
        yield return new WaitUntil(() => op.isDone);
        yield return Fade(1f, 0f, FADE_OUT_DURATION);
    }

    IEnumerator WaitThenActivate(System.Action onActivate)
    {
        yield return null;
        onActivate?.Invoke();
        yield return new WaitForSeconds(0.05f); // margen para que cargue la escena
        yield return Fade(1f, 0f, FADE_OUT_DURATION);
    }

    void SetupBubbles()
    {
        ClearBubbles();
        float screenW = Screen.width;
        float screenH = Screen.height;
        PopulateLayer(_layerBack,  screenW, screenH, BUBBLE_COUNT_BACK,  SIZE_BACK_MIN,  SIZE_BACK_MAX,  0.6f);
        PopulateLayer(_layerFront, screenW, screenH, BUBBLE_COUNT_FRONT, SIZE_FRONT_MIN, SIZE_FRONT_MAX, 1f);

        if (_bubbleSound != null)
        {
            _audioSource.clip = _bubbleSound;
            _audioSource.Play();
        }
    }

    void PopulateLayer(RectTransform layer, float screenW, float screenH, int count, float sizeMin, float sizeMax, float alpha)
    {
        float containerH = screenH * BUBBLE_SCREENS;
        layer.sizeDelta        = new Vector2(screenW, containerH);
        layer.anchoredPosition = new Vector2(0, -screenH * 1.5f);

        for (int i = 0; i < count; i++)
        {
            var go  = new GameObject("Bubble");
            go.transform.SetParent(layer, false);

            var img = go.AddComponent<Image>();
            if (_bubbleSprites != null && _bubbleSprites.Length > 0)
            {
                var sp = _bubbleSprites[Random.Range(0, _bubbleSprites.Length)];
                if (sp != null) img.sprite = sp;
            }
            img.color         = new Color(1f, 1f, 1f, Random.Range(alpha * 0.6f, alpha));
            img.raycastTarget  = false;

            float size = Random.Range(sizeMin, sizeMax);
            var rt     = go.GetComponent<RectTransform>();
            rt.sizeDelta        = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(
                Random.Range(-screenW * 0.5f, screenW * 0.5f),
                Random.Range(-containerH * 0.5f, containerH * 0.5f)
            );
        }
    }

    IEnumerator MoveLayers(System.Action onActivate)
    {
        float screenH        = Screen.height;
        float from           = -screenH * 1.5f;
        float toBack         = screenH * BUBBLE_TRAVEL * SPEED_BACK;
        float toFront        = screenH * BUBBLE_TRAVEL * SPEED_FRONT;
        float bgFadeProgress = BG_FADE_AT / BUBBLE_TRAVEL;
        float t              = 0f;
        bool  activated      = false;

        while (t < ANIM_DURATION)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / ANIM_DURATION);

            _layerBack.anchoredPosition  = new Vector2(0, Mathf.Lerp(from, toBack,  progress));
            _layerFront.anchoredPosition = new Vector2(0, Mathf.Lerp(from, toFront, progress));

            if (!activated && progress >= bgFadeProgress)
            {
                activated = true;
                onActivate?.Invoke();
            }

            yield return null;
        }
    }

    void ClearBubbles()
    {
        foreach (Transform child in _layerBack)  Destroy(child.gameObject);
        foreach (Transform child in _layerFront) Destroy(child.gameObject);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        Color c = _overlay.color;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, t / duration);
            _overlay.color = c;
            yield return null;
        }
        c.a = to;
        _overlay.color = c;
    }
}
