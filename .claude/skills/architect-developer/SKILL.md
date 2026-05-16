---
name: architect-developer
description: Godot 4 architecture for Coralia — autoloads, scene structure, signals, patterns
---

# Architect Developer — Coralia (Godot 4)

## Autoload singletons (global, always available)
Located in `scripts/autoloads/`. **Do not create new ones.**

| Autoload | Responsibility |
|---|---|
| `GameManager` | Current level ID, global signals bus, scene transitions |
| `SaveManager` | JSON persistence (`user://save_game.json`), schema in `_default_save()` |
| `EconomyManager` | Coins, gems, lives (with regen timer), transactions |
| `AudioManager` | 3 audio buses: `MUSICA_Y_AMBIENT`, `UI_FX`, `BUBBLE_POP` |
| `LevelManager` | Load/parse `data/levels/NNN.json`, `get_total_levels()` |
| `LocaleManager` | `TranslationServer.set_locale()`, persists to `SaveManager.data.settings.language` |
| `AnalyticsManager` | `track(event, params)` — stub, integrates Firebase/GameAnalytics |
| `AdsManager` | AdMob rewarded + interstitial — stub |
| `IAPManager` | RevenueCat IAP — stub |
| `BattlePassManager` | BP XP, tier, season — stub |
| `FirebaseManager` | Cloud save, auth — stub |

## Scene structure
```
scenes/
├── main/
│   └── boot.tscn         ← Entry point: routes to onboarding or gameplay
├── gameplay/
│   ├── gameplay.tscn     ← Gameplay root (Gameplay.gd)
│   ├── canon.tscn        ← Canon with 2-bubble queue (Canon.gd)
│   └── bubble.tscn       ← Single bubble (Bubble.gd)
```
Pending scenes to create: `santuario/santuario.tscn`, `ui/level_select.tscn`, `ui/pre_level.tscn`, `main/onboarding.tscn`

## Signal bus pattern
Cross-system communication goes through `GameManager` signals:
```gdscript
# Emit from anywhere:
GameManager.emit_signal("level_completed", level_id, score)

# Connect in GameManager._ready():
GameManager.level_completed.connect(AnalyticsManager._on_level_completed)
```

## Gameplay core classes
- `Grid` (`grid.gd`) — hex grid, `bubbles: Dictionary` (Vector2i → Bubble), match detection via `grid_logic.gd`
- `Canon` (`canon.gd`) — drag aim, trajectory with 1 bounce, 2-bubble queue, smart queue
- `Bubble` (`bubble.gd`) — enum `Type` (RED/BLUE/YELLOW/GREEN/PURPLE/ORANGE/RAINBOW), `is_creature: bool`
- `Gameplay` (`gameplay.gd`) — coordinates Grid + Canon, reads `LevelManager`, handles win/lose

## Level JSON format
```json
{
  "id": 1,
  "chapter": 1,
  "name": "Level Name",
  "objective": { "type": "clear_all" },
  "max_shots": 22,
  "available_colors": ["red", "blue"],
  "rainbow_chance": 0.0,
  "bubbles": [[col, row, "color"], ...],
  "obstacles": []
}
```
Objective types: `clear_all`, `rescue` (needs `creature_position: [col, row]` and `creature_id: "string"`)

## Save schema (key fields)
```gdscript
{
  "highest_level_completed": 0,
  "best_scores": {},           # key = str(level_id)
  "creatures_rescued": [],
  "currencies": {"coins": 0, "gems": 50},
  "lives": 5,
  "lives_last_regen": timestamp,
  "streak": {"current": 0, "longest": 0, "last_claim_day": 0, "last_login_timestamp": 0},
  "battle_pass": {"season": 1, "is_premium": false, "tier": 0, "xp_current_tier": 0},
  "settings": {"language": "es", "music_volume": 0.7, "ui_volume": 0.8, "pop_volume": 1.0, "vibration_enabled": true},
  "tutorial_completed": false
}
```

## Adding a new feature — checklist
1. Read GDD section for the feature
2. Check if an autoload stub already covers it (it probably does)
3. Add new save keys to `_default_save()` and update `_migrate()`
4. Wire signals through `GameManager`
5. All UI strings → `tr("key")` + entry in `localization/translations.csv` (6 languages)
6. Test with `SaveManager.reset_save()` to verify fresh start works
