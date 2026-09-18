using System.Collections.Generic;
using UnityEngine;

// Los números que definen el mundo: la forma del trazado, cuántos niveles tiene y dónde empieza
// cada capítulo. Todo lo que se apoya en el mapa — el suelo, el camino, la banda, los nodos, las
// decoraciones — lo lee de acá.
//
// Los capítulos NO son un continuo: cada uno es una isla con su propio suelo y su propio camino,
// separada de la siguiente. Esa separación se mide en "niveles de aire" y se resuelve en un solo
// lugar, acá: si cada componente la calculara por su cuenta, bastaría que uno usara otro hueco
// para que el camino de un capítulo quedara desalineado con su suelo.
[ExecuteAlways]
public class WorldMapDefinition : MonoBehaviour
{
    [SerializeField] WorldMapPath.Layout layout = WorldMapPath.Layout.Default;

    [Tooltip("Cuántos niveles de aire quedan entre un capítulo y el siguiente.")]
    [SerializeField] float chapterGap = 6f;

    [Tooltip("Cuántos niveles dibujar. En 0 usa los que haya en el índice de niveles.")]
    [SerializeField] int levelsOverride;

    [Header("Ventana de capítulos")]
    [Tooltip("De dónde se lee la posición para saber en qué capítulo está el jugador.")]
    [SerializeField] WorldMapScroll scroll;

    [Tooltip("Cuántos capítulos se mantienen construidos a cada lado del actual. 1 = el anterior, el actual y el siguiente.")]
    [SerializeField] int chapterRadius = 1;

    [Header("Orden de dibujado")]
    [Tooltip("Cuántos escalones de orden por unidad de mundo. Más alto separa mejor piezas cercanas, pero el orden tiene tope (±32767) y un mundo largo lo puede pasar.")]
    [SerializeField] float orderPerUnit = 4f;

    // Un capítulo colocado en el mundo.
    public struct Span
    {
        public int   number;      // el número de capítulo
        public int   firstLevel;  // índice global de su primer nivel, dentro de LevelLoader.Entries
        public int   count;       // cuántos niveles tiene
        public float start;       // en qué índice DE MUNDO arranca, ya con las separaciones sumadas

        public float End => start + Mathf.Max(0, count - 1);
    }

    public WorldMapPath.Layout Layout => layout;

    // Del índice real, salvo que se pise a mano para probar. Así la escena no depende de que
    // alguien mantenga un número sincronizado con la cantidad de archivos de nivel.
    public int Levels => levelsOverride > 0
        ? Mathf.Min(levelsOverride, Mathf.Max(1, LevelLoader.Entries.Count))
        : Mathf.Max(1, LevelLoader.Entries.Count);

    // Los capítulos con su sitio ya resuelto. Se recalcula al pedirlo: son unas pocas decenas de
    // entradas y evita tener que acordarse de invalidar una caché al cambiar cualquier número.
    public List<Span> Spans()
    {
        var entries = LevelLoader.Entries;
        var spans   = new List<Span>();

        int total = Mathf.Min(entries.Count, Levels);
        if (total == 0) return spans;

        int   start = 0;
        float world = 0f;

        for (int i = 1; i <= total; i++)
        {
            bool last = i == total;
            if (!last && entries[i].chapter == entries[start].chapter) continue;

            spans.Add(new Span
            {
                number     = entries[start].chapter,
                firstLevel = start,
                count      = i - start,
                start      = world,
            });

            world += (i - start - 1) + chapterGap;
            start  = i;
        }

        return spans;
    }

    // Los capítulos que EXISTEN ahora mismo: el actual y los que tiene al lado. El resto no se
    // construye.
    //
    // Es lo que permite que el mundo no tenga tamaño: con tres capítulos o con cuarenta, en la
    // escena siempre hay tres islas. Un suelo único con todos sería una malla que crece sin
    // freno y que igual solo se ve de a un trozo.
    public List<Span> VisibleSpans()
    {
        var spans = Spans();
        if (spans.Count == 0 || chapterRadius < 0) return spans;

        int current = CurrentSpan(spans);
        int first   = Mathf.Max(0, current - chapterRadius);
        int last    = Mathf.Min(spans.Count - 1, current + chapterRadius);

        return spans.GetRange(first, last - first + 1);
    }

    // De dónde sale la posición del jugador. Se busca sola si nadie la asignó, y UNA sola vez:
    // esto se lee en cada frame, y sin el campo puesto el mapa se quedaba clavado en el capítulo 1
    // — la ventana no rotaba nunca y las islas siguientes no llegaban a construirse.
    WorldMapScroll Scroll
    {
        get
        {
            if (scroll != null || _lookedForScroll) return scroll;

            _lookedForScroll = true;
            scroll = FindAnyObjectByType<WorldMapScroll>();

            if (scroll == null)
                Debug.LogWarning("[WorldMapDefinition] No hay ningún WorldMapScroll en la escena — " +
                                 "el mapa se queda siempre en el primer capítulo.", this);
            return scroll;
        }
    }

    bool _lookedForScroll;

