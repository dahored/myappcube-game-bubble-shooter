using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Coordinador de escena — NO es un singleton DontDestroyOnLoad (a diferencia de
// AudioManager/LocaleManager), vive y muere con la escena Gameplay.
public class GameplayController : MonoBehaviour
{
    [SerializeField] GridController   grid;
    [SerializeField] CannonController cannon;
    [SerializeField] WinLosePanel     winLosePanel;
    [SerializeField] TMP_Text         shotsLabel;

    [Header("SFX (opcional — dejar vacío hasta tener los clips)")]
    [SerializeField] AudioClip popClip;
    [SerializeField] AudioClip dropClip;

    const int CONTINUE_SHOTS_BONUS = 5; // GDD §7 — "Pagar gemas: +5 disparos"

    LevelData  _level;
    int        _shotsRemaining;
    Vector2Int _creatureCell = new(-1, -1);
    bool       _creatureFreed;
    bool       _levelEnded;

    void Start()
    {
        int levelId = PlayerPrefs.GetInt("selected_level", 1);
        _level = LevelLoader.LoadById(levelId);
        if (_level == null)
        {
            Debug.LogError($"[GameplayController] No se encontró el nivel {levelId}");
            return;
        }

        if (_level.objective != null && _level.objective.type == "rescue" && _level.objective.creature_position?.Count == 2)
        {
            // creature_position es [fila, col] (ver LevelData.cs) -> Vector2Int(col, fila)
            _creatureCell = new Vector2Int(_level.objective.creature_position[1], _level.objective.creature_position[0]);
        }

        grid.SpawnFromLevel(_level);
        cannon.Init(_level.available_colors, _level.rainbow_chance);
        cannon.OnBubbleLanded += OnBubbleLanded;

        winLosePanel.OnContinuePressed += OnContinuePressed;
        winLosePanel.OnAbandonPressed  += OnAbandonPressed;

        _shotsRemaining = _level.max_shots;
        RefreshShotsLabel();
    }

    void OnDestroy()
    {
        if (cannon != null) cannon.OnBubbleLanded -= OnBubbleLanded;
        if (winLosePanel != null)
        {
            winLosePanel.OnContinuePressed -= OnContinuePressed;
            winLosePanel.OnAbandonPressed  -= OnAbandonPressed;
        }
    }

    // GDD §7 — "Pagar gemas": +5 disparos, la ronda sigue (no cuenta como nuevo intento).
    void OnContinuePressed()
    {
        SaveManager.Gems -= WinLosePanel.ContinueGemsCost;
        _shotsRemaining  += CONTINUE_SHOTS_BONUS;
        _levelEnded       = false;
        RefreshShotsLabel();
        cannon.SetInputEnabled(true);
        winLosePanel.Close();
    }

    // GDD §7 — "Aceptar derrota": -1 vida, la navegación al mapa ya la hace WinLosePanel.
    void OnAbandonPressed() => SaveManager.Lives--;

    void OnBubbleLanded(Vector2Int landedCell)
    {
        if (_levelEnded) return;

        _shotsRemaining--;
        RefreshShotsLabel();

        var removed = ResolveMatchAndDrop(landedCell);
        if (removed.Contains(_creatureCell)) _creatureFreed = true;

        CheckWinLose();
    }

    // Match (3+ conectadas) -> explode, después drop de todo lo que quedó flotando.
    // Devuelve el set de celdas removidas (para chequear el objetivo rescue).
    HashSet<Vector2Int> ResolveMatchAndDrop(Vector2Int landedCell)
    {
        var removed = new HashSet<Vector2Int>();

        var matched = grid.FindConnectedSameColor(landedCell);
        if (matched.Count < 3) return removed;

        foreach (var cell in matched)
        {
            if (grid.TryGetBubble(cell, out var view)) view.PlayPopAnimation();
            grid.RemoveBubble(cell);
            removed.Add(cell);
        }
        if (matched.Count > 0) AudioManager.Instance?.PlayPop(popClip);

        var floating = grid.FindUnreachableFromCeiling();
        foreach (var cell in floating)
        {
            if (grid.TryGetBubble(cell, out var view)) view.PlayDropAnimation();
            grid.RemoveBubble(cell);
            removed.Add(cell);
        }
        if (floating.Count > 0) AudioManager.Instance?.PlayPop(dropClip);

        return removed;
    }

    void CheckWinLose()
    {
        bool objectiveMet = _level.objective.type == "rescue" ? _creatureFreed : grid.CellCount == 0;
        if (objectiveMet) { EndLevel(true); return; }
        if (_shotsRemaining <= 0) EndLevel(false);
    }

    void EndLevel(bool won)
    {
        _levelEnded = true;
        cannon.SetInputEnabled(false);

        if (won && _level.id >= SaveManager.MaxUnlockedLevel)
            SaveManager.MaxUnlockedLevel = _level.id + 1;

        if (won) winLosePanel.ShowWin(_level.objective.type == "rescue");
        else     winLosePanel.ShowLose();
    }

    void RefreshShotsLabel()
    {
        if (shotsLabel) shotsLabel.text = $"{LocaleManager.Get("ui.gameplay.shots_remaining")}: {_shotsRemaining}";
    }
}
