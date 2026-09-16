using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Difficulty = LevelDifficultyEstimator.Difficulty;

// Arma el layout de un nivel como lo haría alguien a mano, no tirando burbujas al azar.
//
// Son tres pasos separados a propósito:
//
//   1. SILUETA — qué celdas se ocupan. Sale de un repertorio de formas reconocibles (bloque,
//      cúpula, copa, rombo, columnas, onda), no de ruido. Después se espeja, que es lo que más
//      hace que un nivel "se vea diseñado", y se le aplican mordidas y huecos para que no quede
//      con cara de plantilla.
//
//   2. COLOR — manchas, no celdas sueltas. Se siembran semillas y crecen por vecindad hasta
//      cubrir la silueta, así cada color queda en grupos conectados de tamaño parecido. El tamaño
//      de mancha es la palanca de dificultad más fuerte que hay: manchas grandes se revientan de
//      un disparo y arrastran cascadas; manchas chicas obligan a gastar disparos de a uno.
//
//   3. SELECCIÓN — se generan muchos candidatos y se MIDEN con LevelDifficultyEstimator, que es
//      el mismo simulador que evalúa los niveles hechos a mano. Gana el que mide más parecido a
//      la dificultad pedida. Acá está lo "pensante": no se confía en que los parámetros den el
//      resultado buscado, se verifica jugando cada candidato.
public static class LevelGenerator
{
    const int ATTEMPTS = 60;

    // Burbujas quitadas por disparo que debería tener cada dificultad. Es la medida con la que se
    // elige el candidato: un nivel fácil se limpia en pocas jugadas grandes, uno difícil exige
    // muchas jugadas chicas. Salen de medir los niveles del capítulo 1 hechos a mano.
    const float EFFICIENCY_EASY   = 6.5f;
    const float EFFICIENCY_MEDIUM = 5.0f;
    const float EFFICIENCY_HARD   = 3.5f;

    // Un nivel tiene que llenar la pantalla para leerse como nivel. Por debajo de 8 filas se ve a
    // media hecho por más que los números den bien. El tope son 12 porque más abajo el grid nace
    // demasiado cerca del cañón.
    const int MIN_ROWS = 8;
    const int MAX_ROWS = 25;

    // Celdas por fila en promedio (alternan 10 y 9), para estimar cuántas burbujas debería tener
    // un layout de N filas antes de generarlo.
    const float CELLS_PER_ROW = 9.5f;

    struct Recipe
    {
        public int   rows;
        public int   colors;
        public float blobSize;   // burbujas por mancha de color
        public float density;    // cuánto de la silueta sobrevive a las mordidas
    }

