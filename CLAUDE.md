# Coralia — Contexto para Claude Code

Este archivo se carga automáticamente al iniciar una sesión de Claude Code en este repo. Contiene el contexto mínimo que Claude debe conocer para trabajar bien sin re-explicación cada vez.

## Qué es Coralia

**Coralia** es un Bubble Shooter mobile cozy submarino para Android + iOS. Estudio: **myappcube** (Diego). Stack: **Defold + Lua**. Modelo: **F2P híbrido** (Ads + IAP + Battle Pass + Suscripción fase 2). Audiencia: mujeres 25-45 casual.

Protagonista: **Marina** (sirena joven). Antagonista: **La Sombra Profunda** (criatura herida que Marina libera con compasión). Estructura: 6 capítulos × 10 niveles = 60 niveles MVP. 6 idiomas al lanzamiento (es, en, it, fr, de, pt).

## Motor: Defold

Todo el código activo está en **Lua + Defold**. Tipos de archivo clave:
- `.collection` — escena/colección (protobuf text)
- `.go` — game object (lista de componentes)
- `.script` — script Lua adjunto a un game object
- `.gui` — escena GUI (protobuf text)
- `.gui_script` — script Lua adjunto a un GUI
- `.collectionproxy` — proxy de colección (lazy-load de escenas)
- `.atlas` — atlas de sprites
- `game.project` — configuración del proyecto

## Documentos de referencia (orden de lectura)

1. **`docs/07_Status_y_Roadmap.md`** — siempre primero. Dónde estamos y qué queda.
2. **`docs/06_Backlog_GitHub_Issues.md`** — el backlog. Cada H2 es un issue con acceptance criteria.
3. **`docs/02_GDD_Coralia.md`** — GDD completo (17 secciones). Consultar antes de implementar features:
   - Mecánicas → secciones 1-4
   - Economía → sección 6
   - Retención (daily, missions, achievements) → sección 7
   - Battle Pass → sección 8
   - Monetización (ads, IAP) → sección 9
   - UI/pantallas → sección 10
   - Arte → sección 11
   - Audio → sección 12
   - Narrativa (criaturas, antagonista) → sección 13
   - Arquitectura técnica → sección 14
4. **`docs/03_Wireframes_Coralia.md`** — spec textual de las 17 pantallas.
5. **`docs/wireframes/styled_mockups.html`** — mockups visuales estilizados.
6. **`CHANGELOG.md`** — historial de cambios por chunk.

## Estructura de archivos

```
game.project              ← config Defold (1080x1920 portrait, bootstrap, bundles)
input/
  game.input_binding      ← touch + back
main/
  main.collection         ← bootstrap (socket "main")
  main.go                 ← game object con main.script + proxies de escenas
  main.script             ← router: recibe "go_to", maneja disable/unload/async_load
splash1/                  ← socket "splash1" — logo estudio, fade in/out, skip on tap
splash2/                  ← socket "splash2" — logo juego + versión dinámica + dots
level_map/                ← socket "level_map" — scroll inverso por capítulos
gameplay/                 ← (próximo) socket "gameplay" — cañón + grid + HUD
modules/
  config.lua              ← constantes globales (grid, física, colores, economía)
  router.lua              ← router.go(scene_name) → msg a main:/main#main_script
  save_manager.lua        ← save_mgr.load() / .save(data) vía sys.save/sys.load
  level_manager.lua       ← level_mgr.load(id) / .load_all() — caché de JSONs
assets/
  atlas/
    logos.atlas           ← logo.png (1080x1080) + logo_myappcube.png (1024x1024)
    bubbles.atlas         ← 7 colores idle (200x200 px cada uno)
  fonts/
    coralia_ui.font       ← distance field, referencia vera_mo_bd.ttf
  images/logos/           ← PNGs originales de logos
  sprites/bubbles/
    idle/                 ← 7 PNGs 200x200 (extraídos de v1 spritesheets)
    v1/                   ← spritesheets originales 2400x200, 12 frames
data/levels/              ← 001.json–020.json (cap. 1: lvl 1-10, cap. 2: lvl 11-20)
localization/
  translations.csv        ← 6 idiomas (pendiente activar en Defold)
```

## Reglas clave de Defold

### URLs y sockets
- Socket name = el campo `name:` en el `.collection` (ej: `name: "main"` → socket `"main"`)
- URL completa: `socket:/instance_path#component_id`
- Ejemplo: `main:/main#main_script` (socket="main", instance="/main", component="main_script")
- Las paths dentro de una collection son **FLAT**: `children:` solo hereda transform, NO afecta URL path

### IDs y mensajes
- IDs de game objects y URLs NO se pueden pasar dentro de tablas de `msg.post` — se corrompen
- Para responder a quien envió un mensaje: usar `sender` en `on_message`, nunca pasar URL en tabla

