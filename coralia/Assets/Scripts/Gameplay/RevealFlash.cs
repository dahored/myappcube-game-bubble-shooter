using UnityEngine;
using UnityEngine.UI;

// El estallido que acompaña la aparición de una burbuja: sale chico detrás de ella, crece y se
// apaga.
//
// Vida propia y no una corrutina de quien lo lanza, por lo mismo que PopParticle: si el que lo
// pidió se destruye o corta su corrutina a mitad de camino, el destello se quedaría congelado en
// pantalla. Acá se apaga y se destruye solo, pase lo que pase con quien lo creó.
public class RevealFlash : MonoBehaviour
{
    RectTransform _rt;
    Image         _image;
    Color         _baseColor;
    float         _lifetime;
    float         _fromScale;
    float         _toScale;
    float         _spin;
    float         _t;

    // Se crea al lado de la burbuja y con su mismo anclaje, así queda centrado encima sin importar
    // cómo esté posicionada. Se mete en el índice de hermano de ella, o sea justo DETRÁS: el
    // destello enmarca a la burbuja, no la tapa.
    public static void Spawn(RectTransform beside, Sprite sprite, float size,
                             float lifetime, float fromScale, float toScale, float spin)
    {
        if (sprite == null || beside == null || beside.parent == null) return;

        var go = new GameObject("RevealFlash", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;

        rt.SetParent(beside.parent, false);
        rt.SetSiblingIndex(beside.GetSiblingIndex());

        rt.anchorMin        = beside.anchorMin;
        rt.anchorMax        = beside.anchorMax;
        rt.pivot            = beside.pivot;
        rt.anchoredPosition = beside.anchoredPosition;
        rt.sizeDelta        = Vector2.one * size;

        var image = go.GetComponent<Image>();
        image.sprite        = sprite;
        image.raycastTarget = false;

        go.AddComponent<RevealFlash>().Init(lifetime, fromScale, toScale, spin);
    }

    void Init(float lifetime, float fromScale, float toScale, float spin)
    {
        _rt        = (RectTransform)transform;
        _image     = GetComponent<Image>();
        _baseColor = _image.color;
        _lifetime  = Mathf.Max(0.01f, lifetime);
        _fromScale = fromScale;
        _toScale   = toScale;
        _spin      = spin;

        Apply(0f);
    }

    void Update()
    {
        _t += Time.deltaTime;

        if (_t >= _lifetime) { Destroy(gameObject); return; }
        Apply(_t / _lifetime);
    }

    // Crece rápido y frena, que es como se lee un destello: la explosión está al principio. Con
    // una interpolación lineal parecería que se infla.
    void Apply(float p)
    {
        float eased = 1f - (1f - p) * (1f - p);

        _rt.localScale    = Vector3.one * Mathf.Lerp(_fromScale, _toScale, eased);
        _rt.localRotation = Quaternion.Euler(0f, 0f, _spin * eased);
        _image.color      = new Color(_baseColor.r, _baseColor.g, _baseColor.b, _baseColor.a * (1f - p));
    }
}
