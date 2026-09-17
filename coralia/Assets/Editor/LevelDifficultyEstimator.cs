using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Estima cuántos disparos cuesta limpiar un nivel y en qué rango de dificultad cae, para no tener
// que jugarlo entero solo para saber si max_shots quedó holgado o imposible.
//
// Cómo lo calcula: simula una partida jugada perfecto. En cada turno mira todos los grupos de
// color que hay, revienta el que más burbujas se lleva por disparo — contando las que quedan
// colgando y caen — y repite hasta vaciar el grid. Usa las MISMAS dos reglas que el juego:
// BubbleColorExtensions.LinksWith para el match y la BFS desde la fila 0 para la caída.
//
// Es una cota OPTIMISTA y hay que leerla así: asume que cada disparo entra exactamente donde
// conviene y que nunca se falla. Un jugador real necesita más. Por eso el resultado no se usa como
// max_shots sino como base a la que se le suma un margen según qué tan difícil se quiera el nivel.
public static class LevelDifficultyEstimator
{
    public enum Difficulty { Imposible, Dificil, Medio, Facil, Trivial }

    // Margen (max_shots / disparos realistas) donde cae cada rango. Un margen de 1.0 significa que
    // el nivel solo se gana sin desperdiciar un solo disparo.
    //
    // Tienen que quedar por encima de los SUGGEST_* de abajo: si no, aplicar la sugerencia de una
    // dificultad devolvería un nivel clasificado en otra.
    const float MARGIN_HARD    = 1.42f;
    const float MARGIN_MEDIUM  = 1.70f;
    const float MARGIN_EASY    = 2.25f;

    // Márgenes con los que se sugiere max_shots. Van ENCIMA de los disparos realistas, que ya
    // traen su propio recargo por azar de color y por fallar: los dos se suman, no se reemplazan.
    // Un nivel fácil con cinco colores termina con bastante más aire que uno fácil con dos.
    const float SUGGEST_HARD   = 1.32f;
    const float SUGGEST_MEDIUM = 1.55f;
    const float SUGGEST_EASY   = 1.95f;

    public struct Report
    {
        public bool valid;          // false si el grid tiene un color que el enum no conoce
        public string error;

        public int bubbles;
        public int colors;          // colores distintos realmente presentes en el grid
        public int clusters;        // grupos de color conectados
        public int lone;            // grupos de una sola burbuja — cuestan dos disparos

        public int  minShots;       // disparos jugando perfecto
        public int  realisticShots; // lo mismo, más lo que se pierde por azar y por fallar
        public int  popped;         // burbujas reventadas por match en ese recorrido
        public int  popScore;       // lo que valieron esas burbujas: depende de la racha de cada disparo
        public int  dropped;        // burbujas que cayeron por quedar sueltas
        public bool rescue;         // la simulación paró al liberar la criatura, no al vaciar

        // Qué fracción del nivel se lleva el MEJOR primer disparo. Es la medida de lo frágil que
        // es el layout: con 0.6, más de la mitad del grid se cae de una y el nivel se termina en
        // tres jugadas por más disparos que tenga asignados.
        public float collapse;
    }

    public static Report Analyze(LevelData level)
    {
        var report = new Report { valid = true };

        var cells = new Dictionary<Vector2Int, BubbleColor>();
        foreach (var bubble in level?.bubbles ?? new List<BubbleEntry>())
        {
            if (!TryParse(bubble.color, out var color))
                return new Report { valid = false, error = $"Color desconocido: '{bubble.color}'" };

            cells[new Vector2Int(bubble.col, bubble.row)] = color;
        }

        report.bubbles = cells.Count;
        report.colors  = cells.Values.Distinct().Count();
        if (cells.Count == 0) return report;

        var groups = Clusters(cells);
        report.clusters = groups.Count;
        report.lone     = groups.Count(g => g.Count == 1);

        // En un nivel de rescate la partida termina al liberar la criatura, no al vaciar el grid,
        // así que la simulación para ahí.
        Vector2Int? target = null;
        var position = level.objective?.type == "rescue" ? level.objective.creature_position : null;
        if (position != null && position.Count >= 2) target = new Vector2Int(position[1], position[0]);

        report.rescue = target.HasValue;

        Simulate(cells, target, out report.minShots, out report.popped, out report.dropped, out report.popScore);
        report.realisticShots = Realistic(report.minShots, report.colors);
        report.collapse       = Collapse(cells);
        return report;
    }

