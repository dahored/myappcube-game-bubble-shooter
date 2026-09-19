using System.Collections;
using System.Collections.Generic;
using Solo.MOST_IN_ONE;
using UnityEngine;
using UnityEngine.UI;

// Cañón: apuntado por drag (recibido vía AimInputRelay en AimArea), cola de 2 burbujas
// + swap por tap, dispara y gestiona la burbuja en vuelo hasta que aterriza.
public class CannonController : MonoBehaviour
{
    [SerializeField] RectTransform  muzzlePoint; // hijo de gridContainer, marca de dónde salen los disparos
    [SerializeField] Image          currentBubbleImage;
    [SerializeField] Button         currentBubbleButton;
    [SerializeField] Image          nextBubbleImage;
    [SerializeField] GameObject     bubblePrefab;
    [SerializeField] GridController grid;
    [SerializeField] RectTransform  gridContainer;
    [SerializeField] Canvas         canvas;
    [SerializeField] TrajectoryLine trajectoryLine;
    [SerializeField] float          shotSpeed = 1500f; // GDD 1.5
    [SerializeField] AudioClip      shootClip; // opcional — dejar vacío hasta tener el clip
    [SerializeField] AudioClip      landClip;  // opcional — al pegarse al grid (distinto del match/pop, que ya suena en BubbleView)
    [SerializeField] AudioClip      swapClip;  // opcional — al tocar Current para intercambiarlo con Next

    [Header("Swap manual (tap en Current)")]
    [SerializeField] float swapPopDuration = 0.15f;
    [SerializeField] float swapPopScale    = 0.25f; // qué tan grande llega el pico del pop (1 + esto)
    [Tooltip("Cuánto tardan las dos burbujas en cruzarse de lugar.")]
    [SerializeField] float swapTravelDuration = 0.22f;
    [Tooltip("Cuánto se arquea cada una al cruzar, en píxeles. Sin arco las dos pasan por la misma recta y se tapan entre sí justo en el medio del recorrido.")]
    [SerializeField] float swapArc = 34f;
    [SerializeField] AnimationCurve swapCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Animación: 'next' rueda hacia 'current' (almeja) tras cada disparo")]
    [SerializeField] Image           travelingBubbleImage;   // clon temporal — Image aparte, inactivo por defecto, mismo tamaño que Current/Next
    [SerializeField] float           nextIntoCurrentDuration = 0.2f;
    [SerializeField] AnimationCurve  nextIntoCurrentCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // Cada "frame" es un GameObject aparte (OctopusSprite1/2/3), no un solo Image que cambia
    // de sprite — se activa uno y se apagan los otros dos, sin Animator.
    [Header("Octopus — mascota que entrega la next bubble (3 GameObjects, uno visible a la vez)")]
    [SerializeField] GameObject octopusIdleSprite;  // OctopusSprite1 — reposo entre disparos
    [SerializeField] GameObject octopusThrowSprite; // OctopusSprite2 — el lanzamiento, dura lo mismo que el viaje de la bola (nextIntoCurrentDuration)
    [SerializeField] GameObject octopusWaitSprite;  // OctopusSprite3 — espera breve justo después de entregar, antes de volver a reposo
    [SerializeField] float      octopusWaitHold = 0.15f; // cuánto se ve el Sprite3 — único tiempo nuevo, todo lo demás reusa nextIntoCurrentDuration

    [Header("Aparición de la primera burbuja del nivel")]
    [Tooltip("Sonido de la aparición. Opcional.")]
    [SerializeField] AudioClip revealClip;
    [Tooltip("Cuánto dura el pop de la burbuja. El brillo tiene sus propios tiempos acá abajo.")]
    [SerializeField] float     revealDuration = 0.45f;
    [Tooltip("Qué tan grande llega el pico del pop de la burbuja antes de asentarse en su tamaño.")]
    [SerializeField] float     revealPopScale = 1.3f;
    [Tooltip("El resplandor y las chispas. Las texturas se dibujan por código, no hay que asignar ningún sprite.")]
    [SerializeField] Sparkle.Settings revealSparkle = new();

    [Header("Mano fantasma — cómo disparar (issue #7)")]
    [SerializeField] ShootHintView shootHint;     // opcional — dejar vacío hasta tener el prefab
    [SerializeField] float         idleHintDelay      = 6f;   // segundos sin interactuar antes de mostrarla de nuevo
    [SerializeField] float         hintSwayAngle      = 25f;  // grados a cada lado de la vertical, demuestra que también se apunta a los costados
    [SerializeField] float         hintSwayPeriod     = 12f;  // segundos por ciclo completo (izq→der→izq) — muy lento y suave, no un tic rápido
    [SerializeField] float         hintTrajectoryAlpha = 0.4f; // línea "fantasma" semitransparente, distinta de un apuntado real
    [SerializeField] float         hintHandDistance    = 550f; // qué tan lejos del cañón se posiciona la mano sobre la línea

    public event System.Action<Vector2Int> OnBubbleLanded;

    List<string>      _availableColors;
    List<BubbleColor> _availableColorsParsed; // fallback de RollColor() si el grid se queda sin colores rastreables
    int          _shotsRemaining;
    BubbleColor  _current;
    BubbleColor  _next;
    ShotBubble   _flyingShot;
    Vector2Int?  _predictedCell;   // dónde prometió la mira que iba a quedar este disparo
    bool         _inputEnabled = true;
    bool         _pointerDown;     // el dedo está apoyado, aunque el guard haya cortado el apuntado
    Vector2      _pointerPos;      // dónde, para retomar la mira sin esperar a que se mueva
    bool         _dragging;
    bool         _hintShown;
    bool         _currentHiddenForReveal;
    Coroutine    _swapRoutine;
    RectTransform _swapCloneOut, _swapCloneIn; // las dos burbujas viajeras del intercambio
    float        _idleTimer;
    Coroutine    _hintSwayRoutine;
    Vector2      _aimDir = Vector2.up;
    bool         _muzzleMeasured;
    Vector2      _muzzleLocalBase; // posición de muzzlePoint convertida al espacio local de gridContainer,
                                    // SIN scroll — calculada acá en vez de a mano, así no importa el anchor/dispositivo

