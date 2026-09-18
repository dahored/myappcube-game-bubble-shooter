using UnityEngine;

// Los números que definen el mundo: la forma del trazado y cuántos niveles tiene. Todo lo que se
// apoya en el mapa — el suelo, el camino, los nodos, las decoraciones — los lee de acá.
//
// Existe porque si cada componente guardara su copia, cambiar el zigzag obligaría a cambiarlo en
// cuatro lados y el día que se olvide uno el suelo quedaría corto o los adornos flotando.
[ExecuteAlways]
public class WorldMapDefinition : MonoBehaviour
{
    [SerializeField] WorldMapPath.Layout layout = WorldMapPath.Layout.Default;

    [Tooltip("Cuántos niveles dibujar. En 0 usa los que haya en el índice de niveles.")]
    [SerializeField] int levelsOverride;

    public WorldMapPath.Layout Layout => layout;

    // Del índice real, salvo que se pise a mano para probar. Así la escena no depende de que
    // alguien mantenga un número sincronizado con la cantidad de archivos de nivel.
    public int Levels => levelsOverride > 0
        ? levelsOverride
        : Mathf.Max(1, LevelLoader.Entries.Count);

    // Cuánto mide el recorrido de punta a punta, en unidades de mundo.
    public float Length => Mathf.Max(0, Levels - 1) * layout.spacing;

    void OnValidate()
    {
        // Los hijos son los que arman las mallas, así que el cambio hay que reenviárselo: acá
        // solo cambiaron números.
        foreach (var rebuildable in GetComponentsInChildren<IWorldMapRebuildable>(true))
            rebuildable.Rebuild();
    }

    // Lo busca hacia arriba en la jerarquía: los componentes del mapa cuelgan todos del mismo
    // objeto raíz, así que no hace falta asignarlo a mano en cada uno.
    public static WorldMapDefinition For(Component component)
    {
        var found = component.GetComponentInParent<WorldMapDefinition>(true);
        if (found == null)
            Debug.LogWarning($"[{component.GetType().Name}] No hay ningún WorldMapDefinition " +
                             "en sus padres — el mapa no sabe qué forma tiene ni cuántos niveles hay.");
        return found;
    }
}
