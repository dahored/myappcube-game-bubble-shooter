using UnityEngine;

// Permite que una escena avise que todavía está armándose, para que la transición no descubra la
// pantalla antes de tiempo.
//
// Existe porque "escena cargada" y "escena lista" no son lo mismo. SceneManager da por terminada
// la carga apenas activa la escena, pero si algo se construye a lo largo de varios frames — como
// el mapa, que instancia sus nodos repartidos para no trabar la animación — el jugador vería ese
// armado a medias durante el fundido de salida.
//
// Quien tarde en estar listo llama a Hold() al arrancar y Release() al terminar. Si nadie lo usa,
// todo funciona exactamente como antes.
public static class SceneReady
{
    static int _pending;

    public static bool IsReady => _pending <= 0;

    public static void Hold() => _pending++;

    public static void Release() => _pending = Mathf.Max(0, _pending - 1);

    // Por si algo quedó colgado (una excepción a mitad de camino, un objeto destruido antes de
    // liberar). La transición igual tiene su propio tope de espera, pero esto deja el contador
    // limpio para la escena siguiente.
    public static void Clear() => _pending = 0;
}
