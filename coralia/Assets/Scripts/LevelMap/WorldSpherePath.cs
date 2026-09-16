using UnityEngine;

// Matemática del recorrido del mapa curvo, compartida por todo lo que se apoya sobre él: la cinta
// del camino, las decoraciones, y lo que venga.
//
// Está acá y no duplicada en cada script a propósito: si el camino y las decoraciones calcularan
// la curva por separado, cualquier ajuste en una tendría que replicarse a mano en la otra, y basta
// que se olvide una vez para que los adornos queden flotando al costado del camino.
//
// Todo trabaja con direcciones unitarias desde el centro de la esfera, en su espacio LOCAL, y con
// 'f' medido en índice de nodo: 0 = primer nodo, 2.5 = a mitad de camino entre el tercero y el cuarto.
public static class WorldSpherePath
{
    // Cuánto se avanza en 'f' para medir la tangente. Suficientemente chico para seguir la curva,
    // suficientemente grande para que la resta no pierda precisión.
    const float TANGENT_STEP = 0.05f;

    // Punto del camino en 'f'.
    //
    // Dentro del rango de nodos manda la spline. Fuera, la prolongación se hace con un arco RECTO
    // en la dirección de salida: extrapolar la spline hacia afuera hace que se curve sola — el
    // polinomio cúbico se dispara — y deja la punta torcida.
    public static Vector3 Sample(Vector3[] nodeDirs, float f, float anglePerNode)
    {
        if (nodeDirs == null || nodeDirs.Length == 0) return Vector3.forward;

        int last = nodeDirs.Length - 1;
        if (f >= 0f && f <= last) return SampleSpline(nodeDirs, f);

        bool  atStart = f < 0f;
        float overrun = atStart ? -f : f - last;

        Vector3 anchor = atStart ? nodeDirs[0] : nodeDirs[last];
        Vector3 inside = SampleSpline(nodeDirs, atStart ? 0.05f : last - 0.05f);

        Vector3 outward = anchor - inside;
        outward = (outward - Vector3.Dot(outward, anchor) * anchor).normalized;
        if (outward.sqrMagnitude < 0.5f) return anchor;

        float rad = overrun * anglePerNode * Mathf.Deg2Rad;
        return (anchor * Mathf.Cos(rad) + outward * Mathf.Sin(rad)).normalized;
    }

    // Marco de referencia del camino en 'f': hacia afuera de la esfera, y hacia el costado.
    // 'side' apunta al lado izquierdo visto desde la cámara.
    public static void SampleFrame(Vector3[] nodeDirs, float f, float anglePerNode,
                                   out Vector3 normal, out Vector3 side)
    {
        normal = Sample(nodeDirs, f, anglePerNode);

        Vector3 back    = Sample(nodeDirs, f - TANGENT_STEP, anglePerNode);
        Vector3 ahead   = Sample(nodeDirs, f + TANGENT_STEP, anglePerNode);
        Vector3 tangent = ahead - back;
        tangent = (tangent - Vector3.Dot(tangent, normal) * normal).normalized;

        side = tangent.sqrMagnitude < 0.5f
            ? Vector3.Cross(Vector3.up, normal).normalized
            : Vector3.Cross(tangent, normal).normalized;
    }

    // Punto del camino en 'f', corrido 'offsetWorld' unidades de mundo hacia el costado.
    // Positivo = izquierda. El corrimiento sigue la curvatura de la esfera, no una línea recta.
    public static Vector3 SampleOffset(Vector3[] nodeDirs, float f, float anglePerNode,
                                       float offsetWorld, float radiusWorld)
    {
        SampleFrame(nodeDirs, f, anglePerNode, out Vector3 normal, out Vector3 side);

        float rad = offsetWorld / Mathf.Max(radiusWorld, 0.0001f);
        return (normal * Mathf.Cos(rad) + side * Mathf.Sin(rad)).normalized;
    }

    // Catmull-Rom sobre las direcciones de los nodos: la curva pasa exactamente por cada nodo pero
    // llega y sale con la misma pendiente, que es lo que elimina el quiebre en cada nivel.
    static Vector3 SampleSpline(Vector3[] pts, float f)
    {
        // Con un solo punto no hay curva que interpolar, y sin esto revienta: el Clamp de abajo
        // recibe un máximo de -1 y devuelve -1, que entra como índice del array.
        //
        // Pasa de verdad, en la frontera entre capítulos: cuando la ventana visible asoma apenas
        // el primer nivel del capítulo siguiente, ese tramo se arma con un único nodo.
        if (pts.Length == 0) return Vector3.forward;
        if (pts.Length == 1) return pts[0];

        int   i = Mathf.Clamp(Mathf.FloorToInt(f), 0, pts.Length - 2);
        float t = f - i;

        Vector3 p0 = Control(pts, i - 1);
        Vector3 p1 = pts[i];
        Vector3 p2 = pts[i + 1];
        Vector3 p3 = Control(pts, i + 2);

        float t2 = t * t;
        float t3 = t2 * t;

        Vector3 p = 0.5f * ((2f * p1)
                          + (-p0 + p2) * t
                          + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                          + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);

        // La interpolación se va hacia adentro de la esfera; volver a la superficie es normalizar.
        return p.normalized;
    }

    // En los extremos no hay vecino, así que se espeja el siguiente — evita que la curva arranque
    // o termine con una pendiente inventada.
    static Vector3 Control(Vector3[] pts, int index)
    {
        if (index < 0)           return (2f * pts[0] - pts[1]).normalized;
        if (index >= pts.Length) return (2f * pts[pts.Length - 1] - pts[pts.Length - 2]).normalized;
        return pts[index];
    }
}
