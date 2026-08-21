---
name: project-manager
description: Coralia project status, file map, backlog priority, and workflow for each issue
---

# Project Manager — Coralia

## What is Coralia

Cozy underwater Bubble Shooter (Unity 6, C#). Solo dev: Diego (myappcube). F2P hybrid: Ads + IAP + Battle Pass. Audience: women 25-45 casual. 6 chapters × 10 levels = 60 levels MVP. 6 languages at launch (es, en, it, fr, de, pt).

> Engine history: Godot 4 (prototype) → Defold (brief) → Unity 6 (`fb0a6da`, May 2026). All active code today is Unity/C#, under `coralia/`.

## Current state — check live, don't trust a snapshot here

This file used to hardcode "current state as of [date]" and went stale within weeks. Don't repeat that. To find the real current state:

1. `gh issue list --state all` — the actual backlog, with open/closed status. Issues are the source of truth for what's done vs. pending, not this doc.
2. `docs/07_Status_y_Roadmap.md` — periodically-updated status doc (engine-history section is accurate; verify anything specific against the code before trusting it).
3. `CLAUDE.md`'s "Estado actual del código" section — updated less often than issues, but gives a readable summary.
4. `git log --oneline -20` — recent commits, for what actually landed.

## Key file locations (Unity)

```
coralia/Assets/Scenes/{Splash,Home,Game}/          ← .unity scenes
coralia/Assets/Scripts/{Core,UI,Home,Splash,LevelMap,Gameplay,Data}/   ← C# scripts
coralia/Assets/Resources/translations.csv          ← 6-language UI strings
coralia/Assets/Resources/Levels/Chapter_1/2/3/*.json  ← real level data (LevelData.cs)
coralia/Assets/Prefabs/                             ← UI + gameplay prefabs
docs/06_Backlog_GitHub_Issues.md                    ← issue drafts (predates GitHub issues existing; verify against `gh issue list`)
docs/02_GDD_Coralia.md                              ← Game Design Document (17 sections; §14 architecture is Godot-era, don't trust it)
```

## Issue workflow

1. Diego says "trabajemos en el issue N"
2. `gh issue view N` to read it (or `docs/06_Backlog_GitHub_Issues.md` if it predates a real GitHub issue — check dependencies either way)
3. Read the relevant GDD section before coding
4. `git checkout -b issue-N-short-description`
5. Implement per acceptance criteria
6. Test in the Unity Editor (Play mode)
7. Commit (see the `commit` skill for message/branch conventions) + push + PR → `Closes #N` in the PR description
8. Update `CHANGELOG.md`

## What NOT to do

- ❌ No MonoBehaviour singletons for cross-scene state — extend the existing static managers in `Scripts/Core/` (`SaveManager`, `LocaleManager`, `AudioManager`, `SceneLoader`)
- ❌ No hardcoded UI strings — add a key to `Resources/translations.csv`, access via `LocaleManager.Get(key)`
- ❌ No new features outside GDD/backlog without discussing first
- ❌ No binary level formats (JSON only, under `Resources/Levels/Chapter_N/`)
- ❌ No mixing unrelated features in one commit — see the `commit` skill
