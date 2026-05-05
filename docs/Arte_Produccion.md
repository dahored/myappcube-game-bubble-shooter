# Arte — Guía de Producción Completa

**Versión:** 1.0 — 2026-05-05
**Autor:** Diego (myappcube)
**Referencia principal:** GDD §11 (Arte y dirección visual)

Este documento es la fuente única de verdad para toda la producción visual de Coralia. Úsalo como:
1. Tracker de progreso (marcar ✅ conforme se va completando)
2. Brief para freelancers (secciones de brief están marcadas)
3. Spec técnico para integración en Godot

---

## Dirección visual (resumen ejecutivo para freelancers)

**Estilo:** Cozy, hand-drawn-looking, ilustración suave 2D.
**Influencias:** Animal Crossing: New Horizons, Stardew Valley, Sky: Children of the Light, Studio Ghibli (escenas submarinas).
**NO queremos:** Disney princess hiperestilizado, realismo, low-poly, pixel art, anime kawaii.
**Identidad:** Dulce y luminosa pero no infantil. Mágica pero no fantasy genérica. Cute pero no chibi extremo.

**Paleta de colores base:**
| Token | Hex | Uso principal |
|---|---|---|
| coral_pink | #F4A6A0 | Acentos cálidos, coral del santuario |
| coral_deep | #D87B7B | Sombras, botones activos |
| seafoam | #A8E0D5 | Agua media, fondos UI |
| pearl_white | #FBF6E9 | Backgrounds claros, perlas |
| aqua_deep | #4A8FB7 | Agua profunda, modo oscuro |
| bubble_blue | #7EC9E2 | Burbuja azul |
| bubble_yellow | #F9D85E | Burbuja amarilla |
| bubble_green | #9ED48A | Burbuja verde |
| bubble_purple | #B59FD9 | Burbuja morada |
| bubble_red | #EE7A7A | Burbuja roja |
| bubble_orange | #F0A060 | Burbuja naranja (definir con concept) |
| gold_treasure | #E5BE5C | Gemas, Battle Pass, hitos |
| dark_overlay | #0F2238 | Overlays, modo oscuro, derrota |

**Tipografías:**
- Títulos/branding: **Quicksand Bold** (Google Fonts, free)
- Cuerpo/UI: **Nunito Regular + Nunito Black** (Google Fonts, free)

---

## Issues de GitHub — resumen

| # | Issue | Prioridad | Tamaño | Estado |
|---|---|---|---|---|
| **#34** | Design system Godot — tipografías, paleta, theme global | HIGH | S | ⬜ Pendiente |
| **#35** | Sprites de burbujas y cañón (gameplay core) | HIGH | M | ⬜ Pendiente |
| **#36** | Marina protagonista — diseño base + 6 animaciones | HIGH | L | ⬜ Pendiente |
| **#37** | 12 criaturas hero + criaturas comunes | MEDIUM | XL | ⬜ Pendiente |
| **#38** | Backgrounds gameplay + santuario + cinemáticas | MEDIUM | XL | ⬜ Pendiente |
| **#39** | Logo Coralia + branding + app icons | HIGH | M | ⬜ Pendiente |
| **#40** | Efectos de partículas y UI completa | MEDIUM | L | ⬜ Pendiente |
| **#9** | UI polish (depende de #34) | MEDIUM | L | ⬜ Pendiente |

**Orden de implementación recomendado:**
1. #34 (design system) — base para todo, solo Diego/Claude
2. #39 (logo + branding) — blocker para stores
3. #35 (burbujas + cañón) — gameplay core, se ve en cada partida
4. #36 (Marina) — identidad del juego, necesaria para onboarding (#7) y santuario (#10)
5. #9 (UI polish) — depende de #34
6. #40 (partículas + UI icons) — feedback visual
7. #37 (criaturas) — corazón emocional, necesaria para santuario completo
8. #38 (backgrounds + cinemáticas) — inmersión y momentos WOW

---

## Directorio de assets — estructura completa

