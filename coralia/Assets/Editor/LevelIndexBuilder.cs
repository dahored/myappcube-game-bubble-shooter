using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Genera Resources/levels_index.json: el listado liviano de todos los niveles que usa el mapa.
//
// Hay que correrlo al agregar, quitar o renumerar niveles. Se avisa solo si queda desactualizado:
// LevelLoader compara la cantidad al cargar y el editor de niveles lo regenera después de crear
// un nivel nuevo.
public static class LevelIndexBuilder
{
    const string LEVELS_FOLDER = "Assets/Resources/Levels";
    const string INDEX_PATH    = "Assets/Resources/levels_index.json";

    // Los 9xx son niveles de prueba: no van al mapa.
    const int FIRST_TEST_ID = 900;

    [MenuItem("Coralia/Regenerar índice de niveles")]
    public static void Rebuild()
    {
        var entries = Collect();
        if (entries.Count == 0)
        {
            EditorUtility.DisplayDialog("Índice de niveles",
                $"No se encontró ningún nivel en {LEVELS_FOLDER}.", "Entendido");
            return;
        }

        Write(entries);

        EditorUtility.DisplayDialog("Índice de niveles",
            $"{entries.Count} niveles indexados en {entries.Select(e => e.chapter).Distinct().Count()} capítulos.",
            "Bien");
    }

    // Sin diálogos, para llamarlo desde el editor de niveles al crear o renombrar uno.
    public static void RebuildSilently()
    {
        var entries = Collect();
        if (entries.Count > 0) Write(entries);
    }

    static List<LevelIndexEntry> Collect()
    {
        var entries = new List<LevelIndexEntry>();
        if (!Directory.Exists(LEVELS_FOLDER)) return entries;

        foreach (var file in Directory.GetFiles(LEVELS_FOLDER, "*.json", SearchOption.AllDirectories))
        {
            LevelData level;
            try { level = JsonUtility.FromJson<LevelData>(File.ReadAllText(file)); }
            catch { continue; }

            if (level == null || level.id >= FIRST_TEST_ID) continue;

            // La ruta que espera Resources.Load: relativa a Resources y sin extensión.
            string path = file.Replace('\\', '/')
                              .Replace("Assets/Resources/", "")
                              .Replace(".json", "");

            entries.Add(new LevelIndexEntry
            {
                id      = level.id,
                chapter = level.chapter,
                name    = level.name,
                path    = path,
            });
        }

        // Mismo orden en que el mapa dibuja los nodos, para que no tenga que ordenar nada.
        entries.Sort((a, b) => a.chapter != b.chapter ? a.chapter.CompareTo(b.chapter) : a.id.CompareTo(b.id));

        var repeated = entries.GroupBy(e => e.id).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        if (repeated.Length > 0)
            Debug.LogError($"[LevelIndexBuilder] Hay ids repetidos: {string.Join(", ", repeated)}. " +
                           "El mapa va a mostrar nodos duplicados y entrar a un nivel puede abrir el otro.");

        return entries;
    }

    static void Write(List<LevelIndexEntry> entries)
    {
        var index = new LevelIndex { levels = entries.ToArray() };

        File.WriteAllText(INDEX_PATH, JsonUtility.ToJson(index, true));
        AssetDatabase.ImportAsset(INDEX_PATH);

        Debug.Log($"[LevelIndexBuilder] {entries.Count} niveles en {INDEX_PATH}.");
    }
}
