using UnityEngine;

public class SceneTransitionSetup : MonoBehaviour
{
    [SerializeField] Sprite[]   bubbleSprites;
    [SerializeField] AudioClip  bubbleSound;

    void Awake()
    {
        SceneTransition.SetBubbleSprites(bubbleSprites);
        SceneTransition.SetBubbleSound(bubbleSound);
        DontDestroyOnLoad(gameObject);
    }
}
