using System.Collections.Generic;
using UnityEngine;

// Catálogo de decoraciones del mapa: traduce el id que escribe el JSON del capítulo
// ("coral_red") al asset de Unity, y guarda los valores por defecto de cada elemento.
//
// Existe porque un JSON no puede apuntar a un asset de Unity. Además concentra acá el tamaño y el
// comportamiento de cada decoración, así el JSON del capítulo queda corto: solo dice QUÉ va y
// DÓNDE, no cómo se ve.
//
// Y es la pieza que le va a dar al futuro editor de capítulos su paleta de elementos disponibles.
[CreateAssetMenu(fileName = "DecorationCatalog", menuName = "Coralia/Decoration Catalog")]
public class DecorationCatalog : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("Cómo se la nombra en el JSON del capítulo. Sin espacios.")]
        public string id;

        [Tooltip("Lo más simple: un sprite suelto. Se le arma un SpriteRenderer solo.")]
        public Sprite sprite;

        [Tooltip("Alternativa: un prefab ya armado (con su animación de balanceo, varias piezas, etc.). Si está, se usa este en vez del sprite.")]
        public GameObject prefab;

        [Tooltip("Alto del elemento en unidades de MUNDO, mismo criterio que 'Node Size'.")]
        public float height = 2f;

        [Tooltip("Variación de tamaño al azar, como fracción. 0.2 = hasta 20% más grande o más chico.")]
        [Range(0f, 0.6f)]
        public float sizeVariation = 0.15f;
    }

    [SerializeField] Entry[] entries;

    Dictionary<string, Entry> _byId;

    public Entry Find(string id)
    {
        if (_byId == null)
        {
            _byId = new Dictionary<string, Entry>();
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                    _byId[entry.id] = entry;
                }
            }
        }

        return _byId.TryGetValue(id ?? string.Empty, out var found) ? found : null;
    }
}
