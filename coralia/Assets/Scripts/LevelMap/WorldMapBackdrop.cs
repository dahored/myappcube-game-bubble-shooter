using UnityEngine;

// El fondo de lo que queda por encima del horizonte.
//
// El suelo del mundo termina en el horizonte y detrás no hay nada, así que ahí se veía el color
// de limpieza de la cámara: una banda oscura y plana en la mitad superior de la pantalla.
//
// Es un telón pegado a la cámara, no un objeto del mundo: no se mueve al scrollear ni se hunde
// con la curvatura, simplemente está siempre ahí atrás. Su tamaño sale del FOV y de la relación
// de pantalla en cada frame, así que acompaña solo lo que haga WorldMapCameraFit al pasar de
// teléfono a tablet, sin números que ajustar por dispositivo.
//
// Dibujarlo detrás del mapa NO es cuestión de distancia ni de sorting order: el suelo es opaco
// (cola Geometry 2000) y un SpriteRenderer normal va en la transparente 3000, o sea que se
// dibujaría ENCIMA por más lejos que estuviera. Por eso usa Mat_MapBackdrop, que está en la cola
// Background (1000) y no escribe profundidad: sale primero y todo lo demás lo tapa.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class WorldMapBackdrop : MonoBehaviour
{
    [Tooltip("La imagen del fondo. Vacío: no se dibuja nada y se ve el color de la cámara, como antes.")]
    [SerializeField] Sprite sprite;

    [Tooltip("Mat_MapBackdrop. Sin esto el fondo taparía el mapa en vez de quedar detrás — ver el comentario de arriba.")]
    [SerializeField] Material material;

    [Tooltip("A qué distancia de la cámara se planta. No cambia el tamaño en pantalla (se recalcula), solo dónde queda respecto del resto; basta con que sea menor que el Far Clip.")]
    [SerializeField] float distance = 200f;

    [Tooltip("Cuánto se pasa del borde de la pantalla. Un poco de más evita ver el filo por errores de redondeo o al rotar.")]
    [SerializeField] float overscan = 1.04f;

    [Tooltip("Corrimiento fino en pantalla, en fracción de su tamaño. Sirve para elegir QUÉ parte de la imagen queda a la vista.")]
    [SerializeField] Vector2 shift;

    [SerializeField] Color tint = Color.white;

    const string CHILD_NAME = "Backdrop";

    Camera         _camera;
    SpriteRenderer _renderer;

    // En Update y no en LateUpdate, por lo mismo que el resto del mapa: los nodos calculan su
    // altura en LateUpdate a partir de la cámara, y el orden entre dos LateUpdate no está
    // garantizado. Acá además la cámara ya se movió (WorldMapCameraFit también corre en Update).
    void Update()
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        if (_camera == null) return;

        var target = Renderer();
        if (target == null) return;

        target.gameObject.SetActive(sprite != null);
        if (sprite == null) return;

        target.sprite        = sprite;
        target.sharedMaterial = material;
        target.color         = tint;
        target.sortingOrder  = WorldMapDefinition.ORDER_BACKDROP;

        Fit(target);
    }

    // Lo que se ve a esa distancia: el FOV da el alto y la relación de pantalla, el ancho. La
    // escala se toma del lado que MÁS haga falta —no del promedio— para que la imagen cubra
    // siempre, aunque sobre por el otro lado. Recortar es aceptable; dejar un borde vacío no.
    void Fit(SpriteRenderer target)
    {
        float height = 2f * distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width  = height * _camera.aspect;

        Vector2 size = sprite.bounds.size;
        if (size.x <= 0f || size.y <= 0f) return;

        float scale = Mathf.Max(width / size.x, height / size.y) * overscan;

        target.transform.localScale    = Vector3.one * scale;
        target.transform.localRotation = Quaternion.identity;
        target.transform.localPosition = new Vector3(shift.x * width, shift.y * height, distance);
    }

    // El hijo se crea solo y no se guarda en la escena: se regenera en cada arranque, y guardarlo
    // dejaría uno viejo dentro del .unity que aparecería duplicado junto al recién creado.
    //
    // Se readopta buscándolo por nombre porque los objetos DontSave sobreviven a una recompilación
    // de scripts pero la referencia no: sin esto, cada recompilación en el editor crearía otro.
    SpriteRenderer Renderer()
    {
        if (_renderer != null) return _renderer;

        var existing = transform.Find(CHILD_NAME);
        if (existing != null) return _renderer = existing.GetComponent<SpriteRenderer>();

        var go = new GameObject(CHILD_NAME, typeof(SpriteRenderer)) { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(transform, false);

        return _renderer = go.GetComponent<SpriteRenderer>();
    }
}
