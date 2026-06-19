using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum PinDirection { Top, Bottom }

// Botón flotante direccional.
// Top: aparece arriba cuando el nodo actual está DEBAJO del viewport.
// Bottom: aparece abajo cuando el nodo actual está ENCIMA del viewport.
public class ScrollPinController : MonoBehaviour
{
    [SerializeField] ScrollRect    scrollRect;
    [SerializeField] RectTransform contentRT;
    [SerializeField] PinDirection  direction;   // Top o Bottom

    [Header("Threshold (en cantidad de nodos)")]
    [SerializeField] int   nodesThreshold = 2;

    [Header("Salto")]
    [SerializeField] int   jumpCount    = 2;
    [SerializeField] float jumpHeight   = 24f;
    [SerializeField] float jumpDuration = 0.50f;
    [SerializeField] float jumpPause    = 1.80f;

    [Header("Animación")]
    [SerializeField] float scrollDuration = 0.45f;
    [SerializeField] float fadeDuration   = 0.20f;

    CanvasGroup   cg;
    RectTransform rt;
    RectTransform viewportRT;
    RectTransform nodeRT;
    float         spacing;
    Vector2       basePos;
    bool          isVisible;
    Coroutine     fadeRoutine;
    Coroutine     scrollRoutine;
    Coroutine     jumpAfterFadeRoutine;
    Coroutine     jumpLoopRoutine;

    void Awake()
    {
        rt         = GetComponent<RectTransform>();
        cg         = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        viewportRT = scrollRect.viewport;
        basePos    = rt.anchoredPosition;
        SetAlpha(0f);
        cg.interactable = cg.blocksRaycasts = false;
    }

    public void Init(RectTransform currentNode, float nodeSpacing)
    {
        nodeRT  = currentNode;
        spacing = nodeSpacing;

        // Solo el Bottom pin centra el scroll al cargar (es el pin "principal")
        if (direction == PinDirection.Bottom)
        {
            float scrollable = contentRT.rect.height - viewportRT.rect.height;
            if (scrollable > 0f)
            {
                float centeredY = currentNode.anchoredPosition.y - viewportRT.rect.height * 0.5f;
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01(centeredY / scrollable);
            }
        }

        scrollRect.onValueChanged.AddListener(_ => Evaluate());
        Evaluate();
    }

    void Evaluate()
    {
        if (nodeRT == null) return;

        float scrollable = contentRT.rect.height - viewportRT.rect.height;
        if (scrollable <= 0f) return;

        float viewBottom = scrollRect.verticalNormalizedPosition * scrollable;
        float viewTop    = viewBottom + viewportRT.rect.height;
        float nodeY      = nodeRT.anchoredPosition.y;
        float threshold  = nodesThreshold * spacing;

        bool shouldShow = direction == PinDirection.Top
            ? nodeY < viewBottom - threshold   // nodo debajo del viewport → pin arriba
            : nodeY > viewTop   + threshold;   // nodo encima del viewport → pin abajo

        if (shouldShow == isVisible) return;
        isVisible = shouldShow;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(isVisible ? 1f : 0f));
        cg.interactable = cg.blocksRaycasts = isVisible;

        if (isVisible)
        {
            StopJump();
            jumpAfterFadeRoutine = StartCoroutine(JumpAfterFade());
        }
        else
        {
            StopJump();
            rt.anchoredPosition = basePos;
        }
    }

    void StopJump()
    {
        if (jumpAfterFadeRoutine != null) { StopCoroutine(jumpAfterFadeRoutine); jumpAfterFadeRoutine = null; }
        if (jumpLoopRoutine      != null) { StopCoroutine(jumpLoopRoutine);      jumpLoopRoutine      = null; }
    }

    IEnumerator JumpAfterFade()
    {
        yield return new WaitForSeconds(fadeDuration);
        basePos = rt.anchoredPosition;
        jumpLoopRoutine = StartCoroutine(JumpLoop());
    }

    IEnumerator JumpLoop()
    {
        // Top pin salta hacia abajo (apunta hacia el nodo que está debajo)
        // Bottom pin salta hacia arriba (apunta hacia el nodo que está arriba)
        float dir = direction == PinDirection.Top ? -1f : 1f;
        while (true)
        {
            for (int j = 0; j < jumpCount; j++)
            {
                for (float t = 0f; t < 1f; t += Time.deltaTime / jumpDuration)
                {
                    float offsetY = Mathf.Sin(t * Mathf.PI) * jumpHeight * dir;
                    rt.anchoredPosition = basePos + new Vector2(0f, offsetY);
                    yield return null;
                }
                rt.anchoredPosition = basePos;
            }
            yield return new WaitForSeconds(jumpPause);
        }
    }

    public void OnTap()
    {
        if (nodeRT == null) return;
        if (scrollRoutine != null) StopCoroutine(scrollRoutine);
        scrollRoutine = StartCoroutine(ScrollToNode());
    }

    IEnumerator ScrollToNode()
    {
        float scrollable  = contentRT.rect.height - viewportRT.rect.height;
        float centeredY   = nodeRT.anchoredPosition.y - viewportRT.rect.height * 0.5f;
        float targetNormY = Mathf.Clamp01(centeredY / scrollable);

        float start = scrollRect.verticalNormalizedPosition;
        for (float t = 0f; t < 1f; t += Time.deltaTime / scrollDuration)
        {
            scrollRect.verticalNormalizedPosition = Mathf.Lerp(start, targetNormY, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        scrollRect.verticalNormalizedPosition = targetNormY;
    }

    IEnumerator Fade(float to)
    {
        float from = cg.alpha;
        for (float t = 0f; t < 1f; t += Time.deltaTime / fadeDuration)
        {
            SetAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }
        SetAlpha(to);
    }

    void SetAlpha(float a) => cg.alpha = a;
}
