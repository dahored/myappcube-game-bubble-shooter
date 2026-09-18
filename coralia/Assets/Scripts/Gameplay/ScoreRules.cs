using System.Collections.Generic;
using UnityEngine;

// Las reglas de puntaje del juego, en un solo lugar. Las usan GameplayController mientras se
// juega y LevelDifficultyEstimator para calcular los umbrales de estrellas de cada nivel.
//
// Antes estaban duplicadas en los dos archivos: cambiar una sin la otra dejaba las estrellas
// pidiendo un puntaje que el nivel ya no podía dar.
//
// La regla (definida por Diego):
//   - Una burbuja reventada en un match de 3+ vale 10 puntos en el primer combo, 20 en el
//     segundo consecutivo, 30 en el tercero... La racha sube con cada disparo que matchea.
//   - Fallar un disparo la corta: el siguiente match vuelve a valer 10.
//   - Las que caen por quedar sueltas valen 50 fijo, sin importar la racha — no se ganan
//     apuntando, se ganan de rebote.
//   - Cada disparo que sobra al ganar suma 1.000.
public static class ScoreRules
{
    public const int POINTS_PER_POP_BASE       = 10;
    public const int POINTS_PER_DROP           = 50;
    public const int POINTS_PER_REMAINING_SHOT = 1_000;

    // Cuánto vale UNA burbuja reventada con la racha en 'streak'. Streak 1 es el primer match;
    // el Max evita que un 0 (racha recién cortada) anule el puntaje del disparo que la reinicia.
    public static int PopValue(int streak) => POINTS_PER_POP_BASE * Mathf.Max(1, streak);

    public static int PopScore(int bubbles, int streak) => bubbles * PopValue(streak);

    public static int DropScore(int bubbles) => bubbles * POINTS_PER_DROP;

    public static int RemainingShotsScore(int shots) => Mathf.Max(0, shots) * POINTS_PER_REMAINING_SHOT;

    // Cuánto de ese bonus se otorga realmente. La regla: puede ayudar a alcanzar la SIGUIENTE
    // estrella, pero no la de después — nunca salta un escalón que no se ganó jugando.
    //
    // Sin esto, con 1.000 por disparo unos pocos sobrantes valían más que el tablero entero y
    // compraban las tres estrellas sin importar cómo se jugó.
    //
    // Se topa el bonus y no la barra a propósito: así el puntaje que se muestra, el que llena la
    // barra y el que queda como récord son el mismo número. Topando la barra habría dos verdades
    // en pantalla — un puntaje alto al lado de una barra que no le corresponde.
    //
    // Excepción del último tramo: con dos estrellas ya hechas jugando, el bonus completa la
    // tercera y sigue sumando sin límite. Ahí no hay escalón que saltarse.
    public static int AllowedBonus(int gameplayScore, int earnedBonus, List<int> thresholds)
    {
        if (thresholds == null || thresholds.Count < 3) return earnedBonus;

        int stars = 0;
        foreach (var threshold in thresholds)
            if (gameplayScore >= threshold) stars++;

        if (stars >= 2) return earnedBonus;

        // Se puede llegar al escalón siguiente, pero hay que quedarse debajo del que le sigue.
        int ceiling = thresholds[stars + 1];
        return Mathf.Clamp(ceiling - 1 - gameplayScore, 0, earnedBonus);
    }
}
