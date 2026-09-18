using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// El barrido de lectura del tablero al empezar un nivel largo (issue #74).
//
// En la posición de juego el grid se retira hacia arriba para no quedar encima del cañón, así que
// en un nivel alto las primeras filas arrancan fuera de cuadro. El jugador empieza a disparar sin
// haber visto con qué se va a encontrar.
//
// Esto baja la vista hasta el techo del tablero, se detiene un momento y vuelve. No toca la cámara
// ni la posición de juego: mueve el contenedor del grid con un offset propio de GridController que
// siempre termina en cero, así que al terminar todo queda exactamente donde quedaba antes.
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

        yield return Slide(0f, -travel, travelDuration, travelEase, abortOnSkip: true);

        for (float t = 0f; t < holdDuration && !_skipped; t += Time.deltaTime) yield return null;

        // La vuelta nunca se aborta: es la que devuelve el tablero a su sitio. Si el jugador saltó,
        // sale desde donde haya quedado la ida y en menos tiempo.
        yield return _skipped
            ? Slide(grid.IntroOffsetY, 0f, skipDuration,   returnEase, abortOnSkip: false)
            : Slide(-travel,           0f, returnDuration, returnEase, abortOnSkip: false);

        grid.IntroOffsetY = 0f; // exacto, sin arrastre de redondeo del último frame
        _running = false;
    }

    IEnumerator Slide(float from, float to, float duration, AnimationCurve ease, bool abortOnSkip)
    {
        if (duration <= 0f) { grid.IntroOffsetY = to; yield break; }

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (abortOnSkip && _skipped) yield break;
            grid.IntroOffsetY = Mathf.LerpUnclamped(from, to, ease.Evaluate(t / duration));
            yield return null;
        }

        grid.IntroOffsetY = to;
    }
}