    // Escribe el layout, los colores, los disparos y los umbrales sobre el nivel. NO toca id,
    // name, chapter ni el tipo de objetivo: eso es trabajo de diseño que ya está hecho.
    // `exactColors` significa que la paleta no es una sugerencia sino la decisión ya tomada: se
    // usan todos esos colores y ninguno más, aunque la dificultad pidiera otra cantidad. Es lo que
    // pasa cuando el nivel ya tiene marcados sus available_colors.
    public static bool Generate(LevelData level, Difficulty target, float progress, string[] palette,
                                int seed, bool exactColors = false)
    {
        if (level == null || palette == null || palette.Length < 2) return false;

        var rng    = new System.Random(seed);
        var recipe = RecipeFor(target, progress, palette.Length);

        if (exactColors) recipe.colors = palette.Length;

        List<BubbleEntry> best       = null;
        string[]          bestColors = null;
        float             bestScore  = float.MinValue;
        LevelDifficultyEstimator.Report bestReport = default;

        for (int attempt = 0; attempt < ATTEMPTS; attempt++)
        {
            var attemptRecipe = Jitter(recipe, rng);

            var shape = Silhouette(attemptRecipe, rng);

            // Una silueta que no llegó a la profundidad pedida no se rescata: el layout entero se
            // vuelve a tirar. Salía de acá el nivel de dos filas.
            if (shape.Count == 0 || shape.Max(cell => cell.y) + 1 < attemptRecipe.rows - 1) continue;

            // La mayoría de las veces el color también se espeja. Es lo que termina de hacer que
            // el nivel se lea como una figura y no como un relleno: la silueta simétrica sola no
            // alcanza si encima las manchas van cada una por su lado.
            var painted = Paint(shape, palette, attemptRecipe.colors, attemptRecipe.blobSize, rng);
            if (rng.NextDouble() < 0.75) MirrorColors(painted);

            // Después del espejo, no antes: reflejar puede dejar sin representación a un color que
            // solo vivía de un lado.
            Consolidate(painted);
            var bubbles = painted.Select(kv => new BubbleEntry { row = kv.Key.y, col = kv.Key.x, color = kv.Value })
                                 .ToList();

            var colors = painted.Values.Distinct().OrderBy(c => System.Array.IndexOf(palette, c)).ToArray();
            var probe  = new LevelData
            {
                bubbles   = bubbles,
                objective = level.objective,
                max_shots = level.max_shots,
            };

            var report = LevelDifficultyEstimator.Analyze(probe);
            if (!report.valid || report.minShots <= 0) continue;

            float score = Score(report, target, colors.Length, attemptRecipe);
            if (score <= bestScore) continue;

            bestScore  = score;
            best       = bubbles;
            bestColors = colors;
            bestReport = report;
        }

        if (best == null) return false;

        best.Sort((a, b) => a.row != b.row ? a.row.CompareTo(b.row) : a.col.CompareTo(b.col));

        level.bubbles          = best;
        level.available_colors = bestColors.ToList();
        level.min_shots_to_clear = bestReport.minShots;
        level.max_shots          = LevelDifficultyEstimator.Suggest(bestReport.realisticShots, target);
        level.star_thresholds    = LevelDifficultyEstimator.SuggestStars(bestReport, level.max_shots).ToList();

        if (level.objective?.type == "rescue") PlaceCreature(level, best);

        return true;
    }

    // Qué tan bien mide un candidato contra lo que se pidió. Negativo siempre: 0 sería el nivel
    // perfecto y lo que se compara es quién se aleja menos.
    static float Score(LevelDifficultyEstimator.Report report, Difficulty target, int colors, Recipe recipe)
    {
        float efficiency = report.bubbles / (float)report.minShots;
        float wanted = target switch
        {
            Difficulty.Facil   => EFFICIENCY_EASY,
            Difficulty.Dificil => EFFICIENCY_HARD,
            _                  => EFFICIENCY_MEDIUM,
        };

        float score = -Mathf.Abs(efficiency - wanted);

        // El tamaño pesa tanto como la eficiencia. Sin esto gana cualquier layout diminuto: trece
        // burbujas limpiadas en dos disparos dan una eficiencia impecable y un nivel que no lo es.
        float expected = recipe.rows * CELLS_PER_ROW * recipe.density;
        score -= Mathf.Abs(report.bubbles - expected) / expected * 5f;

        // Que falte un color pedido descalifica el candidato casi por sí solo: si el nivel dice
        // que juega con cuatro, tiene que haber cuatro en el grid.
        score -= Mathf.Abs(colors - recipe.colors) * 3f;

        // Grupos de una sola burbuja: cuestan dos disparos y se sienten a relleno. En un nivel
        // difícil son parte del desafío; en uno fácil, un error.
        float lonePenalty = target == Difficulty.Facil ? 0.30f : 0.08f;
        score -= report.lone * lonePenalty;

        return score;
    }

    // ---------------------------------------------------------------- receta