    // Posición real del muzzle ahora mismo: si GridController retiró el grid (ScrollOffsetY > 0),
    // hay que restarlo acá para que el cañón siga apuntando desde su lugar visual fijo — el
    // grid se mueve, el cañón no.
    Vector2 MuzzleLocal => _muzzleLocalBase - Vector2.up * (grid != null ? grid.ScrollOffsetY : 0f);

    void Awake()
    {
        currentBubbleButton.onClick.AddListener(SwapCurrentAndNext);

        // Oculto desde el arranque — solo debe verse durante la animación post-disparo. Si en
        // el Editor queda activo por error (ej. se dejó así para poder ubicarlo/ajustarlo),
        // esto evita el cuadro blanco (Image sin sprite) visible antes del primer disparo.
        if (travelingBubbleImage) travelingBubbleImage.gameObject.SetActive(false);
    }

    // Start() y no Awake(): SafeAreaPanel ajusta el tamaño real de SafeArea en su propio
    // Awake(), y Unity garantiza que todos los Awake() de la escena terminan antes que
    // cualquier Start() — así la posición mundial de muzzlePoint ya es la definitiva.
    void Start()
    {
        PublishMuzzleReference();
        if (grid != null)
        {
            grid.OnBubbleTapped += HandleBubbleTapped;

            // Drag que empieza justo encima de una burbuja — mismo destino que AimInputRelay
            // (AimArea), para poder apuntar arrastrando desde cualquier parte de la pantalla,
            // no solo los huecos vacíos del grid (pedido de Diego).
            grid.OnBubbleDragBegin += OnAimBegin;
            grid.OnBubbleDragMove  += OnAimDrag;
            grid.OnBubbleDragEnd   += OnAimEnd;
        }
    }

    void Update()
    {
        // _inputEnabled también congela el disparo ya en vuelo — si no, pausar a mitad de
        // un tiro lo dejaría animándose solo detrás del PausedPanel.
        if (!_inputEnabled) return;

        // El dedo se apoyó mientras la burbuja anterior volaba: en cuanto aterriza, la mira
        // retoma sola. Antes esto se resolvía en OnAimDrag, pero Unity solo manda drag cuando
        // el dedo SE MUEVE — con el dedo quieto, apuntar quedaba trabado hasta moverlo.
        if (!_dragging && _pointerDown && _flyingShot == null)
        {
            _dragging = true;
            HideHint();
            UpdateAim(_pointerPos);
        }

        if (_flyingShot != null)
        {
            var impact = _flyingShot.Tick(Time.deltaTime);
            if (impact.HasValue) ResolveImpact(impact.Value);
            return;
        }

        // Nudge de inactividad (issue #7): mientras es el turno del jugador (sin disparo en
        // vuelo, sin estar ya arrastrando) y no se mostró todavía, cuenta el tiempo sin
        // interactuar. Aplica en cualquier nivel, no solo el tutorial del primer disparo.
        if (!_dragging && !_hintShown)
        {
            _idleTimer += Time.deltaTime;
            if (_idleTimer >= idleHintDelay) ShowHint();
        }
    }

    // Le pasa al grid la altura del muzzle, que es contra lo que aquel mide cuánto tiene que
    // retirarse para no quedar encima del cañón. Idempotente a propósito: se llama desde Start()
    // y también desde Init().
    //
    // Las dos veces hacen falta. Start() cubre cualquier escena donde el cañón exista sin nivel
    // armado; Init() cubre el orden de arranque, que es el que importa: entre dos Start() Unity no
    // garantiza cuál va primero, así que si el del cañón corriera antes que el de
    // GameplayController, el grid todavía no tendría burbujas y el retiro daría cero. Init() lo
    // llama GameplayController justo después de poblar el grid, o sea en un orden conocido — y
    // recién ahí la posición de juego es un número final, que es lo que necesita leer
    // GridIntroPreview antes del primer frame dibujado.
    void PublishMuzzleReference()
    {
        // La MEDICIÓN va una sola vez. WorldToGridLocal convierte al espacio del gridContainer, y
        // la línea de abajo mueve ese mismo contenedor: volver a medir después daría un valor ya
        // contaminado por ese movimiento, y MuzzleLocal terminaría restando el retiro dos veces.
        //
        // Se mide mientras el contenedor está todavía en su posición base, que es seguro: nada lo
        // desplaza hasta que alguien pasa por acá (sin referencia del cañón, RecomputeScroll no
        // tiene contra qué medir y deja el retiro en cero).
        if (!_muzzleMeasured)
        {
            _muzzleMeasured  = true;
            _muzzleLocalBase = WorldToGridLocal(muzzlePoint.position);
        }

        // Publicar, en cambio, se repite sin problema: es el mismo número, y vuelve a asentar el
        // retiro por si la primera vez el grid todavía no tenía burbujas.
        if (grid != null) grid.SetMuzzleReferenceY(_muzzleLocalBase.y); // deja el grid ya colocado, sin animar
    }