    // Cuánto se lleva el disparo más destructivo que hay disponible al empezar, como fracción del
    // nivel. No mide dificultad: mide si el layout se sostiene.
    //
    // Lo que lo dispara casi siempre es el techo. La fila 0 es lo único que sujeta al resto, así
    // que una franja larga de un mismo color ahí arriba es una cuerda cortable: se revienta y
    // todo lo que colgaba se desprende junto. Un nivel puede tener 14 disparos asignados y
    // terminarse en 3 por esto.
    static float Collapse(Dictionary<Vector2Int, BubbleColor> cells)
    {
        if (cells.Count == 0) return 0f;

        int worst = 0;

        foreach (var group in Clusters(cells))
        {
            if (group.Count < 2) continue; // un grupo de uno no explota con un solo disparo

            var trial = new Dictionary<Vector2Int, BubbleColor>(cells);
            foreach (var cell in group) trial.Remove(cell);

            worst = Mathf.Max(worst, group.Count + Falling(trial).Count);
        }

        return worst / (float)cells.Count;
    }

    // El simulador juega perfecto, pero nadie juega así — y no solo por puntería.
    //
    // El cañón entrega colores al azar entre los que quedan en el grid. Cuantos más colores tenga
    // el nivel, más seguido toca uno que en ese momento no sirve para nada: hay que soltarlo en
    // cualquier lado, y esa burbuja se SUMA al grid, que es doblemente caro. Con dos colores casi
    // siempre hay dónde ponerla; con cinco, buena parte de los disparos son de descarte.
    //
    // Encima va un margen fijo por los disparos que simplemente se fallan.
    const float WASTE_PER_EXTRA_COLOR = 0.13f;
    const float MISS_ALLOWANCE        = 1.15f;

    public static int Realistic(int minShots, int colors)
    {
        float waste = 1f + Mathf.Max(0, colors - 2) * WASTE_PER_EXTRA_COLOR;
        return Mathf.CeilToInt(minShots * waste * MISS_ALLOWANCE);
    }

    // Recorrido codicioso: en cada turno elige el grupo con mejor relación burbujas quitadas por
    // disparo. Codicioso y no exhaustivo — el óptimo real exigiría explorar todo el árbol de
    // jugadas, que es exponencial y no vale la pena para una sugerencia.
    static void Simulate(Dictionary<Vector2Int, BubbleColor> start, Vector2Int? target,
                         out int shots, out int popped, out int dropped, out int popScore)
    {
        var cells = new Dictionary<Vector2Int, BubbleColor>(start);
        shots = popped = dropped = popScore = 0;

        // Jugando perfecto la racha sube en cada disparo, salvo cuando hay que gastar uno
        // pegando una burbuja que todavía no llega a tres: ese no matchea y la corta.
        int streak = 0;

        // Cada vuelta quita al menos una burbuja, así que el tope solo cubre un caso imprevisto.
        int guard = cells.Count * 2 + 16;

        while (cells.Count > 0 && guard-- > 0)
        {
            List<Vector2Int> bestGroup = null, bestFalling = null;
            int   bestCost = 1;
            float bestGain = -1f;

            foreach (var group in Clusters(cells))
            {
                // Un grupo de 2 explota con un disparo (2 + el disparo = 3). Uno solo necesita
                // dos: el primero no llega a tres y se queda pegado.
                int cost = group.Count >= 2 ? 1 : 2;

                var trial = new Dictionary<Vector2Int, BubbleColor>(cells);
                foreach (var cell in group) trial.Remove(cell);

                var falling = Falling(trial);
                float gain  = (group.Count + falling.Count) / (float)cost;

                // En un rescate lo que importa es llegar a la criatura, no vaciar el grid: una
                // jugada que la libera gana siempre. Es una heurística burda — no busca el camino
                // más corto hacia ella, solo aprovecha el momento en que aparece.
                if (target.HasValue && (group.Contains(target.Value) || falling.Contains(target.Value)))
                    gain = float.MaxValue;

                if (gain <= bestGain) continue;

                bestGain    = gain;
                bestGroup   = group;
                bestFalling = falling;
                bestCost    = cost;
            }

            if (bestGroup == null) break;

            foreach (var cell in bestGroup)   cells.Remove(cell);
            foreach (var cell in bestFalling) cells.Remove(cell);

            if (bestCost > 1) streak = 0;
            streak++;

            shots    += bestCost;
            popped   += bestGroup.Count;
            dropped  += bestFalling.Count;
            popScore += ScoreRules.PopScore(bestGroup.Count, streak);

            if (target.HasValue && !cells.ContainsKey(target.Value)) return;
        }
    }

