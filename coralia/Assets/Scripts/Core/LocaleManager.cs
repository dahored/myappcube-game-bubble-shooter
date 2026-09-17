using System.Collections.Generic;
using UnityEngine;

public static class LocaleManager
{
    static readonly Dictionary<string, string> _keys = new();
    static bool _loaded;
    public static event System.Action OnLanguageChanged;

    public static string Get(string key)
    {
        if (!_loaded) Load();
        return _keys.TryGetValue(key, out var val) ? val : key;
    }

    // Para texto que tiene un original razonable fuera del CSV — los nombres de nivel, que se
    // escriben en el editor y viven en el JSON. Si la clave todavía no se exportó, se muestra ese
    // original en vez de la clave cruda ("level.7.name" en pantalla).
    public static string Get(string key, string fallback)
    {
        if (!_loaded) Load();
        return _keys.TryGetValue(key, out var val) && val.Length > 0 ? val : fallback;
    }

    static void Load()
    {
        _loaded = true;
        var csv = Resources.Load<TextAsset>("translations");
        if (csv == null) { Debug.LogWarning("[LocaleManager] translations.csv not found"); return; }

        var lang = SaveManager.Language;
        var lines = csv.text.Split('\n');
        if (lines.Length < 2) return;

        var headers = lines[0].Trim().Split(',');
        int col = System.Array.IndexOf(headers, lang);
        if (col < 1) col = 1; // fallback a es

        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Trim().Split(',');
            if (parts.Length <= col || parts[0].Length == 0) continue;

            // Celda vacía = todavía sin traducir. Se cae al español en vez de dejar el texto en
            // blanco, que es lo que permite ir traduciendo de a poco sin romper nada: una clave
            // nueva se ve en español en los otros idiomas hasta que alguien la complete.
            string value = parts[col];
            if (value.Length == 0 && parts.Length > 1) value = parts[1];

            _keys[parts[0]] = value;
        }
    }

    public static void Reload() { 
        _loaded = false; _keys.Clear(); Load(); 
        OnLanguageChanged?.Invoke();
    }
}
