using UnityEngine;

// Ajusta el borde inferior de un panel para que caiga justo en el horizonte, así ocupa exactamente
// el fondo de agua que se ve por encima del suelo.
//
// A mano no se puede: dónde cae el horizonte depende del encuadre, y WorldMapCameraFit mueve la
// cámara y el FOV entre teléfono y tablet. Un alto fijo cuadra en un dispositivo y se sale en el
// resto.
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class HorizonFit : MonoBehaviour
{
    [Tooltip("Vacío: se usa la cámara principal.")]
    [SerializeField] Camera eye;

    [Tooltip("Cuánto baja del horizonte, en píxeles del lienzo. En positivo el panel se mete un poco sobre el suelo, que suele quedar mejor que cortar justo en la línea.")]
    [SerializeField] float overlap = 0f;

    // En LateUpdate porque WorldMapCameraFit mueve la cámara en Update: midiendo antes, el panel
    // iría siempre un frame por detrás del encuadre.
    void LateUpdate()
    {
        var camera = eye != null ? eye : Camera.main;
        if (camera == null) return;

        var rect = (RectTransform)transform;
        if (rect.parent is not RectTransform parent) return;

        float height = parent.rect.height;
        if (height <= 0f) return;

        // Horizon viene de 0 a 1 desde abajo, que es justo cómo se mide el borde inferior de un
        // rect estirado: cuánto sube respecto del borde de abajo del padre.
        float bottom = WorldMapCurve.Horizon(camera) * height - overlap;

        // Solo el borde de ABAJO. El de arriba se deja como esté para poder bajar el panel por
        // debajo del panel superior sin que esto se lo pise en el frame siguiente.
        rect.offsetMin = new Vector2(rect.offsetMin.x, bottom);
    }
}
