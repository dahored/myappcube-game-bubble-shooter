---
name: implement-feature
description: Step-by-step guide to implement any Phase 2 feature in Coralia following established patterns
---

# Implement Feature — Coralia

Use this skill when starting work on a new issue from `docs/06_Backlog_GitHub_Issues.md`.

## Pre-implementation checklist
1. **Read the issue** in `docs/06_Backlog_GitHub_Issues.md` — find the H2 section, note the acceptance criteria
2. **Read the relevant GDD section** in `docs/02_GDD_Coralia.md` (the issue body references the exact section)
3. **Check dependencies** — does this feature need another issue to be done first?
4. **Create branch**: `git checkout -b issue-N-short-description`

## Implementation patterns by feature type

### Adding a new screen
1. Create scene at `scenes/CATEGORY/screen_name.tscn`
2. Create script at `scripts/CATEGORY/screen_name.gd` with `extends Control` (or Node2D)
3. Register transitions from `boot.gd` or the calling scene
4. All labels/buttons text via `tr("ui.screen.key")` + add key to `localization/translations.csv`

### Extending an autoload (e.g. EconomyManager, AudioManager)
1. Add the function to the existing `.gd` file in `scripts/autoloads/`
2. If it needs new save data, add the key to `SaveManager._default_save()` and update `_migrate()`
3. Emit a signal so other systems can react (define signal at top of the autoload)
4. Wire the signal connection in `GameManager._ready()` if cross-system

### Adding audio (AudioManager)
- Music files: `assets/audio/music/track_name.ogg`
- SFX files: `assets/audio/sfx/sfx_name.ogg`
- Call `AudioManager.play_music("track_name")` or `AudioManager.play_sfx("sfx_name", AudioManager.AudioCategory.BUBBLE_POP)`
- Volumes read from `SaveManager.data.settings.music_volume / ui_volume / pop_volume`

### Adding a new save field
```gdscript
# In save_manager.gd _default_save():
"new_field": default_value,

# In _migrate():
migrated["new_field"] = old_data.get("new_field", default_value)
```

### Adding i18n keys
In `localization/translations.csv`, add a row:
```
key,es,en,it,fr,de,pt
ui.button.play,"Jugar","Play","Gioca","Jouer","Spielen","Jogar"
```
Then in GDScript: `label.text = tr("ui.button.play")`

## Testing before commit
- Run the scene in Godot (F5 or the play scene button)
- Test win condition AND lose condition
- Test with `SaveManager.reset_save()` (fresh start — use debug reset button in gameplay)
- Test the debug Prev/Next buttons still work

## Commit & close
```bash
git add scripts/... scenes/... localization/...
git commit -m "feat: short description of what was implemented

Closes #N

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
git push origin issue-N-short-description
# Then open PR on GitHub
```

Update `CHANGELOG.md` with a brief bullet under the correct phase.
