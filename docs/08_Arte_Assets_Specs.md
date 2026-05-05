# Arte Assets — Especificaciones de Producción
## Coralia · myappcube · Diego (solo dev)

**Versión:** 1.0 · **Fecha:** 2026-05-05
**Herramientas:** ver tabla al final. Generás item por item en el orden de prioridad.

---

## Stack de herramientas

| Asset | Herramienta | Costo |
|---|---|---|
| Fondos, personajes, criaturas (estáticos) | **Midjourney** | ~$10/mes |
| Animaciones de personajes | **Kling AI** o **Runway ML** | ~$10-15/mes |
| Burbujas, botones, UI vectorial | **Figma** | Gratis |
| Logo, iconos de app | **Figma** + Midjourney | mismo plan |
| Efectos de partículas | **Godot CPUParticles2D** | Gratis (código) |
| Edición/recorte de imágenes | **Photopea** (Photoshop gratuito online) | Gratis |
| Spritesheet de animaciones | **Free Sprite Sheet Packer** (online) | Gratis |

**Flujo general:**
1. Generás en Midjourney → descargás PNG
2. Editás/recortás en Photopea si hace falta (fondo transparente, ajustes)
3. Guardás en `assets/sprites/` o `assets/backgrounds/` según corresponda
4. Importás en Godot

---

## PRIORIDAD 1 — Sin esto el juego no corre visualmente

---

### A1 · Burbujas de colores (jugabilidad core)

**Propósito:** las 8 burbujas que dispara el jugador y forman el grid.
**Herramienta:** Figma (geométrico, no necesitás Midjourney)
**Cantidad:** 8 sprites (rojo, azul, verde, amarillo, naranja, violeta, turquesa, rosa)
**Formato:** PNG con fondo transparente

#### Especificaciones técnicas
```
Tamaño canvas: 96 × 96 px  (BUBBLE_DIAMETER del juego = 96.0)
Forma: círculo perfecto, diámetro 88 px (4 px de margen por lado para antialiasing)
Estilo: esfera con gradiente — punto de luz arriba-izquierda (iluminación consistente)
```

#### Cómo crear en Figma (paso a paso)
1. Frame 96×96 px
2. Círculo 88×88, color base de la burbuja
3. Círculo interior más pequeño con opacidad 40% blanco (reflejo superior-izquierda, ~30×30 px)
4. Gradiente radial del color base hacia color más oscuro abajo-derecha
5. Stroke exterior 2 px, color base darkened 20%
6. Export → PNG, escala 1x

#### Paleta de colores
| Nombre interno | Color base | Hex |
|---|---|---|
| `red` | Coral rojo | `#E85D5D` |
| `blue` | Azul océano | `#4A90D9` |
| `green` | Verde alga | `#5BAD6F` |
| `yellow` | Amarillo arena | `#F0C040` |
| `orange` | Naranja coral | `#F07840` |
| `purple` | Violeta profundo | `#8B5CF6` |
| `teal` | Turquesa marino | `#38B2AC` |
| `pink` | Rosa anémona | `#EC4899` |

#### Archivos a guardar
```
assets/sprites/bubbles/bubble_red.png
assets/sprites/bubbles/bubble_blue.png
assets/sprites/bubbles/bubble_green.png
assets/sprites/bubbles/bubble_yellow.png
assets/sprites/bubbles/bubble_orange.png
assets/sprites/bubbles/bubble_purple.png
assets/sprites/bubbles/bubble_teal.png
assets/sprites/bubbles/bubble_pink.png
```

---

### A2 · Icono de app + logo Coralia

**Propósito:** icono visible en App Store, Google Play y el teléfono del usuario. Primera impresión.
**Herramienta:** Midjourney → Figma para ajustar

#### Icono de app
```
Tamaño master: 1024 × 1024 px (iOS requiere esto, Android deriva de ahí)
Forma: cuadrado con esquinas redondeadas (el SO las aplica automáticamente)
NO incluir bordes redondeados en el archivo — entregarlo cuadrado puro
Formato: PNG sin transparencia (fondo sólido obligatorio en stores)
```

**Prompt Midjourney:**
```
cute underwater coral reef game app icon, young mermaid girl with teal tail
swimming among colorful bubbles, cozy kawaii style, Studio Ghibli inspired,
warm coral pink and turquoise color palette, centered composition, clean
simple design suitable for mobile app icon, no text, soft lighting,
digital illustration --ar 1:1 --v 6.1 --style raw
```

**Variaciones a generar:** mínimo 4 opciones, elegís la mejor.