### Routing (cómo cambiar de escena)
1. `router.go("scene_name")` → `msg.post("main:/main#main_script", "go_to", { scene = name })`
2. `main.script` recibe `go_to`, hace disable + final + unload al proxy actual
3. Luego async_load al nuevo proxy
4. El proxy responde `proxy_loaded` → main hace `enable`

### Input
- `acquire_input_focus` en **cada** `gui_script` init Y en `main.script` init
- Sin el acquire en main.script, los proxies NO reciben input

### Datos de niveles (JSON)
- Bundled via `game.project` → `custom_resources = /data/`
- Cargar: `sys.load_resource("/data/levels/001.json")` + `json.decode(bytes)`
- Cacheados en `level_manager._cache`

### Atlas y texturas en GUI
- En GUI, textures se referencian como `"atlas_name/image_name"` (sin extensión de imagen)
- Ej: `"logos/logo_myappcube"` (atlas llamado "logos", imagen `logo_myappcube.png`)

## Convenciones de código

- **Naming archivos:** `snake_case` (ej: `level_manager.lua`)
- **Naming variables/funciones:** `snake_case`
- **Constantes:** `SCREAMING_SNAKE_CASE`
- **Módulos:** siempre `local M = {} ... return M`
- **Comentarios:** solo cuando el WHY no es obvio — sin docstrings
- **i18n:** todo string visible → `localization/translations.csv` (pendiente activar)
- **Niveles en JSON:** NUNCA hardcodear niveles en código

## Convenciones cross-proyecto (app-impostor)

- **Two-splash pattern**: splash 1 (studio logo) + splash 2 (game logo + versión + loading) ✅
- **Settings de 4 secciones**: Preferencias / Cuenta y asistencia / Comunidad / Legal
- **3 sliders de audio**: Sonidos del juego / Efectos interfaz / Sonidos pop
- **Vibración** como toggle separado
- **6 idiomas**: es, en, it, fr, de, pt

## Workflow de Git

- **Un commit por chunk/issue**
- **Prefijo en commits**: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `polish:`
- **Branch por issue**: `git checkout -b issue-N-short-description`
- **Mergear a main vía PR**
- **Actualizar CHANGELOG.md** al cerrar cada chunk

## Workflow de issues

1. Diego dice "trabajemos en el issue N" → leer `06_Backlog_GitHub_Issues.md`
2. Verificar dependencias resueltas
3. Leer sección relevante del GDD
4. Branch → implementar según acceptance criteria
5. Probar en Defold (desktop)
6. Commit + push + PR → mergear + cerrar issue
7. Update CHANGELOG.md

## Estado actual del código (2026-05-06)

### Implementado ✅
- Proyecto Defold limpio desde cero (game.project, input binding)
- Módulos reutilizables: config, router, save_manager, level_manager
- Bootstrap: main.collection / main.go / main.script con routing por proxies
- Splash 1: logo estudio, fade in/out 2.7s total, tap to skip
- Splash 2: logo juego, versión dinámica, dots animation, MIN_SHOW 2s
- Level Map: scroll inverso (cap. nuevos arriba), 4 cols, estados locked/open/done dinámicos
- 20 niveles JSON (capítulo 1: lvl 1-10, capítulo 2: lvl 11-20)
- 2 atlases: logos.atlas + bubbles.atlas

### Pendiente (priorizado)
Ver `docs/06_Backlog_GitHub_Issues.md`. Top:
1. Gameplay: cañón + grid hexagonal + match + win/lose
2. Audio: música + SFX placeholders
3. Sistema de vidas (5 vidas, regen 30 min)
4. HUD en gameplay (score, shots, lives)
5. Settings screen

## Cosas a NO hacer

- ❌ NO crear pantallas o features fuera del GDD / backlog
- ❌ NO modificar el GDD sin discutir con Diego
- ❌ NO formatos binarios para niveles (JSON siempre)
- ❌ NO hardcodear strings UI (van a translations.csv)
- ❌ NO commitear: `*.keystore`, `google-services.json`, `GoogleService-Info.plist`
- ❌ NO pasar IDs/URLs de game objects dentro de tablas de msg.post
- ❌ NO paths jerárquicas tipo `/gameplay/hud` — paths en Defold son FLAT

## Cómo Diego prefiere trabajar

- Directo y conciso — no explicar de más
- Confirmar decisiones críticas antes de implementar
- Spanglish OK (mezcla español + términos técnicos en inglés)
- Solo dev sin equipo de arte — soluciones simples, no-artista friendly
- Pushback OK si una decisión técnica parece mal

## Decisiones administrativas pendientes

1. Verificar "Coralia" disponibilidad (App Store, Google Play, dominios, redes)
2. Apple Developer Program ($99/año)
3. Google Play Console ($25 una vez)
4. Firebase project (free tier)
5. AdMob + AppLovin MAX accounts
6. RevenueCat account
7. Repo GitHub privado + push
