using UnityEngine;

// Los boosters que el jugador llevó a este nivel, en el HUD (GDD §3.3).
//
// No instancia nada: los botones viven puestos en la escena, uno por poder, y esto solo enciende
// los que vienen equipados. Así cada uno conserva su arte y su sitio en el Inspector, y la lista
// no tiene que saber nada de prefabs ni de orden.
//
// Y el encendido vive acá, en el padre, y no en cada botón: un objeto apagado no recibe eventos,
// así que uno que se apagara solo al empezar no podría volver a encenderse cuando el tutorial
// regale y equipe un booster a mitad del nivel.
public class BoosterHudList : MonoBehaviour
{
    [Tooltip("Los botones de esta lista. Vacío (lo normal): se recogen solos de los hijos, incluso los que estén apagados.")]
    [SerializeField] BoosterButton[] buttons;

    void Awake()
    {
        if (buttons == null || buttons.Length == 0)
            buttons = GetComponentsInChildren<BoosterButton>(includeInactive: true);

        if (buttons.Length == 0)
            Debug.LogWarning($"[BoosterHudList] '{name}' no tiene ningún BoosterButton colgando: el HUD de boosters va a quedar siempre vacío.", this);
    }

    void OnEnable()
    {
        BoosterLoadout.OnChanged += Refresh;
        Refresh();
    }

    void OnDisable() => BoosterLoadout.OnChanged -= Refresh;

    void Refresh()
    {
        foreach (var button in buttons)
        {
            if (button == null) continue;

            // Por lo EQUIPADO y no por lo que queda en el inventario: gastar el último no puede
            // hacer desaparecer el ícono a mitad de partida. Sin existencias el botón se queda y
            // muestra su '+', que es el camino para reponerlo.
            bool show = BoosterLoadout.IsEquipped(button.Booster);

            if (button.gameObject.activeSelf != show) button.gameObject.SetActive(show);

            // Y se le pide el repintado SIEMPRE, no solo cuando acaba de encenderse: uno que ya
            // venía activo de la escena no recibe ningún OnEnable, así que se quedaría con el
            // número de relleno que tenga puesto el prefab hasta que el jugador lo tocara.
            if (show) button.Refresh();
        }
    }
}
