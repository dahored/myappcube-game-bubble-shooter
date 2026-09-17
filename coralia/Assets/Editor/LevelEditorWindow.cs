using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor de niveles: abre los mismos JSON que carga el juego y los deja pintar a mano.
//
// Trabaja directo sobre Assets/Resources/Levels/**/NNN.json, sin formato intermedio ni paso de
// exportación: lo que se guarda acá es literalmente lo que LevelLoader lee en runtime.
//
// La geometría del grid sale de HexGridMath, no de una copia. Si el editor calculara los vecinos
// o el desplazamiento de filas por su cuenta, tarde o temprano se desincronizaría del juego y se
// diseñarían niveles que en la partida se comportan distinto.
public class LevelEditorWindow : EditorWindow
{
    const string LEVELS_FOLDER   = "Assets/Resources/Levels";
    const string GAMEPLAY_SCENE  = "Assets/Scenes/Game/Gameplay.unity";
    const string SELECTED_LEVEL  = "selected_level"; // misma clave que usa LevelMapController

    // Entrar en Play recarga el dominio y la ventana se reconstruye de cero, así que el nivel
    // abierto se guarda afuera. Sin esto, cada "Probar este nivel" devuelve la lista al principio.
    const string PREF_CURRENT = "Coralia.LevelEditor.CurrentPath";

    const float LIST_WIDTH  = 200f;
    const float PROPS_WIDTH = 300f;

    // Mismo orden que el enum BubbleColor. Si se agrega un color, va acá y también en
    // BubbleColor.Parse y en GridController.SpriteFor — el enum es la fuente de verdad.
    // Sin "rainbow": dejó de ser un color de burbuja y pasa a ser un booster. Sigue en el enum y
    // en Swatches para poder abrir niveles viejos que la tengan escrita.
    static readonly string[] Colors =
    {
        "red", "blue", "yellow", "green", "purple", "orange",
        "pink", "grey", "brown", "black", "red_wine", "mint_green", "dark_blue", "dark_grey",
    };

    // Aproximaciones del sprite real, solo para distinguirlos mientras se pinta.
    static readonly Dictionary<string, Color> Swatches = new()
    {
        ["red"]        = new Color(0.89f, 0.24f, 0.24f),
        ["blue"]       = new Color(0.27f, 0.55f, 0.92f),
        ["yellow"]     = new Color(0.97f, 0.80f, 0.20f),
        ["green"]      = new Color(0.36f, 0.75f, 0.35f),
        ["purple"]     = new Color(0.62f, 0.38f, 0.86f),
        ["orange"]     = new Color(0.95f, 0.55f, 0.20f),
        ["pink"]       = new Color(0.95f, 0.45f, 0.70f),
        ["grey"]       = new Color(0.62f, 0.64f, 0.67f),
        ["brown"]      = new Color(0.55f, 0.36f, 0.20f),
        ["black"]      = new Color(0.18f, 0.18f, 0.22f),
        ["red_wine"]   = new Color(0.50f, 0.13f, 0.24f),
        ["mint_green"] = new Color(0.55f, 0.88f, 0.72f),
        ["dark_blue"]  = new Color(0.16f, 0.27f, 0.58f),
        ["dark_grey"]  = new Color(0.36f, 0.39f, 0.44f),
        ["rainbow"]    = new Color(0.85f, 0.85f, 0.88f),
    };

    class Entry
    {
        public string    path;
        public LevelData data;
    }

    List<Entry> _levels = new();
    Entry       _current;
    bool        _dirty;

    string  _paint  = "red";
    string  _search = "";
    Vector2 _listScroll, _gridScroll, _propsScroll;

    // La simulación recorre el nivel entero, así que no puede correr en cada repintado: se
    // recalcula solo cuando el grid cambió de verdad.
    LevelDifficultyEstimator.Report _report;
    bool _reportStale = true;

    readonly HashSet<int> _collapsed = new();

    Texture2D _circle;

    [MenuItem("Coralia/Editor de niveles")]
    static void Open()
    {
        var window = GetWindow<LevelEditorWindow>("Niveles");
        window.minSize = new Vector2(920, 620);
    }

    void OnEnable()
    {
        _circle = BuildCircleTexture();
        Reload();
    }

    void OnDisable()
    {
        if (_circle) DestroyImmediate(_circle);
    }

