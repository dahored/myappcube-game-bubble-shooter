using UnityEngine;

// Wobble tipo gelatina en loop — squash & stretch continuo, para darle vida a decoraciones
// estáticas (plantas, corales, etc.) sin necesitar sprites/frames de animación. Mismos
// keyframes que el ejemplo CSS de referencia: scale(1,1) → (0.9,1.1) 25% → (1.1,0.9) 50%
// → (0.95,1.05) 75% → (1,1) 100%, interpolado en loop.
public class GelatineAnimation : MonoBehaviour
{
    [SerializeField] float duration = 3f;       // segundos por ciclo completo
    [SerializeField] float startDelay;          // offset inicial — variar entre varias plantas para que no se muevan todas igual

    static readonly (float t, float x, float y)[] Keyframes =
    {
        (0f,    1f,    1f),
        (0.25f, 0.9f,  1.1f),
        (0.5f,  1.1f,  0.9f),
        (0.75f, 0.95f, 1.05f),
        (1f,    1f,    1f),
    };

    Vector3 _baseScale;
    float   _t;

    void Awake()
    {
        _baseScale = transform.localScale;
        _t         = startDelay / duration;
    }

    void Update()
    {
        _t = (_t + Time.deltaTime / duration) % 1f;
        var (x, y) = Evaluate(_t);
        transform.localScale = new Vector3(_baseScale.x * x, _baseScale.y * y, _baseScale.z);
    }

    static (float x, float y) Evaluate(float t)
    {
        for (int i = 0; i < Keyframes.Length - 1; i++)
        {
            var (t0, x0, y0) = Keyframes[i];
            var (t1, x1, y1) = Keyframes[i + 1];
            if (t < t0 || t > t1) continue;
            float p = (t - t0) / (t1 - t0);
            // SmoothStep en vez de Lerp: velocidad cero en cada keyframe, así los tramos
            // conectan sin quiebre en vez de cambiar de dirección de golpe (línea recta).
            return (Mathf.SmoothStep(x0, x1, p), Mathf.SmoothStep(y0, y1, p));
        }
        return (1f, 1f);
    }
}
