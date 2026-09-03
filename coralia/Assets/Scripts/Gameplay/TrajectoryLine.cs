using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Simula el mismo trayecto que va a recorrer el disparo real (comparte HexGridMath con
// ShotBubble) para que la línea punteada nunca muestre un camino que el disparo no haría.
public class TrajectoryLine : MonoBehaviour
{
    [SerializeField] GameObject     dotPrefab;
    [SerializeField] GridController gridController;
    [SerializeField] RectTransform  gridContainer;
    [SerializeField] int            maxDots  = 40;
    [SerializeField] float          stepSize = 24f;

    [Header("Preview de aterrizaje (sprite bubble_field)")]
    [SerializeField] Image landingPreview;

    readonly List<RectTransform> _pool      = new();
    readonly List<Image>         _poolImage = new();

    void Awake()
    {
        for (int i = 0; i < maxDots; i++)
        {
            var go = Instantiate(dotPrefab, gridContainer);
            go.SetActive(false);
            _pool.Add((RectTransform)go.transform);
            _poolImage.Add(go.GetComponent<Image>());
        }

        if (!landingPreview) Debug.LogWarning("[TrajectoryLine] Falta asignar 'Landing Preview' en el Inspector — no se va a mostrar el preview de aterrizaje.");
        else                 landingPreview.gameObject.SetActive(false);
    }

    // sprite: el mismo sprite de la burbuja actual (grid.SpriteFor(color)) — así el dot
    // se ve como una mini burbuja real, sin depender de teñir un sprite genérico. alpha:
    // 1 = línea real (default), más bajo = look "fantasma" semitransparente para la demo
    // del tutorial (ver CannonController.SwayTrajectoryDemo).
    public void ShowPath(Vector2 originLocal, Vector2 dir, Sprite sprite, float alpha = 1f)
    {
        Vector2 pos       = originLocal;
        Vector2 direction = dir.normalized;
        float   width     = gridContainer.rect.width;
        int     used      = 0;
        bool    landed    = false;

        while (used < maxDots)
        {
            pos += direction * stepSize;
            HexGridMath.ReflectIfNeeded(ref pos, ref direction, width); // rebota todas las veces que haga falta — se muestra el camino completo

            _pool[used].gameObject.SetActive(true);
            _pool[used].anchoredPosition = pos;
            if (_poolImage[used])
            {
                _poolImage[used].sprite = sprite;
                _poolImage[used].color  = new Color(1f, 1f, 1f, alpha);
            }
            used++;

            if (HitsSomething(pos, out var struckCell, out var hitCeiling))
            {
                ShowLandingPreview(pos, struckCell, hitCeiling, alpha);
                landed = true;
                break;
            }
        }

        if (!landed && landingPreview) landingPreview.gameObject.SetActive(false);
        for (int i = used; i < maxDots; i++) _pool[i].gameObject.SetActive(false);
    }

    public void Hide()
    {
        foreach (var dot in _pool) dot.gameObject.SetActive(false);
        if (landingPreview) landingPreview.gameObject.SetActive(false);
    }

    // Misma lógica que CannonController.ResolveImpact — así el preview nunca miente sobre
    // dónde va a quedar pegada la burbuja real.
    void ShowLandingPreview(Vector2 pos, Vector2Int struckCell, bool hitCeiling, float alpha)
    {
        if (!landingPreview) return;

        var reference = hitCeiling
            ? new Vector2Int(HexGridMath.EstimateNearestCell(pos).x, 0)
            : struckCell;
        var cell = gridController.FindNearestEmptyCell(pos, reference);

        landingPreview.gameObject.SetActive(true);
        landingPreview.rectTransform.anchoredPosition = HexGridMath.CellToLocalPos(cell);
        var c = landingPreview.color; c.a = alpha;
        landingPreview.color = c;
    }

    bool HitsSomething(Vector2 pos, out Vector2Int struckCell, out bool hitCeiling)
    {
        // Mismo criterio de techo que ShotBubble.Tick — ver el comentario ahí.
        hitCeiling = pos.y >= -HexGridMath.BubbleRadius;
        if (hitCeiling) { struckCell = default; return true; }

        var cell = HexGridMath.EstimateNearestCell(pos);
        if (CheckOverlap(cell, pos)) { struckCell = cell; return true; }
        foreach (var n in HexGridMath.GetNeighbors(cell))
            if (CheckOverlap(n, pos)) { struckCell = n; return true; }

        struckCell = default;
        return false;
    }

    bool CheckOverlap(Vector2Int cell, Vector2 pos) =>
        gridController.IsOccupied(cell) &&
        Vector2.Distance(pos, HexGridMath.CellToLocalPos(cell)) < HexGridMath.BubbleDiameter;
}
