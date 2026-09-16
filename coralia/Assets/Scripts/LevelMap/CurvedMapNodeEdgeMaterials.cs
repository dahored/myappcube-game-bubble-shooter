using UnityEngine;

// Cambia el material de los cilindros 3D del nodo (el canto tipo moneda) según el estado del
// nivel — morado normal, dorado cuando se pasó al primer intento, etc.
//
// Va aparte de LevelNodeView a propósito: ese script maneja sprites de UI y además es compartido
// con el mapa real (LevelMap), así que no debe saber nada de cilindros. Este componente se le
// agrega solo a los prefabs del mapa curvo y lo llama WorldSphereNodePositioner justo después
// del Setup() del nodo.
public class CurvedMapNodeEdgeMaterials : MonoBehaviour
{
    [System.Serializable]
    public class Group
    {
        [Tooltip("Los cilindros que comparten este material (ej. los dos del nodo, o los de las estrellas).")]
        public Renderer[] renderers;
        [Tooltip("Bloqueado, y completado con reintentos.")]
        public Material normal;
        [Tooltip("Nivel disponible para jugar.")]
        public Material current;
        [Tooltip("Completado al primer intento.")]
        public Material gold;
    }

    [Tooltip("Un grupo por cada familia de material. Ej: uno para los cilindros del nodo y otro para los de las estrellas.")]
    [SerializeField] Group[] groups;

    public void ApplyState(NodeState state)
    {
        if (groups == null || groups.Length == 0)
        {
            Debug.LogWarning($"[CurvedMapNodeEdgeMaterials] '{name}' no tiene ningún grupo configurado — los cilindros no van a cambiar de material.", this);
            return;
        }

        foreach (var group in groups)
        {
            if (group?.renderers == null) continue;

            Material material = state switch
            {
                NodeState.Available        => group.current,
                NodeState.CompleteFirstTry => group.gold,
                _                          => group.normal,
            };
            if (!material) continue; // sin material para ese estado, se deja el que ya tenía

            // sharedMaterial y no material: 'material' clona el asset en cada nodo, dejando una
            // copia por nivel en memoria y rompiendo el batching. Acá solo se asigna uno que ya existe.
            foreach (var renderer in group.renderers)
                if (renderer) renderer.sharedMaterial = material;
        }
    }
}
