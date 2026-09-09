using UnityEngine;

// Rotación continua en loop sobre el eje Z (pensado para uGUI) — útil para decoraciones tipo
// destello giratorio (ej. "Flashes" detrás de un ícono), pero sirve para cualquier elemento.
public class RotateAnimation : MonoBehaviour
{
    [Tooltip("Segundos que tarda en completar una vuelta de 360°. Más chico = gira más rápido.")]
    [SerializeField] float secondsPerRotation = 4f;
    [SerializeField] bool  clockwise = true;

    void Update()
    {
        // Z positivo gira en sentido antihorario en pantalla — se invierte el signo para que
        // "clockwise" se sienta como uno esperaría al mirarlo.
        float degreesPerSecond = 360f / secondsPerRotation;
        float delta = degreesPerSecond * Time.deltaTime * (clockwise ? -1f : 1f);
        transform.Rotate(0f, 0f, delta);
    }
}
