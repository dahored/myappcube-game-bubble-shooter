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
//
// Sirve igual para un Image de UI que para un SpriteRenderer del mundo. La misma mascota aparece
// en el cañón, que es UI, y como decoración del arrecife, que se dibuja con sprites: pedir dos
// componentes casi iguales para eso obligaría a acordarse de cuál va en cada sitio.
public class EyeBlink : MonoBehaviour
{
    [SerializeField] Image          target;       // opcional — si no se asigna, se busca solo acá
    [SerializeField] SpriteRenderer spriteTarget; // ídem, para cuando la mascota no es UI
    [SerializeField] Sprite eyesOpen;
    [SerializeField] Sprite eyesHalf;
    [SerializeField] Sprite eyesClosed;

    [Header("Timing")]
    [SerializeField] float minInterval   = 2f;    // segundos entre parpadeos
    [SerializeField] float maxInterval   = 5f;
    [SerializeField] float frameDuration = 0.06f; // cuánto dura cada paso (abierto->medio->cerrado->medio->abierto)

    void Awake()
    {
        if (!target)       target       = GetComponent<Image>();
        if (!spriteTarget) spriteTarget = GetComponent<SpriteRenderer>();

        if (!target && !spriteTarget)
            Debug.LogWarning($"[EyeBlink] '{name}' no tiene ni Image ni SpriteRenderer — no hay nada que hacer parpadear.", this);
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
        if (!sprite) return;

        if (target)       target.sprite       = sprite;
        if (spriteTarget) spriteTarget.sprite = sprite;
    }
}