    // La dificultad elige el rango; el avance dentro del capítulo elige dónde caer en él. Por eso
    // un nivel fácil del final de un capítulo se parece más a uno medio del principio.
    static Recipe RecipeFor(Difficulty target, float progress, int paletteSize)
    {
        var recipe = target switch
        {
            Difficulty.Facil => new Recipe
            {
                rows     = Mathf.RoundToInt(Mathf.Lerp(9f, 13f, progress)),
                colors   = Mathf.RoundToInt(Mathf.Lerp(2f, 3f, progress)),
                blobSize = Mathf.Lerp(12f, 9f, progress),
                density  = 1f,
            },
            Difficulty.Dificil => new Recipe
            {
                rows     = Mathf.RoundToInt(Mathf.Lerp(17f, 25f, progress)),
                colors   = Mathf.RoundToInt(Mathf.Lerp(4f, 5f, progress)),
                blobSize = Mathf.Lerp(5f, 4f, progress),
                density  = 0.92f,
            },
            _ => new Recipe
            {
                rows     = Mathf.RoundToInt(Mathf.Lerp(13f, 18f, progress)),
                colors   = Mathf.RoundToInt(Mathf.Lerp(3f, 4f, progress)),
                blobSize = Mathf.Lerp(8f, 6f, progress),
                density  = 0.97f,
            },
        };

        recipe.rows   = Mathf.Clamp(recipe.rows, MIN_ROWS, MAX_ROWS);
        recipe.colors = Mathf.Clamp(recipe.colors, 2, paletteSize);
        return recipe;
    }

    // Variación entre candidatos. Sin esto los 60 intentos serían la misma receta y solo cambiaría
    // el azar de la forma.
    static Recipe Jitter(Recipe recipe, System.Random rng)
    {
        recipe.rows     = Mathf.Clamp(recipe.rows + rng.Next(-1, 2), MIN_ROWS, MAX_ROWS);
        recipe.blobSize = Mathf.Max(2f, recipe.blobSize + (float)(rng.NextDouble() - 0.5));
        return recipe;   // los colores no se tocan: pueden venir fijados desde afuera
    }

    // ---------------------------------------------------------------- silueta

    static HashSet<Vector2Int> Silhouette(Recipe recipe, System.Random rng)
    {
        // Siluetas que se reconocen de un vistazo. El perfil dice cuánto se mete el borde en cada
        // fila: 0 es fila entera, 1 es la más angosta.
        var cells = (rng.Next(7)) switch
        {
            0 => Shape(recipe, t => 0f),                                    // bloque
            1 => Shape(recipe, t => t),                                     // cúpula: cierra abajo
            2 => Shape(recipe, t => 1f - t),                                // copa: abre abajo
            3 => Shape(recipe, t => Mathf.Abs(t - 0.5f) * 2f),              // rombo
            4 => Shape(recipe, t => 1f - Mathf.Abs(t - 0.5f) * 2f),         // reloj de arena
            5 => Shape(recipe, t => Mathf.Floor(t * 3f) / 3f),              // escalonada
            _ => Columns(recipe, rng),
        };

        if (recipe.density < 1f) Erode(cells, recipe.density, rng);

        // La cavidad va antes del espejo, así que también sale simétrica. Solo de vez en cuando:
        // es el detalle que evita la cara de plantilla, pero de a mucho deforma la figura.
        if (rng.NextDouble() < 0.25) Punch(cells, rng);

        cells = Mirror(cells);
        PruneFloating(cells);
        return cells;
    }

    // Familia de formas definidas por cuánto se mete el borde en cada fila. Un solo generador
    // cubre bloque, cúpula, copa, rombo y onda según qué perfil se le pase.
    //
    // El perfil trabaja en fracciones (recibe 0 en la fila de arriba y 1 en la de abajo, devuelve
    // 0 para fila entera y 1 para la más angosta) en vez de en columnas. Así la misma forma se ve
    // igual con 8 filas que con 12; con valores absolutos, una cúpula alta se cerraba a dos
    // celdas por la mitad y el resto del nivel quedaba en un hilo.
    static HashSet<Vector2Int> Shape(Recipe recipe, System.Func<float, float> profile)
    {
        var cells = new HashSet<Vector2Int>();

        for (int row = 0; row < recipe.rows; row++)
        {
            int cols     = HexGridMath.ColsInRow(row);
            int maxInset = Mathf.Max(0, cols / 2 - 2);   // deja al menos cuatro celdas por fila

            float t   = recipe.rows <= 1 ? 0f : row / (float)(recipe.rows - 1);
            int inset = Mathf.Clamp(Mathf.RoundToInt(profile(t) * maxInset), 0, maxInset);

            for (int col = inset; col < cols - inset; col++)
                cells.Add(new Vector2Int(col, row));
        }

        return cells;
    }