#### Logo Coralia (texto)
```
Tamaño: 1200 × 400 px
Fondo: transparente
Estilo: texto "Coralia" con tipografía redondeada/orgánica, color coral_pink
        con outline blanco, pequeñas burbujas decorativas alrededor
```

**Prompt Midjourney:**
```
logo design text "CORALIA" underwater ocean theme, bubbly rounded font,
coral pink color with white outline, small colorful bubbles around letters,
teal and coral color palette, cute cozy style, transparent background,
clean vector-style illustration --ar 3:1 --v 6.1
```

#### Archivos a guardar
```
assets/branding/icon_1024.png         ← master
assets/branding/icon_512.png          ← derivado (resize)
assets/branding/icon_192.png          ← Android adaptive icon
assets/branding/logo_coralia.png      ← logo con transparencia
```

---

### A3 · Fondo de gameplay (Capítulo 1)

**Propósito:** el fondo que se ve detrás del grid de burbujas durante las partidas.
**Herramienta:** Midjourney
**Cantidad:** 1 por ahora (Capítulo 1 — La Cala Apagada), el resto después

```
Tamaño: 1080 × 1920 px
Formato: JPG (sin transparencia, es fondo sólido)
Área activa: la parte SUPERIOR del fondo (donde está el grid de burbujas)
             debe ser más oscura/simple para que las burbujas sean legibles
Área inferior: puede ser más detallada (está oculta por el grid y el cañón)
```

**Prompt Midjourney:**
```
underwater coral reef scene, dimly lit cove, dark teal water, soft caustic
light rays from above, sleeping coral formations in shades of grey and muted
teal, few small sleeping fish, dreamy atmosphere, cozy melancholic mood,
Studio Ghibli color palette, portrait orientation, game background art,
no characters, soft gradients, upper area darker and simpler --ar 9:16 --v 6.1
```

**Nota:** "La Cala Apagada" = zona sin restaurar. El arrecife debe verse
apagado, dormido, en espera. Cuando se restaure (cinemática), se vuelve
vibrante y colorido.

**Archivos a guardar**
```
assets/backgrounds/gameplay/chapter_01_bg.jpg
```

---

## PRIORIDAD 2 — MVP con buen look visual

---

### B1 · Marina — diseño base del personaje

**Propósito:** protagonista del juego. Aparece en: gameplay (lateral), mapa de niveles
(caminando), santuario (primer plano), pantallas de victoria/derrota.
**Herramienta:** Midjourney → Photopea para recortar fondo

```
Tamaño canvas: 400 × 600 px
Formato: PNG con fondo transparente (recortado en Photopea)
Vista: de frente, pose neutral/idle, cuerpo completo
```

**Prompt Midjourney:**
```
young female mermaid character design, round face, big expressive eyes,
dark wavy hair with small coral accessories, turquoise and teal fish tail,
warm skin tone, simple cute top with coral/pink color, friendly gentle
expression, cozy kawaii style, Studio Ghibli inspired, full body portrait,
clean white background for easy cutout, soft cel-shading, 2D game character
concept art, no background elements --ar 2:3 --v 6.1 --style raw
```

**Importante:** generá 4+ variaciones, elegís la que más te guste y la usás
como referencia consistente en TODOS los demás assets de Marina.
Una vez elegida, guardá el prompt exacto como "prompt maestro de Marina".

**Archivos a guardar**
```
assets/sprites/marina/marina_base.png    ← diseño aprobado recortado
assets/sprites/marina/marina_ref.png     ← referencia con fondo (para Midjourney)
```

---

### B2 · Marina — animaciones (6 estados)

**Propósito:** dar vida al personaje en distintas situaciones del juego.
**Herramienta:** **Kling AI** — subís la imagen base de Marina y describís el movimiento.
**Formato output:** video MP4 → extraer frames → spritesheet

Para cada animación:
1. Subís `marina_base.png` a Kling AI como imagen de referencia
2. Escribís el prompt de movimiento
3. Generás video ~3-4 segundos
4. Extraés frames con Free Sprite Sheet Packer o EZGif
5. Importás como AnimatedSprite2D en Godot

#### Animaciones a generar

| ID | Nombre | FPS | Frames aprox | Loop |
|---|---|---|---|---|
| `idle` | Respirar suave, cola moviéndose | 8 | 12 | Sí |
| `shoot` | Lanza un disparo, brazo hacia adelante | 12 | 8 | No |
| `victory` | Salto de alegría, celebración | 12 | 16 | No |
| `defeat` | Baja la cabeza, decepcionada | 8 | 12 | No |
| `rescue` | Abraza a una criatura pequeña | 12 | 20 | No |
| `greet` | Saluda con la mano, sonríe | 8 | 10 | No |

