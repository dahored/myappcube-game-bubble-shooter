using System.Collections.Generic;
using UnityEngine;

// Factorea el loop de carga que LevelMapController ya usa, sin tocar ese archivo.
public static class LevelLoader
{
    public static List<LevelData> LoadAll()
    {
        var result = new List<LevelData>();
        var jsons  = Resources.LoadAll<TextAsset>("Levels");
        foreach (var json in jsons)
        {
            var lvl = JsonUtility.FromJson<LevelData>(json.text);
            if (lvl != null) result.Add(lvl);
        }
        return result;
    }

    public static LevelData LoadById(int id)
    {
        foreach (var lvl in LoadAll())
            if (lvl.id == id) return lvl;
        return null;
    }
}