    public void Init(List<string> availableColors, int shotsRemaining)
    {
        PublishMuzzleReference();

        _availableColors       = availableColors;
        _availableColorsParsed = new List<BubbleColor>();
        foreach (var c in availableColors) _availableColorsParsed.Add(BubbleColorExtensions.Parse(c));
        _shotsRemaining  = shotsRemaining;
        _current = RollColor();
        _next    = RollColor();
        RefreshPreview();
        SetOctopusSprite(octopusIdleSprite);

        // Obligatorio mientras nunca se haya disparado en todo el juego (en la práctica,
        // siempre nivel 1) — no depende del idle timer, se muestra ya mismo.
        if (!SaveManager.HasFiredFirstShot) ShowHint();
    }

    // Llamado por GameplayController cuando suma el bonus de NoMoreMovesPanel — ahí sí hace
    // falta refrescar YA (el "next" pudo haber quedado oculto por quedarse sin disparos, y
    // acá no hay ninguna animación de por medio que vaya a mostrar el resultado después).
    public void SetShotsRemaining(int remaining)
    {
        _shotsRemaining = remaining;
        RefreshPreview();
    }

    // Llamado por GameplayController.OnBubbleLanded justo al gastar el disparo — a propósito
    // SIN refrescar la preview: ResolveImpact() está a mitad de camino resolviendo este mismo
    // disparo y va a lanzar AnimateNextIntoCurrent() apenas termine, que ya muestra el
    // resultado en el momento correcto. Si acá se llamara a SetShotsRemaining (con refresh),
    // currentBubbleImage se revelaba de golpe ANTES de que el clon viajero llegara, rompiendo
    // el efecto de "llegada" (reportado por Diego). Solo actualiza el conteo interno para que
    // esa animación, un instante después, calcule bien si hay/no hay next.
    public void UpdateShotsRemainingSilently(int remaining) => _shotsRemaining = remaining;

    // Los disparos que sobraron al ganar no se evaporan con el nivel: se gastan en pantalla.
    // Es el MISMO ciclo que un disparo real — se apaga la burbuja de la recámara, sale la que
    // estaba ahí, y la cola avanza con la animación del pulpo — solo que en vez de aterrizar en
    // el grid revienta a mitad de camino, sin rebotar.
    //
    // Sube RECTO. La variedad no viene de inclinar el disparo sino de dónde revienta: quien
    // llama le pasa un corrimiento lateral chico y acá la burbuja va del cañón a ese punto.
    // Inclinar el tiro obligaba a pensar en grados, y un ángulo que en el papel parece mínimo
    // manda la burbuja lejísimos cuando el recorrido es largo.
    //
    // Avisa por onBurst dónde reventó (espacio local del grid) para que quien llama ponga ahí el
    // número de puntos: acá no sabemos cuánto vale, eso lo lleva GameplayController.
    public IEnumerator PlayCelebrationShot(float lateralOffset, float minDistance, float maxDistance,
                                           float flightTime, AudioClip popClip, System.Action<Vector2> onBurst)
    {
        if (!bubblePrefab || !gridContainer) yield break;

        Vector2 start = MuzzleLocal;
        Vector2 burst = new Vector2(start.x + lateralOffset,
                                    start.y + Random.Range(minDistance, maxDistance));

        BubbleColor fired = _current;

        // La cola avanza ANTES de animar, no después.
        //
        // Antes se avanzaba al final y la recarga se saltaba por completo: esperar
        // AnimateNextIntoCurrent después de cada explosión (nextIntoCurrentDuration +
        // octopusWaitHold, 0,35 s) triplicaba el remate. Pero el problema era el orden, no el
        // tiempo. Sabiendo ya cuál es la siguiente, el pulpo puede pasarla MIENTRAS la disparada
        // sube: las dos animaciones ocupan el mismo hueco y la recarga no cuesta nada extra.
        _shotsRemaining = Mathf.Max(0, _shotsRemaining - 1);
        _current = _next;
        _next    = LevelColor();

        // Las dos ranuras quedan vacías durante el vuelo: la concha porque su burbuja salió
        // disparada, y el pulpo porque la suya está justo yendo hacia la concha.
        if (currentBubbleImage) currentBubbleImage.enabled = false;
        if (nextBubbleImage)    nextBubbleImage.enabled    = false;

        var go   = Instantiate(bubblePrefab, gridContainer);
        var view = go.GetComponent<BubbleView>();
        var rt   = (RectTransform)go.transform;

        view.Setup(Vector2Int.zero, fired, grid.SpriteFor(fired)); // Setup la pone en la celda 0,0
        rt.anchoredPosition = start;                               // y acá la mandamos al cañón

        // La recarga en paralelo: la burbuja que el pulpo entrega viaja hasta la concha con el
        // mismo presupuesto de tiempo que el disparo. Sin esto el pulpo se quedaba quieto con la
        // cola avanzando de golpe, como si las bolas salieran solas del cañón.
        RectTransform handoff  = null;
        Vector2       handFrom = default, handTo = default, sizeFrom = default, sizeTo = default;

        if (_shotsRemaining > 0 && currentBubbleImage && nextBubbleImage)
        {
            var currentRect = (RectTransform)currentBubbleImage.transform;
            var nextRect    = (RectTransform)nextBubbleImage.transform;

            handFrom = nextRect.anchoredPosition; handTo = currentRect.anchoredPosition;
            sizeFrom = nextRect.sizeDelta;        sizeTo = currentRect.sizeDelta;

            handoff = SpawnTravelClone(grid.SpriteFor(_current), handFrom, sizeFrom);
            SetOctopusSprite(octopusThrowSprite);
        }
        else SetOctopusSprite(octopusWaitSprite); // era la última: ya no queda nada que pasar

        AudioManager.Instance?.PlaySfx(shootClip);

        // MediumImpact y no LightImpact como el resto: en el remate cada burbuja lanzada trae
        // además el toque de su propio pop al reventar, así que con la misma intensidad los veinte
        // avisos de un remate de diez disparos se mezclan en un zumbido. Con el lanzamiento más
        // fuerte que la explosión, la tanda se siente como una ráfaga de golpes.
        if (SaveManager.Vibration) MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.MediumImpact);

