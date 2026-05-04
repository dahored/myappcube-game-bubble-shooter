# Changelog — Coralia

Todos los cambios significativos del proyecto se registran acá. Formato basado en [Keep a Changelog](https://keepachangelog.com/), versionado semántico cuando lleguemos al lanzamiento.

## [Unreleased]

### Fase 1 — Prototipo jugable (en progreso)

#### Chunk 5 — JSON Level Loader (2026-05-01)
- `data/levels/001.json` — primer nivel definido en JSON con objective `rescue`, criatura "coqui" en (5, 0), 15 disparos, 4 colores
- `level_manager.gd` autoload con implementación completa: `load_level()`, validación de campos requeridos, `get_total_levels()` que cuenta archivos en `data/levels/`
- `grid.gd` — reemplazo de `spawn_initial_grid()` por `setup_from_level(data)` que parsea el JSON y construye el grid; mapeo `COLOR_STR_TO_TYPE` para colores
- `bubble.gd` — flag `is_creature` con renderizado de estrella dorada 5-puntas como marcador de objetivo de rescate
- `canon.gd` — método `configure_playable_types()` para reconfigurar colores jugables según el nivel
- `gameplay.gd` — pipeline de carga: lee `GameManager.current_level_id` → `LevelManager.load_level()` → configura grid + cañón + HUD; soporte de objective types (`clear_all` y `rescue`)
- `gameplay.tscn` — agregados `LevelLabel` (top-right, debajo de Chunk label), `DebugButtons` (Prev / Next) para navegar entre niveles
- `game_manager.gd` — default `current_level_id = 1` (antes era 0)

#### Chunk 4 — Win/Lose Conditions (2026-05-01)
- `gameplay.gd` reescrito como root coordinator: tracking de `shots_remaining` (hardcoded 25), detección win (grid vacío) / lose (sin disparos)
- `canon.gd` — flag `level_active` separado de `can_shoot`; signal `shot_fired`
- `grid.gd` — signal `state_settled` que se emite tras cada disparo aterrizado (con o sin match) para evaluación de win/lose
- `gameplay.tscn` — HUD ampliado con `ShotsLabel`, `ObjectiveLabel`, `EndScreen` modal con título + subtítulo + botón Reintentar
- Colores y font sizes correctos en todos los Labels (antes blancos sobre fondo crema → invisibles)

#### Chunk 3 — Match Detection + Drops (2026-05-01)
- `bubble.gd` — métodos `explode()` (tween scale up + fade out) y `start_falling()` (gravity-based drop con variación horizontal random); estado `DROPPING` agregado
- `grid.gd` — algoritmos:
  - `find_connected_same_color()`: flood-fill BFS desde la celda donde aterrizó la burbuja
  - `find_floating_bubbles()`: BFS desde fila 0 (techo); cualquier celda no alcanzada es flotante
  - `_explode_group()`: anima explosión + suma score (10 pts/burbuja)
  - `_drop_floating_bubbles()`: gravity drop con bonus de 15 pts (1.5x)
  - Pipeline `add_landed_bubble` → `_process_matches_and_drops` con delay de 0.15s entre explosión y drops para legibilidad visual
- `gameplay.gd` (nuevo) — wirea `Grid.score_changed` → `HUD/ScoreLabel`
- `gameplay.tscn` — agregado `ScoreLabel` en HUD

#### Chunk 2 — Cañón y disparo (2026-05-01)
- `canon.gd` (nuevo) — sistema de input drag-vs-tap, dirección de apuntado, línea de trayectoria con primer rebote calculado en coords locales del cañón, cola de 2 burbujas (current + next), color swap por tap
- `canon.tscn` (nuevo) — Node2D con `Line2D` + dos instancias de Bubble (current + next preview); concha azul placeholder dibujada con `_draw()`
- `bubble.gd` — extensión con state machine (IDLE / IN_FLIGHT / IN_GRID), `launch()`, `_update_flight()` con rebote en paredes y detección de colisión con grid bubbles, signal `landed`, `prev_position` para snap correcto
- `grid.gd` — método `add_landed_bubble()` que recibe la burbuja en vuelo, calcula celda destino vía `pixel_to_grid` desde `prev_position`, busca vecino vacío si la celda está ocupada, re-parenta del Gameplay root al Grid container
- `gameplay.tscn` — instancia el `Canon`; cambio de `Background.mouse_filter` a Ignore (antes consumía los inputs antes del cañón)
- Fix: `_input` en lugar de `_unhandled_input` en cañón (los Controls como ColorRect consumen `_unhandled_input` por default)
- Fix: removido `.bind(b)` en signal connect (causaba "method expected 1, called with 2")

#### Chunk 1 — Grid hexagonal (2026-05-01)
- `grid_logic.gd` — math puro del hex grid: `grid_to_pixel`, `pixel_to_grid`, `get_neighbors` (hasta 6 vecinos hexagonales, con offset para filas pares vs impares), `is_valid_cell`
- `bubble.gd` (nuevo) — class `Bubble` con `_draw()` que renderiza círculo de color con highlight superior izquierdo y outline; 7 tipos (RED, BLUE, GREEN, YELLOW, PURPLE, ORANGE, RAINBOW)
- `bubble.tscn` (nuevo) — Node2D simple con script
- `grid.gd` (nuevo) — `Grid` class que spawnea 84 burbujas random (8 filas × 11/10 cols alternadas), Dictionary `bubbles` con key `Vector2i(col, row)`
- `gameplay.tscn` (nuevo) — escena raíz con Background ColorRect crema + Grid container + HUD CanvasLayer
- `boot.gd` — actualizado para saltar onboarding/santuario y cargar gameplay.tscn directo durante Fase 1

### Fase 0 — Pre-producción (completada 2026-04-30)

#### Setup técnico inicial (Tarea #5)
- `project.godot` — configuración mobile portrait 1080×1920 con renderer `gl_compatibility`, 11 autoloads registrados, `pointing/emulate_touch_from_mouse=true`
- 11 autoload stubs en `scripts/autoloads/`: GameManager, AudioManager, SaveManager, EconomyManager, BattlePassManager, AdsManager, IAPManager, AnalyticsManager, FirebaseManager, LevelManager, LocaleManager
- `localization/translations.csv` con 50+ keys starter en 6 idiomas (es, en, it, fr, de, pt)
- `boot.tscn` + `boot.gd` placeholder
- `.gitignore` para Godot 4 + secrets, `README.md`, `icon.svg` placeholder
- Estructura de carpetas según GDD sección 14.2

#### Documentos de Fase 0
- `docs/01_Concepto_Inicial.md` (v0.3) — visión, premisa, decisiones lockeadas, convenciones cross-proyecto con Impostor app
- `docs/02_GDD_Coralia.md` (v0.6) — Game Design Document completo con 17 secciones: mecánicas, niveles, power-ups, progresión, santuario, economía, retención, Battle Pass, monetización, UI/pantallas (17 screens), arte, audio, narrativa (12 criaturas hero + Sombra Profunda antagonista), stack técnico, analytics, roadmap post-MVP, apéndices
- `docs/03_Wireframes_Coralia.md` (v0.2) — spec detallado de las 17 pantallas con framework, layouts ASCII, componentes reutilizables
- `docs/04_Plan_Fase1_Coralia.md` (v0.1) — plan de los 7 chunks de Fase 1 con duración, deliverables y dependencias

#### Decisiones lockeadas durante Fase 0
- Nombre del juego: **Coralia**, estudio: **myappcube**
- Tema: cozy underwater, protagonista Marina (sirena joven), antagonista La Sombra Profunda (no villano — herida)
- Estructura: 6 capítulos × 10 niveles = 60 niveles MVP
- Modelo F2P híbrido: Ads + IAP + Battle Pass + Suscripción (fase 2)
- 6 idiomas al MVP (es, en, it, fr, de, pt) con pipeline AI a $0
- Tema visual: Modo Arrecife (claro) + Modo Profundidades (oscuro) + Automático
- Convenciones cross-proyecto con app-impostor: 2 splashes, Settings de 4 secciones, 3 sliders de audio + vibración
