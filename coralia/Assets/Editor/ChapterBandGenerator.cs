using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Genera la banda de decoraciones más lejana de un capítulo — la que tapa los costados vacíos
// en pantallas anchas (issue #66).
//
// Por qué hace falta: la cámara del mapa tiene FOV VERTICAL fijo de 60°, así que una pantalla
// más ancha no ve más alto sino más a los lados. El semiancho visible es 0.577 * distancia *
// aspecto: 0.267*d en un iPhone alargado, 0.433*d en un iPad 4:3. Con las decoraciones a offset
// 1.8 como mucho, el iPad se queda sin cobertura pasadas unas 4 unidades de distancia y el hueco
// se abre hacia el horizonte.
//
// Escribe INSERTANDO texto antes del cierre del array, sin releer ni reescribir el resto del
// archivo: los Chapter_N.json están puestos a mano y un round-trip por JsonUtility los
// reformatearía entero y agregaría campos por defecto que hoy no están.
public class ChapterBandGenerator : EditorWindow
{
    const string CHAPTERS_FOLDER = "Assets/Resources/Chapters";

    // Todo lo que esté a este offset o más se considera parte de esta banda, y es lo único que
    // el generador puede borrar. Las decoraciones hechas a mano llegan a 1.8.
    const float BAND_FLOOR = 2.5f;

    [SerializeField] int    chapterIndex;
    [SerializeField] float  offsetMin  = 3.0f;
    [SerializeField] float  offsetMax  = 3.6f;
    [SerializeField] float  spacing    = 1.5f;   // cada cuántos niveles cae una pieza, por lado
    [SerializeField] float  jitter     = 0.4f;   // desorden en 'at', para que no queden en fila
    [SerializeField] float  scaleMin   = 1.1f;
    [SerializeField] float  scaleMax   = 1.6f;
    [SerializeField] int    seed       = 1;
    [SerializeField] bool   replace    = true;

    string[] _files;
    List<string> _ids = new();
    List<bool>   _use = new();

    [MenuItem("Coralia/Generar banda lejana de decoraciones")]
    static void Open() => GetWindow<ChapterBandGenerator>("Banda lejana");

    void OnEnable()
    {
        _files = Directory.Exists(CHAPTERS_FOLDER)
            ? Directory.GetFiles(CHAPTERS_FOLDER, "*.json").OrderBy(f => f).ToArray()
            : new string[0];

        LoadCatalogIds();
    }

    // Los ids salen del catálogo real, no de una lista escrita acá: si mañana agregás una pieza
    // nueva aparece sola en la ventana.
    void LoadCatalogIds()
    {
        _ids.Clear();
        _use.Clear();

        var catalog = AssetDatabase.FindAssets("t:DecorationCatalog")
                                   .Select(AssetDatabase.GUIDToAssetPath)
                                   .FirstOrDefault();
        if (catalog == null) return;

        foreach (var line in File.ReadLines(catalog))
        {
            var match = Regex.Match(line, @"^\s*-\s*id:\s*(\S+)");
            if (!match.Success) continue;

            string id = match.Groups[1].Value;
            _ids.Add(id);
            // A esta distancia las piezas chicas no se leen: por defecto solo grupos y rocas.
            _use.Add(id.StartsWith("group") || id.StartsWith("rock"));
        }
    }

