using System.Collections;
using UnityEngine;

// Flotar suave arriba y abajo, en bucle. Para las criaturas del fondo: quietas se leen como
// calcomanías, y con esto parecen suspendidas en el agua.
//
// Sirve igual en UI que en el mundo. La misma criatura aparece en Home como parte de la pantalla
// y en el arrecife como decoración, y un componente por cada caso obligaría a acordarse de cuál
// va en cada sitio — que es la clase de detalle que falla en silencio.
//
// La diferencia real es solo la unidad: un RectTransform se mueve en píxeles del lienzo y un
// Transform normal en unidades de mundo, donde 12 sería enorme. Por eso la amplitud se interpreta
// distinta según dónde esté, en vez de pedir dos campos que casi siempre estarían de más.
public class FloatAnimation : MonoBehaviour
{
    [SerializeField] float amplitude   = 12f;  // en UI son píxeles; en el mundo, unidades
    [SerializeField] float frequency   = 0.6f; // velocidad del ciclo
    [SerializeField] float phaseOffset = 0f;   // desfase para que no floten todas igual
    [SerializeField] float startDelay  = 0f;   // espera antes de empezar

    [Tooltip("Sortea el desfase en vez de usar el de arriba, para que varias copias no arranquen en el mismo punto de la onda.")]
    [SerializeField] bool randomPhase;

    [Tooltip("Cuánto varía la velocidad entre una copia y otra, en fracción. Es lo que de verdad las separa: con solo el desfase van todas al mismo ritmo, mantienen la distancia entre ellas para siempre y se siguen leyendo como un único movimiento.")]
    [SerializeField, Range(0f, 0.5f)] float frequencyVariation;

    [Tooltip("Cuánto varía el recorrido entre una copia y otra, en fracción. Cuatro criaturas que suben exactamente lo mismo se ven copiadas aunque no vayan sincronizadas.")]
    [SerializeField, Range(0f, 0.5f)] float amplitudeVariation;

    [Tooltip("Cuánto vale la amplitud cuando la criatura NO es de UI. Las unidades de mundo son mucho más grandes que los píxeles: sin esto, una amplitud pensada para el lienzo mandaría al bicho fuera de pantalla.")]
    [SerializeField] float worldScale = 0.01f;

    RectTransform _rect;   // nulo si no es de UI
    Vector3       _origin;
    float         _startTime;
    bool          _active;

    // Los de ESTA copia. Se sortean una vez y se guardan: leyendo los del Inspector en cada
    // frame, el azar volvería a tirarse siempre y el movimiento iría a saltos.
    float         _frequency;
    float         _amplitude;

    void Awake() => _rect = transform as RectTransform;

    void Start() => StartCoroutine(DelayedStart());

    IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(startDelay);

        // El origen se toma DESPUÉS de la espera a propósito: quien coloca la pieza puede seguir
        // moviéndola durante esos primeros frames, y guardarlo antes la haría flotar alrededor de
        // un sitio en el que ya no está.
        _origin    = _rect ? _rect.anchoredPosition : transform.localPosition;
        _startTime = Time.time;
        _active    = true;

        // El desfase va en ciclos, así que de 0 a 1 cubre la onda entera. Se sortea acá y no como
        // una espera: retrasando el arranque, la criatura se queda quieta un rato y se nota; con
        // el desfase, empieza a moverse ya pero en otro punto de la subida.
        if (randomPhase) phaseOffset = Random.value;

        _frequency = frequency * (1f + Random.Range(-frequencyVariation, frequencyVariation));
        _amplitude = amplitude * (1f + Random.Range(-amplitudeVariation, amplitudeVariation));
    }

    void Update()
    {
        if (!_active) return;

        float elapsed = Time.time - _startTime;

        // Arranca desde cero en vez de saltar a media onda: sin la rampa, la criatura da un tirón
        // en su primer frame visible.
        float ramp = Mathf.Clamp01(elapsed / 0.4f);

        float y    = Mathf.Sin((elapsed * _frequency + phaseOffset) * Mathf.PI * 2f)
                     * _amplitude * ramp * (_rect ? 1f : worldScale);

        if (_rect) _rect.anchoredPosition = (Vector2)_origin + new Vector2(0f, y);
        else       transform.localPosition = _origin + new Vector3(0f, y, 0f);
    }
}
