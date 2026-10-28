using UnityEngine;

// Lo que se rescata y NO es una criatura: perlas, caracolas, cofres, llaves. Cuenta para el
// objetivo del nivel igual que un bicho, pero no tiene nombre propio, no respira dentro de la
// burbuja y no se queda a vivir en el santuario.
//
// Aparte de CreatureCatalog y no una lista dentro suyo porque son dos trabajos que no coinciden:
// las doce criaturas son personajes con arte, voz y ficha, y esto son cacharros del fondo del
// mar. Juntos en un asset, tocar uno obligaba a abrir el otro.
//
// Los ids son únicos ENTRE LOS DOS catálogos: quien los pide los escribe a secas sin decir de
// cuál salen, y quien contesta por los dos es Rescuable.
[CreateAssetMenu(fileName = "ObjectCatalog", menuName = "Coralia/Object Catalog")]
public class ObjectCatalog : RescuableCatalog
{
    public const string RESOURCE_PATH = "ObjectCatalog";

    static ObjectCatalog _instance;
    static bool          _looked;

    public static ObjectCatalog Instance
    {
        get
        {
            if (_looked) return _instance;

            _looked   = true;
            _instance = Resources.Load<ObjectCatalog>(RESOURCE_PATH);

            if (_instance == null)
                Debug.LogWarning($"[ObjectCatalog] No se encontró 'Resources/{RESOURCE_PATH}': los objetos rescatables van a salir sin arte.");

            return _instance;
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        // Los ids tienen que ser únicos entre LOS DOS catálogos, y separados ya no hay un solo
        // sitio desde el que mirarlo. Lo comprueba este y no el de criaturas porque el de
        // criaturas no debería saber que existe este — mismo criterio que RescueBubbleCatalog,
        // que carga el inventario para comprobar sus 'holds'.
        var creatures = CreatureCatalog.Instance;

        if (creatures == null) return;

        foreach (string id in Ids)
            if (creatures.Has(id))
                Debug.LogWarning($"[ObjectCatalog] '{id}' también está en el CreatureCatalog. Quien lo pida va a recibir la criatura, porque se mira primero.", this);
    }
#endif
}
