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

    List<string> _availableColors;
    float        _rainbowChance;
    BubbleColor  _current;
    BubbleColor  _next;
    ShotBubble   _flyingShot;
    bool         _inputEnabled = true;
    bool         _dragging;
    Vector2      _aimDir = Vector2.up;

    void Awake() => currentBubbleButton.onClick.AddListener(SwapCurrentAndNext);

    void Update()
    {
        if (_flyingShot == null) return;
        var impact = _flyingShot.Tick(Time.deltaTime);
        if (impact.HasValue) ResolveImpact(impact.Value);
    }

    public void Init(List<string> availableColors, float rainbowChance)
    {
        _availableColors = availableColors;
        _rainbowChance   = rainbowChance;
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
        Vector2 dir   = local - muzzlePoint.anchoredPosition;
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
        dir.Normalize();
        if (dir.y < 0.15f) dir.y = 0.15f; // GDD 1.3: no se puede apuntar hacia abajo del todo
        _aimDir = dir.normalized;
        trajectoryLine.ShowPath(muzzlePoint.anchoredPosition, _aimDir);
    }

    Vector2 ScreenToGridLocal(Vector2 screenPos)
    {
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridContainer, screenPos, cam, out var local);
        return local;
    }

    void Fire()
    {
        var go   = Instantiate(bubblePrefab, gridContainer);
        var shot = go.AddComponent<ShotBubble>();
        shot.Init(gridContainer, grid, muzzlePoint.anchoredPosition, _aimDir, shotSpeed, _current, grid.SpriteFor(_current));
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

        _current = _next;
        _next    = RollColor();
        RefreshPreview();

        OnBubbleLanded?.Invoke(cell);
    }

    void SwapCurrentAndNext()
    {
        if (_flyingShot != null) return; // GDD 1.3: swap es gratis pero no durante el vuelo
        (_current, _next) = (_next, _current);
        RefreshPreview();
    }

    BubbleColor RollColor()
    {
        if (Random.value < _rainbowChance) return BubbleColor.Rainbow;
        var colorStr = _availableColors[Random.Range(0, _availableColors.Count)];
        return BubbleColorExtensions.Parse(colorStr);
    }

    void RefreshPreview()
    {
        currentBubbleImage.sprite = grid.SpriteFor(_current);
        nextBubbleImage.sprite    = grid.SpriteFor(_next);
    }
}
