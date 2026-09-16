using UnityEngine;

// Plano con una imagen de fondo, colocado lejos detrás de todo. En el mapa 3D la esfera tapa la
// parte de abajo, así que esto solo se ve en la franja de cielo que queda por encima del horizonte.
//
// Se posiciona y escala solo a partir de la cámara: distancia, FOV y relación de aspecto. Si se
// dejara a mano, habría que recalcular la escala para cada tamaño de pantalla.
//
// Corre también en el editor, así se ve sin darle Play.
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class CameraBackgroundQuad : MonoBehaviour
{
    [Tooltip("Si se deja vacío usa Camera.main.")]
    [SerializeField] Camera targetCamera;
    [Tooltip("A qué distancia de la cámara se pone. Tiene que quedar MÁS LEJOS que la esfera para que esta lo tape.")]
    [SerializeField] float distance = 120f;
    [Tooltip("Mantiene la proporción de la imagen y la recorta para cubrir la pantalla, en vez de deformarla.")]
    [SerializeField] bool preserveAspect = true;
    [Tooltip("Qué franja de la imagen queda a la vista, cuando sobra por arriba y abajo. 0 = centro, 1 = borde superior, -1 = inferior.")]
    [Range(-1f, 1f)]
    [SerializeField] float verticalFraming = 0f;

    void LateUpdate()
    {
        var cam = targetCamera ? targetCamera : Camera.main;
        if (!cam || cam.orthographic) return;

        float height = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width  = height * cam.aspect;
        float shift  = 0f;

        if (preserveAspect)
        {
            var tex = GetComponent<MeshRenderer>().sharedMaterial?.mainTexture;
            if (tex && tex.height > 0)
            {
                float imageAspect = (float)tex.width / tex.height;

                // Criterio "cubrir": se agranda el lado que falte, nunca se achica ninguno — así la
                // imagen llena la pantalla entera y lo que sobra se recorta, sin deformarse.
                if (imageAspect > cam.aspect)
                {
                    width = height * imageAspect;
                }
                else
                {
                    float fullHeight = width / imageAspect;
                    // Lo que sobra de alto se puede correr para elegir qué franja se ve.
                    shift  = (fullHeight - height) * 0.5f * verticalFraming;
                    height = fullHeight;
                }
            }
        }

        // El Quad de Unity mira hacia su -Z, así que alineado con la cámara queda de frente.
        transform.rotation   = cam.transform.rotation;
        transform.position   = cam.transform.position
                             + cam.transform.forward * distance
                             - cam.transform.up * shift;
        transform.localScale = new Vector3(width, height, 1f);
    }
}