    void OnGUI()
    {
        if (_files.Length == 0)
        {
            EditorGUILayout.HelpBox($"No hay ningún capítulo en {CHAPTERS_FOLDER}.", MessageType.Warning);
            return;
        }

        chapterIndex = EditorGUILayout.Popup("Capítulo", chapterIndex,
                                             _files.Select(Path.GetFileNameWithoutExtension).ToArray());

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Colocación", EditorStyles.boldLabel);
        offsetMin = EditorGUILayout.FloatField("Offset mínimo", offsetMin);
        offsetMax = EditorGUILayout.FloatField("Offset máximo", offsetMax);
        spacing   = EditorGUILayout.FloatField("Cada cuántos niveles", spacing);
        jitter    = EditorGUILayout.Slider("Desorden", jitter, 0f, 1f);
        scaleMin  = EditorGUILayout.FloatField("Escala mínima", scaleMin);
        scaleMax  = EditorGUILayout.FloatField("Escala máxima", scaleMax);
        seed      = EditorGUILayout.IntField("Semilla", seed);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Piezas", EditorStyles.boldLabel);
        for (int i = 0; i < _ids.Count; i++)
            _use[i] = EditorGUILayout.ToggleLeft(_ids[i], _use[i]);

        EditorGUILayout.Space();
        replace = EditorGUILayout.ToggleLeft($"Reemplazar la banda anterior (offset >= {BAND_FLOOR})", replace);
        EditorGUILayout.HelpBox(
            $"Solo se tocan las decoraciones con offset >= {BAND_FLOOR}. Las tuyas, que llegan a 1.8, " +
            "no se leen ni se reescriben — el generador inserta texto antes del cierre del array.",
            MessageType.Info);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!_use.Any(u => u)))
            if (GUILayout.Button("Generar", GUILayout.Height(30)))
                Generate(_files[chapterIndex]);

        // Para medir: esta banda son decenas de sprites transparentes grandes, y el mapa está
        // limitado por fill rate (#65). Quitarla y volver a poner es la única forma de saber
        // cuánto cuesta en un dispositivo real.
        if (GUILayout.Button("Quitar banda (para comparar rendimiento)"))
            Clear(_files[chapterIndex]);
    }

    void Clear(string path)
    {
        string text = File.ReadAllText(path);
        text = RemoveBand(text, out int removed);

        if (removed == 0)
        {
            EditorUtility.DisplayDialog("Banda lejana", "Ese capítulo no tiene banda que quitar.", "Entendido");
            return;
        }

        // La última fila que queda se puede haber quedado con una coma colgando si la banda
        // estaba al final del array, que es donde este generador siempre la pone.
        text = Regex.Replace(text, @",(\s*\n\s*\])", "$1");

        File.WriteAllText(path, text);
        AssetDatabase.ImportAsset(path);

        EditorUtility.DisplayDialog("Banda lejana",
            $"{removed} decoraciones quitadas de {Path.GetFileNameWithoutExtension(path)}.", "Bien");
    }

    void Generate(string path)
    {
        string text = File.ReadAllText(path);

        int removed = 0;
        if (replace) text = RemoveBand(text, out removed);

        // El tramo a cubrir sale de lo que ya hay: así la banda acompaña al capítulo sin que
        // haya que decirle cuántos niveles tiene.
        var ats = Regex.Matches(text, @"""at""\s*:\s*(-?[\d.]+)")
                       .Select(m => float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                       .ToList();
        if (ats.Count == 0)
        {
            EditorUtility.DisplayDialog("Banda lejana",
                "El capítulo no tiene ninguna decoración de la cual deducir el tramo a cubrir.", "Entendido");
            return;
        }

        var ids = _ids.Where((_, i) => _use[i]).ToList();
        var rng = new System.Random(seed);
        var rows = new List<string>();

        // Una pieza por lado cada 'spacing' niveles, alternando el arranque para que izquierda y
        // derecha no queden enfrentadas de a pares.
        for (int side = 0; side < 2; side++)
        {
            string name = side == 0 ? "left" : "right";
            for (float at = ats.Min() + side * spacing * 0.5f; at <= ats.Max(); at += spacing)
            {
                float placed = at + (float)(rng.NextDouble() * 2 - 1) * jitter;
                float offset = Mathf.Lerp(offsetMin, offsetMax, (float)rng.NextDouble());
                float scale  = Mathf.Lerp(scaleMin, scaleMax, (float)rng.NextDouble());
                string id    = ids[rng.Next(ids.Count)];
                bool   flip  = rng.Next(2) == 0;

                rows.Add($"    {{ \"id\": \"{id}\", \"at\": {N(placed)}, \"side\": \"{name}\", " +
                         $"\"offset\": {N(offset)}, \"scale\": {N(scale)}{(flip ? ", \"flip\": true" : "")} }}");
            }
        }

        text = Insert(text, rows);
        File.WriteAllText(path, text);
        AssetDatabase.ImportAsset(path);

        EditorUtility.DisplayDialog("Banda lejana",
            $"{rows.Count} decoraciones agregadas a {Path.GetFileNameWithoutExtension(path)}" +
            (removed > 0 ? $"\n{removed} de la banda anterior reemplazadas." : "") +
            $"\n\nCubre de {N(ats.Min())} a {N(ats.Max())} en 'at'.", "Bien");
    }

    // Borra solo las líneas cuya decoración está en la banda. Va por línea y no por parseo del
    // JSON entero justamente para no tocar el formato del resto.
    static string RemoveBand(string text, out int removed)
    {
        var kept = new List<string>();
        removed = 0;

        foreach (var line in text.Split('\n'))
        {
            var match = Regex.Match(line, @"""offset""\s*:\s*([\d.]+)");
            if (match.Success &&
                float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) >= BAND_FLOOR)
            {
                removed++;
                continue;
            }
            kept.Add(line);
        }

        return string.Join("\n", kept);
    }

    // Inserta antes del cierre del array, arreglando la coma de la última fila que había.
    static string Insert(string text, List<string> rows)
    {
        int close = text.LastIndexOf("  ]", System.StringComparison.Ordinal);
        if (close < 0) return text;

        string head = text.Substring(0, close).TrimEnd();
        string tail = text.Substring(close);

        var sb = new StringBuilder(head);
        if (!head.EndsWith("[")) sb.Append(',');
        sb.Append('\n').Append(string.Join(",\n", rows)).Append('\n');
        return sb.Append(tail).ToString();
    }

    // Sin notación científica ni comas decimales: el JSON tiene que leerse igual en cualquier
    // configuración regional.
    static string N(float value) =>
        System.Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);
}
