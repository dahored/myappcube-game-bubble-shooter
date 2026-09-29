using UnityEngine;

// Le pasa a SceneTransition los sprites y el sonido de las burbujas, y se queda vivo entre
// escenas para no tener que volver a asignarlos en cada una.
//
// Está puesto en varias escenas (splash y los mapas), así que hay que quedarse con UNO: sin el
// guard, cada vuelta al mapa dejaba otro DontDestroyOnLoad encima del anterior y se acumulaban
// para siempre.
public class SceneTransitionSetup : MonoBehaviour
{
    [SerializeField] Sprite[]   bubbleSprites;
    [SerializeField] AudioClip  bubbleSound;

    static SceneTransitionSetup _instance;

    void Awake()
    {
        // El primero gana. Los que vienen después igual entregan lo suyo antes de irse — así una
        // escena puede traer sus propias burbujas aunque no sea la que sobrevive.
        SceneTransition.SetBubbleSprites(bubbleSprites);
        SceneTransition.SetBubbleSound(bubbleSound);

        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
