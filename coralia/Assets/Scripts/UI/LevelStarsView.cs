using System.Collections;
using Solo.MOST_IN_ONE;
using UnityEngine;

// Wrapper del prefab LevelStars: "Fields" son los 3 marcos de fondo (siempre visibles),
// "Stars" son las 3 estrellas doradas que se prenden según el puntaje — mismo patrón que
// LevelNodeView.stars en el mapa de niveles.
public class LevelStarsView : MonoBehaviour
{
    [SerializeField] GameObject[] stars; // Star1, Star2, Star3 — tamaño fijo 3

    [Header("Reveal en cadena (WinPanel)")]
    [SerializeField] float popDuration        = 0.3f;
    [SerializeField] float delayBetweenStars  = 0.15f;
    [SerializeField] AnimationCurve popCurve  = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.6f, 1.15f),
        new Keyframe(1f, 1f));

    [Header("Rotación al aparecer (opcional)")]
    [SerializeField] bool  spinOnReveal  = true; // combina con el pop de escala de arriba
    [SerializeField] float spinRotations = 2f;   // vueltas completas al principio, frena hasta 0°

    [Header("Sonido (opcional — clip pendiente)")]
    [SerializeField] AudioClip revealClip;    // uno por estrella — dejar vacío hasta tener el sonido
    [SerializeField] AudioClip allStarsClip;  // reemplaza a revealClip en la 3ra estrella si está asignado

    Vector3[] _baseScales;

    void Awake()
    {
        _baseScales = new Vector3[stars.Length];
        for (int i = 0; i < stars.Length; i++)
            _baseScales[i] = stars[i].transform.localScale;
    }

    // Instantáneo, sin animación — para casos donde no hace falta el reveal (ej. reset).
    public void SetStars(int count)
    {
        for (int i = 0; i < stars.Length; i++)
            stars[i].SetActive(i < count);
    }

    // Reveal en cadena 1, 2, 3 con un pop de escala — usado al abrir el WinPanel.
    public IEnumerator PlayStars(int count)
    {
        SetStars(0);
        for (int i = 0; i < count && i < stars.Length; i++)
        {
            yield return PopIn(i, isLast: i == stars.Length - 1);
            if (i < count - 1) yield return new WaitForSeconds(delayBetweenStars);
        }
    }

    IEnumerator PopIn(int index, bool isLast)
    {
        var go = stars[index];
        go.SetActive(true);
        var t = go.transform;

        // En la 3ra estrella, si hay un clip de "las 3 completas" asignado, ese reemplaza al
        // de estrella individual (no queremos los dos sonando pisados) — mismo criterio que
        // ProgressScoreView.
        bool skipRevealClip = isLast && allStarsClip != null;
        if (!skipRevealClip) AudioManager.Instance?.PlayUi(revealClip); // no hace nada si el clip está vacío (PlayOn ya lo maneja)
        if (isLast) AudioManager.Instance?.PlayUi(allStarsClip);
        if (SaveManager.Vibration) MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.LightImpact);

        float time = 0f;
        while (time < popDuration)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / popDuration);
            t.localScale = _baseScales[index] * popCurve.Evaluate(p);

            if (spinOnReveal)
            {
                // Ease-out cúbico: gira rápido al principio y frena hasta quedar derecha
                // justo cuando termina el pop — no es una vuelta a velocidad constante.
                float spinP = 1f - Mathf.Pow(1f - p, 3f);
                float angle = Mathf.Lerp(spinRotations * 360f, 0f, spinP);
                t.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            yield return null;
        }
        t.localScale = _baseScales[index];
        if (spinOnReveal) t.localRotation = Quaternion.identity;
    }
}
