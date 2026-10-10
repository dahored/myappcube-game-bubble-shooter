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

    [Header("Zona de la bomba")]
    [Tooltip("Color del hexágono que marca qué va a volar al apuntar con un booster de área. Las marcas se dibujan solas, no hay nada que asignar.")]
    [SerializeField] Color blastMarkColor = new(1f, 1f, 1f, 0.6f);

    [Tooltip("Grosor del filo de esas marcas: bajo da una línea nítida, alto un halo difuso.")]
    [Range(0.02f, 0.4f)]
    [SerializeField] float blastMarkEdge = 0.1f;

    [Tooltip("Cuánto se atenúan las marcas que caen sobre una celda VACÍA, respecto a las que están sobre una burbuja. Las de encima de una burbuja son las que importan —esas son las que vuelan—; las vacías solo completan la forma del hexágono.")]
    [Range(0f, 1f)]
    [SerializeField] float blastEmptyFade = 0.3f;

    // Crecen bajo demanda hasta el tamaño de la zona más grande que se haya mostrado y de ahí en
    // más se reusan: la mira se recalcula cada frame mientras el dedo se mueve, y crear y destruir
    // diecinueve Image por frame es presión de GC constante.
    readonly List<Image> _blastMarks = new();

    readonly List<RectTransform> _pool      = new();
    readonly List<Image>         _poolImage = new();

    // Dónde dice esta mira que va a quedar la burbuja. CannonController la toma al disparar y la
    // respeta al aterrizar, así el círculo transparente deja de ser una predicción y pasa a ser
    // el destino: la burbuja queda exactamente ahí y no en la celda de al lado.
    //
    // Sin esto, la mira y el disparo recorren el mismo camino pero con distinta resolución — la
    // mira de a 24px, el disparo en tramos de medio radio — y terminan en puntos apenas distintos
    // que a veces caen del otro lado de la frontera entre dos celdas.
    public Vector2Int? LandingCell { get; private set; }

    // Contra cuál chocó, que no es lo mismo que dónde se posa. Nulo si fue el techo.
    public Vector2Int? StruckCell { get; private set; }

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
    // booster: cuál va cargado, para marcar lo que se va a llevar. None (lo normal) no
    // marca nada — solo tiene zona lo que explota por área.
    public void ShowPath(Vector2 originLocal, Vector2 dir, Sprite sprite, float alpha = 1f, Booster booster = Booster.None)
    {
        Vector2 pos       = originLocal;
        Vector2 direction = dir.normalized;
        int     used      = 0;
        bool    landed    = false;

        while (used < maxDots)
        {
            pos += direction * stepSize;
            HexGridMath.ReflectIfNeeded(ref pos, ref direction); // rebota todas las veces que haga falta — se muestra el camino completo

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

        if (!landed)
        {
            LandingCell = null;
            StruckCell  = null;
            if (landingPreview) landingPreview.gameObject.SetActive(false);
        }

        // Después de resolver el aterrizaje, porque la zona cuelga de esa celda.
        ShowBlastZone(booster, alpha);

        for (int i = used; i < maxDots; i++) _pool[i].gameObject.SetActive(false);
    }

    public void Hide()
    {
        foreach (var dot in _pool) dot.gameObject.SetActive(false);
        foreach (var mark in _blastMarks) mark.gameObject.SetActive(false);
        if (landingPreview) landingPreview.gameObject.SetActive(false);
    }

    // El hexágono que va a volar, dibujado sobre el tablero mientras se apunta. Es la misma
    // promesa que hace el círculo de aterrizaje, extendida a un área: con un poder que se paga,
    // disparar a ciegas y descubrir después qué se llevó es lo que hace que no se use.
    //
    // Sale de BoosterRules.CellsHitBy, el mismo cálculo que la explosión de verdad — no de una
    // forma dibujada aparte que habría que acordarse de actualizar. Por eso vale para cualquier
    // poder sin tocar este archivo: la bomba marca su hexágono y la raya su fila.
    void ShowBlastZone(Booster booster, float alpha)
    {
        int used = 0;

        if (booster != Booster.None && LandingCell.HasValue)
        {
            foreach (var cell in BoosterRules.CellsAimedBy(booster, LandingCell.Value, StruckCell, gridController))
            {
                var mark = MarkAt(used++);
                mark.rectTransform.anchoredPosition = HexGridMath.CellToLocalPos(cell);

                // La marca sobre una burbuja es la que cuenta: esa burbuja vuela. La que cae en un
                // hueco solo está completando la forma, así que va atenuada — si pesaran lo mismo,
                // apuntando al borde del tablero se vería la forma entera brillando sobre el agua
                // vacía y parecería que va a explotar la nada.
                float weight = gridController.IsOccupied(cell) ? 1f : blastEmptyFade;

                mark.color = new Color(blastMarkColor.r, blastMarkColor.g, blastMarkColor.b,
                                       blastMarkColor.a * alpha * weight);
                mark.gameObject.SetActive(true);

                // DELANTE de las burbujas, y se reafirma cada vez: el gridContainer va sumando
                // burbujas al final a medida que aterrizan, así que una marca colocada al frente
                // una sola vez queda sepultada por todo lo que llegue después. Detrás no sirve —
                // sobre una burbuja el anillo no se vería, que es justo donde hace falta.
                mark.rectTransform.SetAsLastSibling();
            }
        }

        for (int i = used; i < _blastMarks.Count; i++) _blastMarks[i].gameObject.SetActive(false);
    }

    Image MarkAt(int index)
    {
        while (_blastMarks.Count <= index)
        {
            var go = new GameObject("BlastMark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            {
                // Misma convención que el resto de los hijos generados del proyecto: sin esto
                // quedan serializados dentro del .unity.
                hideFlags = HideFlags.DontSave,
            };

            var rt = (RectTransform)go.transform;
            rt.SetParent(gridContainer, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            // Dividido por RING_EDGE: el filo del aro vive al 80% del radio de su textura, así
            // que una marca del tamaño exacto de la burbuja dibuja un aro POR DENTRO de ella.
            // Agrandando la textura en esa proporción, el filo cae justo sobre el borde.
            rt.sizeDelta = Vector2.one * (HexGridMath.BubbleDiameter / SparkleTextures.RING_EDGE);

            var image = go.GetComponent<Image>();
            image.sprite        = SparkleTextures.Ring(blastMarkEdge);
            image.raycastTarget = false;   // apuntar por encima de la marca tiene que seguir funcionando

            _blastMarks.Add(image);
        }

        return _blastMarks[index];
    }

    // Misma lógica que CannonController.ResolveImpact — así el preview nunca miente sobre
    // dónde va a quedar pegada la burbuja real.
    void ShowLandingPreview(Vector2 pos, Vector2Int struckCell, bool hitCeiling, float alpha)
    {
        var reference = hitCeiling
            ? new Vector2Int(HexGridMath.EstimateNearestCell(pos).x, 0)
            : struckCell;
        var cell = gridController.FindNearestEmptyCell(pos, reference);

        // Se guarda aunque no haya sprite de preview asignado: el destino del disparo no depende
        // de que se esté dibujando el círculo.
        LandingCell = cell;
        StruckCell  = hitCeiling ? null : struckCell;

        if (!landingPreview) return;

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
