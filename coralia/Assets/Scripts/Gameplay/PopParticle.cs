using UnityEngine;
using UnityEngine.UI;

// Vida propia e independiente de la burbuja que la generó. Si esta animación corriera
// como coroutine en BubbleView, se mataría junto con la burbuja (que se destruye antes
// de que la partícula termine su fade), dejándola congelada como un puntito a mitad de
// camino en vez de desaparecer del todo — por eso es su propio componente.
public class PopParticle : MonoBehaviour
{
    RectTransform _rt;
    Image         _image;
    Vector2       _origin;
    Vector2       _velocity;
    Color         _baseColor;
    float         _lifetime;
    float         _t;

    public void Init(Vector2 origin, Vector2 velocity, float lifetime)
    {
        _rt        = (RectTransform)transform;
        _image     = GetComponent<Image>();
        _origin    = origin;
        _velocity  = velocity;
        _lifetime  = lifetime;
        _baseColor = _image.color;
    }

    void Update()
    {
        _t += Time.deltaTime;
        if (_t >= _lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float p = _t / _lifetime;
        _rt.anchoredPosition = _origin + _velocity * _t;
        _rt.localScale       = Vector3.one * (1f - p);
        _image.color         = new Color(_baseColor.r, _baseColor.g, _baseColor.b, _baseColor.a * (1f - p));
    }
}
