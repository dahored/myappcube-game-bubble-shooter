using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Un booster en la pantalla previa al nivel (StartGamePanel, GDD §3.3): se toca para equiparlo
// o quitarlo, hasta el máximo del loadout.
//
// Equipar no cuesta nada — el inventario se gasta recién al disparar en el nivel. Por eso este
// componente nunca descuenta: solo lee cuántos quedan y alterna la selección.
//
// Tiene tres estados excluyentes, y cuál manda se decide acá y no en quien lo monta:
//   bloqueado  → el poder todavía no existe para el jugador: candado y nada más.
//   sin ninguno → el '+' para conseguirlo.
//   con stock   → el número, y se puede equipar.
public class BoosterItemView : MonoBehaviour
{
    [Tooltip("Qué poder representa, cuando nadie lo asigna por código. En la pantalla previa lo pisa el nivel (LevelData.allowed_boosters), así que acá se deja en 'None'.\n\n'None' es lo que marca el ítem como BLOQUEADO: un hueco que no apunta a ningún poder no tiene nada que equipar.")]
    [SerializeField] BubbleSpecial booster = BubbleSpecial.None;

    [SerializeField] Button button;

    [Header("Estados")]
    [Tooltip("El ícono del poder. El sprite lo pone el catálogo (Resources/BoosterCatalog) según qué booster sea: lo que haya puesto a mano acá solo se ve si el catálogo no lo tiene.\n\nSe apaga mientras el ítem está bloqueado, para que se vea el candado en su lugar.")]
    [SerializeField] Image iconObject;

    [Tooltip("El candado. Se enciende solo cuando 'Booster' es None.")]
    [SerializeField] GameObject lockObject;

    [Tooltip("Lo que se enciende mientras el poder está EQUIPADO: el BGCheck y el BoosterIconCheck.\n\nEs una lista porque ese estado suele ser más de un objeto, y pedir que estén bajo un mismo padre obligaría a reordenar el prefab solo para esto.")]
    [SerializeField] GameObject[] equippedState;

    [Header("Cantidad")]
    [Tooltip("La chapa que enmarca el número (BoosterTotal). Se apaga entera cuando no queda ninguno; asignando solo el texto, la chapa se quedaría vacía en pantalla.")]
    [SerializeField] GameObject countBadge;
    [SerializeField] TMP_Text   countText;

    [Tooltip("El '+' para conseguir más. Se muestra solo cuando el inventario está en cero y el poder ya está desbloqueado.")]
    [SerializeField] GameObject addBadge;

    [Tooltip("El modal de compra. Sin esto, tocar un booster que está en cero no hace nada.\n\nSi el '+' tiene su propio Button, quítaselo o nunca se llega acá: se come el toque antes de que llegue a este ítem.")]
    [SerializeField] RefillBoosterPanel refillPanel;

    bool Locked => booster == BubbleSpecial.None;

    void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button == null) Debug.LogWarning($"[BoosterItemView] Falta asignar 'Button' en '{name}' y el objeto no tiene uno — el ítem no responde al toque.", this);
        else button.onClick.AddListener(OnPressed);

        SaveManager.OnBoostersChanged += Refresh;
        BoosterLoadout.OnChanged      += Refresh;
    }

    void OnEnable() => Refresh();

    // Qué poder muestra este hueco. Lo decide el nivel, no el prefab: la pantalla previa reparte
    // acá el trío de LevelData.allowed_boosters, y el mismo ítem sirve para cualquiera.
    public void Bind(BubbleSpecial value)
    {
        booster = value;
        Refresh();
    }

    void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnPressed);

        // Obligatorios: los dos eventos son ESTÁTICOS, así que un ítem destruido al cambiar de
        // escena seguiría enganchado y se le llamaría Refresh sobre un objeto que ya no existe.
        SaveManager.OnBoostersChanged -= Refresh;
        BoosterLoadout.OnChanged      -= Refresh;
    }

    void OnPressed()
    {
        if (Locked) return;

        // Sin ninguno no hay nada que equipar: ahí el toque es para conseguir más. Todo el ítem
        // abre el panel, no solo el '+' — el '+' es la señal, pero el objetivo de toque útil es
        // la pieza entera.
        if (SaveManager.BoosterCount(booster) <= 0)
        {
            if (refillPanel != null) refillPanel.Show(booster);
            return;
        }

        BoosterLoadout.Toggle(booster);
        // Sin refresco explícito: Toggle avisa por evento cuando algo cambió, y cuando devuelve
        // false sin cambiar nada —el loadout ya está lleno— no hay nada nuevo que mostrar.
    }

    void Refresh()
    {
        int  count    = Locked ? 0 : SaveManager.BoosterCount(booster);
        bool equipped = BoosterLoadout.IsEquipped(booster);

        Show(lockObject, Locked);
        Show(iconObject != null ? iconObject.gameObject : null, !Locked);

        // Solo si el catálogo lo tiene: así un booster sin arte todavía se queda con el sprite
        // del prefab en vez de aparecer en blanco.
        var art = BoosterRules.IconFor(booster);
        if (iconObject != null && art != null) iconObject.sprite = art;

        if (equippedState != null)
            foreach (var go in equippedState)
                Show(go, equipped);

        // El número se va al equipar: el check ocupa su sitio y las dos marcas a la vez compiten
        // por decir lo mismo. Cuántos quedan importa al elegir, no después de elegido.
        Show(countBadge != null ? countBadge : countText != null ? countText.gameObject : null, count > 0 && !equipped);
        if (countText != null) countText.text = count.ToString();

        Show(addBadge, !Locked && count <= 0);

        // Bloqueado se apaga; sin stock se deja vivo, porque ahí el toque es para conseguir más
        // en cuanto exista la tienda.
        if (button != null) button.interactable = !Locked;
    }

    static void Show(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on) go.SetActive(on);
    }
}