    // Todos los grupos de color del grid. Un mismo rainbow puede pertenecer a varios grupos según
    // desde dónde se mire; acá cada burbuja se cuenta una sola vez, lo que alcanza para elegir
    // jugada pero hace que 'clusters' sea una aproximación cuando hay rainbow en el grid.
    static List<List<Vector2Int>> Clusters(Dictionary<Vector2Int, BubbleColor> cells)
    {
        var seen   = new HashSet<Vector2Int>();
        var result = new List<List<Vector2Int>>();

        foreach (var seed in cells.Keys)
        {
            if (seen.Contains(seed)) continue;

            var group = Cluster(cells, seed);
            foreach (var cell in group) seen.Add(cell);
            result.Add(group);
        }

        return result;
    }

    // Igual que GridController.FindConnectedSameColor: la comparación es siempre contra el color
    // de la SEMILLA, no entre vecinos. La diferencia importa con rainbow — rojo→rainbow→azul no
    // forma un solo grupo, porque azul se sigue comparando contra rojo.
    static List<Vector2Int> Cluster(Dictionary<Vector2Int, BubbleColor> cells, Vector2Int start)
    {
        var seedColor = cells[start];
        var visited   = new HashSet<Vector2Int> { start };
        var queue     = new Queue<Vector2Int>();
        var result    = new List<Vector2Int>();

        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            result.Add(cell);

            foreach (var neighbor in HexGridMath.GetNeighbors(cell))
            {
                if (visited.Contains(neighbor) || !cells.TryGetValue(neighbor, out var color)) continue;
                if (!seedColor.LinksWith(color)) continue;

                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return result;
    }

    // Lo que ya no llega al techo. Misma BFS que GridController.FindUnreachableFromCeiling.
    static List<Vector2Int> Falling(Dictionary<Vector2Int, BubbleColor> cells)
    {
        var reachable = new HashSet<Vector2Int>();
        var queue     = new Queue<Vector2Int>();

        foreach (var cell in cells.Keys)
        {
            if (cell.y != 0) continue;
            reachable.Add(cell);
            queue.Enqueue(cell);
        }

        while (queue.Count > 0)
        {
            foreach (var neighbor in HexGridMath.GetNeighbors(queue.Dequeue()))
            {
                if (!cells.ContainsKey(neighbor) || !reachable.Add(neighbor)) continue;
                queue.Enqueue(neighbor);
            }
        }

        return cells.Keys.Where(cell => !reachable.Contains(cell)).ToList();
    }

    // ---------------------------------------------------------------- lecturas baratas

    // Se mide contra los disparos realistas, no contra el óptimo: lo que define la dificultad es
    // cuánto aire le queda a un jugador de verdad, no a uno que no falla nunca.
    public static Difficulty Rate(int realisticShots, int maxShots)
    {
        if (realisticShots <= 0 || maxShots < realisticShots) return Difficulty.Imposible;

        float margin = maxShots / (float)realisticShots;

        if (margin < MARGIN_HARD)   return Difficulty.Dificil;
        if (margin < MARGIN_MEDIUM) return Difficulty.Medio;
        if (margin < MARGIN_EASY)   return Difficulty.Facil;
        return Difficulty.Trivial;
    }

    public static int Suggest(int realisticShots, Difficulty target)
    {
        float margin = target switch
        {
            Difficulty.Dificil => SUGGEST_HARD,
            Difficulty.Medio   => SUGGEST_MEDIUM,
            _                  => SUGGEST_EASY,
        };
        return Mathf.Max(realisticShots + 1, Mathf.CeilToInt(realisticShots * margin));
    }

    // Ritmo de cada tanda de diez niveles: tres fáciles para entrar, dos medios, dos fáciles que
    // aflojan, dos medios y el décimo difícil como cierre. El jugador nunca encadena dos subidas
    // seguidas, y cada decena termina en un pico reconocible.
    static readonly Difficulty[] TenLevelBeat =
    {
        Difficulty.Facil, Difficulty.Facil, Difficulty.Facil,
        Difficulty.Medio, Difficulty.Medio,
        Difficulty.Facil, Difficulty.Facil,
        Difficulty.Medio, Difficulty.Medio,
        Difficulty.Dificil,
    };

    public static Difficulty Expected(int positionInChapter) =>
        TenLevelBeat[Mathf.Max(0, positionInChapter - 1) % TenLevelBeat.Length];

    // Qué tan adentro del capítulo está el nivel, de 0 a 1. El ritmo de diez dice si toca fácil o
    // difícil; esto dice con cuánta intensidad — un "fácil" del nivel 55 es más grande y tiene más
    // colores que uno del nivel 5.
    public static float Progress(int positionInChapter, int chapterSize) =>
        chapterSize <= 1 ? 0f : Mathf.Clamp01((positionInChapter - 1) / (float)(chapterSize - 1));

    // Puntaje del recorrido óptimo, sin los bonus de combo (dependen de encadenar disparos, que
    // esta simulación no modela). Sirve como piso para calibrar star_thresholds a mano.
    // Umbrales de estrellas: el puntaje del recorrido óptimo es el 100% de la barra, y cada
    // estrella se gana donde está dibujada sobre ella. Nada más.
    //
    // No incluye los bonus de combo, que dependen de encadenar disparos y no se pueden predecir
    // desde el layout. Eso empuja el puntaje real hacia arriba, así que los umbrales quedan algo
    // conservadores — deseable, porque errar hacia "se ganó una estrella de más" es mejor que
    // hacia "es imposible sacar tres".
    public static int[] SuggestStars(Report report)
    {
        int target = RealisticScore(report);

        return new[]
        {
            Round(target * BAR_STAR_1),
            Round(target * BAR_STAR_2),
            Round(target * BAR_STAR_3),
        };
    }

    // Dónde está dibujada cada estrella sobre la barra de progreso, como fracción del ancho.
    //
    // No es un detalle visual: los marcos están colocados a mano en la escena en esos
    // porcentajes, y ProgressScoreView reconstruye el máximo de la barra como star3 / 0.90. Si
    // los umbrales no salen de acá, la barra cruza la estrella sin encenderla.
    //
    // Cambiar estos números obliga a mover los marcos en la escena, y al revés.
    public const float BAR_STAR_1 = 0.40f;
    public const float BAR_STAR_2 = 0.65f;
    public const float BAR_STAR_3 = 0.90f;

    // A decenas. Antes era a millares, que con puntajes de siete dígitos no se notaba; con la
    // escala nueva un nivel entero puede valer unos pocos miles y redondear así lo destruiría.
    static int Round(float value) => Mathf.Max(1, Mathf.RoundToInt(value / 10f)) * 10;

    // El puntaje que se espera de una partida BUENA, no de una perfecta. Es la base de los
    // umbrales de estrellas, así que la diferencia importa más que en cualquier otro número de
    // acá.
    //
    // report.popScore viene del recorrido perfecto: el simulador no falla un disparo, así que la
    // racha sube 1, 2, 3… hasta el final y las últimas burbujas valen diez o veinte veces las
    // primeras. Un jugador real falla, y cada fallo corta la racha y la devuelve a 10. Pedir el
    // 90% del recorrido perfecto era pedir una partida sin un solo error.
    //
    // Acá se reparte el recorrido en tantos tramos como fallos se esperan (realisticShots menos
    // los mínimos) y se cobra la racha media de un tramo, no la de la partida entera.
    //
    // El bonus por disparos sobrantes NO entra. El umbral es lo que vale el TABLERO: todas sus
    // burbujas, reventadas o caídas. El bonus es un extra que el jugador suma encima y que le
    // ayuda a cruzar esa meta — no la meta misma. Por eso tampoco depende de max_shots.
    public static int RealisticScore(Report report)
    {
        int misses = Mathf.Max(0, report.realisticShots - report.minShots);
        int runs   = misses + 1;                       // en cuántos tramos queda partida la racha
        float runLength   = report.minShots / (float)runs;
        float averageStreak = (runLength + 1f) / 2f;   // media de 1..runLength

        int popScore = Mathf.RoundToInt(report.popped * ScoreRules.POINTS_PER_POP_BASE * averageStreak);

        return popScore + ScoreRules.DropScore(report.dropped);
    }

    static bool TryParse(string value, out BubbleColor color)
    {
        try   { color = BubbleColorExtensions.Parse(value); return true; }
        catch { color = default; return false; }
    }
}
