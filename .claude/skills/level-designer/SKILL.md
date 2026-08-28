---
name: level-designer
description: Create and validate Coralia levels — JSON format, difficulty curve, objective types, bubble placement
---

# Level Designer — Coralia

## File location

`coralia/Assets/Resources/Levels/Chapter_N/NNN.json` — chapter folder (`Chapter_1`, `Chapter_2`, `Chapter_3`, ...), zero-padded 3-digit filename (e.g. `006.json`, `020.json`). Filename doesn't strictly matter for loading (`LevelLoader.LoadById(id)` matches by the `id` field inside the JSON, not the filename), but keep the convention for readability.

## Complete JSON schema

Matches `LevelData.cs` (`coralia/Assets/Scripts/Data/LevelData.cs`) exactly — `bubbles` is an array of **objects**, not `[col, row, color]` arrays:

```json
{
  "id": 6,
  "chapter": 1,
  "name": "Nombre del nivel",
  "objective": { "type": "clear_all" },
  "max_shots": 22,
  "min_shots_to_clear": 17,
  "available_colors": ["red", "blue", "yellow"],
  "rainbow_chance": 0.05,
  "bubbles": [
    { "row": 0, "col": 3, "color": "red" },
    { "row": 0, "col": 4, "color": "blue" }
  ],
  "obstacles": [],
  "star_thresholds": [40, 100, 200]
}
```

## Objective types

| Type | Required fields | Win condition |
|---|---|---|
| `clear_all` | none | All bubbles removed |
| `rescue` | `creature_position: [row, col]`, `creature_id: "string"` | The bubble at that exact cell gets removed (by direct match or by the ceiling-drop chain) |

`creature_position` must match an actual entry in `bubbles[]` at that `row`/`col`.

## Grid coordinates

- `col`: even rows (0, 2, 4, ...) allow `0`–`9` (10 columns); odd rows allow `0`–`8` (9 columns, shifted half a bubble-diameter right). Going outside this range doesn't error, but `HexGridMath` silently clamps the bubble to the nearest valid column — it won't render where you put it in the JSON, so stay in range.
- `row`: no hard maximum in code. Practically, keep initial layouts to roughly **10-12 rows** so the level doesn't start visually crowded against the cannon (see `docs/02_GDD_Coralia.md` for the actual vertical layout budget if it changes).
- Row `0` is the ceiling. For the level's starting bubbles to correctly register as "connected to the ceiling" (so the drop-chain logic works), at least one bubble per column-run should originate at row `0` — a level that starts at row `2`+ with nothing at row `0` means the game considers *nothing* ceiling-connected, so the first match drops the entire level at once. (This was a real bug found and fixed during initial gameplay-loop testing — verify row `0` has content before shipping a level.)
- Bubbles that land from missed shots stick permanently — the grid can grow further down than what the JSON defines as the player plays. That's expected classic bubble-shooter behavior, not something to design around.

## min_shots_to_clear y star_thresholds

`min_shots_to_clear` es el mínimo de disparos para completar el nivel jugando óptimo — se define **jugando el nivel a mano** (o simulándolo), no por fórmula. `max_shots - min_shots_to_clear` es el margen real de la dificultad: mucho margen = nivel fácil, margen justo = nivel difícil. Es el mismo criterio de la tabla de tasa de éxito de arriba, pero explícito en el JSON en vez de quedar solo en la cabeza de quien diseñó el nivel.

Una vez que `min_shots_to_clear` está definido, `star_thresholds` sale de una referencia de score ideal:

```
score_ideal = burbujas × 10 + max(0, max_shots − min_shots_to_clear) × 10
star_thresholds = [40%, 65%, 90%] de score_ideal
```

(Los pesos 10/10 son `SCORE_PER_POP`/`SCORE_PER_REMAINING_SHOT` de `GameplayController.cs` — mantenerlos sincronizados si esos valores cambian.) Mientras `min_shots_to_clear` siga en `0` (no calibrado), el fallback usa `burbujas` en su lugar — menos preciso, ya que no contempla cadenas largas, pero sirve de placeholder.

Los tres cortes (40/65/90%) también son el 100% de `ProgressScoreView` — la barra de score en vivo del HUD de gameplay. Su 3er umbral (90%) es exactamente el `STAR_3_RATIO_OF_BAR` hardcodeado en `ProgressScoreView.cs`; si estos porcentajes cambian acá, hay que actualizar esa constante también para que las estrellas sigan cayendo justo donde están los `Field1/2/3` puestos a mano en el prefab.

## Difficulty curve (GDD §2.4 and §2.5 — read those sections directly, this is a summary, not the source of truth)

| Tramo | Niveles | Tasa de éxito target |
|---|---|---|
| Onboarding | 1-10 | 90-95% |
| Introducción | 11-25 | 70-80% |
| Media | 26-40 | 50-60% |
| Difícil | 41-55 | 30-40% |
| Climax | 56-60 | 20-30% |

Shots per chapter (GDD §2.5): regla general `disparos disponibles = disparos óptimos × 1.3`. Chapter 1: 18-25, Chapter 2: 22-28, Chapter 3: 25-32, Chapter 4: 28-35, Chapter 5: 30-38, Chapter 6: 32-42.

## Colors available

`red`, `blue`, `yellow`, `green`, `purple`, `orange`, `rainbow` — these are the 7 colors `GridController` currently has sprites wired for. (The bubble sprite pack has more colors available — `black`, `brown`, `dark_blue`, `dark_grey`, `grey`, `mint_green`, `pink`, `red_wine` — but they aren't wired into `GridController` yet; see issue #48 before using them in a level.)

Rainbow bubbles match any adjacent color — use sparingly (1-2 per level max, `rainbow_chance` a small value like `0.0`-`0.08`).

## Validation checklist (manual)

- [ ] At least 2 bubbles of each `available_colors` entry exist in `bubbles[]` (otherwise a color can appear in the shooter queue with nothing to match)
- [ ] `creature_position` matches an actual entry in `bubbles[]` (rescue levels only)
- [ ] `max_shots` is achievable — a skilled player should win in roughly the target success rate for that level range (see table above)
- [ ] `min_shots_to_clear` is set from actual hand-testing (not left at `0`), and `star_thresholds` recalculated from it (see the section above)
- [ ] No isolated bubbles (a single bubble surrounded by empty space with no matching color reachable)
- [ ] Row `0` has bubbles (see the ceiling-connectivity note above)
- [ ] `col` values are within the valid range for that row's parity (0-9 even, 0-8 odd)
- [ ] Level is winnable by hand-testing in the Unity Editor (Play mode, `Gameplay.unity`, with `PlayerPrefs["selected_level"]` set to this level's `id`)

## Naming convention (chapter names per GDD §2.1)

- Ch. 1 (1-10): Cala Apagada
- Ch. 2 (11-20): Jardín de Anémonas
- Ch. 3 (21-30): Bosque de Algas
- Ch. 4 (31-40): Cueva de Cristales
- Ch. 5 (41-50): Profundidades de Coral
- Ch. 6 (51-60): Ciudad de las Perlas
