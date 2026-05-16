using System.Collections.Generic;
using UnityEngine;

public static class LocaleManager
{
    static readonly Dictionary<string, string> _keys = new();
    static bool _loaded;

    public static string Get(string key)
    {
        if (!_loaded) Load();
        return _keys.TryGetValue(key, out var val) ? val : key;
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
            if (parts.Length > col && parts[0].Length > 0)
                _keys[parts[0]] = parts[col];
        }
    }

    public static void Reload() { _loaded = false; _keys.Clear(); Load(); }
}
