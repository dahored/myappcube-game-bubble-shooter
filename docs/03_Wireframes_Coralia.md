# Wireframes — Coralia

**Versión:** 0.1 (en construcción)
**Fecha:** 2026-04-30
**Documento vivo:** sí — se actualiza durante el diseño en Figma

---

## Cómo usar este documento

Este documento es el **spec detallado de las 17 pantallas** del MVP, escrito para guiar el diseño en Figma. No reemplaza Figma — es el "shopping list" de qué dibujar.

Por cada pantalla incluye: dimensiones, layout esquemático en ASCII, lista numerada de elementos con su descripción, estados, e interacciones. Los detalles de propósito y flujo están en GDD sección 10.

**Recomendación de uso:**
1. Leés la sección **Framework** una vez para entender convenciones generales (dimensiones, tipografías, componentes reutilizables).
2. Para cada pantalla, abrís este doc y Figma en paralelo y dibujás siguiendo el spec.
3. Cuando descubras que un elemento del spec no funciona, lo ajustás acá primero, después en Figma.

---

## Framework

### Dimensiones y safe area

Diseño base para Figma: **frame iPhone 15 portrait** (1179 × 2556 px, escala 3x). Trabajar en este frame evita sorpresas en devices reales.

Áreas críticas:

| Área | Px (alto) | Notas |
|---|---|---|
| Status bar (top) | 144 | iOS dynamic island / Android status bar — no poner UI tappable acá |
| Safe area top | 144 (post status bar) | Inicio de UI confiable |
| Safe area bottom | 102 (home indicator iOS) | Espacio inferior reservado |
| Área útil | ~2310 px (de 144 a 2454) | Donde vive todo el contenido |

Para Android: el área útil es similar, con algunos px adicionales arriba para el status bar. Diseño portrait → encaja bien en ambos.

### Grid

- **8 columnas** con 24 px gutter, 48 px padding lateral (margen de 48 px desde los bordes laterales del frame)
- **8 px baseline grid** vertical — todos los elementos snap a múltiplos de 8

### Tipografías

| Estilo | Fuente | Tamaño | Peso | Uso |
|---|---|---|---|---|
| `H1` | Quicksand | 48 px | Bold | Títulos de pantalla principales |
| `H2` | Quicksand | 36 px | Bold | Subtítulos, headers de sección |
| `H3` | Quicksand | 28 px | Semibold | Cards, popups |
| `Body` | Nunito | 24 px | Regular | Texto general, descripciones |
| `Body Small` | Nunito | 20 px | Regular | Secundario, captions |
| `Number Big` | Nunito | 56 px | Black | Score, contadores grandes |
| `Number Small` | Nunito | 28 px | Black | Vidas, monedas, gemas en HUD |
| `Button` | Quicksand | 32 px | Semibold | Texto de botones |

### Spacing scale

Múltiplos de 8 que se usan consistentemente: `4`, `8`, `16`, `24`, `32`, `48`, `64`, `96`. Nunca usar valores arbitrarios fuera de esta escala (mantiene ritmo visual).

### Componentes reutilizables

#### `btn_primary`
- Tamaño: alto 144 px, ancho variable (mín 320 px, expand al contenido)
- Border radius: 32 px
- Fondo: gradient `coral_pink` → `coral_deep` (Modo Arrecife) / `aqua_deep` → `coral_deep` (Modo Profundidades)
- Texto: blanco, estilo `Button`
- Padding horizontal: 48 px
- Sombra suave: y=8, blur=24, color rgba(0,0,0,0.15)

#### `btn_secondary`
- Igual dimensión que primary
- Fondo: blanco con borde 4 px de `coral_pink` (Modo Arrecife) / `seafoam` border en Modo Profundidades
- Texto: `coral_deep` o `seafoam` según modo

#### `btn_ad`
- Igual dimensión que secondary
- Borde y texto en `gold_treasure`
- Icono de play de video a la izquierda del texto

#### `btn_icon`
- 96 × 96 px circular
- Fondo blanco semi-transparente con icono al centro
- Usado para Settings, Profile, etc., en esquinas

#### `card_shop_item`
- Ancho 100% del contenido area, alto 240 px
- Border radius: 24 px
- Fondo: `pearl_white` (light) / dark blue (dark)
- 3 áreas internas: imagen del producto (izq, 192×192), descripción (centro), precio + botón (der)

#### `popup_container`
- Modal centrado, 90% del ancho del frame, alto variable
- Border radius: 48 px
- Fondo: blanco/dark según modo
- Overlay detrás: `dark_overlay` 70% opacity
- Padding interno: 48 px todos lados
- Botón de cerrar (X) en esquina top-right (96×96 área tappable, icono 32×32)

#### `hud_currency`
- Pill horizontal 48 px alto, ancho contenido
- Border radius: 24 px
- Fondo blanco semi-transparente
- Layout: icono moneda/gema (32×32) + número (estilo `Number Small`)
- Tap → abre Shop tab correspondiente

#### `hud_lives`
- Pill horizontal con icono de corazón + número de vidas + timer hasta próxima vida regenerada
- Tap → muestra opciones de comprar/esperar/ver ad

### Iconografía

- Estilo: filled outlined (líneas gruesas pero rellenas en su mayoría)
- Tamaño base: 48 px
- Trazos: 4 px stroke
- Source: armar set propio o usar Phosphor Icons (free, abundante, estilo cute)

### Animaciones (referencia)

- Transición entre pantallas: slide horizontal 300ms ease-out
- Popups: scale-in desde 0.9 a 1.0, opacity 0 a 1, 200ms ease-out
- Tap feedback: scale 1.0 → 0.95 → 1.0 en 100ms
- Loading: spinner o partículas burbujas circulares

### Convenciones cross-modo (Light vs Dark)

Cada pantalla se diseña primero en **Modo Arrecife (light)** y se deriva a **Modo Profundidades (dark)** swappeando tokens de color. Tokens vienen de GDD sección 11.2.

| Elemento | Light (Arrecife) | Dark (Profundidades) |
|---|---|---|
| Background pantalla | `pearl_white` o gradient suave coral | `aqua_deep` o gradient violeta-azul profundo |
| Texto primario | `coral_deep` | `pearl_white` |
| Texto secundario | gris medio | gris claro |
| Acento | `coral_pink` | `coral_pink` (igual, pero con más contraste sobre dark) |

