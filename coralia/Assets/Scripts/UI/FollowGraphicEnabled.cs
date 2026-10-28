using UnityEngine;
using UnityEngine.UI;

// Este Graphic sigue a su padre: se apaga cuando él se apaga y se desvanece cuando él se
// desvanece.
//
// Hace falta porque apagar un componente Graphic NO apaga a sus hijos: son objetos activos con su
// propio Graphic encendido. El cañón apaga la burbuja de la recámara desde siete sitios distintos
// —al disparar, al cargar un poder, al relevar, al abrir el nivel— y sin esto el brillo se quedaba
// pintado en la almeja mientras la burbuja volaba (reportado por Diego).
//
// Y con el alfa pasa lo mismo: el pop de una burbuja baja el alfa de SU Image, no el de sus
// hijos, así que en los tutoriales las burbujas reventaban y su cristal se quedaba flotando
// entero (reportado por Diego).
//
// Se sigue cada frame en vez de parchear los sitios que lo tocan: los que vengan no van a saber
// que esta capa existe, y el siguiente volvería a dejar el brillo colgado.
public class FollowGraphicEnabled : MonoBehaviour
{
    Graphic _self;
    Graphic _host;

    void Awake()
    {
        _self = GetComponent<Graphic>();
        _host = transform.parent != null ? transform.parent.GetComponent<Graphic>() : null;

        if (_host == null) enabled = false;   // sin padre que seguir no hay nada que hacer
    }

    // El propio componente guarda si LE toca verse: el padre manda sobre eso, pero no al revés.
    // Sin esta memoria, un brillo apagado a propósito —una burbuja de poder, que trae el suyo—
    // volvería a encenderse en cuanto el padre se encendiera.
    public bool Wanted { get; set; }

    [Tooltip("Marcado: además de apagarse con el padre, copia su transparencia. Para una capa que tiene que desvanecerse con él — un brillo sobre una burbuja que revienta.")]
    [SerializeField] bool followAlpha = true;

    void LateUpdate()
    {
        bool show = Wanted && _host.enabled;

        if (_self.enabled != show) _self.enabled = show;

        if (!followAlpha) return;

        float alpha = _host.color.a;

        if (!Mathf.Approximately(_self.color.a, alpha))
        {
            var c = _self.color;
            _self.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}
