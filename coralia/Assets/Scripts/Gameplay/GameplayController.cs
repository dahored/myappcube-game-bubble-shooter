using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Coordinador de escena — NO es un singleton DontDestroyOnLoad (a diferencia de
// AudioManager/LocaleManager), vive y muere con la escena Gameplay.
public class GameplayController : MonoBehaviour
{
    [SerializeField] GridController    grid;
    [SerializeField] CannonController  cannon;
    [SerializeField] WinPanel          winPanel;
    [SerializeField] NoMoreMovesPanel  noMoreShotsPanel;
    [SerializeField] LosePanel         losePanel;
    [SerializeField] OutOfLivesPanel   outOfLivesPanel; // guard de entrada — ver Start() (issue #52)
    [SerializeField] TMP_Text          shotsLabel;

    [Header("Sin disparos")]
    [Tooltip("Si está desactivado, al quedarse sin disparos se salta la oferta de monedas y se muestra LosePanel directo.")]
    [SerializeField] bool enableNoMoreShotsOffer = true;

    [Header("Pausa")]
    [SerializeField] PausedPanel pausedPanel;
    [SerializeField] Button      openPausedButton;

    [Header("HUD")]
    [SerializeField] ProgressScoreView progressScore;

    [Header("SFX (opcional — dejar vacío hasta tener los clips)")]
    [SerializeField] AudioClip popClip;
    [SerializeField] AudioClip dropClip;

    const float END_LEVEL_DELAY      = 1.1f;  // espera a que terminen las animaciones de pop/drop antes de mostrar el panel
    const float POP_CHAIN_DELAY      = 0.08f; // segundos entre el pop de cada burbuja del match, en cadena
    const float POP_CHAIN_DELAY_MAX  = 0.4f;   // tope — para que un match gigante no tarde una eternidad en terminar

    // GDD §4.2 / docs/04_Plan_Fase1_Coralia.md — fórmula de score, escalada x1000 (pedido de
    // Diego: el score final de un nivel debe sentirse "alto", en el orden de cientos de miles,
    // no de cientos) — mantiene las mismas proporciones relativas entre pop/drop/disparo
    // sobrante que el GDD original, solo con más ceros. Si esto cambia, hay que reescalar
    // también star_thresholds en TODOS los niveles (Resources/Levels/Chapter_N/*.json) y
    // actualizar la nota equivalente en .claude/skills/level-designer/SKILL.md.
    const int SCORE_PER_POP            = 10_000;
    const int SCORE_PER_DROP           = 15_000;
    const int SCORE_PER_REMAINING_SHOT = 10_000;

    // El GDD menciona "combos largos suben score multiplicador" pero sin definir el valor
    // (queda anotado como pendiente) — estos dos valores son una propuesta propia, fáciles
    // de retocar si el balance no se siente bien:
    // - Cadena: bonus por el TAMAÑO del match+drop de un mismo disparo, por cada burbuja
    //   que pasa del mínimo de 3 (un match de 3 no da bonus, uno de 8 sí).
    // - Combo: bonus por RACHA de disparos consecutivos que matchean sin fallar ninguno.
    const int CHAIN_BONUS_PER_EXTRA_BUBBLE = 1_000;
    const int COMBO_BONUS_PER_STREAK       = 2_000;

    LevelData  _level;
    int        _shotsRemaining;
    int        _bubblesPopped;
    int        _bubblesDropped;
    int        _comboBonus;   // acumulado durante todo el nivel
    int        _comboStreak;  // racha actual de disparos consecutivos con match
    Vector2Int _creatureCell = new(-1, -1);
    bool       _creatureFreed;
    bool       _levelEnded;
    bool       _isFirstAttempt; // nunca se había intentado este nivel antes de esta partida (issue #49)
    int        _noMoreShotsUsedCount; // cuántas veces ya se pagó la oferta en esta partida — sube el costo

    void Start()
    {
        // Por si se entra a esta escena directo (sin pasar por Splash, donde se activa
        // normalmente) — así las transiciones animadas funcionan igual al testear.
        SceneTransition.Enabled = true;

        // Música propia de gameplay, más calma que la del lobby (Home/LevelMap) — sin esto,
        // seguiría sonando la del lobby porque AudioManager persiste entre escenas.
        AudioManager.Instance?.PlayGameplayMusic();

        if (!ValidateReferences()) return;

        // Guard de entrada — cubre reintentar (ResetProgressPanel) quedándose sin vidas en
        // el proceso, y cualquier otro camino que llegue a esta escena sin pasar por el
        // chequeo de LevelMapController (issue #52). No arma el nivel si no hay vidas.
        // Acá, a diferencia del Level Map, cerrar el panel no puede dejar solo "esconderlo"
        // — no hay nivel armado detrás — así que su X manda de vuelta al mapa.
        if (SaveManager.Lives <= 0)
        {
            // Ya se muestra acá — limpiar la bandera para que LevelMapController no lo
            // repita apenas GoToLevelMap() aterrice ahí (ver SaveManager.NotifyOutOfLivesOnMapLoad).
            SaveManager.NotifyOutOfLivesOnMapLoad = false;

            if (outOfLivesPanel != null)
            {
                outOfLivesPanel.OnClosed += GoToLevelMap;
                outOfLivesPanel.Open();
            }
            else Debug.LogWarning("[GameplayController] Falta asignar 'Out Of Lives Panel' en el Inspector.");
            return;
        }

        int levelId = PlayerPrefs.GetInt("selected_level", 1);
        _level = LevelLoader.LoadById(levelId);
        if (_level == null)
        {
            Debug.LogError($"[GameplayController] No se encontró el nivel {levelId}");
            return;
        }

        // Leer ANTES de marcar — si nunca se intentó este nivel, esta partida es el intento
        // #1 (condición para el nodo dorado, ver SaveManager.RecordLevelWin en EndLevel()).
        _isFirstAttempt = !SaveManager.HasAttemptedLevel(levelId);
        SaveManager.MarkLevelAttempted(levelId);

        if (_level.objective != null && _level.objective.type == "rescue" && _level.objective.creature_position?.Count == 2)
        {
            // creature_position es [fila, col] (ver LevelData.cs) -> Vector2Int(col, fila)
            _creatureCell = new Vector2Int(_level.objective.creature_position[1], _level.objective.creature_position[0]);
        }

        grid.SpawnFromLevel(_level);
        cannon.Init(_level.available_colors, _level.rainbow_chance);
        cannon.OnBubbleLanded += OnBubbleLanded;

        // noMoreShotsPanel puede no estar asignado todavía (WIP) — ya se avisó en
        // ValidateReferences(), acá solo evita el crash si falta.
        if (noMoreShotsPanel != null)
        {
            noMoreShotsPanel.OnContinuePressed += OnContinuePressed;
            noMoreShotsPanel.OnDeclinedPressed += OnDeclined;
        }

        if (openPausedButton != null) openPausedButton.onClick.AddListener(OpenPaused);
        if (pausedPanel      != null) pausedPanel.OnResumePressed += OnResumePressed;

        _shotsRemaining = _level.max_shots;
        RefreshShotsLabel();
    }

    // Referencias core (grid/cannon): sin ellas no hay nivel que jugar, se aborta Start().
    // Referencias de presentación (paneles/HUD, todavía WIP en varios casos): se avisa con
    // un LogWarning específico pero el juego sigue — cada uso individual ya tiene su propio
    // null-check más abajo, así que faltar una no debe tirar NullReferenceException en medio
    // de una partida.
    bool ValidateReferences()
    {
        bool ok = true;
        if (grid   == null) { Debug.LogWarning("[GameplayController] Falta asignar 'Grid' en el Inspector.");   ok = false; }
        if (cannon == null) { Debug.LogWarning("[GameplayController] Falta asignar 'Cannon' en el Inspector."); ok = false; }

        if (winPanel         == null) Debug.LogWarning("[GameplayController] Falta asignar 'Win Panel' en el Inspector — la victoria no va a mostrar panel todavía.");
        if (noMoreShotsPanel == null) Debug.LogWarning("[GameplayController] Falta asignar 'No More Shots Panel' en el Inspector — la oferta de seguir jugando no va a mostrar panel todavía.");
        if (losePanel        == null) Debug.LogWarning("[GameplayController] Falta asignar 'Lose Panel' en el Inspector — la derrota no va a mostrar panel todavía.");
        if (shotsLabel       == null) Debug.LogWarning("[GameplayController] Falta asignar 'Shots Label' en el Inspector — el HUD de disparos no se va a actualizar.");

        if (pausedPanel      == null) Debug.LogWarning("[GameplayController] Falta asignar 'Paused Panel' en el Inspector — el botón de pausa no va a hacer nada todavía.");
        if (openPausedButton == null) Debug.LogWarning("[GameplayController] Falta asignar 'Open Paused Button' en el Inspector.");
        if (progressScore    == null) Debug.LogWarning("[GameplayController] Falta asignar 'Progress Score' en el Inspector — la barra de score no se va a actualizar.");

        return ok;
    }

    void OnDestroy()
    {
        if (cannon != null) cannon.OnBubbleLanded -= OnBubbleLanded;
        if (noMoreShotsPanel != null)
        {
            noMoreShotsPanel.OnContinuePressed -= OnContinuePressed;
            noMoreShotsPanel.OnDeclinedPressed -= OnDeclined;
        }
        if (pausedPanel != null) pausedPanel.OnResumePressed -= OnResumePressed;
        if (outOfLivesPanel != null) outOfLivesPanel.OnClosed -= GoToLevelMap;
    }

    void GoToLevelMap() => SceneLoader.GoTo(SceneLoader.LEVEL_MAP);

    // Pausa: solo bloquea input y congela el disparo en vuelo (si había uno) — el grid y
    // el HUD quedan tal cual se ven detrás del panel, no hace falta Time.timeScale.
    void OpenPaused()
    {
        if (_levelEnded) return; // no tiene sentido pausar sobre un panel de victoria/derrota ya abierto
        cannon.SetInputEnabled(false);
        if (pausedPanel != null) pausedPanel.Open();
    }

    void OnResumePressed() => cannon.SetInputEnabled(true);

    // GDD §7 — "Pagar" (con monedas, ver comentario en NoMoreMovesPanel): +N disparos
    // (ver NoMoreMovesPanel.ShotsBonus/GetCost), la ronda sigue (no cuenta como nuevo
    // intento). El costo sube cada vez que se vuelve a usar en la misma partida.
    void OnContinuePressed()
    {
        int cost = noMoreShotsPanel.GetCost(_noMoreShotsUsedCount);
        if (SaveManager.Coins < cost) return; // el botón ya debería estar deshabilitado, defensivo

        SaveManager.Coins -= cost;
        _noMoreShotsUsedCount++;
        _shotsRemaining += noMoreShotsPanel.ShotsBonus;
        _levelEnded      = false;
        RefreshShotsLabel();
        cannon.SetInputEnabled(true);
        noMoreShotsPanel.Close();
    }

    // El jugador cerró la oferta de NoMoreMovesPanel sin pagar -> derrota real.
    void OnDeclined()
    {
        noMoreShotsPanel.Close();
        ShowRealLoss();
    }

    // Derrota real: -1 vida (GDD §7) y se muestra LosePanel, que ya maneja su propia
    // navegación (retry/mapa). Se llama tanto al declinar la oferta como, si
    // enableNoMoreShotsOffer está apagado, directo al quedarse sin disparos.
    void ShowRealLoss()
    {
        SaveManager.LoseLife();
        if (losePanel != null) losePanel.Open();
        else Debug.LogWarning("[GameplayController] LosePanel no está asignado.");
    }

    void OnBubbleLanded(Vector2Int landedCell)
    {
        if (_levelEnded) return;

        _shotsRemaining--;
        RefreshShotsLabel();

        var removed = ResolveMatchAndDrop(landedCell);
        if (removed.Contains(_creatureCell)) _creatureFreed = true;

        // El grid ya refleja el estado final de este disparo (match + drop aplicados) —
        // recién acá tiene sentido decidir si hay que retirarlo del cañón o si puede volver
        // a bajar (ver GridController.RecomputeScroll).
        grid.RecomputeScroll();

        if (progressScore != null) progressScore.SetScore(LiveScore, _level.star_thresholds);

        CheckWinLose();
    }

    // Match (3+ conectadas) -> explode, después drop de todo lo que quedó flotando.
    // Devuelve el set de celdas removidas (para chequear el objetivo rescue).
    HashSet<Vector2Int> ResolveMatchAndDrop(Vector2Int landedCell)
    {
        var removed = new HashSet<Vector2Int>();

        var matched = grid.FindConnectedSameColor(landedCell);
        if (matched.Count < 3)
        {
            _comboStreak = 0; // el disparo no matcheó nada — corta la racha de combo
            return removed;
        }

        _comboStreak++;

        // FindConnectedSameColor devuelve las celdas en orden de flood-fill (BFS) desde la
        // burbuja que tocó el disparo — así que el índice ya es "qué tan lejos" está cada
        // una, y alcanza con escalonar el pop según ese orden para que explote en cadena
        // en vez de todas juntas. El estado lógico del grid (RemoveBubble) sigue siendo
        // instantáneo — solo la animación visual del pop se demora.
        for (int i = 0; i < matched.Count; i++)
        {
            var cell = matched[i];
            if (grid.TryGetBubble(cell, out var view))
                view.PlayPopAnimation(Mathf.Min(i * POP_CHAIN_DELAY, POP_CHAIN_DELAY_MAX), popClip);
            grid.RemoveBubble(cell);
            removed.Add(cell);
        }
        _bubblesPopped += matched.Count;

        var floating = grid.FindUnreachableFromCeiling();
        foreach (var cell in floating)
        {
            if (grid.TryGetBubble(cell, out var view)) view.PlayDropAnimation(dropClip);
            grid.RemoveBubble(cell);
            removed.Add(cell);
        }
        _bubblesDropped += floating.Count;

        // Cadena: bonus por cuánto pasó este disparo del mínimo de 3 (match + todo lo que
        // cayó con él). Combo: bonus FIJO por disparo mientras la racha siga viva (no
        // multiplicado por el largo de la racha) — la versión anterior multiplicaba por
        // _comboStreak en cada disparo, lo que acumulaba en forma cuadrática con rachas
        // largas y hacía que el score total se disparara muy por encima del millón sin
        // querer (reportado por Diego). Así el combo suma parejo, disparo a disparo.
        int chainSize  = matched.Count + floating.Count;
        int chainBonus = Mathf.Max(0, chainSize - 3) * CHAIN_BONUS_PER_EXTRA_BUBBLE;
        int comboBonus = _comboStreak >= 2 ? COMBO_BONUS_PER_STREAK : 0;
        _comboBonus += chainBonus + comboBonus;

        grid.Shake(chainSize); // no hace nada si no llega al umbral — ver GridController.shakeThreshold

        return removed;
    }

    void CheckWinLose()
    {
        bool objectiveMet = _level.objective.type == "rescue" ? _creatureFreed : grid.CellCount == 0;
        if (objectiveMet) { StartCoroutine(EndLevelAfterAnimations(true)); return; }
        if (_shotsRemaining <= 0) StartCoroutine(EndLevelAfterAnimations(false));
    }

    // El grid (el diccionario de celdas) ya está lógicamente vacío/definido apenas termina
    // ResolveMatchAndDrop, pero las animaciones de pop/drop siguen jugando unos frames más
    // por su cuenta — sin esta espera, el panel de victoria/derrota tapaba la animación.
    IEnumerator EndLevelAfterAnimations(bool won)
    {
        cannon.SetInputEnabled(false); // bloquea el input ya mismo, no hace falta esperar
        yield return new WaitForSeconds(END_LEVEL_DELAY);
        EndLevel(won);
    }

    void EndLevel(bool won)
    {
        _levelEnded = true;

        // "Primera vez" = nunca se había GANADO este nivel antes (issue #49) — reemplaza el
        // heurístico viejo basado en MaxUnlockedLevel, que quedaba poco confiable mientras
        // el avance de nivel siguiera TEMP-desactivado más abajo (daba true en cada repetición).
        bool firstCompletion = won && !SaveManager.HasCompletedLevel(_level.id);

        // Reactivado — Diego ya está probando la progresión real entre niveles. Este mismo
        // "avance real" es la condición para la animación de retorno al mapa (issue #55).
        if (won && _level.id >= SaveManager.MaxUnlockedLevel)
        {
            SaveManager.MaxUnlockedLevel = _level.id + 1;
            SaveManager.JustAdvancedFromLevelId = _level.id;
        }

        if (won)
        {
            if (winPanel == null) { Debug.LogWarning("[GameplayController] WinPanel no está asignado."); return; }
            int score  = LiveScore + _shotsRemaining * SCORE_PER_REMAINING_SHOT;
            int stars  = CalculateStars(score);
            SaveManager.RecordLevelWin(_level.id, stars, _isFirstAttempt);
            var awards = CalculateAwards(firstCompletion);
            winPanel.Show(_level.id, score, stars, awards);
        }
        else if (enableNoMoreShotsOffer)
        {
            if (noMoreShotsPanel == null) { Debug.LogWarning("[GameplayController] NoMoreMovesPanel no está asignado."); return; }
            noMoreShotsPanel.Show(_noMoreShotsUsedCount);
        }
        else
        {
            ShowRealLoss();
        }
    }

    // Score sin el bonus de disparos sobrantes — ese solo se conoce al terminar el nivel
    // (depende de cuántos disparos quedaron), así que mostrarlo en vivo en ProgressScoreView
    // haría que la barra subiera/bajara de forma rara mientras se juega. El salto final se ve
    // en el contador animado de WinPanel, no acá.
    int LiveScore => _bubblesPopped * SCORE_PER_POP + _bubblesDropped * SCORE_PER_DROP + _comboBonus;

    // GDD §4.2 — estrellas según star_thresholds del nivel (calibrados a mano por playtesting,
    // no por fórmula). 0 a 3 estrellas.
    int CalculateStars(int score)
    {
        int stars = 0;
        if (_level.star_thresholds != null)
            foreach (var threshold in _level.star_thresholds)
                if (score >= threshold) stars++;
        return Mathf.Clamp(stars, 0, 3);
    }

    // GDD §6.3-6.4 — monedas base por capítulo (cap.1: 50, cap.2-3: 75, cap.4-6: 100), +50%
    // en la primera completación, más 1-3 de bonus (solo primera vez). El GDD original
    // separaba este bonus en una moneda "gemas" aparte, pero el juego nunca tuvo una
    // segunda moneda real — todo se unificó en monedas (ver SaveManager.Coins), así que
    // el bonus de primera vez es simplemente parte del mismo total, no un award aparte.
    // El GDD §6.3 sí especifica monedas por completar nivel (50/75/100 según capítulo, +50%
    // primera vez) — pero eso asumía DOS monedas separadas (monedas = soft, se gana jugando;
    // gemas = premium, paga NoMoreMovesPanel). Al consolidar gemas→monedas (nunca hubo arte
    // para una segunda moneda) se fusionaron sin querer los dos roles: la misma moneda ahora
    // llovía por ganar Y era la que debía trabar la oferta de "seguir jugando", sin presión de
    // monetización real. Decisión con Diego: sacar el award de completar nivel — las monedas
    // van a venir de otros lados cuando se implementen (santuario, daily rewards, misiones,
    // logros — GDD §6.3), no de la sola acción de pasar un nivel.
    List<(Sprite icon, int amount)> CalculateAwards(bool firstCompletion)
    {
        return new List<(Sprite, int)>();
    }

    void RefreshShotsLabel()
    {
        if (shotsLabel) shotsLabel.text = _shotsRemaining.ToString();
    }
}