    // Columnas colgando de un techo lleno — la silueta de estalactitas. La fila 0 completa es lo
    // que las sostiene: sin ella caerían todas al primer disparo.
    static HashSet<Vector2Int> Columns(Recipe recipe, System.Random rng)
    {
        var cells = new HashSet<Vector2Int>();
        int cols  = HexGridMath.ColsInRow(0);

        for (int col = 0; col < cols; col++) cells.Add(new Vector2Int(col, 0));

        for (int col = 0; col < cols; col += 2)
        {
            // Las columnas bajan entre el 70% y el total de la altura pedida: desparejas, pero
            // ninguna se queda a mitad de camino dejando el nivel corto.
            int depth = rng.Next(Mathf.CeilToInt(recipe.rows * 0.7f), recipe.rows + 1);

            for (int row = 1; row < depth; row++)
                if (col < HexGridMath.ColsInRow(row)) cells.Add(new Vector2Int(col, row));
        }

        return cells;
    }

    // Mordidas en el contorno. Solo toca celdas del borde, así el interior queda macizo y la
    // silueta se ve gastada en vez de agujereada.
    static void Erode(HashSet<Vector2Int> cells, float density, System.Random rng)
    {
        var edge = cells.Where(cell => HexGridMath.GetNeighbors(cell).Any(n => !cells.Contains(n))).ToList();

        foreach (var cell in edge)
        {
            if (cell.y == 0) continue;                       // el techo sostiene todo
            if (rng.NextDouble() >= 1f - density) continue;
            cells.Remove(cell);
        }
    }

    // Una cavidad interior. Le da al nivel un lugar donde las burbujas quedan colgando, que es de
    // donde salen las cascadas grandes.
    static void Punch(HashSet<Vector2Int> cells, System.Random rng)
    {
        var inner = cells.Where(cell => cell.y > 1 && HexGridMath.GetNeighbors(cell).All(cells.Contains)).ToList();
        if (inner.Count == 0) return;

        var center = inner[rng.Next(inner.Count)];
        cells.Remove(center);

        foreach (var neighbor in HexGridMath.GetNeighbors(center))
            if (neighbor.y > 0 && rng.NextDouble() < 0.5) cells.Remove(neighbor);
    }

    // Se queda con la mitad izquierda y la refleja. La simetría es lo que más separa un nivel
    // compuesto de uno generado: el ojo la lee como intención.
    static HashSet<Vector2Int> Mirror(HashSet<Vector2Int> cells)
    {
        var result = new HashSet<Vector2Int>();

        foreach (var cell in cells)
        {
            int cols = HexGridMath.ColsInRow(cell.y);
            if (cell.x * 2 >= cols) continue;

            result.Add(cell);
            result.Add(new Vector2Int(cols - 1 - cell.x, cell.y));
        }

        return result;
    }

    // Lo que no llega al techo caería apenas empieza el nivel, así que no es parte del layout.
    static void PruneFloating(HashSet<Vector2Int> cells)
    {
        var reachable = new HashSet<Vector2Int>();
        var queue     = new Queue<Vector2Int>();

        foreach (var cell in cells)
            if (cell.y == 0) { reachable.Add(cell); queue.Enqueue(cell); }

        while (queue.Count > 0)
            foreach (var neighbor in HexGridMath.GetNeighbors(queue.Dequeue()))
                if (cells.Contains(neighbor) && reachable.Add(neighbor)) queue.Enqueue(neighbor);

        cells.RemoveWhere(cell => !reachable.Contains(cell));
    }

    // ---------------------------------------------------------------- color

