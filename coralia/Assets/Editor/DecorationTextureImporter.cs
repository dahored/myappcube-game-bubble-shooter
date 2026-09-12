using UnityEditor;
using UnityEngine;

// Configura solo las texturas de decoración del mapa al importarlas.
//
// Estas necesitan ajustes distintos a los de la UI: viven en un mundo 3D y se alejan hacia el
// horizonte, así que llevan mipmaps (sin ellos titilan de lejos) y 'Preserve Coverage' (sin eso el
// alfa se va promediando en cada nivel y la pieza se ve translúcida a la distancia). La UI, que se
// dibuja siempre al mismo tamaño, no quiere nada de eso.
//
// Acotado a la carpeta de decoraciones a propósito: no toca los sprites de UI ni ningún otro.
public class DecorationTextureImporter : AssetPostprocessor
{
    const string FOLDER   = "Assets/Sprites/Decorations/";
    const int    MAX_SIZE = 512;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(FOLDER)) return;

        var importer = (TextureImporter)assetImporter;

        // Solo en la PRIMERA importación. Si se aplicara en cada reimportación, pisaría los
        // ajustes hechos a mano — sobre todo el pivot, que en piezas irregulares como un arco o un
        // barco hay que correrlo al punto de apoyo real y no se puede adivinar por código.
        if (!importer.importSettingsMissing) return;

        Apply(importer);
    }

    static void Apply(TextureImporter importer)
    {
        importer.textureType          = TextureImporterType.Sprite;
        importer.alphaIsTransparency  = true;   // evita halos oscuros en los bordes
        importer.mipmapEnabled        = true;   // sin esto, las piezas lejanas titilan al scrollear
        importer.mipMapsPreserveCoverage = true; // sin esto, se ven translúcidas a la distancia
        importer.alphaTestReferenceValue = 0.5f;
        importer.wrapMode             = TextureWrapMode.Clamp;
        importer.maxTextureSize       = MAX_SIZE;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        // Malla ajustada al contorno: con 'Full Rect' cada pieza pinta su PNG cuadrado entero,
        // transparencia incluida, y eso en móvil se paga en fill rate.
        settings.spriteMeshType  = SpriteMeshType.Tight;
        // Apoyadas en el suelo. En las piezas irregulares hay que ajustarlo a mano después.
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        importer.SetTextureSettings(settings);
    }

    // Para las texturas que ya estaban importadas antes de existir este script. Es a pedido y no
    // automático, justamente para no pisar ajustes manuales sin que nadie lo haya pedido.
    [MenuItem("Coralia/Reaplicar ajustes a texturas de decoración")]
    static void ReapplyToExisting()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { FOLDER.TrimEnd('/') });
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[DecorationTextureImporter] No se encontraron texturas en {FOLDER}.");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Reaplicar ajustes de decoración",
                $"Se van a reconfigurar {guids.Length} texturas de {FOLDER}\n\n" +
                "Ojo: esto pisa el pivot de cada una y lo deja en Bottom Center. " +
                "Los pivots ajustados a mano hay que volver a ponerlos.",
                "Aplicar", "Cancelar"))
            return;

        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                Apply(importer);
                importer.SaveAndReimport();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[DecorationTextureImporter] {guids.Length} texturas reconfiguradas.");
    }
}
