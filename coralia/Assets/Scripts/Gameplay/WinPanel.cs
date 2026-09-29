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
    [Tooltip("Aviso de récord nuevo, bajo el puntaje. Opcional — sin esto el panel funciona igual, solo no se anuncia el récord.")]
    [SerializeField] TMP_Text       newRecordText;
    [SerializeField] AwardItem[]    awardItems;
    [SerializeField] Button         nextButton;
    [SerializeField] Button         closeButton;

    [Header("Sonido (opcional — dejar vacío hasta tener el clip)")]
    [SerializeField] AudioClip winClip;         // fanfarria de victoria, suena apenas se abre el panel — distinto de LevelStarsView.allStarsClip (remate de las 3 estrellas)
    [SerializeField, Range(0f, 1f)] float winClipVolume = 0.6f; // volumen propio, más bajo que un click de UI normal — a full sonaba "duro" (reportado por Diego)
    [SerializeField] AudioClip scoreTickClip;   // tick corto y repetido mientras el contador de score sube
    [SerializeField] AudioClip scoreCompleteClip; // remate al llegar al valor final — distinto del tick
    [SerializeField] AudioClip newRecordClip;     // solo al superar el récord anterior, después del remate de score
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

    int  _levelId;
    bool _countingScore;   // el auto-avance no se va hasta que el contador termine

    protected override void Awake()
    {
        base.Awake();
        nextButton.onClick.AddListener(GoToNextLevel);
        closeButton.onClick.AddListener(() => SceneLoader.GoTo(SceneLoader.LEVEL_MAP));
    }

    // isNewRecord lo calcula GameplayController, que es el único que puede leer el récord
    // anterior antes de guardar el nuevo.
    public void Show(int levelId, int score, int stars, List<(Sprite icon, int amount)> awards,
                     bool isNewRecord)
    {
        _levelId = levelId;
        ValidateReferences();

        if (levelBannerText) levelBannerText.text = LocaleManager.Get("ui.victory.level_banner").Replace("{id}", levelId.ToString());
        if (starsView)        starsView.SetStars(0);       // arranca en 0, se anima después de abrir
        if (scorePointsText) scorePointsText.text  = "0";  // ídem — el contador lo lleva a "score"
        ShowNewRecord(isNewRecord);

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
        // El contador arranca ya, en paralelo con la entrada de la card y con las estrellas. El
        // puntaje es lo primero que se busca al ganar, y hacerlo esperar dos animaciones antes
        // de mover el primer dígito se sentía como que el panel no reaccionaba.
        StartCoroutine(AnimateScoreCountUp(score));

        yield return new WaitForSeconds(OpenDuration);
        if (starsView) yield return starsView.PlayStars(stars);

        if (!nextButtonActive)
        {
            // El auto-avance sigue esperando al contador: irse del panel con el número a medio
            // subir sería peor que la espera que acabamos de sacar.
            while (_countingScore) yield return null;

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

    // Solo aparece al superar el récord. El resto del tiempo no hay nada que decir: ScoreLabel
    // ya rotula el número y está siempre visible.
    //
    // La primera victoria sí cuenta como récord: no hay partida anterior contra la cual perder,
    // así que ese puntaje es el mejor que existe para ese nivel.
    //
    // Sale junto con la card, sin esperar al contador de puntos: para cuando el panel se abre el
    // puntaje final ya está decidido, así que retenerlo no revelaba nada, solo hacía esperar.
    void ShowNewRecord(bool isNewRecord)
    {
        if (!newRecordText) return;

        newRecordText.gameObject.SetActive(isNewRecord);
        if (!isNewRecord) return;

        newRecordText.text = LocaleManager.Get("ui.victory.subtitle.new_record");

        AudioManager.Instance?.PlayUi(newRecordClip);
        if (SaveManager.Vibration) MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.Success);
    }

    IEnumerator AnimateScoreCountUp(int target)
    {
        if (!scorePointsText) yield break;

        _countingScore = true;

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
        _countingScore = false;
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