---

## Pantalla 1 — Company Splash

**Dimensión:** 1179 × 2556
**Duración:** 1.5-2s, auto-cierra
**Layout:** centrado vertical y horizontal, todo el frame

```
┌─────────────────────────────────┐
│                                 │
│                                 │
│                                 │
│                                 │
│                                 │
│        ┌─────────────┐          │
│        │             │          │
│        │   LOGO      │          │
│        │  myappcube  │          │
│        │             │          │
│        └─────────────┘          │
│                                 │
│         tagline opcional        │
│                                 │
│                                 │
│                                 │
│                                 │
└─────────────────────────────────┘
```

**Elementos:**

1. **Background sólido** — `pearl_white` (light) / `aqua_deep` (dark). Sin imágenes ni decoración.
2. **Logo myappcube** — centrado en el frame. Tamaño ~640 × 640 px o según diseño del logo. Animación de aparición: fade-in + scale 0.95 → 1.0 en 400ms.
3. **Tagline opcional** — debajo del logo, espacio 64 px. Estilo `Body Small`. Texto pendiente de definir (ej. "Juegos para todos los días" o similar).

**Estados:** ninguno. Es pantalla pasiva.

**Interacciones:** tap en cualquier lado → skip directo a Loading Splash (con cap mínimo de 0.5s para evitar tap accidental al abrir el juego).

---

## Pantalla 2 — Loading Splash

**Dimensión:** 1179 × 2556
**Duración:** hasta que termine la carga, target 1.5-3s
**Layout:** vertical centrado con barra de carga abajo

```
┌─────────────────────────────────┐
│                                 │
│      [Marina silueta animada    │
│       de fondo, semi-trans]     │
│                                 │
│        ┌──────────┐             │
│        │  Logo    │             │
│        │ Coralia  │             │
│        └──────────┘             │
│                                 │
│        C O R A L I A            │
│                                 │
│                                 │
│      ┌───────────────────┐      │
│      │█████████░░░░░░░░░│      │
│      └───────────────────┘      │
│                                 │
│                                 │
│                                 │
│              v1.0.0             │
│           by Diego              │
└─────────────────────────────────┘
```

**Elementos:**

1. **Background** — gradient suave: `seafoam` arriba → `pearl_white` abajo (light) / `aqua_deep` arriba → `dark_overlay` abajo (dark)
2. **Marina silueta animada** — silueta de Marina nadando suave en el background, alrededor de 30% opacity. Loop de animación idle. Tamaño aprox 800 × 1200 px, posición ligeramente desplazada del centro.
3. **Logo Coralia** — centrado, ~480 × 480 px, encima del nombre del juego.
4. **Nombre del juego** — texto "CORALIA" debajo del logo, estilo `H1` con tracking de 8 px (espaciado entre letras), color `coral_deep` (light) / `pearl_white` (dark).
5. **Barra de carga** — horizontal, ancho 750 px, alto 24 px, border radius 12 px. Track de fondo `seafoam` con 30% opacity. Fill animado en `coral_pink`. Reflexión sutil de luz en el fill (white gradient overlay).
6. **Versión** — texto `v1.0.0` en footer, estilo `Body Small`, color gris medio.
7. **Crédito** — texto `by Diego` debajo de la versión, estilo `Body Small`, color gris medio.

**Estados:**

- **Carga normal:** barra avanza suave de 0% a 100%
- **Error de carga:** la barra se detiene, aparece texto "Error al cargar — toca para reintentar" + botón `btn_secondary` "Reintentar"

**Interacciones:**

- En estado normal: ninguna interacción (pantalla pasiva)
- En estado error: tap en cualquier lado o en el botón → reinicia carga

---

## Pantalla 3 — Onboarding

**Dimensión:** 1179 × 2556
**Cuándo aparece:** una sola vez, en el primer abrir del juego (después de Loading Splash)
**Layout:** los primeros 3 niveles del juego con overlays de tutorial sobreimpresos

```
┌─────────────────────────────────┐
│  [grid de burbujas del nivel 1] │
│  ▓▓▓▓▓▓▓▓▓▓▓                   │
│  ▓ ▓ ▓ ▓ ▓ ▓                   │
│   ▓ ▓ ▓ ▓ ▓                    │
│                                 │
│  ┌──────────────────┐          │
│  │  ¡Hola! Soy      │ ← bocadillo
│  │  Marina. Tocá y  │   contextual
│  │  arrastrá para   │          │
│  │  apuntar.        │          │
│  └──────────────────┘          │
│            ↓                    │
│       [puntero animado]         │
│                                 │
│         ╲                       │
│          ╲                      │
│           ╲                     │
│         🎯cañón                 │
└─────────────────────────────────┘
```

**Elementos:**

1. **Background y grid** — son el nivel 1 real (ver pantalla 14 — Gameplay), pero con overlays didácticos sobreimpresos.
2. **Bocadillo de Marina** — caja pop-up con cola direccional, fondo `pearl_white` translúcido, borde `coral_pink`, padding 32 px. Texto del bocadillo cambia con cada paso.
3. **Puntero animado** — manita o flecha que se mueve mostrando la acción a hacer. Loop hasta que el jugador la imite.
4. **Botón "saltar tutorial"** — texto pequeño en esquina top-right, oculto los primeros 2 segundos para evitar tap accidental, después aparece con fade.

**Pasos del onboarding:**

| Paso | Bocadillo de Marina | Acción esperada |
|---|---|---|
| 1 | "¡Hola! Soy Marina. Tocá y arrastrá para apuntar." | Drag desde el cañón |
| 2 | "Soltá para disparar la burbuja." | Release |
| 3 | "¡Mirá! Cuando tres del mismo color se juntan, explotan. ¿Vemos quién está atrapado?" | Disparar para hacer match y rescatar criatura |

**Estados:** progresando paso 1 → 2 → 3. Tras paso 3, transición a Santuario por primera vez con animación especial de "bienvenida al santuario".

**Interacciones:**
- Se ejecuta una sola vez por jugador (flag `tutorial_completed` en SaveGame)
- Tap en "saltar tutorial" → confirma con popup ("¿Estás seguro?") y salta al Santuario directamente

---

## Pantalla 4 — Santuario (main menu)

