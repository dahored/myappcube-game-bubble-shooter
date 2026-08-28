using System.Collections.Generic;
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

    public event System.Action<Vector2Int> OnBubbleLanded;

    List<string>      _availableColors;
    List<BubbleColor> _availableColorsParsed; // fallback de RollColor() si el grid se queda sin colores rastreables
    float             _rainbowChance;
    BubbleColor  _current;
    BubbleColor  _next;
    ShotBubble   _flyingShot;
    bool         _inputEnabled = true;
    bool         _dragging;
    Vector2      _aimDir = Vector2.up;
    Vector2      _muzzleLocalBase; // posición de muzzlePoint convertida al espacio local de gridContainer,
                                    // SIN scroll — calculada acá en vez de a mano, así no importa el anchor/dispositivo

    // Posición real del muzzle ahora mismo: si GridController retiró el grid (ScrollOffsetY > 0),
    // hay que restarlo acá para que el cañón siga apuntando desde su lugar visual fijo — el
    // grid se mueve, el cañón no.
    Vector2 MuzzleLocal => _muzzleLocalBase - Vector2.up * (grid != null ? grid.ScrollOffsetY : 0f);

    void Awake() => currentBubbleButton.onClick.AddListener(SwapCurrentAndNext);

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
        }
    }

    void Update()
    {
        // _inputEnabled también congela el disparo ya en vuelo — si no, pausar a mitad de
        // un tiro lo dejaría animándose solo detrás del PausedPanel.
        if (!_inputEnabled || _flyingShot == null) return;
        var impact = _flyingShot.Tick(Time.deltaTime);
        if (impact.HasValue) ResolveImpact(impact.Value);
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
    }

    public void SetInputEnabled(bool enabled) => _inputEnabled = enabled;

    // --- Llamado por AimInputRelay (drag sobre AimArea) ---
    public void OnAimBegin(Vector2 screenPos)
    {
        if (!_inputEnabled || _flyingShot != null) return;
        _dragging = true;
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
        trajectoryLine.Hide();
        Fire();
    }

    void UpdateAim(Vector2 screenPos)
    {
        Vector2 local = ScreenToGridLocal(screenPos);
        Vector2 dir   = local - MuzzleLocal;
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
        dir.Normalize();
        if (dir.y < 0.15f) dir.y = 0.15f; // GDD 1.3: no se puede apuntar hacia abajo del todo
        _aimDir = dir.normalized;
        trajectoryLine.ShowPath(MuzzleLocal, _aimDir, grid.SpriteFor(_current));
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
        var go   = Instantiate(bubblePrefab, gridContainer);
        var shot = go.AddComponent<ShotBubble>();
        shot.Init(gridContainer, grid, MuzzleLocal, _aimDir, shotSpeed, _current, grid.SpriteFor(_current));
        _flyingShot = shot;
        AudioManager.Instance?.PlaySfx(shootClip);
    }

    void ResolveImpact(ShotBubble.ImpactInfo impact)
    {
        var shotView  = _flyingShot.GetComponent<BubbleView>();
        var reference = impact.HitCeiling
            ? new Vector2Int(HexGridMath.EstimateNearestCell(impact.LocalPos).x, 0)
            : impact.StruckCell;
        var cell = grid.FindNearestEmptyCell(impact.LocalPos, reference);

        grid.RegisterExisting(shotView, cell);
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
        RefreshPreview();
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
        currentBubbleImage.sprite = grid.SpriteFor(_current);
        nextBubbleImage.sprite    = grid.SpriteFor(_next);
    }
}
