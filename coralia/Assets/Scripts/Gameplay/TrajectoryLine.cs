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
    }

    // sprite: el mismo sprite de la burbuja actual (grid.SpriteFor(color)) — así el dot
    // se ve como una mini burbuja real, sin depender de teñir un sprite genérico.
    public void ShowPath(Vector2 originLocal, Vector2 dir, Sprite sprite)
    {
        Vector2 pos       = originLocal;
        Vector2 direction = dir.normalized;
        float   width     = gridContainer.rect.width;
        int     bounces   = 0;
        int     used      = 0;

        while (used < maxDots)
        {
            pos += direction * stepSize;
            if (HexGridMath.ReflectIfNeeded(ref pos, ref direction, width)) bounces++;
            if (bounces > 1) break; // GDD 1.3: la línea muestra hasta el primer rebote

            _pool[used].gameObject.SetActive(true);
            _pool[used].anchoredPosition = pos;
            if (_poolImage[used])
            {
                _poolImage[used].sprite = sprite;
                _poolImage[used].color  = Color.white;
            }
            used++;

            if (HitsSomething(pos)) break;
        }

        for (int i = used; i < maxDots; i++) _pool[i].gameObject.SetActive(false);
    }

    public void Hide()
    {
        foreach (var dot in _pool) dot.gameObject.SetActive(false);
    }

    bool HitsSomething(Vector2 pos)
    {
        // Mismo criterio de techo que ShotBubble.Tick — ver el comentario ahí.
        if (pos.y >= -HexGridMath.BubbleRadius) return true;

        var cell = HexGridMath.EstimateNearestCell(pos);
        if (CheckOverlap(cell, pos)) return true;
        foreach (var n in HexGridMath.GetNeighbors(cell))
            if (CheckOverlap(n, pos)) return true;
        return false;
    }

    bool CheckOverlap(Vector2Int cell, Vector2 pos) =>
        gridController.IsOccupied(cell) &&
        Vector2.Distance(pos, HexGridMath.CellToLocalPos(cell)) < HexGridMath.BubbleDiameter;
}
