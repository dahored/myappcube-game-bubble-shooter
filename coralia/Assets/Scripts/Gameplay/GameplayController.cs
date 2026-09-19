using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    [SerializeField] ClaimPanel        claimPanel; // confirmación al comprar más disparos en NoMoreMovesPanel
    [SerializeField] GridIntroPreview  gridIntro;  // barrido de lectura del tablero al empezar (opcional)

    [Header("Sin disparos")]
    [Tooltip("Si está desactivado, al quedarse sin disparos se salta la oferta de monedas y se muestra LosePanel directo.")]
    [SerializeField] bool enableNoMoreShotsOffer = true;

    [Header("Pausa")]
    [SerializeField] PausedPanel pausedPanel;
    [SerializeField] Button      openPausedButton;

    [Header("HUD")]
    [SerializeField] ProgressScoreView progressScore;
    [SerializeField] TMP_Text          totalLivesText;  // vidas actuales al arrancar el nivel (GameDataWrapper/TotalLives)
    [SerializeField] TMP_Text          levelNumberText; // "LV {id}" del nivel actual (GameDataWrapper/LevelNumberLabel)

    [Header("SFX (opcional — dejar vacío hasta tener los clips)")]
    [Header("Remate de victoria — los disparos que sobran se gastan en pantalla")]
    [Tooltip("Rango de distancia que recorre cada disparo automático antes de reventar. Varía entre los dos para que no revienten todos en el mismo punto.")]
    [SerializeField] float victoryShotMinDistance = 350f;
    [SerializeField] float victoryShotMaxDistance = 900f;
    [Tooltip("Cuánto se corre al azar hacia los lados el punto donde revienta cada disparo, en píxeles. Todos suben rectos; esto solo evita que exploten en la misma columna.")]
    [SerializeField] float victoryShotSpread      = 120f;
    [Tooltip("Cuántas burbujas salen como mucho. Si sobran más disparos, esas burbujas reparten el bonus de todas.")]
    [SerializeField] int   victoryMaxShots        = 10;

    [Tooltip("Cuánto dura cada disparo del remate.")]
    [SerializeField] float victoryShotDuration    = 0.12f;

    [SerializeField] AudioClip popClip;
    [SerializeField] AudioClip dropClip;

    const float END_LEVEL_DELAY      = 1.1f;  // espera a que terminen las animaciones de pop/drop antes de mostrar el panel
    const float WIN_PANEL_PAUSE      = 0.35f; // respiro tras llenarse la barra, para que se vea la última estrella
    const float PROGRESS_BAR_TIMEOUT = 3f;    // tope de espera de esa barra, para no colgar el fin de nivel
    const float POP_CHAIN_DELAY      = 0.08f; // segundos entre el pop de cada burbuja del match, en cadena
    const float DROP_CHAIN_DELAY     = 0.05f; // ídem para las que caen — algo más rápido, son más
    const float CHAIN_TOTAL_MAX      = 0.7f;  // cuánto puede durar la cadena entera, por larga que sea

    // Separación entre un elemento de la cadena y el siguiente. Con pocas burbujas usa el paso
    // natural; con muchas lo comprime para que la cadena entre en CHAIN_TOTAL_MAX.
    //
    // La clave es que NINGUNA comparte instante con otra: un combo de veinte explota veinte veces
    // seguidas, muy rápido, en vez de cinco veces y un golpe.
    static float ChainStep(int count, float naturalStep) =>
        count > 1 ? Mathf.Min(naturalStep, CHAIN_TOTAL_MAX / (count - 1)) : 0f;

    // La fórmula vive en ScoreRules, compartida con LevelDifficultyEstimator. Si cambia, hay
    // que recalcular star_thresholds en TODOS los niveles: Coralia -> Recalcular umbrales de
    // estrellas.

    LevelData  _level;
    int        _shotsRemaining;
    int        _bubblesPopped;
    int        _bubblesDropped;
    int        _popScore;     // acumulado disparo a disparo: cada burbuja valió según la racha de SU momento
    int        _victoryBonus;  // el bonus ya topado — lo reparte el remate de disparos
    int        _comboStreak;  // racha actual de disparos consecutivos con match
    Vector2Int _creatureCell = new(-1, -1);
    bool       _creatureFreed;
    bool       _levelEnded;
    bool       _isFirstAttempt; // nunca se había intentado este nivel antes de esta partida (issue #49)
    int        _noMoreShotsUsedCount; // cuántas veces ya se pagó la oferta en esta partida — sube el costo
    bool       _endingPending;        // hay un cierre de nivel esperando a que terminen las animaciones
    bool       _isEditorTest;         // se entró desde el editor de niveles: nada de esta partida se guarda

    // La pone el editor de niveles justo antes de entrar a Play. No es una constante de SaveManager
    // porque no es progreso: es una marca de un solo uso entre el editor y esta escena.
    public const string EDITOR_TEST_KEY = "level_editor_test";

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
        if (!SaveManager.HasLivesAvailable)
        {
            // Ya se muestra acá — limpiar la bandera para que LevelMapController no lo
            // repita apenas GoToLevelMap() aterrice ahí (ver SaveManager.NotifyOutOfLivesOnMapLoad).
            SaveManager.NotifyOutOfLivesOnMapLoad = false;

            // cannon.Update() corre igual aunque Init() nunca se haya llamado (_inputEnabled
            // arranca en true por default) — sin esto, el timer de inactividad del tutorial
            // seguía corriendo detrás del panel y terminaba mostrando "Hold to aim" mientras
            // el nivel ni siquiera está armado (reportado por Diego). No hace falta
            // rehabilitarlo después: este camino siempre termina en GoToLevelMap o en recargar
            // la escena entera (RetryLoad), nunca en seguir jugando esta misma sesión.
            cannon.SetInputEnabled(false);

            if (outOfLivesPanel != null)
            {
                outOfLivesPanel.OnCloseButtonPressed += GoToLevelMap;
                outOfLivesPanel.OnResolved           += RetryLoad; // compró un refill acá mismo — recién ahora hay vidas para armar el nivel
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

        // Probar un nivel desde el editor no puede tocar la partida. Ganar el 60 para ver cómo
        // quedó dejaba desbloqueados los 60 niveles de golpe, y no hay forma de volver atrás.
        //
        // La marca se consume acá: vale para esta partida y para ninguna más. Si el jugador vuelve
        // al mapa y entra a un nivel normalmente, ya no está.
        _isEditorTest = PlayerPrefs.GetInt(EDITOR_TEST_KEY, 0) == 1;
        if (_isEditorTest)
        {
            PlayerPrefs.DeleteKey(EDITOR_TEST_KEY);
            PlayerPrefs.Save();
            Debug.Log($"[GameplayController] Nivel {levelId} en modo prueba: no se guarda progreso.");
        }

        // Leer ANTES de marcar — si nunca se intentó este nivel, esta partida es el intento
        // #1 (condición para el nodo dorado, ver SaveManager.RecordLevelWin en EndLevel()).
        _isFirstAttempt = !SaveManager.HasAttemptedLevel(levelId);
        if (!_isEditorTest) SaveManager.MarkLevelAttempted(levelId);

        if (_level.objective != null && _level.objective.type == "rescue" && _level.objective.creature_position?.Count == 2)
        {
            // creature_position es [fila, col] (ver LevelData.cs) -> Vector2Int(col, fila)
            _creatureCell = new Vector2Int(_level.objective.creature_position[1], _level.objective.creature_position[0]);
        }

        // Se setean una sola vez acá — a diferencia de LivesPillView (Level Map), acá no hace
        // falta refrescar en vivo cada segundo: las vidas no cambian a mitad de una partida
        // (perder la última siempre termina en un panel que corta la escena), y el nivel
        // tampoco cambia sin recargar. Sin timer de regen porque en pleno juego no aplica.
        // Con el boost de vidas infinitas activo, el número no significa nada: perder no cuesta
        // vidas (ver SaveManager.LoseLife), así que mostrar "4" es engañoso. Mismo criterio que
        // LivesPillView en el Level Map, que ahí lo resuelve con SetBadgeInfinite().
        if (totalLivesText)
            totalLivesText.text = SaveManager.IsInfiniteLivesActive ? "∞" : SaveManager.Lives.ToString();
        if (levelNumberText) levelNumberText.text = $"LV {_level.id}";

        grid.SpawnFromLevel(_level);
        _shotsRemaining = _level.max_shots; // antes de cannon.Init() — necesita el conteo ya listo para el primer RefreshPreview
        cannon.Init(_level.available_colors, _shotsRemaining);
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

        RefreshShotsLabel();

        StartCoroutine(OpenLevel());
    }

    // La entrada al nivel, antes del primer disparo (issue #74). Dos cosas encadenadas:
    //
    //   1. Si el tablero es tan largo que sus primeras filas nacen fuera de cuadro, el nivel abre
    //      con la vista puesta abajo, espera y sube. En un tablero que entra entero esto no pasa.
    //   2. La concha abre vacía y la primera burbuja aparece con destello y sonido, ya con el
    //      tablero en su sitio.
    //
    // Todo el primer bloque corre de forma SÍNCRONA, todavía dentro de Start(): StartCoroutine
    // ejecuta el cuerpo hasta el primer yield sin esperar al frame siguiente. Eso importa porque
    // es lo que hace que el primer frame dibujado ya salga con la vista abajo y la recámara vacía;
    // si se colocaran después, se vería un instante el nivel armado y recién ahí el salto.
    //
    // Mientras dura no se puede apuntar, disparar ni pausar: la vista está en movimiento y todavía
    // no hay burbuja que disparar. El botón de pausa se apaga en vez de dejarse andando porque
    // cualquier toque adelanta la subida — sin esto, el toque que abre la pausa dispararía las dos
    // cosas a la vez y el orden entre ellas no está definido.
    IEnumerator OpenLevel()
    {
        bool preview = gridIntro != null && gridIntro.Begin();

        cannon.HideCurrentForReveal();
        cannon.SetInputEnabled(false);
        if (openPausedButton) openPausedButton.interactable = false;

        if (preview) yield return gridIntro.Play();
        yield return cannon.RevealCurrent();

        if (openPausedButton) openPausedButton.interactable = true;
        cannon.SetInputEnabled(true);
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
        if (totalLivesText   == null) Debug.LogWarning("[GameplayController] Falta asignar 'Total Lives Text' en el Inspector — el HUD no va a mostrar las vidas actuales.");
        if (levelNumberText  == null) Debug.LogWarning("[GameplayController] Falta asignar 'Level Number Text' en el Inspector — el HUD no va a mostrar el número de nivel.");
        if (claimPanel       == null) Debug.LogWarning("[GameplayController] Falta asignar 'Claim Panel' en el Inspector — comprar disparos extra no va a mostrar confirmación.");
        if (gridIntro        == null) Debug.LogWarning("[GameplayController] Falta asignar 'Grid Intro' en el Inspector — los niveles largos van a empezar sin el barrido de vista previa del tablero.");

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

    // El guard de entrada de Start() cortó ANTES de armar el nivel — recargar la escena entera
    // es más simple y seguro que intentar "continuar" Start() a mitad de camino desde acá.
    void RetryLoad() => SceneLoader.GoTo(SceneLoader.GAMEPLAY);

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

        int shotsBonus = noMoreShotsPanel.ShotsBonus;
        SaveManager.Coins -= cost;
        _noMoreShotsUsedCount++;
        _shotsRemaining += shotsBonus;
        _levelEnded      = false;
        RefreshShotsLabel();
        cannon.SetShotsRemaining(_shotsRemaining); // el "next" pudo estar oculto por quedarse sin disparos — vuelve a mostrarse con el bonus
        noMoreShotsPanel.Close();

        // El input se re-habilita recién cuando ClaimPanel se cierra (no apenas se cierra
        // NoMoreMovesPanel) — así no se puede disparar mientras la confirmación sigue abierta.
        if (claimPanel)
        {
            claimPanel.ShowShots(shotsBonus);
            claimPanel.OnClosed += HandleClaimClosed;

            void HandleClaimClosed()
            {
                claimPanel.OnClosed -= HandleClaimClosed;
                cannon.SetInputEnabled(true);
            }
        }
        else
        {
            cannon.SetInputEnabled(true); // sin ClaimPanel asignado, no bloquear al jugador por un campo sin wirear
        }
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
        if (!_isEditorTest) SaveManager.LoseLife(); // probar un nivel no cuesta vidas
        if (losePanel != null) losePanel.Open();
        else Debug.LogWarning("[GameplayController] LosePanel no está asignado.");
    }

    void OnBubbleLanded(Vector2Int landedCell)
    {
        if (_levelEnded) return;

        _shotsRemaining--;
        RefreshShotsLabel();
        // CannonController tiene su propia copia del conteo (para current/next) — sin este
        // aviso se desincroniza y sigue mostrando el "next" aunque ya no queden disparos para
        // usarlo. Versión "silently" (sin refrescar la preview ya mismo): ResolveImpact() está
        // a mitad de camino resolviendo este mismo disparo y va a lanzar su propia animación
        // (AnimateNextIntoCurrent) apenas termine — usar SetShotsRemaining acá revelaba la
        // bola de destino de golpe ANTES de que el clon viajero llegara, rompiendo el efecto
        // de "llegada" (reportado por Diego).
        cannon.UpdateShotsRemainingSilently(_shotsRemaining);

        var removed = ResolveMatchAndDrop(landedCell);
        if (removed.Contains(_creatureCell)) _creatureFreed = true;

        if (progressScore != null) progressScore.SetScore(LiveScore, _level.star_thresholds);

        CheckWinLose();
    }

    // Match (3+ conectadas) -> explode, después drop de todo lo que quedó flotando.
    // Devuelve el set de celdas removidas (para chequear el objetivo rescue).
    HashSet<Vector2Int> ResolveMatchAndDrop(Vector2Int landedCell)
    {
        var removed = new HashSet<Vector2Int>();

        // Antes del flood-fill: si lo que aterrizó fue una arcoíris, acá elige de qué color se
        // vuelve. Si no, sería la semilla de un match que se lleva el grid completo.
        grid.ResolveRainbow(landedCell);

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
        // El intervalo se COMPRIME en los combos grandes en vez de toparse. Antes era
        // Min(i * paso, tope): pasadas las cinco o seis primeras, todas quedaban con el mismo
        // retraso y reventaban a la vez — junto con su vibración, que se sentía como un golpe
        // único en lugar de una cadena.
        float popStep = ChainStep(matched.Count, POP_CHAIN_DELAY);

        // Lo que vale CADA burbuja de este disparo: sube con la racha. Se calcula una vez acá
        // porque es igual para todas las del match — lo que cambia entre disparos, no dentro.
        int popValue = ScoreRules.PopValue(_comboStreak);

        for (int i = 0; i < matched.Count; i++)
        {
            var cell = matched[i];
            if (grid.TryGetBubble(cell, out var view))
                view.PlayPopAnimation(i * popStep, popClip);
            grid.SpawnScorePopup(cell, popValue, i * popStep, ScorePopup.POP_LIFETIME);
            grid.RemoveBubble(cell);
            removed.Add(cell);
        }
        _bubblesPopped += matched.Count;

        // Las que caen, de abajo hacia arriba y también escalonadas: es un desmoronamiento, y
        // soltarlas todas en el mismo frame lo convertía en un bloque que baja de una pieza.
        var floating  = grid.FindUnreachableFromCeiling();
        var collapsing = floating.OrderByDescending(cell => cell.y).ToList();
        float dropStep = ChainStep(collapsing.Count, DROP_CHAIN_DELAY);

        for (int i = 0; i < collapsing.Count; i++)
        {
            var cell = collapsing[i];
            if (grid.TryGetBubble(cell, out var view)) view.PlayDropAnimation(dropClip, i * dropStep);
            // Las que caen valen fijo, sin racha: el número distinto es justamente lo que hace
            // ver que se ganaron de otra forma. GridController lo pone a la altura del cañón,
            // cuando la burbuja terminó de caer.
            grid.SpawnDropScorePopup(cell, ScoreRules.POINTS_PER_DROP, i * dropStep);
            grid.RemoveBubble(cell);
            removed.Add(cell);
        }
        _bubblesDropped += floating.Count;

        // Se acumula acá y no se calcula al final multiplicando el total de burbujas: para
        // entonces la racha de cada disparo ya no se sabe. Y usa el MISMO popValue que se
        // mostró en pantalla, así lo que sumó el marcador es exactamente lo que se vio.
        _popScore += matched.Count * popValue;

        int chainSize = matched.Count + floating.Count;

        // Un golpe de temblor por burbuja, al ritmo de sus pops. Antes era una sola sacudida
        // disparada acá, o sea cuando las burbujas todavía no habían empezado a explotar.
        if (grid.ShakeWorthIt(chainSize))
            StartCoroutine(ShakeAlongChain(matched.Count, popStep, collapsing.Count, dropStep));

        return removed;
    }

    // Acompaña la cadena con un golpe de temblor por burbuja, con los mismos tiempos que se le
    // pasaron a las animaciones. Los pops primero y las caídas después, porque así es como
    // ocurren en pantalla.
    //
    // La energía del temblor se acumula y decae sola (GridController.ShakePulse), así que esto no
    // produce sacudidas sueltas: mientras los pops llegan más rápido de lo que la energía baja, la
    // pantalla tiembla de forma sostenida y se apaga cuando termina la cadena.
    IEnumerator ShakeAlongChain(int popCount, float popStep, int dropCount, float dropStep)
    {
        for (int i = 0; i < popCount; i++)
        {
            grid.ShakePulse();
            if (popStep > 0f) yield return new WaitForSeconds(popStep);
        }

        for (int i = 0; i < dropCount; i++)
        {
            grid.ShakePulse();
            if (dropStep > 0f) yield return new WaitForSeconds(dropStep);
        }
    }

    void CheckWinLose()
    {
        // El cierre no es inmediato: EndLevelAfterAnimations espera a que terminen los pops. En
        // ese rato se puede llegar acá otra vez y encolar un segundo cierre — y lo peor no es el
        // duplicado, es que si el jugador COMPRA disparos mientras uno está pendiente, esa
        // corrutina vieja igual va a terminar y cerrar el nivel con los disparos recién pagados.
        if (_levelEnded || _endingPending) return;

        bool objectiveMet = _level.objective.type == "rescue" ? _creatureFreed : grid.CellCount == 0;
        if (objectiveMet) { _endingPending = true; StartCoroutine(EndLevelAfterAnimations(true)); return; }

        if (_shotsRemaining <= 0)
        {
            _endingPending = true;
            StartCoroutine(EndLevelAfterAnimations(false));
        }
    }

    // El grid (el diccionario de celdas) ya está lógicamente vacío/definido apenas termina
    // ResolveMatchAndDrop, pero las animaciones de pop/drop siguen jugando unos frames más
    // por su cuenta — sin esta espera, el panel de victoria/derrota tapaba la animación.
    IEnumerator EndLevelAfterAnimations(bool won)
    {
        cannon.SetInputEnabled(false); // bloquea el input ya mismo, no hace falta esperar
        yield return new WaitForSeconds(END_LEVEL_DELAY);

        _endingPending = false;
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
        if (won && !_isEditorTest && _level.id >= SaveManager.MaxUnlockedLevel)
        {
            SaveManager.MaxUnlockedLevel = _level.id + 1;
            SaveManager.JustAdvancedFromLevelId = _level.id;
        }

        if (won)
        {
            if (winPanel == null) { Debug.LogWarning("[GameplayController] WinPanel no está asignado."); return; }
            // El bonus no se cobra entero: solo lo que quepa sin saltarse un escalón de estrella
            // que no se ganó jugando (ver ScoreRules.AllowedBonus).
            _victoryBonus = ScoreRules.AllowedBonus(LiveScore,
                                                    ScoreRules.RemainingShotsScore(_shotsRemaining),
                                                    _level.star_thresholds);
            int score  = LiveScore + _victoryBonus;

            // Con el total, bonus incluido: los disparos que sobran también cuentan para las
            // estrellas. El umbral ya los tiene en cuenta (LevelDifficultyEstimator.RealisticScore
            // los suma contra los disparos realistas), así que no regalan estrellas — dan el
            // margen que cubre los disparos fallados.
            int stars  = CalculateStars(score);

            // Leído ANTES de guardar: después de RecordLevelScore el récord ya es este puntaje.
            // La primera victoria también cuenta como récord (previousBest == 0): no hay partida
            // anterior contra la cual perder, así que cualquier puntaje es el mejor hasta ahora.
            int  previousBest = SaveManager.GetLevelBestScore(_level.id);
            bool newRecord    = score > previousBest;

            // El panel igual se muestra con sus estrellas y su puntaje: probar un nivel sirve
            // justamente para ver cómo queda eso. Lo que no pasa es que quede registrado.
            if (!_isEditorTest)
            {
                SaveManager.RecordLevelWin(_level.id, stars, _isFirstAttempt);
                SaveManager.RecordLevelScore(_level.id, score);
            }

            var awards = CalculateAwards(firstCompletion);

            StartCoroutine(PlayVictorySequence(score,
                () => winPanel.Show(_level.id, score, stars, awards, newRecord)));
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

    IEnumerator PlayVictorySequence(int finalScore, System.Action show)
    {
        yield return SpendRemainingShots();
        yield return ShowWinAfterProgressBar(finalScore, show);
    }

    // Los disparos que sobran no desaparecen con el nivel: se disparan solos, uno por uno, y
    // cada uno revienta mostrando el bonus ACUMULADO (1000, 2000, 3000…). El número que crece
    // es lo que hace ver que suman; uno que repitiera "1000" cinco veces no diría nada.
    //
    // No toca _shotsRemaining: el puntaje final ya se calculó con él y restarlo acá lo cambiaría
    // a mitad de la animación. El contador del HUD baja aparte, solo para la vista.
    IEnumerator SpendRemainingShots()
    {
        if (cannon == null || grid == null || _shotsRemaining <= 0) yield break;

        cannon.SetInputEnabled(false);
        cannon.PrepareCelebrationColors();

        // Se disparan como mucho victoryMaxShots burbujas, aunque sobren cuarenta. Las que salen
        // reparten el bonus COMPLETO entre ellas, así que con veinte sobrantes cada burbuja vale
        // por cuatro y el número final es el mismo.
        //
        // Así el remate dura siempre lo mismo sin tener que repartir un presupuesto: cada disparo
        // conserva su duración y se ve, en vez de acelerarse hasta el parpadeo cuando sobran
        // muchos.
        int   shots  = Mathf.Min(_shotsRemaining, Mathf.Max(1, victoryMaxShots));
        float flight = Mathf.Max(0.02f, victoryShotDuration);

        for (int i = 1; i <= shots; i++)
        {
            // El bonus ya topado, repartido parejo entre las burbujas que salen. Se calcula sobre
            // el total y no sumando de a poco para que el último número caiga exacto en
            // _victoryBonus, sin arrastrar el error del redondeo.
            int bonus = Mathf.RoundToInt(_victoryBonus * i / (float)shots);

            // Capturado en una local: el callback corre dentro de la corrutina, cuando el bucle
            // ya podría haber avanzado.
            int shown = bonus;

            // El contador baja hasta 0 en el mismo número de pasos: con veinte sobrantes y cinco
            // burbujas va 16, 12, 8, 4, 0 en vez de quedarse en 15.
            if (shotsLabel)
                shotsLabel.text = (_shotsRemaining - Mathf.RoundToInt(_shotsRemaining * i / (float)shots)).ToString();

            yield return cannon.PlayCelebrationShot(Random.Range(-victoryShotSpread, victoryShotSpread),
                victoryShotMinDistance,
                victoryShotMaxDistance, flight, popClip,
                burst => grid.SpawnScorePopupAt(burst, shown, 0f, ScorePopup.CELEBRATION_LIFETIME));

            // La barra sube con cada disparo: el bonus cuenta para las estrellas, así que este
            // es el momento en que una tercera estrella que faltaba se puede encender.
            if (progressScore != null)
                progressScore.SetScore(LiveScore + bonus, _level.star_thresholds);
        }
    }

    // Cierra la barra con el puntaje final, bonus incluido, y espera a que termine de llenarse.
    //
    // Si el panel se abriera en el mismo frame, ese último tramo pasaría entero detrás del modal
    // y el jugador se enteraría de su tercera estrella al ver el resultado, sin haberla visto
    // ganarse.
    IEnumerator ShowWinAfterProgressBar(int finalScore, System.Action show)
    {
        if (progressScore != null)
        {
            progressScore.SetScore(finalScore, _level.star_thresholds);

            yield return null; // un frame para que arranquen el llenado y los pops

            // Con tope: si la barra no llegara nunca a su destino (umbrales vacíos, el
            // componente desactivado a mitad de camino), el panel de victoria no aparecería y
            // el nivel quedaría colgado sin nada en pantalla. Perder la animación es barato;
            // dejar al jugador encerrado en un nivel terminado, no.
            float waited = 0f;
            while (progressScore.IsAnimating && waited < PROGRESS_BAR_TIMEOUT)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(WIN_PANEL_PAUSE);
        }

        show();
    }

    // Score sin el bonus de disparos sobrantes — ese solo se conoce al terminar el nivel
    // (depende de cuántos disparos quedaron), así que mostrarlo en vivo en ProgressScoreView
    // haría que la barra subiera/bajara de forma rara mientras se juega. El salto final se ve
    // en el contador animado de WinPanel, no acá.
    int LiveScore => _popScore + ScoreRules.DropScore(_bubblesDropped);

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
