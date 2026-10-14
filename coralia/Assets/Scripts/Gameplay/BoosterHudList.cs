using UnityEngine;

// Los boosters de gameplay en el HUD (GDD §3.3).
//
// Están TODOS y siempre, se hayan equipado o no, y el nivel no decide cuáles — es la decisión
// lockeada del GDD §3.5: "3 de inicio equipados; los de gameplay, todos los desbloqueados". Los de
// inicio se pagan por partida y hay que elegir, pero uno de gameplay solo cuesta si se usa, así
// que esconderlo no protege de nada y sí quita las dos cosas que la barra tiene que hacer:
// enseñar que el poder existe y dar el camino para conseguirlo.
//
// El que todavía no está desbloqueado se queda igual, con su candado — de eso se encarga el
// propio botón, que es quien sabe repintarse.
//
// No instancia nada: los botones viven puestos en la escena, uno por poder, así cada uno conserva
// su arte y su sitio en el Inspector, y la lista no tiene que saber nada de prefabs ni de orden.
//
// Y el encendido vive acá, en el padre, y no en cada botón: un objeto apagado no recibe eventos,
// así que uno que se apagara solo no podría volver a encenderse.
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

    // Sin suscripción al loadout: lo que se ve ya no depende de lo que el jugador equipó. Lo que
    // sí cambia durante el nivel —cuántos quedan, y si un tutorial acaba de desbloquear uno— lo
    // escucha cada botón por su cuenta (SaveManager.OnBoostersChanged), que es quien se repinta.
    void OnEnable() => Refresh();

    void Refresh()
    {
        foreach (var button in buttons)
        {
            if (button == null) continue;

            // Lo único que decide es la familia, y la familia no cambia nunca durante un nivel:
            // los de inicio ya hicieron su trabajo al abrir, así que un ícono suyo en el HUD sería
            // un botón que no hace nada. Ni el inventario ni el desbloqueo entran en esta cuenta
            // —gastar el último no puede hacer desaparecer el ícono a mitad de partida, y un
            // poder por conocer tiene que verse para saber que existe—: los dos son cosa del
            // botón, que los dibuja como el '+' y como el candado.
            bool show = BoosterRules.IsInGame(button.Booster);

            if (button.gameObject.activeSelf != show) button.gameObject.SetActive(show);

            // Y se le pide el repintado SIEMPRE, no solo cuando acaba de encenderse: uno que ya
            // venía activo de la escena no recibe ningún OnEnable, así que se quedaría con el
            // número de relleno que tenga puesto el prefab hasta que el jugador lo tocara.
            if (show) button.Refresh();
        }
    }
}