**Dimensión:** 1179 × 2556
**Pantalla principal del juego — todo se accede desde acá**
**Layout:** vista panorámica del arrecife con HUDs en top y bottom, e iconos en esquinas

```
┌─────────────────────────────────┐
│ ⚙️  [Eventos banner]    👤     │ ← top: settings (izq), eventos (centro), profile (der)
│                                 │
│ 🪙2,450  💎87  ❤️3 (12:34)      │ ← HUD currencies
│                                                       
│                                 │
│   [Vista panorámica del         │
│    arrecife con criaturas       │
│    rescatadas nadando idle —    │
│    cambia color/vida según      │
│    progreso]                    │
│                                 │
│   🐠  🐢      🐙                │
│       🦐  🐡                    │
│                                 │
│   [Marina parada en una concha  │
│    en primer plano, idle]       │
│                                 │
│                                 │
│  [racha 12🔥]                   │ ← indicador de racha
│                                 │
│  ┌────────────────────────┐    │
│  │       J U G A R        │    │ ← btn_primary grande
│  └────────────────────────┘    │
│                                 │
│  🛒 Shop  🎫 BP  📅 Daily       │ ← acceso rápido
└─────────────────────────────────┘
```

**Elementos:**

1. **Background** — vista del arrecife. Animado: corrientes suaves, peces nadando, partículas de luz. Cambia su nivel de "vida" según el progreso (los primeros niveles muestran arrecife más apagado, los últimos lo muestran restaurado).
2. **HUD top-left: Settings icon** — `btn_icon` (96×96) con engranaje. Tap → Pantalla 8 (Settings).
3. **HUD top-right: Profile icon** — `btn_icon` con avatar de Marina (skin actual). Tap → Pantalla 9 (Profile).
4. **HUD top-center: Events banner** — solo aparece si hay evento activo. Pill horizontal (640×96) con título del evento y countdown. Tap → Pantalla 11 (Events).
5. **HUD currencies (debajo del top)** — fila horizontal con 3 pills: monedas (`hud_currency` + número), gemas (`hud_currency` + número), vidas (`hud_lives` + número + timer). Cada uno tappable: monedas/gemas → Shop, vidas → popup de comprar.
6. **Criaturas rescatadas** — ~10-30 criaturas (según progreso) animadas idle nadando por la pantalla. Tap en una → bestiary popup con info de esa criatura.
7. **Marina** — idle animation en primer plano sobre una concha o coral. Tap → animación de saludo + frase aleatoria en bocadillo.
8. **Indicador de racha** — pill compacto con icono de fuego + número de días. Si está por terminar el día y no hay actividad: animación de urgencia. Tap → popup de racha con info detallada.
9. **Botón JUGAR** — `btn_primary` extra-grande (alto 160 px), centrado horizontal, posición vertical 65% del frame. Tap → Pantalla 12 (Level Select).
10. **Acceso rápido bottom** — fila de 3 iconos: Shop, Battle Pass, Daily Rewards. Cada uno con badge si hay novedad (rojo con número). Tap → respectiva pantalla.

**Estados:**

- **Default:** todo lo descrito arriba
- **Battle Pass nuevo (popup auto):** al abrir el juego en día 1 de temporada nueva, popup automático "Nueva temporada disponible" con CTA al BP
- **Welcome Back:** si volviste tras 7+ días, popup "Te extrañamos" con regalo de bienvenida
- **Daily Reward disponible:** badge rojo con `!` en el icono de Daily Rewards
- **Modo Profundidades:** background con tonos azul-violeta profundos, criaturas más bioluminiscentes, Marina con detalles glow

**Interacciones:**
- Pull-to-refresh: actualizar estado del santuario (criaturas, eventos)
- Long-press en una criatura: mostrar su nombre y diálogo característico
- Tap en background vacío: animación de Marina (saludo o bostezo cute)

---

## Pantalla 5 — Daily Rewards

**Dimensión:** 1179 × 2556
**Cuándo aparece:** popup al primer login del día, o desde Santuario tap manual
**Layout:** popup centrado con carrusel horizontal de 7 días

```
┌─────────────────────────────────┐
│        [overlay oscuro]         │
│                                 │
│   ┌──────────────────────────┐ │
│   │           ✕              │ │
│   │                          │ │
│   │   RECOMPENSA DIARIA      │ │ ← H2
│   │   Día 3 de 7             │ │
│   │                          │ │
│   │  ┌──┬──┬──┬──┬──┬──┬──┐ │ │
│   │  │ ✓│ ✓│ 💎│  │  │  │  │ │ │ ← 7 días, días 1-2 ya reclamados
│   │  │1 │2 │3 │4 │5 │6 │7 │ │ │   día 3 destacado, 4-7 grises
│   │  └──┴──┴──┴──┴──┴──┴──┘ │ │
│   │                          │ │
│   │     +5 GEMAS HOY         │ │
│   │                          │ │
│   │   ┌──────────────────┐  │ │
│   │   │   RECLAMAR       │  │ │
│   │   └──────────────────┘  │ │
│   │                          │ │
│   │   Próximo regalo en      │ │
│   │   9h 23min               │ │
│   └──────────────────────────┘ │
│                                 │
└─────────────────────────────────┘
```

**Elementos:**

1. **Overlay** — `dark_overlay` 70% opacity sobre Santuario
2. **Popup container** — `popup_container`, alto 1400 px aproximado
3. **Botón cerrar** — X en top-right
4. **Título H2** — "RECOMPENSA DIARIA"
5. **Subtítulo** — "Día N de 7" con N actual destacado
6. **Carrusel de 7 días** — fila horizontal, cada día es una mini-card 120×160 px, todas dentro del popup con scroll horizontal si no caben
7. **Estados de cada día:**
   - Reclamado: check verde + recompensa visible (translúcida)
   - Hoy disponible: pulsa con animación, recompensa destacada, borde dorado
   - Futuro: gris translúcido, recompensa visible pero atenuada
8. **Recompensa de hoy destacada** — texto grande "+5 GEMAS HOY" con icono de la recompensa
9. **Botón RECLAMAR** — `btn_primary`. Si ya se reclamó: deshabilitado y dice "RECLAMADO"
10. **Countdown** — "Próximo regalo en Xh Ymin", estilo `Body Small`

**Estados:**

