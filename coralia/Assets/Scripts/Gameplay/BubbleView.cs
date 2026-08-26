using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BubbleView : MonoBehaviour
{
    [SerializeField] Image bubbleImage;
    [SerializeField] Image creatureIcon;

    const float POP_DURATION      = 0.25f; // GDD 1.5 — animación de explosión
    const float DROP_GRAVITY      = 2600f; // px/s² — caída acelerada en vez de velocidad constante, se siente más real
    const float DROP_MAX_DURATION = 1f;    // tope de seguridad
    const float DROP_FADE_START   = 0.5f;  // a partir de acá empieza a desvanecerse

    const int   POP_PARTICLE_COUNT    = 8;
    const float POP_PARTICLE_SIZE     = 22f;  // px — bastante más chico que la burbuja (92px)
    const float POP_PARTICLE_SPEED    = 260f; // px/s
    const float POP_PARTICLE_LIFETIME = 0.35f;

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

    // delay: para que el match no explote todo junto — GameplayController le pasa un
    // delay creciente según qué tan lejos está cada burbuja de la que disparó el jugador.
    public void PlayPopAnimation(float delay = 0f) => StartCoroutine(PopAndDestroy(delay));
    public void PlayDropAnimation() => StartCoroutine(DropAndDestroy());

    IEnumerator PopAndDestroy(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        SpawnPopParticles();

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

    // "Explosión" gratis, sin arte nuevo: reusa el mismo sprite de la burbuja, achicado,
    // como un puñado de partículas que salen disparadas en círculo y se desvanecen. No usa
    // ParticleSystem de Unity porque acá todo es uGUI (Canvas), no mundo 2D/3D — ParticleSystem
    // no rinde bien dentro de un Canvas Screen Space Overlay sin configuración extra.
    void SpawnPopParticles()
    {
        if (!bubbleImage || !bubbleImage.sprite) return;

        var parent = transform.parent;
        var origin = ((RectTransform)transform).anchoredPosition;
        var color  = bubbleImage.color;

        for (int i = 0; i < POP_PARTICLE_COUNT; i++)
        {
            var go = new GameObject("PopParticle", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = origin;
            rt.sizeDelta        = Vector2.one * POP_PARTICLE_SIZE;

            var img = go.GetComponent<Image>();
            img.sprite        = bubbleImage.sprite;
            img.color         = color;
            img.raycastTarget = false;

            float angle = (360f / POP_PARTICLE_COUNT) * i + Random.Range(-15f, 15f); // jitter, no todas parejitas
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            float   speed = POP_PARTICLE_SPEED * Random.Range(0.75f, 1.25f);

            go.AddComponent<PopParticle>().Init(origin, dir * speed, POP_PARTICLE_LIFETIME);
        }
    }

    // Gravedad simulada (acelera en vez de moverse a velocidad constante) + un giro
    // aleatorio por burbuja, para que la caída en cadena no se vea toda igual/rígida.
    IEnumerator DropAndDestroy()
    {
        var     rt       = (RectTransform)transform;
        Vector2 pos      = rt.anchoredPosition;
        float   velocity = 0f;
        float   spin     = Random.Range(-180f, 180f); // grados/seg
        float   t        = 0f;

        while (t < DROP_MAX_DURATION)
        {
            t += Time.deltaTime;
            velocity -= DROP_GRAVITY * Time.deltaTime;
            pos.y    += velocity * Time.deltaTime;
            rt.anchoredPosition = pos;
            rt.Rotate(0f, 0f, spin * Time.deltaTime);

            if (t > DROP_FADE_START && bubbleImage)
            {
                float fadeP = (t - DROP_FADE_START) / (DROP_MAX_DURATION - DROP_FADE_START);
                bubbleImage.color = new Color(1f, 1f, 1f, 1f - fadeP);
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}