**Prompt Kling AI — ejemplo para `idle`:**
```
The mermaid character breathes softly, her tail sways gently left and right,
hair floats slightly as if underwater, subtle blinking, calm peaceful movement,
loop animation, cozy game character style
```

**Tamaño de cada frame:** 400 × 600 px (igual que el base)
**Formato spritesheet final:** PNG con transparencia

**Archivos a guardar**
```
assets/sprites/marina/marina_idle.png       ← spritesheet
assets/sprites/marina/marina_shoot.png
assets/sprites/marina/marina_victory.png
assets/sprites/marina/marina_defeat.png
assets/sprites/marina/marina_rescue.png
assets/sprites/marina/marina_greet.png
```

---

### B3 · Fondo del Santuario

**Propósito:** el panorama del arrecife que se ve en la pantalla principal del juego.
Visualmente el más importante — el jugador lo ve cada vez que abre el juego.
**Herramienta:** Midjourney
**Variantes:** 6 (una por capítulo/zona) + versión "todo apagado" inicial

```
Tamaño: 1080 × 1920 px
Formato: JPG
Estilo: panorámica horizontal del arrecife dividida en zonas distinguibles
```

**Prompt base (zona 1 restaurada — La Cala Apagada iluminada):**
```
panoramic underwater coral reef sanctuary scene, vibrant warm colors, golden
light rays from surface, colorful coral formations glowing softly, small
colorful fish swimming peacefully, sea anemones, starfish, cozy magical
atmosphere, Studio Ghibli art style, lush and alive, game background art,
portrait orientation, rich detailed illustration --ar 9:16 --v 6.1
```

**Prompt zona apagada (estado inicial):**
```
same composition but desaturated, grey coral, dim lighting, empty and quiet
underwater cave, sad lonely atmosphere, slight blue-grey color palette,
waiting to be restored, same Studio Ghibli style --ar 9:16 --v 6.1
```

**Archivos a guardar**
```
assets/backgrounds/sanctuary/sanctuary_zone1_lit.jpg
assets/backgrounds/sanctuary/sanctuary_zone1_dark.jpg
assets/backgrounds/sanctuary/sanctuary_all_dark.jpg   ← estado inicial
```

---

### B4 · UI — Botones y elementos de interfaz

**Propósito:** todos los botones, pills, panels que se ven en menús y HUD.
**Herramienta:** Figma (todo vectorial, no necesitás Midjourney)

#### btn_primary (botón principal — JUGAR, CONTINUAR, etc.)
```
Tamaño: 640 × 144 px
Border radius: 32 px
Fondo: gradiente lineal coral_pink (#F4A69F) arriba → coral_deep (#D87B7B) abajo
Sombra: y=6, blur=16, color rgba(0,0,0,0.2)
Texto: blanco, Quicksand Bold 52px
Estado hover: darkened 10%
Estado pressed: darkened 20% + scale 0.96
```

#### btn_secondary (botón secundario — Reintentar, Salir, etc.)
```
Tamaño: mismo que primary
Fondo: blanco
Border: 3px coral_pink
Texto: coral_deep, mismo tamaño
```

#### btn_ad (botón de anuncio — +5 disparos ad, etc.)
```
Tamaño: mismo
Fondo: blanco
Border: 3px gold_treasure (#F0C040)
Texto: gold_treasure + icono play video a la izquierda
```

#### HUD pill — monedas / gemas / vidas
```
Tamaño: ancho variable × 56 px alto
Border radius: 28 px (completamente redondeado)
Fondo: blanco 85% opacidad
Border: 1px blanco
Sombra: y=2, blur=8, rgba(0,0,0,0.1)
Layout: icono 32×32 + número Nunito Black 28px
```

#### Panel popup
```
Tamaño: 960 × variable px
Border radius: 48 px
Fondo: blanco
Sombra: y=8, blur=40, rgba(0,0,0,0.3)
Overlay detrás: rgba(0,0,0,0.6) full screen
```

**Archivos a guardar**
```
assets/ui/btn_primary.png           ← 640×144px
assets/ui/btn_secondary.png
assets/ui/btn_ad.png
assets/ui/hud_pill_bg.png          ← 9-slice para stretch
assets/ui/panel_popup_bg.png       ← 9-slice
assets/ui/bottom_nav_bg.png        ← 1080×160px fondo barra nav
```

---

## PRIORIDAD 3 — Enriquece la experiencia

---

### C1 · Criaturas hero (12 en total)

**Propósito:** las criaturas que Marina rescata. Aparecen en el grid de burbujas
(atrapadas), en la pantalla de victoria, y en el Santuario nadando.
**Herramienta:** Midjourney → Photopea para recortar

