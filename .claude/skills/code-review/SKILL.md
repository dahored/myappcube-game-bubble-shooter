---
name: code-review
description: GDScript/Godot 4 code review checklist specific to Coralia conventions
---

# Code Review — Coralia (Godot 4 / GDScript)

## GDScript conventions
- [ ] Static typing used where possible: `var lives: int = 5`, `func foo(x: int) -> String:`
- [ ] `class_name PascalCase` declared in files that are referenced by other scripts
- [ ] Variables: `snake_case`, Constants: `SCREAMING_SNAKE_CASE`, Signals: past-tense verb (`level_completed`, `bubble_popped`)
- [ ] Files: `snake_case.gd`
- [ ] No multi-paragraph docstrings — one short `##` line for public functions with 3+ lines

## Strings & i18n
- [ ] **Zero hardcoded UI strings** — everything visible to the player uses `tr("key.path")` with a matching key in `localization/translations.csv`
- [ ] Signal text and modal text (win/lose) must also be i18n'd
- [ ] Print statements (`[Manager] message`) are OK for debug, not for UI

## Architecture
- [ ] No new autoloads created (use the 11 in `scripts/autoloads/`)
- [ ] Cross-autoload communication goes through `GameManager` signals, not direct calls
- [ ] Levels use JSON in `data/levels/` — no hardcoded level data in code
- [ ] No binary formats for data (only JSON, CSV, SVG)

## Economy / persistence
- [ ] Currency changes always go through `EconomyManager` (not direct `SaveManager.data` writes)
- [ ] `SaveManager.save_to_disk()` called after every state change that must survive a crash
- [ ] `_default_save()` in `save_manager.gd` updated when new save keys are added

## Signals & nodes
- [ ] Signals connected in `_ready()`, not in `_init()`
- [ ] `@onready` used for node refs, not `get_node()` calls in functions
- [ ] `$Path/To/Node` only in the scene that owns those nodes

## Mobile considerations
- [ ] Touch input used (not mouse input) for any new interactive elements
- [ ] No hardcoded pixel sizes — use anchors/containers for layout
- [ ] `Input.vibrate_handheld()` goes through `AudioManager.vibrate()`, not called directly

## Debug code
- [ ] Debug buttons (Prev/Next/Reset in gameplay.tscn) must remain isolated — remove or hide in production builds
- [ ] No `reset_save()` calls reachable from normal gameplay flow