- **Disponible para reclamar:** botón activo, animación de aparición de gemas/monedas al reclamar
- **Ya reclamado:** botón deshabilitado, countdown visible
- **Racha rota:** texto rojo "Tu racha se rompió. Día 1 de 7." + botón para reclamar día 1

**Interacciones:**
- Tap RECLAMAR: animación de drop de la recompensa, sonido alegre, contadores actualizan
- Tap en otro día (cualquiera): tooltip con la recompensa de ese día
- Tap fuera o X: cierra popup

---

## Pantalla 6 — Battle Pass

**Dimensión:** 1179 × 2556
**Layout:** vertical scroll con tracks paralelos free + premium

```
┌─────────────────────────────────┐
│  ←                  ⏱️ 12 días  │ ← back + countdown
│                                 │
│   TEMPORADA 1                   │ ← H1
│   Despertar del Coral           │
│                                 │
│   [Hero image temporada]        │
│                                 │
│  Tier 18 ─────●──── Tier 19    │ ← progress bar XP
│            450/1000 XP          │
│                                 │
│  ┌────────────────────────┐    │
│  │ ¡Comprar Premium $4.99 │    │ ← solo si NO premium
│  └────────────────────────┘    │
│                                 │
│   ─── TRACKS ───               │
│                                 │
│  Free    │ Premium             │ ← headers
│ ────────┼────────              │
│  T20  💎10│ ← T20 ✨skin       │ ← reclamado/disponible/futuro
│  T19  ✓  │     ✓ 💎25         │
│  T18 ●hoy│   ●hoy ✨ Pop esp   │ ← tier actual destacado
│  T17  ✓  │     ✓ 💎15         │
│  T16  ✓  │     ✓ 1 power-up   │
│  T15  ✓  │     ✓ ✨skin cañón │
│   ...    │      ...            │
│                                 │
└─────────────────────────────────┘
```

**Elementos:**

1. **Header back arrow** — top-left, vuelve al Santuario
2. **Countdown** — top-right, "X días" hasta fin de temporada
3. **Título H1** — "TEMPORADA 1"
4. **Subtítulo H2** — nombre del tema ("Despertar del Coral")
5. **Hero image** — banner visual de la temporada (~750×400 px) representando el tema
6. **Progress bar XP** — bar horizontal con marcador del tier actual, números de tier prev/next, "X/1000 XP" debajo
7. **Botón comprar premium** — solo si el jugador no es premium. `btn_primary` dorado especial, "Comprar Premium $4.99"
8. **Lista de tiers (40 total)** — scroll vertical, cada fila es un tier mostrando:
   - Free reward (icono + cantidad/item)
   - Premium reward (icono + cantidad/item, con badge ✨ premium)
   - Estado: ✓ reclamado / ● hoy disponible / candado de futuro
9. **Tap en tier disponible** — animación de claim, drop de la recompensa

**Estados:**

- **Free user:** premium track visible pero todos los rewards con candado dorado + "Premium"
- **Premium user:** ambos tracks reclamables. Botón comprar oculto. Indicador "Premium activo" arriba.
- **Temporada terminando (<72h):** banner urgencia "¡Últimos días! Reclamá antes de que expire"
- **Modo Profundidades:** todo el frame con paleta dark, hero image y rewards mantienen color

**Interacciones:**
- Scroll vertical: navegar tiers
- Tap en reward reclamable: claim animación
- Tap en reward futuro: tooltip "Necesitas X XP más"
- Tap en hero image: ver detalles del tema (cinemática corta)

---

## Pantalla 7 — Shop

**Dimensión:** 1179 × 2556
**Layout:** tabs horizontales arriba + grid de productos abajo

```
┌─────────────────────────────────┐
│  ←  TIENDA      🪙2,450 💎87   │ ← back + currencies HUD
│                                 │
│  Gemas │ Vidas │ Power-ups │ ★ │ ← tabs (★ = especiales/ofertas)
│ ───────┴───────┴──────────┴────│
│                                 │
│  [Banner: Starter Pack          │ ← banner ofertas activas
│   $2.99 — termina en 3d 12h]    │
│                                 │
│  ┌────────────────────────┐    │
│  │ 💎  80 Gemas           │    │ ← card_shop_item
│  │ Burbujita        $0.99 │    │
│  │                  [BUY] │    │
│  └────────────────────────┘    │
│                                 │
│  ┌────────────────────────┐    │
│  │ 💎💎 450 Gemas  +13%   │    │
│  │ Concha           $4.99 │    │
│  │                  [BUY] │    │
│  └────────────────────────┘    │
│                                 │
│  ┌────────────────────────┐    │
│  │ 💎💎💎 1000 +25% BEST! │    │ ← badge "best value"
│  │ Coral            $9.99 │    │
│  │                  [BUY] │    │
│  └────────────────────────┘    │
│                                 │
│   ...                           │
└─────────────────────────────────┘
```

**Elementos:**

1. **Header** — back arrow + título "TIENDA" + HUD currencies
2. **Tabs** — horizontal, 4 tabs: Gemas, Vidas, Power-ups, Especiales (★). Tab activo en `coral_pink`, otros gris medio.
3. **Banner ofertas activas** — solo si hay Starter/Weekend/Holiday/Flash deal. Card destacada con countdown.
4. **Grid de productos** — vertical scroll, cards `card_shop_item` con: imagen del producto, nombre, descripción, precio, botón BUY
5. **Best value badge** — badge dorado en el pack con mejor relación gemas/$ (típicamente $9.99 o $19.99)
6. **Card del Starter Pack** — destacada al top con borde glow + countdown + "Valor $9.99 → $2.99 (70% OFF)"

**Estados por tab:**

| Tab | Productos visibles |
|---|---|
| Gemas | 6 packs ($0.99 a $99.99) + Vidas Infinitas (1h, 24h, 7d) |
| Vidas | Refill instantáneo (100 gemas) o vidas individuales (25 c/u) |
| Power-ups | 6 power-ups en cantidades 1, 5, 20 |
| Especiales | Starter Pack (si activo), Weekend Deal, eventos |

**Interacciones:**
- Tap BUY → popup confirmación de compra → trigger RevenueCat IAP flow
- Compra exitosa: animación de drop + actualiza HUD currencies + confeti
- Compra fallida: popup error con razón