```
Tamaño por criatura: 200 × 200 px
Formato: PNG con fondo transparente
Vista: frente, expresión distintiva, estilo consistente con Marina
```

#### Lista de las 12 criaturas hero

| # | Nombre | Especie | Capítulo | Personalidad |
|---|---|---|---|---|
| 1 | Coqui | Pez payaso naranja | 1 | Curioso, bromista |
| 2 | Burbujín | Pez globo azul | 1 | Tímido, adorable |
| 3 | Caracol | Caracol de mar | 1 | Sabio, lento pero seguro |
| 4 | Lumi | Pulpo luminoso | 2 | Artista, cambia colores |
| 5 | Coral | Caballito de mar rosa | 2 | Romántica, soñadora |
| 6 | Rayo | Mantarraya bebé | 3 | Veloz, juguetón |
| 7 | Perla | Almeja con perla | 3 | Reservada, valiosa |
| 8 | Spike | Estrella de mar roja | 4 | Valiente, protector |
| 9 | Medusa | Medusa iridiscente | 4 | Misteriosa, etérea |
| 10 | Tortu | Tortuga bebé verde | 5 | Tranquilo, filosófico |
| 11 | Sandy | Cangrejo arenero | 5 | Gracioso, torpe |
| 12 | Abisma | Pez de las profundidades | 6 | La Sombra Profunda liberada |

**Prompt base Midjourney (adaptar por criatura):**
```
cute kawaii [ESPECIE] sea creature character design, round chibi proportions,
big shiny eyes, [COLOR] color palette, friendly expression, cozy underwater
game art style, Studio Ghibli inspired, clean white background, full body,
2D game character --ar 1:1 --v 6.1 --style raw
```

**Archivos a guardar**
```
assets/sprites/creatures/hero/coqui.png
assets/sprites/creatures/hero/burbujin.png
... (12 archivos)
```

---

### C2 · Cañón

**Propósito:** el lanzador de burbujas, centrado en la parte inferior del gameplay.
**Herramienta:** Midjourney → Photopea para recortar

```
Tamaño: 200 × 280 px
Formato: PNG con fondo transparente
Rotación: apunta hacia arriba (0°). Godot lo rota dinámicamente al apuntar.
Estilo: concha marina grande y decorativa con abertura como cañón
```

**Prompt Midjourney:**
```
giant decorative sea shell cannon, coral and teal colors, pearl accents,
cute but functional design, pointing upward, magical underwater aesthetic,
cozy game art style, clean white background, 2D game asset, no text --ar 5:7 --v 6.1
```

**Archivo a guardar**
```
assets/sprites/canon/canon_shell.png
```

---

### C3 · Fondos de gameplay — capítulos 2 a 6

**Propósito:** fondo único por capítulo para el gameplay.
**Herramienta:** Midjourney (el prompt base ya está en A3, adaptarlo por capítulo)
**Tamaño:** 1080 × 1920 px · JPG

| Capítulo | Nombre | Ambiente visual |
|---|---|---|
| 2 | Jardín de Anémonas | Anémonas de colores vivos, luz cálida rosada |
| 3 | Bosque de Algas | Algas altas y verdes, luz filtrada verde |
| 4 | Cueva de Cristales | Cristales bioluminiscentes, azul profundo |
| 5 | Profundidades de Coral | Coral denso, naranja y rojo, oscuro |
| 6 | Ciudad de las Perlas | Ruinas submarinas doradas, partículas de luz |

**Archivos a guardar**
```
assets/backgrounds/gameplay/chapter_02_bg.jpg
assets/backgrounds/gameplay/chapter_03_bg.jpg
assets/backgrounds/gameplay/chapter_04_bg.jpg
assets/backgrounds/gameplay/chapter_05_bg.jpg
assets/backgrounds/gameplay/chapter_06_bg.jpg
```

---

### C4 · Mapa de niveles — path y decoración

**Propósito:** el camino serpenteante del Level Select por el que camina Marina.
**Herramienta:** Midjourney para elementos decorativos, Figma para el path en sí.

#### Path / camino
```
Ancho: 24 px
Color completado: coral_pink con brillo
Color por recorrer: gris claro
Estilo: línea orgánica con pequeñas perlas decorativas cada ~100px
Crearlo en Figma como línea vectorial
```

#### Decoración del mapa por capítulo
```
Elementos: corales, algas, peces pequeños, burbujas, estrellas de mar
Tamaño elementos: 60-120 px
Formato: PNG con transparencia
```

