# Changelog — Coralia

Todos los cambios significativos del proyecto se registran acá. Formato basado en [Keep a Changelog](https://keepachangelog.com/), versionado semántico cuando lleguemos al lanzamiento.

## [Unreleased]

### Fase 2 — MVP (en progreso)

#### Chunk H — Unity 6: primer loop jugable + WinLosePanel (2026-08-20)
- Migración de plataforma a Unity 6 (`fb0a6da`) — los chunks A-G de abajo corresponden a la implementación en Godot 4, reemplazada por completo. Todo el código activo hoy vive en `coralia/Assets/`.
- **Gameplay loop core (issue #46):** grid hexagonal + cañón + match + win/lose en uGUI puro (sin Physics2D) sobre los 30 niveles reales. `HexGridMath`, `GridController` (flood-fill de match + BFS de drop desde techo), `CannonController` (drag + trayectoria + cola de 2 colores + swap), objetivos `clear_all` y `rescue`.
- **`WinLosePanel`:** victoria / oferta de seguir jugando por gemas (GDD §7, +5 disparos por 15 gemas) / confirmación de abandono (-1 vida) sobre la misma Card.
- **`SaveManager`:** nuevos `Gems` y `Lives` persistidos en PlayerPrefs.
- Sprites de burbujas placeholder reemplazados por el set completo (9 colores).
- Pendiente: agregar `Gameplay.unity` a Build Settings, wiring final de audio/vidas consumiéndose de verdad.

#### Chunk G — Sistema de estrellas 1-2-3 (2026-05-05)
- **Sistema de score final con bonus por eficiencia:** `score_final = score_base + tiros_sobrantes × 10`. Incentiva terminar el nivel en pocos tiros (como "sugar crush" en CC). Constante `BONUS_PER_REMAINING_SHOT = 10` en `gameplay.gd`.
- **`star_thresholds` en todos los JSONs (001-020):** array `[1★, 2★, 3★]` calibrado por nivel. Los thresholds asumen score final (base + bonus).
- **HUD en tiempo real:** `HudStarsLabel` centrado bajo los corazones, muestra `☆☆☆` → `★★☆` → `★★★` mientras el score sube durante el nivel (sin bonus todavía — se aplica al ganar).
- **Pantalla de victoria:** `StarsRow` con 3 etiquetas ★/☆ a 72px. Subtitle muestra desglose `score + bonus = total (récord)` cuando corresponde.
- **Level Select:** nodos completados muestran `★★☆` en dorado en lugar del score numérico.
- **`SaveManager`:** añadidos `stars_for_score(score, thresholds)` (static) y `get_best_stars(level_id)`.
- **GDD §4.2 actualizado:** documenta la decisión de implementar en MVP, mecánica de bonus, y workflow de calibración de niveles. GDD §14.4 actualizado con campo `star_thresholds` en el formato JSON.
- **Workflow de diseño de niveles:** playtestar 3 veces (experto / normal / usando todos los tiros) → los 3 scores finales son los thresholds.

#### Chunk F — Santuario + Level Select + flujo de navegación (2026-05-05)
- `boot.gd` — redirige al Santuario al iniciar (antes iba directo a gameplay)
- `sanctuary.gd` — reescrito con `VBoxContainer + set_anchors_preset(PRESET_FULL_RECT)` para layout correcto en todas las pantallas. Muestra top bar, monedas/gemas/vidas, botón JUGAR, bottom bar.
- `level_select.gd` — reescrito con mismo patrón VBoxContainer. Scroll invertido (nivel 1 abajo, últimos arriba, igual que CC). `LEVELS_PER_CHAPTER = 15` (estándar de la industria). Zigzag de nodos. Scroll automático al nivel actual.
- `gameplay.gd` / `gameplay.tscn` — añadidos botón "← Salir" en HUD (mid-level) y en EndScreen. Reglas de vida: salir mid-level = consume vida; game over → reintentar = consume vida; victoria → siguiente = gratis.
- Reglas documentadas en GDD: `not level_ended` para exit mid-level, `not level_won` para retry (cubre game over sin duplicar al ganar).
- `docs/03_Wireframes_Coralia.md` — actualizado a v0.3 con `bottom_nav` (4 tabs, 160px), Marina caminando el mapa, distribución de 15 niveles por capítulo.
- Capturas de referencia de Candy Crush + Bubble Witch en `docs/other_games_captures/` (8 PNG).
- `docs/08_Arte_Assets_Specs.md` — creado: guía completa de producción de arte para solo dev con AI tools (Midjourney, Kling AI, Figma, Photopea). Priorities 1-4, prompts, especificaciones técnicas, checklist. Incluye sección B5 de iconos UI (estrellas, HUD, Level Select, bottom nav).

#### Chunk E — Más niveles: del 5 al 20 (2026-05-05)
- 15 nuevos niveles JSON en `data/levels/006-020.json` siguiendo la curva del GDD §2.4
- **Capítulo 1 (La Cala Apagada) — niveles 6-10:** wrap-up del capítulo con 2-5 colores, 17-32 burbujas. Progresión: arco simple (clear_all, 3 colores) → muro del cangrejo (rescue, wall level #7) → corriente de medusas → islotes (forma archipiélago) → estrella dormida (pirámide 5 filas, chapter finale)
- **Capítulo 2 (Jardín de Anémonas) — niveles 11-20:** introducción de 4→6 colores y grids más densos. Objetivos mixtos (clear_all y rescue). Wall level estratégico en #15 (Tormenta de burbujas, 30 burbujas con 26 disparos). Chapter finale #20 (El jardín florecido, fila completa de 11 + pirámide hasta criatura en fila 4)
- Criaturas nuevas: cangrejo, medusa, estrella, caballito, pececillo, pulpo, medusita, tortuga, estrella_marina
- `get_total_levels()` detecta archivos dinámicamente — sin cambios de código necesarios
- Validación de bounds: todos los 20 niveles pasan el check de `col < max_cols` y criatura dentro del array de burbujas

#### Chunk A — Persistencia / save-load (2026-05-01)
- `save_manager.gd` — implementación completa: `load_save()`, `save_to_disk()`, migración de versiones, defaults, API de conveniencia (`record_level_completion`, `get_best_score`, `is_level_completed`, `reset_save`)
- Schema de save: `highest_level_completed`, `best_scores` por nivel, `creatures_rescued`, `total_score`, `last_played_level`, `currencies`, settings, etc.
- `boot.gd` — al arrancar carga `highest_level_completed + 1` (siguiente desbloqueado), no `last_played` (anti-redundancia: no recargás un nivel ya ganado)
- `gameplay.gd` — al ganar llama `record_level_completion`; HUD muestra mejor score en el título del nivel; modal de victoria muestra "¡Nuevo récord!" cuando aplica
- `gameplay.tscn` — botón "Reset Save" agregado a Debug Buttons (junto a Prev/Next) para testing
- Save file en `~/Library/Application Support/Godot/app_userdata/Coralia/save_game.json` (Mac) — JSON plain, sin encriptación todavía (placeholder para post-MVP)

### Fase 1 — Prototipo jugable (✅ COMPLETADA 2026-05-01)

#### Chunk 7 — Validation playtest (SKIPPED por decisión del solo dev)
- `docs/05_Playtest_Guide_Coralia.md` — guía completa de playtest con build standalone, reclutamiento, protocolo, matriz de decisión
- `docs/templates/playtest_form_per_tester.md` — formulario por tester
- `docs/templates/playtest_results_summary.md` — template de resumen final
- Pendiente para hacer informalmente antes del global launch

#### Chunk 6 — 5 niveles del prototipo + polish UX (2026-05-01)
- 5 niveles hand-designed en `data/levels/001-005.json` con curva de dificultad creciente (clear_all → rescue progresivo)
- Posiciones de criaturas variadas por fila (top, middle, deep) — antes todas iban a la última fila por accidente
- **Smart queue v2:** threshold de 1+ instancias en grid (antes era 2+ que dejaba colores huérfanos congelados). Comportamiento Candy Crush: siempre ofrece colores que existen en el board.
- **Color shuffle al cargar nivel:** posiciones de burbujas fijas (parte del puzzle), pero los colores en cada posición se randomizan en cada carga. La criatura preserva posición Y color porque define el objetivo.
- **Animación de rotación de cola:** unificada para disparo Y refresh. Cuando se dispara, el next se desliza al cañón creciendo y una nueva burbuja aparece en preview con fade-in. Cuando un color queda inválido, el current cae fuera + fade-out + shrink, después next se desliza igual. ~0.4s.
- UX: botón del modal cambia de "Reintentar" a "Siguiente nivel →" al ganar (si hay próximo nivel)
- Refactor: `_animate_queue_advance(new_current_color, new_next_color, drop_current)` unifica las dos rutas de animación

#### Chunk 5 — JSON Level Loader (2026-05-01)
- `data/levels/001.json` — primer nivel con objective `rescue`
- `level_manager.gd` autoload con `load_level()`, validación, `get_total_levels()`
- `grid.gd` — `setup_from_level(data)` reemplaza spawn random
- `bubble.gd` — flag `is_creature` con renderizado de estrella dorada
- `canon.gd` — `configure_playable_types()` para colores específicos del nivel
- `gameplay.gd` — pipeline de carga: GameManager.current_level_id → LevelManager.load_level() → configura todo
- `gameplay.tscn` — `LevelLabel`, `DebugButtons` (Prev/Next) para navegación de niveles

#### Chunk 4 — Win/Lose Conditions (2026-05-01)
- `gameplay.gd` reescrito como root coordinator: `shots_remaining` (hardcoded 25 inicial), detección win/lose
- `canon.gd` — flag `level_active`; signal `shot_fired`
- `grid.gd` — signal `state_settled` tras cada disparo aterrizado
- `gameplay.tscn` — HUD ampliado con `ShotsLabel`, `ObjectiveLabel`, `EndScreen` modal
- Fix: colores de fuente en Labels (eran blancos sobre crema → invisibles)

#### Chunk 3 — Match Detection + Drops (2026-05-01)
- `bubble.gd` — `explode()` (tween scale + fade), `start_falling()` (gravity), estado `DROPPING`
- `grid.gd` — `find_connected_same_color()` (flood-fill), `find_floating_bubbles()` (BFS desde techo)
- Score: 10 pts por burbuja explotada + 15 pts por caída (1.5x bonus)
- Pipeline: `add_landed_bubble` → `_process_matches_and_drops` con delay 0.15s entre explosión y drops

#### Chunk 2 — Cañón y disparo (2026-05-01)
- `canon.gd` (nuevo) — input drag-vs-tap, trayectoria con primer rebote, cola de 2 burbujas, color swap por tap
- `canon.tscn` (nuevo) — Line2D + 2 Bubble instances + concha azul placeholder
- `bubble.gd` — state machine (IDLE/IN_FLIGHT/IN_GRID), `launch()`, `_update_flight()` con colisión y rebote
- Fix: `_input` en vez de `_unhandled_input` (ColorRect background consumía eventos)
- Fix: removido `.bind()` en signal connect

#### Chunk 1 — Grid hexagonal (2026-05-01)
- `grid_logic.gd` — math puro del hex grid (grid_to_pixel, pixel_to_grid, get_neighbors)
- `bubble.gd` (nuevo) — class Bubble con `_draw()` + 7 tipos
- `grid.gd` (nuevo) — spawnea 84 burbujas (8 filas × 11/10)
- `gameplay.tscn` (nuevo) — escena raíz con Background + Grid + HUD

### Fase 0 — Pre-producción (✅ COMPLETADA 2026-04-30)

#### Setup técnico inicial (Tarea #5)
- `project.godot` — mobile portrait 1080×1920, renderer `gl_compatibility`, 11 autoloads, emulate_touch_from_mouse
- 11 autoload stubs en `scripts/autoloads/`
- `localization/translations.csv` con 50+ keys en 6 idiomas
- `boot.tscn` + `boot.gd` placeholder
- `.gitignore`, `README.md`, `icon.svg`

#### Documentos de Fase 0
- `docs/01_Concepto_Inicial.md` (v0.3) — visión, decisiones lockeadas, convenciones cross-proyecto
- `docs/02_GDD_Coralia.md` (v0.6) — GDD con 17 secciones
- `docs/03_Wireframes_Coralia.md` (v0.2) — spec de las 17 pantallas
- `docs/04_Plan_Fase1_Coralia.md` (v0.1) — plan de Fase 1 (7 chunks)

#### Decisiones lockeadas durante Fase 0
- Nombre: **Coralia** · Estudio: **myappcube**
- Tema: cozy underwater · Protagonista: Marina · Antagonista: La Sombra Profunda
- Estructura: 6 capítulos × 10 niveles = 60 niveles MVP
- F2P híbrido: Ads + IAP + Battle Pass + Suscripción (fase 2)
- 6 idiomas al MVP (es, en, it, fr, de, pt) con pipeline AI a $0
- Light/Dark/Auto themes (Modo Arrecife / Modo Profundidades)
- Convenciones cross-proyecto con app-impostor: 2 splashes, Settings de 4 secciones, 3 sliders de audio + vibración