---

## Pantalla 8 — Settings

**Dimensión:** 1179 × 2556
**Layout:** lista vertical con 4 secciones (estructura cross-proyecto myappcube)

```
┌─────────────────────────────────┐
│  ←  AJUSTES                     │
│                                 │
│  ── PREFERENCIAS DEL JUEGO ──   │ ← section header
│  Sonidos del juego  [▮▮▮░░] 70%│
│  Efectos interfaz   [▮▮▮▮░] 80%│
│  Sonidos pop        [▮▮▮▮▮] 100%│
│  Vibración            (●)  ON   │
│                                 │
│  ── CUENTA Y ASISTENCIA ──      │
│  Perfil                      › │
│  Suscripción       (Próximamente)│
│  Cómo jugar                  › │
│  Idioma           Español    › │
│  Tema             Automático › │
│  Ayuda                       › │
│  Restaurar compras           › │
│                                 │
│  ── COMUNIDAD ──                │
│  Valorar la app              › │
│  Compartir                   › │
│  Redes sociales              › │
│  Sitio web                   › │
│                                 │
│  ── LEGAL ──                    │
│  Política de privacidad      › │
│  Condiciones del servicio    › │
│                                 │
│  v1.0.0 · myappcube             │ ← footer
└─────────────────────────────────┘
```

**Elementos:**

1. **Header back** — back arrow + título "AJUSTES"
2. **Section headers** — H3 con underline horizontal en `coral_pink`
3. **Settings rows** — alto 96 px cada uno, label izq + control der (slider, toggle, picker o `›` flecha)
4. **Sliders de audio** — 3 separados con visualización de nivel + porcentaje
5. **Toggle vibración** — switch nativo con accent color `coral_pink`
6. **Filas con `›`** — abren sub-pantalla o popup
7. **Suscripción** — muestra "Próximamente" hasta fase 2 (Coralia Plus)
8. **Idioma row** — abre popup con 6 opciones (radio buttons): Español, English, Italiano, Français, Deutsch, Português
9. **Tema row** — abre popup con 3 opciones: Modo Arrecife (claro), Modo Profundidades (oscuro), Automático (sigue al SO)
10. **Footer** — versión + estudio en `Body Small` color gris

**Interacciones:**
- Sliders: drag para ajustar, sonido de muestra al soltar
- Toggle vibración: tap para alternar, dispara vibración corta de muestra
- Filas de comunidad: abren respectivas URLs en navegador externo o share sheet nativo

---

## Pantalla 9 — Profile

**Dimensión:** 1179 × 2556
**Layout:** avatar + stats arriba, logros con scroll abajo

```
┌─────────────────────────────────┐
│  ←  PERFIL                  ✏️ │ ← edit username
│                                 │
│       ┌──────────┐              │
│       │  AVATAR  │              │ ← skin actual de Marina (256×256)
│       │  Marina  │              │
│       └──────────┘              │
│        Marina123                │ ← username
│        Código: ABC-123          │ ← código de amigo
│                                 │
│  ── ESTADÍSTICAS ──             │
│  Niveles ganados      27        │
│  Criaturas rescatadas 14        │
│  Racha actual / max  12 / 15    │
│  Días jugados         18        │
│                                 │
│  Vincular cuenta                │
│  [📘 Facebook] [🍎 Apple]      │
│                                 │
│  ── LOGROS  14/40 ──            │
│  ┌────┬────┬────┬────┐         │
│  │ 🏆 │ 🏆 │ 🔒 │ 🔒 │         │ ← grid 4 columnas
│  ├────┼────┼────┼────┤         │
│  │ 🏆 │ 🔒 │ 🔒 │ 🔒 │         │
│  ├────┼────┼────┼────┤         │
│  │ ... │    │    │    │         │
│  └────┴────┴────┴────┘         │
└─────────────────────────────────┘
```

**Elementos:**

1. **Header** — back + título + edit (lápiz) que permite cambiar username
2. **Avatar de Marina** — circular 256×256, skin actual visible (Battle Pass o default)
3. **Username** — H2 centrado debajo del avatar
4. **Código de amigo** — `Body Small` con icono copy → tap copia al clipboard
5. **Stats grid** — lista vertical de pares label/value
6. **Vincular cuenta buttons** — 2 botones horizontales con logos. Si ya está vinculado: muestra "Vinculado ✓" en gris
7. **Logros header** — H3 + contador "14/40"
8. **Logros grid** — 4 columnas, cada celda 240×240 px, icono del logro + nombre debajo. Logros desbloqueados con color, los bloqueados grises con candado.

**Interacciones:**
- Tap en logro desbloqueado: popup con detalles + recompensa otorgada
- Tap en logro bloqueado: popup con descripción de cómo desbloquearlo
- Tap edit: input text con username + botón guardar
- Tap "Vincular cuenta": flow de OAuth (Apple / Facebook / Google según)

---

## Pantalla 10 — Leaderboard

**Dimensión:** 1179 × 2556
**Layout:** tabs + lista vertical de top players

```
┌─────────────────────────────────┐
│  ←  LEADERBOARD     ⏱️ 5d 23h  │ ← reset countdown
│                                 │
│  Global │ Amigos │ Por Nivel    │ ← tabs
│ ────────┴────────┴──────        │
│                                 │
│  TOP 10 ESTA SEMANA             │
│                                 │
│  🥇 1. Lucia      24,500 💎 100 │
│  🥈 2. Pablo      22,100 💎 75  │
│  🥉 3. Sofia      19,800 💎 50  │
│  ── ── ── ── ── ── ── ── ──    │
│   4. Mario       18,400        │
│   5. Carla       17,200        │
│   ...                           │
│  10. Andrés      14,000        │
│                                 │
│  ── TU POSICIÓN ──              │
│   47. Marina123  9,800         │
│                                 │
│  Recompensas:                   │
│  Top 10 → 100 💎 + skin        │
│  Top 100 → 50 💎               │
│  Top 1000 → 25 💎              │
└─────────────────────────────────┘
```

**Elementos:**

1. **Header** — back + título + countdown hasta reset semanal
2. **Tabs** — Global / Amigos / Por Nivel
3. **Sub-header** — "TOP 10 ESTA SEMANA" o equivalente
4. **Lista top 3** — destacada con medallas y borde dorado/plata/bronce
5. **Lista 4-100** — filas regulares con rank + avatar + username + score
6. **Tu posición** — destacada en card aparte si no estás en top visible
7. **Recompensas** — info estática al fondo

