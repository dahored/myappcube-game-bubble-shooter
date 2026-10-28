using UnityEngine;

// Las criaturas con nombre (GDD §13.3): las que el jugador recuerda, las que tienen ficha de
// bestiario y las que se quedan a vivir en el santuario.
//
// Estar EN este archivo es lo que las hace criaturas. No hay un campo "es criatura" que marcar, y
// por eso no se puede olvidar ni contradecir: de acá salen la gelatina —un cofre no respira,
// ver BubbleView— y la respuesta que el santuario va a hacer sobre a quién montarle ficha.
//
// Lo que no es criatura va en ObjectCatalog. Los ids son únicos ENTRE LOS DOS, porque quien los
// pide los escribe a secas; quien contesta por los dos es Rescuable.
[CreateAssetMenu(fileName = "CreatureCatalog", menuName = "Coralia/Creature Catalog")]
public class CreatureCatalog : RescuableCatalog
{
    // Dónde se busca dentro de Resources. Si se renombra el asset, el catálogo deja de
    // encontrarse y todo cae al arte genérico.
    public const string RESOURCE_PATH = "CreatureCatalog";

    // Se cachea el resultado INCLUIDO el fallo, igual que el catálogo de poderes: un nivel sin
    // catálogo reintentaría un Resources.Load por cada criatura y por cada refresco del hub.
    static CreatureCatalog _instance;
    static bool            _looked;

    public static CreatureCatalog Instance
    {
        get
        {
            if (_looked) return _instance;

            _looked   = true;
            _instance = Resources.Load<CreatureCatalog>(RESOURCE_PATH);

            if (_instance == null)
                Debug.LogWarning($"[CreatureCatalog] No se encontró 'Resources/{RESOURCE_PATH}': los niveles de rescate van a salir sin arte.");

            return _instance;
        }
    }
}
