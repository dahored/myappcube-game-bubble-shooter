using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Solo.MOST_IN_ONE;
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
    [SerializeField] GridIntroPreview  gridIntro;  // vista previa del tablero al empezar (opcional)

    [Tooltip("Respiro entre que la transición destapa la pantalla y que empieza la entrada al nivel. Sin esto la animación arranca en el mismo frame en que se termina de ver el telón, y se lee como si ya hubiera empezado.")]
    [SerializeField] float openBeat = 0.25f;

    [Header("Sin disparos")]
    [Tooltip("Si está desactivado, al quedarse sin disparos se salta la oferta de monedas y se muestra LosePanel directo.")]
    [SerializeField] bool enableNoMoreShotsOffer = true;

    [Header("Pausa")]
    [SerializeField] PausedPanel pausedPanel;

    [Tooltip("El panel que explica una mecánica la primera vez (issue #58). Opcional: sin él el nivel arranca igual, solo que nadie explica nada.")]
    [SerializeField] TutorialPanel tutorialPanel;
    [SerializeField] Button      openPausedButton;

    [Header("HUD")]
    [SerializeField] ProgressScoreView progressScore;

    [Tooltip("El hub de objetivos del HUD: qué hay que conseguir en este nivel y cuánto falta. Opcional — sin él el nivel se juega igual, pero el jugador no ve qué le piden.")]
    [SerializeField] ObjectiveHub objectiveHub;

    [Tooltip("El destello al reventar una burbuja de rescate. Es el momento que el jugador vino a buscar, así que se separa del pop corriente: sin él, liberar una criatura se ve igual que reventar una burbuja cualquiera.\n\nMismo bloque que usan los poderes al estallar. Con 'Glow Size' en CERO usa el del estallido de la Bomba.")]
    [SerializeField] Sparkle.Settings rescueSparkle = new();

    [Tooltip("Lo que tarda lo rescatado en volar de su burbuja al hub, en segundos. El contador baja CUANDO LLEGA, así que esto es también lo que el número tarda en reaccionar.\n\nEn CERO usa el valor por defecto.")]
    [Range(0f, 2f)]
    [SerializeField] float rescueFlight = 0.55f;

    [Tooltip("Cuánto se curva el vuelo, en tanto por uno de lo que recorre. El desvío va PERPENDICULAR al camino, así que curva igual suba recto o vaya en diagonal.\n\nEn CERO va derecho, que se lee como una interfaz moviendo un dato en vez de como algo que escapa.")]
    [Range(0f, 0.8f)]
    [SerializeField] float rescueBend = 0.3f;

    [Tooltip("Cada cuánto suelta una chispa por el camino, en segundos. Es lo que convierte el vuelo en un rastro en vez de un icono que se desliza.\n\nCuanto más corto, más seguida sale la cola: si se espacia demasiado deja de ser un cometa y se ve como las cuentas de un collar.\n\nEn CERO usa el valor por defecto.")]
    [Range(0f, 0.2f)]
    [SerializeField] float rescueTrail = 0.012f;

    [Tooltip("Lo grande que sale cada chispa de la estela, respecto a una burbuja. Hace falta más de lo que parece: la chispa de cuatro puntas gasta casi todo su ancho en brazos finos, y lo que de verdad brilla es el centro.\n\nCada una sale además con su propia variación, para que no se vean estampadas con el mismo sello.\n\nEn CERO usa el valor por defecto.")]
    [Range(0f, 3f)]
    [SerializeField] float rescueTrailSize = 1.6f;

    [Tooltip("Cuánto aguanta encendida cada chispa antes de EMPEZAR a apagarse, en segundos. Es lo que decide si la estela se queda puesta mientras el icono viaja o si se deshace detrás de él.\n\nDespués de esto todavía tarda en irse del todo: el apagado va aparte y es siempre el mismo.\n\nEn CERO usa el valor por defecto.")]
    [Range(0f, 2f)]
    [SerializeField] float rescueTrailHold = 0.7f;

    [Tooltip("De qué color es la estela. Con alfa en CERO usa blanco.")]
    [SerializeField] Color rescueTrailColor = Color.white;

    [Tooltip("El icono del objetivo 'limpia el tablero', donde lo que se cuenta son burbujas y no criaturas. Vacío usa el sprite del primer color del nivel.")]
    [SerializeField] Sprite clearAllIcon;
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

    [Header("Bomba de Coral (booster)")]
    [Tooltip("El destello de la explosión, en el centro del impacto. Es lo único que distingue a la bomba de un match grande antes de que las burbujas empiecen a reventar.")]
    [SerializeField] Sparkle.Settings bombSparkle = new();

    [Tooltip("El fogonazo de pantalla y la onda expansiva. El destello avisa de que pasó algo grande mirara donde mirara el jugador; la onda dice dónde y hasta dónde llegó.")]
    [SerializeField] BombBlast.Settings bombBlast = new();

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
    // maxTotal: cuánto puede durar la cadena entera. Por defecto el tope de los pops; el contagio
    // de un comodín pasa el suyo, que es más largo a propósito — ahí la cadena ES el efecto, no el
    // acompañamiento de la explosión.
    static float ChainStep(int count, float naturalStep, float maxTotal = CHAIN_TOTAL_MAX) =>
        count > 1 ? Mathf.Min(naturalStep, maxTotal / (count - 1)) : 0f;

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
    // El id con el que TutorialPanel conoce la explicación del rescate. Constante y no un string
    // suelto porque el mismo valor tiene que estar escrito igual en el Inspector del panel.
    const string TUTORIAL_RESCUE = "rescue";

    // El booster que acaba de presentarse y hay que dejar cargado en cuanto arranque el nivel.
    Booster _autoLoadBooster;

    // Qué hay que rescatar y cuánto queda de cada cosa, en el mismo orden que los huecos del hub.
    //
    // La celda guarda a QUÉ objetivo pertenece y no solo que hay una criatura: con tres criaturas
    // en el tablero, al reventar una hay que saber cuál de los tres contadores baja. Se arma al
    // abrir y no se vuelve a tocar — las burbujas no cambian de celda, se van.
    readonly List<string>       _targets   = new();
    readonly List<int>          _remaining = new();

    // Lo que el hub ENSEÑA ahora mismo, que va por detrás de _remaining a propósito.
    //
    // La cuenta de verdad baja al instante porque de ella depende si el nivel está ganado; el
    // número de pantalla baja burbuja a burbuja, al ritmo de los pops. Con los dos juntos, un
    // match de doce restaba doce de golpe mientras las burbujas todavía estaban reventando.
    readonly List<int> _shown = new();
    readonly Dictionary<Vector2Int, int> _creatureAt = new();

    // Y con qué receta se montó cada una, para su estallido: cuando el vuelo arranca la burbuja
    // ya reventó, así que no se le puede preguntar a ella.
    readonly Dictionary<Vector2Int, RescueBubbleCatalog.Recipe> _recipeAt = new();
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

        // Una tirada por partida. Lo que decide es a cuál de las burbujas igual de solas van las
        // hijas de la Madre: sin esto, repetir el nivel las mandaba siempre al mismo sitio.
        BoosterRules.Variety = Random.Range(1, int.MaxValue);
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

        // Después de poblar el grid: el objetivo cuenta lo que de verdad está en el tablero, no
        // lo que el JSON dice que debería estar.
        SetUpObjective();
        _shotsRemaining = _level.max_shots; // antes de cannon.Init() — necesita el conteo ya listo para el primer RefreshPreview
        cannon.Init(_level.available_colors, _shotsRemaining);
        cannon.OnBubbleLanded += OnBubbleLanded;

        ApplyStartBoosters();

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

        // Desde acá hay partida abierta. Si la app muere antes de que termine, al próximo arranque
        // se cobra como abandono (SaveManager.SettleAbandonedLevel). Probar un nivel desde el
        // editor no cuenta, igual que no cuesta vida ni guarda progreso.
        if (!_isEditorTest) SaveManager.MarkLevelInProgress();

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

        yield return WaitForCurtain();

        if (preview) yield return gridIntro.Play();
        yield return cannon.RevealCurrent();
        yield return ExplainMechanics();

        // Y ahora sí, lo que cada poder de inicio tiene que ENSEÑAR, uno detrás de otro.
        //
        // Acá y no al consumirlos, que es donde estaba: ahí no se había dibujado ni el primer
        // frame, la vista previa movía el tablero por encima y el modal del tutorial tapaba la
        // pantalla. El efecto ocurría de verdad, pero a nadie — y un poder que se paga y no se ve
        // pasar se lee como que no funcionó (reportado por Diego).
        //
        // Antes de soltar el control a propósito: mientras se ve lo que uno compró no se dispara.
        yield return PlayStartEffects();

        if (openPausedButton) openPausedButton.interactable = true;
        cannon.SetInputEnabled(true);

        // El booster recién presentado llega puesto en la recámara: el jugador acaba de leer qué
        // hace y lo primero que ve al cerrar el modal es la bomba lista para disparar. Dejarlo en
        // el inventario sería pedirle que encuentre el ícono del HUD justo cuando todavía no sabe
        // que existe. Va DESPUÉS de soltar el control porque LoadBooster lo exige, y así además
        // trae su destello y su sonido en vez de aparecer en silencio.
        if (_autoLoadBooster != Booster.None)
        {
            var booster = _autoLoadBooster;
            _autoLoadBooster = Booster.None;

            // Uno que se usa señalando el tablero no cabe en la recámara: no tiene burbuja, así
            // que LoadBooster lo rechazaba avisando de unos 'Bubble Frames' que nunca va a tener
            // (reportado por Diego). Lo equivalente es abrirle el modo de elegir, que es
            // exactamente lo mismo que el jugador acaba de ver en el tutorial.
            if (BoosterRules.PicksOnBoard(booster)) BeginPick(booster);
            else                                    cannon.LoadBooster(booster);
        }
    }

    // Los boosters de inicio que el jugador trajo equipados (GDD §3.2). Se gastan acá, al abrir el
    // nivel, y no al disparar como los de gameplay: su efecto ya quedó puesto y no hay un momento
    // posterior en el que el jugador decida nada.
    //
    // Se desequipan además de consumirse. El loadout se limpia al abrir la pantalla previa, pero
    // reiniciar el nivel desde dentro no pasa por ahí — sin esto, cada reintento volvería a
    // aplicar un booster que ya no está en el inventario.
    void ApplyStartBoosters()
    {
        // Sobre una copia: desequipar modifica la misma lista que se está recorriendo.
        foreach (var booster in new List<Booster>(BoosterLoadout.Equipped))
        {
            if (!BoosterRules.IsStart(booster)) continue;
            if (!SaveManager.ConsumeBooster(booster)) continue;

            // A la cola de lo que hay que enseñar, y NADA se aplica todavía.
            //
            // Ni siquiera la perla de la recámara, que antes se convertía acá mismo: el jugador
            // veía la burbuja ya arcoíris mientras leía el tutorial, y cuando por fin salía el
            // icono anunciando el poder, lo que anunciaba ya había pasado (reportado por Diego).
            //
            // Un poder de inicio se consume al abrir el nivel, pero OCURRE cuando su icono llega
            // a donde va a actuar. Lo de antes es esperar.
            _startEffects.Add(booster);

            BoosterLoadout.Toggle(booster);
        }
    }

    // Sumar tiros al límite del nivel. El conteo vive en dos sitios —acá, que es quien decide
    // cuándo se acabó el nivel, y en el cañón, que lo usa para saber si todavía hay 'next' que
    // enseñar— así que los dos tienen que enterarse o la recámara se queda vacía con tiros de
    // sobra. Mismo camino que usa comprar disparos extra con gemas.
    void AddShots(int extra)
    {
        if (extra <= 0) return;

        _shotsRemaining += extra;
        RefreshShotsLabel();
        cannon.SetShotsRemaining(_shotsRemaining);
    }

    // La otra mitad de la Perla: convierte burbujas YA PUESTAS en comodines, escalonadas y con su
    // sonido, para que el jugador vea lo que acaba de pagar.
    //
    // Las de la cola (CannonController._rainbowsLeft) llegan de a una a lo largo de la partida y
    // no se anuncian; estas se ven todas al abrir y se pueden planear, que es lo que las hace
    // valer. Las dos cosas a la vez son el poder entero (pedido de Diego).
    // Los de inicio que se aplicaron en este nivel, en el orden en que se aplicaron.
    readonly List<Booster> _startEffects = new();

    [Header("Poderes de inicio — cómo se enseñan al abrir")]
    [Tooltip("La pausa antes de enseñar CADA poder de inicio. La primera separa el regalo del modal del tutorial, que si no arranca encima de él y parece parte del mismo golpe; las siguientes separan un poder del anterior.\n\nEn CERO usa el valor por defecto, no 'sin pausa'.")]
    [Range(0f, 2f)]
    [SerializeField] float startEffectDelay = DEFAULT_START_DELAY;

    [Tooltip("Lo que tarda el contador de disparos en subir del todo, sume 5 o sume 20. Es un tiempo FIJO y no un paso por unidad: lo que el jugador tiene que leer es 'me están dando disparos', y con un paso fijo un regalo grande se haría eterno.\n\nEn CERO usa el valor por defecto, no 'instantáneo'.")]
    [SerializeField] float shotsGainDuration = DEFAULT_SHOTS_GAIN;

    [Tooltip("El sonido de CADA disparo que entra. Suena una vez por unidad, así que es el que convierte el número en una cuenta. Opcional.")]
    [SerializeField] AudioClip shotsGainClip;

    [Tooltip("Cuánto crece el número en cada golpe, de 0 a 1. Es lo que lo despega del HUD mientras sube: sin esto el número cambia pero no mira nadie.\n\nEn CERO usa el valor por defecto, no 'sin golpe'.")]
    [Range(0f, 1f)]
    [SerializeField] float shotsGainPulse = 0.35f;

    [Tooltip("Lo grande que sale el icono del poder en medio de la pantalla, en píxeles.\n\nEn CERO usa el valor por defecto.")]
    [Range(0f, 600f)]
    [SerializeField] float startIntroScale = DEFAULT_INTRO_SIZE;

    [Tooltip("Lo que tarda el icono en aparecer: crece de nada, se pasa un poco de su tamaño y vuelve. El rebote es lo que lo hace aterrizar en vez de encenderse.\n\nEn CERO usa el valor por defecto.")]
    [Range(0f, 1.5f)]
    [SerializeField] float startIntroPop = DEFAULT_INTRO_POP;

    [Tooltip("El sonido del icono al aparecer en el centro. Es el golpe que anuncia el regalo, igual para todos los poderes de inicio; lo que cada uno hace después tiene sus propios sonidos. Opcional.")]
    [SerializeField] AudioClip startIntroClip;

    [Tooltip("Marcado: cuando el jugador trae varios poderes de inicio, salen TODOS a la vez en el centro —uno solo centrado, dos lado a lado, tres en triángulo— y se aplican juntos.\n\nDesmarcado: se presentan de a uno, cada uno con su entrada y su efecto antes de pasar al siguiente. Se entiende mejor pero con tres equipados se hace largo.")]
    [SerializeField] bool startIntroTogether;

    [Tooltip("Lo separados que salen los iconos entre sí en el modo 'todos a la vez', en proporción a su tamaño.")]
    [Range(0.5f, 2f)]
    [SerializeField] float startIntroSpread = 0.72f;

    [Tooltip("Lo grandes que salen los iconos en el modo 'todos a la vez', respecto a 'Start Intro Scale'. Tres a tamaño completo no caben sin pisarse, así que acá se achican sin tocar el tamaño del modo de a uno.\n\nEn CERO usa el valor por defecto.")]
    [Range(0.3f, 1f)]
    [SerializeField] float startIntroTogetherScale = 0.75f;

    [Tooltip("Lo que se separan entre sí los efectos en el modo 'todos a la vez'. No es solo ritmo: van en el orden de BoosterRules.StartOrder, así que esta pausa es lo que garantiza que el poder que QUITA burbujas termine antes de que el que las siembra ponga nada.\n\nEn CERO usa el valor por defecto.")]
    [Range(0f, 1.5f)]
    [SerializeField] float startIntroTogetherStagger = DEFAULT_TOGETHER_STAGGER;

    [Tooltip("Marcado: debajo del icono sale el nombre del poder. Con cinco poderes de inicio, tres de ellos son objetos azules submarinos y el icono solo no los distingue — y el nombre es con el que el jugador los va a pedir después en la tienda.\n\nSe desvanece cuando el icono arranca hacia su destino: el nombre acompaña, no viaja.")]
    [SerializeField] bool startIntroShowName = true;

    [Tooltip("La separación entre el icono y su nombre, en píxeles.")]
    [Range(0f, 300f)]
    [SerializeField] float startIntroNameGap = 36f;

    [Tooltip("La fuente del nombre. Vacío usa la que el proyecto tenga por defecto en TextMeshPro.")]
    [SerializeField] TMP_FontAsset startIntroNameFont;

    [Tooltip("El cuerpo del nombre, en píxeles.")]
    [Range(10f, 160f)]
    [SerializeField] float startIntroNameSize = 52f;

    [SerializeField] Color startIntroNameColor = Color.white;

    [Tooltip("El velo que se pone detrás del icono para que destaque sobre el tablero. Se abre y se cierra con él.\n\nCon alfa en CERO usa el valor por defecto; para quitarlo de verdad, pon el color totalmente transparente con alfa mínimo.")]
    [SerializeField] Color startIntroVeil = new(0f, 0.05f, 0.12f, 0.55f);

    [Tooltip("El velo que oscurece el tablero mientras el jugador elige una burbuja (Punto Débil). Oscuro pero translúcido: lo que hay que ver es el tablero, y el velo solo dice que ahora el toque hace otra cosa.\n\nCon alfa en CERO usa el valor por defecto.")]
    [SerializeField] Color pickVeil = new(0f, 0.05f, 0.12f, 0.35f);

    [Tooltip("Lo que tarda el velo en aparecer y en irse, en segundos.\n\nEn CERO usa el valor por defecto.")]
    [Range(0f, 1f)]
    [SerializeField] float pickFade = 0.18f;

    [Tooltip("El cuerpo del aviso que explica qué hacer mientras se elige, en píxeles.")]
    [Range(10f, 120f)]
    [SerializeField] float pickHintSize = 44f;

    [Tooltip("El resplandor y las chispas que acompañan al icono al aparecer. Mismo bloque que usan los poderes al estallar.\n\nCon 'Glow Size' en CERO usa un juego de valores por defecto, para que el efecto no nazca invisible.")]
    [SerializeField] Sparkle.Settings startIntroSparkle = new();

    [Tooltip("Lo que el icono se queda quieto en el centro antes de salir hacia donde actúa el poder. Es el tiempo de RECONOCERLO: sin esta pausa cruza la pantalla y nadie sabe qué era.\n\nEn CERO usa el valor por defecto.")]
    [Range(0f, 2f)]
    [SerializeField] float startIntroHold = DEFAULT_INTRO_HOLD;

    [Tooltip("Lo que tarda en viajar del centro a su destino, encogiéndose por el camino.\n\nEn CERO usa el valor por defecto.")]
    [Range(0f, 2f)]
    [SerializeField] float startIntroTravel = DEFAULT_INTRO_TRAVEL;

    [Tooltip("Cuánto sube de tono el sonido entre el primer disparo que entra y el último. Es el rango COMPLETO, no el paso: la cuenta recorre siempre el mismo trecho sume 5 o sume 20.\n\nEn CERO usa el valor por defecto, no 'todos al mismo tono'.")]
    [Range(0f, 1f)]
    [SerializeField] float shotsGainPitchRise = 0.45f;

    // Los valores con los que nace el efecto cuando el componente de la escena todavía no tiene
    // los campos. Unity rellena un campo NUEVO en algo ya guardado con el cero del tipo, no con
    // su inicializador, así que sin esto el efecto nace apagado: sin tiempo, sin golpe de tamaño
    // y sin subir de tono — justo los tres síntomas de "no hace nada" (reportado por Diego).
    //
    // Quien quiera de verdad apagar uno pone un valor mínimo, no cero.
    // Cuánto crece una burbuja cuando la ola pasa por ella. No es configurable: es el tamaño del
    // gesto, no una decisión de diseño, y un campo más por esto sería ruido en el Inspector.
    // Lo ancha que se dibuja la cresta con su estela, en burbujas, y cada cuántas burbujas suena
    // un pop. No son decisiones de diseño sino el tamaño del gesto: un campo más por cada una
    // sería ruido en una ficha que ya tiene cinco.
    const int   TIDE_POP_EVERY       = 4;

    // Lo pequeña que acaba la última burbuja de la estela y lo separadas que van entre sí. Son el
    // tamaño del gesto, no decisiones de diseño: un campo más por cada una sería ruido en una
    // ficha que ya tiene cinco.
    const float TIDE_TAIL_SCALE      = 0.2f;
    const float TIDE_TAIL_GAP        = 0.45f;

    // Cuántas burbujas por hueco de la cola, cuánto se transparenta la espuma, y lo que una
    // burbuja del tablero tarda en reventar desde que el filo la pasa.
    // Una burbuja de la espuma: dónde va respecto al filo y cómo se mueve por su cuenta. Struct
    // y no tupla porque son siete datos y leerlos por nombre es la diferencia entre entender el
    // movimiento y descifrarlo.
    struct Foam
    {
        public RectTransform rt;
        public float back;    // cuánto va por detrás del filo, en píxeles
        public float baseY;   // su altura de partida dentro de la franja
        public float rise;    // cuánto asciende durante la travesía
        public float amp;     // lo que ondula
        public float freq;    // a qué ritmo
        public float phase;   // y por dónde empieza esa ondulación
    }

    const int   TIDE_FOAM_PER_STEP   = 2;
    const float TIDE_FOAM_ALPHA      = 0.75f;
    const float TIDE_POP_LAG         = 0.1f;

    // Cuánto crece una burbuja cuando la onda le pasa por encima, y cuánto dura ese golpe. Son
    // el tamaño del gesto, no decisiones de diseño: un campo por cada uno sería ruido en una
    // ficha que ya tiene tres.
    const float CLEAR_NUDGE          = 0.1f;
    const float CLEAR_NUDGE_TIME     = 0.25f;

    const float DEFAULT_INK_SPREAD   = 0.4f;
    const float DEFAULT_INK_HOLD     = 1f;
    const float DEFAULT_INK_FADE     = 0.5f;

    // Lo que tarda la mancha en acabar de aparecer. Corto: es un salpicón, no una llegada.
    const float INK_BLOT_POP         = 0.12f;

    // Qué parte del desvanecido ocupa el contagio por la cadena. Menos de uno a propósito: la
    // última de la cadena tiene que empezar a teñirse antes de que la mancha haya terminado de
    // irse, o se vería cambiar sola después de que el gesto acabó.
    const float CHAIN_SPREAD         = 0.6f;

    // El salpicón de cada burbuja alcanzada: crece y vuelve, como si algo le hubiera caído encima.
    const float INK_NUDGE            = 0.14f;
    const float INK_NUDGE_TIME       = 0.22f;

    const float DEFAULT_CLEAR_FADE   = 0.25f;

    // Lo que tarda en plantarse cada mina y cuánto se escalonan entre sí, cuando el catálogo no
    // dice nada. Cero significa "usa esto" y no "instantáneo": Unity rellena un campo nuevo en un
    // asset ya guardado con el cero del TIPO, así que sin esto el poder saldría de golpe en un
    // catálogo que nadie tocó todavía.
    // El velo y el aviso del modo "elige una burbuja", cuando el Inspector no dice nada.
    static readonly Color DEFAULT_PICK_VEIL = new(0f, 0.05f, 0.12f, 0.35f);
    const float DEFAULT_PICK_FADE = 0.18f;
    const float DEFAULT_PICK_HINT = 44f;

    const float DEFAULT_SEED_FADE = 0.25f;
    const float DEFAULT_SEED_STEP = 0.12f;

    // La onda por defecto: cruza la pantalla entera, que es lo que este poder necesita y la de la
    // Bomba no — aquella se queda en su radio.
    static readonly BombBlast.Settings DEFAULT_CLEAR_WAVE = new()
    {
        waveColor    = new Color(0.75f, 0.95f, 1f, 0.8f),
        waveFrom     = 40f,

        // DIÁMETRO, no radio: desde el centro de la pantalla la esquina está a unos 1100 de
        // radio, así que para pasarse del borde hace falta más del doble de esa cifra.
        waveTo       = 2800f,
        waveDuration = 0.9f,
        waveEdge     = 0.1f,
        waveCount    = 2,
        waveStagger  = 0.12f,
    };
    const int   DEFAULT_TIDE_ROWS    = 3;
    const float DEFAULT_TIDE_CROSS   = 0.6f;
    const int   DEFAULT_TIDE_TRAIL   = 12;
    static readonly Color DEFAULT_INTRO_VEIL = new(0f, 0.05f, 0.12f, 0.55f);
    static readonly Color DEFAULT_TIDE_COLOR = new(0.72f, 0.95f, 1f, 0.95f);
    const float DEFAULT_INTRO_POP    = 0.35f;
    const float DEFAULT_INTRO_SIZE   = 240f;

    // Los valores del inicializador de la clase, que es lo que un campo nuevo NO hereda cuando
    // Unity lo rellena en un componente ya guardado. Creado en código sí los trae.
    static readonly Sparkle.Settings DEFAULT_INTRO_SPARKLE = new();
    const float DEFAULT_INTRO_HOLD   = 0.45f;
    const float DEFAULT_INTRO_TRAVEL = 0.5f;

    // A qué tamaño llega el icono a su destino. Pequeño pero no cero: desaparecer en un punto se
    // lee como que se perdió, y lo que tiene que decir es que ENTRÓ ahí.
    const float INTRO_ARRIVAL_SCALE  = 0.3f;
    const float DEFAULT_INTRO_NAME_SIZE = 52f;

    const float DEFAULT_TOGETHER_STAGGER = 0.3f;
    const float DEFAULT_TOGETHER_SCALE   = 0.75f;
    const float DEFAULT_START_DELAY = 0.6f;
    const float DEFAULT_SHOTS_GAIN  = 1.2f;
    const float DEFAULT_SHOTS_PULSE = 0.35f;
    const float DEFAULT_SHOTS_PITCH = 0.45f;

    // Cada poder de inicio, enseñado por turno y esperando a que termine el anterior.
    //
    // En secuencia y no a la vez porque son regalos distintos: dos cosas ocurriendo juntas se
    // leen como una sola que no se entiende, y el jugador acaba de pagar por las dos.
    IEnumerator PlayStartEffects()
    {
        float wait = startEffectDelay > 0f ? startEffectDelay : DEFAULT_START_DELAY;

        // Ordenados por lo que le hacen al tablero, no por cómo los equipó el jugador: el que
        // quita burbujas tiene que pasar antes que el que las siembra, o se lleva por delante lo
        // que el otro acaba de poner. OrderBy es estable, así que entre iguales se respeta el
        // orden del loadout.
        var ordered = _startEffects.OrderBy(BoosterRules.StartOrder).ToList();

        // Todos a la vez, si está encendido y hay más de uno: una sola entrada en vez de tres
        // seguidas. Con tres poderes equipados, verlos de a uno son casi diez segundos antes de
        // poder jugar.
        if (startIntroTogether && ordered.Count > 1)
        {
            yield return new WaitForSeconds(wait);
            yield return PresentStartBoostersTogether(ordered);

            _startEffects.Clear();
            if (cannon) cannon.CheckAutoSwap();
            yield break;
        }

        foreach (var booster in ordered)
        {
            // Un respiro antes de cada uno. El jugador acaba de cerrar un modal y la vista todavía
            // se está asentando: el regalo que sale en ese mismo instante se lee como la última
            // parte del tutorial y no como algo que le están dando.
            yield return new WaitForSeconds(wait);

            // Y la misma entrada para todos: el icono dice QUÉ se activó y a dónde va. Lo que
            // viene detrás es el efecto propio de cada uno.
            yield return PresentStartBooster(booster, StartBoosterTarget(booster));

            yield return StartBoosterEffect(booster);
        }

        _startEffects.Clear();
        if (cannon) cannon.CheckAutoSwap();
    }

    // Lo que hace cada poder de inicio cuando su icono llega. En un solo sitio porque los dos
    // modos de presentación —de a uno o todos juntos— lo necesitan igual, y el que se quedara
    // atrás sería un poder que funciona en un modo y no en el otro.
    IEnumerator StartBoosterEffect(Booster booster)
    {
        switch (booster)
        {
            // La perla se convierte justo cuando el icono aterriza en la recámara, y recién
            // entonces salen las luces a sembrar el tablero.
            case Booster.Rainbow:
                cannon.MakeCurrentRainbow();
                yield return SeedRainbows();
                break;

            case Booster.Oxygen:     yield return GainShots(BoosterRules.OXYGEN_SHOTS); break;
            case Booster.LowTide:    yield return SweepLowTide(); break;
            case Booster.ClearWater: yield return ClearTheWater(); break;
            case Booster.Mine:       yield return PlantMines();    break;
        }
    }

    // La marea se retira y deja al descubierto las filas de abajo (GDD §3.2).
    //
    // Dos tiempos, y el primero es el que hace que se entienda: un barrido recorre las burbujas
    // condenadas antes de tocarlas, fila por fila y de izquierda a derecha, con su sonido. Sin
    // ese repaso las filas desaparecen de golpe y el jugador ve un hueco sin saber de dónde salió
    // (pedido de Diego). Recién después revientan, en cadena, como un match grande.
    //
    // NO puntúa, a diferencia de un match: las estrellas salen del score, así que una fila que
    // suma puntos es comprar estrellas con monedas. Lo que se compra es aire contra el cañón, y
    // los puntos siguen siendo del jugador — mismo criterio que el Agua Clara, que convierte en
    // vez de borrar justo para que el racimo lo reviente él.
    // El Agua Clara: el color más escaso del tablero pasa al más abundante (GDD §3.2).
    //
    // CONVIERTE, no borra. Borrar resolvería parte del objetivo solo y tiraría todo lo que
    // colgara de esas burbujas — un derrumbe que nadie previó al balancear el nivel. Así el
    // tablero sigue igual de lleno pero queda un racimo grande que el jugador revienta con su
    // disparo: el mérito es suyo, no del poder.
    //
    // Y ese color deja de salir del cañón. Sin eso, las burbujas que acaba de quitar volverían
    // por la cola y el jugador habría pagado por un respiro de un disparo.
    IEnumerator ClearTheWater()
    {
        var (from, to) = BoosterRules.ClearWaterColors(grid.ColorCounts());

        // Con un solo color en el tablero no hay "el más escaso". El poder ya se cobró —se
        // consume al abrir— pero al menos no se queda colgado fingiendo que hace algo.
        if (from == BubbleColor.Rainbow || to == BubbleColor.Rainbow) yield break;

        var cells = grid.CellsOfColor(from);
        if (cells.Count == 0) yield break;

        var   art  = BoosterRules.ArtFor(Booster.ClearWater);
        float fade = art != null && art.clearFade > 0f ? art.clearFade : DEFAULT_CLEAR_FADE;

        var wave = art != null && art.clearWave != null && art.clearWave.waveTo > 0f
            ? art.clearWave
            : DEFAULT_CLEAR_WAVE;

        // La onda sale de donde se apagó el icono, y ese punto es el centro de la pantalla —
        // misma cuenta que StartBoosterTarget, para que no haya dos ideas de dónde está.
        var     container = (RectTransform)grid.transform;
        Vector2 origin    = container.InverseTransformPoint(ScreenCenter());

        BombBlast.Ripple(container, origin, wave);

        if (art?.clearClip) AudioManager.Instance?.PlaySfx(art.clearClip);
        if (SaveManager.Vibration) MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.MediumImpact);

        // Y cada burbuja cambia CUANDO EL FRENTE LA ALCANZA.
        //
        // El reparto no es por orden ni por tandas: es la distancia real al centro convertida en
        // tiempo con la misma cuenta que usa la onda para crecer. Por eso el teñido va pegado al
        // frente y no detrás ni delante — lo que se ve y lo que pasa son lo mismo.
        //
        // Una burbuja más lejos que 'Wave To' no se quedaría nunca sin cambiar: el tiempo se topa
        // al final de la onda, así que el poder siempre termina lo que empezó.
        //
        // El radio de la onda es un DIÁMETRO, así que la distancia a la que llega es la mitad.
        float span    = Mathf.Max(1f, wave.waveTo - wave.waveFrom) / 2f;
        float started = wave.waveFrom / 2f;

        float AtDistance(Vector2Int cell)
        {
            float distance = Vector2.Distance(origin, HexGridMath.CellToLocalPos(cell));

            return Mathf.Clamp01((distance - started) / span) * wave.waveDuration;
        }

        // El empujón lo recibe TODO el tablero, no solo lo que cambia de color: lo que cruza es
        // una onda, y una onda mueve lo que toca. Viendo saltar solo las burbujas que se tiñen,
        // el efecto se lee como un parpadeo por colores en vez de como algo que pasa por encima.
        foreach (var cell in new List<Vector2Int>(grid.Occupied))
            if (grid.TryGetBubble(cell, out var view) && view != null)
                StartCoroutine(Nudge((RectTransform)view.transform, AtDistance(cell), CLEAR_NUDGE_TIME, CLEAR_NUDGE));

        foreach (var cell in cells)
            grid.Recolor(cell, to, AtDistance(cell), fade);

        // El cañón deja de ofrecerlo, ya mismo: si esperara a que la onda terminara, el jugador
        // podría ver llegar a la recámara justo el color que está desapareciendo del tablero.
        cannon.BanColor(from);

        yield return new WaitForSeconds(wave.waveDuration + fade);
    }

    IEnumerator SweepLowTide()
    {
        var art  = BoosterRules.ArtFor(Booster.LowTide);
        int rows = art != null && art.tideRows > 0 ? art.tideRows : DEFAULT_TIDE_ROWS;

        var doomed = BoosterRules.LowTideCells(grid, rows);
        if (doomed.Count == 0) yield break;

        float cross = art != null && art.tideSweepDuration > 0f ? art.tideSweepDuration : DEFAULT_TIDE_CROSS;
        int   trail = art != null && art.tideTrail         > 0  ? art.tideTrail         : DEFAULT_TIDE_TRAIL;
        Color tint  = art != null && art.tideSweepColor.a  > 0f ? art.tideSweepColor    : DEFAULT_TIDE_COLOR;

        Sprite bubble = art != null && art.tideBubble != null ? art.tideBubble : BoosterRules.GlowFor(Booster.LowTide);
        if (bubble == null) yield break;

        // La franja que la ola tiene que cubrir: de la fila más alta que se lleva a la más baja.
        float bandTop    = doomed.Min(c => HexGridMath.CellToLocalPos(c).y);
        float bandBottom = doomed.Max(c => HexGridMath.CellToLocalPos(c).y);

        var container = (RectTransform)grid.transform;
        var foam      = new List<Foam>();

        // La espuma se reparte por TODA la franja, no una burbuja por fila.
        //
        // Atada a las filas se leían tres carriles rectos por mucho que cada burbuja se saliera un
        // poco de su sitio: el ojo encuentra la rejilla igual (reportado por Diego). Repartiéndola
        // a lo ancho y a lo alto sin rejilla de por medio, lo que se ve es agua.
        //
        // Y cada una se mueve por su cuenta mientras cruza: sube despacio y ondula a su ritmo. Es
        // lo que separa una nube de puntos trasladándose de algo que fluye.
        // La densidad sale de la longitud de la cola y de lo alta que es la franja: una marea de
        // cinco filas necesita más espuma que una de una para verse igual de llena.
        int rowsHit  = doomed.Select(c => c.y).Distinct().Count();
        int count    = Mathf.Max(1, trail) * rowsHit * TIDE_FOAM_PER_STEP;
        float  tailPx = trail * TIDE_TAIL_GAP * HexGridMath.BubbleDiameter;
        float  reach  = HexGridMath.BubbleRadius;   // cuánto se sale de la franja por arriba y por abajo

        for (int i = 0; i < count; i++)
        {
            // Al cuadrado: así se amontonan cerca de la cabeza y se van soltando hacia la cola,
            // que es como se deshace la espuma de verdad. Repartidas parejo parecen un enjambre.
            float back = Mathf.Pow(Random.value, 2f);

            float shrink = Mathf.Lerp(1f, TIDE_TAIL_SCALE, back) * Random.Range(0.7f, 1.2f);

            var go = new GameObject("TideFoam", typeof(RectTransform), typeof(Image)) { hideFlags = HideFlags.DontSave };
            var rt = (RectTransform)go.transform;

            rt.SetParent(container, false);
            rt.SetAsLastSibling();
            rt.sizeDelta  = Vector2.one * HexGridMath.BubbleDiameter;
            rt.localScale = Vector3.one * shrink;

            var image = go.GetComponent<Image>();
            image.sprite        = bubble;
            image.raycastTarget = false;

            // Translúcida a propósito: por debajo están reventando burbujas, y una espuma opaca
            // se las comería justo cuando hay que verlas irse. Las de atrás, más tenues.
            image.color = new Color(tint.r, tint.g, tint.b, tint.a * Mathf.Lerp(1f, 0.35f, back) * TIDE_FOAM_ALPHA);

            foam.Add(new Foam
            {
                rt    = rt,
                back  = back * tailPx,
                baseY = Random.Range(bandTop - reach, bandBottom + reach),
                rise  = Random.Range(0.2f, 1f) * HexGridMath.BubbleRadius,   // sube mientras cruza
                amp   = Random.Range(0.1f, 0.45f) * HexGridMath.BubbleDiameter,
                freq  = Random.Range(1.5f, 4f),
                phase = Random.Range(0f, Mathf.PI * 2f),
            });
        }

        // De fuera del tablero a fuera del otro lado, contando la cola: entrando y saliendo por
        // el borde, nadie la ve aparecer ni desvanecerse en medio del agua.
        float tail = trail * TIDE_TAIL_GAP * HexGridMath.BubbleDiameter;
        float from = -HexGridMath.DesignWidth / 2f - HexGridMath.BubbleDiameter;
        float to   =  HexGridMath.DesignWidth / 2f + HexGridMath.BubbleDiameter + tail;

        if (art?.tideSweepClip) AudioManager.Instance?.PlaySfx(art.tideSweepClip);
        if (SaveManager.Vibration) MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.MediumImpact);

        // Quién queda por reventar, ordenado por columna: la ola se lleva lo que ya ha pasado.
        var pending = new List<Vector2Int>(doomed);
        pending.Sort((a, b) => HexGridMath.CellToLocalPos(a).x.CompareTo(HexGridMath.CellToLocalPos(b).x));

        int next    = 0;
        var removed = new HashSet<Vector2Int>();

        for (float t = 0f; t < cross; t += Time.deltaTime)
        {
            float edge = Mathf.Lerp(from, to, Mathf.Clamp01(t / cross));

            foreach (var bit in foam)
            {
                if (!bit.rt) continue;

                // Lo que ESTA burbuja lleva de travesía: las de la cola entran más tarde, así que
                // su ondulación y su subida arrancan cuando a ellas les toca, no todas a la vez.
                float own = Mathf.Max(0f, (edge - bit.back - from) / Mathf.Max(1f, to - from));

                float y = bit.baseY
                        - bit.rise * own                                  // la espuma asciende
                        + bit.amp  * Mathf.Sin(bit.phase + own * bit.freq * Mathf.PI * 2f);

                bit.rt.anchoredPosition = new Vector2(edge - bit.back, y);
            }

            // Todo lo que la primera burbuja ya dejó atrás revienta. Sin cadena ni escalonado: lo
            // que marca el ritmo es la ola, y un retraso encima la separaría de lo que destruye.
            while (next < pending.Count && HexGridMath.CellToLocalPos(pending[next]).x <= edge)
            {
                var cell = pending[next++];

                // Revienta un instante DESPUÉS de que el filo la pase, no justo encima: así el
                // estallido ocurre con la espuma ya cubriéndola, que es lo que se pidió — las
                // burbujas se van deshaciendo a lo largo del rastro y no en una línea de corte.
                //
                // Una de cada tantas suena: treinta pops seguidos son ruido, y lo que tiene que
                // oírse es la ola cruzando.
                if (grid.TryGetBubble(cell, out var view))
                    view.PlayPopAnimation(TIDE_POP_LAG, removed.Count % TIDE_POP_EVERY == 0 ? art?.burstClip : null);

                grid.RemoveBubble(cell);
                removed.Add(cell);
            }

            yield return null;
        }

        // Lo que quedara por redondeo, cuando la ola ya salió por el otro lado.
        while (next < pending.Count)
        {
            var cell = pending[next++];
            if (grid.TryGetBubble(cell, out var view)) view.PlayPopAnimation(0f, null);
            grid.RemoveBubble(cell);
            removed.Add(cell);
        }

        foreach (var bit in foam) if (bit.rt) Destroy(bit.rt.gameObject);

        _bubblesPopped += doomed.Count;

        // Por si acaso: las filas de abajo no sostienen a nadie POR DEBAJO, pero en un tablero con
        // huecos una burbuja de más arriba puede estar colgando por un camino que baja hasta acá y
        // vuelve a subir. Sale gratis preguntarlo.
        var collapsed = CollapseFloating(removed);

        yield return new WaitForSeconds(collapsed.count * collapsed.step);
    }

    // La entrada de un poder de inicio: su icono sale en medio de la pantalla, se deja reconocer
    // y se va hacia donde el poder va a actuar.
    //
    // La misma para todos a propósito. Son regalos que ocurren solos, sin que el jugador toque
    // nada, y lo que los hace legibles es que siempre se cuenten igual: esto es lo que compraste,
    // y esto es lo que va a tocar. Lo que cada uno haga al llegar ya es cosa suya.
    //
    // El destino va en coordenadas de MUNDO porque cada poder actúa en un sitio distinto —el
    // contador del HUD, un borde del tablero, la recámara— y son jerarquías distintas. En mundo
    // se piden todos igual.
    IEnumerator PresentStartBooster(Booster booster, Vector3 target)
    {
        if (shotsLabel == null) yield break;

        var canvas = shotsLabel.canvas;
        if (canvas == null) yield break;

        float size   = startIntroScale  > 0f ? startIntroScale  : DEFAULT_INTRO_SIZE;
        float pop    = startIntroPop    > 0f ? startIntroPop    : DEFAULT_INTRO_POP;
        float hold   = startIntroHold   > 0f ? startIntroHold   : DEFAULT_INTRO_HOLD;
        float travel = startIntroTravel > 0f ? startIntroTravel : DEFAULT_INTRO_TRAVEL;
        Color veil   = startIntroVeil.a > 0f ? startIntroVeil   : DEFAULT_INTRO_VEIL;

        var root = (RectTransform)canvas.rootCanvas.transform;

        var veilImage = MakeVeil(root, veil);

        var made = MakeIntroIcon(booster, root, size);
        if (made.rt == null) { Destroy(veilImage.gameObject); yield break; }

        var rt        = made.rt;
        var iconImage = made.image;
        var label     = made.label;

        Vector3 from = root.TransformPoint(Vector3.zero);   // el centro de la pantalla
        rt.position  = from;

        var sparkle = startIntroSparkle != null && startIntroSparkle.glowSize > 0f
            ? startIntroSparkle
            : DEFAULT_INTRO_SPARKLE;

        Sparkle.Burst(rt, sparkle);

        if (startIntroClip) AudioManager.Instance?.PlaySfx(startIntroClip);
        if (SaveManager.Vibration) MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.MediumImpact);

        // Crece de nada, se pasa un 20% y vuelve. Pasarse y volver es lo que lo hace ATERRIZAR:
        // creciendo hasta su tamaño y parando, el icono se enciende en vez de llegar. El velo
        // sube con él, así que la pantalla se apaga al mismo ritmo.
        float popUp = pop * 0.6f;

        for (float t = 0f; t < pop; t += Time.deltaTime)
        {
            float k = t < popUp
                ? Mathf.Lerp(0f,   1.2f, Ease(t / popUp))
                : Mathf.Lerp(1.2f, 1f,   Ease((t - popUp) / Mathf.Max(0.0001f, pop - popUp)));

            rt.localScale   = Vector3.one * k;
            veilImage.color = new Color(veil.r, veil.g, veil.b, veil.a * Mathf.Clamp01(t / pop));

            yield return null;
        }

        rt.localScale = Vector3.one;

        yield return new WaitForSeconds(hold);

        for (float t = 0f; t < travel; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / travel);

            // Arranca despacio y se dispara hacia el destino: la salida lenta deja ver de dónde
            // sale y la llegada rápida se lee como que LLEGA, no como que pasa por ahí.
            float ease = p * p;

            rt.position     = Vector3.Lerp(from, target, ease);
            rt.localScale   = Vector3.one * Mathf.Lerp(1f, INTRO_ARRIVAL_SCALE, ease);
            veilImage.color = new Color(veil.r, veil.g, veil.b, veil.a * (1f - p));

            // El nombre se queda atrás: acompaña al icono mientras está quieto y se apaga en
            // cuanto arranca. Viajando con él llegaría al contador un texto que no entra en
            // ningún sitio, y encima ilegible por el encogimiento.
            if (label) label.alpha = 1f - Mathf.InverseLerp(0f, 0.35f, p);

            // El desvanecido va al final y no todo el camino: uno que se apaga desde que sale
            // llega invisible y no se ve entrar en ningún sitio.
            iconImage.color = new Color(1f, 1f, 1f, 1f - Mathf.InverseLerp(0.75f, 1f, p));

            yield return null;
        }

        Destroy(rt.gameObject);
        Destroy(veilImage.gameObject);
    }

    // El velo que apaga el tablero detrás de los iconos, para que no compitan con cincuenta
    // burbujas de colores. Nace transparente: lo abre quien lo pida.
    // El velo y el aviso de "ahora toca elegir". El velo NO recibe toques: tiene que dejar pasar
    // el tap a la burbuja que hay debajo, que es justo lo que el jugador va a hacer.
    void ShowPickVeil(Booster booster)
    {
        if (shotsLabel == null || shotsLabel.canvas == null) return;

        var root = (RectTransform)shotsLabel.canvas.rootCanvas.transform;
        var veil = pickVeil.a > 0f ? pickVeil : DEFAULT_PICK_VEIL;

        _pickVeilImage      = MakeVeil(root, veil);
        _pickVeilImage.name = "PickVeil";
        _pickVeilAlpha      = veil.a;

        // El renglón de avisos del juego, si lo hay: el mismo que usa el hint de disparo. Puesto
        // por mi cuenta abajo del todo, el aviso caía justo encima de la barra de poderes
        // (reportado por Diego) — y ese sitio ya estaba resuelto para esto.
        _pickHint   = cannon != null && cannon.Hint != null ? cannon.Hint.Message : null;
        _pickBorrowed = _pickHint != null;

        if (_pickHint == null) _pickHint = MakeHint(root, "");

        _pickHint.text = BoosterRules.TextFor("ui.booster.pick.hint", booster);
        _pickHint.gameObject.SetActive(true);

        StartCoroutine(FadePick());
    }

    void HidePickVeil()
    {
        if (_pickVeilImage == null && _pickHint == null) return;

        StartCoroutine(FadePickOut(_pickVeilImage, _pickHint, _pickVeilAlpha, _pickBorrowed));

        _pickVeilImage = null;
        _pickHint      = null;
        _pickBorrowed  = false;
    }

    IEnumerator FadePick()
    {
        float time = pickFade > 0f ? pickFade : DEFAULT_PICK_FADE;

        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            SetPickAlpha(_pickVeilImage, _pickHint, t / time, _pickVeilAlpha);
            yield return null;
        }

        SetPickAlpha(_pickVeilImage, _pickHint, 1f, _pickVeilAlpha);
    }

    // Se le pasan las piezas en vez de leer los campos: para cuando termine el desvanecido, el
    // modo puede haber vuelto a arrancar con unas nuevas, y apagaría las que acaban de nacer.
    IEnumerator FadePickOut(Image veil, TMP_Text hint, float veilAlpha, bool borrowed)
    {
        float time = pickFade > 0f ? pickFade : DEFAULT_PICK_FADE;

        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            SetPickAlpha(veil, hint, 1f - t / time, veilAlpha);
            yield return null;
        }

        if (veil) Destroy(veil.gameObject);

        // El prestado se apaga y se devuelve entero, con su alfa: destruirlo dejaría al hint de
        // disparo sin renglón para el resto de la partida.
        if (borrowed && hint)
        {
            hint.alpha = 1f;
            hint.gameObject.SetActive(false);
        }
        else if (hint) Destroy(hint.gameObject);
    }

    static void SetPickAlpha(Image veil, TMP_Text hint, float progress, float veilAlpha)
    {
        float p = Mathf.Clamp01(progress);

        if (veil)
        {
            var c = veil.color;
            veil.color = new Color(c.r, c.g, c.b, veilAlpha * p);
        }

        if (hint) hint.alpha = p;
    }

    TMP_Text MakeHint(RectTransform root, string text)
    {
        var go = new GameObject("PickHint", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
        var rt = (RectTransform)go.transform;

        rt.SetParent(root, false);
        rt.SetAsLastSibling();

        // Pegado abajo y a lo ancho: ahí no tapa el tablero, que es lo único que el jugador
        // necesita mirar mientras elige.
        rt.anchorMin        = new Vector2(0.05f, 0.06f);
        rt.anchorMax        = new Vector2(0.95f, 0.06f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = new Vector2(0f, pickHintSize * 2.4f);

        var label = go.AddComponent<TextMeshProUGUI>();

        if (startIntroNameFont != null) label.font = startIntroNameFont;
        if (label.font != null)         label.fontSharedMaterial = label.font.material;

        label.text             = text;
        label.fontSize         = pickHintSize > 0f ? pickHintSize : DEFAULT_PICK_HINT;
        label.color            = startIntroNameColor.a > 0f ? startIntroNameColor : Color.white;
        label.raycastTarget    = false;
        label.enableAutoSizing = false;
        label.alignment        = TMPro.TextAlignmentOptions.Center;
        label.textWrappingMode = TMPro.TextWrappingModes.Normal;

        return label;
    }

    static Image MakeVeil(RectTransform root, Color veil)
    {
        var go = new GameObject("StartBoosterVeil", typeof(RectTransform), typeof(Image)) { hideFlags = HideFlags.DontSave };
        var rt = (RectTransform)go.transform;

        rt.SetParent(root, false);
        rt.SetAsLastSibling();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.color         = new Color(veil.r, veil.g, veil.b, 0f);
        image.raycastTarget = false;

        return image;
    }

    // Un icono de poder listo para animar, con su nombre debajo si corresponde. Nace a escala
    // cero: lo hace crecer quien lo pida.
    (RectTransform rt, Image image, TMP_Text label) MakeIntroIcon(Booster booster, RectTransform root, float size)
    {
        var icon = BoosterRules.IconFor(booster);

        // Sin icono en el catálogo, el poder entraba MUDO: esta entrada se abandonaba y el efecto
        // ocurría sin que nada lo anunciara — que es justo lo que la entrada vino a arreglar.
        //
        // Un asset a medio configurar no puede apagar una pieza del juego. Se avisa por consola,
        // que es donde se arregla, y se sigue con el brillo del propio poder: dice menos que su
        // icono, pero dice que algo acaba de pasar.
        if (icon == null)
        {
            Debug.LogWarning($"[GameplayController] '{booster}' no tiene 'Icon' en el catálogo — su entrada sale con el brillo genérico.", this);
            icon = BoosterRules.GlowFor(booster);
        }

        if (icon == null) return (null, null, null);

        var go = new GameObject("StartBoosterIcon", typeof(RectTransform), typeof(Image)) { hideFlags = HideFlags.DontSave };
        var rt = (RectTransform)go.transform;

        rt.SetParent(root, false);
        rt.SetAsLastSibling();          // por encima del velo
        rt.sizeDelta  = Vector2.one * size;
        rt.localScale = Vector3.zero;

        var image = go.GetComponent<Image>();
        image.sprite         = icon;
        image.raycastTarget  = false;
        image.preserveAspect = true;

        TMP_Text label = null;

        // El nombre, con su propia fuente.
        //
        // Hecho de cero y no clonando el contador del HUD: aquel lleva contorno porque vive sobre
        // un cartel de madera y tiene que leerse encima de él, y ese mismo contorno sobre el velo
        // oscuro de aquí se ve como un borde pegado. Dos sitios distintos, dos tipografías.
        //
        // Cuelga del ICONO, así que crece y se coloca con él sin repetir cuentas.
        if (startIntroShowName)
        {
            var labelGo = new GameObject("StartBoosterName", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
            labelGo.transform.SetParent(rt, false);

            label = labelGo.AddComponent<TextMeshProUGUI>();

            if (startIntroNameFont != null) label.font = startIntroNameFont;

            // El material BASE de la fuente, no el que traiga puesto: en TextMeshPro el contorno
            // vive en el material, no en la fuente.
            if (label.font != null) label.fontSharedMaterial = label.font.material;

            label.text             = BoosterRules.NameFor(booster);
            label.fontSize         = startIntroNameSize > 0f ? startIntroNameSize : DEFAULT_INTRO_NAME_SIZE;
            label.color            = startIntroNameColor.a > 0f ? startIntroNameColor : Color.white;
            label.raycastTarget    = false;
            label.enableAutoSizing = false;
            label.alignment        = TMPro.TextAlignmentOptions.Center;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

            var labelRt = label.rectTransform;
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0f);
            labelRt.pivot     = new Vector2(0.5f, 1f);
            labelRt.anchoredPosition = new Vector2(0f, -startIntroNameGap);
            labelRt.sizeDelta  = new Vector2(size * 3f, 0f);
            labelRt.localScale = Vector3.one;
        }

        return (rt, image, label);
    }

    // Todos los poderes de inicio a la vez: salen juntos en el centro, repartidos según cuántos
    // sean, y cada uno se va a su sitio.
    //
    // Los efectos arrancan en el orden de BoosterRules.StartOrder y no literalmente a la vez, con
    // un escalón de un suspiro entre ellos. A la vista son simultáneos —nadie distingue dos
    // décimas— pero el que quita burbujas sigue pasando antes que el que las siembra, que es el
    // problema que este orden vino a resolver. Simultáneo de verdad lo traería de vuelta.
    IEnumerator PresentStartBoostersTogether(List<Booster> boosters)
    {
        if (boosters.Count == 0 || shotsLabel == null) yield break;

        var canvas = shotsLabel.canvas;
        if (canvas == null) yield break;

        float pop    = startIntroPop    > 0f ? startIntroPop    : DEFAULT_INTRO_POP;
        float hold   = startIntroHold   > 0f ? startIntroHold   : DEFAULT_INTRO_HOLD;
        float travel = startIntroTravel > 0f ? startIntroTravel : DEFAULT_INTRO_TRAVEL;
        Color veil   = startIntroVeil.a > 0f ? startIntroVeil   : DEFAULT_INTRO_VEIL;

        float shrink  = startIntroTogetherScale   > 0f ? startIntroTogetherScale   : DEFAULT_TOGETHER_SCALE;
        float stagger = startIntroTogetherStagger > 0f ? startIntroTogetherStagger : DEFAULT_TOGETHER_STAGGER;

        // Más pequeños que en el modo de a uno: tres a tamaño completo no caben sin pisarse.
        float size = (startIntroScale > 0f ? startIntroScale : DEFAULT_INTRO_SIZE) * shrink;

        var root      = (RectTransform)canvas.rootCanvas.transform;
        var veilImage = MakeVeil(root, veil);

        var spots = IntroLayout(boosters.Count, size * startIntroSpread);
        var icons = new List<(RectTransform rt, Image image, TMP_Text label, Vector3 from, Vector3 to)>();

        Vector3 center = root.TransformPoint(Vector3.zero);

        for (int i = 0; i < boosters.Count; i++)
        {
            var made = MakeIntroIcon(boosters[i], root, size);
            if (made.rt == null) continue;

            // Cada uno en su sitio del grupo, y de ahí a donde actúa su poder.
            Vector3 from = center + (Vector3)spots[i];
            made.rt.position = from;

            icons.Add((made.rt, made.image, made.label, from, StartBoosterTarget(boosters[i])));
        }

        if (icons.Count == 0) { Destroy(veilImage.gameObject); yield break; }

        // Un solo destello y un solo sonido para el grupo: tres a la vez se oyen como un golpe
        // sucio, y lo que se anuncia es "esto es lo que traes", no tres cosas sueltas.
        Sparkle.Burst(icons[0].rt, startIntroSparkle != null && startIntroSparkle.glowSize > 0f
            ? startIntroSparkle : DEFAULT_INTRO_SPARKLE);

        if (startIntroClip) AudioManager.Instance?.PlaySfx(startIntroClip);
        if (SaveManager.Vibration) MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.MediumImpact);

        float popUp = pop * 0.6f;

        for (float t = 0f; t < pop; t += Time.deltaTime)
        {
            float k = t < popUp
                ? Mathf.Lerp(0f,   1.2f, Ease(t / popUp))
                : Mathf.Lerp(1.2f, 1f,   Ease((t - popUp) / Mathf.Max(0.0001f, pop - popUp)));

            foreach (var icon in icons) if (icon.rt) icon.rt.localScale = Vector3.one * k;

            veilImage.color = new Color(veil.r, veil.g, veil.b, veil.a * Mathf.Clamp01(t / pop));
            yield return null;
        }

        foreach (var icon in icons) if (icon.rt) icon.rt.localScale = Vector3.one;

        yield return new WaitForSeconds(hold);

        for (float t = 0f; t < travel; t += Time.deltaTime)
        {
            float p    = Mathf.Clamp01(t / travel);
            float ease = p * p;

            foreach (var icon in icons)
            {
                if (!icon.rt) continue;

                icon.rt.position   = Vector3.Lerp(icon.from, icon.to, ease);
                icon.rt.localScale = Vector3.one * Mathf.Lerp(1f, INTRO_ARRIVAL_SCALE, ease);

                if (icon.image) icon.image.color = new Color(1f, 1f, 1f, 1f - Mathf.InverseLerp(0.75f, 1f, p));
                if (icon.label) icon.label.alpha = 1f - Mathf.InverseLerp(0f, 0.35f, p);
            }

            veilImage.color = new Color(veil.r, veil.g, veil.b, veil.a * (1f - p));
            yield return null;
        }

        foreach (var icon in icons) if (icon.rt) Destroy(icon.rt.gameObject);
        Destroy(veilImage.gameObject);

        // Y ahora los efectos, lanzados casi a la vez y esperando a que acabe el último: lo que
        // viene después es soltar el control, y eso no puede pasar con la ola todavía cruzando.
        var running = new List<Coroutine>();

        foreach (var booster in boosters)
        {
            running.Add(StartCoroutine(StartBoosterEffect(booster)));
            yield return new WaitForSeconds(stagger);
        }

        foreach (var routine in running) yield return routine;
    }

    // Dónde se coloca cada icono del grupo, respecto al centro.
    //
    // Hasta tres porque ese es el tope de poderes equipados (BoosterLoadout.MAX_EQUIPPED), y el
    // triángulo de tres es la única forma que no deja a ninguno descolgado. De cuatro en adelante
    // se reparten en fila, que no va a pasar pero tampoco se rompe.
    static Vector2[] IntroLayout(int count, float gap) => count switch
    {
        1 => new[] { Vector2.zero },
        2 => new[] { new Vector2(-gap, 0f), new Vector2(gap, 0f) },
        3 => new[] { new Vector2(0f, gap * 0.8f), new Vector2(-gap, -gap * 0.6f), new Vector2(gap, -gap * 0.6f) },
        _ => Enumerable.Range(0, count)
                       .Select(i => new Vector2((i - (count - 1) / 2f) * gap, 0f))
                       .ToArray(),
    };

    // La Tinta de Pulpo: salpica unas cuantas burbujas del radio y las tiñe del color que VUELVE
    // a la recámara (GDD §3.2).
    //
    // Del que vuelve y no del que se dispara: teñir del disparado haría match al instante y todo
    // reventaría en el mismo movimiento — un efecto, no una jugada. Del que vuelve, el terreno
    // queda sembrado y el remate es del jugador en el disparo siguiente.
    //
    // No revienta: de ahí que no pase por ResolveBoosterBlast como los demás.
    void SplashInk(Vector2Int center)
    {
        if (cannon == null) return;

        var color = cannon.CurrentColor;

        // Un comodín no tiñe de nada: encaja con todo, así que "pintar de arcoíris" sería
        // repartir comodines gratis — otro poder, y mucho más fuerte.
        if (color == BubbleColor.Rainbow) return;

        var   art    = BoosterRules.ArtFor(Booster.Ink);
        int   count  = BoosterRules.InkSplash;
        float spread = art != null && art.inkSpread > 0f ? art.inkSpread  : DEFAULT_INK_SPREAD;
        float fade   = art != null && art.inkFade   > 0f ? art.inkFade    : DEFAULT_INK_FADE;

        // Lo que la salpicadura espera tras el estallido. Puede ser CERO —y lo es por defecto—
        // porque las dos cosas son el mismo momento: el frasco se parte y la tinta salta. Un
        // retardo las separaba en dos sucesos (pedido de Diego).
        float wait = Mathf.Max(0f, art?.inkDelay ?? 0f);

        var splashed = BoosterRules.InkCells(center, grid, count, BoosterRules.InkRadius);
        if (splashed.Count == 0) return;

        // De la más cercana al impacto hacia afuera: la mancha se abre desde donde cayó, que es
        // lo que el jugador estaba mirando.
        Vector2 from = HexGridMath.CellToLocalPos(center);
        splashed.Sort((a, b) => Vector2.Distance(from, HexGridMath.CellToLocalPos(a))
                        .CompareTo(Vector2.Distance(from, HexGridMath.CellToLocalPos(b))));

        float hold = art != null && art.inkHold > 0f ? art.inkHold : DEFAULT_INK_HOLD;
        float step = splashed.Count > 1 ? spread / (splashed.Count - 1) : 0f;

        Sprite drop = BoosterRules.InkDrop;
        float  size = BoosterRules.InkDropSize;
        Color  tint = BoosterRules.InkDropTint(color);

        // Lo ya pintado por otra gota no se vuelve a contar: dos que caen en el mismo racimo son
        // una sola mancha más grande, no el doble de trabajo.
        var painted = new HashSet<Vector2Int>();

        for (int i = 0; i < splashed.Count; i++)
        {
            if (!grid.TryGetBubble(splashed[i], out var view) || view == null) continue;
            if (painted.Contains(splashed[i])) continue;

            float at = wait + i * step;

            // La mancha aparece ENCIMA, sin viaje: la tinta salpica, no vuela. Y cuelga de la
            // burbuja, así que va donde ella vaya — el tablero se desplaza cuando se retira del
            // cañón, y una mancha anclada al tablero se quedaría en el sitio viejo.
            StartCoroutine(InkBlot((RectTransform)view.transform, drop, tint, size, at, hold, fade));

            // El empujón va cuando CAE, no cuando se pinta: es el golpe de algo que aterriza.
            StartCoroutine(Nudge((RectTransform)view.transform, at, INK_NUDGE_TIME, INK_NUDGE));

            if (art?.inkClip) StartCoroutine(PlayClipAfter(art.inkClip, at));
            StartCoroutine(VibrateAfter(MOST_HapticFeedback.HapticTypes.LightImpact, at));

            // Y se lleva LA CADENA entera, no solo la burbuja que pilló: cayendo sobre una que
            // está acompañada, el color se extiende por todas sus vecinas y el jugador se
            // encuentra un racimo listo para reventar.
            var chain = BoosterRules.InkChain(splashed[i], grid);

            // El contagio sale de donde cayó la gota hacia afuera, dentro del mismo tiempo que
            // tarde en desvanecerse: lo que se va y lo que aparece siguen siendo el mismo gesto,
            // por larga que sea la cadena.
            float creep = chain.Count > 1 ? fade * CHAIN_SPREAD / (chain.Count - 1) : 0f;

            // El sonido del tinte va con la primera de la cadena y no con cada una: una cadena
            // larga son diez burbujas cambiando, y diez clips encimados no suenan a diez cosas,
            // suenan a ruido.
            if (art?.inkPaintClip) StartCoroutine(PlayClipAfter(art.inkPaintClip, at + hold));

            // Más suave que la caída: la gota golpea, el color solo aparece.
            StartCoroutine(VibrateAfter(MOST_HapticFeedback.HapticTypes.SoftImpact, at + hold));

            for (int j = 0; j < chain.Count; j++)
            {
                if (!painted.Add(chain[j])) continue;

                grid.Recolor(chain[j], color, at + hold + j * creep, fade);

                if (j > 0 && grid.TryGetBubble(chain[j], out var member) && member != null)
                    StartCoroutine(Nudge((RectTransform)member.transform, at + hold + j * creep,
                                         INK_NUDGE_TIME, INK_NUDGE * 0.6f));
            }
        }

        // El reparto termina mucho después de que el poder devuelva el control, y teñir también
        // puede dejar un color sin una sola burbuja en el tablero. El chequeo del estallido ya
        // pasó para entonces, así que hace falta otro cuando la última mancha se fue.
        StartCoroutine(CheckSwapAfter(wait + spread + hold + fade));
    }

    IEnumerator CheckSwapAfter(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!_levelEnded && cannon) cannon.CheckAutoSwap();
    }

    // Una mancha de tinta encima de una burbuja: aparece, se queda quieta y se va.
    //
    // Cuelga de la burbuja y no del tablero, así que ni el retiro del grid ni un derrumbe la
    // dejan flotando donde ya no hay nada. Si la burbuja desaparece, la mancha se va con ella.
    IEnumerator InkBlot(RectTransform bubble, Sprite sprite, Color tint, float size,
                        float delay, float hold, float fade)
    {
        if (bubble == null || sprite == null) yield break;

        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (bubble == null) yield break;

        var go = new GameObject("InkBlot", typeof(RectTransform), typeof(Image)) { hideFlags = HideFlags.DontSave };
        var rt = (RectTransform)go.transform;

        rt.SetParent(bubble, worldPositionStays: false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * (HexGridMath.BubbleDiameter * size);
        rt.anchoredPosition = Vector2.zero;
        rt.SetAsLastSibling();

        // Cada una cae girada por su lado: cuatro manchas idénticas se leen como un sello
        // repetido y no como algo que salpicó.
        rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));

        var image = go.GetComponent<Image>();
        image.sprite         = sprite;
        image.color          = tint;
        image.raycastTarget  = false;
        image.preserveAspect = true;

        // Un golpe al aparecer, y quieta: lo que se pidió es que no haya movimiento mientras está
        // encima, así que el gesto se gasta entero en el primer instante.
        for (float t = 0f; t < INK_BLOT_POP && go != null; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / INK_BLOT_POP);

            rt.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, Ease(p));
            yield return null;
        }

        if (go == null) yield break;
        rt.localScale = Vector3.one;

        yield return new WaitForSeconds(hold);

        for (float t = 0f; t < fade && go != null; t += Time.deltaTime)
        {
            image.color = new Color(tint.r, tint.g, tint.b, tint.a * (1f - Mathf.Clamp01(t / fade)));
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    // Un golpe de tamaño y vuelta a lo de antes.    // Un golpe de tamaño y vuelta a lo de antes. Lo usa la onda del Agua Clara al pasar por cada
    // burbuja, escalonado por la distancia, así que el tablero entero se mueve a su paso.
    //
    // Guarda y restaura la escala en vez de darla por 1: una burbuja puede venir de otro gesto, y
    // dejarla en 1 al terminar la encogería o la estiraría sin que nadie supiera por qué.
    IEnumerator Nudge(RectTransform rt, float delay, float duration, float amount)
    {
        if (rt == null) yield break;

        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (rt == null) yield break;

        Vector3 size = rt.localScale;
        float   span = Mathf.Max(0.05f, duration);

        for (float t = 0f; t < span; t += Time.deltaTime)
        {
            if (rt == null) yield break;

            rt.localScale = size * (1f + amount * Mathf.Sin(Mathf.Clamp01(t / span) * Mathf.PI));
            yield return null;
        }

        if (rt != null) rt.localScale = size;
    }

    // El centro de la pantalla, en coordenadas de mundo. Es donde sale el icono de un poder de
    // inicio, y para los que actúan en todo el tablero es también donde se quedan.
    Vector3 ScreenCenter()
    {
        var canvas = shotsLabel != null ? shotsLabel.canvas : null;

        return canvas != null
            ? ((RectTransform)canvas.rootCanvas.transform).TransformPoint(Vector3.zero)
            : grid.transform.position;
    }

    // Dónde actúa cada poder de inicio, en coordenadas de mundo: es a donde va su icono.
    Vector3 StartBoosterTarget(Booster booster)
    {
        var container = (RectTransform)grid.transform;

        switch (booster)
        {
            // Al contador de disparos, que es lo único que este poder toca.
            case Booster.Oxygen:
                return shotsLabel != null ? shotsLabel.transform.position : container.position;

            // A la perla de la recámara: de ahí salen las luces que siembran el tablero, así que
            // el icono entra justo donde va a empezar lo siguiente.
            case Booster.Rainbow:
                return cannon != null ? container.TransformPoint(cannon.CurrentBubbleLocal) : container.position;

            // A ningún sitio: se queda donde está y se apaga ahí, porque de ese punto sale la
            // onda. Los demás viajan a donde actúan; este ACTÚA en toda la pantalla, y una onda
            // que cubre la pantalla nace del centro de la pantalla.
            case Booster.ClearWater:
                return ScreenCenter();

            // Tampoco va a un sitio: las minas caen repartidas por TODO el tablero, de arriba
            // abajo, así que no hay un punto al que apuntar. Se apaga en el centro y desde ahí
            // salen las luces a plantarlas.
            case Booster.Mine:
                return ScreenCenter();

            // Al borde por el que va a entrar la ola, a la altura de las filas que se lleva.
            case Booster.LowTide:
                var art  = BoosterRules.ArtFor(Booster.LowTide);
                int rows = art != null && art.tideRows > 0 ? art.tideRows : DEFAULT_TIDE_ROWS;
                var band = BoosterRules.LowTideCells(grid, rows);

                if (band.Count == 0) return container.position;

                float top    = band.Min(c => HexGridMath.CellToLocalPos(c).y);
                float bottom = band.Max(c => HexGridMath.CellToLocalPos(c).y);

                return container.TransformPoint(new Vector3(-HexGridMath.DesignWidth / 2f, (top + bottom) / 2f, 0f));

            default:
                return container.position;
        }
    }

    // Suave a la entrada y a la salida. Lo usa el rebote del '+5': con tramos lineales el número
    // cambia de velocidad de golpe en el pico y el gesto se ve mecánico.
    static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // El contador sube de uno en uno, con su sonido y su golpe de tamaño.
    //
    // De uno en uno y no de un salto porque lo que se compró son CINCO disparos, no un número
    // distinto: contándolos se ve lo que entró. Y en un tiempo fijo, así que cuantos más entren
    // más rápido caen — un regalo grande se siente generoso en vez de lento.
    IEnumerator GainShots(int extra)
    {
        if (extra <= 0) yield break;

        // Cero significa "el valor por defecto" y no "de golpe": el campo es nuevo, y Unity rellena
        // un campo nuevo en un componente ya guardado con el cero del TIPO, no con lo que diga el
        // inicializador. Sin esto el efecto nacería apagado en la única escena que lo usa.
        float total = shotsGainDuration   > 0f ? shotsGainDuration   : DEFAULT_SHOTS_GAIN;
        float pulse = shotsGainPulse      > 0f ? shotsGainPulse      : DEFAULT_SHOTS_PULSE;
        float rise  = shotsGainPitchRise  > 0f ? shotsGainPitchRise  : DEFAULT_SHOTS_PITCH;
        float step  = total / extra;
        Vector3 size = shotsLabel != null ? shotsLabel.transform.localScale : Vector3.one;

        for (int i = 0; i < extra; i++)
        {
            AddShots(1);

            // El tono sube repartido entre todos los que entran, así que el recorrido es el mismo
            // con 5 que con 20 — igual que la duración.
            float climb = extra > 1 ? i / (float)(extra - 1) : 0f;

            if (shotsGainClip) AudioManager.Instance?.PlayCount(shotsGainClip, 1f + rise * climb);
            if (SaveManager.Vibration) MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.LightImpact);

            // El golpe entero dentro del paso: cada unidad tiene que tener el suyo, y uno que
            // durara más que el paso se comería al siguiente justo cuando caen rápido.
            for (float t = 0f; t < step; t += Time.deltaTime)
            {
                if (shotsLabel)
                    shotsLabel.transform.localScale = size * (1f + pulse * Mathf.Sin(Mathf.Clamp01(t / step) * Mathf.PI));

                yield return null;
            }
        }

        if (shotsLabel) shotsLabel.transform.localScale = size;
    }

    IEnumerator SeedRainbows()
    {
        var art    = BoosterRules.ArtFor(Booster.Rainbow);
        var travel = art?.seedTravel;
        var seeds  = BoosterRules.SeedCells(grid);

        bool travels = travel != null && travel.enabled;

        // Cero significa "usa el del contagio" y no "instantáneo": el campo es nuevo, y Unity
        // rellena un campo nuevo en un asset ya guardado con el cero del TIPO, no con el valor
        // que diga el inicializador. Sin esto, la siembra saldría de golpe en un catálogo que
        // nadie tocó todavía.
        float fade = art != null && art.seedFade > 0f ? art.seedFade : art?.claimFade ?? 0f;
        float step = travel != null && travel.stagger > 0f ? travel.stagger : art?.claimStep ?? 0f;

        // La luz es el glow del propio poder: lo que viaja no es una burbuja todavía, es lo que
        // va a convertir una. Si el catálogo no tiene glow se manda la perla misma, que se lee
        // igual de bien y evita que el efecto desaparezca por un campo sin llenar.
        Sprite light = BoosterRules.GlowFor(Booster.Rainbow) ?? grid.SpriteFor(BubbleColor.Rainbow);

        // Desde la perla que el jugador tiene delante, no desde el borde del tablero: lo que
        // cuenta el efecto es que el poder que acaba de pagar reparte desde su propia burbuja.
        Vector2 from = cannon != null ? cannon.CurrentBubbleLocal : Vector2.zero;

        for (int i = 0; i < seeds.Count; i++)
        {
            // Con viaje, cada burbuja se convierte CUANDO LLEGA su luz — es el mismo reloj que
            // usa la Madre para sus hijas, así lo que se ve y lo que pasa no se separan nunca.
            float at = travels ? travel.TotalFor(i) : i * step;

            if (travels)
                TravelingBubble.Send((RectTransform)grid.transform, from,
                                     HexGridMath.CellToLocalPos(seeds[i]), light, travel, i);

            grid.TurnIntoRainbow(seeds[i], at, fade);
            StartCoroutine(PlayClipAfter(travel?.arriveClip, at));
        }

        // Se espera a que la última llegue y acabe de teñirse: lo que venga detrás es otro regalo
        // y tiene que empezar cuando este haya terminado de contarse.
        float last = seeds.Count > 0 ? (travels ? travel.TotalFor(seeds.Count - 1) : (seeds.Count - 1) * step) : 0f;

        yield return new WaitForSeconds(last + fade);
    }

    // Planta las minas. Comparte TODO con la siembra de la Perla salvo en qué convierte: el
    // mismo reparto por bandas, el mismo viaje de luces, el mismo gesto al llegar.
    //
    // Y comparte el motivo: si las minas aparecieran de golpe, el jugador vería un tablero
    // distinto sin haber visto pasar nada, y lo que pagó no se cobra.
    IEnumerator PlantMines()
    {
        var art    = BoosterRules.ArtFor(Booster.Mine);
        var travel = art?.seedTravel;
        var cells  = BoosterRules.SeedCells(grid);

        bool travels = travel != null && travel.enabled;

        float fade = art != null && art.seedFade > 0f ? art.seedFade : DEFAULT_SEED_FADE;
        float step = travel != null && travel.stagger > 0f ? travel.stagger : DEFAULT_SEED_STEP;

        // La luz es el glow del poder; sin él, la propia burbuja de mina. Lo que viaja no es una
        // mina todavía: es lo que va a convertir una burbuja en mina.
        Sprite light = BoosterRules.GlowFor(Booster.Mine) ?? BoosterRules.BubbleSpriteFor(Booster.Mine);

        // Desde el centro de la pantalla, que es donde se apagó el icono. La Perla sale de su
        // perla porque la tiene delante; este poder no tiene burbuja en la recámara.
        Vector2 from = ((RectTransform)grid.transform).InverseTransformPoint(ScreenCenter());

        for (int i = 0; i < cells.Count; i++)
        {
            float at = travels ? travel.TotalFor(i) : i * step;

            if (travels)
                TravelingBubble.Send((RectTransform)grid.transform, from,
                                     HexGridMath.CellToLocalPos(cells[i]), light, travel, i);

            grid.TurnIntoMine(cells[i], at, fade);
            StartCoroutine(PlayClipAfter(art?.minePlantClip != null ? art.minePlantClip : travel?.arriveClip, at));
        }

        float last = cells.Count > 0 ? (travels ? travel.TotalFor(cells.Count - 1) : (cells.Count - 1) * step) : 0f;

        yield return new WaitForSeconds(last + fade);
    }

    // Lo que haya que explicar de este nivel, antes de soltar el control.
    //
    // Va acá y no al cargar: el jugador lee teniendo delante el tablero del que se le habla, ya
    // compuesto y con la burbuja en la recámara. Y como el control todavía no está suelto, no
    // puede disparar a ciegas por detrás del modal.
    IEnumerator ExplainMechanics()
    {
        if (tutorialPanel == null) yield break;

        // Los que pide el JSON del nivel, en el orden en que están escritos.
        if (_level.tutorials != null)
            foreach (string id in _level.tutorials)
                yield return Explain(id);

        // Y el del rescate, que no hace falta escribir: el objetivo del nivel ya lo dice. Si
        // además está en la lista, el panel no lo repite.
        if (_level.objective != null && _level.objective.type == "rescue")
            yield return Explain(TUTORIAL_RESCUE);
    }

    IEnumerator Explain(string id)
    {
        bool waiting = true;

        // Si este tutorial presenta un booster, el jugador se lleva uno para probarlo ahí mismo.
        //
        // Se mira Pending ANTES de Show porque Show marca el tutorial como visto: preguntando
        // después, nunca regalaría. Y como el tutorial sale una sola vez en la vida, el regalo
        // también — volver a entrar al nivel, o reiniciarlo, no da más.
        var booster = BoosterRules.BoosterForTutorial(id);
        if (booster != Booster.None && tutorialPanel.Pending(id))
        {
            SaveManager.GrantBooster(booster, BoosterRules.TUTORIAL_GRANT);

            // Equipar es cosa de los de inicio: son los que se eligen en la pantalla previa y los
            // que ApplyStartBoosters consume leyendo el loadout. Uno de gameplay ya no necesita
            // pasar por acá —su ícono está en el HUD siempre, esté equipado o no— y colarlo
            // gastaría uno de los tres huecos que le hacen falta al jugador para otra cosa.
            if (BoosterRules.IsStart(booster)) BoosterLoadout.Equip(booster);

            // Solo los de gameplay se cargan en la recámara. Pasar uno de inicio por LoadBooster
            // lo trataría como una burbuja especial y lo dejaría puesto sin poder dispararse.
            //
            // Y el de inicio no se APLICA acá aunque lo parezca: ApplyStartBoosters lo cobra y lo
            // encola, y la cola se reproduce al cerrarse el modal (ver PlayStartEffects, que corre
            // después de ExplainMechanics). Así el poder que el tutorial acaba de explicar entra
            // con su icono y su efecto, igual que si lo hubiera traído equipado.
            if (BoosterRules.IsInGame(booster)) _autoLoadBooster = booster;
            else                                ApplyStartBoosters();
        }

        if (!tutorialPanel.Show(id, () => waiting = false)) yield break;

        while (waiting) yield return null;
    }

    // La escena se activa a mitad de la animación de burbujas de la transición, así que Start()
    // corre con la pantalla todavía tapada y no se destapa hasta unos 0,3 s después. Animar ahí es
    // animar para nadie: se oye el sonido y no se ve nada (reportado por Diego al entrar a un
    // nivel corto, donde la aparición de la burbuja es lo único que hay que ver y dura 0,45 s).
    //
    // Esperar no descoloca nada: la vista y la recámara ya quedaron puestas de forma síncrona en
    // Start, así que el telón se levanta sobre el nivel ya compuesto y recién entonces se mueve.
    //
    // El tope es por si la transición queda colgada. Vale más ver la animación tarde que un nivel
    // que no arranca nunca.
    IEnumerator WaitForCurtain()
    {
        const float TIMEOUT = 5f;

        for (float t = 0f; SceneTransition.Covering && t < TIMEOUT; )
        {
            // El primer frame después de cargar una escena puede traer un deltaTime enorme; el
            // tope por frame evita que el timeout se consuma de una.
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            yield return null;
        }

        if (openBeat > 0f) yield return new WaitForSecondsRealtime(openBeat);
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
        if (tutorialPanel    == null) Debug.LogWarning("[GameplayController] Falta asignar 'Tutorial Panel' en el Inspector — las mecánicas nuevas no se le van a explicar a nadie.");

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

    void OnBubbleLanded(Vector2Int landedCell, Vector2Int? struckCell, Booster special)
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

        // Un poder de área no revienta al tocar: se queda cargando un momento. Todo lo que viene
        // después del estallido tiene que esperar a que ocurra, así que se va por una corrutina.
        //
        // Quién tiene área lo decide BoosterRules y no una lista de casos acá: así un poder nuevo
        // entra sin tocar este archivo.
        if (BoosterRules.CellsHitBy(special, landedCell, struckCell, grid).Count > 0)
        {
            StartCoroutine(ArmAndBlast(special, landedCell, struckCell));
            return;
        }

        var removed = ResolveMatchAndDrop(landedCell);

        // La aterrizada va APARTE y no dentro de 'removed': una mina estalla con lo que le pasa
        // al lado y aterrizar es eso, pero si el disparo no matcheó esa burbuja se QUEDA — y
        // metida en el montón de las que se fueron, el objetivo la descontaba igual.
        AfterRemoval(removed, landedCell);
    }

    // El poder se pega, carga, y recién entonces estalla.
    //
    // La espera no es decoración: sin ella la burbuja cae y revienta en el mismo frame, y no se
    // llega a ver que fue ELLA la que explotó —se lee como un match enorme y raro—. Con la carga,
    // el jugador ve la bola que acaba de colocar encenderse, y la explosión es su consecuencia.
    //
    // El tiempo de carga sale del catálogo de cada poder: la bomba tiene mecha, la raya apenas
    // un instante. En cero, estalla en cuanto toca.
    IEnumerator ArmAndBlast(Booster booster, Vector2Int center, Vector2Int? struck)
    {
        // Sin input ni pausa mientras carga. Otro disparo aterrizando a mitad de la cuenta
        // cambiaría el tablero sobre el que se calculó la zona que el jugador acaba de ver
        // marcada; y como la pausa de este juego no congela el tiempo, pausar durante la carga
        // dejaría la bomba estallando detrás del panel.
        cannon.SetInputEnabled(false);
        if (openPausedButton) openPausedButton.interactable = false;

        var art   = BoosterRules.ArtFor(booster);
        var blast = art?.burstBlast ?? bombBlast;

        if (grid.TryGetBubble(center, out var armed))
            BombBlast.Charge((RectTransform)armed.transform, blast);

        // Arranca con la luz, no con la explosión: es el sonido de la mecha.
        AudioManager.Instance?.PlaySfx(art?.chargeClip);

        float charge = Mathf.Max(0f, blast.chargeTime);
        float lead   = Mathf.Clamp(art?.clipLead ?? 0f, 0f, charge);

        if (charge > lead) yield return new WaitForSeconds(charge - lead);
        AudioManager.Instance?.PlaySfx(art?.burstClip);
        if (lead > 0f)     yield return new WaitForSeconds(lead);

        if (_levelEnded) yield break;   // el nivel se acabó por otro lado mientras cargaba

        AfterRemoval(ResolveBoosterBlast(booster, center, struck));

        // La Tinta hace su match como cualquier burbuja y RECIÉN DESPUÉS salpica lo que queda
        // alrededor (pedido de Diego). En este orden, las que el impacto se llevó ya no están en
        // el tablero, así que la mancha no se gasta en burbujas que estaban muriéndose.
        //
        // El color que aplica no se ve afectado: un poder no consume la cola, así que lo que hay
        // en la recámara después de resolver es lo mismo que había antes.
        if (BoosterRules.Tints(booster)) SplashInk(center);

        // Solo si el nivel sigue: con el panel de victoria o derrota ya abierto, devolver el
        // control sería dejar disparar por detrás de él.
        if (_levelEnded) yield break;

        cannon.SetInputEnabled(true);
        if (openPausedButton) openPausedButton.interactable = true;

        // Y que el cañón revise lo que tiene servido: el estallido pudo llevarse el último rojo
        // del tablero, y el relevo —que es quien normalmente pregunta— no corre al usar un poder.
        if (cannon) cannon.CheckAutoSwap();
    }

    // Un golpe es que reviente algo PEGADO a ella: da igual que se fuera en un match, en una
    // explosión o que acabe de aterrizar. Cada golpe le quita la capa de fuera, y la que ya está
    // a la vista queda liberada (GDD §2.2).
    //
    // Una sola burbuja no acusa dos golpes de la misma jugada: un match de doce que la rodea por
    // tres lados es UN golpe, no tres, o tres capas se irían de una.
    void HitRescuesTouching(HashSet<Vector2Int> touched, Vector2Int? landed = null)
    {
        if (_creatureAt.Count == 0) return;

        var hit = new HashSet<Vector2Int>();

        foreach (var cell in touched)
            foreach (var around in HexGridMath.GetNeighbors(cell))
                Consider(around);

        if (landed.HasValue)
            foreach (var around in HexGridMath.GetNeighbors(landed.Value))
                Consider(around);

        foreach (var cell in hit)
            // El golpe se ve cuando revienta lo que lo causó, no antes: los pops de un racimo van
            // escalonados y la capa que cae a destiempo se adelanta a su motivo.
            StartCoroutine(HitRescue(cell, NearestPop(cell)));

        // Esto es la regla del CONTENEDOR, el que no tiene color: lo abre lo que pase a su lado.
        //
        // La que sí tiene 'match' queda fuera a propósito: esa se abre entrando en un match de su
        // color, y lo resuelve ResolveMatchAndDrop. Metida también acá, cualquier vecina que
        // reventara se la llevaba — un match azul rescataba a la que pedía verdes (reportado por
        // Diego).
        void Consider(Vector2Int cell)
        {
            if (!_creatureAt.ContainsKey(cell))                           return;
            if (!grid.TryGetBubble(cell, out var view) || view == null)   return;
            if (view.CreatureMatches)                                     return;

            hit.Add(cell);
        }
    }

    IEnumerator HitRescue(Vector2Int cell, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (!grid.TryGetBubble(cell, out var view) || view == null) yield break;

        // Cuál es la capa que va a saltar con este golpe, para poder darle SU sonido. Se mira
        // antes de romperla: después ya no queda rastro de cuál era.
        int breaking = view.LayersLeft - 1;

        // Con capas, el golpe se queda en el caparazón. Sin ellas, se la lleva.
        if (!view.BreakLayer())
        {
            _recipeAt.TryGetValue(cell, out var shell);

            AudioManager.Instance?.PlaySfx(RescueBubbleCatalog.LayerBurst(shell, breaking));
            yield break;
        }

        // Y estalla como cualquier otra. Quitarla del registro a secas solo la saca de las
        // cuentas: el objeto se quedaba en pantalla para siempre, sin reventar y sin destruirse
        // (reportado por Diego).
        view.PlayPopAnimation(0f, popClip);

        grid.RemoveBubble(cell);

        var freed = new HashSet<Vector2Int> { cell };

        // Lo que colgaba de ella se cae: la criatura podía ser lo único que sostenía media rama,
        // y sin esto se quedaría flotando en el agua.
        CollapseFloating(freed);

        AfterRemoval(freed);
    }

    // Cuándo revienta lo más cercano a esa celda. Es el instante en que la burbuja acusa el
    // golpe: de los vecinos que se van, manda el PRIMERO.
    float NearestPop(Vector2Int cell)
    {
        float at = float.MaxValue;

        foreach (var around in HexGridMath.GetNeighbors(cell))
            if (_poppedAt.TryGetValue(around, out float t)) at = Mathf.Min(at, t);

        return at == float.MaxValue ? 0f : at;
    }

    // ---- El objetivo del nivel -------------------------------------------------------------

    bool AllRescued
    {
        get
        {
            foreach (int left in _remaining) if (left > 0) return false;

            return true;
        }
    }

    // Qué pide este nivel, listo para contarlo y para enseñarlo. Se llama DESPUÉS de poblar el
    // grid: una criatura escrita donde no hay burbuja no cuenta, o el nivel sería imposible de
    // ganar y el hub pediría algo que no está.
    void SetUpObjective()
    {
        _targets.Clear();
        _remaining.Clear();
        _shown.Clear();
        _creatureAt.Clear();
        _recipeAt.Clear();
        _freed        = 0;
        _rescueUnits  = 0;

        // Se agrupan por lo que LLEVAN, no por la receta: tres coquíes en tres recetas distintas
        // son un solo hueco con un 3, que es lo que el jugador entiende. Un hueco por receta le
        // pediría distinguir "coquí bajo hielo" de "coquí suelto" como si fueran dos objetivos.
        if (_level.objective != null)
            foreach (var at in _level.objective.Rescues())
            {
                if (at == null || !grid.IsOccupied(at.Cell)) continue;   // sin burbuja no hay nada que contar

                string holds = at.Holds();
                int    index = _targets.IndexOf(holds);

                if (index < 0)
                {
                    index = _targets.Count;
                    _targets.Add(holds);
                    _remaining.Add(0);
                    _shown.Add(0);
                }

                _creatureAt[at.Cell] = index;
                _recipeAt[at.Cell]   = at.Recipe;
                _rescueUnits++;
                _remaining[index]++;
                _shown[index]++;
            }

        ShowObjective();
    }

    void ShowObjective()
    {
        if (objectiveHub == null) return;

        var goals = new List<(Sprite icon, int total)>();

        if (_targets.Count > 0)
            for (int i = 0; i < _targets.Count; i++)
                goals.Add((CreatureCatalog.Icon(_targets[i]), _remaining[i]));

        // Sin rescate, lo que se cuenta son burbujas: el hub sirve igual para "limpia el tablero"
        // y el jugador ve cuánto le queda en vez de deducirlo mirando.
        else if (grid.CellCount > 0)
        {
            goals.Add((clearAllIcon != null ? clearAllIcon : grid.SpriteFor(FirstLevelColor()), grid.CellCount));
            _shown.Add(grid.CellCount);
        }

        objectiveHub.Show(goals);
    }

    BubbleColor FirstLevelColor() =>
        _level.available_colors != null && _level.available_colors.Count > 0
            ? BubbleColorExtensions.Parse(_level.available_colors[0])
            : BubbleColor.Red;

    // Lo que esta remoción se llevó del objetivo. Baja el contador que toque y refresca su hueco.
    void CountRescued(HashSet<Vector2Int> removed)
    {
        if (_shown.Count == 0) return;

        foreach (var cell in removed)
        {
            // "Limpia el tablero": cuenta todas. Con rescate, solo las que llevaban criatura.
            int index = 0;

            if (_targets.Count > 0)
            {
                if (!_creatureAt.TryGetValue(cell, out index)) continue;

                _creatureAt.Remove(cell);   // ya contada: una celda no libera dos veces

                // La cuenta de verdad baja YA: de ella depende si el nivel está ganado, y
                // esperar a la animación lo declararía a destiempo.
                _remaining[index] = Mathf.Max(0, _remaining[index] - 1);

                _freed++;

                _recipeAt.TryGetValue(cell, out var recipe);

                // Dos sonidos y no uno: el cristal que se rompe y el bicho que sale. Son dos
                // cosas distintas y juntarlas en un clip obligaría a tener uno por criatura.
                StartCoroutine(PlayClipAfter(RescueBubbleCatalog.Burst(recipe),            PopTime(cell)));
                StartCoroutine(PlayClipAfter(CreatureCatalog.RescueClip(_targets[index]),  PopTime(cell)));

                // El destello va en el MISMO sitio para los dos caminos —la que entra en un match
                // y la que abre un vecino— porque los dos acaban acá. Puesto en cada uno serían
                // dos efectos que mantener iguales.
                StartCoroutine(SparkleAfter(HexGridMath.CellToLocalPos(cell),
                                            rescueSparkle.glowSize > 0f ? rescueSparkle : bombSparkle,
                                            PopTime(cell)));

                // Lo rescatado VUELA al hub, y el número baja cuando llega. Que el contador se
                // mueva solo, con la criatura todavía en el tablero, deja el objetivo y lo que
                // pasa en pantalla como dos cosas sin relación — y lo que se compró con ese
                // disparo es justamente la relación entre las dos (pedido de Diego).
                // El punto lo da el grid, que es quien sabe cómo está montado su contenedor. No
                // se le pregunta a la burbuja: para cuando esto corre ya salió de la lista.
                StartCoroutine(FlyToHub(grid.WorldOf(cell), _targets[index], index, PopTime(cell)));
                continue;
            }

            // Y el número de pantalla baja de uno en uno, cada uno cuando SU burbuja revienta. Se
            // descuenta en vez de fijar un valor: las de un mismo objetivo revientan en cualquier
            // orden, y fijando valores el número saltaría hacia atrás.
            StartCoroutine(TickObjective(index, PopTime(cell)));
        }
    }

    // El icono sale de la burbuja y se va al hub, dejando chispas. Vive en el canvas raíz y no en
    // el tablero: el grid está recortado por arriba —lo tapa el panel del HUD— y un vuelo que
    // sube hasta ahí se cortaría justo donde tiene que llegar.
    IEnumerator FlyToHub(Vector3 fromWorld, string creatureId, int index, float delay)
    {
        _pendingTicks++;

        if (delay > 0f) yield return new WaitForSeconds(delay);

        _pendingTicks--;

        var slot = objectiveHub != null ? objectiveHub.SlotAt(index) : null;
        var icon = CreatureCatalog.Icon(creatureId);

        // Sin hub o sin arte no hay vuelo que enseñar, pero el objetivo sí tiene que bajar: el
        // efecto es el adorno, la cuenta es el juego.
        if (slot == null || icon == null || shotsLabel == null || shotsLabel.canvas == null)
        {
            Land(index);
            yield break;
        }

        var root = (RectTransform)shotsLabel.canvas.rootCanvas.transform;

        // TODO el vuelo va en coordenadas de MUNDO, de punta a punta. Convertir a coordenadas del
        // canvas obliga a acertar con el pivote y el anclaje de cada rect por el camino, y basta
        // que uno no esté centrado para que el icono salga corrido de su burbuja — pasó dos veces
        // (reportado por Diego). En mundo no hay nada que acertar: el punto es el punto.
        Vector3 from = fromWorld;
        Vector3 to   = slot.position;

        var flyer = new GameObject("Rescued", typeof(RectTransform), typeof(Image)) { hideFlags = HideFlags.DontSave };
        var rt    = (RectTransform)flyer.transform;

        rt.SetParent(root, false);
        rt.SetAsLastSibling();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * HexGridMath.BubbleDiameter;
        rt.position  = from;

        var image = flyer.GetComponent<Image>();

        image.sprite         = icon;
        image.raycastTarget  = false;
        image.preserveAspect = true;

        // Nace pequeño para que el crecimiento SE VEA. Naciendo a tamaño natural, el golpe de
        // zoom es de 1 a 1,2 y en un icono de 90 píxeles eso no se nota (reportado por Diego).
        rt.localScale = Vector3.one * RESCUE_POP_FROM;

        // Lo vivo tiembla y lo de piedra no. Sale de en qué lista del catálogo esté: una perla
        // o un cofre moviéndose como gelatina se leen como de goma, y un caracol que sube tieso
        // parece un icono y no un bicho al que acaban de soltar.
        bool jelly = CreatureCatalog.Creature(creatureId);

        float time  = rescueFlight > 0f ? rescueFlight : DEFAULT_RESCUE_FLIGHT;
        float trail = rescueTrail  > 0f ? rescueTrail  : DEFAULT_RESCUE_TRAIL;
        float spark = 0f;

        // Antes de salir: crece en el sitio y espera un instante. Es lo que separa "la burbuja
        // reventó" de "esto de dentro se soltó" — saliendo disparado en el mismo frame, las dos
        // cosas se leen como una sola y no se ve qué se rescató.
        yield return Swell(rt, RESCUE_POP_TIME, RESCUE_POP_SCALE);

        // Y donde lo de piedra se queda quieto un instante, lo vivo rebota. Es el mismo hueco de
        // tiempo: el rebote no retrasa la salida, solo la llena.
        if (jelly) yield return Wobble(rt, RESCUE_POP_HOLD + JELLY_EXTRA, RESCUE_POP_SCALE);
        else       yield return new WaitForSeconds(RESCUE_POP_HOLD);

        var mid = ControlPoint(from, to, fromWorld);

        Vector3 last = from;

        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / time);

            rt.position = Bezier(from, mid, to, p);

            // Del tamaño del golpe al del hueco del hub: el zoom se gasta POR EL CAMINO, así que
            // lo que llega arriba ya tiene el tamaño del icono y no da un salto al aterrizar.
            float grow = Mathf.Lerp(RESCUE_POP_SCALE, 0.55f, p);

            // El temblor va MULTIPLICANDO ese tamaño, no sustituyéndolo: así el bicho sigue
            // encogiendo hacia el hueco del hub mientras tiembla, en vez de quedarse clavado.
            rt.localScale = jelly ? Vector3.one * grow + Jelly.Pulse(t, JELLY_FLY) * grow
                                  : Vector3.one * grow;

            if ((spark += Time.deltaTime) >= trail)
            {
                spark = 0f;

                // Hacia dónde va el icono en este instante, para poder esparcir las chispas a lo
                // ANCHO del camino y no a lo largo. Sin esto el desvío salía en direcciones de la
                // pantalla y la estela se leía como una raya (reportado por Diego).
                Vector3 step = rt.position - last;
                Vector3 side = step.sqrMagnitude > 0.0001f
                             ? new Vector3(-step.y, step.x, 0f).normalized
                             : Vector3.right;

                // El ancho de la estela es el del icono: es lo que la hace una estela y no un
                // hilo. Se mide del icono que vuela, así que encoger el icono encoge el rastro.
                float width = rt.sizeDelta.x * rt.localScale.x;

                // Un puñado cada vez, no una. Con una por tanda sale una hilera de cuentas; lo
                // que parece polvo de estrellas es un grupito disperso en cada paso.
                int many = Random.Range(TRAIL_PER_STEP, TRAIL_PER_STEP * 2 + 1);

                for (int i = 0; i < many; i++)
                {
                    // A lo ancho, repartido por todo el ancho del icono; y un poco adelante o
                    // atrás, para que el grupo no quede alineado en una fila perpendicular.
                    Vector3 at = rt.position
                               + side * (Random.Range(-0.5f, 0.5f) * width)
                               + step.normalized * (Random.Range(-0.4f, 0.4f) * width);

                    StartCoroutine(TrailMote(root, at));
                }

                // Las chispas nacen encima de todo y el icono se vuelve a subir detrás de ellas.
                // Colocándolas por debajo había que fiarse de que nada en el canvas las tapara, y
                // eso era justo lo que no se podía dar por sabido: no se veían (reportado por
                // Diego, tres veces). Encima de todo no hay nada que suponer.
                rt.SetAsLastSibling();
            }

            last = rt.position;

            yield return null;
        }

        Destroy(flyer);
        Land(index);
    }

    void Land(int index)
    {
        if (index >= _shown.Count) return;

        _shown[index] = Mathf.Max(0, _shown[index] - 1);

        objectiveHub?.SetRemaining(index, _shown[index]);
        objectiveHub?.Bump(index);
    }

    // Un punto de luz que se queda atrás y se apaga. Son los que convierten el vuelo en un rastro.
    //
    // El sprite es el del faro y no el resplandor corriente: aquel es una gaussiana con el núcleo
    // muy estrecho, y a veinte píxeles de ancho lo sólido eran tres — la estela entera se veía
    // como un alfiler (reportado por Diego). El del faro tiene meseta: lo que se dibuja es casi
    // todo el ancho que se le pide.
    //
    // Una chispa de las que quedan por el camino. No forman una franja sólida: son puntos de luz
    // sueltos, de tamaños distintos y apartados del camino, que se encienden y se apagan — que es
    // lo que hace el rastro del ejemplo que pasó Diego.
    //
    // Por eso la de cuatro puntas y no el disco de resplandor: un disco da un trazo de pintura y
    // esto tiene que leerse como polvo de estrellas, con brillos que titilan.
    //
    IEnumerator TrailMote(RectTransform root, Vector3 at)
    {
        var go = new GameObject("RescueMote", typeof(RectTransform), typeof(Image)) { hideFlags = HideFlags.DontSave };
        var rt = (RectTransform)go.transform;

        // Cada una de su tamaño, y con mucha diferencia entre la mayor y la menor: lo que hace
        // que un grupo se lea como polvo y no como un patrón es que no haya dos iguales.
        float size = (rescueTrailSize > 0f ? rescueTrailSize : DEFAULT_TRAIL_SIZE)
                   * HexGridMath.BubbleDiameter * Random.Range(0.3f, 1.3f);

        rt.SetParent(root, false);
        rt.SetAsLastSibling();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * size;

        // El sitio ya viene repartido a lo ancho del camino desde el vuelo, que es el único que
        // sabe hacia dónde va el icono en este instante.
        rt.position = at;

        // Y girada al azar, que cuatro puntas siempre en cruz delatan que es el mismo dibujo.
        rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));

        var image = go.GetComponent<Image>();
        var tint  = rescueTrailColor.a > 0f ? rescueTrailColor : Color.white;

        image.sprite        = SparkleTextures.Mote;
        image.raycastTarget = false;

        // La vida son dos tramos: lo que aguanta puesta y lo que tarda en irse. En segundos y no
        // en fracción del vuelo, porque lo que se está ajustando acá es cuánto dura el rastro en
        // pantalla, y eso no tiene por qué cambiar si un día el icono vuela más rápido.
        float hold = rescueTrailHold > 0f ? rescueTrailHold : DEFAULT_TRAIL_HOLD;
        float life = hold + TRAIL_FADE;

        // Cada una empieza su titileo en un punto distinto del suyo. Arrancando todas a la vez,
        // la estela entera late de golpe en lugar de centellear.
        float phase = Random.Range(0f, 0.25f);

        // Nace ENCENDIDA y a su tamaño. Antes subía de cero durante la primera parte de su vida,
        // y eso son los primeros frames —los únicos en los que está pegada al icono y se la mira—
        // dibujando algo transparente.
        image.color   = tint;
        rt.localScale = Vector3.one;

        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            if (image == null) yield break;

            // Encendida del todo mientras dura la espera, y a partir de ahí bajando.
            float lit = t < hold ? 1f : 1f - (t - hold) / TRAIL_FADE;

            // Y encima titila, sin llegar a apagarse del todo en ningún momento del latido.
            float twinkle = 0.8f + 0.2f * Mathf.Sin((t + phase) * TWINKLE_HZ * Mathf.PI * 2f);

            image.color   = new Color(tint.r, tint.g, tint.b, tint.a * Mathf.Clamp01(lit) * twinkle);
            rt.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(lit));
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    // Más de una burbuja y media, que parece mucho y no lo es: la chispa de cuatro puntas gasta
    // su ancho en brazos muy finos, así que de una imagen de 90 píxeles lo que de verdad brilla
    // son los veinte del medio. A una burbuja todavía se veían chiquitas (reportado por Diego).
    const float DEFAULT_TRAIL_SIZE = 1.6f;

    // Cuánto aguanta encendida cada chispa y cuánto tarda en apagarse después, en segundos;
    // cuántas suelta como MÍNIMO cada tanda (el máximo es el doble); y a qué ritmo titila.
    const float DEFAULT_TRAIL_HOLD = 0.7f;
    const float TRAIL_FADE         = 0.45f;
    const int   TRAIL_PER_STEP     = 3;
    const float TWINKLE_HZ         = 6f;

    // El punto que curva el vuelo. El desvío va PERPENDICULAR al camino y no en los ejes de la
    // pantalla: desplazándolo en vertical, una burbuja que queda justo debajo de su hueco movía
    // el control sobre la propia recta y el vuelo salía derecho (reportado por Diego).
    //
    // Y se curva hacia AFUERA, al lado de la pantalla en el que esté la burbuja: la de la
    // izquierda sale abriéndose por la izquierda y la de la derecha por la derecha. Hacia adentro
    // cruzaría por encima del tablero, tapando lo que el jugador está mirando.
    //
    // El lado lo decide dónde ESTÁ la burbuja y no hacia dónde va: comparando origen y destino,
    // una que queda casi debajo de su hueco los tiene a unos píxeles y el lado salía uno u otro
    // por azar, así que dos rescates seguidos curvaban distinto sin motivo (reportado por Diego).
    Vector3 ControlPoint(Vector3 from, Vector3 to, Vector3 fromWorld)
    {
        var   dir  = to - from;
        float away = dir.magnitude * (rescueBend > 0f ? rescueBend : DEFAULT_RESCUE_BEND);

        if (away <= 0f) return (from + to) * 0.5f;

        var perp = new Vector3(-dir.y, dir.x, 0f).normalized;

        // Hacia AFUERA, al lado de la pantalla donde está la burbuja. El lado se mira en
        // píxeles de pantalla y no en el mundo: en mundo el centro depende de dónde esté el
        // canvas, y en pantalla es siempre la mitad del ancho.
        bool left = Camera.main != null
            ? RectTransformUtility.WorldToScreenPoint(null, fromWorld).x < Screen.width * 0.5f
            : from.x < to.x;

        if (perp.x > 0f == left) perp = -perp;

        return (from + to) * 0.5f + perp * away;
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;

        return u * u * a + 2f * u * t * b + t * t * c;
    }

    const float DEFAULT_RESCUE_FLIGHT = 0.55f;
    const float DEFAULT_RESCUE_TRAIL  = 0.012f;
    const float DEFAULT_RESCUE_BEND   = 0.3f;

    // El golpe de salida: crece a 1,2 donde estaba, se queda un instante y recién entonces sale.
    const float RESCUE_POP_FROM  = 0.45f;
    const float RESCUE_POP_TIME  = 0.28f;
    const float RESCUE_POP_SCALE = 1.2f;
    const float RESCUE_POP_HOLD  = 0.12f;

    // La gelatina: cuánto se deforma al salir y cuánto por el camino, y lo que se alarga la
    // espera de salida para que el rebote quepa. El ritmo y la amortiguación están en 'Jelly'.
    const float JELLY_POP   = 0.22f;
    const float JELLY_FLY   = 0.10f;
    const float JELLY_EXTRA = 0.25f;

    // Cuántos descuentos hay agendados y todavía sin ocurrir. Es lo que convierte el número de
    // pantalla en algo DERIVADO del tablero en vez de una cuenta paralela.
    int _pendingTicks;

    IEnumerator TickObjective(int index, float delay)
    {
        _pendingTicks++;

        if (delay > 0f) yield return new WaitForSeconds(delay);

        _pendingTicks--;

        if (index >= _shown.Count) yield break;

        // Limpiar el tablero: el número sale del grid, que es quien lo sabe, más los descuentos
        // que faltan por verse. Así la animación escalona igual pero el valor no puede quedar
        // desincronizado — y no lo quedaba por los pops sino por todo lo demás que mueve el
        // tablero sin pasar por acá (reportado por Diego: decía 16 con treinta y pico puestas).
        //
        // Con rescate no sirve: lo que se cuenta son criaturas y el grid no sabe cuántas quedan.
        _shown[index] = _targets.Count > 0
            ? Mathf.Max(0, _shown[index] - 1)
            : grid.CellCount + _pendingTicks;

        objectiveHub?.SetRemaining(index, _shown[index]);
    }

    // Lo que hay que mirar después de cualquier remoción, venga de un match o de una explosión.
    // ---- Elegir sobre el tablero (Punto Débil) --------------------------------------------
    //
    // Vive acá y no en un componente aparte para que no haya nada que cablear en la escena: el
    // velo y el aviso se construyen por código, igual que la entrada de los poderes de inicio.
    //
    // El modo es poco más que un interruptor. Tocar una burbuja ya llegaba hasta acá por
    // GridController.OnBubbleTapped —lo usa el cañón para apuntar de un toque— así que lo único
    // que cambia es QUIÉN atiende ese toque mientras dura.
    Booster  _picking = Booster.None;
    Image    _pickVeilImage;
    bool     _pickBorrowed;   // el renglón es del hint de disparo: se devuelve, no se destruye
    float    _pickVeilAlpha;   // el alfa configurado: MakeVeil nace en cero y lo perdería
    TMP_Text _pickHint;

    public bool Picking => _picking != Booster.None;

    // Devuelve si el modo arrancó. Falso no es un error: puede no quedar ninguno, o el nivel
    // puede haber terminado mientras el dedo iba al botón.
    public bool BeginPick(Booster booster)
    {
        if (_levelEnded || Picking || grid == null)      return false;
        if (!BoosterRules.PicksOnBoard(booster))         return false;
        if (SaveManager.BoosterCount(booster) <= 0)      return false;

        _picking = booster;

        // El cañón se calla: si no, el mismo toque apuntaría y dispararía además de gastar el
        // poder. Los dos escuchan el MISMO evento del grid.
        cannon?.SetInputEnabled(false);

        grid.OnBubbleTapped    += HandlePicked;
        grid.OnBubblePressed   += ShowPickPreview;

        // Arrastrar RE-APUNTA en vez de cancelar: el dedo recorre el tablero y el objetivo lo
        // sigue, y lo que haya debajo al soltar es lo que se lleva. Cancelar se hace soltando
        // fuera del tablero o tocando otra vez el botón (pedido de Diego).
        grid.OnBubbleHovered   += ShowPickPreview;
        grid.OnBubbleUnhovered += PickLeft;
        grid.OnBubbleDragEnd   += ReleasePick;

        ShowPickVeil(booster);
        return true;
    }

    // Se cancela desde el mismo botón del HUD que lo encendió, igual que descargar un poder de la
    // recámara: encender y apagar tienen que costar lo mismo, que es nada.
    public void CancelPick()
    {
        if (!Picking) return;

        EndPick();
        cannon?.SetInputEnabled(true);
    }

    void EndPick()
    {
        if (grid != null)
        {
            grid.OnBubbleTapped   -= HandlePicked;
            grid.OnBubblePressed  -= ShowPickPreview;
            grid.OnBubbleHovered  -= ShowPickPreview;
            grid.OnBubbleUnhovered -= PickLeft;
            grid.OnBubbleDragEnd  -= ReleasePick;
        }

        ClearPickPreview();

        _picking = Booster.None;

        HidePickVeil();
    }

    void HandlePicked(Vector2Int cell)
    {
        var booster = _picking;

        if (!grid.IsOccupied(cell)) return;   // el tablero cambió entre el toque y esto

        ClearPickPreview();
        EndPick();

        // Se cobra DESPUÉS de saber que había dónde usarlo, y si el cobro falla no pasa nada: sin
        // existencias no se debería haber podido entrar al modo, pero cobrar y no hacer nada es
        // la única forma de que el jugador pierda algo sin verlo.
        if (!SaveManager.ConsumeBooster(booster)) { cannon?.SetInputEnabled(true); return; }

        AudioManager.Instance?.PlaySfx(BoosterRules.ArtFor(booster)?.burstClip);

        AfterRemoval(ResolveBoosterBlast(booster, cell, cell));

        // No gasta disparo (decisión de Diego): se paga aparte, y cobrarle además un tiro lo
        // dejaría peor que disparar bien. Así que el turno sigue donde estaba.
        if (!_levelEnded)
        {
            cannon?.SetInputEnabled(true);
            cannon?.CheckAutoSwap();
        }
    }

    // Lo que se llevaría el toque que el dedo tiene apoyado ahora mismo.
    //
    // La vista previa existe porque este poder no gasta disparo: lo único que cuesta equivocarse
    // es perderlo entero, así que el jugador tiene que poder VER qué compra y deslizar fuera si
    // no era eso. El arrepentimiento sale gratis del propio sistema de eventos — 'Up' llega
    // siempre y 'Click' solo si el dedo acabó encima.
    readonly List<Vector2Int> _pickPreview = new();

    Vector2Int? _pickAt;             // sobre qué celda está el dedo ahora

    void ShowPickPreview(Vector2Int cell)
    {
        if (!Picking || !grid.IsOccupied(cell)) return;
        if (_pickAt == cell) { _pickLeaving = false; return; }   // sigue donde estaba

        _pickLeaving = false;

        ClearPickPreview();
        _pickAt = cell;

        foreach (var hit in BoosterRules.CellsHitBy(_picking, cell, cell, grid))
        {
            if (!grid.TryGetBubble(hit, out var view) || view == null) continue;

            _pickPreview.Add(hit);
            StartCoroutine(Swell((RectTransform)view.transform, PICK_PREVIEW_TIME, PICK_PREVIEW_SCALE));
        }

        // Los MISMOS aros con los que la Bomba marca su zona al apuntar. El jugador ya sabe lo
        // que significan, y un resplandor propio para decir lo mismo sería hacérselo aprender
        // dos veces.
        cannon?.Trajectory?.MarkCells(_pickPreview);
    }

    // El dedo sale de la burbuja marcada. No se borra en el acto: pasando de una a la de al lado,
    // Unity manda primero la salida y después la entrada, así que borrar aquí apagaría lo que la
    // entrada está a punto de encender. Se espera un frame, y si para entonces entró en otra, no
    // había nada que borrar.
    bool _pickLeaving;

    void PickLeft(Vector2Int cell)
    {
        if (!Picking || _pickAt != cell) return;

        _pickLeaving = true;
        StartCoroutine(ClearIfLeft());
    }

    IEnumerator ClearIfLeft()
    {
        yield return null;

        if (_pickLeaving) ClearPickPreview();
    }

    // Y al soltar se lleva lo que estuviera marcado. Soltando en el vacío no hay nada marcado y
    // no pasa nada: ese es el camino para arrepentirse sin gastar el poder.
    void ReleasePick(Vector2 screenPos)
    {
        if (!Picking) return;

        if (_pickAt.HasValue) HandlePicked(_pickAt.Value);
        else                  ClearPickPreview();
    }

    void ClearPickPreview()
    {
        foreach (var cell in _pickPreview)
            if (grid.TryGetBubble(cell, out var view) && view != null)
                StartCoroutine(Swell((RectTransform)view.transform, PICK_PREVIEW_TIME, 1f));

        cannon?.Trajectory?.ClearMarks();

        _pickPreview.Clear();
        _pickAt = null;
    }

    // Crece o vuelve, sin rebote: la vista previa tiene que leerse como un estado —"estas se van
    // a ir"— y un gesto con rebote se lee como que algo ya pasó.
    // El rebote en el sitio, alrededor de un tamaño fijo. La fórmula está en 'Jelly', compartida
    // con la burbuja: el bicho que tiembla esperando y el que tiembla al salir son el mismo.
    static IEnumerator Wobble(RectTransform target, float time, float around)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            if (target == null) yield break;

            target.localScale = Vector3.one * around + Jelly.Pulse(t, JELLY_POP) * around;
            yield return null;
        }

        if (target != null) target.localScale = Vector3.one * around;
    }

    static IEnumerator Swell(RectTransform target, float time, float to)
    {
        if (target == null) yield break;

        float from = target.localScale.x;

        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            if (target == null) yield break;

            target.localScale = Vector3.one * Mathf.Lerp(from, to, t / time);
            yield return null;
        }

        if (target != null) target.localScale = Vector3.one * to;
    }

    const float PICK_PREVIEW_TIME  = 0.1f;
    const float PICK_PREVIEW_SCALE = 1.14f;

    // Cuándo revienta cada celda de esta jugada, en segundos desde ahora.
    //
    // Hace falta porque una mina no se enciende "porque hubo un match" sino porque una burbuja
    // DESAPARECIÓ a su lado, y los pops de una cadena van escalonados. Estallando al instante, la
    // mina pegada a la última del racimo se adelantaba a lo que la encendía (reportado por Diego).
    //
    // Se acumula durante toda la jugada —incluida la cadena de minas, que añade las suyas con su
    // propio retraso— y se vacía al final, en AfterRemoval.
    readonly Dictionary<Vector2Int, float> _poppedAt = new();

    // Cuándo revienta esa celda. Si no reventó —una burbuja que ATERRIZA al lado de una mina
    // también la enciende— va al instante: no hay nada que esperar.
    float PopTime(Vector2Int cell) => _poppedAt.TryGetValue(cell, out float t) ? t : 0f;

    void AfterRemoval(HashSet<Vector2Int> removed, Vector2Int? landed = null)
    {
        // Las minas primero, y la cadena ENTERA: una que estalla puede encender a la de al lado y
        // esa a la siguiente. Mirar si el nivel terminó a mitad de esa cadena sería decidirlo
        // sobre un tablero que todavía va a cambiar, y lo que la cadena se lleve cuenta igual
        // para la criatura y para el marcador.
        DetonateMinesTouching(removed, landed);

        // Y lo que las burbujas de rescate acusen de todo eso: una capa por golpe, y la que se
        // quede a la vista se libera. Va ANTES de contar, porque lo que libere entra en 'removed'
        // y tiene que contarse como rescatado en esta misma jugada.
        HitRescuesTouching(removed, landed);

        CountRescued(removed);

        // Un disparo que no matchea SUMA una burbuja al tablero, así que el objetivo de limpiarlo
        // se aleja. Se refresca contra el grid en vez de sumar a mano: el mismo número derivado
        // que usan los descuentos, para que no haya dos formas de calcularlo.
        if (_targets.Count == 0 && landed.HasValue && grid.IsOccupied(landed.Value) && _shown.Count > 0)
        {
            _shown[0] = grid.CellCount + _pendingTicks;
            objectiveHub?.SetRemaining(0, _shown[0]);
        }

        if (progressScore != null) progressScore.SetScore(LiveScore, _level.star_thresholds);

        CheckWinLose();

        _poppedAt.Clear();
    }

    // Enciende las minas que tengan algo pegado en 'touched', y las que esas enciendan, hasta que
    // no quede ninguna. Lo que se lleven se suma a 'touched', que por eso entra y sale: quien
    // llamó tiene que terminar con la lista COMPLETA de lo que desapareció.
    //
    // Una mina estalla por contacto y no por color: lo mismo da que la burbuja de al lado se haya
    // ido en un match, en una explosión o que acabe de llegar. Una sola regla y los tres casos
    // que pidió Diego —disparo, cadena y otra mina— salen de ella sin un caso especial cada uno.
    void DetonateMinesTouching(HashSet<Vector2Int> touched, Vector2Int? landed = null)
    {
        // Cada mina con CUÁNDO se enciende: el instante en que revienta la primera vecina que se
        // va a su lado. Una mina pegada a la última del racimo espera a esa; estallando al tirar
        // se adelantaba a lo que la encendía.
        var pending = new Dictionary<Vector2Int, float>();
        var fired   = new HashSet<Vector2Int>();

        foreach (var cell in touched)
            foreach (var around in HexGridMath.GetNeighbors(cell))
                Queue(around, PopTime(cell));

        // Y lo que acaba de llegar, que no se fue a ningún sitio pero igual la toca.
        if (landed.HasValue)
            foreach (var around in HexGridMath.GetNeighbors(landed.Value))
                Queue(around, 0f);

        while (pending.Count > 0)
        {
            // La que antes se encienda, primero: una que estalle tarde puede encender a otra, y
            // resolviéndolas en desorden esa otra saldría antes que quien la enciende.
            var  mine = Vector2Int.zero;
            float at  = float.MaxValue;

            foreach (var pair in pending)
                if (pair.Value < at) { mine = pair.Key; at = pair.Value; }

            pending.Remove(mine);

            if (!fired.Add(mine)) continue;

            // Las minas que caen DENTRO del radio se apuntan ANTES de que el estallido se las
            // lleve: preguntarlo después no encontraría ninguna, porque ya no estarían en el
            // tablero. Una mina alcanzada por otra estalla, no revienta como una burbuja más.
            var caught = new List<Vector2Int>();

            foreach (var cell in BoosterRules.CellsHitBy(Booster.Mine, mine, null, grid))
                if (cell != mine && !fired.Contains(cell)
                    && grid.TryGetBubble(cell, out var view) && view.IsMine) caught.Add(cell);

            // Se dispara aunque su celda ya esté vacía: la que se llevó una vecina estalla igual,
            // desde donde estaba. Es lo que hace que la cadena salga hacia afuera en vez de
            // pararse en la primera.
            // El estallido suena acá y no dentro de ResolveBoosterBlast: los que se disparan ya
            // lo tocan antes de llamarla —el sonido va con la mecha, no con el estallido— y
            // ponerlo dentro lo repetiría dos veces en cada bomba.
            StartCoroutine(PlayClipAfter(BoosterRules.ArtFor(Booster.Mine)?.burstClip, at));

            var blown = ResolveBoosterBlast(Booster.Mine, mine, null, at);

            touched.UnionWith(blown);

            // Y ahora sí, con el reloj del estallido ya apuntado: cada una se enciende cuando la
            // onda llega HASTA ELLA, no cuando la anterior empieza.
            foreach (var cell in caught) Queue(cell, PopTime(cell), force: true);

            foreach (var cell in blown)
                foreach (var around in HexGridMath.GetNeighbors(cell))
                    Queue(around, PopTime(cell));
        }

        // Se queda con el PRIMER contacto: dos vecinas que revientan a destiempo la encienden
        // cuando llega la primera, no cuando acaba la segunda.
        void Queue(Vector2Int cell, float when, bool force = false)
        {
            if (fired.Contains(cell)) return;
            if (!force && !(grid.TryGetBubble(cell, out var view) && view.IsMine)) return;

            if (!pending.TryGetValue(cell, out float had) || when < had) pending[cell] = when;
        }
    }

    // Match (3+ conectadas) -> explode, después drop de todo lo que quedó flotando.
    // Devuelve el set de celdas removidas (para chequear el objetivo rescue).
    HashSet<Vector2Int> ResolveMatchAndDrop(Vector2Int landedCell)
    {
        var removed = new HashSet<Vector2Int>();

        // Hay que preguntarlo ANTES de resolverla: un instante después ya es de otro color y no
        // queda rastro de que el estallido lo provocó un comodín.
        bool wasRainbow = grid.TryGetBubble(landedCell, out var landed) && landed.ColorType == BubbleColor.Rainbow;

        // Antes del flood-fill: si lo que aterrizó fue una arcoíris, acá elige de qué color se
        // vuelve. Si no, sería la semilla de un match que se lleva el grid completo.
        //
        // Cuando devuelve false sigue siendo comodín, y eso ya dice que no hubo match: no se puede
        // buscar desde ella sin reventar el tablero, y tampoco hace falta.
        if (!grid.ResolveRainbow(landedCell))
        {
            _comboStreak = 0;
            return removed;
        }

        var matched = grid.FindConnectedSameColor(landedCell);
        if (matched.Count < GridController.MIN_MATCH)
        {
            _comboStreak = 0; // el disparo no matcheó nada — corta la racha de combo
            return removed;
        }

        // El efecto del comodín no depende de que la arcoíris sea la que acaba de aterrizar: una
        // que se quedó en el tablero de un disparo anterior y ahora entra en el grupo hizo
        // exactamente el mismo trabajo, y antes se iba en silencio.
        //
        // La semilla de los efectos es el comodín y no la aterrizada: es la que los provocó, y
        // con varias en el grupo la primera del recorrido es la más cercana al impacto.
        Vector2Int rainbowCell = landedCell;
        bool       hasRainbow  = wasRainbow;

        if (!hasRainbow)
            foreach (var cell in matched)
                if (grid.TryGetBubble(cell, out var v) && v.ColorType == BubbleColor.Rainbow)
                {
                    hasRainbow = true;
                    rainbowCell = cell;
                    break;
                }

        // Las de rescate que entraron en el grupo por su color NO se van con él si todavía tienen
        // caparazón: el match les cuesta una capa y se quedan. Sin esto, un poder de rescate con
        // tres capas se abriría de un solo match y el caparazón no significaría nada.
        //
        // Se sacan del grupo ANTES de puntuar y de reventar, no después: metidas en la cuenta
        // sumarían puntos y pedirían un pop que no va a ocurrir.
        for (int i = matched.Count - 1; i >= 0; i--)
        {
            if (!grid.TryGetBubble(matched[i], out var trapped) || trapped == null) continue;
            if (!trapped.IsCreature || trapped.LayersLeft <= 0)                     continue;

            StartCoroutine(HitRescue(matched[i], i * ChainStep(matched.Count, POP_CHAIN_DELAY)));
            matched.RemoveAt(i);
        }

        if (matched.Count < GridController.MIN_MATCH)
        {
            // Quitadas las acorazadas puede no quedar match: el grupo era tres y dos eran
            // caparazón. El golpe ya se dio, pero no hay nada que reventar.
            _comboStreak = 0;
            return removed;
        }

        _comboStreak++;

        VibrateBurst(matched.Count);

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

        // Con un comodín hay DOS fases en vez de una cascada. Primero el color se propaga por todo
        // el grupo, desde la que tocó la perla hacia afuera —el orden del flood-fill es el orden en
        // que las reclamó de verdad— y cuando ha llegado a la última, estallan todas juntas.
        //
        // Separarlas es lo que deja VER la propagación: mezcladas, cada burbuja se teñía y moría
        // en el mismo gesto y el efecto se leía igual que un match normal.
        RectTransform seed = grid.TryGetBubble(rainbowCell, out var seedView) ? (RectTransform)seedView.transform : null;

        // El contagio tiene sus propios tiempos, del catálogo: es el efecto del poder y tiene que
        // poder verse: con el paso de los pops era demasiado rápido para notarse.
        var   rainbowArt = hasRainbow ? BoosterRules.ArtFor(Booster.Rainbow) : null;
        float claimStep  = rainbowArt != null
            ? ChainStep(matched.Count, rainbowArt.claimStep, rainbowArt.claimMaxTotal)
            : 0f;
        float claimFade  = rainbowArt?.claimFade ?? 0f;

        // Y el estallido espera además la pausa con todas ya teñidas.
        float burstDelay = hasRainbow
            ? (matched.Count - 1) * claimStep + claimFade + (rainbowArt?.claimHold ?? 0f)
            : 0f;

        for (int i = 0; i < matched.Count; i++)
        {
            var cell = matched[i];

            if (grid.TryGetBubble(cell, out var view))
            {
                if (hasRainbow) view.ClaimAsRainbow(i * claimStep, claimFade);

                // Y una sola suena: quince pops a la vez son ruido, y lo que tiene que oírse es
                // el golpe del poder.
                view.PlayPopAnimation(hasRainbow ? burstDelay : i * popStep,
                                      hasRainbow && i > 0 ? null : popClip);
            }

            float at = hasRainbow ? burstDelay : i * popStep;

            grid.SpawnScorePopup(cell, popValue, at, ScorePopup.POP_LIFETIME);
            grid.RemoveBubble(cell);
            removed.Add(cell);

            _poppedAt[cell] = at;
        }
        _bubblesPopped += matched.Count;

        var collapsed = CollapseFloating(removed, burstDelay);

        // Se acumula acá y no se calcula al final multiplicando el total de burbujas: para
        // entonces la racha de cada disparo ya no se sabe. Y usa el MISMO popValue que se
        // mostró en pantalla, así lo que sumó el marcador es exactamente lo que se vio.
        _popScore += matched.Count * popValue;

        int chainSize = matched.Count + collapsed.count;

        // El golpe del comodín espera a que el color haya llegado a la última: es el remate de la
        // contaminación, no algo que pase mientras.
        if (hasRainbow)
        {
            StartCoroutine(RainbowBurst(seed, burstDelay, matched.Count, collapsed.count, collapsed.step));
        }
        // Un golpe de temblor por burbuja, al ritmo de sus pops. Antes era una sola sacudida
        // disparada acá, o sea cuando las burbujas todavía no habían empezado a explotar.
        else if (grid.ShakeWorthIt(chainSize))
        {
            StartCoroutine(ShakeAlongChain(matched.Count, popStep, collapsed.count, collapsed.step));
        }

        return removed;
    }

    // La bomba no busca color: revienta un disco hexagonal alrededor de donde cayó, y después
    // el tablero se desmorona igual que tras un match.
    //
    // Un poder de área CUENTA para la racha de combo, igual que un match: la sube y sus burbujas
    // valen lo que valga la racha en ese momento (decisión de Diego). Tratarlo como un disparo
    // neutro hacía que una racha de 20 volviera a mostrar 10 al explotar, y se leía como si la
    // bomba la hubiera roto — que es exactamente lo contrario de lo que un poder debería hacer.
    // 'startDelay' retrasa lo que se VE, no lo que pasa: el grid se resuelve en el acto como
    // siempre. Lo usa la cadena de minas, donde cada una tiene que esperar a que reviente lo que
    // la enciende — y de paso hace que una mina encendida por otra salga detrás y no a la vez.
    HashSet<Vector2Int> ResolveBoosterBlast(Booster booster, Vector2Int center, Vector2Int? struck,
                                            float startDelay = 0f)
    {
        var removed = new HashSet<Vector2Int>();
        var art     = BoosterRules.ArtFor(booster);

        // Las mismas celdas que la mira acaba de marcar. El orden lo da CellsHitBy —anillos para
        // la bomba, de un extremo al otro para la raya— y el índice ya sirve para escalonar la
        // explosión en vez de reventar todo en el mismo frame.
        var hit = BoosterRules.CellsHitBy(booster, center, struck, grid)
                              .Where(grid.IsOccupied)
                              .ToList();

        // La burbuja del propio poder SIEMPRE se va con él, esté o no en la zona que afecta. La
        // bomba se incluye sola porque su radio sale de donde está parada, pero un poder que
        // golpea en una fila y acaba posado en otra —la Raya Eléctrica— se quedaba fuera: la fila
        // de la que colgaba desaparecía y ella caía al fondo como una burbuja cualquiera, en vez
        // de estallar (reportado por Diego).
        //
        // Va la PRIMERA porque es el origen: lo demás revienta porque ella lo hizo.
        if (grid.IsOccupied(center) && !hit.Contains(center)) hit.Insert(0, center);

        // Antes de los pops, mientras la burbuja del poder todavía existe para marcar el lugar.
        //
        // Con retraso se lanza desde el PUNTO y no desde la burbuja: para cuando llegue su turno
        // esa burbuja ya no existe, y colgar el destello de ella lo haría desaparecer con ella.
        if (startDelay > 0f)
            StartCoroutine(SparkleAfter(HexGridMath.CellToLocalPos(center),
                                        art?.burstSparkle ?? bombSparkle, startDelay));
        else if (grid.TryGetBubble(center, out var armed))
            Sparkle.Burst((RectTransform)armed.transform, art?.burstSparkle ?? bombSparkle);

        var blast     = art?.burstBlast ?? bombBlast;
        bool perBubble = art?.burstPerBubble ?? false;

        // El fogonazo y la onda salen del centro del impacto, en coordenadas del tablero: así la
        // onda acompaña al tablero si este se desplaza mientras se está abriendo.
        //
        // Salvo que el poder estalle burbuja por burbuja: ahí no hay un centro que mirar —una
        // fila de once no explota desde el medio— y cada celda pone su onda más abajo, al ritmo
        // de su propio pop.
        if (!perBubble)
        {
            if (startDelay > 0f)
                StartCoroutine(BlastAfter(HexGridMath.CellToLocalPos(center), blast, startDelay));
            else
                BombBlast.Play((RectTransform)grid.transform, HexGridMath.CellToLocalPos(center), blast);
        }

        // HeavyImpact, no el LightImpact del resto del juego: cada burbuja que revienta ya da su
        // propio toque ligero, así que la cadena de diecinueve suena a escombros. El golpe fuerte
        // va UNA vez y antes que todos ellos — es la explosión, y lo que viene después es su
        // consecuencia. Con la misma intensidad que los pops, el estallido se perdería entre ellos.
        if (startDelay > 0f) StartCoroutine(VibrateAfter(MOST_HapticFeedback.HapticTypes.HeavyImpact, startDelay));
        else                 Vibrate(booster);

        float popStep = ChainStep(hit.Count, POP_CHAIN_DELAY);

        // La descarga va por la fila ENTERA y no por 'hit', que solo trae las celdas ocupadas:
        // la corriente no se salta los huecos. Va al mismo paso que los pops, así cada burbuja
        // revienta cuando el arco llega hasta ella y no antes.
        if (art != null && art.burstLightning.enabled)
            Lightning.Sweep((RectTransform)grid.transform, (struck ?? center).y, (struck ?? center).x,
                            art.burstLightning, popStep);

        _comboStreak++;

        // Igual que en el match: se calcula una vez porque es el mismo para todas las de este
        // disparo, y se usa EL MISMO número para el popup y para el marcador, así lo que suma es
        // exactamente lo que se vio en pantalla.
        int popValue = ScoreRules.PopValue(_comboStreak);

        // Las que el poder alcanza LEJOS no estallan a la vez que el impacto: sale una hija desde
        // ahí y revienta al llegar. El retraso es el viaje, así que lo que se ve y lo que pasa
        // van juntos — sin esto, dos burbujas del otro extremo del tablero desaparecen solas y se
        // lee como un fallo del juego, no como un poder.
        var travel   = art?.burstTravel;
        bool travels = travel != null && travel.enabled;
        var children = art?.bubbleOrbit?.sprites;

        // Las últimas de la lista son las que el poder alcanza a distancia. Lo dice BoosterRules
        // y no una medida de "está lejos": el racimo que la Madre revienta puede estirarse varias
        // burbujas, y midiendo le salía una hija a la otra punta de su propio racimo.
        int firstTraveler = hit.Count - BoosterRules.TravelersOf(booster);
        int sent          = 0;

        // Lo que cada hija se lleva DE PROPINA al caer: las de su color que estuvieran pegadas a
        // su objetivo. Se apunta acá y se resuelve después del bucle porque hay que mirarlo con
        // el tablero tal como está en ese momento —las hijas van al final de la lista, así que
        // para entonces el racimo del impacto ya salió— y reventarlo dentro cambiaría lo que el
        // propio bucle va a encontrar.
        var alongside = new List<(Vector2Int cell, float at)>();

        for (int i = 0; i < hit.Count; i++)
        {
            var cell = hit[i];

            bool goes = travels && i >= firstTraveler;
            float at  = startDelay + (goes ? travel.TotalFor(sent) : i * popStep);

            if (goes)
            {
                // La hija sale DEL COLOR de la burbuja que va a buscar, no de un color fijo del
                // catálogo. Es lo que explica el viaje sin una sola palabra: sale una amarilla,
                // va derecha a una amarilla. Con un color cualquiera, dónde acaba cada una
                // parecería arbitrario.
                //
                // El sprite sale del grid, que ya es el único sitio donde vive el arte de cada
                // color. Repetirlo en el catálogo del poder sería tener dos listas que mantener
                // iguales a mano.
                var sprite = grid.TryGetColor(cell, out var targetColor)
                    ? grid.SpriteFor(targetColor)
                    : (children != null && children.Length > sent ? children[sent] : null);

                TravelingBubble.Send((RectTransform)grid.transform,
                                     HexGridMath.CellToLocalPos(hit[0]),
                                     HexGridMath.CellToLocalPos(cell),
                                     sprite, travel, sent++);

                // Y el racimo de ese objetivo, si lo tiene. Empieza cuando la hija aterriza, no
                // al disparar el poder: lo que se lleva el grupo es ella al caer, y adelantarlo
                // se vería como burbujas desapareciendo solas al otro lado del tablero.
                if (BoosterRules.ChildrenMatch(booster))
                {
                    // 'FindConnectedSameColor' sale en anchura desde el objetivo, así que la
                    // lista ya viene ordenada de la más cercana a la más lejana. Eso es lo que
                    // hace que el racimo reviente EN CADENA, igual que un match disparado, en vez
                    // de desaparecer de golpe (pedido de Diego).
                    var friends  = grid.FindConnectedSameColor(cell);
                    float chain  = ChainStep(friends.Count, POP_CHAIN_DELAY);
                    int   link   = 1;

                    if (friends.Count >= BoosterRules.CHILD_MIN_MATCH)
                        foreach (var friend in friends)
                            if (friend != cell && !removed.Contains(friend) && !hit.Contains(friend))
                                alongside.Add((friend, at + link++ * chain));
                }
            }

            if (grid.TryGetBubble(cell, out var view))
                view.PlayPopAnimation(at, popClip);

            // Y la que la hija alcanza suena con el clip de llegada: es un estallido del poder,
            // no una burbuja que revienta de paso. Con el pop a secas, el final del viaje se oía
            // igual que una del montón y no se entendía que lo había causado la hija.
            if (goes) StartCoroutine(PlayClipAfter(ArriveClip(art), at));

            // En el mismo instante que su pop, no antes: la onda y el sonido de esa burbuja son
            // la misma cosa vista de dos maneras, y separarlos se oye como un eco.
            if (perBubble)
                BombBlast.Ripple((RectTransform)grid.transform, HexGridMath.CellToLocalPos(cell), blast, at);

            grid.SpawnScorePopup(cell, popValue, at, ScorePopup.POP_LIFETIME);
            grid.RemoveBubble(cell);
            removed.Add(cell);

            _poppedAt[cell] = at;
        }

        // Las hijas que no encontraron a quién ir salen IGUAL y revientan en el agua. No sacarlas
        // deja al poder a medias sin decir por qué —el jugador ve una sola cuando esperaba dos—
        // y parece que falló en vez de que el tablero ya no daba para más.
        for (int extra = sent; travels && extra < BoosterRules.TravelersOf(booster); extra++)
        {
            // Hacia abajo y a un lado, que es donde está el océano vacío: reventar en un hueco
            // entre burbujas se leería como que apuntó mal, no como que no había a dónde ir.
            var away = HexGridMath.CellToLocalPos(hit[0])
                     + new Vector2(extra % 2 == 0 ? 0.6f : -0.6f, -0.8f).normalized
                     * HexGridMath.BubbleDiameter * 3.5f;

            var spare = children != null && children.Length > extra ? children[extra] : null;

            TravelingBubble.Send((RectTransform)grid.transform, HexGridMath.CellToLocalPos(hit[0]), away, spare, travel, extra);
            StartCoroutine(PopInOpenWater(away, travel.TotalFor(extra), art));
        }

        // Y ahora sí lo que las hijas se llevaron de propina. Fuera del bucle de arriba para no
        // modificar el tablero mientras ese lo recorre.
        foreach (var (cell, at) in alongside)
        {
            if (!removed.Add(cell)) continue;   // dos hijas pueden rozar el mismo racimo

            if (grid.TryGetBubble(cell, out var view)) view.PlayPopAnimation(at, popClip);

            grid.SpawnScorePopup(cell, popValue, at, ScorePopup.POP_LIFETIME);
            grid.RemoveBubble(cell);

            _poppedAt[cell] = at;

            _bubblesPopped++;
            _popScore += popValue;
        }

        _bubblesPopped += hit.Count;
        _popScore      += hit.Count * popValue;

        // Esperando el viaje más largo: lo que colgaba de una burbuja que todavía no ha estallado
        // no puede empezar a caerse.
        // Esperando a la ÚLTIMA en llegar, que con el escalonado ya no es la misma que la primera.
        var collapsed = CollapseFloating(removed, sent > 0 ? travel.TotalFor(sent - 1) : 0f);

        if ((BoosterRules.ArtFor(Booster.Bomb)?.shake ?? false) || grid.ShakeWorthIt(hit.Count + collapsed.count))
            StartCoroutine(ShakeAlongChain(hit.Count, popStep, collapsed.count, collapsed.step));

        return removed;
    }

    // La hija que no tenía objetivo se deshace donde llegó. Sin burbuja a la que pegarse, lo
    // único que queda es el chispazo y el sonido — y con eso basta: lo que había que evitar era
    // que desapareciera sin más a mitad del agua.
    IEnumerator PopInOpenWater(Vector2 at, float delay, BoosterCatalog.Entry art)
    {
        yield return new WaitForSeconds(delay);

        Sparkle.BurstAt((RectTransform)grid.transform, at, art?.burstSparkle ?? bombSparkle);
        AudioManager.Instance?.PlaySfx(ArriveClip(art));
    }

    // Lo que suena cuando una hija termina su viaje. Cae en el estallido del poder si no se le
    // puso uno propio: quedarse mudo se lee como que el efecto falló, y es peor que repetir.
    static AudioClip ArriveClip(BoosterCatalog.Entry art) =>
        art?.burstTravel?.arriveClip != null ? art.burstTravel.arriveClip : art?.burstClip;

    // Un clip suelto más tarde. Lo piden los poderes cuyo efecto no ocurre todo en el mismo
    // instante: sin esto, el sonido del estallido de una hija habría que colgarlo de la burbuja
    // de destino, que es de quien MENOS depende — ella solo recibe.
    // Lo mismo que PlayClipAfter pero para el móvil: los efectos escalonados anuncian lo suyo con
    // retraso, y un toque que llega cuando ya pasó la cosa se siente como un fallo del teléfono.
    IEnumerator VibrateAfter(MOST_HapticFeedback.HapticTypes type, float delay)
    {
        if (!SaveManager.Vibration) yield break;

        if (delay > 0f) yield return new WaitForSeconds(delay);

        MOST_HapticFeedback.Generate(type);
    }

    IEnumerator SparkleAfter(Vector2 at, Sparkle.Settings settings, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (grid != null) Sparkle.BurstAt((RectTransform)grid.transform, at, settings);
    }

    IEnumerator BlastAfter(Vector2 at, BombBlast.Settings settings, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (grid != null) BombBlast.Play((RectTransform)grid.transform, at, settings);
    }

    IEnumerator PlayClipAfter(AudioClip clip, float delay)
    {
        if (clip == null) yield break;

        yield return new WaitForSeconds(delay);

        AudioManager.Instance?.PlaySfx(clip);
    }

    // Todo lo que quedó colgando sin camino al techo se desprende, de abajo hacia arriba y
    // escalonado: es un desmoronamiento, y soltarlo todo en el mismo frame lo convertía en un
    // bloque que baja de una pieza.
    //
    // Compartido entre el match y la explosión de la bomba: las dos dejan el grid en el mismo
    // tipo de estado (un agujero), y lo que se cae después de cada una se decide igual.
    // delay: cuánto esperan las caídas antes de arrancar. Lo necesita el comodín, cuyas burbujas
    // estallan todas juntas al final de la contaminación: sin esto lo que colgaba de ellas caería
    // antes de que explotaran, que es el orden al revés.
    (int count, float step) CollapseFloating(HashSet<Vector2Int> removed, float delay = 0f)
    {
        var collapsing = grid.FindUnreachableFromCeiling().OrderByDescending(cell => cell.y).ToList();

        // Lo que lleva caparazón no se libera por perder el techo: se agarra. Se saca de las
        // caídas ANTES de contar nada, porque no se cae ni suma puntos de caída — baja a otra
        // celda y lo que pierde es una capa.
        //
        // Sin esto el derrumbe regalaba el rescate entero: tres capas deshechas por suerte, con
        // un caparazón que solo significaba algo cuando el golpe venía de un disparo.
        //
        // La ÚLTIMA capa no se agarra: romperla es exactamente lo que libera, igual que el último
        // golpe de un match (ver HitRescue). Así tres capas son tres desprendimientos y una es
        // uno — el caparazón cuesta lo mismo venga el golpe de donde venga.
        var clinging = new List<(Vector2Int from, BubbleView view)>();

        for (int i = collapsing.Count - 1; i >= 0; i--)
        {
            if (!grid.TryGetBubble(collapsing[i], out var shelled) || shelled == null) continue;
            if (!shelled.IsCreature || shelled.LayersLeft <= 1)                        continue;

            clinging.Add((collapsing[i], shelled));

            // Fuera del tablero ya: puesta, se sostendría a SÍ MISMA —la celda de al lado la
            // tendría por vecina ocupada— y bajaría a un hueco que también está cayendo.
            grid.RemoveBubble(collapsing[i]);
            collapsing.RemoveAt(i);
        }

        float step = ChainStep(collapsing.Count, DROP_CHAIN_DELAY);

        for (int i = 0; i < collapsing.Count; i++)
        {
            var cell = collapsing[i];
            if (grid.TryGetBubble(cell, out var view)) view.PlayDropAnimation(dropClip, delay + i * step);
            // Las que caen valen fijo, sin racha: el número distinto es justamente lo que hace
            // ver que se ganaron de otra forma. GridController lo pone a la altura del cañón,
            // cuando la burbuja terminó de caer.
            grid.SpawnDropScorePopup(cell, ScoreRules.POINTS_PER_DROP, delay + i * step);
            grid.RemoveBubble(cell);
            removed.Add(cell);

            _poppedAt[cell] = delay + i * step;
        }
        _bubblesDropped += collapsing.Count;

        // Después del bucle y no dentro: para acá lo que se cayó ya salió del tablero, así que
        // el hueco al que bajan es el de verdad y no uno que está por desaparecer.
        ClingOn(clinging, removed, delay + collapsing.Count * step);

        return (collapsing.Count, step);
    }

    // Lo que se desprendió con caparazón baja a la celda más cercana que siga colgando del techo,
    // y el aterrizaje le cuesta una capa. Una criatura bajo tres se desprende tres veces antes de
    // salir, igual que pide tres matches: el caparazón cuesta lo mismo venga el golpe de donde
    // venga.
    //
    // Si no queda dónde agarrarse —el tablero se vació— se libera. No hay tablero del que
    // desprenderse, y dejarla flotando sola sería un nivel que no se puede terminar.
    void ClingOn(List<(Vector2Int from, BubbleView view)> clinging, HashSet<Vector2Int> removed, float delay)
    {
        foreach (var (from, view) in clinging)
        {
            if (view == null) continue;

            // Una a una, no todas de golpe: SlideBubbleTo la deja registrada en su celda nueva, y
            // así la siguiente ya la ve ocupada en vez de mudarse al mismo hueco.
            var landing = grid.FindLandingCell(from);

            if (!landing.HasValue)
            {
                view.PlayDropAnimation(dropClip, delay);

                // Metida en 'removed' y no contada acá: quien llamó ya pasa ese conjunto por
                // CountRescued, y liberarla por un segundo camino serían dos sitios que tienen
                // que decir lo mismo.
                removed.Add(from);
                _poppedAt[from] = delay;
                continue;
            }

            // Cuál es la capa que salta, antes de romperla: después no queda rastro de cuál era,
            // y es con eso con lo que se busca su sonido.
            int breaking = view.LayersLeft - 1;

            _recipeAt.TryGetValue(from, out var recipe);

            float fall = grid.SlideBubbleTo(view, landing.Value, delay);

            // Las dos tablas van POR CELDA, así que la mudanza hay que escribirla también en
            // ellas: con la clave vieja la criatura dejaba de contar para el objetivo y el nivel
            // se volvía imposible de ganar.
            if (_creatureAt.Remove(from, out int index)) _creatureAt[landing.Value] = index;
            if (_recipeAt.Remove(from))                  _recipeAt[landing.Value]   = recipe;

            StartCoroutine(ShedLayerOnLanding(view, recipe, breaking, delay + fall));
        }
    }

    // La capa salta CUANDO ATERRIZA y no al desprenderse: el golpe es el aterrizaje, y romperla
    // antes deja el cristal sonando con la burbuja todavía en el aire.
    IEnumerator ShedLayerOnLanding(BubbleView view, RescueBubbleCatalog.Recipe recipe, int breaking, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (view == null) yield break;

        view.BreakLayer();

        AudioManager.Instance?.PlaySfx(RescueBubbleCatalog.LayerBurst(recipe, breaking));
    }

    // Acompaña la cadena con un golpe de temblor por burbuja, con los mismos tiempos que se le
    // pasaron a las animaciones. Los pops primero y las caídas después, porque así es como
    // ocurren en pantalla.
    //
    // La energía del temblor se acumula y decae sola (GridController.ShakePulse), así que esto no
    // produce sacudidas sueltas: mientras los pops llegan más rápido de lo que la energía baja, la
    // pantalla tiembla de forma sostenida y se apaga cuando termina la cadena.
    // El remate del comodín, una vez que el color llegó a la última burbuja: el chispazo, el golpe
    // sonoro, la vibración y el temblor, todos a la vez.
    //
    // El temblor es una sacudida ÚNICA y no una cadena: las burbujas estallan juntas, así que
    // repartirlo por burbuja lo convertiría en un temblequeo largo en vez de un golpe.
    IEnumerator RainbowBurst(RectTransform seed, float delay, int popCount, int dropCount, float dropStep)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        PlayBoosterBurst(Booster.Rainbow);

        var art = BoosterRules.ArtFor(Booster.Rainbow);
        if (art != null && seed != null)
        {
            Sparkle.Burst(seed, art.burstSparkle);

            // Y el golpe de pantalla, que es lo que convierte el estallido en algo que se nota
            // mirara donde mirara el jugador en vez de quedarse en el racimo.
            BombBlast.Play((RectTransform)grid.transform, seed.anchoredPosition, art.burstBlast);
        }

        if ((art?.shake ?? false) || grid.ShakeWorthIt(popCount + dropCount))
            yield return ShakeAlongChain(popCount, 0f, dropCount, dropStep);
    }

    // El golpe de un booster al hacer efecto: su sonido y su vibración, los dos del catálogo.
    //
    // Va aparte del pop de cada burbuja porque son dos cosas distintas: el pop es la burbuja
    // muriendo y esto es el poder haciendo lo suyo. Y vive en el catálogo y no acá para que
    // añadir un booster nuevo no obligue a tocar este archivo.
    void PlayBoosterBurst(Booster booster)
    {
        var art = BoosterRules.ArtFor(booster);
        if (art == null) return;

        AudioManager.Instance?.PlaySfx(art.burstClip);
        Vibrate(booster);
    }

    // El golpe del estallido, por tamaño del grupo. Va UNA vez y ANTES de la cadena de pops, con
    // el mismo criterio que los poderes: cada burbuja ya da su toque ligero, así que lo que falta
    // no es más ruido sino el golpe que los abre.
    //
    // Por debajo del umbral no se añade nada: un match de tres ES el toque de tres pops, y
    // meterle un golpe fuerte delante lo convertiría en una explosión que no fue.
    const int MATCH_MEDIUM = 5;
    const int MATCH_HEAVY  = 8;

    void VibrateBurst(int count)
    {
        if (!SaveManager.Vibration || count < MATCH_MEDIUM) return;

        MOST_HapticFeedback.Generate(count >= MATCH_HEAVY
            ? MOST_HapticFeedback.HapticTypes.HeavyImpact
            : MOST_HapticFeedback.HapticTypes.MediumImpact);
    }

    void Vibrate(Booster booster)
    {
        var art = BoosterRules.ArtFor(booster);

        if (art != null && art.vibrate && SaveManager.Vibration)
            MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.HeavyImpact);
    }

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

        bool objectiveMet = _targets.Count > 0 ? AllRescued : grid.CellCount == 0;
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

        // La partida terminó. Al perder esto ya lo hizo LoseLife, pero ganar no pasa por ahí: sin
        // esta línea, cerrar la app sobre el panel de victoria cobraría una vida por un nivel
        // que el jugador acababa de ganar.
        SaveManager.ClearLevelInProgress();

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
                                                    _level.star_thresholds,
                                                    _targets.Count > 0);
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

        // El tope de burbujas no tiene por qué coincidir con los disparos que sobraban, así que
        // el vaciado se pide explícito en vez de confiar en que el contador haya llegado a cero.
        cannon.EndCelebration();
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
    // Lo rescatado cuenta en vivo, no al ganar: la barra tiene que moverse CUANDO el jugador
    // libera algo, que es el momento en que siente que avanzó.
    int LiveScore => _popScore + ScoreRules.DropScore(_bubblesDropped)
                   + ScoreRules.RescueScore(_level?.star_thresholds, _rescueUnits, _freed);

    int _freed;        // cuántos se liberaron ya
    int _rescueUnits;  // cuántos pide el nivel: es lo que reparte la barra entre ellos

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