    // Manchas que crecen desde semillas repartidas, todas al mismo ritmo. El resultado se parece a
    // un mapa de regiones: cada color ocupa zonas conectadas de tamaño parecido, en vez de quedar
    // salpicado celda por celda.
    static Dictionary<Vector2Int, string> Paint(HashSet<Vector2Int> cells, string[] palette,
                                                int colorCount, float blobSize, System.Random rng)
    {
        var colors = palette.Take(Mathf.Clamp(colorCount, 2, palette.Length)).ToArray();
        var result = new Dictionary<Vector2Int, string>();

        var order = cells.ToList();
        Shuffle(order, rng);

        int seedCount = Mathf.Clamp(Mathf.CeilToInt(cells.Count / Mathf.Max(2f, blobSize)),
                                    colors.Length, order.Count);

        var fronts = new List<(Queue<Vector2Int> queue, string color)>();
        for (int i = 0; i < seedCount; i++)
        {
            // Las primeras semillas recorren la paleta entera para que ningún color falte; de ahí
            // en adelante se reparten al azar y un mismo color puede tener varias manchas.
            string color = i < colors.Length ? colors[i] : colors[rng.Next(colors.Length)];

            result[order[i]] = color;
            fronts.Add((new Queue<Vector2Int>(new[] { order[i] }), color));
        }

        bool grew = true;
        while (result.Count < cells.Count && grew)
        {
            grew = false;

            foreach (var (queue, color) in fronts)
            {
                if (queue.Count == 0) continue;

                var cell = queue.Dequeue();

                // Una celda por turno, para que todas las manchas crezcan parejo y ninguna se
                // coma el grid antes de que las demás arranquen.
                foreach (var neighbor in HexGridMath.GetNeighbors(cell))
                {
                    if (!cells.Contains(neighbor) || result.ContainsKey(neighbor)) continue;

                    result[neighbor] = color;
                    queue.Enqueue(neighbor);
                    grew = true;
                    break;
                }

                // La celda vuelve a la cola mientras le quede por dónde seguir: si se descartara
                // al primer vecino pintado, la mancha perdería la mitad de su frente.
                if (HexGridMath.GetNeighbors(cell).Any(n => cells.Contains(n) && !result.ContainsKey(n)))
                    queue.Enqueue(cell);
            }
        }

        foreach (var cell in cells)
            if (!result.ContainsKey(cell)) result[cell] = colors[rng.Next(colors.Length)];

        return result;
    }

    // Copia el color de cada celda sobre su espejo. Las manchas quedan reflejadas y el nivel gana
    // un eje central visible, que es como se ven los niveles compuestos a mano.
    static void MirrorColors(Dictionary<Vector2Int, string> painted)
    {
        foreach (var cell in painted.Keys.ToList())
        {
            int cols = HexGridMath.ColsInRow(cell.y);
            if (cell.x * 2 >= cols) continue;

            var mirror = new Vector2Int(cols - 1 - cell.x, cell.y);
            if (painted.ContainsKey(mirror)) painted[mirror] = painted[cell];
        }
    }

    // Un color con una sola burbuja en todo el nivel no se puede matchear nunca: hay que gastar
    // dos disparos solo para sacarlo. Se absorbe en el color de al lado.
    static void Consolidate(Dictionary<Vector2Int, string> painted)
    {
        var counts = painted.Values.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());

        foreach (var orphan in counts.Where(kv => kv.Value < 2).Select(kv => kv.Key).ToList())
        {
            foreach (var cell in painted.Where(kv => kv.Value == orphan).Select(kv => kv.Key).ToList())
            {
                string replacement = HexGridMath.GetNeighbors(cell)
                    .Where(painted.ContainsKey)
                    .Select(n => painted[n])
                    .FirstOrDefault(c => c != orphan);

                if (replacement != null) painted[cell] = replacement;
            }
        }
    }

    // ---------------------------------------------------------------- rescate

    // La criatura va lo más hondo posible y hacia el centro: es el punto más caro de alcanzar, que
    // es de lo que trata el objetivo.
    static void PlaceCreature(LevelData level, List<BubbleEntry> bubbles)
    {
        int deepest = bubbles.Max(b => b.row);
        var row     = bubbles.Where(b => b.row == deepest).ToList();
        float middle = (HexGridMath.ColsInRow(deepest) - 1) * 0.5f;

        var chosen = row.OrderBy(b => Mathf.Abs(b.col - middle)).First();
        level.objective.creature_position = new List<int> { chosen.row, chosen.col };
    }

    static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
