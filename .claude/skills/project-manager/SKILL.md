---
name: project-manager
description: Coralia project status, file map, backlog priority, and workflow for each issue
---

# Project Manager — Coralia

## What is Coralia
Cozy underwater Bubble Shooter (Godot 4 + GDScript). Solo dev: Diego (myappcube). F2P hybrid: Ads + IAP + Battle Pass. Audience: women 25-45 casual. 6 chapters × 10 levels = 60 levels MVP. 6 languages at launch.

## Current state (as of 2026-05-01)
- **Phase 1** ✅ Complete — playable prototype, 5 JSON levels, save system
- **Phase 2** 🔄 In progress — Chunk A (persistence) done, rest in backlog

## Key file locations
```
project.godot                   ← Godot 4 project config + autoload registration
scenes/main/boot.tscn           ← Entry point, routes based on save state
scenes/gameplay/gameplay.tscn   ← Main gameplay scene
scripts/autoloads/              ← 11 global singletons (do NOT add new ones)
scripts/gameplay/               ← grid.gd, grid_logic.gd, bubble.gd, canon.gd, gameplay.gd
data/levels/001-005.json        ← 5 levels (JSON, format: GDD §14.4)
localization/translations.csv   ← 50+ keys in 6 languages (es, en, it, fr, de, pt)
docs/06_Backlog_GitHub_Issues.md ← Source of truth for all issues
docs/02_GDD_Coralia.md          ← Game Design Document (17 sections)
```

## Phase 2 priority order (by impact)
1. **Audio** — music + SFX placeholders (issue #1, size-M, 1-2 days)
2. **Sistema de vidas** — 5 lives, 30-min regen (size-M)
3. **Sistema de monedas + gemas** — drops per level (size-M)
4. **Más niveles** — from 5 to 20+ using AI gen (size-L, 3-5 days)
5. **Onboarding tutorial** — 3-step, first-run only (size-M)
6. **Santuario** — main hub screen (size-XL)
7. **Level Select** — serpentine map (size-L)

## Issue workflow
1. Diego says "trabajemos en el issue N"
2. Read `docs/06_Backlog_GitHub_Issues.md` to find the issue
3. Check dependencies are resolved
4. Read relevant GDD section before coding
5. `git checkout -b issue-N-short-description`
6. Implement per acceptance criteria
7. Test in Godot (run the scene)
8. Commit + push + PR → close issue
9. Update CHANGELOG.md

## What NOT to do
- ❌ No new autoloads (11 exist — extend them)
- ❌ No hardcoded UI strings (use `tr("key")` + translations.csv)
- ❌ No new features outside GDD/backlog without discussing first
- ❌ No binary level formats (JSON only)
- ❌ No mixing features in one commit