    // En qué capítulo está la cámara. Se mide contra el final de cada isla: mientras el scroll no
    // la haya pasado, ese sigue siendo el capítulo actual.
    int CurrentSpan(List<Span> spans)
    {
        var source = Scroll;
        float index = (source ? source.Offset : 0f) / Mathf.Max(0.01f, layout.spacing);

        for (int i = 0; i < spans.Count; i++)
            if (index <= spans[i].End) return i;

        return spans.Count - 1;
    }

    // Quién se pinta encima de quién, por profundidad: lo más cercano a la cámara, primero.
    //
    // Vive acá y no en las decoraciones porque los nodos tienen que usar EL MISMO número. Con
    // escalas distintas un nodo puede quedar detrás de una roca que está más lejos que él, que es
    // exactamente lo que pasaba: las decoraciones escalaban con la profundidad y los nodos no.
    //
    // Sin esto Unity ordena los transparentes por distancia al CENTRO de sus bounds, y una roca
    // alta y lejana le gana a un coral bajo y cercano.
    public int SortingOrder(float z) =>
        Mathf.Clamp(Mathf.RoundToInt(-z * orderPerUnit), -ORDER_LIMIT, ORDER_LIMIT);

    // El tope del orden por profundidad. Existe porque sortingOrder es un short (±32767) y un
    // mundo largo se pasaría.
    public const int ORDER_LIMIT = 20000;

    // El suelo, el camino y la banda van POR DEBAJO de cualquier decoración, pase lo que pase.
    //
    // No pueden entrar en el reparto por profundidad: son una malla cada uno que cubre el mundo
    // entero, así que un único número tendría que valer para el trozo cercano y para el lejano a
    // la vez. Como son piso y las decoraciones se paran encima, la respuesta correcta es siempre
    // la misma y no hace falta calcular nada.
    //
    // Entre ellos sí hay orden: tierra, camino y encima la banda.
    public const int ORDER_GROUND = -(ORDER_LIMIT + 3);
    public const int ORDER_PATH   = -(ORDER_LIMIT + 2);
    public const int ORDER_TRAIL  = -(ORDER_LIMIT + 1);

    // Qué niveles caen dentro de un tramo del mundo, devueltos como índices de
    // LevelLoader.Entries. Falso si no hay ninguno — pasa cuando la cámara está sobre el hueco
    // entre dos islas.
    //
    // Hace falta porque el mundo y el índice de niveles NO son la misma escala: entre capítulo y
    // capítulo hay 'chapterGap' niveles de aire. Quien quiera saber qué nodos dibujar tiene que
    // preguntarlo acá; deducirlo dividiendo la posición por el espaciado se desfasa un capítulo
    // entero cada pocos capítulos, y los nodos dejan de aparecer.
    public bool LevelsBetween(float worldFrom, float worldTo, out int first, out int last)
    {
        first = int.MaxValue;
        last  = int.MinValue;

        foreach (var span in Spans())
        {
            if (span.End < worldFrom || span.start > worldTo) continue;

            int from = Mathf.Max(0,             Mathf.FloorToInt(worldFrom - span.start));
            int to   = Mathf.Min(span.count - 1, Mathf.CeilToInt (worldTo   - span.start));
            if (to < from) continue;

            first = Mathf.Min(first, span.firstLevel + from);
            last  = Mathf.Max(last,  span.firstLevel + to);
        }

        return last >= first;
    }

    // Dónde cae un nivel en el mundo, contando las separaciones entre capítulos.
    public float WorldIndex(int levelIndex)
    {
        foreach (var span in Spans())
            if (levelIndex >= span.firstLevel && levelIndex < span.firstLevel + span.count)
                return span.start + (levelIndex - span.firstLevel);

        return levelIndex;
    }

    // Cuánto mide el mundo entero de punta a punta, separaciones incluidas.
    public float Length
    {
        get
        {
            var spans = Spans();
            return spans.Count == 0 ? 0f : spans[^1].End;
        }
    }

    int _builtSpan = -1;

    // La ventana se revisa por frame: cuando el jugador cruza a otro capítulo hay que construir
    // el que entra y soltar el que sale. No hace nada mientras no cambie.
    void LateUpdate()
    {
        var spans = Spans();
        if (spans.Count == 0) return;

        int current = CurrentSpan(spans);
        if (current == _builtSpan) return;

        _builtSpan = current;
        Rebuild();
    }

    void Rebuild()
    {
        foreach (var rebuildable in GetComponentsInChildren<IWorldMapRebuildable>(true))
            rebuildable.Rebuild();
    }

    void OnValidate()
    {
        _lookedForScroll = false;

        // Los hijos son los que arman las mallas, así que el cambio hay que reenviárselo: acá
        // solo cambiaron números.
        _builtSpan = -1;
        Rebuild();
    }

    // Lo busca hacia arriba en la jerarquía: los componentes del mapa cuelgan todos del mismo
    // objeto raíz, así que no hace falta asignarlo a mano en cada uno.
    public static WorldMapDefinition For(Component component)
    {
        var found = component.GetComponentInParent<WorldMapDefinition>(true);
        if (found == null)
            Debug.LogWarning($"[{component.GetType().Name}] No hay ningún WorldMapDefinition " +
                             "en sus padres — el mapa no sabe qué forma tiene ni cuántos niveles hay.");
        return found;
    }
}
