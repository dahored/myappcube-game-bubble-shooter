using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Vuelca a translations.csv los nombres de niveles y capítulos que hoy viven en sus JSON.
//
// Los nombres se escriben en el editor de niveles, en español, y ahí es donde tienen que seguir
// escribiéndose: es el lugar donde se ven junto al grid que nombran. Pero el texto que llega a
// pantalla sale del CSV, como todo el resto de la interfaz. Esta herramienta es el puente: se
// corre cuando se agregan o renombran niveles, y deja las columnas de los otros idiomas vacías
// para que alguien las traduzca después.
//
// Nunca pisa una traducción existente. Si una clave ya está en el CSV, solo se actualiza su
// columna española; lo que haya en inglés, italiano, francés, alemán o portugués queda intacto.
public static class TranslationExporter
{
    const string CSV_PATH    = "Assets/Resources/translations.csv";
    const string LEVELS_PATH = "Assets/Resources/Levels";
    const string CHAPTERS    = "Assets/Resources/Chapters";

    // Chequea que ninguna fila tenga más celdas que la cabecera, que es lo que pasa cuando una
    // traducción lleva una coma. LocaleManager.Load parte por comas sin mirar comillas, así que
    // una sola coma corre todos los idiomas siguientes un lugar: el alemán aparece recortado y
    // el portugués muestra la segunda mitad del alemán. Pasó de verdad, y en silencio.
    [MenuItem("Coralia/Revisar translations.csv")]
    static void Validate()
    {
        if (!File.Exists(CSV_PATH)) return;

        var lines   = File.ReadAllLines(CSV_PATH);
        int columns = lines[0].Split(',').Length;
        var broken  = new List<string>();

        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim().Length == 0) continue;

            int cells = lines[i].Split(',').Length;
            if (cells != columns) broken.Add($"línea {i + 1}: {lines[i].Split(',')[0]} ({cells} celdas)");
        }

        if (broken.Count == 0)
        {
            EditorUtility.DisplayDialog("Revisar traducciones",
                $"{lines.Length - 1} claves, todas con {columns} columnas.", "Bien");
            return;
        }

        Debug.LogError("[TranslationExporter] Filas con comas dentro del texto:\n" + string.Join("\n", broken));
        EditorUtility.DisplayDialog("Revisar traducciones",
            $"{broken.Count} filas tienen comas dentro del texto y desalinean los idiomas. " +
            "Están listadas en la consola.", "Entendido");
    }

    [MenuItem("Coralia/Exportar nombres de nivel a traducciones")]
    static void Export()
    {
        if (!File.Exists(CSV_PATH))
        {
            EditorUtility.DisplayDialog("Exportar nombres", $"No se encontró {CSV_PATH}.", "Entendido");
            return;
        }

        var spanish = CollectNames();
        if (spanish.Count == 0)
        {
            EditorUtility.DisplayDialog("Exportar nombres", "No se encontró ningún nivel con nombre.", "Entendido");
            return;
        }

        var lines   = File.ReadAllLines(CSV_PATH).ToList();
        int columns = lines[0].Split(',').Length;

        // Índice de las filas que ya existen, para actualizarlas en su sitio en vez de duplicar.
        var rowOf = new Dictionary<string, int>();
        for (int i = 1; i < lines.Count; i++)
        {
            string key = lines[i].Split(',')[0].Trim();
            if (key.Length > 0) rowOf[key] = i;
        }

        int added = 0, updated = 0;

        foreach (var (key, name) in spanish)
        {
            if (rowOf.TryGetValue(key, out int row))
            {
                var cells = lines[row].Split(',').ToList();
                while (cells.Count < columns) cells.Add("");

                if (cells[1] == name) continue;

                cells[1]   = name;   // solo la columna española
                lines[row] = string.Join(",", cells);
                updated++;
            }
            else
            {
                // Clave nueva: español puesto, el resto vacío. LocaleManager cae al español
                // mientras estén así, de modo que el juego no muestra huecos.
                lines.Add(key + "," + name + new string(',', columns - 2));
                added++;
            }
        }

        if (added == 0 && updated == 0)
        {
            EditorUtility.DisplayDialog("Exportar nombres", "El CSV ya estaba al día.", "Entendido");
            return;
        }

        File.WriteAllLines(CSV_PATH, lines);
        AssetDatabase.ImportAsset(CSV_PATH);

        Debug.Log($"[TranslationExporter] {added} claves nuevas, {updated} actualizadas en {CSV_PATH}. " +
                  "Las columnas en, it, fr, de y pt quedan vacías hasta que se traduzcan.");
    }

    // Las comas romperían el CSV, que se parsea con un split simple (ver LocaleManager.Load).
    static List<(string key, string name)> CollectNames()
    {
        var result = new List<(string, string)>();

        foreach (var path in Directory.GetFiles(LEVELS_PATH, "*.json", SearchOption.AllDirectories)
                                      .OrderBy(p => p))
        {
            var level = Read<LevelData>(path);
            if (level == null || string.IsNullOrWhiteSpace(level.name)) continue;

            result.Add((level.NameKey, Sanitize(level.name, path)));
        }

        if (Directory.Exists(CHAPTERS))
            foreach (var path in Directory.GetFiles(CHAPTERS, "*.json").OrderBy(p => p))
            {
                var chapter = Read<ChapterData>(path);
                if (chapter == null || string.IsNullOrWhiteSpace(chapter.name)) continue;

                result.Add((chapter.NameKey, Sanitize(chapter.name, path)));
            }

        return result;
    }

    static string Sanitize(string name, string path)
    {
        if (!name.Contains(',')) return name;

        Debug.LogWarning($"[TranslationExporter] '{name}' ({Path.GetFileName(path)}) tiene una coma: " +
                         "se reemplaza por un punto, porque partiría la fila del CSV.", null);
        return name.Replace(',', '.');
    }

    static T Read<T>(string path) where T : class
    {
        try { return JsonUtility.FromJson<T>(File.ReadAllText(path)); }
        catch { return null; }
    }
}
