using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// El panel que explica una mecánica la primera vez que el jugador se la encuentra (issue #58).
//
// Es UNO para todas y no uno por mecánica. Cada mecánica nueva —rescatar una criatura, romper
// una roca, un booster— necesita lo mismo: una imagen, un texto y un botón. Construir un panel
// por cada una significaría repetir la animación de apertura, el sonido, el i18n y la lógica de
// "solo una vez" tantas veces como mecánicas tenga el juego.
//
// El contenido vive acá, en la lista de entradas, y no en quien lo abre: así se puede revisar
// todo lo que el juego explica mirando un solo sitio, y el que dispara el tutorial solo tiene
// que nombrar la mecánica.
//
// No se parece a ShootHintView (issue #7) a propósito: aquella es una mano fantasma DENTRO del
// gameplay, sin cortar el juego, y sirve para un gesto que se entiende haciéndolo. Esto es un
// modal, y sirve para una regla que hay que leer.
public class TutorialPanel : UIPanel
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Cómo lo nombra quien lo dispara, y con lo que se recuerda que ya se vio. No se muestra nunca: elegir algo estable, porque cambiarlo hace que el tutorial vuelva a salir.")]
        public string id;

        [Tooltip("Clave de translations.csv para el título.")]
        public string titleKey;

        [Tooltip("Clave de translations.csv para la explicación.")]
        public string bodyKey;

        [Tooltip("La imagen fija que acompaña al texto. Opcional si se usa una animación.")]
        public Sprite image;

        [Tooltip("Una demostración en movimiento en lugar de la imagen fija, para mecánicas que se entienden mejor viéndolas. Si está puesta, manda sobre la imagen.\n\nVale tanto un PREFAB —se instancia al mostrarlo y se destruye al cerrar— como un objeto que ya esté colgando del panel en la escena, que simplemente se enciende y se apaga. El panel distingue cuál es solo.")]
        public GameObject animation;

        [Tooltip("Apagado, ESTE tutorial sale todas las veces. Solo para probarlo sin tener que borrar el progreso.\n\nEl interruptor de arriba manda sobre todos: con él apagado, este también sale siempre aunque esté marcado.")]
        public bool once = true;
    }

    [Tooltip("Prendido (normal): cada tutorial sale una sola vez en la vida, según su propio 'Once'.\n\nApagado: NINGUNO se recuerda y todos vuelven a salir cada vez que toque. Es el interruptor para probar sin ir marcando entrada por entrada ni borrar el progreso del jugador — que es lo que hace el menú de depuración, y no es lo mismo: esto no toca lo guardado, solo deja de consultarlo.")]
    [SerializeField] bool rememberSeen = true;

    [Tooltip("Todo lo que el juego explica, en un solo sitio. Quien dispara un tutorial solo nombra el id.")]
    [SerializeField] Entry[] entries;

    [Header("Contenido")]
    [SerializeField] TMP_Text  titleText;
    [SerializeField] TMP_Text  bodyText;
    [SerializeField] Image     imageSlot;
    [Tooltip("Dónde se instancia la demostración de la entrada CUANDO es un prefab del proyecto. Si la demostración ya vive colgada del panel en la escena, esto no hace falta: se queda donde esté y el panel solo la enciende.")]
    [SerializeField] Transform animationSlot;
    [SerializeField] Button    okButton;
    [Tooltip("Texto del botón de cierre. Opcional: si se deja vacío, el botón se queda con lo que diga su propio LocalizedText.")]
    [SerializeField] TMP_Text  okText;

    // Lo que se está mostrando y lo que espera turno. Dos mecánicas pueden aparecer en el mismo
    // nivel, y sin cola la segunda llamada pisaría a la primera: el jugador vería el tutorial
    // equivocado y el otro quedaría marcado como visto sin haberse leído.
    readonly Queue<(Entry entry, Action onDismissed)> _pending = new();

    Entry      _showing;
    Action     _onDismissed;
    GameObject _spawnedAnimation;   // solo si vino de un prefab: hay que destruirla
    GameObject _activeAnimation;    // la que esté encendida, venga de donde venga

    protected override void Awake()
    {
        base.Awake();

        if (okButton) okButton.onClick.AddListener(Close);
        else Debug.LogWarning($"[TutorialPanel] '{name}' no tiene botón de cierre asignado: el tutorial se abriría sin forma de cerrarlo.", this);

        OnClosed += Dismissed;
    }

    void OnDestroy() => OnClosed -= Dismissed;

    // Muestra el tutorial de esa mecánica, si toca.
    //
    // Devuelve si se va a mostrar, para que quien lo pida pueda decidir sin suscribirse a nada:
    // casi siempre es un "¿paro el nivel o sigo?".
    public bool Show(string id, Action onDismissed = null)
    {
        var entry = Find(id);
        if (entry == null) return false;

        if (Remembers(entry) && SaveManager.HasSeenTutorial(entry.id)) return false;

        // Se marca al PEDIRLO y no al cerrarlo: si el jugador sale del nivel con el panel abierto
        // no se vuelve a marcar nunca, y el tutorial saldría en bucle cada vez que entre.
        if (Remembers(entry)) SaveManager.MarkTutorialSeen(entry.id);

        _pending.Enqueue((entry, onDismissed));

        if (_showing == null) Next();

        return true;
    }

    // Si esa mecánica todavía tiene algo que explicar. Sirve para preguntarlo sin abrir nada —
    // por ejemplo, para decidir si hace falta esperar antes de arrancar una animación.
    public bool Pending(string id)
    {
        var entry = Find(id);

        return entry != null && (!Remembers(entry) || !SaveManager.HasSeenTutorial(entry.id));
    }

    // Los dos interruptores en uno. El global manda: apagarlo devuelve todos los tutoriales de
    // golpe sin tocar las entradas una por una, que es lo que hace falta mientras se prueba.
    bool Remembers(Entry entry) => rememberSeen && entry.once;

    Entry Find(string id)
    {
        if (string.IsNullOrEmpty(id) || entries == null) return null;

        foreach (var entry in entries)
            if (entry != null && entry.id == id) return entry;

        Debug.LogWarning($"[TutorialPanel] Nadie explica '{id}': no está en la lista de entradas de '{name}'.", this);

        return null;
    }

    void Next()
    {
        if (_pending.Count == 0)
        {
            _showing = null;
            return;
        }

        (_showing, _onDismissed) = _pending.Dequeue();

        if (titleText) titleText.text = LocaleManager.Get(_showing.titleKey);
        if (bodyText)  bodyText.text  = LocaleManager.Get(_showing.bodyKey);
        if (okText)    okText.text    = LocaleManager.Get("ui.tutorial.ok");

        ShowVisual(_showing);

        Open();
    }

    void ShowVisual(Entry entry)
    {
        ClearVisual();

        if (entry.animation)
        {
            // El slot solo hace falta para instanciar: una demostración que ya está en la escena
            // tiene su sitio desde que se montó. Pedirlo igual dejaba el panel en blanco con todo
            // bien puesto, que es el peor tipo de fallo — nada señala qué falta.
            if (Mounted(entry.animation))
            {
                _activeAnimation = Switch(entry.animation);
            }
            else if (animationSlot != null)
            {
                _activeAnimation = _spawnedAnimation = Instantiate(entry.animation, animationSlot);
            }
            else
            {
                Debug.LogWarning($"[TutorialPanel] '{entry.id}' trae un prefab de demostración pero falta asignar 'Animation Slot': no hay dónde instanciarlo.", this);
            }

            if (imageSlot) imageSlot.enabled = false;
            return;
        }

        if (imageSlot)
        {
            imageSlot.enabled = entry.image != null;
            imageSlot.sprite  = entry.image;
        }
    }

    // Si esa demostración ya vive en la escena en vez de ser un prefab del proyecto. Un objeto
    // de escena pertenece a una escena cargada y uno del proyecto no, así que la pregunta se
    // responde sola y no hace falta un check en el Inspector que haya que acordarse de marcar.
    static bool Mounted(GameObject animation) => animation.scene.IsValid();

    // Las montadas en la escena solo se encienden y se apagan. Se apagan TODAS antes, no solo la
    // anterior: si alguna quedó visible de cuando se estaba montando el panel, se verían dos
    // demostraciones a la vez y ninguna sería la que toca.
    GameObject Switch(GameObject wanted)
    {
        if (entries != null)
            foreach (var other in entries)
                if (other?.animation != null && Mounted(other.animation) && other.animation != wanted)
                    other.animation.SetActive(false);

        wanted.SetActive(true);

        return wanted;
    }

    void ClearVisual()
    {
        // Una montada en la escena no se destruye: se apaga, y sigue ahí para la próxima vez. La
        // instanciada sí, y por eso se la excluye: Destroy tarda hasta el final del frame, así
        // que apagarla antes sería tocar algo que ya está en camino a desaparecer.
        if (_activeAnimation && _activeAnimation != _spawnedAnimation) _activeAnimation.SetActive(false);

        _activeAnimation = null;

        if (_spawnedAnimation)
        {
            Destroy(_spawnedAnimation);
            _spawnedAnimation = null;
        }
    }

    void Dismissed()
    {
        var callback = _onDismissed;

        _showing     = null;
        _onDismissed = null;

        ClearVisual();

        callback?.Invoke();

        // Lo siguiente de la cola se abre DESPUÉS de avisar: quien esperaba este tutorial ya pudo
        // seguir con lo suyo, y el panel se vuelve a abrir encima sin que nadie quede colgado.
        Next();
    }
}