**Prompt Midjourney (elementos decorativos):**
```
set of small cute underwater decorative elements, coral pieces, sea plants,
tiny fish, starfish, shells, seaweed, isolated on white background,
cozy kawaii style, Studio Ghibli color palette, 2D game assets,
multiple items in same image, clean cutout style --ar 4:3 --v 6.1
```

**Archivos a guardar**
```
assets/sprites/map/path_pearl.png         ← decoración del camino
assets/sprites/map/decorations_ch1.png    ← sheet de decoraciones cap 1
... por capítulo
```

---

### C5 · Efectos visuales (Godot puro — sin arte externo)

Estos NO necesitan assets de Midjourney. Se construyen con `CPUParticles2D` en Godot.

| Efecto | Descripción | Nodo Godot |
|---|---|---|
| Burbuja pop | Círculos pequeños explotan hacia afuera | CPUParticles2D radial |
| Match explosion | Destellos de color en el punto de match | GPUParticles2D |
| Criatura rescatada | Estrellas y corazones flotando hacia arriba | CPUParticles2D |
| Combo x5+ | Número grande con glow que aparece y desaparece | AnimationPlayer + Label |
| Corrientes de agua | Partículas suaves horizontales en fondo | CPUParticles2D drift |
| Confeti victoria | Confeti multicolor cayendo | CPUParticles2D gravity |

---

## PRIORIDAD 4 — Post-lanzamiento

---

### D1 · Skins de Marina (Battle Pass)
```
Mismo tamaño que base: 400 × 600 px
Variantes de color/accesorio sobre el diseño base aprobado
Prompt: "[mismo prompt de Marina] but with [variación: dark blue tail, red hair, golden accessories, etc.]"
```

### D2 · Capturas para App Store
```
iPhone: 1290 × 2796 px (6.9" Pro Max)
iPad: 2048 × 2732 px
Mínimo 3 capturas, máximo 10
Contenido: gameplay real + overlay de texto descriptivo
Crear en Figma con screenshot del juego + texto superpuesto
```

### D3 · Banner de eventos temporales
```
Tamaño: 960 × 400 px
Formato: PNG
Contenido: imagen del evento + texto del nombre
Generar en Midjourney + texto en Figma
```

---

## Checklist de producción

Marcá cada item al completarlo:

### Prioridad 1 (necesario para build presentable)
- [ ] A1 · Burbujas de colores (8 sprites) — Figma
- [ ] A2 · Icono de app 1024×1024 — Midjourney + Figma
- [ ] A2 · Logo Coralia — Midjourney + Figma
- [ ] A3 · Fondo gameplay capítulo 1 — Midjourney

### Prioridad 2 (MVP con buen look)
- [ ] B1 · Marina diseño base — Midjourney + Photopea
- [ ] B2 · Marina idle animation — Kling AI
- [ ] B2 · Marina victory animation — Kling AI
- [ ] B2 · Marina defeat animation — Kling AI
- [ ] B3 · Fondo Santuario zona 1 lit — Midjourney
- [ ] B3 · Fondo Santuario all dark — Midjourney
- [ ] B4 · btn_primary — Figma
- [ ] B4 · btn_secondary — Figma
- [ ] B4 · btn_ad — Figma
- [ ] B4 · HUD pills — Figma

### Prioridad 3 (experiencia completa)
- [ ] B2 · Marina shoot animation — Kling AI
- [ ] B2 · Marina rescue animation — Kling AI
- [ ] B2 · Marina greet animation — Kling AI
- [ ] C1 · Criaturas hero 1-6 (capítulos 1-3)
- [ ] C1 · Criaturas hero 7-12 (capítulos 4-6)
- [ ] C2 · Cañón — Midjourney + Photopea
- [ ] C3 · Fondos gameplay capítulos 2-6
- [ ] C4 · Path decoración mapa

### Prioridad 4 (post-lanzamiento)
- [ ] D1 · Skins Marina Battle Pass
- [ ] D2 · Capturas App Store
- [ ] D3 · Banners de eventos

---

## Notas de consistencia de estilo

Antes de empezar, definir el "prompt maestro de estilo" y usarlo en TODOS los assets:

```
Estilo base Coralia:
- cute kawaii cozy aesthetic
- Studio Ghibli color palette (warm, slightly desaturated, harmonious)
- soft cel-shading, no hard lines
- underwater ocean theme
- coral pink + teal + pearl white + gold como colores dominantes
- warm gentle lighting
- 2D game art, clean edges for easy cutout
```

Guardá el resultado aprobado de cada asset como referencia para el siguiente.
Midjourney tiene la opción `--cref [URL]` para mantener consistencia de personaje
entre generaciones.
