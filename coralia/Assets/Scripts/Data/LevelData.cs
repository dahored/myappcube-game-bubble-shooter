using System;
using System.Collections.Generic;

[Serializable]
public class LevelData
{
    public int id;
    public int chapter;
    public int level_in_chapter;
    public int target_score;
    public int max_shots;
    public List<string> grid;
    public List<string> colors_available;
}