```
assets/
├── branding/
│   ├── logo_coralia_master.svg
│   ├── logo_coralia_1024.png
│   ├── logo_coralia_horizontal.png
│   ├── logo_coralia_white.png
│   ├── logo_myappcube_master.svg
│   └── logo_myappcube_1024.png
│
├── fonts/
│   ├── Quicksand-Bold.ttf
│   ├── Nunito-Regular.ttf
│   └── Nunito-Black.ttf
│
├── sprites/
│   ├── bubbles/
│   │   ├── bubble_red.png          (96×96 px)
│   │   ├── bubble_blue.png
│   │   ├── bubble_yellow.png
│   │   ├── bubble_green.png
│   │   ├── bubble_purple.png
│   │   ├── bubble_orange.png
│   │   ├── bubble_rainbow.png
│   │   └── bubble_ice.png          (obstáculo futuro)
│   │
│   ├── cannon/
│   │   ├── cannon_idle.png
│   │   ├── cannon_shoot.png        (o sprite sheet)
│   │   └── cannon_base.png
│   │
│   ├── characters/
│   │   ├── marina/
│   │   │   ├── marina_idle.tres    (SpriteFrames)
│   │   │   ├── marina_shoot.tres
│   │   │   ├── marina_victory.tres
│   │   │   ├── marina_defeat.tres
│   │   │   ├── marina_rescue.tres
│   │   │   └── marina_greet.tres
│   │   └── shadow/                 (antagonista — 6 estados)
│   │       ├── shadow_ch1.png
│   │       ├── ... shadow_ch6.png
│   │
│   ├── creatures/
│   │   ├── hero/                   (12 criaturas, SpriteFrames .tres)
│   │   │   ├── coqui.tres
│   │   │   ├── burbujin.tres
│   │   │   └── ... (12 total)
│   │   └── common/                 (10-15 criaturas sin nombre)
│   │       └── ...
│   │
│   ├── powerups/                   (6 iconos 128×128 px)
│   │   ├── icon_powerup_bomb.png
│   │   ├── icon_powerup_ray.png
│   │   ├── icon_powerup_color.png
│   │   ├── icon_powerup_laser.png
│   │   ├── icon_powerup_fish.png
│   │   └── icon_powerup_air.png
│   │
│   ├── obstacles/                  (6 overlays 96×96 px)
│   │   ├── icon_obs_ice.png
│   │   ├── icon_obs_cage.png
│   │   ├── icon_obs_sticky.png
│   │   ├── icon_obs_generator.png
│   │   ├── icon_obs_bomb.png
│   │   └── icon_obs_chain.png
│   │
│   └── hud/
│       ├── hud_heart_full.png      (32×32)
│       ├── hud_heart_empty.png
│       ├── hud_coin.png
│       ├── hud_gem.png
│       ├── hud_shots.png
│       ├── hud_timer.png
│       ├── icon_settings.png
│       ├── icon_profile.png
│       ├── icon_shop.png
│       ├── icon_battle_pass.png
│       ├── icon_daily.png
│       ├── icon_lock.png
│       ├── icon_star_full.png
│       ├── icon_star_empty.png
│       └── badge_new.png
│
├── backgrounds/
│   ├── gameplay/
│   │   ├── bg_gameplay_ch1.png     (1080×1920 px)
│   │   ├── bg_gameplay_ch2.png
│   │   ├── bg_gameplay_ch3.png
│   │   ├── bg_gameplay_ch4.png
│   │   ├── bg_gameplay_ch5.png
│   │   └── bg_gameplay_ch6.png
│   └── sanctuary/
│       ├── bg_sanctuary_ch1_dark.png
│       ├── bg_sanctuary_ch1_lit.png
│       ├── ... (×6 = 12 archivos total)
│
├── animations/
│   └── restoration/
│       ├── restore_ch1.ogv         (o escena Godot)
│       ├── ... restore_ch6.ogv
│
└── ui/
    ├── store/
    │   ├── screenshot_01.png       (1290×2796 iOS)
    │   ├── screenshot_02.png
    │   ├── screenshot_03.png
    │   └── feature_graphic.png     (1024×500 Android)
    └── icons/
        (íconos de app — generados por Godot export)
```

---

## Brief para freelancer — Marina

**Título del encargo:** Diseño de personaje + 6 animaciones para juego mobile cozy

**Descripción del personaje:**
Marina es una sirena joven (~16-18 años), habitante del arrecife, cuidadora y exploradora. Es cálida, optimista, empática y ligeramente juguetona. Su motivación es restaurar el arrecife que ama.

