using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// El barrido de lectura del tablero al empezar un nivel largo (issue #74).
//
// En la posición de juego el grid se retira hacia arriba para no quedar encima del cañón, así que
// en un nivel alto las primeras filas arrancan fuera de cuadro. El jugador empieza a disparar sin
// haber visto con qué se va a encontrar.
//
// Esto baja la vista hasta el techo del tablero, se detiene un momento y vuelve. No toca la cámara
// ni la posición de juego: es un offset propio que siempre termina en cero, así que al terminar
// todo queda exactamente donde quedaba antes.
//
// Se mueve el tablero Y TODO LO QUE LO ACOMPAÑA — el cañón, sus gráficos, la línea de trayectoria,
// las rocas de los costados. O sea, un barrido de cámara y no un tablero deslizándose.
//
// Que se muevan juntos no es solo estético: la posición del cañón se guarda UNA vez, medida en el
// espacio del contenedor del grid (CannonController._muzzleLocalBase). Si el tablero se moviera
// solo, esa medida quedaría desfasada todo el barrido y cualquier cosa que dibuje desde el cañón
// —la mira, el aviso de "mantené para apuntar"— saldría a cientos de píxeles de donde va. Moviendo
// los dos, la distancia entre ellos no cambia y no hay nada que compensar.
//
// Se mueven los HIJOS del SafeArea y no el SafeArea mismo, porque de ese lo manda SafeAreaPanel:
// le pone los offsets en cero al arrancar y otra vez un frame después, justo encima del barrido.
//
// Ninguna de las dos cantidades que usa es un número a mano:
//
//   - CUÁNDO vale la pena — cuánto del tablero quedó realmente tapado (GridController.HiddenAbove).
//   - HASTA DÓNDE bajar — el retiro (GridController.ScrollOffsetY), o sea volver a la posición
//     BASE del contenedor. Esa es, por definición, donde se ve un tablero que entra entero: el
//     extremo de arriba se muestra igual que en un nivel corto, sin inventar un encuadre nuevo.
//
// Un nivel más largo o un dispositivo más alto los cambian solos.
public class GridIntroPreview : MonoBehaviour
{
    [SerializeField] GridController grid;

    [Tooltip("Cuántas filas tienen que quedar fuera de cuadro para que el barrido valga la pena. Por debajo de esto el tablero se considera que entra entero y no pasa nada.")]
    [SerializeField] float minHiddenRows = 1f;

    [Header("Recorrido")]
    [Tooltip("Bajar hasta el techo del tablero.")]
    [SerializeField] float travelDuration = 0.5f;

    [Tooltip("Cuánto se queda quieto arriba, para que se alcance a leer.")]
    [SerializeField] float holdDuration = 0.35f;

    [Tooltip("Volver a la posición de juego. Algo más lento que la ida: la ida es el barrido, la vuelta es el nivel acomodándose.")]
    [SerializeField] float returnDuration = 0.65f;

    [SerializeField] AnimationCurve travelEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] AnimationCurve returnEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Cuánto tarda la vuelta cuando el jugador toca la pantalla para saltarse el barrido. Corto pero no instantáneo: el tablero puede tener varios cientos de píxeles por recorrer, y un salto seco se lee como un error.")]
    [SerializeField] float skipDuration = 0.12f;

    bool _running;
    bool _skipped;

    // Los hermanos del grid que acompañan el barrido, con la posición en la que estaban al
    // empezar. Se toman al arrancar el barrido y no en Awake: para entonces el SafeAreaPanel y
    // cualquier otro acomodo de arranque ya terminaron.
    readonly List<RectTransform> _movers    = new();
    readonly List<Vector2>       _moverBase = new();

    // El grid es el único de la escena, así que no vale la pena obligar a arrastrarlo: se busca
    // solo y solo se avisa si de verdad no está.
    void Awake()
    {
        if (grid == null) grid = FindAnyObjectByType<GridController>();
        if (grid == null)
            Debug.LogWarning("[GridIntroPreview] No hay ningún GridController en la escena — los niveles largos van a empezar sin el barrido de vista previa.", this);
    }

    // Si el barrido va a correr o no. Lo pregunta GameplayController ANTES de bloquear el input:
    // apagarlo y encenderlo en el mismo frame para nada le apagaría de paso el aviso del primer
    // disparo (ver CannonController.SetInputEnabled).
    public bool WillPlay => grid != null && grid.HiddenAbove >= minHiddenRows * HexGridMath.RowHeight;

    void Update()
    {
        if (!_running || _skipped) return;

        // Cualquier toque lo salta. No pasa por el EventSystem a propósito: AimArea cubre la
        // pantalla entera, así que filtrar por "tocó UI" no distinguiría nada.
        bool tapped = (Mouse.current       != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                      (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);

        if (tapped) _skipped = true;
    }

    public IEnumerator Play()
    {
        if (grid == null) yield break; // ya se avisó en Awake

        if (grid.HiddenAbove < minHiddenRows * HexGridMath.RowHeight) yield break; // ya se ve entero

        float travel = grid.ScrollOffsetY; // bajar esto devuelve el contenedor a su posición base

        _running = true;
        _skipped = false;

        CollectMovers();

        yield return Slide(0f, -travel, travelDuration, travelEase, abortOnSkip: true);

        for (float t = 0f; t < holdDuration && !_skipped; t += Time.deltaTime) yield return null;

        // La vuelta nunca se aborta: es la que devuelve el tablero a su sitio. Si el jugador saltó,
        // sale desde donde haya quedado la ida y en menos tiempo.
        yield return _skipped
            ? Slide(grid.IntroOffsetY, 0f, skipDuration,   returnEase, abortOnSkip: false) // donde quedó la ida
            : Slide(-travel,           0f, returnDuration, returnEase, abortOnSkip: false);

        SetOffset(0f); // exacto, sin arrastre de redondeo del último frame
        _movers.Clear();
        _moverBase.Clear();
        _running = false;
    }

    // Todo lo que cuelga del mismo padre que el grid, menos dos cosas: el grid, que se mueve por su
    // cuenta (GridController compone ese offset con el retiro y el temblor, y escribir su posición
    // desde afuera se los pisaría), y la zona de input, que tiene que seguir cubriendo la pantalla
    // entera para que el toque que salta el barrido llegue igual.
    //
    // Sale de la jerarquía y no de una lista en el Inspector para que una decoración nueva entre
    // al barrido sola, sin que haya que acordarse de agregarla.
    void CollectMovers()
    {
        _movers.Clear();
        _moverBase.Clear();

        var gridRect = (RectTransform)grid.transform;
        if (gridRect.parent is not RectTransform parent) return;

        foreach (Transform t in parent)
        {
            // 'as' y no un cast: un hijo que no sea de UI reventaría el foreach entero.
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

    IEnumerator Slide(float from, float to, float duration, AnimationCurve ease, bool abortOnSkip)
    {
        if (duration <= 0f) { SetOffset(to); yield break; }

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (abortOnSkip && _skipped) yield break;
            SetOffset(Mathf.LerpUnclamped(from, to, ease.Evaluate(t / duration)));
            yield return null;
        }

        SetOffset(to);
    }
}
