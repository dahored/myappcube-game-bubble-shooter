using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Utilidad de diagnóstico: Unity solo avisa "The referenced script (Unknown) on this
// Behaviour is missing!" en la consola, sin decir en qué GameObject — esto recorre la
// escena abierta y todos los prefabs del proyecto y loguea la ruta exacta de cada uno,
// usando el mismo chequeo que usa el Editor internamente (Component == null cuando el
// script no resuelve).
public static class FindMissingScripts
{
    [MenuItem("Tools/Coralia/Buscar scripts faltantes en la escena abierta")]
    static void FindInOpenScene()
    {
        int count = 0;
        var roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
            count += CheckRecursive(root.transform, null);

        if (count == 0) Debug.Log("[FindMissingScripts] No se encontraron scripts faltantes en la escena abierta.");
        else            Debug.Log($"[FindMissingScripts] {count} scripts faltantes en la escena abierta (ver warnings arriba).");
    }

    // Abre cada escena del proyecto en modo Additive (sin tocar la escena que ya tenés
    // abierta ni pedirte guardar), la revisa, y la cierra — así se puede correr aunque
    // tengas cambios sin guardar en la escena actual.
    [MenuItem("Tools/Coralia/Buscar scripts faltantes en TODAS las escenas del proyecto")]
    static void FindInAllScenes()
    {
        int totalCount = 0;
        string activeScenePath = EditorSceneManager.GetActiveScene().path;

        // Restringido a Assets/ — sin esto, FindAssets también trae escenas de sample
        // packages de solo lectura (ej. URP), que Unity se niega a abrir y tira un popup.
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);

            // La escena que ya estaba abierta no se reabre/cierra — Unity no deja cerrar
            // la última escena cargada, así que se revisa directo con la que ya está en
            // memoria en vez de duplicarla.
            if (path == activeScenePath)
            {
                int activeCount = 0;
                foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    activeCount += CheckRecursive(root.transform, path);
                totalCount += activeCount;
                if (activeCount > 0) Debug.LogWarning($"[FindMissingScripts] {activeCount} scripts faltantes en la escena: {path}");
                continue;
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
                count += CheckRecursive(root.transform, path);
            totalCount += count;

            if (count > 0) Debug.LogWarning($"[FindMissingScripts] {count} scripts faltantes en la escena: {path}");
            EditorSceneManager.CloseScene(scene, true);
        }

        if (totalCount == 0) Debug.Log("[FindMissingScripts] No se encontraron scripts faltantes en ninguna escena del proyecto.");
        else                 Debug.Log($"[FindMissingScripts] {totalCount} scripts faltantes en total (ver warnings arriba, con la escena de cada uno).");
    }

    [MenuItem("Tools/Coralia/Buscar scripts faltantes en todos los prefabs")]
    static void FindInAllPrefabs()
    {
        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            var path   = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            foreach (var c in prefab.GetComponentsInChildren<Component>(true))
            {
                if (c != null) continue;
                Debug.LogWarning($"[FindMissingScripts] Script faltante en prefab: {path}", prefab);
                count++;
            }
        }

        if (count == 0) Debug.Log("[FindMissingScripts] No se encontraron scripts faltantes en ningún prefab.");
        else            Debug.Log($"[FindMissingScripts] {count} scripts faltantes en prefabs (ver warnings arriba).");
    }

    static int CheckRecursive(Transform t, string scenePath)
    {
        int count = 0;
        foreach (var c in t.GetComponents<Component>())
        {
            if (c != null) continue;
            string where = scenePath != null ? $"{scenePath} -> {GetPath(t)}" : GetPath(t);
            Debug.LogWarning($"[FindMissingScripts] Script faltante en: {where}", t.gameObject);
            count++;
        }
        for (int i = 0; i < t.childCount; i++)
            count += CheckRecursive(t.GetChild(i), scenePath);
        return count;
    }

    static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