**Especificaciones visuales:**
- Cabello: ondulado, color coral pálido (#F4A6A0) o turquesa suave (#A8E0D5) — presentar ambas opciones
- Ojos: grandes, expresivos, color verde marino
- Atuendo: top de algas tejidas o conchas pequeñas, modesto, sin escote pronunciado
- Cola: degradado de turquesa a coral (#7EC9E2 → #F4A6A0) con detalles bioluminiscentes sutiles
- Accesorios: flor de coral en el cabello, brazaletes de perlas
- Postura: relajada y cálida, no heroica ni agresiva

**Estilo de referencia:**
- SÍ: Stardew Valley characters, Studio Ghibli (Ponyo, Nausicaä), Animal Crossing: New Horizons, Spiritfarer
- NO: Disney Princess, chibi extremo, anime estilo shonen, realismo

**Animaciones requeridas (6):**
1. **idle** — flotando suavemente, cabello moviéndose, ~6-8 frames, loop
2. **shoot** — gestura del cañón (lanzar burbuja), ~4 frames
3. **victory** — giro alegre con burbujitas alrededor, ~8 frames
4. **defeat** — suspiro suave, expresión resignada pero no triste, ~4 frames
5. **rescue** — abraza con ternura a una criatura, ~6 frames
6. **greet** — saludo al abrir el juego (wink, wave), ~4 frames

**Entregables:**
- PNG con transparencia para cada frame (o sprite sheet horizontal)
- Tamaño base: ~350-450px de alto (portrait mobile)
- Formato: PNG + archivo fuente (PSD/Procreate/Aseprite)

**Paleta del juego:**
#F4A6A0 coral_pink | #A8E0D5 seafoam | #FBF6E9 pearl_white | #4A8FB7 aqua_deep | #7EC9E2 bubble_blue

**Presupuesto estimado:** $80-150 USD total con las 6 animaciones

---

## Brief para freelancer — Criaturas hero

**Título del encargo:** 12 criaturas marinas para juego mobile cozy (mismo estilo que Marina)

**Contexto:**
Las criaturas son rescatadas por Marina y se unen a su santuario. Cada una tiene personalidad propia. Deben ser memorables, entrañables, y coherentes con el estilo de Marina.

**Estilo:**
Mismo artista que Marina o brief idéntico de estilo. Cozy/Ghibli, no chibi extremo, no kawaii genérico.

**Especificaciones generales:**
- Tamaño: 128×128 px para uso en gameplay/grid; 256×256 px para santuario/bestiario
- Animación idle: 4-6 frames, loop suave
- PNG con transparencia

**Lista de criaturas (Capítulo 1 primero):**

| Nombre | Especie | Color base | Personalidad |
|---|---|---|---|
| Coquí | Caballito de mar | Verde menta (#9ED48A) | Tímido, curioso, primer rescate |
| Burbujín | Pez payaso | Naranja (#F0A060) | Parlanchín, exagerado, cómico |
| (Cap 2-6) | Ver GDD §13.3 | Ver GDD | Ver GDD |

**Entregables por criatura:**
- Sprite sheet con idle animation (~4-6 frames)
- PNG individual si lo permite el flujo del artista
- Archivo fuente

**Notas de dirección:**
- Expresiones visibles que reflejen la personalidad de cada una
- No todas tienen que ser "cute" igual — Burbujín es exagerado, Coquí es suave
- Cada criatura vive en el santuario nadando forever — la idle animation la vemos constantemente

---

## Specs técnicos de Godot (para integración)

### Sprites de burbujas
- Tamaño: 96×96 px — coincide con `GridLogic.BUBBLE_DIAMETER = 96.0`
- Animación pop: AnimationPlayer en `bubble.tscn`, 0.25s
- `bubble.gd` usa `Sprite2D` — reemplazar `_draw()` actual

### Backgrounds de gameplay
- Cargados en `gameplay.tscn` según `level_data.get("chapter", 1)`
- TextureRect con `expand_mode = IGNORE_SIZE` y `stretch_mode = KEEP_ASPECT_COVERED`

### App Icons — tamaños requeridos
**Android (Adaptive Icon):**
- ic_launcher_foreground.png: 108×108 dp (safe zone 72×72 dp)
- ic_launcher_background.png: 108×108 dp
- Store: 512×512 px

**iOS:**
- AppStore: 1024×1024 px (sin transparencia, sin esquinas — Apple las redondea)
- Godot export genera el resto automáticamente desde el 1024

### Partículas
- Implementadas como GPUParticles2D en escenas `scenes/effects/fx_*.tscn`
- Instanciar con `preload()` y `queue_free()` cuando termina la emisión

---

## Presupuesto estimado total

| Categoría | Costo estimado | Fuente |
|---|---|---|
| Tipografías (Quicksand + Nunito) | $0 | Google Fonts |
| Design system Godot | $0 | Diego/Claude |
| Logo Coralia (básico) | $0-50 | Canva + retoque |
| Sprites de burbujas | $50-100 | Freelancer o asset pack |
| Marina + 6 animaciones | $80-150 | Freelancer Fiverr/Upwork |
| 12 criaturas hero | $600-1,200 | Freelancer (mismo artista) |
| Criaturas comunes (10-15) | $0-100 | AI gen + retoque |
| Backgrounds gameplay (6) | $100-200 | AI gen + freelancer |
| Backgrounds santuario (12) | $150-300 | AI gen + freelancer |
| Cinemáticas restauración (6) | $200-400 | Freelancer animador |
| Iconos UI + power-ups | $0-50 | Phosphor Icons + Kenney.nl |
| Efectos de partículas | $0 | Diego/Claude (Godot GPUParticles) |
| App icons + screenshots | $0-20 | Herramientas online |
| Sombra Profunda | $100-200 | Freelancer |
| **TOTAL ESTIMADO** | **$1,280-$2,770** | Plan Maestro: $2,000-$4,000 ✅ |

---

*Documento mantenido por Diego. Actualizar estado de cada issue conforme avanza la producción.*
