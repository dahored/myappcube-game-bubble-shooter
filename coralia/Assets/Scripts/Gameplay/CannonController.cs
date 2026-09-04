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

    [Header("Animación: 'next' rueda hacia 'current' (almeja) tras cada disparo")]
    [SerializeField] Image           travelingBubbleImage;   // clon temporal — Image aparte, inactivo por defecto, mismo tamaño que Current/Next
    [SerializeField] float           nextIntoCurrentDuration = 0.2f;
    [SerializeField] AnimationCurve  nextIntoCurrentCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

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
    float             _rainbowChance;
    BubbleColor  _current;
    BubbleColor  _next;
    ShotBubble   _flyingShot;
    bool         _inputEnabled = true;
    bool         _dragging;
    bool         _hintShown;
    float        _idleTimer;
    Coroutine    _hintSwayRoutine;
    Vector2      _aimDir = Vector2.up;
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
        _muzzleLocalBase = WorldToGridLocal(muzzlePoint.position);
        if (grid != null)
        {
            grid.SetMuzzleReferenceY(_muzzleLocalBase.y);
            grid.RecomputeScroll();
            grid.OnBubbleTapped += HandleBubbleTapped;
        }
    }

    void Update()
    {
        // _inputEnabled también congela el disparo ya en vuelo — si no, pausar a mitad de
        // un tiro lo dejaría animándose solo detrás del PausedPanel.
        if (!_inputEnabled) return;

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

    public void Init(List<string> availableColors, float rainbowChance)
    {
        _availableColors       = availableColors;
        _availableColorsParsed = new List<BubbleColor>();
        foreach (var c in availableColors) _availableColorsParsed.Add(BubbleColorExtensions.Parse(c));
        _rainbowChance = rainbowChance;
        _current = RollColor();
        _next    = RollColor();
        RefreshPreview();

        // Obligatorio mientras nunca se haya disparado en todo el juego (en la práctica,
        // siempre nivel 1) — no depende del idle timer, se muestra ya mismo.
        if (!SaveManager.HasFiredFirstShot) ShowHint();
    }

    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
        if (!enabled) HideHint(); // que no siga animando detrás de PausedPanel/paneles de fin de nivel
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
        if (!_inputEnabled || _flyingShot != null) return;
        _dragging = true;
        HideHint();
        UpdateAim(screenPos);
    }

    public void OnAimDrag(Vector2 screenPos)
    {
        if (_dragging) UpdateAim(screenPos);
    }

    public void OnAimEnd(Vector2 screenPos)
    {
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
        // Oculta el ícono estático del cañón mientras dura el vuelo — si no, se ve la bola
        // "quieta" en el ícono Y la instancia nueva volando al mismo tiempo, como si fueran
        // dos bolas (una copia). Vuelve a mostrarse en RefreshPreview() cuando aterriza y
        // la cola avanza.
        if (currentBubbleImage) currentBubbleImage.enabled = false;

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
        var shotView  = _flyingShot.GetComponent<BubbleView>();
        var reference = impact.HitCeiling
            ? new Vector2Int(HexGridMath.EstimateNearestCell(impact.LocalPos).x, 0)
            : impact.StruckCell;
        var cell = grid.FindNearestEmptyCell(impact.LocalPos, reference);

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

        // El "current" recién promovido pudo quedar huérfano por esa misma cascada (aunque
        // su color no haya matcheado nada, puede haber caído igual). Se re-sortea antes de
        // mostrarlo — mejor una burbuja distinta a una que no puede matchear con nada.
        if (_current != BubbleColor.Rainbow && !grid.ColorsOnGrid().Contains(_current))
            _current = RollColor();

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
        if (!travelingBubbleImage)
        {
            RefreshPreview(); // sin el clon asignado en el Editor, cae al swap instantáneo de siempre
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
        nextBubbleImage.sprite     = grid.SpriteFor(_next); // el nuevo "next" recién aparece cuando el clon ya llegó
    }

    void SwapCurrentAndNext()
    {
        if (_flyingShot != null) return; // GDD 1.3: swap es gratis pero no durante el vuelo
        (_current, _next) = (_next, _current);
        RefreshPreview();
    }

    // Smart queue: solo ofrece colores que todavía están en el grid, para no regalar
    // burbujas con las que no se puede matchear nada. Si el grid no tiene ninguno
    // rastreable (ej. solo quedan rainbow, o está vacío), cae al pool del nivel.
    BubbleColor RollColor()
    {
        if (Random.value < _rainbowChance) return BubbleColor.Rainbow;

        var onGrid = grid.ColorsOnGrid();
        List<BubbleColor> pool = onGrid.Count > 0 ? new List<BubbleColor>(onGrid) : _availableColorsParsed;
        return pool[Random.Range(0, pool.Count)];
    }

    void RefreshPreview()
    {
        currentBubbleImage.sprite  = grid.SpriteFor(_current);
        currentBubbleImage.enabled = true; // por si venía oculta de Fire() — vuelve a mostrarse con el color ya rotado
        nextBubbleImage.sprite     = grid.SpriteFor(_next);
    }
}
