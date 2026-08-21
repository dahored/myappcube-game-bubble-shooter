---
name: architect-developer
description: Unity 6 (C#) architecture for Coralia — managers, scene structure, patterns
---

# Architect Developer — Coralia (Unity 6, C#)

## Managers (static classes, NOT MonoBehaviour singletons)

Located in `coralia/Assets/Scripts/Core/`. No `DontDestroyOnLoad` GameObjects for these — they're plain static classes, always available, no instance needed.

| Manager | Responsibility |
|---|---|
| `SaveManager` | PlayerPrefs-backed persistence. Per-property get/set (e.g. `SaveManager.Gems`, `SaveManager.Lives`, `SaveManager.MaxUnlockedLevel`). Each property wraps `PlayerPrefs.GetInt/SetInt` + `.Save()`. |
| `LocaleManager` | Static dictionary loaded from `Resources/translations.csv`. `LocaleManager.Get(key)` returns the raw string for the active language — no built-in placeholder substitution, callers do `.Replace("{placeholder}", value.ToString())` manually. `OnLanguageChanged` event for dynamic UI refresh. |
| `AudioManager` | `AudioManager.Instance?.PlaySfx(clip)` / `PlayPop(clip)` — bootstraps itself from a prefab (see `[AudioManager] Bootstrap` log). SFX clips are optional `[SerializeField]` fields left empty until real audio exists; calls are always null-conditional (`?.`) so missing clips don't throw. |
| `SceneLoader` | Static class of scene-name constants (`SceneLoader.LEVEL_MAP`, `SceneLoader.GAMEPLAY`, etc.) plus `SceneLoader.GoTo(sceneName)`, which delegates to `SceneTransition`. |
| `SceneTransition` | Self-instantiating (`DontDestroyOnLoad`) the first time it's used. Handles the fade + animated-bubbles transition between scenes and the underlying `SceneManager.LoadSceneAsync`. |

**Do not create MonoBehaviour singletons for cross-scene state** — follow the static-class pattern above.

## Scene structure

```
coralia/Assets/Scenes/
├── Splash/
│   ├── SplashStudio.unity   ← studio logo (two-splash pattern, first screen)
│   └── SplashGame.unity     ← game logo + version + loading (second screen)
├── Home/
│   └── HomeGame.unity       ← lobby
└── Game/
    ├── LevelMap.unity       ← level select (LevelMapController, LevelNodeView, ScrollPinController)
    └── Gameplay.unity       ← cañón + grid + match (GameplayController orchestrates the scene)
```

## Scripts folder layout

```
coralia/Assets/Scripts/
├── Core/       ← SaveManager, LocaleManager, AudioManager, SceneLoader, SceneTransition
├── UI/         ← reusable UI components: ButtonPop, UIPanel, SettingsToggle, LocalizedText,
│                 ResponsiveLayout, SafeAreaPanel, TopPanelController, ResourcePillView, MessageView
├── Home/       ← HomeGame.cs
├── Splash/     ← SplashStudio.cs, SplashGame.cs
├── LevelMap/   ← LevelMapController.cs, LevelNodeView.cs, ScrollPinController.cs
├── Gameplay/   ← HexGridMath, BubbleColor, BubbleView, GridController, CannonController,
│                 ShotBubble, TrajectoryLine, AimInputRelay, GameplayController, WinLosePanel
└── Data/       ← LevelData.cs (serializable level model), LevelLoader.cs
```

## UIPanel base pattern

Popups/modals (`SettingsPanel`, `WinLosePanel`) inherit from `UIPanel` (`Scripts/UI/UIPanel.cs`):
- `[RequireComponent(typeof(CanvasGroup))]`, abstract class.
- `[SerializeField] RectTransform card` — the scalable popup content (separate from the panel root, used for the open/close scale+fade animation).
- `Open()` / `Close()` — coroutine-based, animates `card.localScale` via `openCurve`/`closeCurve` (`AnimationCurve`) while fading the `CanvasGroup`.
- Subclasses override `Awake()` (calling `base.Awake()`) to wire button listeners; they don't touch the animation logic.

## Gameplay core classes (`Scripts/Gameplay/`)

- `HexGridMath` (static) — pure geometry: offset coordinates, row 0 = ceiling, odd rows shifted half a bubble-diameter right. `ColsInRow(row)` returns the fixed max column count per row parity (see the constant in the file, not hardcoded elsewhere). `CellToLocalPos`/`EstimateNearestCell` center the grid using a fixed `DesignWidth`, independent of the actual runtime container size — wall-bounce math (`ReflectIfNeeded`) uses the real, dynamic container width instead, since bounces need to reflect off the actual screen edge.
- `GridController` — single source of truth for grid occupancy (`Dictionary<Vector2Int, BubbleView>`). Owns match detection (flood-fill), ceiling-connectivity BFS (drop logic), nearest-empty-cell search. Nothing else touches its internal dictionary directly.
- `CannonController` — drag-to-aim (via `AimInputRelay` forwarding from `AimArea`), 2-bubble queue + tap-to-swap, fires `ShotBubble`, raises `OnBubbleLanded`.
- `ShotBubble` — bubble in flight. No own `Update()` — `CannonController.Update()` calls `Tick(dt)` each frame so the whole shot flow stays orchestrated from one place.
- `GameplayController` — per-scene coordinator (plain `MonoBehaviour`, **not** a `DontDestroyOnLoad` singleton — lives and dies with the `Gameplay` scene). Reads `PlayerPrefs["selected_level"]`, loads the level via `LevelLoader`, orchestrates match → drop → win/lose, HUD.
- `WinLosePanel : UIPanel` — presentation only; raises events (`OnContinuePressed`, `OnAbandonPressed`) that `GameplayController` listens to and acts on (SRP — the panel never touches game state itself).

## Level JSON format

Real schema, used by `LevelData.cs` (`Scripts/Data/`) and loaded via `LevelLoader.LoadById(int)` from `coralia/Assets/Resources/Levels/Chapter_N/NNN.json`:

```json
{
  "id": 1,
  "chapter": 1,
  "name": "Nombre del nivel",
  "objective": { "type": "clear_all" },
  "max_shots": 22,
  "available_colors": ["red", "blue"],
  "rainbow_chance": 0.0,
  "bubbles": [
    { "row": 0, "col": 0, "color": "red" }
  ],
  "obstacles": [],
  "star_thresholds": [40, 100, 200]
}
```

Bubble entries are **objects** (`{row, col, color}`), not `[col, row, "color"]` arrays. Objective types: `clear_all`, `rescue` (needs `creature_position: [row, col]` and `creature_id`).

## Save data (key fields)

`SaveManager` stores each field as an individual PlayerPrefs key (no single JSON blob) — see the per-property pattern in `Scripts/Core/SaveManager.cs`: `Language`, `MaxUnlockedLevel`, `Gems`, `Lives`, `MusicVolume`/`SfxVolume`/`UiVolume`/`PopVolume`, `Vibration`, `SoundEnabled`, `MusicEnabled`.

## Adding a new feature — checklist

1. Read the relevant GDD section (`docs/02_GDD_Coralia.md`) — but not its §14 (architecture), which is Godot-era and stale.
2. Check `docs/07_Status_y_Roadmap.md` and the actual code for what's already implemented before assuming a feature is missing.
3. Add new `SaveManager` properties following the existing per-property PlayerPrefs pattern if the feature needs persistence.
4. Communicate cross-system state via C# events (`public event System.Action ...`) on the relevant controller/panel, not a global signal bus.
5. All UI strings → `Resources/translations.csv` (6 languages: es, en, it, fr, de, pt), accessed via `LocaleManager.Get(key)`. Use `LocalizedText.cs` for static UI text, `LocaleManager.OnLanguageChanged` to refresh dynamic text.
6. Follow the naming conventions in `CLAUDE.md` (no namespace, PascalCase classes, camelCase `[SerializeField]` fields aligned in columns, `_camelCase` private fields).