**Tabs:**

| Tab | Contenido |
|---|---|
| Global | Top players del mundo, reset semanal |
| Amigos | Solo tus amigos, reset semanal |
| Por Nivel | Selector de nivel, muestra mejores scores en ese nivel específico |

**Interacciones:**
- Tap en jugador: ver su perfil público (avatar, stats básicas, código de amigo)
- Pull-to-refresh: actualizar leaderboard

---

## Pantalla 11 — Events

**Dimensión:** 1179 × 2556
**Layout:** card del evento principal arriba, lista de próximos abajo

```
┌─────────────────────────────────┐
│  ←  EVENTOS                     │
│                                 │
│  ── EVENTO ACTIVO ──            │
│  ┌─────────────────────────┐   │
│  │  [Hero image evento]    │   │
│  │                         │   │
│  │ FESTIVAL DE CORAL       │   │ ← H2
│  │ Termina en 2d 14h       │   │
│  │                         │   │
│  │ Niveles especiales con  │   │ ← descripción
│  │ recompensas dobles.     │   │
│  │                         │   │
│  │ Tu progreso: 12/30      │   │
│  │ [▮▮▮░░░░░░] 40%        │   │
│  │                         │   │
│  │ Premios:                │   │
│  │ ✓ 5 niveles → 50 gemas │   │
│  │ ● 15 niveles → skin    │   │
│  │ ○ 30 niveles → 200💎    │   │
│  │                         │   │
│  │ ┌─────────────────────┐│   │
│  │ │  JUGAR EVENTO       ││   │
│  │ └─────────────────────┘│   │
│  └─────────────────────────┘   │
│                                 │
│  ── PRÓXIMOS EVENTOS ──         │
│  Luna Llena   en 5 días        │
│  Marea de Coleccionables  12d  │
└─────────────────────────────────┘
```

**Elementos:**

1. **Header** — back + título
2. **Card del evento activo** — `card_shop_item` extendida con:
   - Hero image del evento (banner)
   - Nombre del evento (H2)
   - Countdown
   - Descripción de la mecánica
   - Progreso del jugador con barra
   - Lista de hitos (tres niveles típicamente, con check si desbloqueado)
   - CTA `JUGAR EVENTO` → Pantalla 12 (Level Select del evento)
3. **Lista próximos eventos** — eventos que aún no empezaron, con countdowns

**Estados:**
- Sin evento activo: card "No hay eventos activos. Vuelve pronto." + lista de próximos
- Evento terminando (<24h): card con animación de urgencia + countdown grande

---

## Pantalla 12 — Level Select

**Dimensión:** 1179 × 2556
**Layout:** mapa serpenteante vertical con scroll

```
┌─────────────────────────────────┐
│  ←  CAPÍTULO 3   🪙... 💎... ❤️│ ← header + HUD
│  Bosque de Algas                │ ← nombre del capítulo
│                                 │
│  ──── nivel 30 (próximo cap)  │ ← capítulo siguiente locked
│                ●                │
│           ●                     │
│                ●                │
│  29 ────────●                   │ ← niveles ya completados
│         ● 28  con criatura     │
│       ●      rescatada         │
│  27 ●─────●                    │
│              ●26                │
│         ●                       │
│  ●25                            │ ← nivel actual (pulsing)
│        ●                        │
│  24 ●                           │
│       ● 23                      │
│  22 ────────●                   │ ← nodos completados
│        ● 21                     │
│  ──── inicio capítulo 3 ────   │
└─────────────────────────────────┘
```

**Elementos:**

1. **Header** — back arrow + nombre del capítulo + HUD currencies
2. **Subtítulo** — nombre poético del capítulo (ej. "Bosque de Algas")
3. **Mapa serpenteante** — vertical scroll, camino que zigzaguea entre niveles
4. **Nodos de nivel** — círculos numerados:
   - Completado: verde con criatura rescatada (icono pequeño)
   - Actual: pulsa con animación, color `coral_pink`
   - Bloqueado: gris con candado
5. **Conexiones entre nodos** — caminos curvos `coral_pink` (los recorridos) y grises (los no)
6. **Decoración del mapa** — algas, peces, partículas según el tema del capítulo
7. **Marcador de inicio/fin de capítulo** — separadores horizontales con texto
8. **Capítulo siguiente** — visible en parte superior pero locked hasta completar el actual

**Estados:**
- **Default:** scroll fluido, nivel actual centrado al abrir
- **Capítulo recién desbloqueado:** animación de revelación al abrir (~3s) antes de poder jugar
- **Evento activo:** badge especial en niveles del evento

**Interacciones:**
- Scroll vertical para navegar
- Tap en nivel actual: → Pantalla 13 (Pre-level)
- Tap en nivel completado: opción de re-jugar o ver mejor score
- Tap en nivel bloqueado: tooltip "Completa el nivel anterior"

---

## Pantalla 13 — Pre-level

**Dimensión:** 1179 × 2556
**Cuándo aparece:** tras seleccionar un nivel
**Layout:** info del nivel arriba, slots de power-ups abajo

```
┌─────────────────────────────────┐
│  ←  NIVEL 27        🪙... 💎...│
│                                 │
│  [Imagen criatura a rescatar]   │
│  ┌──────────┐                   │
│  │   🐙     │                   │ ← Lumi (en este caso)
│  └──────────┘                   │
│                                 │
│  Rescatar a Lumi                │ ← H2
│  "Te observé desde la primera   │ ← diálogo de la criatura
│  burbuja, niña."                │
│                                 │
│  Disparos disponibles: 28       │
│                                 │
│  ── EQUIPÁ TUS POWER-UPS ──     │
│                                 │
│  ┌──────┐ ┌──────┐ ┌──────┐    │ ← 3 slots equipables
│  │  💣  │ │  +   │ │  +   │    │
│  │ Bomba│ │ vacío│ │ vacío│    │
│  │ x 2  │ │      │ │      │    │
│  └──────┘ └──────┘ └──────┘    │
│                                 │
│  ┌────────────────────────┐    │
│  │  🎬 Power-up gratis     │    │ ← btn_ad
│  │     viendo un anuncio   │    │
│  └────────────────────────┘    │
│                                 │
│  ┌────────────────────────┐    │
│  │       J U G A R         │    │ ← btn_primary
│  └────────────────────────┘    │
└─────────────────────────────────┘
```

