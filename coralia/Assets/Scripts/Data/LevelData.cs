using System;
using System.Collections.Generic;

[Serializable]
public class LevelData
{
    public int id;
    public int chapter;

    // El nombre EN ESPAÑOL, que es lo que se escribe en el editor de niveles. No es el que va a
    // pantalla: para mostrarlo se usa DisplayName, que busca la traducción.
    public string name;

    // Clave de traducción derivada del id, así los 180 JSON no tienen que repetirla. La exporta
    // a translations.csv el menú Coralia → Exportar nombres de nivel a traducciones.
    public string NameKey => $"level.{id}.name";

    // Lo que se muestra al jugador. Mientras la clave no esté en el CSV cae al nombre en
    // español, así que traducir de a poco nunca deja una pantalla en blanco ni con la clave cruda.
    public string DisplayName => LocaleManager.Get(NameKey, name);
    public int max_shots;
    public int min_shots_to_clear;    // mínimo de disparos jugando óptimo (definido a mano por el
                                       // diseñador al calibrar el nivel, jugándolo — 0 = todavía no
                                       // calibrado). max_shots - min_shots_to_clear es el margen real
                                       // que determina si el nivel es fácil/difícil, y de ahí salen
                                       // los star_thresholds — ver GameplayController.CalculateStars.
    // SIN USO. La arcoíris salía del cañón con esta probabilidad, pero como burbuja no funciona:
    // conecta con cualquier color, así que un solo disparo se llevaba el grid entero. Pasa a ser
    // un booster. El campo sigue acá porque está escrito en los 180 JSON; el día que exista el
    // booster se borra de todos de una vez.
    public float rainbow_chance;
    public List<string> available_colors;
    public List<int> star_thresholds; // [1 estrella, 2 estrellas, 3 estrellas]
    public List<string> obstacles;
    public ObjectiveData objective;
    public List<BubbleEntry> bubbles;
}

[Serializable]
public class ObjectiveData
{
    public string type;               // "clear_all" | "rescue"
    public string creature_id;        // id de la criatura a rescatar (solo en "rescue")
    public List<int> creature_position; // [fila, col] donde está la criatura
}

[Serializable]
public class BubbleEntry
{
    public int row;
    public int col;
    public string color;
}