    void Reload()
    {
        _levels = Directory.Exists(LEVELS_FOLDER)
            ? Directory.GetFiles(LEVELS_FOLDER, "*.json", SearchOption.AllDirectories)
                .Select(p => new Entry { path = p.Replace('\\', '/'), data = Read(p) })
                .Where(e => e.data != null)
                .OrderBy(e => e.data.chapter).ThenBy(e => e.data.id)
                .ToList()
            : new List<Entry>();

        string wanted = _current?.path ?? EditorPrefs.GetString(PREF_CURRENT, "");
        _current = _levels.FirstOrDefault(e => e.path == wanted) ?? _levels.FirstOrDefault();

        _dirty       = false;
        _reportStale = true;

        if (_current != null) EditorPrefs.SetString(PREF_CURRENT, _current.path);

        // Solo queda abierto el capítulo del nivel en el que se está trabajando. Con 180 niveles
        // en tres capítulos, la lista completa es una tira por la que hay que scrollear a ciegas.
        _collapsed.Clear();
        foreach (var chapter in _levels.Select(e => e.data.chapter).Distinct())
            if (_current == null || chapter != _current.data.chapter) _collapsed.Add(chapter);
    }

    void Select(Entry entry)
    {
        _current     = entry;
        _dirty       = false;
        _reportStale = true;

        EditorPrefs.SetString(PREF_CURRENT, entry.path);
        GUI.FocusControl(null);
    }

    static LevelData Read(string path)
    {
        try { return JsonUtility.FromJson<LevelData>(File.ReadAllText(path)); }
        catch { return null; }
    }

