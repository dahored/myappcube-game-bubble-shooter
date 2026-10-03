using UnityEditor;
using UnityEngine;

// Darse boosters a mano para probarlos. Mientras no exista la tienda (issue #23) no hay ninguna
// forma de conseguir una bomba dentro del juego, así que sin esto el booster no se puede probar.
//
// Se borra el día que la tienda exista — o se deja, que para calibrar el radio de la explosión
// hace falta disparar muchas más bombas de las que nadie compraría.
public static class BoosterDebugMenu
{
    const int GRANT = 5;

    [MenuItem("Coralia/Debug — Regalar 5 bombas")]
    static void GrantBombs()
    {
        SaveManager.GrantBooster(BubbleSpecial.Bomb, GRANT);
        Debug.Log($"[Coralia] Bombas disponibles: {SaveManager.BoosterCount(BubbleSpecial.Bomb)}");
    }

    // Qué ve cada pantalla de boosters y por qué. Los tres datos viven en sitios distintos
    // —inventario, bit de desbloqueo y loadout— y un ícono que no aparece puede ser cualquiera de
    // los tres: sin verlos juntos no se distingue "no lo tengo" de "no lo equipé".
    [MenuItem("Coralia/Debug — Estado de los boosters")]
    static void DumpBoosters()
    {
        var report = new System.Text.StringBuilder("[Coralia] Estado de los boosters\n");

        foreach (BubbleSpecial booster in System.Enum.GetValues(typeof(BubbleSpecial)))
        {
            if (booster == BubbleSpecial.None) continue;

            report.AppendLine(
                $"  {booster}: tengo {SaveManager.BoosterCount(booster)} · " +
                $"desbloqueado {SaveManager.IsBoosterUnlocked(booster)} · " +
                $"tutorial visto {SaveManager.HasSeenTutorial(BoosterRules.NameOf(booster))} · " +
                $"equipado {BoosterLoadout.IsEquipped(booster)}");
        }

        report.Append($"  Loadout guardado: '{PlayerPrefs.GetString("booster_loadout", "")}'");

        Debug.Log(report.ToString());
    }

    [MenuItem("Coralia/Debug — Olvidar tutoriales vistos")]
    static void ForgetTutorials()
    {
        SaveManager.ForgetTutorials();
        Debug.Log("[Coralia] Tutoriales olvidados: vuelven a salir desde el próximo nivel.");
    }
}
