using UnityEngine;

// La forma del recorrido del mapa, compartida por todo lo que se apoya en él: la cinta del
// camino, los nodos y las decoraciones.
//
// Está acá y no repetida en cada script a propósito: si el camino y los adornos calcularan el
// trazado por separado, cualquier ajuste al zigzag tendría que replicarse a mano y basta
// olvidarlo una vez para que los adornos queden flotando al costado.
//
// Trabaja en el plano XZ: Z avanza con los niveles, X es el zigzag. La altura no existe acá —
// la curvatura visual la mete el shader al dibujar, no la matemática.
public static class WorldMapPath
{
    // Cuánto se avanza en 'f' para medir la tangente. Chico para seguir la curva, grande para
    // que la resta no pierda precisión.
    const float TANGENT_STEP = 0.05f;

    [System.Serializable]
    public struct Layout
    {
        [Tooltip("Cuántas unidades de mundo avanza el camino por cada nivel.")]
        public float spacing;

        [Tooltip("Cuánto se aparta del centro, a cada lado.")]
        public float zigzag;

        [Tooltip("Cada cuántos niveles completa una ese del zigzag.")]
        public float period;

        public static Layout Default => new Layout { spacing = 4f, zigzag = 5f, period = 6f };
    }

    // La posición cruda del nodo 'index'. Una senoidal y no un vaivén en pico: el camino tiene
    // que doblar suave, y un triángulo deja esquinas donde la cinta se quiebra.
    public static Vector3 Node(float index, Layout layout) => new Vector3(
        Mathf.Sin(index / Mathf.Max(0.01f, layout.period) * Mathf.PI * 2f) * layout.zigzag,
        0f,
        index * layout.spacing);

    // El punto del camino en 'f', con f en índice de nivel: 0 = el primero, 2.5 = a mitad entre
    // el tercero y el cuarto.
    //
    // La senoidal ya es continua, así que no hace falta interpolar entre nodos — se evalúa
    // directo en el valor fraccionario y sale suave por construcción.
    public static Vector3 Sample(float f, Layout layout) => Node(f, layout);

    // Punto y dirección de avance. La tangente sirve para orientar lo que va sobre el camino y
    // para sacar la perpendicular por la que se corren las decoraciones.
    public static void SampleFrame(float f, Layout layout, out Vector3 point, out Vector3 forward)
    {
        point   = Sample(f, layout);
        forward = (Sample(f + TANGENT_STEP, layout) - Sample(f - TANGENT_STEP, layout)).normalized;

        // En el arranque de un capítulo con zigzag en 0 la resta puede dar cero: sin esto, todo
        // lo que se oriente con la tangente apuntaría a cualquier lado ese frame.
        if (forward.sqrMagnitude < 0.5f) forward = Vector3.forward;
    }

    // Un punto corrido a un costado del camino. Positivo va a la derecha.
    //
    // perpendicular = true sigue la normal del camino, así que en un tramo diagonal la pieza se
    // corre TAMBIÉN hacia adelante o hacia atrás. Es lo correcto para bordear el camino como un
    // árbol bordea una carretera, pero desalinea la pieza del nodo con el que comparte índice.
    //
    // perpendicular = false aparta solo en X, manteniendo la misma profundidad. La pieza queda a
    // la misma altura de pantalla que su nodo, a costa de acercarse al camino en las diagonales.
    public static Vector3 SampleOffset(float f, Layout layout, float offset, bool perpendicular = true)
    {
        Vector3 point = Sample(f, layout);

        if (!perpendicular)
        {
            point.x += offset;
            return point;
        }

        SampleFrame(f, layout, out point, out var forward);

        // El costado sale de cruzar con el eje vertical y no de rotar 90° en X, así el offset
        // sigue siendo horizontal por más que el camino se incline.
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        return point + right * offset;
    }
}
