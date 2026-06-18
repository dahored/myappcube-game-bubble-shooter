using System;
using System.Collections.Generic;

[Serializable]
public class LevelData
{
    public int id;
    public int chapter;
    public string name;
    public int max_shots;
    public float rainbow_chance;      // probabilidad de burbuja arcoíris (0.0–1.0)
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
