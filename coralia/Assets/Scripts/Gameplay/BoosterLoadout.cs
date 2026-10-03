using System;
using System.Collections.Generic;
using UnityEngine;

// Qué boosters se llevan al próximo nivel. Se eligen en la pantalla previa (StartGamePanel,
// GDD §3.3) y los lee el HUD de gameplay para saber qué íconos mostrar.
//
// Equipar NO descuenta nada: el inventario se gasta recién al disparar. Esto es solo una
// selección, y por eso vive aparte de SaveManager — ahí está lo que el jugador TIENE, acá lo
// que decidió llevar, que es una pregunta distinta y de otra pantalla.
public static class BoosterLoadout
{
    public const int MAX_EQUIPPED = 3;   // GDD §3.5 — cap lockeado

    const string KEY = "booster_loadout";

    public static event Action OnChanged;

    static List<BubbleSpecial> _equipped;

    // Lo que el jugador decidió llevar, se le haya gastado o no durante el nivel.
    //
    // NO se filtra por inventario a propósito: el ícono del HUD tiene que seguir ahí al gastar el
    // último —con su '+' para reponerlo— en vez de desaparecer a mitad de partida. Que no se
    // pueda equipar lo que no se tiene ya lo impide CanEquip, que es donde corresponde.
    public static IReadOnlyList<BubbleSpecial> Equipped
    {
        get
        {
            Load();
            return _equipped;
        }
    }

    public static int Count => Equipped.Count;

    public static bool IsEquipped(BubbleSpecial booster)
    {
        if (booster == BubbleSpecial.None) return false;

        Load();

        return _equipped.Contains(booster);
    }

    public static bool CanEquip(BubbleSpecial booster) =>
        booster != BubbleSpecial.None &&
        SaveManager.BoosterCount(booster) > 0 &&
        (IsEquipped(booster) || Count < MAX_EQUIPPED);

    // Devuelve si quedó equipado, para que quien llame pueda dar feedback de "no se pudo"
    // sin volver a preguntar.
    public static bool Toggle(BubbleSpecial booster)
    {
        if (booster == BubbleSpecial.None) return false;

        Load();

        if (IsEquipped(booster))
        {
            _equipped.Remove(booster);
            Save();
            return false;
        }

        if (!CanEquip(booster)) return false;

        _equipped.Add(booster);
        Save();

        return true;
    }

    // Equipar sin pasar por el toque del jugador. Lo usa el tutorial que presenta un booster: ahí
    // no lo eligió él, se lo pusieron en la mano, y aun así tiene que aparecerle en el HUD.
    public static bool Equip(BubbleSpecial booster)
    {
        if (booster == BubbleSpecial.None || IsEquipped(booster)) return false;

        // El cap se respeta igual: romperlo por la puerta de atrás dejaría al jugador con cuatro
        // íconos en un HUD diseñado para tres.
        if (Count >= MAX_EQUIPPED) return false;

        _equipped.Add(booster);
        Save();

        return true;
    }

    public static void Clear()
    {
        Load();

        if (_equipped.Count == 0) return;

        _equipped.Clear();
        Save();
    }

    static void Load()
    {
        if (_equipped != null) return;

        _equipped = new List<BubbleSpecial>();

        foreach (var name in PlayerPrefs.GetString(KEY, "").Split(','))
            if (Enum.TryParse(name, out BubbleSpecial booster) && booster != BubbleSpecial.None && !_equipped.Contains(booster))
                _equipped.Add(booster);
    }

    static void Save()
    {
        PlayerPrefs.SetString(KEY, string.Join(",", _equipped));
        PlayerPrefs.Save();

        OnChanged?.Invoke();
    }
}
