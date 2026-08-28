---
name: implement-feature
description: Step-by-step guide to implement any Phase 2 feature in Coralia following established patterns
---

# Implement Feature — Coralia (Unity 6, C#)

Use this skill when starting work on a new issue.

## Pre-implementation checklist

1. **Read the issue** — `gh issue view N`, or `docs/06_Backlog_GitHub_Issues.md` if it predates a real GitHub issue (verify it still matches, that doc was written pre-Unity in places)
2. **Read the relevant GDD section** in `docs/02_GDD_Coralia.md` — skip §14 (architecture), it's Godot-era and stale
3. **Check dependencies** — does this feature need another issue done first? Verify against actual code, not just the issue's stated dependency, since docs can lag behind what's really implemented
4. **Create branch**: `git checkout -b issue-N-short-description`

## Implementation patterns by feature type

### Adding a new screen

Unity scenes/prefabs are assembled by hand in the Editor — Claude Code can write every script, but Diego does the scene/hierarchy/Inspector wiring (drag references, set anchors, add components). The workflow is a back-and-forth: write the script(s) first, then guide the Editor steps precisely (exact component names, exact fields to wire).

1. Scripts first: create the `MonoBehaviour`(s) in `coralia/Assets/Scripts/<Category>/`, no namespace, PascalCase class name matching the file name.
2. Scene: new `.unity` file under `coralia/Assets/Scenes/<Category>/`, matching the existing Canvas/CanvasScaler setup from a sibling scene (Screen Space Overlay, Scale With Screen Size, reference 1080×1920, match 0.5) — don't reinvent it, copy the pattern.
3. Routing: `SceneLoader.GoTo(SceneLoader.SCENE_NAME)` (add the constant to `SceneLoader.cs` if it's a new scene) — this delegates to `SceneTransition`, don't call `SceneManager.LoadScene` directly.
4. All labels/buttons text via `LocaleManager.Get("ui.screen.key")` + add the key to `Resources/translations.csv` (6 languages).
5. Diego adds the scene to Build Settings — that's an Editor/ProjectSettings action, not something to do via file edits.

### Extending a static manager (e.g. `SaveManager`, `AudioManager`)

1. Add the function/property to the existing static class in `Scripts/Core/`.
2. If it needs new save data, follow the existing per-property pattern in `SaveManager.cs`: a `const string KEY_X` and a property wrapping `PlayerPrefs.GetInt/SetInt/GetFloat/...` + `.Save()`. There's no single JSON blob to migrate — each field is independent.
3. Cross-system communication is a plain C# event (`public event System.Action<T> OnSomething;`) on the relevant controller, not a global signal bus — see `WinLosePanel.OnContinuePressed`/`CannonController.OnBubbleLanded` for the pattern.

### Adding audio (`AudioManager`)

- Files: drop into `assets/audio/{music,sfx}/` first (source), then import into `coralia/Assets/Resources/Audio/`.
- Call `AudioManager.Instance?.PlaySfx(clip)` / `PlayPop(clip)` — always null-conditional, since `[SerializeField] AudioClip` fields are commonly left empty until real audio exists (the game must run fine with silence, just no crash).
- Volumes read from `SaveManager.MusicVolume` / `SfxVolume` / `UiVolume` / `PopVolume`.

### Adding a new save field

```csharp
// In SaveManager.cs:
const string KEY_NEW_FIELD = "new_field";

public static int NewField
{
    get => PlayerPrefs.GetInt(KEY_NEW_FIELD, defaultValue);
    set { PlayerPrefs.SetInt(KEY_NEW_FIELD, value); PlayerPrefs.Save(); }
}
```

### Adding i18n keys

In `coralia/Assets/Resources/translations.csv`, add a row (header is `keys,es,en,it,fr,de,pt`):
```
ui.button.play,Jugar,Play,Gioca,Jouer,Spielen,Jogar
```
Then in C#: `someText.text = LocaleManager.Get("ui.button.play");`. For placeholders (`{value}`), `LocaleManager.Get` does no substitution — do it manually: `.Replace("{gems}", cost.ToString())`.

## Testing before commit

- Play mode in the Unity Editor — test the actual scene, not just that it compiles.
- Test win condition AND lose condition where relevant.
- Watch the Console for null-reference exceptions — most first-pass bugs in this project have been unwired `[SerializeField]` fields in the Inspector, not logic errors. Ask Diego to paste the exact Console line; the stack trace's line number tells you which field is empty.
- If the feature touches device-dependent layout (aspect ratio, safe area), test in more than one Simulator device profile — Unity's `CanvasScaler` "Match Width Or Height" doesn't give every device the same effective width/height in reference units.

## Commit & close

See the `commit` skill for the authoritative branch/message/CHANGELOG conventions — don't duplicate them here, they drift out of sync. In short: `git add` specific files (never `-A`), prefixed commit message, `Closes #N`, PR, update `CHANGELOG.md`.
