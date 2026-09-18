using System.Collections;
using TMPro;
using UnityEngine;

// El número que sale de una burbuja al reventar: cuánto valió ESA burbuja con la racha del
// momento (10 en el primer combo, 20 en el segundo…, 50 las que caen). Sube, se agranda de
// golpe y se desvanece. Se destruye solo.
//
// Va chico, del tamaño de la burbuja: en un combo de quince salen quince números, y a ese
// tamaño se separan solos en vez de taparse.
public class ScorePopup : MonoBehaviour
{
    [SerializeField] TMP_Text label;

    // Cuánto dura el número en pantalla. Las que caen duran más: aparecen a media caída, con
    // el derrumbe todavía en movimiento y la vista siguiendo a las burbujas, así que necesitan
    // más tiempo para leerse que un pop que sale quieto en su celda.
    public const float POP_LIFETIME  = 0.55f;
    public const float DROP_LIFETIME = 0.95f;

    // Los del remate de victoria duran bastante más: los disparos pasan en un segundo, así que
    // si el número se fuera al ritmo del pop no daría tiempo a leer cuánto sumó.
    public const float CELEBRATION_LIFETIME = 1.4f;

    const float RISE_DISTANCE = 70f;
    const float POP_DURATION  = 0.16f;  // el golpe de escala inicial
    const float POP_OVERSHOOT = 1.25f;
    const float FADE_START    = 0.45f;  // fracción de la vida a partir de la cual se apaga

    RectTransform _rt;

    // delay: el mismo escalonado que el pop de la burbuja, para que el número salga junto con
    // su explosión y no antes. lifetime: POP_LIFETIME o DROP_LIFETIME según de dónde venga.
    public void Play(int points, float delay, float lifetime)
    {
        _rt = (RectTransform)transform;

        if (!label) label = GetComponentInChildren<TMP_Text>(true);
        if (!label)
        {
            Debug.LogWarning("[ScorePopup] El prefab no tiene ningún TMP_Text — no hay número que mostrar.");
            Destroy(gameObject);
            return;
        }

        // Un TMP_Text viene con Raycast Target activado, y estos flotan por encima del AimArea:
        // durante un combo quince números seguidos se comían el puntero y apuntar quedaba
        // bloqueado hasta que se apagaban. No son interactivos, así que nunca deben recibirlo.
        label.raycastTarget = false;

        label.text = points.ToString();
        StartCoroutine(Rise(delay, lifetime));
    }

    IEnumerator Rise(float delay, float lifetime)
    {
        // Escala 0 mientras espera su turno: activar el objeto y que se quede quieto y visible
        // un cuarto de segundo antes de moverse delata el truco.
        transform.localScale = Vector3.zero;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        Vector2 origin  = _rt.anchoredPosition;
        Color   baseCol = label.color;
        float   t       = 0f;

        while (t < lifetime)
        {
            t += Time.deltaTime;
            float p = t / lifetime;

            _rt.anchoredPosition = origin + Vector2.up * (RISE_DISTANCE * p);

            // Overshoot corto al principio y después escala 1: el pop es lo que hace que el
            // número "salga" de la explosión en vez de aparecer ya puesto.
            float pop = t < POP_DURATION
                ? Mathf.Lerp(0f, POP_OVERSHOOT, t / POP_DURATION)
                : Mathf.Lerp(POP_OVERSHOOT, 1f, Mathf.Min(1f, (t - POP_DURATION) / POP_DURATION));
            transform.localScale = Vector3.one * pop;

            if (p > FADE_START)
                label.color = new Color(baseCol.r, baseCol.g, baseCol.b,
                                        1f - (p - FADE_START) / (1f - FADE_START));

            yield return null;
        }

        Destroy(gameObject);
    }
}
