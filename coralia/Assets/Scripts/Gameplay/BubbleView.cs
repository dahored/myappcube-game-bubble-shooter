using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BubbleView : MonoBehaviour
{
    [SerializeField] Image bubbleImage;
    [SerializeField] Image creatureIcon;

    const float POP_DURATION  = 0.25f; // GDD 1.5 — animación de explosión
    const float DROP_DURATION = 0.4f;

    public Vector2Int  Cell       { get; private set; }
    public BubbleColor ColorType  { get; private set; }
    public bool        IsCreature { get; private set; }

    public void Setup(Vector2Int cell, BubbleColor color, Sprite sprite)
    {
        Cell      = cell;
        ColorType = color;
        if (bubbleImage) bubbleImage.sprite = sprite;

        var rt = (RectTransform)transform;
        rt.anchoredPosition = HexGridMath.CellToLocalPos(cell);
    }

    public void SetCell(Vector2Int cell) => Cell = cell;

    public void SetCreatureMarker(bool isCreature)
    {
        IsCreature = isCreature;
        if (creatureIcon) creatureIcon.gameObject.SetActive(isCreature);
    }

    public void PlayPopAnimation() => StartCoroutine(PopAndDestroy());
    public void PlayDropAnimation() => StartCoroutine(DropAndDestroy());

    // TODO: VFX hook — partículas de explosión cuando haya presupuesto de arte para eso
    IEnumerator PopAndDestroy()
    {
        Vector3 baseScale = transform.localScale;
        float   t = 0f;
        while (t < POP_DURATION)
        {
            t += Time.deltaTime;
            float p = t / POP_DURATION;
            transform.localScale = baseScale * (1f + 0.3f * p);
            if (bubbleImage) bubbleImage.color = new Color(1f, 1f, 1f, 1f - p);
            yield return null;
        }
        Destroy(gameObject);
    }

    IEnumerator DropAndDestroy()
    {
        var     rt   = (RectTransform)transform;
        Vector2 from = rt.anchoredPosition;
        Vector2 to   = from + Vector2.down * 400f;
        float   t = 0f;
        while (t < DROP_DURATION)
        {
            t += Time.deltaTime;
            float p = t / DROP_DURATION;
            rt.anchoredPosition = Vector2.Lerp(from, to, p);
            if (bubbleImage) bubbleImage.color = new Color(1f, 1f, 1f, 1f - p);
            yield return null;
        }
        Destroy(gameObject);
    }
}
