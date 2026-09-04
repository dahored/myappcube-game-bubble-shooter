using System.Collections;
using System.Collections.Generic;
using Solo.MOST_IN_ONE;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Panel de victoria: banner "Nivel {id}", estrellas según el score final (vs.
// LevelData.star_thresholds — GDD §4.2), puntaje, y hasta N recompensas (monedas —
// GDD §6.3-6.4). GameplayController calcula todo (score/estrellas/recompensas) y lo pasa a
// Show() — este panel solo presenta. Sin buyButton/adButton, eso es exclusivo de LosePanel.
// El texto "¡Completado!" es fijo (no depende de datos del nivel) — va con LocalizedText.cs
// directo en el GameObject Content/CompleteLevel/Text, no se setea acá.
public class WinPanel : UIPanel
{
    [SerializeField] TMP_Text       levelBannerText;
    [SerializeField] LevelStarsView starsView;
    [SerializeField] TMP_Text       scorePointsText;
    [SerializeField] AwardItem[]    awardItems;
    [SerializeField] Button         nextButton;
    [SerializeField] Button         closeButton;

    [Header("Sonido (opcional — dejar vacío hasta tener el clip)")]
    [SerializeField] AudioClip winClip;         // fanfarria de victoria, suena apenas se abre el panel — distinto de LevelStarsView.allStarsClip (remate de las 3 estrellas)
    [SerializeField, Range(0f, 1f)] float winClipVolume = 0.6f; // volumen propio, más bajo que un click de UI normal — a full sonaba "duro" (reportado por Diego)
    [SerializeField] AudioClip scoreTickClip;   // tick corto y repetido mientras el contador de score sube
    [SerializeField] AudioClip scoreCompleteClip; // remate al llegar al valor final — distinto del tick
    [SerializeField] float     scoreTickSoundCooldown = 0.06f; // mismo criterio que scoreCountHapticCooldown, tunable aparte

    [Header("Reveal de score (contador 0 -> score final)")]
    [SerializeField] float scoreCountMinDuration = 0.4f;
    [SerializeField] float scoreCountMaxDuration = 1.2f;
    [SerializeField] float scoreCountPerPoint    = 0.0012f; // segundos extra por punto, con tope arriba
    [SerializeField] float scoreCountHapticCooldown = 0.06f; // "tick" continuo mientras cuenta, no un pulso único

    [Header("Next Button — opcional")]
    [Tooltip("Activado: se muestra el botón Next, comportamiento actual sin cambios. Desactivado: el botón se oculta y, apenas termina el reveal, se espera 'Auto Advance Delay' y se avanza solo.")]
    [SerializeField] bool  nextButtonActive  = true;
    [SerializeField] float autoAdvanceDelay  = 2f;

    int _levelId;

    protected override void Awake()
    {
        base.Awake();
        nextButton.onClick.AddListener(GoToNextLevel);
        closeButton.onClick.AddListener(() => SceneLoader.GoTo(SceneLoader.LEVEL_MAP));
    }

    public void Show(int levelId, int score, int stars, List<(Sprite icon, int amount)> awards)
    {
        _levelId = levelId;
        ValidateReferences();

        if (levelBannerText) levelBannerText.text = LocaleManager.Get("ui.victory.level_banner").Replace("{id}", levelId.ToString());
        if (starsView)        starsView.SetStars(0);       // arranca en 0, se anima después de abrir
        if (scorePointsText) scorePointsText.text  = "0";  // ídem — el contador lo lleva a "score"

        for (int i = 0; i < awardItems.Length; i++)
        {
            bool has = awards != null && i < awards.Count;
            awardItems[i].gameObject.SetActive(has);
            if (has) awardItems[i].Set(awards[i].icon, awards[i].amount);
        }

        // Si no hay Next, tampoco hay salida manual con la X — el auto-avance se encarga solo,
        // sin que el jugador pueda interrumpir la secuencia (mismo criterio que la referencia).
        if (nextButton)  nextButton.gameObject.SetActive(nextButtonActive);
        if (closeButton) closeButton.gameObject.SetActive(nextButtonActive);

        Open();
        AudioManager.Instance?.StopMusic(); // corta la música de gameplay al ganar (pedido de Diego)
        AudioManager.Instance?.PlayUi(winClip, winClipVolume);
        StartCoroutine(PlayRevealSequence(score, stars));
    }

    // Espera a que la card termine de entrar (OpenDuration, heredado de UIPanel) y recién ahí
    // dispara las estrellas en cadena (1, 2, 3) seguidas del contador de score 0 -> final. Si
    // Next Button Active está apagado, no espera el tap — sigue sola después de una pausa.
    IEnumerator PlayRevealSequence(int score, int stars)
    {
        yield return new WaitForSeconds(OpenDuration);
        if (starsView) yield return starsView.PlayStars(stars);
        yield return AnimateScoreCountUp(score);

        if (!nextButtonActive)
        {
            yield return new WaitForSeconds(autoAdvanceDelay);
            OnAutoAdvance();
        }
    }

    // Se dispara solo cuando Next Button Active está apagado, después del reveal completo.
    // Sin panel de recompensas definido todavía (ver conversación con Diego) — por ahora
    // vuelve directo al mapa. Cuando exista ese panel, enganchar acá en vez del GoTo directo.
    void OnAutoAdvance()
    {
        SceneLoader.GoTo(SceneLoader.LEVEL_MAP);
    }

    IEnumerator AnimateScoreCountUp(int target)
    {
        if (!scorePointsText) yield break;

        float duration = Mathf.Clamp(target * scoreCountPerPoint, scoreCountMinDuration, scoreCountMaxDuration);
        float t = 0f;
        float tickTimer = 0f; // cooldown propio del sonido — PlaySfx no tiene throttle incorporado como GenerateWithCooldown
        while (t < duration)
        {
            t += Time.deltaTime;
            int value = Mathf.RoundToInt(Mathf.Lerp(0f, target, t / duration));
            scorePointsText.text = value.ToString("N0");
            // Continuo mientras dura el conteo — GenerateWithCooldown ya se auto-limita, así
            // que llamarlo cada frame no lo satura, se siente como un "tick" parejo.
            if (SaveManager.Vibration) MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.SoftImpact, scoreCountHapticCooldown);

            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f)
            {
                AudioManager.Instance?.PlaySfx(scoreTickClip);
                tickTimer = scoreTickSoundCooldown;
            }
            yield return null;
        }
        scorePointsText.text = target.ToString("N0");
        AudioManager.Instance?.PlaySfx(scoreCompleteClip);
    }

    void GoToNextLevel()
    {
        int nextId = _levelId + 1;
        if (LevelLoader.LoadById(nextId) != null)
        {
            PlayerPrefs.SetInt("selected_level", nextId);
            SceneLoader.GoTo(SceneLoader.GAMEPLAY);
        }
        else
        {
            SceneLoader.GoTo(SceneLoader.LEVEL_MAP); // todavía no hay más niveles cargados
        }
    }

    // Campos sin asignar en el Inspector no deben fallar en silencio (el texto se queda con
    // el placeholder escrito a mano, ej. "Title", sin ninguna pista de por qué) — se avisa acá
    // con el nombre exacto del campo que falta conectar.
    void ValidateReferences()
    {
        if (!levelBannerText) Debug.LogWarning("[WinPanel] Falta asignar 'Level Banner Text' en el Inspector.");
        if (!starsView)        Debug.LogWarning("[WinPanel] Falta asignar 'Stars View' en el Inspector.");
        if (!scorePointsText) Debug.LogWarning("[WinPanel] Falta asignar 'Score Points Text' en el Inspector.");
        // Award Items vacío es válido por ahora — la economía de recompensas todavía está
        // pendiente de definir (ver conversación), no es un error de asignación.
    }
}
