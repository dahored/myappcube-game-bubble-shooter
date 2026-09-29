using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// La vista previa del tablero al empezar un nivel largo (issue #74).
//
// En la posición de juego el grid se retira hacia arriba para no quedar encima del cañón, así que
// en un nivel alto las primeras filas arrancan fuera de cuadro. El jugador empieza a disparar sin
// haber visto con qué se va a encontrar.
//
// Así que el nivel ABRE con la vista puesta abajo, mostrando el techo del tablero. Se queda quieto
// el tiempo que haga falta para leerlo y recién entonces sube a la posición de juego, que es
// cuando se libera el cañón. Un toque en cualquier parte adelanta la subida.
//
// Se mueve el tablero Y TODO LO QUE LO ACOMPAÑA — el cañón, sus gráficos, la línea de trayectoria,
// las rocas de los costados. O sea, una panorámica y no un tablero deslizándose.
//
// Que se muevan juntos no es solo estético: la posición del cañón se guarda UNA vez, medida en el
// espacio del contenedor del grid (CannonController._muzzleLocalBase). Si el tablero se moviera
// solo, esa medida quedaría desfasada toda la subida y cualquier cosa que dibuje desde el cañón
// —la mira, el aviso de "mantené para apuntar"— saldría a cientos de píxeles de donde va. Moviendo
// los dos, la distancia entre ellos no cambia y no hay nada que compensar.
//
// Se mueven los HIJOS del SafeArea y no el SafeArea mismo, porque de ese se encarga SafeAreaPanel:
// le pone los offsets en cero al arrancar y otra vez un frame después, justo encima de la subida.
//
// Ninguna de las dos cantidades que decide es un número a mano:
//
//   - CUÁNDO vale la pena — cuánto del tablero quedó realmente tapado (GridController.HiddenAbove).
//   - CUÁNTO bajar — el retiro (GridController.ScrollOffsetY), o sea la posición BASE del
//     contenedor. Esa es, por definición, donde se ve un tablero que entra entero: el techo se
//     muestra igual que en un nivel corto, sin inventar un encuadre nuevo.
//
// Un nivel más largo o un dispositivo más alto los cambian solos.
public class GridIntroPreview : MonoBehaviour
{
    [SerializeField] GridController grid;

    [Tooltip("Cuántas filas tienen que quedar fuera de cuadro para que la vista previa valga la pena. Por debajo de esto el tablero se considera que entra entero y el nivel abre normal.")]
    [SerializeField] float minHiddenRows = 1f;

    [Header("Tiempos")]
    [Tooltip("Cuánto se queda quieto abajo antes de subir, para que dé tiempo a leer el tablero.")]
    [SerializeField] float holdAtTop = 1.5f;

    [Tooltip("Cuánto tarda en subir a la posición de juego. Lento a propósito: es el nivel acomodándose, no un corte.")]
    [SerializeField] float riseDuration = 1.2f;

    [SerializeField] AnimationCurve riseEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Cuánto tarda la subida cuando el jugador toca la pantalla para adelantarla. Corto pero no instantáneo: el tablero puede tener varios cientos de píxeles por recorrer, y un salto seco se lee como un error.")]
    [SerializeField] float skipDuration = 0.2f;

    bool  _running;
    bool  _skipped;
    float _travel;

    // Los hermanos del grid que acompañan el movimiento, con la posición en la que estaban al
    // empezar.
    readonly List<RectTransform> _movers    = new();
    readonly List<Vector2>       _moverBase = new();

    // El grid es el único de la escena, así que no vale la pena obligar a arrastrarlo: se busca
    // solo y solo se avisa si de verdad no está.
    void Awake()
    {
        if (grid == null) grid = FindAnyObjectByType<GridController>();
        if (grid == null)
            Debug.LogWarning("[GridIntroPreview] No hay ningún GridController en la escena — los niveles largos van a empezar sin la vista previa del tablero.", this);
    }

    // Coloca la vista abajo YA, sin animar, y dice si hay algo que mostrar.
    //
    // Va aparte de Play() y no es una corrutina porque tiene que correr DENTRO del Start() de
    // GameplayController: así el primer frame que se dibuja ya sale con la vista puesta abajo. Si
    // esperara al primer paso de la corrutina, el nivel aparecería un instante en su posición de
    // juego y recién después saltaría — justo el parpadeo que esto viene a evitar.
    public bool Begin()
    {
        if (grid == null) return false;
        if (grid.HiddenAbove < minHiddenRows * HexGridMath.RowHeight) return false; // ya se ve entero

        _travel  = grid.ScrollOffsetY; // bajar esto deja el contenedor en su posición base
        _running = true;
        _skipped = false;

        CollectMovers();
        SetOffset(-_travel);
        return true;
    }

    void Update()
    {
        if (!_running || _skipped) return;

        // Cualquier toque adelanta la subida. No pasa por el EventSystem a propósito: AimArea
        // cubre la pantalla entera, así que filtrar por "tocó UI" no distinguiría nada.
        bool tapped = (Mouse.current       != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                      (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);

        if (tapped) _skipped = true;
    }

    // La espera y la subida. Solo tiene sentido después de un Begin() que haya devuelto true.
    public IEnumerator Play()
    {
        if (!_running) yield break;

        for (float t = 0f; t < holdAtTop && !_skipped; t += Time.deltaTime) yield return null;

        float duration = _skipped ? skipDuration : riseDuration;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SetOffset(Mathf.LerpUnclamped(-_travel, 0f, riseEase.Evaluate(t / duration)));
            yield return null;
        }

        SetOffset(0f); // exacto, sin arrastre de redondeo del último frame

        _movers.Clear();
        _moverBase.Clear();
        _running = false;
    }

    // Todo lo que cuelga del mismo padre que el grid, menos dos cosas: el grid, que se mueve por su
    // cuenta (GridController compone ese offset con el retiro y el temblor, y escribir su posición
    // desde afuera se los pisaría), y la zona de input, que tiene que seguir cubriendo la pantalla
    // entera para que el toque que adelanta la subida llegue igual.
    //
    // Sale de la jerarquía y no de una lista en el Inspector para que una decoración nueva entre
    // al movimiento sola, sin que haya que acordarse de agregarla.
    void CollectMovers()
    {
        _movers.Clear();
        _moverBase.Clear();

        var gridRect = (RectTransform)grid.transform;
        if (gridRect.parent is not RectTransform parent) return;

        foreach (Transform t in parent)
        {
            // Patrón y no cast directo: un hijo que no sea de UI reventaría el foreach entero.
            if (t is not RectTransform child || child == gridRect) continue;
            if (child.GetComponentInChildren<AimInputRelay>(true) != null) continue;

            _movers.Add(child);
            _moverBase.Add(child.anchoredPosition);
        }
    }

    void SetOffset(float y)
    {
        grid.IntroOffsetY = y;

        for (int i = 0; i < _movers.Count; i++)
            if (_movers[i]) _movers[i].anchoredPosition = _moverBase[i] + Vector2.up * y;
    }
}
