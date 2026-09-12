using UnityEngine;

// Wobble tipo gelatina en loop — squash & stretch continuo, para darle vida a decoraciones
// estáticas (plantas, corales, etc.) sin necesitar sprites/frames de animación. Mismos
// keyframes que el ejemplo CSS de referencia: scale(1,1) → (1-s,1+s) 25% → (1+s,1-s) 50%
// → (1-s/2,1+s/2) 75% → (1,1) 100% (s = squashAmount), interpolado en loop.
//
// Balanceo ondulatorio opcional (rotación de lado a lado) — aparte del squash/stretch, para
// decoraciones altas (algas, corales) que además se mecen como en una corriente. Período
// independiente del squash para que no queden sincronizados y se vea más orgánico.
public class GelatineAnimation : MonoBehaviour
{
    [Header("Squash & stretch")]
    [SerializeField] float duration      = 3f; // segundos por ciclo completo
    [SerializeField] float startDelay;         // offset inicial — variar entre varias plantas para que no se muevan todas igual
    [Tooltip("Si está tildado, ignora 'Start Delay' y sortea uno nuevo (entre 0.5 y 2.5s) cada vez que corre el script — evita tener que variarlo a mano planta por planta.")]
    [SerializeField] bool  randomDelay;
    [Tooltip("Si está tildado, ignora 'Duration' y sortea uno nuevo (entre Duration Min/Max) cada vez que corre el script.")]
    [SerializeField] bool  randomDuration;
    [SerializeField] float durationMin = 8f;
    [SerializeField] float durationMax = 12f;
    [SerializeField, Range(0f, 0.5f)] float squashAmount = 0.1f; // qué tan lejos de 1 llega la escala en cada extremo — antes hardcodeado, ahora editable por instancia

    [Header("Balanceo ondulatorio (opcional)")]
    [SerializeField] bool  enableSway     = false; // además del squash/stretch, mece de lado a lado
    [SerializeField] float swayAngle      = 6f;    // grados a cada lado
    [SerializeField] float swayDuration   = 4f;    // segundos por ciclo completo — distinto de 'duration' a propósito

    (float t, float x, float y)[] _keyframes;

    Vector3 _baseScale;
    float   _t;
    float   _swayT;

    void Awake()
    {
        _baseScale = transform.localScale;
        if (randomDelay) startDelay = Random.Range(0.5f, 2.5f);
        if (randomDuration) duration = Random.Range(durationMin, durationMax);
        _t         = startDelay / duration;
        _swayT     = startDelay / swayDuration;

        // Calculado una vez acá (no en Update) — un array nuevo por frame sería la misma
        // presión de GC que ya se corrigió en TrajectoryLine este mismo proyecto.
        _keyframes = new (float, float, float)[]
        {
            (0f,    1f,                     1f),
            (0.25f, 1f - squashAmount,       1f + squashAmount),
            (0.5f,  1f + squashAmount,       1f - squashAmount),
            (0.75f, 1f - squashAmount * 0.5f, 1f + squashAmount * 0.5f),
            (1f,    1f,                     1f),
        };
    }

    void Update()
    {
        _t = (_t + Time.deltaTime / duration) % 1f;
        var (x, y) = Evaluate(_t);
        transform.localScale = new Vector3(_baseScale.x * x, _baseScale.y * y, _baseScale.z);

        if (enableSway)
        {
            _swayT += Time.deltaTime / swayDuration;
            float angle = Mathf.Sin(_swayT * Mathf.PI * 2f) * swayAngle;
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    (float x, float y) Evaluate(float t)
    {
        for (int i = 0; i < _keyframes.Length - 1; i++)
        {
            var (t0, x0, y0) = _keyframes[i];
            var (t1, x1, y1) = _keyframes[i + 1];
            if (t < t0 || t > t1) continue;
            float p = (t - t0) / (t1 - t0);
            // SmoothStep en vez de Lerp: velocidad cero en cada keyframe, así los tramos
            // conectan sin quiebre en vez de cambiar de dirección de golpe (línea recta).
            return (Mathf.SmoothStep(x0, x1, p), Mathf.SmoothStep(y0, y1, p));
        }
        return (1f, 1f);
    }
}
