using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Difficulty = LevelDifficultyEstimator.Difficulty;

// Arma el layout de un nivel como lo haría alguien a mano, no tirando burbujas al azar.
//
// Son tres pasos separados a propósito:
//
//   1. SILUETA — qué celdas se ocupan. Es UNA figura dibujada a mano (LevelShapeLibrary),
//      estirada por su cuerpo hasta la altura del nivel y a veces espejada. Una sola y no varias
//      apiladas: el nivel tiene que leerse como una pieza.
//
//   2. COLOR — manchas chicas, de tres o cuatro burbujas. Se siembran semillas y crecen por
//      vecindad hasta cubrir la silueta. El tamaño de mancha es la palanca más delicada del
//      generador: una mancha grande del mismo color es un combo ya servido, y basta con unas
//      pocas para que un nivel de 38 disparos se resuelva en 11.
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
    const float EFFICIENCY_EASY   = 5.0f;
    const float EFFICIENCY_MEDIUM = 4.0f;
    const float EFFICIENCY_HARD   = 3.0f;

    // Cuánto puede llevarse el mejor disparo del nivel. Un quinto ya es una cascada vistosa; más
    // que eso y el nivel se termina en un puñado de jugadas por más disparos que tenga asignados.
    //
    // Va de la mano del tamaño de mancha: manchas grandes del mismo color son combos servidos, y
    // son la razón por la que un nivel de 38 disparos se resolvía en 11.
    const float COLLAPSE_LIMIT = 0.22f;

    // Un nivel tiene que llenar la pantalla para leerse como nivel. Por debajo de 8 filas se ve a
    // media hecho por más que los números den bien. El tope son 12 porque más abajo el grid nace
    // demasiado cerca del cañón.
    const int MIN_ROWS = 8;
    const int MAX_ROWS = 25;

    // Celdas por fila en promedio (alternan 10 y 9) y qué proporción de ellas ocupa una figura
    // típica del catálogo, contando sus bordes en diagonal. Juntas estiman cuántas burbujas
    // debería tener un layout de N filas antes de generarlo.
    const float CELLS_PER_ROW = 9.5f;
    const float SHAPE_FILL    = 0.85f;

    struct Recipe
    {
        public int   rows;
        public int   colors;
        public float blobSize;   // burbujas por mancha de color
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

            // Tres de cada cuatro niveles llevan un patrón de color — anillos, franjas, equis,
            // rombos. Es lo que hace que se lean como algo dibujado, y de paso mantiene los
            // grupos de un color acotados por construcción. El resto va con manchas orgánicas,
            // que rompen la regularidad cuando varios niveles seguidos usarían patrón.
            var painted = rng.NextDouble() < 0.75
                ? PaintPattern(shape, palette, attemptRecipe.colors, attemptRecipe.rows, rng)
                : Paint(shape, palette, attemptRecipe.colors, attemptRecipe.blobSize, rng);

            if (rng.NextDouble() < 0.5) MirrorColors(painted);

            // El techo se refuerza DESPUÉS del espejo, porque espejar puede volver a juntar dos
            // mitades del mismo color justo en el centro de la fila 0.
            ReinforceCeiling(painted, painted.Values.Distinct().ToArray(), rng);

            // Y consolidar al final: los dos pasos anteriores pueden dejar un color con una sola
            // burbuja en todo el nivel.
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
        float expected = recipe.rows * CELLS_PER_ROW * SHAPE_FILL;
        score -= Mathf.Abs(report.bubbles - expected) / expected * 5f;

        // Que falte un color pedido descalifica el candidato casi por sí solo: si el nivel dice
        // que juega con cuatro, tiene que haber cuatro en el grid.
        score -= Mathf.Abs(colors - recipe.colors) * 3f;

        // Grupos de una sola burbuja: cuestan dos disparos y se sienten a relleno. En un nivel
        // difícil son parte del desafío; en uno fácil, un error.
        float lonePenalty = target == Difficulty.Facil ? 0.30f : 0.08f;
        score -= report.lone * lonePenalty;

        // Fragilidad: cuánto se lleva el disparo más destructivo disponible al empezar. Un nivel
        // que pierde más de un tercio de una sola jugada se siente roto por más que la cuenta de
        // disparos dé bien — el jugador ve desmoronarse media pantalla sin haber hecho nada
        // especial. Se penaliza fuerte y creciendo, para que estos candidatos no ganen nunca
        // aunque su eficiencia sea la pedida.
        if (report.collapse > COLLAPSE_LIMIT)
            score -= (report.collapse - COLLAPSE_LIMIT) * 25f;

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
                rows     = Mathf.RoundToInt(Mathf.Lerp(9f, 12f, progress)),
                colors   = Mathf.RoundToInt(Mathf.Lerp(2f, 3f, progress)),
                blobSize = Mathf.Lerp(4.5f, 4f, progress),
            },
            Difficulty.Dificil => new Recipe
            {
                rows     = Mathf.RoundToInt(Mathf.Lerp(18f, 24f, progress)),
                colors   = Mathf.RoundToInt(Mathf.Lerp(4f, 5f, progress)),
                blobSize = Mathf.Lerp(3f, 2.5f, progress),
            },
            _ => new Recipe
            {
                rows     = Mathf.RoundToInt(Mathf.Lerp(13f, 17f, progress)),
                colors   = Mathf.RoundToInt(Mathf.Lerp(3f, 4f, progress)),
                blobSize = Mathf.Lerp(3.5f, 3f, progress),
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

    // UNA figura por nivel, estirada a la altura pedida.
    //
    // Una sola y no varias apiladas: un nivel tiene que leerse como una pieza. Apilando figuras
    // distintas cada tramo se ve sin relación con el de arriba, y el resultado parece amontonado
    // en vez de compuesto. La variedad sale del catálogo y del espejado, no de mezclar.
    static HashSet<Vector2Int> Silhouette(Recipe recipe, System.Random rng)
    {
        var shape = LevelShapeLibrary.All[rng.Next(LevelShapeLibrary.All.Length)];
        var rows  = LevelShapeLibrary.Expand(shape, recipe.rows);

        // Espejar duplica el repertorio sin dibujar nada. En las simétricas no cambia nada y no
        // molesta; en las que no lo son, se leen distinto.
        bool flip  = rng.NextDouble() < 0.5;
        var  cells = new HashSet<Vector2Int>();

        for (int row = 0; row < rows.Count; row++)
        {
            string line = rows[row];
            if (flip) line = new string(line.Reverse().ToArray());

            for (int col = 0; col < HexGridMath.ColsInRow(row); col++)
                if (Filled(line, col, HexGridMath.IsOddRow(row))) cells.Add(new Vector2Int(col, row));
        }

        // Nada de morder el contorno. Una celda quitada al azar no se lee como diseño sino como
        // una burbuja que falta, y en los niveles difíciles eso salpicaba el borde entero. La
        // figura se usa tal como está dibujada.
        PruneFloating(cells);
        return cells;
    }

    // Si la celda 'col' de esta fila cae dentro del dibujo.
    //
    // Las filas impares tienen nueve celdas y van corridas media burbuja, así que su celda 'col'
    // no cae sobre la columna 'col' del dibujo sino JUSTO ENTRE la 'col' y la 'col + 1'. Se mira
    // ese par y alcanza con que una esté llena.
    //
    // Recortar el dibujo a nueve columnas, que era lo que se hacía antes, borra la columna de la
    // derecha: una fila simétrica como "..######.." quedaba "..######." y la figura entera salía
    // corrida hacia un lado. Este emparejado sí conserva la simetría — si el dibujo cumple
    // line[i] == line[9-i], la fila impar cumple mark[c] == mark[8-c].
    static bool Filled(string line, int col, bool oddRow)
    {
        if (!oddRow) return col < line.Length && line[col] == '#';

        bool left  = col     < line.Length && line[col]     == '#';
        bool right = col + 1 < line.Length && line[col + 1] == '#';
        return left || right;
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

    // Pinta con un patrón del catálogo: cada celda cae en una banda y cada banda toma un color.
    //
    // Es la forma en que están hechos los niveles de los bubble shooters que se ven bien: una masa
    // densa con un dibujo de color encima. Y tiene una ventaja que no se ve: las bandas acotan los
    // grupos por construcción — un anillo o una franja tienen un ancho fijo, mientras que una
    // mancha que crece libre puede terminar con veinte burbujas del mismo color.
    //
    // Las bandas se reparten en la paleta con módulo, así que el mismo patrón se lee distinto con
    // dos colores que con cinco.
    static Dictionary<Vector2Int, string> PaintPattern(HashSet<Vector2Int> cells, string[] palette,
                                                       int colorCount, int rows, System.Random rng)
    {
        var colors  = palette.Take(Mathf.Clamp(colorCount, 2, palette.Length)).ToArray();
        var pattern = LevelPatternLibrary.All[rng.Next(LevelPatternLibrary.All.Length)];
        var field   = LevelPatternLibrary.FieldOf(cells, rows);

        // Corrimiento al azar: si no, el patrón siempre empieza por el mismo color de la paleta y
        // todos los niveles que lo usan se parecen entre sí.
        int shift = rng.Next(colors.Length);

        var result = new Dictionary<Vector2Int, string>(cells.Count);
        foreach (var cell in cells)
        {
            int band = pattern.band(cell, field);
            result[cell] = colors[((band + shift) % colors.Length + colors.Length) % colors.Length];
        }

        return result;
    }

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

    // Cuántas burbujas seguidas del mismo color se toleran en el techo.
    const int MAX_CEILING_RUN = 2;

    // Rompe las franjas largas de un mismo color en la fila 0.
    //
    // Es el arreglo más importante del generador, y no se nota mirando el nivel quieto. La fila 0
    // es lo ÚNICO que sujeta al resto: todo lo demás cuelga de ella. Si hay cinco burbujas
    // seguidas del mismo color arriba, un solo match las borra y la mitad del nivel se desprende
    // y cae junta. De ahí salían los niveles de 14 disparos que se terminaban en 3.
    //
    // Cortando las franjas, cada match del techo abre un hueco pero deja el resto colgando de las
    // burbujas de al lado, y el nivel se desarma de a poco — que es como se siente un nivel hecho
    // a mano.
    static void ReinforceCeiling(Dictionary<Vector2Int, string> painted, string[] colors, System.Random rng)
    {
        int cols = HexGridMath.ColsInRow(0);
        int run  = 1;

        for (int col = 1; col < cols; col++)
        {
            var current  = new Vector2Int(col, 0);
            var previous = new Vector2Int(col - 1, 0);

            if (!painted.TryGetValue(current, out var color) ||
                !painted.TryGetValue(previous, out var before))
            {
                run = 1;
                continue;
            }

            if (color != before) { run = 1; continue; }
            if (++run <= MAX_CEILING_RUN) continue;

            // Cualquier otro color del nivel corta la franja; se elige al azar para no dejar un
            // patrón regular visible.
            var others = colors.Where(c => c != color).ToArray();
            if (others.Length == 0) continue;

            painted[current] = others[rng.Next(others.Length)];
            run = 1;
        }
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