**Elementos:**

1. **Header** — back + número del nivel + HUD
2. **Imagen de la criatura a rescatar** — círculo destacado con animación idle de la criatura
3. **Título** — "Rescatar a [nombre]" (cuando es objetivo rescue)
4. **Diálogo** — frase de la criatura, en cursiva
5. **Disparos disponibles** — número grande
6. **Sección equipar power-ups** — header H3
7. **Slots de power-ups (3)** — cada slot 240×240:
   - Vacío: icono `+` con texto "agregar"
   - Equipado: icono del power-up + nombre + cantidad disponible (`x 2`)
   - Tap en slot vacío: bottom sheet con power-ups disponibles
   - Tap en slot equipado: remueve o cambia
8. **Botón ad gratis** — `btn_ad` con cap diario (3/día). Si llegó al cap: deshabilitado con texto "Vuelve mañana"
9. **Botón JUGAR** — `btn_primary`, lleva a Pantalla 14 (Gameplay)

**Estados:**

- **Sin vidas:** popup automático ofreciendo comprar/ad/esperar antes de poder presionar JUGAR
- **Re-jugar nivel completado:** mismo layout pero con badge "Mejor score: X"

**Interacciones:**
- Tap slot vacío: bottom sheet con scroll de power-ups disponibles + costo en gemas si no se tiene
- Tap power-up no disponible: popup "Comprar 1 por X gemas" o cancelar
- Tap JUGAR: transición a Gameplay

---

## Pantalla 14 — Gameplay

**Dimensión:** 1179 × 2556
**LA pantalla principal del juego**
**Layout:** HUD top compacto, grid central, cañón + power-ups bottom

```
┌─────────────────────────────────┐
│ ⏸  Obj: Rescatar 🐙  Disp: 22  │ ← HUD top: pause, objetivo, disparos
│                                 │
│  [Marina silueta lateral]       │
│                                 │
│  ▓▓▓▓▓▓▓▓▓▓▓                   │
│  ▓ ▓ ▓ ▓ ▓ ▓                   │
│   ▓ ▓ ▓ ▓ ▓                    │
│  ▓ ▓ ▓ ▓ ▓ ▓ ▓                 │
│   ▓ ▓ ▓ ▓ ▓                    │ ← grid hexagonal de burbujas
│  ▓ ▓ 🐙 ▓ ▓                    │   con criatura atrapada visible
│   ▓ ▓ ▓ ▓                      │
│                                 │
│                                 │
│         ╲                       │
│          ╲   ← trayectoria      │
│           ╲                     │
│         🎯                      │ ← cañón con burbuja current
│       ⚪⚪                       │   y next preview
│                                 │
│  💣  ⚡  🎯                      │ ← power-ups equipados, tap-to-activate
│ x2  x1  x1                      │
└─────────────────────────────────┘
```

**Elementos:**

1. **HUD top** (alto 96 px):
   - Botón pause (top-left, `btn_icon`)
   - Objetivo del nivel: icono + texto compacto centro
   - Disparos restantes: número grande top-right
2. **Marina lateral** — silueta animada en lateral del frame (decorativa, no interactiva)
3. **Grid hexagonal** — área principal, ocupa ~60% del alto. Burbujas con animación idle suave (pulsing leve), criaturas atrapadas visibles entre las burbujas.
4. **Cañón** — bottom-center, sobre una concha decorativa. Burbuja current (la grande) + next preview (más pequeña al lado).
5. **Línea de trayectoria** — solo visible cuando el jugador apunta (drag activo). Punteada `coral_pink` con primer rebote calculado.
6. **HUD bottom** — los power-ups equipados (3 max), cada uno con icono + cantidad. Tap activa el power-up.
7. **Animaciones in-game:**
   - Match: explosión de partículas + sonido pop
   - Drop de flotantes: caen en cascada con partículas
   - Combo x5+: animación de score multiplicador grande
   - Rescate de criatura: cinemática corta (1.5s) con celebración

**Estados:**

- **Disparando:** burbuja en vuelo, cañón se recarga
- **En cinemática:** rescate o transición de capítulo, controles deshabilitados
- **Animación de victoria:** transición a Pantalla 16
- **Sin disparos:** transición a Pantalla 16 (Game Over)

**Interacciones:**
- Drag desde cualquier punto inferior: apuntar el cañón
- Release: dispara
- Tap en burbuja del cañón: swap con next
- Tap en power-up: activar (bottom sheet con instrucciones de uso)
- Tap en pause: → Pantalla 15

---

## Pantalla 15 — Pause

**Dimensión:** 1179 × 2556
**Cuándo aparece:** overlay sobre Gameplay tras tap en pause
**Layout:** overlay centrado con botones

```
┌─────────────────────────────────┐
│      [Gameplay congelado al     │
│       fondo, dim 60%]           │
│                                 │
│   ┌──────────────────────────┐ │
│   │       PAUSADO            │ │ ← H2
│   │                          │ │
│   │ ┌──────────────────────┐│ │
│   │ │     CONTINUAR        ││ │ ← btn_primary
│   │ └──────────────────────┘│ │
│   │                          │ │
│   │ ┌──────────────────────┐│ │
│   │ │    REINICIAR NIVEL   ││ │ ← btn_secondary
│   │ └──────────────────────┘│ │
│   │                          │ │
│   │ ┌──────────────────────┐│ │
│   │ │  SALIR AL SANTUARIO  ││ │ ← btn_secondary
│   │ └──────────────────────┘│ │
│   │                          │ │
│   │  Música  [▮▮▮▮░] Pop ON │ │ ← toggles rápidos
│   └──────────────────────────┘ │
└─────────────────────────────────┘
```

**Elementos:**

1. **Background** — Gameplay congelado con overlay 60% `dark_overlay`
2. **Popup container** — centrado, ~90% width, alto contenido
3. **Título "PAUSADO"** — H2 centrado
4. **Botón Continuar** — primary
5. **Botón Reiniciar Nivel** — secondary, popup confirmación (pierde progreso del nivel)
6. **Botón Salir al Santuario** — secondary, popup confirmación (perdés vida)
7. **Toggles rápidos** — sliders rápidos de música y sonido pop (atajo a settings sin salir)

