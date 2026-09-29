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

    [Tooltip("Opcional. Con esto asignado, el borde de ARRIBA se coloca justo debajo del panel superior. Hace falta porque ese panel crece al ejecutar para respetar la zona segura del dispositivo, así que su alto en el editor no es el definitivo.")]
    [SerializeField] TopPanelController topPanel;

    [Tooltip("Separación entre el panel superior y el borde de arriba, en píxeles del lienzo.")]
    [SerializeField] float paddingFromPanel = 20f;

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

        rect.offsetMin = new Vector2(rect.offsetMin.x, bottom);

        // El borde de arriba solo se toca si hay panel superior que esquivar. Sin él se deja como
        // esté, para poder colocarlo a mano sin que esto se lo pise al frame siguiente.
        //
        // No vale medirlo en el editor y dejar el número escrito: TopPanelController se agranda al
        // arrancar según la zona segura del dispositivo, así que el alto que se ve mientras editas
        // no es el que va a tener en el teléfono.
        if (topPanel != null)
            rect.offsetMax = new Vector2(rect.offsetMax.x, -(topPanel.PanelHeight + paddingFromPanel));
    }
}