    void OnGUI()
    {
        DrawToolbar();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawLevelList();

            if (_current == null)
            {
                EditorGUILayout.HelpBox($"No se encontró ningún nivel en {LEVELS_FOLDER}.", MessageType.Info);
                return;
            }

            DrawGrid();
            DrawProperties();
        }
    }

    // ---------------------------------------------------------------- barra superior

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Recargar", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                if (ConfirmDiscard()) Reload();
            }

            GUILayout.Space(8);

            using (new EditorGUI.DisabledScope(_current == null || !_dirty))
                if (GUILayout.Button("Guardar", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    Save(_current);

            using (new EditorGUI.DisabledScope(_current == null))
            {
                if (GUILayout.Button("Duplicar", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    Duplicate();

                GUILayout.Space(8);

                if (GUILayout.Button("Generar", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    GenerateCurrent();

                if (GUILayout.Button("Generar capítulo…", EditorStyles.toolbarButton, GUILayout.Width(130)))
                    GenerateChapter();

                GUILayout.Space(8);

                // Probar sin navegar el mapa: cada ajuste de un nivel implicaría entrar al juego y
                // scrollear hasta encontrarlo, y eso solo ya duplica el tiempo de iteración.
                if (GUILayout.Button("▶ Probar este nivel", EditorStyles.toolbarButton, GUILayout.Width(150)))
                    PlayCurrent();
            }

            GUILayout.FlexibleSpace();

            if (_dirty) EditorGUILayout.LabelField("· sin guardar", EditorStyles.miniLabel, GUILayout.Width(90));
        }
    }

    // ---------------------------------------------------------------- lista de niveles

    void DrawLevelList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(LIST_WIDTH)))
        {
            _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);

            using var scroll = new EditorGUILayout.ScrollViewScope(_listScroll, EditorStyles.helpBox);
            _listScroll = scroll.scrollPosition;

            bool searching = !string.IsNullOrEmpty(_search);
            string needle  = _search.ToLower();

            int chapter = -1;
            foreach (var entry in _levels)
            {
                if (searching && !$"{entry.data.id} {entry.data.name}".ToLower().Contains(needle)) continue;

                if (entry.data.chapter != chapter)
                {
                    chapter = entry.data.chapter;

                    int total = _levels.Count(e => e.data.chapter == chapter);
                    bool open = !_collapsed.Contains(chapter);
                    bool now  = EditorGUILayout.Foldout(open, $"Capítulo {chapter}  ({total})", true);

                    if (now != open)
                    {
                        if (now) _collapsed.Remove(chapter);
                        else     _collapsed.Add(chapter);
                    }
                }

                // Buscar atraviesa los capítulos cerrados: si se escribió un nombre, lo que
                // importa es encontrarlo, no dónde estaba guardado.
                if (!searching && _collapsed.Contains(entry.data.chapter)) continue;

                var style = entry == _current ? EditorStyles.miniButtonMid : EditorStyles.label;

                if (GUILayout.Button($"{entry.data.id:000}  {entry.data.name}", style) && ConfirmDiscard())
                    Select(entry);
            }
        }
    }

    // ---------------------------------------------------------------- el grid

    void DrawGrid()
    {
        var level = _current.data;
        level.bubbles ??= new List<BubbleEntry>();

        using var scroll = new EditorGUILayout.ScrollViewScope(_gridScroll);
        _gridScroll = scroll.scrollPosition;

        DrawPalette();
        GUILayout.Space(6);

        // Se muestran un par de filas de más que las usadas, para poder extender el nivel hacia
        // abajo sin ningún control aparte.
        int usedRows    = level.bubbles.Count > 0 ? level.bubbles.Max(b => b.row) + 1 : 0;
        int visibleRows = Mathf.Clamp(usedRows + 3, 12, 30);

        // Todo el dibujo sale de HexGridMath a escala reducida: el editor no recalcula nada de la
        // geometría, solo la achica para que quepa en la ventana. Así no hay forma de que se
        // separe de lo que el jugador ve en la partida.
        const float cell  = 30f;
        const float scale = cell / HexGridMath.BubbleDiameter;

        float rowHeight = HexGridMath.RowHeight * scale;
        float width     = HexGridMath.DesignWidth * scale;

        var area = GUILayoutUtility.GetRect(width, visibleRows * rowHeight + cell * 0.5f,
                                            GUILayout.ExpandWidth(false));

        EditorGUI.DrawRect(area, new Color(0.16f, 0.20f, 0.26f));

        var lookup = new Dictionary<Vector2Int, BubbleEntry>();
        foreach (var b in level.bubbles) lookup[new Vector2Int(b.col, b.row)] = b;

        for (int row = 0; row < visibleRows; row++)
        {
            for (int col = 0; col < HexGridMath.ColsInRow(row); col++)
            {
                var rect = CellRect(area, new Vector2Int(col, row), scale, cell);
                var key  = new Vector2Int(col, row);

                bool filled = lookup.TryGetValue(key, out var bubble);

                GUI.color = filled ? SwatchFor(bubble.color) : new Color(1f, 1f, 1f, 0.12f);
                GUI.DrawTexture(rect, _circle);
                GUI.color = Color.white;

                // La criatura de un nivel de rescate se marca sobre su burbuja.
                if (filled && IsCreatureCell(level, row, col))
                {
                    var label = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
                    label.normal.textColor = Color.black;
                    GUI.Label(rect, "★", label);
                }

                HandleCellClick(rect, level, row, col, lookup);
            }
        }
    }

    // La posición exacta que tendrá la burbuja en la partida, achicada. CellToLocalPos devuelve el
    // CENTRO de la celda, en coordenadas centradas en X y con Y negativo hacia abajo; acá se pasa
    // a la esquina superior izquierda del rectángulo de GUI.
    static Rect CellRect(Rect area, Vector2Int cell, float scale, float size)
    {
        var pos = HexGridMath.CellToLocalPos(cell);

        float x = area.x + (pos.x + HexGridMath.DesignWidth * 0.5f) * scale - size * 0.5f;
        float y = area.y + (-pos.y) * scale - size * 0.5f;

        return new Rect(x, y, size, size);
    }

    void HandleCellClick(Rect rect, LevelData level, int row, int col,
                         Dictionary<Vector2Int, BubbleEntry> lookup)
    {
        var e = Event.current;
        if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
        if (!rect.Contains(e.mousePosition)) return;

        var key    = new Vector2Int(col, row);
        bool erase = e.button == 1 || e.alt;

        if (erase)
        {
            if (lookup.TryGetValue(key, out var existing)) level.bubbles.Remove(existing);
        }
        else if (lookup.TryGetValue(key, out var existing))
        {
            existing.color = _paint;
        }
        else
        {
            level.bubbles.Add(new BubbleEntry { row = row, col = col, color = _paint });
        }

        _dirty       = true;
        _reportStale = true;
        e.Use();
        Repaint();
    }

    void DrawPalette()
    {
        const float swatch = 22f;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Pincel", GUILayout.Width(42));

            foreach (var color in Colors)
            {
                var rect = GUILayoutUtility.GetRect(swatch, swatch, GUILayout.Width(swatch));

                if (color == _paint) EditorGUI.DrawRect(rect, Color.white);

                GUI.color = SwatchFor(color);
                GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), _circle);
                GUI.color = Color.white;

                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    _paint = color;
                    Event.current.Use();
                    Repaint();
                }
            }

            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.LabelField($"{_paint} · clic pinta · clic derecho borra", EditorStyles.miniLabel);
    }

    // ---------------------------------------------------------------- propiedades

    void DrawProperties()
    {
        var level = _current.data;

        // El ancho va en el contenedor, no en el scroll: pasándoselo al ScrollViewScope, los
        // campos de adentro se dibujan más anchos que el área y quedan cortados contra el borde.
        using var column = new EditorGUILayout.VerticalScope(GUILayout.Width(PROPS_WIDTH));
        using var scroll = new EditorGUILayout.ScrollViewScope(_propsScroll);
        _propsScroll = scroll.scrollPosition;

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Nivel", EditorStyles.boldLabel);
        level.name    = EditorGUILayout.TextField("Nombre", level.name);
        level.id      = EditorGUILayout.IntField("Id", level.id);
        level.chapter = EditorGUILayout.IntField("Capítulo", level.chapter);

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Objetivo", EditorStyles.boldLabel);
        level.objective ??= new ObjectiveData { type = "clear_all" };

        int typeIndex = level.objective.type == "rescue" ? 1 : 0;
        typeIndex = EditorGUILayout.Popup("Tipo", typeIndex, new[] { "clear_all", "rescue" });
        level.objective.type = typeIndex == 1 ? "rescue" : "clear_all";

        if (level.objective.type == "rescue")
        {
            level.objective.creature_id = EditorGUILayout.TextField("Criatura", level.objective.creature_id);

            level.objective.creature_position ??= new List<int> { 0, 0 };
            while (level.objective.creature_position.Count < 2) level.objective.creature_position.Add(0);

            level.objective.creature_position[0] = EditorGUILayout.IntField("Fila", level.objective.creature_position[0]);
            level.objective.creature_position[1] = EditorGUILayout.IntField("Columna", level.objective.creature_position[1]);
        }

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Dificultad", EditorStyles.boldLabel);
        level.max_shots          = EditorGUILayout.IntField("Disparos máx.", level.max_shots);
        level.min_shots_to_clear = EditorGUILayout.IntField("Mín. jugando óptimo", level.min_shots_to_clear);

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Colores disponibles", EditorStyles.boldLabel);
        level.available_colors ??= new List<string>();
        foreach (var color in Colors)
        {
            bool on  = level.available_colors.Contains(color);
            bool now = EditorGUILayout.ToggleLeft(color, on);
            if (now && !on)  level.available_colors.Add(color);
            if (!now && on)  level.available_colors.Remove(color);
        }

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Umbrales de estrellas", EditorStyles.boldLabel);
        level.star_thresholds ??= new List<int> { 0, 0, 0 };
        while (level.star_thresholds.Count < 3) level.star_thresholds.Add(0);
        for (int i = 0; i < 3; i++)
            level.star_thresholds[i] = EditorGUILayout.IntField($"{i + 1} estrella{(i > 0 ? "s" : "")}", level.star_thresholds[i]);

        if (EditorGUI.EndChangeCheck()) _dirty = true;

        GUILayout.Space(12);
        DrawSuggestions(level);

        GUILayout.Space(12);
        DrawValidation(level);
    }

    // ---------------------------------------------------------------- sugerencias

    void DrawSuggestions(LevelData level)
    {
        EditorGUILayout.LabelField("Sugerencias", EditorStyles.boldLabel);

        if (_reportStale)
        {
            _report      = LevelDifficultyEstimator.Analyze(level);
            _reportStale = false;
        }

        if (!_report.valid)
        {
            EditorGUILayout.HelpBox(_report.error, MessageType.Error);
            return;
        }

        if (_report.bubbles == 0) return;

        int min       = _report.minShots;
        int realistic = _report.realisticShots;

        EditorGUILayout.LabelField("Jugando perfecto",
                                   $"{min} disparos" + (_report.rescue ? " (hasta la criatura)" : ""));
        EditorGUILayout.LabelField("Jugando normal", $"{realistic} disparos");
        EditorGUILayout.LabelField("Burbujas", $"{_report.bubbles} en {_report.clusters} grupos" +
                                               (_report.lone > 0 ? $" ({_report.lone} sueltas)" : ""));
        EditorGUILayout.LabelField("Puntaje óptimo", $"~{LevelDifficultyEstimator.OptimalScore(_report, level.max_shots):n0}");
        EditorGUILayout.LabelField("Peor disparo", $"se lleva el {_report.collapse:P0} del nivel");

        // El aviso que faltaba: un nivel puede tener los disparos bien calculados y aun así
        // terminarse en tres jugadas porque el techo se corta de un match.
        if (_report.collapse > 0.30f)
            EditorGUILayout.HelpBox(
                $"Un solo disparo se lleva el {_report.collapse:P0} del nivel. Suele ser el techo: " +
                "si la fila de arriba tiene varias burbujas seguidas del mismo color, al reventarlas " +
                "cae todo lo que colgaba de ellas.", MessageType.Warning);

        var actual   = LevelDifficultyEstimator.Rate(realistic, level.max_shots);
        var expected = LevelDifficultyEstimator.Expected(PositionInChapter(level));

        GUILayout.Space(4);
        EditorGUILayout.LabelField("Dificultad", $"{Name(actual)}  ·  se espera {Name(expected)}",
                                   actual == expected ? EditorStyles.label : EditorStyles.boldLabel);

        if (actual == LevelDifficultyEstimator.Difficulty.Imposible)
            EditorGUILayout.HelpBox($"Con {level.max_shots} disparos no alcanza ni jugando perfecto.", MessageType.Error);
        else if (actual != expected)
            EditorGUILayout.HelpBox($"Por su posición en el capítulo debería sentirse {Name(expected).ToLower()}.", MessageType.Warning);

        GUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Aplicar", GUILayout.Width(50));

            foreach (var target in new[]
                     {
                         LevelDifficultyEstimator.Difficulty.Facil,
                         LevelDifficultyEstimator.Difficulty.Medio,
                         LevelDifficultyEstimator.Difficulty.Dificil,
                     })
            {
                int shots = LevelDifficultyEstimator.Suggest(realistic, target);

                if (!GUILayout.Button($"{Name(target)} ({shots})", EditorStyles.miniButton)) continue;

                level.max_shots          = shots;
                level.min_shots_to_clear = min;
                _dirty = true;
                GUI.FocusControl(null);
            }
        }

        GUILayout.Space(4);
        DrawStarSuggestion(level);

        EditorGUILayout.LabelField("'Normal' suma lo que se pierde por color inútil y por fallar.",
                                   EditorStyles.miniLabel);
    }

    void DrawStarSuggestion(LevelData level)
    {
        var stars = LevelDifficultyEstimator.SuggestStars(_report, level.max_shots);

        bool same = level.star_thresholds != null && level.star_thresholds.Count >= 3 &&
                    level.star_thresholds[0] == stars[0] &&
                    level.star_thresholds[1] == stars[1] &&
                    level.star_thresholds[2] == stars[2];

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Estrellas", GUILayout.Width(50));

            using (new EditorGUI.DisabledScope(same))
                if (GUILayout.Button($"{stars[0]:n0} · {stars[1]:n0} · {stars[2]:n0}", EditorStyles.miniButton))
                {
                    level.star_thresholds = new List<int> { stars[0], stars[1], stars[2] };
                    _dirty = true;
                    GUI.FocusControl(null);
                }
        }

        if (same || level.star_thresholds is not { Count: >= 3 } current || current[2] <= 0) return;

        // El desajuste que rompe la barra: si los umbrales no guardan la proporción en la que
        // están dibujados los marcos, el relleno cruza una estrella sin encenderla.
        float ratio1 = current[0] / (float)current[2] * LevelDifficultyEstimator.BAR_STAR_3;
        float ratio2 = current[1] / (float)current[2] * LevelDifficultyEstimator.BAR_STAR_3;

        bool aligned = Mathf.Abs(ratio1 - LevelDifficultyEstimator.BAR_STAR_1) < 0.03f &&
                       Mathf.Abs(ratio2 - LevelDifficultyEstimator.BAR_STAR_2) < 0.03f;

        if (aligned) return;

        EditorGUILayout.HelpBox(
            $"Los umbrales no caen donde están dibujadas las estrellas: la 1ª quedaría al " +
            $"{ratio1:P0} de la barra en vez del {LevelDifficultyEstimator.BAR_STAR_1:P0}, y la 2ª al " +
            $"{ratio2:P0} en vez del {LevelDifficultyEstimator.BAR_STAR_2:P0}. La barra va a pasar de " +
            "largo sin encenderlas.", MessageType.Warning);
    }

    static string Name(LevelDifficultyEstimator.Difficulty difficulty) => difficulty switch
    {
        LevelDifficultyEstimator.Difficulty.Imposible => "Imposible",
        LevelDifficultyEstimator.Difficulty.Dificil   => "Difícil",
        LevelDifficultyEstimator.Difficulty.Medio     => "Medio",
        LevelDifficultyEstimator.Difficulty.Facil     => "Fácil",
        _                                             => "Trivial",
    };

    // Posición del nivel dentro de su capítulo y tamaño del capítulo, para contrastar contra la
    // curva de dificultad del GDD. Se leen de los niveles cargados, no de una constante.
    int PositionInChapter(LevelData level) =>
        _levels.Count(e => e.data.chapter == level.chapter && e.data.id <= level.id);

    int ChapterSize(int chapter) => _levels.Count(e => e.data.chapter == chapter);

    // ---------------------------------------------------------------- validación

    // Las tres formas de romper un nivel sin darse cuenta. Se muestran en vivo y no solo al
    // guardar, porque lo útil es enterarse mientras se está pintando.
    void DrawValidation(LevelData level)
    {
        EditorGUILayout.LabelField("Revisión", EditorStyles.boldLabel);

        var problems = new List<string>();
        var bubbles  = level.bubbles ?? new List<BubbleEntry>();

        if (bubbles.Count == 0) problems.Add("El nivel no tiene ninguna burbuja.");

        foreach (var color in level.available_colors ?? new List<string>())
        {
            int count = bubbles.Count(b => b.color == color);
            if (count < 2)
                problems.Add($"'{color}' está entre los colores disponibles pero solo hay {count} en el grid. " +
                             "Con menos de dos no se puede hacer match.");
        }

        // BubbleColorExtensions.Parse lanza una excepción con un color desconocido, y eso rompe la
        // carga del nivel en plena partida.
        foreach (var unknown in bubbles.Select(b => b.color).Distinct().Where(c => !Swatches.ContainsKey(c ?? "")))
            problems.Add($"El color '{unknown}' no existe en BubbleColor: el nivel no va a cargar.");

        foreach (var orphan in FindOrphans(bubbles))
            problems.Add($"La burbuja en fila {orphan.y}, columna {orphan.x} no tiene camino hasta el techo: " +
                         "va a caer apenas empiece el nivel.");

        if (level.objective?.type == "rescue")
        {
            var pos = level.objective.creature_position;
            bool ok = pos != null && pos.Count >= 2 && bubbles.Any(b => b.row == pos[0] && b.col == pos[1]);
            if (!ok) problems.Add("La criatura no está sobre ninguna burbuja del grid.");
        }

        // CalculateStars cuenta un punto por cada umbral que el puntaje alcance. Con los tres en
        // cero el nivel entrega tres estrellas siempre, sin importar cómo se haya jugado.
        var thresholds = level.star_thresholds;
        if (thresholds != null && thresholds.Count >= 3)
        {
            if (thresholds[0] == 0 && thresholds[1] == 0 && thresholds[2] == 0)
                problems.Add("Los umbrales de estrellas están en cero: el nivel va a dar 3 estrellas siempre.");
            else if (thresholds[0] > thresholds[1] || thresholds[1] > thresholds[2])
                problems.Add("Los umbrales de estrellas tienen que ir de menor a mayor.");
        }

        if (level.min_shots_to_clear <= 0)
            problems.Add("'Mín. jugando óptimo' está sin calibrar (0). Los botones de Sugerencias lo llenan.");
        else if (level.min_shots_to_clear > level.max_shots)
            problems.Add("El mínimo jugando óptimo supera los disparos máximos: el nivel es imposible.");

        if (problems.Count == 0)
            EditorGUILayout.HelpBox("Sin problemas detectados.", MessageType.Info);
        else
            foreach (var problem in problems)
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
    }

    // Burbujas sin camino de vecinos hasta la fila 0. Una sola BFS desde el techo: todo lo que no
    // alcance, cae. Usa HexGridMath.GetNeighbors para no duplicar la regla de vecindad, que cambia
    // entre filas pares e impares y es el error más fácil de cometer.
    static List<Vector2Int> FindOrphans(List<BubbleEntry> bubbles)
    {
        var all = new HashSet<Vector2Int>(bubbles.Select(b => new Vector2Int(b.col, b.row)));
        var reached = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();

        foreach (var cell in all)
            if (cell.y == 0) { reached.Add(cell); queue.Enqueue(cell); }

        while (queue.Count > 0)
        {
            foreach (var neighbor in HexGridMath.GetNeighbors(queue.Dequeue()))
            {
                if (!all.Contains(neighbor) || !reached.Add(neighbor)) continue;
                queue.Enqueue(neighbor);
            }
        }

        return all.Where(c => !reached.Contains(c)).ToList();
    }

    // Magenta para un color que el editor no conoce. Un JSON escrito a mano puede traer uno que
    // todavía no está en el enum, y eso tiene que verse en el grid, no tumbar la ventana.
    static Color SwatchFor(string color) =>
        Swatches.TryGetValue(color ?? "", out var swatch) ? swatch : Color.magenta;

    static bool IsCreatureCell(LevelData level, int row, int col)
    {
        var pos = level.objective?.creature_position;
        return level.objective?.type == "rescue" && pos != null && pos.Count >= 2 &&
               pos[0] == row && pos[1] == col;
    }

    // ---------------------------------------------------------------- generar

    // Paleta de respaldo, solo para un nivel que todavía no tiene colores elegidos. El orden
    // importa: los primeros son los que más se distinguen entre sí de un vistazo.
    static readonly string[] FallbackPalette =
    {
        "red", "blue", "yellow", "green", "purple", "orange", "pink", "mint_green",
    };

    void GenerateCurrent()
    {
        if (_current.data.bubbles is { Count: > 0 } &&
            !EditorUtility.DisplayDialog("Generar layout",
                $"Se reemplaza el grid de '{_current.data.name}' y se recalculan disparos y estrellas.\n\n" +
                "El nombre, el id y el tipo de objetivo se conservan.",
                "Generar", "Cancelar"))
            return;

        if (Generate(_current.data, keepExtraColors: true))
        {
            _dirty       = true;
            _reportStale = true;
        }
        else
        {
            EditorUtility.DisplayDialog("Generar layout",
                "No se pudo armar un layout válido. Probá de nuevo.", "Entendido");
        }
    }

    // keepExtraColors: al generar UN nivel se respetan los colores de más que haya marcados a
    // mano, porque ahí se está ajustando algo puntual. Al regenerar el capítulo entero no: es un
    // acto masivo, y la cantidad la decide la curva pareja para los 60.
    bool Generate(LevelData level, bool keepExtraColors)
    {
        int position = PositionInChapter(level);
        var target   = LevelDifficultyEstimator.Expected(position);
        float progress = LevelDifficultyEstimator.Progress(position, ChapterSize(level.chapter));

        // La semilla sale del id y del reloj: dos niveles distintos nunca salen iguales, y
        // regenerar el mismo nivel da algo nuevo cada vez.
        int seed = level.id * 7919 + (int)(EditorApplication.timeSinceStartup * 1000) % 100000;

        // Los colores marcados mandan sobre CUÁLES se usan — son una decisión de diseño, la
        // paleta del capítulo, lo que pega con el fondo. Pero no sobre cuántos: si el nivel pide
        // más de los que hay marcados, se completan desde la paleta de respaldo.
        var chosen = level.available_colors ?? new List<string>();
        int wanted = ColorCountFor(position, level.chapter, target);
        if (keepExtraColors) wanted = Mathf.Max(wanted, chosen.Count);

        var palette = chosen.Concat(FallbackPalette.Where(c => !chosen.Contains(c)))
                            .Take(wanted)
                            .ToArray();

        return LevelGenerator.Generate(level, target, progress, palette, seed, exactColors: true);
    }

    // Cuántos colores le tocan a un nivel. El tutorial son los primeros niveles del capítulo 1 y
    // nada más: pasado eso, dos colores se vuelven monótonos porque casi cualquier disparo sirve
    // y no hay nada que decidir.
    //
    // Los capítulos 2 y 3 arrancan ya arriba — el jugador que llega ahí lleva 60 niveles jugados.
    static int ColorCountFor(int position, int chapter, LevelDifficultyEstimator.Difficulty target)
    {
        int count = chapter > 1
            ? (position <= 20 ? 4 : 5)
            : position switch
            {
                <= 4  => 2,   // tutorial: entender la mecánica, no elegir
                <= 15 => 3,
                <= 35 => 4,
                _     => 5,
            };

        // El número de arriba es el de un nivel tranquilo; los que aprietan suman uno más.
        if (target != LevelDifficultyEstimator.Difficulty.Facil) count++;

        return Mathf.Clamp(count, 2, FallbackPalette.Length);
    }

    void GenerateChapter()
    {
        int chapter = _current.data.chapter;
        var targets = _levels.Where(e => e.data.chapter == chapter).ToList();

        if (!EditorUtility.DisplayDialog("Generar capítulo completo",
                $"Se van a regenerar los {targets.Count} niveles del capítulo {chapter} y a guardarlos.\n\n" +
                "Se conservan nombres, ids y objetivos, pero los grids actuales se pierden. " +
                "Esto no se puede deshacer.",
                $"Regenerar {targets.Count} niveles", "Cancelar"))
            return;

        int done = 0, failed = 0;
        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < targets.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Generando capítulo",
                    $"{targets[i].data.name} ({i + 1}/{targets.Count})", i / (float)targets.Count);

                if (Generate(targets[i].data, keepExtraColors: false)) { Save(targets[i]); done++; }
                else failed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        _dirty       = false;
        _reportStale = true;

        Debug.Log($"[Editor de niveles] Capítulo {chapter}: {done} niveles generados" +
                  (failed > 0 ? $", {failed} fallaron" : "") + ".");
    }

    // ---------------------------------------------------------------- guardar y probar

    void Save(Entry entry)
    {
        entry.data.bubbles?.Sort((a, b) => a.row != b.row ? a.row.CompareTo(b.row) : a.col.CompareTo(b.col));

        File.WriteAllText(entry.path, JsonUtility.ToJson(entry.data, true));
        AssetDatabase.ImportAsset(entry.path);

        // El índice guarda id, capítulo y nombre: los tres se pueden haber editado recién.
        LevelIndexBuilder.RebuildSilently();

        _dirty = false;
        Debug.Log($"[Editor de niveles] Guardado {entry.path}");
    }

    // El id es global (LevelLoader carga toda la carpeta Levels de una y busca por id, sin mirar
    // en qué subcarpeta está), así que el duplicado toma el siguiente id libre de todo el juego y
    // queda al final. Insertarlo en medio de un capítulo obligaría a renumerar el resto.
    void Duplicate()
    {
        int nextId = _levels.Count > 0 ? _levels.Max(e => e.data.id) + 1 : 1;
        string folder = Path.GetDirectoryName(_current.path);
        string path   = $"{folder}/{nextId:000}.json";

        if (File.Exists(path))
        {
            EditorUtility.DisplayDialog("Duplicar", $"Ya existe {path}.", "Entendido");
            return;
        }

        var copy = JsonUtility.FromJson<LevelData>(JsonUtility.ToJson(_current.data));
        copy.id   = nextId;
        copy.name = $"Nivel {nextId}";

        File.WriteAllText(path, JsonUtility.ToJson(copy, true));
        AssetDatabase.ImportAsset(path);

        // Un nivel que no está en el índice no aparece en el mapa. Se regenera acá para que no
        // dependa de acordarse de hacerlo.
        LevelIndexBuilder.RebuildSilently();

        Reload();

        var created = _levels.FirstOrDefault(e => e.path == path);
        if (created != null) Select(created);

        Debug.Log($"[Editor de niveles] Creado {path} (id {nextId}, capítulo {copy.chapter}).");
    }

    void PlayCurrent()
    {
        if (_dirty && !EditorUtility.DisplayDialog(
                "Probar nivel",
                "Hay cambios sin guardar. Se guardan antes de probar.",
                "Guardar y probar", "Cancelar"))
            return;

        if (_dirty) Save(_current);

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Misma clave que usa LevelMapController al entrar a un nivel.
        PlayerPrefs.SetInt(SELECTED_LEVEL, _current.data.id);

        // Y la marca de prueba: GameplayController la consume al cargar y no guarda nada de esta
        // partida. Sin esto, ganar el nivel 60 para ver cómo quedó dejaba desbloqueados los 60.
        PlayerPrefs.SetInt(GameplayController.EDITOR_TEST_KEY, 1);
        PlayerPrefs.Save();

        EditorSceneManager.OpenScene(GAMEPLAY_SCENE);
        EditorApplication.isPlaying = true;
    }

    bool ConfirmDiscard()
    {
        if (!_dirty) return true;
        return EditorUtility.DisplayDialog("Cambios sin guardar",
            "El nivel abierto tiene cambios sin guardar. ¿Descartarlos?", "Descartar", "Cancelar");
    }

    // Círculo blanco con el borde suavizado, para dibujar las celdas. Se tiñe con GUI.color.
    static Texture2D BuildCircleTexture()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };

        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d  = Mathf.Sqrt(dx * dx + dy * dy);

                // Opaco hasta 0.92 del radio y desvanecido hasta el borde. InverseLerp con los
                // extremos invertidos da justamente eso: 1 adentro, 0 afuera.
                float a = Mathf.InverseLerp(1f, 0.92f, d);

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