        // Por tiempo y no por velocidad: el remate entero tiene que durar lo mismo sobren tres
        // disparos o treinta, así que quien llama reparte el presupuesto y acá solo se cumple.
        for (float t = 0f; t < flightTime; t += Time.deltaTime)
        {
            float p = t / flightTime;
            rt.anchoredPosition = Vector2.Lerp(start, burst, p);

            if (handoff)
            {
                float eased = nextIntoCurrentCurve.Evaluate(Mathf.Clamp01(p));
                handoff.anchoredPosition = Vector2.Lerp(handFrom, handTo, eased);
                handoff.sizeDelta        = Vector2.Lerp(sizeFrom, sizeTo, eased); // las ranuras no miden igual
            }

            yield return null;
        }
        rt.anchoredPosition = burst;

        onBurst?.Invoke(burst);
        view.PlayPopAnimation(0f, popClip); // se destruye sola al terminar

        if (handoff) Destroy(handoff.gameObject);
        RefreshPreview(); // la viajera llegó: la concha se llena y el pulpo recibe la siguiente
    }

    // Cierra el remate: la recámara y el pulpo quedan sin nada.
    //
    // Hace falta porque el remate topa cuántas burbujas salen (victoryMaxShots) mientras el
    // contador baja de a una por burbuja. Con más sobrantes que ese tope el contador no llega a
    // cero solo, y la concha se quedaba con una burbuja y el pulpo con otra, los dos esperando un
    // disparo que ya no va a pasar.
    //
    // El pulpo queda en la pose relajada y no en la de reposo: reposo significa "listo para la
    // próxima", y acá no hay próxima.
    public void EndCelebration()
    {
        _shotsRemaining = 0;
        RefreshPreview();
        SetOctopusSprite(octopusWaitSprite);
    }

    // Para el remate: un color de los que el NIVEL tenía disponibles, no de los que quedan en el
    // grid. Al ganar el grid está vacío, así que RollColor no tendría de dónde elegir.
    BubbleColor LevelColor() =>
        _availableColorsParsed.Count > 0
            ? _availableColorsParsed[Random.Range(0, _availableColorsParsed.Count)]
            : _current;

    // Deja la recámara con colores del nivel antes de arrancar el remate: los que traía vienen
    // del grid de la última jugada y pueden ser uno solo repetido.
    public void PrepareCelebrationColors()
    {
        _current = LevelColor();
        _next    = LevelColor();
        RefreshPreview();
    }

    // Deja la recámara vacía: el nivel abre con la concha sin burbuja, y esta aparece recién
    // cuando se libera el disparo.
    //
    // Se llama desde el Start de GameplayController, después de Init() —que es quien enciende la
    // burbuja al armar la cola— y antes del primer frame dibujado.
    public void HideCurrentForReveal()
    {
        if (currentBubbleImage == null) return;

        _currentHiddenForReveal    = true;
        currentBubbleImage.enabled = false;
    }

    // La aparición: destello, sonido y pop. No hace nada si nadie escondió la burbuja antes, así
    // que llamarla de más es inofensivo.
    public IEnumerator RevealCurrent()
    {
        if (!_currentHiddenForReveal || currentBubbleImage == null) yield break;
        _currentHiddenForReveal = false;

        var     rect      = (RectTransform)currentBubbleImage.transform;
        Vector3 baseScale = rect.localScale;

        Sparkle.Burst(rect, revealSparkle);
        AudioManager.Instance?.PlaySfx(revealClip); // no hace nada si está vacío

        currentBubbleImage.enabled = true;

        // Dos tramos: sube de golpe pasándose del tamaño y después se asienta. Es el mismo gesto
        // del swap (swapPopScale), pero arrancando de cero porque acá la burbuja no estaba.
        const float RISE = 0.45f; // qué fracción del total se va en llegar al pico

        for (float t = 0f; t < revealDuration; t += Time.deltaTime)
        {
            float p     = t / revealDuration;
            float scale = p < RISE
                ? Mathf.Lerp(0f, revealPopScale, p / RISE)
                : Mathf.Lerp(revealPopScale, 1f, (p - RISE) / (1f - RISE));

            rect.localScale = baseScale * scale;
            yield return null;
        }

        rect.localScale = baseScale;
    }

    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
        if (!enabled) { HideHint(); return; } // que no siga animando detrás de PausedPanel/paneles de fin de nivel

        // El aviso obligatorio del primer disparo se apagó al deshabilitar el input (la pausa, o
        // la vista previa del grid). Al volver tiene que estar otra vez: mientras nunca se disparó
        // en todo el juego es la única indicación de cómo se juega, y dejarlo al timer de
        // inactividad lo esconde varios segundos justo en el nivel 1.
        if (!SaveManager.HasFiredFirstShot && !_hintShown) ShowHint();
    }

    void ShowHint()
    {
        _hintShown = true;
        shootHint?.Show();
        // Reusa la línea de trayectoria real (puntos que se desvanecen + preview de
        // aterrizaje ya incluidos) para la demo — mismo look que un apuntado real, sin
        // duplicar nada acá. Se balancea izq/der en loop para mostrar que también se puede
        // apuntar a los costados, no solo derecho hacia arriba.
        _hintSwayRoutine = StartCoroutine(SwayTrajectoryDemo());
    }

    void HideHint()
    {
        _idleTimer = 0f;
        if (!_hintShown) return;
        _hintShown = false;
        shootHint?.Hide();
        if (_hintSwayRoutine != null) { StopCoroutine(_hintSwayRoutine); _hintSwayRoutine = null; }
        trajectoryLine.Hide();
    }

    // Recalcular la línea entera (ShowPath -> HexGridMath.GetNeighbors por cada punto,
    // hasta 40) es pesado para hacerlo cada frame — alocación de arrays constante = presión
    // de GC = microcortes en el dispositivo real, sobre todo porque el hint puede quedar
    // mostrado bastante más tiempo que un apuntado real. Como el balanceo es lento y
    // decorativo, alcanza con recalcular la línea a ~20fps (cada 3 frames); la mano en
    // cambio se mueve todos los frames (SetPosition es solo asignar un Vector2, no alocado).
    const int TRAJECTORY_UPDATE_EVERY_N_FRAMES = 3;

    IEnumerator SwayTrajectoryDemo()
    {
        float t     = 0f;
        int   frame = 0;
        while (true)
        {
            t += Time.deltaTime;
            float angle = Mathf.Sin(t / hintSwayPeriod * Mathf.PI * 2f) * hintSwayAngle;
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector2.up;

            if (frame % TRAJECTORY_UPDATE_EVERY_N_FRAMES == 0)
                trajectoryLine.ShowPath(MuzzleLocal, dir, grid.SpriteFor(_current), hintTrajectoryAlpha);
            shootHint?.SetPosition(MuzzleLocal + dir * hintHandDistance);

            frame++;
            yield return null;
        }
    }

    // --- Llamado por AimInputRelay (drag sobre AimArea) ---
    public void OnAimBegin(Vector2 screenPos)
    {
        // Se registra ANTES del guard: el dedo está apoyado igual aunque no se pueda apuntar
        // todavía, y Update lo usa para retomar la mira en cuanto aterrice el disparo anterior.
        _pointerDown = true;
        _pointerPos  = screenPos;

        if (!_inputEnabled || _flyingShot != null) return;
        _dragging = true;
        HideHint();
        UpdateAim(screenPos);
    }

    public void OnAimDrag(Vector2 screenPos)
    {
        _pointerDown = true;
        _pointerPos  = screenPos;

        // El dedo pudo apoyarse mientras la burbuja anterior todavía volaba. En ese momento
        // OnAimBegin se ignoró, y Unity no vuelve a emitir un "begin" con el dedo ya apoyado: sin
        // esto la mira no reaparece hasta levantar y volver a tocar, y peor, al soltar tampoco
        // dispara porque OnAimEnd se corta en su propio guard.
        if (!_dragging && _inputEnabled && _flyingShot == null)
        {
            _dragging = true;
            HideHint();
        }

        if (_dragging) UpdateAim(screenPos);
    }

    public void OnAimEnd(Vector2 screenPos)
    {
        _pointerDown = false;

        if (!_dragging) return;
        _dragging = false;
        // No se oculta la línea acá — se deja visible mostrando el camino que ya está
        // siguiendo el disparo real, hasta que ResolveImpact() la esconde al aterrizar
        // (pedido de Diego: "que salga el trajectory line mientras llega la bola al destino").
        Fire();
    }

    // --- Tap directo, sin drag: apunta y dispara de una hacia el punto tocado (pedido de
    // Diego, visto en otros bubble shooters) ---

    // Tocar directamente una burbuja del grid (GridController.OnBubbleTapped) — apunta
    // exacto al centro de esa celda.
    void HandleBubbleTapped(Vector2Int cell)
    {
        if (!_inputEnabled || _flyingShot != null || _dragging) return;
        AimAndFire(HexGridMath.CellToLocalPos(cell));
    }

    // Tocar cualquier otro punto de AimArea (AimInputRelay.OnPointerClick) — ej. un espacio
    // vacío entre 2 burbujas, no necesariamente una burbuja exacta.
    public void OnTapShoot(Vector2 screenPos)
    {
        if (!_inputEnabled || _flyingShot != null || _dragging) return;
        AimAndFire(ScreenToGridLocal(screenPos));
    }

    // Punto en común de ambos taps: calcula la dirección igual que el drag manual, muestra
    // la línea (para que el tap también tenga feedback visual de hacia dónde va, ya que no
    // hubo drag previo mostrándola) y dispara de inmediato.
    void AimAndFire(Vector2 targetLocal)
    {
        _aimDir = ComputeAimDir(targetLocal);
        trajectoryLine.ShowPath(MuzzleLocal, _aimDir, grid.SpriteFor(_current));
        Fire();
    }

    void UpdateAim(Vector2 screenPos)
    {
        _aimDir = ComputeAimDir(ScreenToGridLocal(screenPos));
        trajectoryLine.ShowPath(MuzzleLocal, _aimDir, grid.SpriteFor(_current));
    }

    Vector2 ComputeAimDir(Vector2 targetLocal)
    {
        Vector2 dir = targetLocal - MuzzleLocal;
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
        dir.Normalize();
        if (dir.y < 0.15f) dir.y = 0.15f; // GDD 1.3: no se puede apuntar hacia abajo del todo
        return dir.normalized;
    }

    Vector2 ScreenToGridLocal(Vector2 screenPos)
    {
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridContainer, screenPos, cam, out var local);
        return local;
    }

    // Convierte una posición mundo (ej. la de muzzlePoint) al espacio local de gridContainer —
    // funciona sin importar de qué padre/anchor cuelgue el objeto convertido.
    Vector2 WorldToGridLocal(Vector3 worldPos)
    {
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridContainer, screenPoint, cam, out var local);
        return local;
    }

    void Fire()
    {
        CancelSwap(); // antes de tocar currentBubbleImage: CancelSwap la deja visible de nuevo

        // Oculta el ícono estático del cañón mientras dura el vuelo — si no, se ve la bola
        // "quieta" en el ícono Y la instancia nueva volando al mismo tiempo, como si fueran
        // dos bolas (una copia). Vuelve a mostrarse en RefreshPreview() cuando aterriza y
        // la cola avanza.
        if (currentBubbleImage) currentBubbleImage.enabled = false;

        // La celda que marcó la mira en este instante. Es la que va a usarse al aterrizar: el
        // círculo transparente promete un lugar y el disparo lo cumple, sin recalcular nada.
        _predictedCell = trajectoryLine.LandingCell;

        var go   = Instantiate(bubblePrefab, gridContainer);
        var shot = go.AddComponent<ShotBubble>();
        shot.Init(gridContainer, grid, MuzzleLocal, _aimDir, shotSpeed, _current, grid.SpriteFor(_current));
        _flyingShot = shot;
        AudioManager.Instance?.PlaySfx(shootClip);
        if (SaveManager.Vibration) MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.LightImpact);

        if (!SaveManager.HasFiredFirstShot) SaveManager.HasFiredFirstShot = true;
        HideHint();
    }

    void ResolveImpact(ShotBubble.ImpactInfo impact)
    {
        var shotView = _flyingShot.GetComponent<BubbleView>();

        // Primero la celda que prometió la mira. Solo se recalcula si no hay predicción o si esa
        // celda se ocupó mientras la burbuja volaba — cosa que hoy no puede pasar, pero es la
        // única situación en la que la promesa dejaría de ser cumplible.
        Vector2Int cell;
        if (_predictedCell.HasValue && !grid.IsOccupied(_predictedCell.Value))
        {
            cell = _predictedCell.Value;
        }
        else
        {
            var reference = impact.HitCeiling
                ? new Vector2Int(HexGridMath.EstimateNearestCell(impact.LocalPos).x, 0)
                : impact.StruckCell;
            cell = grid.FindNearestEmptyCell(impact.LocalPos, reference);
        }

        _predictedCell = null;

        grid.RegisterExisting(shotView, cell);
        AudioManager.Instance?.PlaySfx(landClip);
        trajectoryLine.Hide(); // recién acá — se mantuvo visible durante todo el vuelo del disparo
        Destroy(_flyingShot); // el componente ShotBubble ya cumplió su función, la BubbleView sigue viva
        _flyingShot = null;

        _current = _next; // avanzar la cola no depende del resultado del match, es siempre así

        // OnBubbleLanded dispara GameplayController.ResolveMatchAndDrop de forma síncrona —
        // para cuando termina esta línea, el grid ya refleja el match/drop de este disparo
        // (incluida la cascada: burbujas de OTRO color que cayeron por quedar desconectadas
        // del techo, no solo las que matchearon directo).
        OnBubbleLanded?.Invoke(cell);

        // El "current" NO se re-sortea aunque la cascada haya borrado todas las burbujas de su
        // color. El jugador ya lo vio como "next" durante todo el vuelo del disparo anterior: es
        // una promesa, y cambiárselo justo al promoverlo se siente como si el juego le hubiera
        // cambiado la burbuja en la mano — reportado como "disparo un color y sale otro".
        //
        // Si quedó sin uso, el swap está para eso, y el nuevo "next" ya se sortea contra el grid
        // de después de la cascada, así que siempre sirve. Vale más no mentir sobre lo que se va a
        // disparar que ahorrarle un disparo perdido de vez en cuando.
        _next = RollColor();
        StartCoroutine(AnimateNextIntoCurrent());
    }

    // "Next" viaja de su posición a la de "Current" (la almeja) en vez de un swap instantáneo —
    // usa un clon (travelingBubbleImage) porque currentBubbleImage sigue oculta (Fire() la apagó)
    // hasta que el clon llega, así el reemplazo se ve como una llegada, no un parpadeo.
    // Solo se usa acá (post-disparo): Init() y SwapCurrentAndNext() son instantáneos a propósito,
    // un swap manual bidireccional no encaja con una animación de un solo sentido.
    IEnumerator AnimateNextIntoCurrent()
    {
        // Mismo criterio de RefreshPreview: sin disparos no hay "current" que mostrar — antes
        // esta corutina ignoraba _shotsRemaining por completo y siempre terminaba reactivando
        // currentBubbleImage sin condición, dejando una bola visible en el cañón después del
        // último disparo (reportado por Diego).
        bool hasCurrent = _shotsRemaining > 0;
        bool hasNext    = _shotsRemaining > 1;

        if (!hasCurrent || !travelingBubbleImage)
        {
            RefreshPreview(); // sin disparos, o sin el clon asignado en el Editor: cae al camino de siempre
            SetOctopusSprite(octopusIdleSprite);
            yield break;
        }

        var fromRT = (RectTransform)nextBubbleImage.transform;
        var toRT   = (RectTransform)currentBubbleImage.transform;
        Vector2 fromPos = fromRT.anchoredPosition;
        Vector2 toPos   = toRT.anchoredPosition;

        travelingBubbleImage.sprite = grid.SpriteFor(_current); // ya es el nuevo "current" (la cola avanzó en ResolveImpact)
        var travelRT = (RectTransform)travelingBubbleImage.transform;
        travelRT.anchoredPosition = fromPos;
        travelingBubbleImage.gameObject.SetActive(true);

        // Sprite1 -> Sprite2 justo cuando arranca el viaje — el lanzamiento dura lo mismo que
        // la bola tarda en llegar, no un tiempo aparte (pedido de Diego: reusar el timing que
        // ya existe en vez de inventar uno nuevo).
        SetOctopusSprite(octopusThrowSprite);

        float time = 0f;
        while (time < nextIntoCurrentDuration)
        {
            time += Time.deltaTime;
            float p = nextIntoCurrentCurve.Evaluate(Mathf.Clamp01(time / nextIntoCurrentDuration));
            travelRT.anchoredPosition = Vector2.Lerp(fromPos, toPos, p);
            yield return null;
        }

        travelingBubbleImage.gameObject.SetActive(false);
        currentBubbleImage.sprite  = grid.SpriteFor(_current);
        currentBubbleImage.enabled = true;
        nextBubbleImage.enabled    = hasNext; // sin next si este disparo que llega es el último que queda
        if (hasNext) nextBubbleImage.sprite = grid.SpriteFor(_next); // recién aparece cuando el clon ya llegó

        // Sprite2 -> Sprite3 al llegar (la nueva "next" ya está puesta arriba) -> pausa breve
        // -> Sprite1 de nuevo, listo para el próximo ciclo.
        SetOctopusSprite(octopusWaitSprite);
        yield return new WaitForSeconds(octopusWaitHold);
        SetOctopusSprite(octopusIdleSprite);
    }

    void SetOctopusSprite(GameObject frame)
    {
        if (octopusIdleSprite)  octopusIdleSprite.SetActive(frame == octopusIdleSprite);
        if (octopusThrowSprite) octopusThrowSprite.SetActive(frame == octopusThrowSprite);
        if (octopusWaitSprite)  octopusWaitSprite.SetActive(frame == octopusWaitSprite);
    }

    void SwapCurrentAndNext()
    {
        // _inputEnabled por lo mismo que el resto: mientras el nivel está abriendo, en pausa o ya
        // terminado no hay turno del jugador, y el swap es una jugada como cualquier otra.
        if (!_inputEnabled || _flyingShot != null) return; // GDD 1.3: swap es gratis pero no durante el vuelo
        if (_swapRoutine != null) return;                  // ya hay un intercambio cruzando

        // Sin "next" no hay con qué intercambiar. Antes esto no se miraba: en el último disparo,
        // donde la next ni siquiera se muestra, tocar la current igual cambiaba su color por el de
        // una burbuja invisible.
        if (_shotsRemaining < 2) return;

        // El intercambio lógico es inmediato aunque la animación tarde: si el jugador dispara a
        // mitad del cruce, sale el color que pidió y no el viejo.
        (_current, _next) = (_next, _current);

        _swapRoutine = StartCoroutine(SwapFeedback());
    }

    // Las dos burbujas se cruzan de lugar, cada una arqueada hacia un lado. Antes los sprites se
    // intercambiaban de golpe y solo quedaba el pop: se veía que algo había pasado, pero no QUÉ.
    IEnumerator SwapFeedback()
    {
        AudioManager.Instance?.PlaySfx(swapClip); // no hace nada si está vacío
        if (SaveManager.Vibration) MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.LightImpact);

        var currentRect = (RectTransform)currentBubbleImage.transform;
        var nextRect    = (RectTransform)nextBubbleImage.transform;

        Vector2 here  = currentRect.anchoredPosition;
        Vector2 there = nextRect.anchoredPosition;
        Vector2 sizeHere  = currentRect.sizeDelta;
        Vector2 sizeThere = nextRect.sizeDelta;

        // Los colores YA están intercambiados, así que la que sale de la recámara lleva el que
        // pasó a ser "next", y viceversa.
        _swapCloneOut = SpawnTravelClone(grid.SpriteFor(_next), here, sizeHere);
        _swapCloneIn  = SpawnTravelClone(grid.SpriteFor(_current), there, sizeThere);

        if (_swapCloneOut != null && _swapCloneIn != null)
        {
            SetSlotsVisible(false);

            // El pulpo sostiene la "next" con los tentáculos, así que la burbuja se le va de las
            // manos y le llega otra: quedarse congelado se ve inerte.
            //
            // Va el Sprite3 (el relajado) y NO el del lanzamiento: ese tiene el tentáculo
            // extendido y la boca abierta, o sea un gesto de ENTREGA. En un swap no llega nada
            // nuevo, el jugador reordena lo que ya tenía. Usarlo acá contaría algo que no pasó y
            // de paso quemaría el gesto: si sirve para entregar y para intercambiar, deja de
            // significar "llegó la siguiente".
            SetOctopusSprite(octopusWaitSprite);

            // Perpendicular al recorrido y no "hacia arriba": las dos ranuras están en diagonal,
            // así que un arco vertical fijo se vería torcido respecto del camino.
            Vector2 bow = Vector2.Perpendicular(there - here).normalized * swapArc;

            for (float t = 0f; t < swapTravelDuration; t += Time.deltaTime)
            {
                float p     = swapCurve.Evaluate(Mathf.Clamp01(t / swapTravelDuration));
                float curve = Mathf.Sin(p * Mathf.PI); // 0 -> 1 -> 0: el arco abre y vuelve a cerrar

                _swapCloneOut.anchoredPosition = Vector2.Lerp(here, there, p) + bow * curve;
                _swapCloneIn.anchoredPosition  = Vector2.Lerp(there, here, p) - bow * curve;

                // Las ranuras no miden lo mismo (la current es más grande), así que el tamaño
                // viaja con la burbuja. Sin esto, cada una daría un salto de escala al llegar.
                _swapCloneOut.sizeDelta = Vector2.Lerp(sizeHere, sizeThere, p);
                _swapCloneIn.sizeDelta  = Vector2.Lerp(sizeThere, sizeHere, p);

                yield return null;
            }

            DestroySwapClones();
            SetOctopusSprite(octopusIdleSprite);
        }

        SetSlotsVisible(true);
        RefreshPreview(); // recién acá aparecen los sprites ya intercambiados en sus ranuras
        yield return SwapPop();

        _swapRoutine = null;
    }

    // Durante el cruce las ranuras se ocultan por ALFA y no apagando el Image.
    //
    // Un Graphic apagado deja de recibir raycasts: con eso, el segundo toque de un doble tap
    // atravesaba la burbuja, caía en el AimArea que está detrás y salía un disparo (reportado por
    // Diego). Con alfa en cero el botón sigue estando donde el dedo lo espera.
    void SetSlotsVisible(bool visible)
    {
        SetAlpha(currentBubbleImage, visible ? 1f : 0f);
        SetAlpha(nextBubbleImage,    visible ? 1f : 0f);
    }

    static void SetAlpha(Image image, float alpha)
    {
        if (image == null) return;

        var color = image.color;
        color.a     = alpha;
        image.color = color;
    }

    // Clon a partir del mismo Image que ya se usa para el viaje post-disparo, así hereda su
    // material, su orden de dibujo y su sitio en la jerarquía sin duplicar nada en la escena.
    RectTransform SpawnTravelClone(Sprite sprite, Vector2 at, Vector2 size)
    {
        if (travelingBubbleImage == null) return null;

        var clone = Instantiate(travelingBubbleImage, travelingBubbleImage.transform.parent);
        clone.name          = "TravelBubble";
        clone.sprite        = sprite;
        clone.raycastTarget = false;
        clone.gameObject.SetActive(true);

        var rect = (RectTransform)clone.transform;
        rect.anchoredPosition = at;
        rect.sizeDelta        = size;
        return rect;
    }

    void DestroySwapClones()
    {
        if (_swapCloneOut) Destroy(_swapCloneOut.gameObject);
        if (_swapCloneIn)  Destroy(_swapCloneIn.gameObject);
        _swapCloneOut = _swapCloneIn = null;
    }

    // Disparar a mitad de un cruce lo corta en seco: el color lógico ya era el correcto desde el
    // primer frame, así que lo único que queda es limpiar las viajeras y dejar las ranuras como
    // corresponde antes de que Fire() siga con lo suyo.
    void CancelSwap()
    {
        if (_swapRoutine == null) return;

        StopCoroutine(_swapRoutine);
        _swapRoutine = null;
        DestroySwapClones();
        SetSlotsVisible(true);
        SetOctopusSprite(octopusIdleSprite);
        RefreshPreview();
    }

    // El swap en sí es instantáneo (RefreshPreview ya cambió los sprites arriba) — esto es
    // solo el "aviso" de que pasó algo: un pop rápido y simétrico en las dos burbujas, más
    // sonido/háptico, mismo criterio que el resto de eventos del cañón (pedido de Diego: sin
    // esto el cambio pasaba desapercibido).
    // El remate: las dos rebotan al llegar a su nueva ranura. El sonido y el háptico ya sonaron
    // al arrancar el cruce, que es cuando el jugador tocó.
    IEnumerator SwapPop()
    {
        Vector3 baseCurrent = currentBubbleImage.transform.localScale;
        Vector3 baseNext    = nextBubbleImage.transform.localScale;

        float t = 0f;
        while (t < swapPopDuration)
        {
            t += Time.deltaTime;
            // 0 -> 1 -> 0 simétrico (mitad de un seno) en vez de un overshoot con rebote —
            // acá el pop es solo un guiño rápido, no necesita el peso de un pop completo.
            float p = Mathf.Sin(Mathf.Clamp01(t / swapPopDuration) * Mathf.PI);
            float scale = 1f + swapPopScale * p;
            currentBubbleImage.transform.localScale = baseCurrent * scale;
            nextBubbleImage.transform.localScale    = baseNext * scale;
            yield return null;
        }
        currentBubbleImage.transform.localScale = baseCurrent;
        nextBubbleImage.transform.localScale    = baseNext;
    }

    // Smart queue: solo ofrece colores que todavía están en el grid, para no regalar
    // burbujas con las que no se puede matchear nada. Si el grid no tiene ninguno
    // rastreable (ej. solo quedan rainbow, o está vacío), cae al pool del nivel.
    //
    // La arcoíris NO sale de acá. Como burbuja de cañón no funciona: conecta con todo, así que o
    // se lleva medio nivel de un disparo o hay que recortarla hasta volverla un color más. Pasa a
    // ser un booster, que el jugador usa cuando decide y no cuando el azar se lo entrega.
    BubbleColor RollColor()
    {
        // Primero el frente alcanzable, que además viene pesado por utilidad. Si el grid está
        // tapado o vacío se cae a los colores que haya, y de ahí al pool del nivel.
        var pool = grid.ReachableColorPool();

        if (pool.Count == 0)
        {
            var onGrid = grid.ColorsOnGrid();
            pool = onGrid.Count > 0 ? new List<BubbleColor>(onGrid) : _availableColorsParsed;
        }

        return pool[Random.Range(0, pool.Count)];
    }

    // "Current" solo se muestra si queda al menos 1 disparo; "next" solo si queda más de 1
    // (si no, no habría con qué disparar después del actual) — antes se mostraban siempre
    // los dos sin importar cuántos disparos quedaban de verdad.
    void RefreshPreview()
    {
        bool hasCurrent = _shotsRemaining > 0;
        bool hasNext    = _shotsRemaining > 1;

        currentBubbleImage.enabled = hasCurrent; // por si venía oculta de Fire() — vuelve a mostrarse con el color ya rotado
        if (hasCurrent) currentBubbleImage.sprite = grid.SpriteFor(_current);

        nextBubbleImage.enabled = hasNext;
        if (hasNext) nextBubbleImage.sprite = grid.SpriteFor(_next);
    }
}
