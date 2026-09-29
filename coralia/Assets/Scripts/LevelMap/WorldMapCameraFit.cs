using UnityEngine;

// Adapta la cámara del mapa a la forma de la pantalla, interpolando entre dos encuadres
// calibrados a mano: uno para teléfono y otro para tablet.
//
// Hace falta porque con una sola cámara se puede fijar el ancho o el alto, no los dos. Un iPad en
// vertical es 0.75 de ancho por alto y un iPhone 0.46: relativo a su altura, el iPad tiene un 62%
// más de ancho. Con los mismos números, en el iPad las decoraciones laterales se van hacia afuera
// y el camino queda nadando en agua.
//
// Se interpola en vez de resolverlo con una fórmula (fijar el FOV horizontal, por ejemplo) porque
// el encuadre bueno no sale solo del ángulo: en la tablet también conviene alejar y subir un poco
// la cámara. Eso es criterio visual, no geometría, así que se calibra a ojo en cada extremo y el
// componente se encarga del medio.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class WorldMapCameraFit : MonoBehaviour
{
    [System.Serializable]
    public class Framing
    {
        [Tooltip("Relación ancho/alto de la pantalla para la que se calibró. iPhone 13 Pro Max vertical = 0.462; iPad Pro 12.9 vertical = 0.75.")]
        public float aspect = 0.462f;

        public Vector3 position   = new Vector3(0f, 30f, -30f);
        public float   fieldOfView = 15f;

        [Tooltip("El 'Flat Until' de WorldMapCurve para este encuadre. Se mide DESDE la cámara, así que si la cámara se mueve, este tiene que acompañar.")]
        public float flatUntil = 35f;
    }

    [Tooltip("La pantalla más alargada, tipo teléfono.")]
    [SerializeField] Framing phone = new Framing();

    [Tooltip("La pantalla más cuadrada, tipo tablet.")]
    [SerializeField] Framing tablet = new Framing { aspect = 0.75f, position = new Vector3(0f, 32f, -33.5f), fieldOfView = 10f, flatUntil = 31.5f };

    [Tooltip("Opcional. Con esto asignado, el 'Flat Until' pasa a salir de los encuadres de acá y deja de editarse en WorldMapCurve.")]
    [SerializeField] WorldMapCurve curve;

    Camera _camera;

    // En Update y no en LateUpdate, por lo mismo que el scroll: los nodos calculan su altura en
    // LateUpdate a partir de la distancia a la cámara. Si la cámara se moviera ahí también, el
    // orden entre los dos scripts quedaría a suerte de Unity.
    void Update()
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        if (_camera == null || phone == null || tablet == null) return;

        // Fuera del rango calibrado no se extrapola: una pantalla más alargada que el teléfono de
        // referencia se queda con el encuadre del teléfono. Extrapolar daría números que nadie
        // miró nunca, justo en los dispositivos más raros.
        float t = Mathf.Approximately(tablet.aspect, phone.aspect)
            ? 0f
            : Mathf.Clamp01((_camera.aspect - phone.aspect) / (tablet.aspect - phone.aspect));

        transform.localPosition = Vector3.Lerp(phone.position, tablet.position, t);
        _camera.fieldOfView     = Mathf.Lerp(phone.fieldOfView, tablet.fieldOfView, t);

        if (curve) curve.FlatUntil = Mathf.Lerp(phone.flatUntil, tablet.flatUntil, t);
    }
}
