using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Dibuja el camino que une los nodos: una cinta de malla apoyada sobre la esfera, que sigue el
// mismo recorrido (incluido el zigzag) y se corta entre capítulos.
//
// Tiene que ser una malla y no un sprite ni un LineRenderer: el camino se curva con el planeta y
// se pierde en el horizonte, y eso solo lo hace geometría de verdad apoyada en la superficie.
//
// El trazado pasa por los mismos puntos que los nodos (los toma de WorldSphereNodePositioner, así
// no pueden quedar desalineados), pero con una spline Catmull-Rom en vez de segmentos rectos — si
// no, en cada nodo se vería un quiebre en vez de una curva.
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class WorldSpherePathRibbon : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("De acá salen el recorrido y los capítulos. Lo llama él mismo al terminar de armar los nodos.")]
    [SerializeField] WorldSphereNodePositioner positioner;
    [Tooltip("Material del camino. La textura conviene que tenga Wrap Mode = Repeat para que se repita a lo largo.")]
    [SerializeField] Material pathMaterial;

    [Header("Forma")]
    [Tooltip("Ancho del camino en unidades de MUNDO (mismo criterio que 'Node Size').")]
    [SerializeField] float width = 1.6f;
    [Tooltip("Cuánto se levanta de la superficie, en unidades de MUNDO. Lo justo para no pelear en profundidad con el suelo.")]
    [SerializeField] float lift = 0.03f;
    [Tooltip("Cuánto se prolonga el camino antes del primer nodo y después del último, en grados.")]
    [SerializeField] float extend = 6f;
    [Tooltip("Largo del remate de las puntas, en unidades de MUNDO. 0 = corte recto. Igual a la mitad del ancho = semicírculo. Más que eso = cola afinada, que es lo que mejor queda en caminos anchos.")]
    [SerializeField] float endCapLength = 2.5f;

    [Header("Calidad")]
    [Tooltip("Cada cuántas unidades de MUNDO se genera un tramo. Más chico = curva más suave y puntas mejor redondeadas, más triángulos.")]
    [SerializeField] float sampleLength = 0.25f;

    [Header("Textura")]
    [Tooltip("Mantiene la textura sin deformar: a lo largo se repite cada tantas unidades como mide el ancho del camino. Desmárcalo para fijar el largo a mano.")]
    [SerializeField] bool  textureKeepAspect = true;
    [Tooltip("Solo si 'Texture Keep Aspect' está desmarcado. Cuántas unidades de MUNDO ocupa una repetición completa a lo largo del camino.")]
    [SerializeField] float textureLength = 4f;

    // Cuántos niveles de más se generan a cada lado de la ventana visible, para que el camino
    // nunca termine justo en el borde de la pantalla.
    const int WINDOW_MARGIN = 4;

    Mesh _mesh;

    void OnEnable()
    {
        if (positioner) positioner.OnWindowChanged += Build;
    }

    void OnDisable()
    {
        if (positioner) positioner.OnWindowChanged -= Build;
    }

    // Lo llama WorldSphereNodePositioner cuando ya tiene el recorrido listo — no se puede hacer en
    // Start() propio porque Unity no garantiza el orden entre los dos.
    public void Build()
    {
        if (!positioner)
        {
            Debug.LogWarning("[WorldSpherePathRibbon] Falta asignar 'Positioner' en el Inspector — no se dibuja el camino.", this);
            return;
        }

        var chapters = positioner.NodeChapters;
        int count    = positioner.LevelCount;
        if (count < 2) return;

        // Solo el tramo que está en pantalla, más margen. Reconstruir esto es barato (unos cientos
        // de vértices) y es lo que permite que un capítulo tenga cientos de niveles sin que el
        // camino dé la vuelta a la esfera y se pise a sí mismo.
        int from = Mathf.Max(0, positioner.WindowFirst - WINDOW_MARGIN);
        int to   = Mathf.Min(count - 1, positioner.WindowLast + WINDOW_MARGIN);

        if (!pathMaterial)
            Debug.LogWarning("[WorldSpherePathRibbon] Falta asignar 'Path Material' — el camino se va a ver con el material rosa de error.", this);

        float radius      = positioner.SurfaceRadiusLocal + lift / Mathf.Max(positioner.SphereScale, 0.0001f);
        float radiusWorld = radius * positioner.SphereScale;
        float halfWidth   = width * 0.5f;
        // El ancho se recorre como un arco sobre la superficie, no como un desplazamiento plano:
        // así mide lo mismo en todo el recorrido y nunca se hunde por debajo del suelo.
        float halfWidthRad = halfWidth / Mathf.Max(radiusWorld, 0.0001f);

        var verts = new List<Vector3>();
        var uvs   = new List<Vector2>();
        var norms = new List<Vector3>();
        var tris  = new List<int>();

        // Un tramo por capítulo: entre capítulos el camino se corta, que es lo que hace que se
        // lean como etapas separadas y no como un continuo.
        int runStart = from;
        for (int i = from + 1; i <= to + 1; i++)
        {
            bool isBreak = i > to
                        || (chapters != null && i < chapters.Count && chapters[i] != chapters[runStart]);
            if (!isBreak) continue;

            BuildRun(runStart, i - 1, count, chapters, radius, radiusWorld, halfWidthRad, halfWidth,
                     verts, uvs, norms, tris);
            runStart = i;
        }

        if (!_mesh) _mesh = new Mesh { name = "PathRibbon", hideFlags = HideFlags.DontSave };
        _mesh.Clear();
        _mesh.indexFormat = verts.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        _mesh.SetVertices(verts);
        _mesh.SetUVs(0, uvs);
        _mesh.SetNormals(norms);
        _mesh.SetTriangles(tris, 0);
        _mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = _mesh;
        if (pathMaterial) GetComponent<MeshRenderer>().sharedMaterial = pathMaterial;
    }

    // Un tramo continuo de camino, del nodo 'first' al nodo 'last'.
    void BuildRun(int first, int last, int count, IReadOnlyList<int> chapters,
                  float radius, float radiusWorld, float halfWidthRad, float halfWidth,
                  List<Vector3> verts, List<Vector2> uvs, List<Vector3> norms, List<int> tris)
    {
        if (last <= first) return; // un tramo de un solo nodo no tiene camino que dibujar

        int segments = last - first;

        // Los puntos por los que tiene que pasar la curva: la dirección de cada nodo desde el
        // centro de la esfera.
        var nodeDirs = new Vector3[segments + 1];
        for (int j = 0; j <= segments; j++)
            nodeDirs[j] = (positioner.RestRotation(first + j) * Vector3.back).normalized;

        // El remate redondeado solo va en los extremos REALES del capítulo. En el borde de la
        // ventana el camino tiene que salir cortado a secas: si se le pusiera punta, se vería el
        // camino afinarse en el aire a media pantalla cada vez que la ventana se corre.
        bool capStart = first == 0 || (chapters != null && first < chapters.Count && chapters[first] != chapters[first - 1]);
        bool capEnd   = last == count - 1 || (chapters != null && last + 1 < chapters.Count && chapters[last + 1] != chapters[last]);

        // Desde dónde se empieza a contar la textura. Tiene que ser el principio del CAPÍTULO y no
        // el de la ventana: si arrancara en cero en cada reconstrucción, el patrón saltaría a otra
        // posición cada vez que la ventana avanza, y se ve como si el piso se moviera solo.
        int chapterStart = first;
        if (chapters != null)
            while (chapterStart > 0 && chapters[chapterStart - 1] == chapters[first]) chapterStart--;

        float baseDist = (positioner.RestAngleAt(first) - positioner.RestAngleAt(chapterStart))
                       * Mathf.Deg2Rad * radiusWorld;

        // Cuánto se prolonga más allá del primer y último nodo, en "nodos", para poder extrapolar
        // con la misma spline del resto del recorrido.
        float overrunStart = capStart ? extend / Mathf.Max(positioner.AngleSpacing, 0.0001f) : 0f;
        float overrunEnd   = capEnd   ? extend / Mathf.Max(positioner.AngleSpacing, 0.0001f) : 0f;
        float overrun = overrunStart; // el muestreo arranca acá
        float spanF   = segments + overrunStart + overrunEnd;

        // La densidad se fija por longitud real y no por nodo: así las puntas redondeadas tienen
        // suficientes tramos para verse curvas aunque el camino sea corto.
        float approxLength = spanF * positioner.AngleSpacing * Mathf.Deg2Rad * radiusWorld;
        int   rows = Mathf.Clamp(Mathf.CeilToInt(approxLength / Mathf.Max(sampleLength, 0.01f)), 2, 4000);

        // Subdivisión a lo ancho, calculada sola: un vértice cada ~1.5 grados de arco. Con solo
        // dos bordes, el cuadrilátero plano que los une sería una cuerda y se hundiría bajo la
        // superficie curva, dejando huecos por donde asoma el suelo.
        int across = Mathf.Clamp(Mathf.CeilToInt(halfWidthRad * 2f * Mathf.Rad2Deg / 1.5f) + 1, 2, 32);

        // Muestreo auxiliar parejo, solo para conocer la curva y su largo real. Las filas de la
        // malla NO son estas: se eligen después, para poder concentrarlas donde hacen falta.
        var probe     = new Vector3[rows + 1];
        var probeDist = new float[rows + 1];

        for (int r = 0; r <= rows; r++)
        {
            float f = -overrun + (float)r / rows * spanF;
            probe[r] = WorldSpherePath.Sample(nodeDirs, f, positioner.AngleSpacing);
            if (r > 0) probeDist[r] = probeDist[r - 1] + Vector3.Angle(probe[r - 1], probe[r]) * Mathf.Deg2Rad * radiusWorld;
        }

        float totalLength = probeDist[rows];
        // Si el tramo es más corto que dos remates, los dos se comerían el camino entero y
        // quedaría una lente en vez de una ruta. Se achican para que siempre quede tramo recto.
        float capLength     = Mathf.Min(Mathf.Max(endCapLength, 0f), totalLength * 0.45f);
        float capLengthStart = capStart ? capLength : 0f;
        float capLengthEnd   = capEnd   ? capLength : 0f;

        var rowDist = BuildRowDistances(totalLength, capLengthStart, capLengthEnd);
        var centers = new Vector3[rowDist.Count];
        for (int r = 0; r < rowDist.Count; r++)
            centers[r] = SampleAtDistance(probe, probeDist, rowDist[r]);

        rows = rowDist.Count - 1;
        int baseIndex = verts.Count;

        // La tangente se mide a una distancia FIJA hacia adelante y atrás, no entre filas vecinas.
        // Dentro de los remates las filas quedan a micras una de otra y la resta pierde precisión:
        // la dirección "a lo ancho" se sacude y deja un vértice corrido, que se ve como una puntita.
        // Ventana amplia a propósito: el zigzag dobla a lo largo de decenas de unidades, así que
        // medir sobre media unidad no suaviza la forma pero sí elimina el temblor numérico.
        float tangentStep = Mathf.Max(sampleLength * 3f, 0.4f);

        for (int r = 0; r <= rows; r++)
        {
            Vector3 dir  = centers[r];
            Vector3 prev = SampleAtDistance(probe, probeDist, rowDist[r] - tangentStep);
            Vector3 next = SampleAtDistance(probe, probeDist, rowDist[r] + tangentStep);

            // Tangente del camino en este punto, tumbada sobre la superficie.
            Vector3 tangent = next - prev;
            tangent = (tangent - Vector3.Dot(tangent, dir) * dir).normalized;
            if (tangent.sqrMagnitude < 0.5f) tangent = Vector3.up; // tramo degenerado, no debería pasar

            Vector3 side = Vector3.Cross(tangent, dir).normalized;

            // Las puntas se afinan siguiendo un perfil de semicírculo, así el final del camino
            // queda redondeado en vez de cortado en seco.
            float taper = Taper(rowDist[r], capLengthStart) * Taper(totalLength - rowDist[r], capLengthEnd);
            float half  = halfWidthRad * taper;
            // A lo ancho la textura entra una vez en 'width'. Para que no salga deformada, a lo
            // largo tiene que repetirse a ese mismo ritmo — si no, se estira en el sentido de avance.
            float repeat = textureKeepAspect ? width : textureLength;
            float v      = (baseDist + rowDist[r]) / Mathf.Max(repeat, 0.0001f);

            for (int c = 0; c < across; c++)
            {
                float u = (float)c / (across - 1);
                float a = Mathf.Lerp(half, -half, u);

                // Punto sobre la superficie, desplazado 'a' radianes de arco hacia el costado.
                Vector3 point = (dir * Mathf.Cos(a) + side * Mathf.Sin(a)).normalized;

                verts.Add(point * radius);
                norms.Add(point);
                uvs.Add(new Vector2(u, v));
            }
        }

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < across - 1; c++)
            {
                int a = baseIndex + r * across + c;
                int b = a + 1;
                int cc = a + across;
                int d = cc + 1;

                tris.Add(a); tris.Add(cc); tris.Add(b);
                tris.Add(b); tris.Add(cc); tris.Add(d);
            }
        }
    }

    // Dónde va cada fila de la malla, medido en longitud de arco desde el principio del tramo.
    //
    // En el cuerpo del camino van parejas, pero en los remates se reparten POR ÁNGULO del
    // semicírculo, no por longitud: la punta tiene curvatura altísima, y con filas parejas le
    // tocaba una sola, que la dejaba en pico. Repartidas por ángulo, la punta queda redonda sin
    // tener que subir la resolución de todo el resto del camino.
    List<float> BuildRowDistances(float totalLength, float capLengthStart, float capLengthEnd)
    {
        var result = new List<float>();
        float step = Mathf.Max(sampleLength, 0.01f);

        if (capLengthStart > 0.0001f)
        {
            int capSteps = Mathf.Clamp(Mathf.CeilToInt(capLengthStart / step) * 3, 8, 64);
            for (int k = 0; k <= capSteps; k++)
            {
                float angle = (float)k / capSteps * Mathf.PI * 0.5f;
                result.Add(capLengthStart * (1f - Mathf.Cos(angle)));
            }
        }
        else
        {
            result.Add(0f);
        }

        // El cuerpo se divide en partes IGUALES entre los dos remates, en vez de avanzar de a un
        // paso fijo y cortar donde toque. Así no queda un tramo suelto más largo justo en la unión
        // con el remate — que en una curva se ve como una faceta recta de un solo lado.
        float bodyLength = totalLength - capLengthStart - capLengthEnd;
        int   bodySteps  = Mathf.Max(1, Mathf.CeilToInt(bodyLength / step));
        for (int k = 1; k < bodySteps; k++)
            result.Add(capLengthStart + bodyLength * k / bodySteps);

        if (capLengthEnd > 0.0001f)
        {
            int capSteps = Mathf.Clamp(Mathf.CeilToInt(capLengthEnd / step) * 3, 8, 64);
            for (int k = capSteps; k >= 0; k--)
            {
                float angle = (float)k / capSteps * Mathf.PI * 0.5f;
                result.Add(totalLength - capLengthEnd * (1f - Mathf.Cos(angle)));
            }
        }
        else
        {
            result.Add(totalLength);
        }

        return result;
    }

    // Punto de la curva a una distancia dada, leído del muestreo auxiliar. Interpolar entre dos
    // muestras vecinas alcanza y sobra: están mucho más juntas que las filas finales.
    static Vector3 SampleAtDistance(Vector3[] probe, float[] probeDist, float distance)
    {
        int last = probe.Length - 1;
        if (distance <= 0f)               return probe[0];
        if (distance >= probeDist[last])  return probe[last];

        int i = 0;
        while (i < last && probeDist[i + 1] < distance) i++;

        float span = probeDist[i + 1] - probeDist[i];
        float t    = span > 0.0001f ? (distance - probeDist[i]) / span : 0f;
        return Vector3.Slerp(probe[i], probe[i + 1], t);
    }

    // Perfil de la punta: 0 justo en el extremo y 1 a 'capLength' de distancia, siguiendo un
    // cuarto de círculo. Eso es lo que redondea el final en vez de dejarlo recto.
    static float Taper(float distanceFromEnd, float capLength)
    {
        if (capLength <= 0.0001f) return 1f;
        if (distanceFromEnd >= capLength) return 1f;

        float x = (capLength - Mathf.Max(distanceFromEnd, 0f)) / capLength;
        return Mathf.Sqrt(Mathf.Max(0f, 1f - x * x));
    }

}
