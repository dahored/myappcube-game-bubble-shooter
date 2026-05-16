---
name: level-designer
description: Create and validate Coralia levels — JSON format, difficulty curve, objective types, bubble placement
---

# Level Designer — Coralia

## File location
`data/levels/NNN.json` — zero-padded 3 digits (e.g. `006.json`, `020.json`)

## Complete JSON schema
```json
{
  "id": 6,
  "chapter": 1,
  "name": "Nombre del nivel",
  "objective": {
    "type": "clear_all"
  },
  "max_shots": 22,
  "available_colors": ["red", "blue", "yellow"],
  "rainbow_chance": 0.05,
  "bubbles": [
    [col, row, "color"],
    [3, 0, "red"],
    [4, 0, "blue"]
  ],
  "obstacles": []
}
```

## Objective types
| Type | Required fields | Win condition |
|---|---|---|
| `clear_all` | none | All bubbles removed |
| `rescue` | `creature_position: [col, row]`, `creature_id: "string"` | Creature bubble removed |
| `color_count` | `target_color: "red"`, `target_count: 5` | N bubbles of color removed |

## Grid coordinates
- Columns: 0-11 (12 cols), Rows: 0-11 (12 rows, 0 = top)
- Hex grid: odd rows are offset half a cell to the right
- Creature position must match a bubble entry in `bubbles[]`

## Difficulty curve (GDD §2.4)
| Levels | Colors | Max shots | Notes |
|---|---|---|---|
| 1-5 | 2 | 18-25 | Tutorial feel, very easy |
| 6-10 | 2-3 | 20-28 | Intro, slightly harder |
| 11-20 | 3-4 | 22-32 | Medium, first obstacles |
| 21-35 | 3-5 | 25-35 | Hard, pay walls approaching |
| 36-60 | 4-6 | 28-40 | Expert, strategic play required |

## Validation checklist (manual)
- [ ] At least 2 bubbles of each `available_colors` entry exist in `bubbles[]`
- [ ] `creature_position` matches an actual entry in `bubbles[]` (rescue levels only)
- [ ] `max_shots` is achievable — a skilled player should win in ~60-70% of attempts
- [ ] No isolated bubbles (single bubble surrounded by empty space with no matching color reachable)
- [ ] Level is winnable by hand-testing in Godot

## After adding levels
Update `LevelManager.get_total_levels()` to return the new count (or make it auto-detect files in `data/levels/`).

## Colors available
`red`, `blue`, `yellow`, `green`, `purple`, `orange`, `rainbow`

Rainbow bubbles pop any adjacent color — use sparingly (1-2 per level max, `rainbow_chance > 0`).

## Naming convention (chapter names per GDD §2.1)
- Ch. 1 (1-10): Cala Apagada
- Ch. 2 (11-20): Jardín de Anémonas
- Ch. 3 (21-30): Bosque de Algas
- Ch. 4 (31-40): Cueva de Cristales
- Ch. 5 (41-50): Profundidades de Coral
- Ch. 6 (51-60): Ciudad de las Perlas
