using System.Collections.Generic;
using UnityEngine;

// El hub de objetivos del HUD: hasta tres huecos con lo que hay que conseguir en este nivel y
// cuánto falta de cada cosa (GDD §2.2).
//
// Los huecos VIVEN EN LA ESCENA, uno por sitio, y acá solo se encienden los que hagan falta. No
// se instancian: así cada uno conserva su tamaño y su sitio en el Inspector, y montar el HUD no
// depende de que este script adivine el layout. Mismo criterio que los botones de poder.
//
// Y no sabe de tipos de objetivo. Quien lleva la partida le pasa una lista de "icono + cuántos" y
// después le va diciendo cuántos quedan. Si mañana entra "conducir al fondo", este archivo no se
// entera — que es justo lo que evita que crezca un caso por tipo.
public class ObjectiveHub : MonoBehaviour
{
    [Tooltip("Los huecos, en el orden en que se ven. Vacío (lo normal): se recogen solos de los hijos, incluso los apagados.")]
    [SerializeField] ObjectiveItemView[] items;

    [Tooltip("Marcado: el hub entero se esconde cuando el nivel no tiene nada que contar. Desmarcado se queda vacío, que deja un hueco raro en el HUD.")]
    [SerializeField] bool hideWhenEmpty = true;

    [Tooltip("Lo que suena cuando algo LLEGA a un hueco: el golpe de recibirlo, a la vez que el hueco acusa el golpe.\n\nVa acá y no en el catálogo de criaturas porque no es el sonido de ESO que llega, sino el del hub recibiéndolo: es el mismo tanto si lo que sube es un caracol como si es una perla. Lo que suena al salir de la burbuja sí es de cada criatura, y ese está en el CreatureCatalog.\n\nOpcional: vacío no suena nada al llegar.")]
    [SerializeField] AudioClip landClip;

    int _shown;

    void Awake()
    {
        if (items == null || items.Length == 0)
            items = GetComponentsInChildren<ObjectiveItemView>(includeInactive: true);

        if (items.Length == 0)
            Debug.LogWarning($"[ObjectiveHub] '{name}' no tiene ningún ObjectiveItemView colgando: el objetivo del nivel no se va a ver.", this);
    }

    // Lo que hay que conseguir, de una a tres cosas. De más se descartan con aviso: el cuarto no
    // cabe, y callarlo dejaría un objetivo que el jugador no puede ver pero sí tiene que cumplir.
    public void Show(IReadOnlyList<(Sprite icon, int total)> goals)
    {
        _shown = goals != null ? Mathf.Min(goals.Count, items.Length) : 0;

        if (goals != null && goals.Count > items.Length)
            Debug.LogWarning($"[ObjectiveHub] El nivel pide {goals.Count} objetivos y el hub tiene {items.Length} huecos: los de más no se ven.", this);

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;

            bool on = i < _shown;

            if (items[i].gameObject.activeSelf != on) items[i].gameObject.SetActive(on);

            if (on) items[i].Show(goals[i].icon, goals[i].total);
        }

        if (hideWhenEmpty && gameObject.activeSelf != (_shown > 0)) gameObject.SetActive(_shown > 0);
    }

    // Dónde está ese hueco, para que lo rescatado sepa a dónde volar.
    public RectTransform SlotAt(int index) =>
        index >= 0 && index < _shown && items[index] != null ? (RectTransform)items[index].transform : null;

    public void Bump(int index)
    {
        if (index < 0 || index >= _shown || items[index] == null) return;

        items[index].Bump();

        AudioManager.Instance?.PlaySfx(landClip);
    }

    public void SetRemaining(int index, int remaining)
    {
        if (index < 0 || index >= _shown || items[index] == null) return;

        items[index].SetRemaining(remaining);
    }
}
