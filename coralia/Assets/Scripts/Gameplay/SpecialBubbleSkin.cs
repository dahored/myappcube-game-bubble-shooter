using UnityEngine;
using UnityEngine.UI;

// El aspecto de una burbuja especial: dos capas encima de la burbuja normal.
//
//   back  — el aspecto del poder. Se mueve de una de dos formas, nunca de las dos: GIRA (un solo
//           dibujo dando vueltas) o CAMBIA de dibujo (varios frames alternándose). Girar y cambiar
//           a la vez se lee como un error, no como una animación.
//   glow  — el brillo esférico, quieto. Va adelante para que el giro no arrastre el reflejo:
//           un brillo que gira con la burbuja parece una calcomanía, uno fijo parece luz.
//
// Se monta por CÓDIGO sobre cualquier RectTransform porque la misma burbuja se dibuja en tres
// objetos distintos y sin parentesco: la del grid (BubbleView), la del cañón (una Image suelta)
// y la que va en vuelo (el prefab). Pedir que las tres lleven las capas armadas a mano en el
// editor es tres oportunidades de que una quede distinta.
//
// Las capas cubren al host, no lo apagan: el arte de la burbuja especial es opaco y del mismo
// tamaño, así que tapa lo que haya debajo. Así el host sigue mandando sobre su propia Image
// (el cañón, por ejemplo, la enciende y apaga durante el disparo) sin pelearse con esto.
public class SpecialBubbleSkin : MonoBehaviour
{
    Image   _back;
    Image   _glow;
    Graphic _host;   // la Image sobre la que se montaron las capas

    Sprite[] _frames;
    float    _spin;
    float    _fps;
    float    _frameTime;
    int      _frame;

    // Si el poder está puesto. Es distinto de que las capas se vean: una bomba cargada sigue
    // puesta mientras el cañón esconde su burbuja durante el vuelo. Sin separarlo, el seguimiento
    // del host de abajo volvía a encender lo que Clear() acababa de apagar, y la bomba quedaba
    // dibujada encima de la burbuja normal que había vuelto a la recámara.
    bool _on;

    // Enciende (o cambia) las capas sobre 'host'.
    //
    // Recibe el booster y no los sprites: el arte vive en BoosterCatalog, y los tres sitios que
    // dibujan una burbuja especial —el grid, la recámara del cañón y la que vuela— no tienen por
    // qué saber de dónde sale ni repetir la consulta cada uno a su manera.
    public static void Apply(Component host, BubbleSpecial booster)
    {
        if (host == null) return;

        var art = BoosterRules.ArtFor(booster);
        if (art == null || art.bubbleFrames == null || art.bubbleFrames.Length == 0) return;

        var skin = host.GetComponent<SpecialBubbleSkin>() ?? host.gameObject.AddComponent<SpecialBubbleSkin>();

        skin._frames = art.bubbleFrames;
        skin._spin   = art.spin;
        skin._fps    = art.framesPerSecond;
        skin._on     = true;

        skin._frame     = 0;
        skin._frameTime = 0f;

        skin._back = skin.Layer(skin._back, "SpecialBack", art.bubbleFrames[0]);
        skin._glow = skin.Layer(skin._glow, "SpecialGlow", BoosterRules.GlowFor(booster));

        // Se reinicia en cada Apply: una burbuja que vuelve a ser especial arranca derecha, no
        // en el ángulo donde quedó la anterior que usó este mismo objeto.
        if (skin._back) skin._back.transform.localRotation = Quaternion.identity;
    }

    public static void Clear(Component host)
    {
        var skin = host == null ? null : host.GetComponent<SpecialBubbleSkin>();
        if (skin == null) return;

        skin._on = false;

        if (skin._back) skin._back.gameObject.SetActive(false);
        if (skin._glow) skin._glow.gameObject.SetActive(false);
    }

    // Las capas acompañan el desvanecido de la burbuja (pop, caída). Sin esto la bomba se
    // apaga pero su giro queda flotando opaco hasta que el objeto se destruye.
    public static void SetAlpha(Component host, float alpha)
    {
        var skin = host == null ? null : host.GetComponent<SpecialBubbleSkin>();
        if (skin == null) return;

        Fade(skin._back, alpha);
        Fade(skin._glow, alpha);
    }

    static void Fade(Image image, float alpha)
    {
        if (image == null) return;
        var c = image.color;
        image.color = new Color(c.r, c.g, c.b, alpha);
    }

    // Se reusan las Image en vez de destruirlas y recrearlas: el cañón cambia de especial a
    // normal y de vuelta en cada disparo, y eso sería dos GameObject nuevos por turno.
    Image Layer(Image existing, string name, Sprite sprite)
    {
        if (sprite == null)
        {
            if (existing) existing.gameObject.SetActive(false);
            return existing;
        }

        if (existing == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image))
            {
                // Misma convención que el resto de los hijos generados del proyecto: sin esto
                // quedan serializados dentro del .unity y del .prefab.
                hideFlags = HideFlags.DontSave,
            };

            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;   // estirada al host: así sirve igual para la burbuja
            rt.anchorMax = Vector2.one;    // del grid (92px) que para el ícono del cañón
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            existing = go.GetComponent<Image>();
            existing.raycastTarget = false; // el tap para apuntar tiene que llegar a la burbuja de abajo
        }

        existing.sprite = sprite;
        existing.gameObject.SetActive(true);
        return existing;
    }

    void Awake() => _host = GetComponent<Graphic>();

    void Update()
    {
        // Las capas siguen al host. Si su Image se apaga, se apagan con ella.
        //
        // Hace falta porque son HIJAS suyas, y apagar un componente Image no apaga a sus hijos:
        // el cañón apaga la burbuja de la recámara mientras el disparo vuela —para que no se vea
        // la bola quieta y la que vuela al mismo tiempo— y sin esto la bomba se quedaba pintada
        // en la almeja durante todo el vuelo, como si no se hubiera movido de ahí.
        //
        // Va acá y no en cada sitio que apaga la Image porque esos sitios son varios (el disparo,
        // la aparición al abrir el nivel, el remate de victoria) y los que vengan no van a saber
        // que esto existe.
        bool visible = _on && (_host == null || _host.enabled);

        if (_back != null && _back.gameObject.activeSelf != visible) _back.gameObject.SetActive(visible);
        if (_glow != null && _glow.gameObject.activeSelf != visible) _glow.gameObject.SetActive(visible);

        if (!visible || _back == null) return;

        // Girar y pasar frames son excluyentes por diseño, pero no se fuerza acá: si alguien pone
        // las dos cosas en el catálogo, se ven las dos y se nota enseguida que está mal. Imponerlo
        // en código sería decidir en silencio cuál de las dos quiso.
        if (_spin != 0f) _back.transform.Rotate(0f, 0f, _spin * Time.deltaTime);

        if (_frames == null || _frames.Length < 2 || _fps <= 0f) return;

        _frameTime += Time.deltaTime;

        float step = 1f / _fps;
        if (_frameTime < step) return;

        // Se descuenta en vez de poner a cero: con un fps alto y un frame largo pueden tocar dos
        // saltos, y poniéndolo a cero la animación se arrastraría respecto al reloj.
        _frameTime -= step;
        _frame      = (_frame + 1) % _frames.Length;

        _back.sprite = _frames[_frame];
    }
}
