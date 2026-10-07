using TMPro;
using UnityEngine;
using UnityEngine.UI;

// El ícono de un booster en el HUD de gameplay (GDD §3.3). Tocarlo carga el poder en el cañón;
// tocarlo de nuevo lo descarga.
//
// Cargar y descargar son gratis — el inventario se gasta recién al disparar (CannonController.Fire).
// Por eso este componente nunca toca SaveManager para descontar: solo lee para mostrar cuántos
// quedan. Un ícono que descuenta al tocarse cobraría por equivocarse de botón.
public class BoosterButton : MonoBehaviour
{
    [Tooltip("Qué poder carga este ícono. Un botón por booster.")]
    [SerializeField] Booster booster = Booster.Bomb;

    // Qué poder muestra, para que la lista del HUD pueda preguntárselo sin duplicar el dato en
    // su propio Inspector.
    public Booster Booster => booster;

    [SerializeField] CannonController cannon;
    [SerializeField] Button           button;

    [Tooltip("El ícono en sí. El sprite lo pone el catálogo (Resources/BoosterCatalog) según qué booster sea: lo que haya puesto a mano acá solo se ve si el catálogo no lo tiene.\n\nSe tiñe con 'Loaded Tint' mientras el poder está cargado en el cañón. El tinte es el recurso pobre para cuando no hay un 'Loaded State' con arte propio que encender.")]
    [SerializeField] Image icon;

    [Tooltip("Lo que se enciende mientras el poder está CARGADO en el cañón, y se apaga al descargarlo o gastarlo. Para el mismo recurso que usa el BoosterItem del panel de inicio para 'equipado': arrastra acá su BGCheck y su BoosterIconCheck.\n\nEs una lista porque ese estado suele ser más de un objeto (un fondo distinto y una marca encima), y pedir que estén bajo un mismo padre obligaría a reordenar el prefab solo para esto.")]
    [SerializeField] GameObject[] loadedState;

    [Tooltip("Cuántos quedan. Opcional — sin esto funciona igual, solo no se ve el número.")]
    [SerializeField] TMP_Text countText;

    [Tooltip("La chapa que enmarca el número, si el número no va suelto. Se apaga entera cuando no queda ninguno; sin esto se apaga solo el texto y la chapa se queda vacía en pantalla.")]
    [SerializeField] GameObject countBadge;

    [Tooltip("Lo que se muestra cuando no queda ninguno (el '+' para comprar).")]
    [SerializeField] GameObject emptyBadge;

    [Tooltip("El modal de compra. Sin esto, tocar un booster que está en cero no hace nada.\n\nSi el '+' tiene su propio Button, quítaselo o nunca se llega acá: se come el toque antes de que llegue a este botón.")]
    [SerializeField] RefillBoosterPanel refillPanel;

    [SerializeField] Color loadedTint = new(1f, 0.85f, 0.35f, 1f);
    [SerializeField] Color idleTint   = Color.white;

    void Awake()
    {
        if (cannon == null) Debug.LogWarning($"[BoosterButton] Falta asignar 'cannon' en {name} — el ícono no va a poder cargar nada.", this);
        if (button == null) button = GetComponent<Button>();
        if (button == null) Debug.LogWarning($"[BoosterButton] Falta asignar 'button' en {name} y el objeto no tiene uno — el ícono no responde al toque.", this);
        if (button != null) button.onClick.AddListener(OnPressed);
        if (cannon != null) cannon.OnBoosterChanged += Refresh;

        // Dos fuentes distintas: el cañón avisa de cargar y descargar (cambia el tinte, no el
        // inventario) y SaveManager de ganar o gastar uno (cambia el número). Un regalo al abrir
        // el nivel llega por acá, cuando este botón ya hizo su OnEnable.
        SaveManager.OnBoostersChanged += Refresh;
    }

    void OnEnable() => Refresh();

    void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnPressed);
        if (cannon != null) cannon.OnBoosterChanged -= Refresh;

        // Obligatorio: el evento es ESTÁTICO, así que un botón destruido al cambiar de escena
        // seguiría enganchado y se le llamaría Refresh sobre un objeto que ya no existe.
        SaveManager.OnBoostersChanged -= Refresh;
    }

    void OnPressed()
    {
        if (cannon == null) return;

        if (cannon.LoadedBooster == booster) { cannon.UnloadBooster(); return; }

        // Sin existencias el toque es para comprar, no para cargar. Va antes de LoadBooster
        // porque este devolvería false en silencio y el jugador se quedaría sin saber qué pasó.
        if (SaveManager.BoosterCount(booster) <= 0)
        {
            if (refillPanel != null) refillPanel.Show(booster);
            return;
        }

        cannon.LoadBooster(booster);
        // Sin refresco explícito en el camino del "no se pudo": LoadBooster avisa por evento
        // cuando sí cambia algo, y si devolvió false no cambió nada que mostrar.
    }

    // Público porque la lista del HUD tiene que poder pedirlo: ella decide qué botones se ven, y
    // encenderlos no basta para que se pinten — un botón que ya estaba activo nunca recibe un
    // OnEnable nuevo, y se quedaba con el número de relleno del prefab hasta que lo tocaran.
    public void Refresh()
    {
        int  count  = SaveManager.BoosterCount(booster);
        bool loaded = cannon != null && cannon.LoadedBooster == booster;

        if (icon != null)
        {
            icon.color = loaded ? loadedTint : idleTint;

            // Solo si el catálogo lo tiene: así un booster sin arte todavía se queda con el
            // sprite del prefab en vez de aparecer en blanco.
            var art = BoosterRules.IconFor(booster);
            if (art != null) icon.sprite = art;
        }

        if (loadedState != null)
            foreach (var go in loadedState)
                if (go != null && go.activeSelf != loaded) go.SetActive(loaded);

        var countObject = countBadge != null ? countBadge : countText != null ? countText.gameObject : null;
        if (countObject != null) countObject.SetActive(count > 0);
        if (countText   != null) countText.text = count.ToString();

        if (emptyBadge != null) emptyBadge.SetActive(count <= 0 && !loaded);

        // Lo ÚLTIMO, y no lo primero como estaba: tocar 'interactable' entra en Selectable, que es
        // código del paquete uGUI y el que nos tiró el IndexOutOfRangeException. Si eso revienta,
        // que reviente con el ícono ya pintado — lo que el jugador ve no puede depender de que una
        // llamada ajena no falle.
        //
        // Siempre activo: con uno cargado es la única forma de descargarlo, y sin existencias es
        // la forma de comprar más. Apagarlo en cero dejaría el '+' visible y muerto.
        if (button != null) button.interactable = true;
    }
}