**Interacciones:**
- Tap continuar o tap fuera del popup: vuelve a gameplay (despausar con countdown 3-2-1)
- Tap reiniciar/salir: confirmación + acción

---

## Pantalla 16 — Game Over / Victory

**Dimensión:** 1179 × 2556
**Cuándo aparece:** al fallar o ganar un nivel
**Dos variantes según resultado**

### Variante A: Game Over

```
┌─────────────────────────────────┐
│      [overlay oscuro]           │
│                                 │
│   ┌──────────────────────────┐ │
│   │       SIN DISPAROS       │ │
│   │                          │ │
│   │  [Marina suspirando      │ │
│   │   animación triste]      │ │
│   │                          │ │
│   │  Te quedaste sin         │ │
│   │  disparos.               │ │
│   │                          │ │
│   │ ┌──────────────────────┐│ │
│   │ │ 🎬 +5 disparos (ad)  ││ │ ← btn_ad
│   │ └──────────────────────┘│ │
│   │                          │ │
│   │ ┌──────────────────────┐│ │
│   │ │ +5 disparos (15 💎)  ││ │ ← btn_secondary
│   │ └──────────────────────┘│ │
│   │                          │ │
│   │ ┌──────────────────────┐│ │
│   │ │  Aceptar y reintentar││ │ ← link sutil (gris)
│   │ └──────────────────────┘│ │
│   │  (perdés 1 vida)         │ │
│   └──────────────────────────┘ │
└─────────────────────────────────┘
```

### Variante B: Victory

```
┌─────────────────────────────────┐
│   ¡LO LOGRASTE!                 │ ← H1 + animación
│                                 │
│   [animación rescate criatura]  │
│                                 │
│   ┌──────────────────────────┐ │
│   │ Score: 24,500            │ │
│   │ Combo máx: x8            │ │
│   │ Disparos sobrantes: 4    │ │
│   │                          │ │
│   │ Criatura rescatada:      │ │
│   │ ┌────┐                  │ │
│   │ │ 🐙 │ Lumi              │ │
│   │ └────┘                  │ │
│   │                          │ │
│   │ ┌──────────────────────┐│ │
│   │ │     CONTINUAR        ││ │
│   │ └──────────────────────┘│ │
│   └──────────────────────────┘ │
└─────────────────────────────────┘
```

**Elementos comunes:**

- Overlay oscuro sobre Gameplay
- Popup con resultado
- Botón principal de continuación

**Variante A (Game Over):**
- Marina con animación triste
- Mensaje "Sin disparos" o "Sin movimientos"
- Opciones: ad para +5 disparos, gemas para +5 disparos, aceptar derrota

**Variante B (Victory):**
- Animación de celebración (Marina + criatura abrazándose)
- Stats: score, combos, disparos sobrantes
- Criatura rescatada destacada
- Botón CONTINUAR → Pantalla 17 (Post-level)

**Interacciones:**
- Game Over → ad: trigger ad → si ve completo, +5 disparos y vuelve a Gameplay
- Game Over → gemas: confirma costo → +5 disparos y vuelve a Gameplay
- Game Over → aceptar: pierde 1 vida, vuelve a Pre-level con opción retry o salir
- Victory → continuar: → Post-level

---

## Pantalla 17 — Post-level

**Dimensión:** 1179 × 2556
**Cuándo aparece:** después de Victory
**Layout:** drop animado de recompensas + opción de duplicar

```
┌─────────────────────────────────┐
│       RECOMPENSAS               │
│                                 │
│   [animación drop de items      │
│    desde arriba con bounce]     │
│                                 │
│  ┌──────────────────────────┐  │
│  │ +75 monedas              │  │
│  │ +2 gemas                 │  │
│  │ +1 power-up: Bomba       │  │
│  │ +50 Battle Pass XP       │  │
│  │ ✨ Lumi rescatada         │  │
│  └──────────────────────────┘  │
│                                 │
│  Battle Pass: tier 18 → 19!     │
│  [▮▮▮▮▮▮▮░░░] 70%              │
│                                 │
│  ┌────────────────────────┐    │
│  │ 🎬 DUPLICAR (ver ad)   │    │ ← btn_ad
│  └────────────────────────┘    │
│                                 │
│  ┌────────────────────────┐    │
│  │  SIGUIENTE NIVEL       │    │ ← btn_primary
│  └────────────────────────┘    │
│                                 │
│  ┌────────────────────────┐    │
│  │  Salir al santuario    │    │ ← link sutil
│  └────────────────────────┘    │
└─────────────────────────────────┘
```

**Elementos:**

1. **Título** — "RECOMPENSAS" H1
2. **Animación de drop** — items aparecen cayendo con bounce
3. **Lista de recompensas** — todo lo ganado en el nivel: monedas, gemas, power-ups, BP XP, criatura
4. **Battle Pass progress** — si subió de tier, animación especial + barra
5. **Botón duplicar** — `btn_ad`, cap 10/día. Al verla: x2 sobre coins/gems/power-ups
6. **Botón siguiente nivel** — `btn_primary`, vuelve a Gameplay con el siguiente nivel cargado
7. **Salir al santuario** — link sutil para terminar la sesión

**Estados especiales:**

- **Última nivel del capítulo** — en lugar de "siguiente nivel", botón "VER MI ARRECIFE" + cinemática de restauración de zona del santuario (5-8s)
- **Ganaste un logro** — popup encima con badge nuevo
- **Subiste de tier en BP** — animación especial del BP

**Interacciones:**
- Tap duplicar: trigger ad → si completo, x2 recompensas con animación adicional
- Tap siguiente: carga nivel siguiente → Pre-level → Gameplay
- Tap salir: → Santuario

---

## Cambios y versiones

| Versión | Fecha | Cambios |
|---|---|---|
| 0.1 | 2026-04-30 | Framework completo + pantallas 1-2 (Company Splash, Loading Splash) como prueba de formato |
| 0.2 | 2026-04-30 | Pantallas 3-17 completas. Las 17 pantallas del MVP especificadas con layout ASCII, lista de elementos, estados e interacciones. Listo para diseñar en Figma. |
