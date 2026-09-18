using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Recalcula star_thresholds en TODOS los niveles de una pasada, sin tocar el layout.
//
// Hace falta cada vez que cambia la fórmula de ScoreRules: los umbrales guardados están en la
// escala vieja, así que un nivel entero puede no llegar ni a la primera estrella, o encenderlas
// las tres en el primer disparo. El botón del editor de niveles hace esto mismo pero de a uno,
// y son 180.
public static class StarThresholdRecalculator
{
    const string LEVELS_FOLDER = "Assets/Resources/Levels";

    [MenuItem("Coralia/Recalcular umbrales de estrellas")]
    public static void RecalculateAll()
    {
        var files = Directory.Exists(LEVELS_FOLDER)
            ? Directory.GetFiles(LEVELS_FOLDER, "*.json", SearchOption.AllDirectories).OrderBy(f => f).ToArray()
            : new string[0];

        if (files.Length == 0)
        {
            EditorUtility.DisplayDialog("Umbrales de estrellas",
                $"No se encontró ningún nivel en {LEVELS_FOLDER}.", "Entendido");
            return;
        }

        if (!EditorUtility.DisplayDialog("Umbrales de estrellas",
                $"Se van a recalcular los umbrales de {files.Length} niveles con la fórmula actual de " +
                "ScoreRules.\n\nSe reescribe solo star_thresholds — el layout, max_shots y los colores " +
                "quedan igual.", "Recalcular", "Cancelar"))
            return;

        int changed = 0;
        var failed  = new List<string>();

        try
        {
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                EditorUtility.DisplayProgressBar("Umbrales de estrellas",
                    Path.GetFileName(file), (i + 1) / (float)files.Length);

                LevelData level;
                try { level = JsonUtility.FromJson<LevelData>(File.ReadAllText(file)); }
                catch { failed.Add($"{Path.GetFileName(file)}: no se pudo leer"); continue; }

                if (level == null) { failed.Add($"{Path.GetFileName(file)}: vacío"); continue; }

                var report = LevelDifficultyEstimator.Analyze(level);
                if (!report.valid)  { failed.Add($"{level.id:000}: {report.error}"); continue; }
                if (report.bubbles == 0) { failed.Add($"{level.id:000}: sin burbujas"); continue; }

                var stars = LevelDifficultyEstimator.SuggestStars(report);

                if (Same(level.star_thresholds, stars)) continue;

                level.star_thresholds = new List<int> { stars[0], stars[1], stars[2] };
                File.WriteAllText(file, JsonUtility.ToJson(level, true));
                changed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();

        string summary = $"{changed} niveles actualizados de {files.Length}.";
        if (failed.Count > 0)
        {
            summary += $"\n\n{failed.Count} sin tocar:\n" + string.Join("\n", failed.Take(10));
            if (failed.Count > 10) summary += $"\n… y {failed.Count - 10} más (ver la consola).";
            Debug.LogWarning("[Umbrales de estrellas] Sin recalcular:\n" + string.Join("\n", failed));
        }

        EditorUtility.DisplayDialog("Umbrales de estrellas", summary, "Bien");
    }

    static bool Same(List<int> current, int[] next) =>
        current != null && current.Count >= 3 &&
        current[0] == next[0] && current[1] == next[1] && current[2] == next[2];
}
