using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Parpadeo en loop para una mascota idle (ej. OctopusSprite1) — alterna el sprite de un mismo
// Image entre 3 variantes (ojos abiertos/medio cerrados/cerrados) con pausas irregulares entre
// parpadeos, para que se sienta natural y no como un tic mecánico. Sin Animator, mismo
// criterio que el resto del proyecto (GelatineAnimation, RotateAnimation, etc.).
//
// Vive en el MISMO GameObject que CannonController prende/apaga (ej. OctopusSprite1) — al
// desactivarse, Unity detiene esta corutina sola; al reactivarse, OnEnable la arranca de
// nuevo. No hace falta que CannonController sepa nada de esto.
public class EyeBlink : MonoBehaviour
{
    [SerializeField] Image  target; // opcional — si no se asigna, se busca solo en este mismo objeto
    [SerializeField] Sprite eyesOpen;
    [SerializeField] Sprite eyesHalf;
    [SerializeField] Sprite eyesClosed;

    [Header("Timing")]
    [SerializeField] float minInterval   = 2f;    // segundos entre parpadeos
    [SerializeField] float maxInterval   = 5f;
    [SerializeField] float frameDuration = 0.06f; // cuánto dura cada paso (abierto->medio->cerrado->medio->abierto)

    void Awake()
    {
        if (!target) target = GetComponent<Image>();
    }

    void OnEnable()
    {
        SetSprite(eyesOpen);
        StartCoroutine(BlinkLoop());
    }

    IEnumerator BlinkLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
            yield return Blink();
        }
    }

    IEnumerator Blink()
    {
        SetSprite(eyesHalf);
        yield return new WaitForSeconds(frameDuration);
        SetSprite(eyesClosed);
        yield return new WaitForSeconds(frameDuration);
        SetSprite(eyesHalf);
        yield return new WaitForSeconds(frameDuration);
        SetSprite(eyesOpen);
    }

    void SetSprite(Sprite sprite)
    {
        if (target && sprite) target.sprite = sprite;
    }
}
