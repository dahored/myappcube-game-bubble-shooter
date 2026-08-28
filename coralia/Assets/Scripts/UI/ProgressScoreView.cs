using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Barra de progreso de score EN VIVO durante el gameplay: se llena a medida que el score
// sube y "popea" cada estrella al cruzar su threshold — mismo criterio que
// GameplayController.CalculateStars (contra LevelData.star_thresholds), así la barra nunca
// puede mostrar algo distinto de lo que WinPanel va a mostrar al final.
//
// "Fields" (los 3 marcos de fondo) no los toca este script — ya están posicionados a mano en
// el Editor en los mismos % que la fórmula de umbrales (40/65/90, ver level-designer SKILL),
// así que no dependen de datos en runtime.
public class ProgressScoreView : MonoBehaviour
{
    [SerializeField] Image        fillImage; // Image Type = Filled (Horizontal)
    [SerializeField] GameObject[] stars;     // Star1, Star2, Star3 — mismo orden que star_thresholds

    [Header("Animación de llenado")]
    [SerializeField] float fillSpeed = 1.5f; // fillAmount/seg

    [Header("Pop de estrella (mismo estilo que LevelStarsView)")]
    [SerializeField] float popDuration = 0.3f;
    [SerializeField] AnimationCurve popCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.6f, 1.15f), new Keyframe(1f, 1f));

    [Header("Sonido (opcional — dejar vacío hasta tener los clips)")]
    [SerializeField] AudioClip starClip;     // se reproduce en CADA estrella que se completa
    [SerializeField] AudioClip allStarsClip; // se reproduce cuando se completan las 3

    // El 3er umbral (3 estrellas) es el 90% del "score ideal" con el que se calculan los
    // star_thresholds — ver .claude/skills/level-designer/SKILL.md. Reconstruir el 100% de
    // la barra a partir de ese dato (en vez de usar el 3er umbral como el 100%) es lo que
    // hace que las 3 estrellas caigan exactas en 40/65/90% del ancho, donde están los Fields
    // puestos a mano — si ese % cambia algún día, hay que actualizarlo acá también.
    const float STAR_3_RATIO_OF_BAR = 0.90f;

    Vector3[] _baseScales;
    float     _targetFill;
    int       _starsEarned;
    Coroutine _fillRoutine;

    void Awake()
    {
        ValidateReferences();

        _baseScales = new Vector3[stars.Length];
        for (int i = 0; i < stars.Length; i++)
        {
            if (!stars[i]) continue;
            _baseScales[i] = stars[i].transform.localScale;
            stars[i].transform.localScale = Vector3.zero; // arrancan "apagadas"
        }
        if (fillImage) fillImage.fillAmount = 0f;
    }

    // Llamar cada vez que el score en vivo cambie (ej. después de cada disparo resuelto).
    // starThresholds es la misma lista de LevelData — el último valor es el 100% de la barra.
    public void SetScore(int score, System.Collections.Generic.List<int> starThresholds)
    {
        if (!fillImage || starThresholds == null || starThresholds.Count == 0) return;

        float barMax = starThresholds[starThresholds.Count - 1] / STAR_3_RATIO_OF_BAR;
        _targetFill  = barMax > 0f ? Mathf.Clamp01(score / barMax) : 0f;

        if (_fillRoutine != null) StopCoroutine(_fillRoutine);
        _fillRoutine = StartCoroutine(AnimateFill());

        // Los umbrales son crecientes — en cuanto el score no alcanza uno, no puede alcanzar
        // los siguientes, así que cortamos ahí. _starsEarned como punto de partida evita
        // re-popear una estrella ya ganada si SetScore se llama de nuevo más tarde.
        for (int i = _starsEarned; i < starThresholds.Count && i < stars.Length; i++)
        {
            if (score < starThresholds[i]) break;
            _starsEarned = i + 1;
            StartCoroutine(PopStar(i, isLast: i == stars.Length - 1));
        }
    }

    IEnumerator AnimateFill()
    {
        while (!Mathf.Approximately(fillImage.fillAmount, _targetFill))
        {
            fillImage.fillAmount = Mathf.MoveTowards(fillImage.fillAmount, _targetFill, fillSpeed * Time.deltaTime);
            yield return null;
        }
    }

    IEnumerator PopStar(int index, bool isLast)
    {
        if (!stars[index]) yield break;

        AudioManager.Instance?.PlayUi(starClip);

        var t = stars[index].transform;
        float time = 0f;
        while (time < popDuration)
        {
            time += Time.deltaTime;
            t.localScale = _baseScales[index] * popCurve.Evaluate(time / popDuration);
            yield return null;
        }
        t.localScale = _baseScales[index];

        if (isLast) AudioManager.Instance?.PlayUi(allStarsClip);
    }

    void ValidateReferences()
    {
        if (!fillImage) Debug.LogWarning("[ProgressScoreView] Falta asignar 'Fill Image' en el Inspector.");
        if (stars == null || stars.Length == 0) Debug.LogWarning("[ProgressScoreView] Falta asignar el array 'Stars' en el Inspector.");
    }
}
