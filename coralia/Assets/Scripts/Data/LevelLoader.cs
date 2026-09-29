using System.Collections.Generic;
using UnityEngine;

// Acceso a los niveles. Dos caminos distintos a propósito:
//
//   Entries — el índice: id, capítulo y nombre de TODOS los niveles. Es lo único que el mapa
//             necesita, pesa unas decenas de KB con 500 niveles y se carga una sola vez.
//
//   LoadById — el nivel completo, con sus burbujas. Un archivo, cuando se va a jugar.
//
// Antes LoadById recorría LoadAll(), que parseaba los 180 JSON enteros para devolver uno. Y eso
// pasaba al tocar cada nodo del mapa, no solo al entrar a jugar.
public static class LevelLoader
{
    const string INDEX_PATH = "levels_index";

    static LevelIndexEntry[] _entries;

    // Índice completo, ordenado por capítulo y después por id — el orden en que el mapa los
    // dibuja. Se arma una vez y queda en memoria.
    public static IReadOnlyList<LevelIndexEntry> Entries
    {
        get
        {
            if (_entries != null) return _entries;

            var asset = Resources.Load<TextAsset>(INDEX_PATH);
            if (asset == null)
            {
                Debug.LogWarning($"[LevelLoader] Falta Resources/{INDEX_PATH}.json — se regenera con " +
                                 "Coralia -> Regenerar índice de niveles. Mientras tanto se leen los " +
                                 "archivos uno por uno, que es mucho más lento.");
                _entries = BuildFallbackIndex();
                return _entries;
            }

            var index = JsonUtility.FromJson<LevelIndex>(asset.text);
            _entries  = index?.levels ?? new LevelIndexEntry[0];
            return _entries;
        }
    }

    public static LevelIndexEntry EntryFor(int id)
    {
        foreach (var entry in Entries)
            if (entry.id == id) return entry;
        return null;
    }

    // El nivel completo. Solo acá se toca el archivo con las burbujas.
    public static LevelData LoadById(int id)
    {
        var entry = EntryFor(id);
        if (entry == null) return null;

        var asset = Resources.Load<TextAsset>(entry.path);
        if (asset == null)
        {
            Debug.LogWarning($"[LevelLoader] El índice apunta a '{entry.path}' y ahí no hay nada. " +
                             "Hay que regenerar el índice.");
            return null;
        }

        return JsonUtility.FromJson<LevelData>(asset.text);
    }

    // Por si el índice no existe todavía: hace lo de antes, leyendo todo. Sirve para que el juego
    // no se rompa, no para dejarlo así.
    static LevelIndexEntry[] BuildFallbackIndex()
    {
        var result = new List<LevelIndexEntry>();

        foreach (var json in Resources.LoadAll<TextAsset>("Levels"))
        {
            var level = JsonUtility.FromJson<LevelData>(json.text);
            if (level == null || level.id >= 900) continue; // 9xx son niveles de prueba

            result.Add(new LevelIndexEntry
            {
                id      = level.id,
                chapter = level.chapter,
                name    = level.name,
                path    = $"Levels/Chapter_{level.chapter}/{level.id:000}",
            });
        }

        result.Sort((a, b) => a.chapter != b.chapter ? a.chapter.CompareTo(b.chapter) : a.id.CompareTo(b.id));
        return result.ToArray();
    }
}
