using UnityEngine;

// El temblor de gelatina, para lo que tenga que verse blando y vivo.
//
// El truco entero es la OPOSICIÓN: lo que se estira a lo ancho se encoge a lo alto. Subiendo los
// dos ejes a la vez el bulto solo se hace más grande, y lo que tiene que leerse es que es blando.
//
// Lo que devuelve es un AÑADIDO al tamaño, no el tamaño: quien lo use lo suma a la escala que ya
// tuviera. Así una criatura puede temblar mientras encoge hacia el hub, en vez de quedarse
// clavada al tamaño que le imponga el temblor.
public static class Jelly
{
    public const float HZ   = 5.5f;
    public const float DAMP = 3.5f;

    // El rebote de un golpe: tiembla y se calma solo. Un rebote largo termina quieto sin que
    // quien lo use tenga que cortarlo en el momento justo.
    public static Vector3 Pulse(float t, float amp) => At(t, amp * Mathf.Exp(-t * DAMP), HZ);

    // El de estar vivo: no se calma nunca y va mucho más despacio. Un bicho esperando dentro de
    // su burbuja respira, no rebota — al ritmo del golpe parecería que lo están sacudiendo.
    public static Vector3 Idle(float t, float amp, float hz = 0.7f) => At(t, amp, hz);

    static Vector3 At(float t, float amp, float hz)
    {
        float wobble = Mathf.Sin(t * hz * Mathf.PI * 2f) * amp;

        return new Vector3(wobble, -wobble, 0f);
    }
}
