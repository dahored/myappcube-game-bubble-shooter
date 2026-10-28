using UnityEngine;
using UnityEngine.UI;

// Le pone a una burbuja dibujada a mano el mismo brillo que llevan las del tablero.
//
// Hace falta porque las demostraciones de los tutoriales componen sus burbujas por su cuenta —no
// usan BubbleView— y desde que los cuerpos se exportan planos salían mates mientras el juego
// detrás brillaba (reportado por Diego).
//
// Vive aparte en vez de repetirse en los diez demos: son diez sitios donde olvidarse, y el que se
// olvide enseña una burbuja que no se parece a la del juego.
public static class BubbleGlow
{
    // Devuelve la misma Image que recibe, para poder encadenarlo en la línea que la crea.
    public static Image AddTo(Image bubble)
    {
        if (bubble == null) return bubble;

        var sprite = BubbleCatalog.Glow;

        // Sin brillo compartido no hay nada que añadir: o el catálogo no está, o los cuerpos
        // todavía lo traen pintado dentro. Las dos son situaciones legítimas.
        if (sprite == null) return bubble;

        var go = new GameObject("Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
        {
            hideFlags = HideFlags.DontSave,
        };

        var rt = (RectTransform)go.transform;

        rt.SetParent(bubble.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();

        image.sprite         = sprite;
        image.raycastTarget  = false;
        image.preserveAspect = true;

        // Que se apague y se desvanezca con su burbuja. Los demos revientan sus burbujas bajando
        // el alfa de la Image, y eso no llega a los hijos: el cristal se quedaba flotando después
        // de que la burbuja desapareciera.
        var follow = go.AddComponent<FollowGraphicEnabled>();

        follow.Wanted = true;

        return bubble;
    }
}
