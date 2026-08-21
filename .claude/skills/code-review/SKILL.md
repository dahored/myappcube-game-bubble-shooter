---
name: code-review
description: C#/Unity code review checklist specific to Coralia conventions
---

# Code Review — Coralia (Unity 6 / C#)

## C# conventions

- [ ] No namespace — classes go in the global namespace (matches all existing code)
- [ ] One public class per file, file name matches the class name (`PascalCase.cs`)
- [ ] `[SerializeField]` fields: `camelCase`, aligned in columns when several appear consecutively (see `SettingsToggle.cs`, `ButtonPop.cs` for the reference style)
- [ ] Private non-serialized fields: `_camelCase`
- [ ] Constants: `SCREAMING_SNAKE_CASE` or `PascalCase` depending on visibility (`const string KEY_LANGUAGE` vs. `public const string SPLASH_STUDIO`)
- [ ] No multi-paragraph comments/docstrings — a short one-liner only when the *why* isn't obvious from the code

## Strings & i18n

- [ ] **Zero hardcoded UI strings** — everything visible to the player goes through `LocaleManager.Get("ui.key.path")` with a matching row in `Resources/translations.csv` (all 6 languages: es, en, it, fr, de, pt)
- [ ] Modal/panel text (win/lose, confirmations) is also i18n'd, not just static labels
- [ ] Placeholder substitution (`{value}`) is manual (`LocaleManager.Get` doesn't do it) — check the `.Replace(...)` actually happens
- [ ] `Debug.Log` messages are fine for diagnostics, not a substitute for UI text

## Architecture

- [ ] No new `MonoBehaviour` singletons / `DontDestroyOnLoad` managers — extend the existing static classes in `Scripts/Core/` (`SaveManager`, `LocaleManager`, `AudioManager`, `SceneLoader`)
- [ ] Cross-system communication uses C# events (`public event System.Action ...`) on the owning controller, not a shared global bus
- [ ] Levels are JSON under `Resources/Levels/Chapter_N/` (object-per-bubble schema, `{row, col, color}`) — no hardcoded level data in code
- [ ] SRP: presentation components (panels, views) raise events and don't touch game state directly; controllers own game-state logic and react to those events (see `WinLosePanel` → `GameplayController` for the pattern)
- [ ] No binary formats for data — JSON/CSV only

## Economy / persistence

- [ ] Currency/lives changes go through `SaveManager` properties (e.g. `SaveManager.Gems -= cost`), not raw `PlayerPrefs` calls scattered elsewhere
- [ ] New persisted fields follow the existing per-property pattern in `SaveManager.cs` (a `const string KEY_X` + a property wrapping `PlayerPrefs.Get/Set` + `.Save()`) — there's no single save blob to migrate

## Unity-specific

- [ ] `[SerializeField]` reference fields are actually wired in the Inspector before merging — an unassigned reference doesn't fail at compile time, only as a `NullReferenceException` at runtime. If reviewing a script you can't test live, flag every new `[SerializeField]` for Diego to confirm is wired.
- [ ] `RectTransformUtility` (not raw `Input.mousePosition`/manual math) for any screen-to-UI-local coordinate conversion, so it works correctly with `Canvas` scale factor and `Screen Space Overlay`/`Camera` render modes alike.
- [ ] Centering/positioning math that depends on a fixed design size (e.g. a hex grid width) uses a fixed design constant, not the actual runtime container size — the runtime size varies per device aspect ratio and mixing the two causes off-center layouts that only show up on non-reference devices (iPad vs. iPhone, for example).
- [ ] Cross-script `Awake()`/`Start()` ordering is never assumed — Unity doesn't guarantee execution order between different scripts. If B depends on A having already run, call A explicitly from an orchestrator (e.g. `GameplayController`) rather than relying on lifecycle timing.

## Mobile considerations

- [ ] Touch/drag input via Unity's `EventSystem` interfaces (`IPointerDownHandler`, `IBeginDragHandler`, etc.), not raw mouse-only APIs
- [ ] Layout uses anchors/`RectTransform`, not hardcoded pixel positions that only work on one screen size
- [ ] Any new interactive UI element has a real `Image`/`Graphic` with `Raycast Target` enabled on its hit area — otherwise the `EventSystem` never detects the touch, silently

## Debug code

- [ ] Temporary `Debug.Log` calls added while diagnosing a bug are removed before the final commit (search for `// TEMP` — the convention used while debugging in this project)
- [ ] No debug-only shortcuts (level skips, save resets) reachable from normal gameplay flow
