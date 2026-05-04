# Plan Fase 1 — Prototipo Jugable

**Versión:** 0.2 — 2026-05-01
**Estado:** ✅ **COMPLETADA** (Chunk 7 skipped por decisión del solo dev)
**Duración real:** 1 sesión intensiva con Claude (vs 3-4 semanas estimadas)
**Output:** prototipo con gameplay core funcional + 5 niveles jugables + persistencia
**Meta única:** **validar que el juego es divertido** ← pendiente validar con playtest informal

---

## Por qué este prototipo

El Plan Maestro lo dice claro: **antes de invertir más tiempo en arte, monetización, UI, sonido, marketing, hay que validar que el juego es divertido**. Si no lo es, todos esos esfuerzos están construidos sobre arena. Si lo es, sabemos que vale la pena seguir.

El prototipo de Fase 1 deliberadamente **no tiene**:

- Arte final (placeholders rectangulares de colores está bien)
- Audio (silencio total)
- UI bonita (botones nativos de Godot, sin estilo)
- Monetización (ni gemas, ni vidas, ni Battle Pass)
- Persistencia (cada vez que abrís resetea)
- Localización (todo en español hardcoded)
- Onboarding ni splashes
- Animaciones bonitas (transiciones brutas)

**Sí tiene:**

- Grid hexagonal funcional
- Cañón con apuntado, trayectoria y disparo
- Match detection con drops de flotantes
- Win/lose conditions con disparos limitados
- 5 niveles cargados desde JSON
- Loop completo de jugar → ganar o perder → reintentar → siguiente nivel

Si **el prototipo es divertido**, sabés que el core mecánico funciona y todo el resto (arte, monetización, social) **construye sobre algo sólido**. Si no, replanteás mecánicas antes de gastar 5 meses más.

---

## Chunks de trabajo

Fase 1 se divide en 7 chunks secuenciales. Cada chunk tiene un deliverable concreto: cuando podés correr el proyecto y ver/hacer la cosa específica, el chunk está completo.

| # | Chunk | Duración | Bloquea a |
|---|---|---|---|
| 1 | Grid hexagonal | 2-3 días | 2, 3 |
| 2 | Cañón y disparo | 3-4 días | 3, 4 |
| 3 | Match detection y drops | 3-4 días | 4 |
| 4 | Win/lose conditions | 2-3 días | 5, 6 |
| 5 | Loader de niveles desde JSON | 2-3 días | 6 |
| 6 | Crear los 5 niveles | 3-4 días | 7 |
| 7 | Validation playtest | 1-2 días | — |

**Total: 16-23 días** (~3-4 semanas según ritmo).

---

## Chunk 1 — Grid hexagonal

**Duración:** 2-3 días
**Bloquea a:** Chunks 2, 3

### Objetivo
Mostrar un grid hexagonal de burbujas estáticas en pantalla, con conversión correcta entre coordenadas pixel y coordenadas de grid.

### Tareas

1. Crear escena `scenes/gameplay/gameplay.tscn` con estructura básica de nodos (Node2D root, child para grid container)
2. Crear escena `scenes/gameplay/bubble.tscn` — un Node2D con Sprite2D circular (placeholder: círculo de color sólido). 6 colores: rojo, azul, verde, amarillo, púrpura, naranja
3. Crear script `scripts/gameplay/grid_logic.gd`:
   - Función `grid_to_pixel(col: int, row: int) -> Vector2` (usa offset hexagonal: filas pares vs impares)
   - Función `pixel_to_grid(pos: Vector2) -> Vector2i`
   - Función `get_neighbors(col: int, row: int) -> Array` (devuelve hasta 6 vecinos hexagonales)
4. Crear script `scripts/gameplay/grid.gd` (attached al grid container):
   - Carga inicial de un grid 11 columnas × 8 filas con burbujas de colores aleatorios
   - Spawn de instancias de `bubble.tscn` en sus posiciones correctas
5. Hook básico de `boot.gd` para que cargue `gameplay.tscn` directamente (skip onboarding/santuario por ahora)

### Deliverable
Correr el proyecto (F5 en Godot) → se ve un grid hexagonal de ~88 burbujas de colores. Las filas alternas están desplazadas medio diámetro horizontal. **Eso es todo, no hay interacción todavía.**

### Validación rápida
- ¿El grid se ve hexagonal correcto, no cuadrado?
- ¿Cabe cómodamente en pantalla portrait con margen lateral?
- ¿Las posiciones se calculan bien al cambiar tamaño de pantalla (test con resolución diferente)?

---

## Chunk 2 — Cañón y disparo

**Duración:** 3-4 días
**Bloquea a:** Chunks 3, 4

### Objetivo
El jugador puede apuntar con drag, ver una línea de trayectoria, y soltar para disparar una burbuja que viaja hasta impactar el grid o una pared.

### Tareas

1. Crear escena `scenes/gameplay/canon.tscn` — Node2D con sprite del cañón (placeholder: rectángulo) en la parte inferior centro de la pantalla
2. Crear `scripts/gameplay/canon.gd`:
   - Detectar `_input` events: drag (touch o mouse) desde cualquier lugar de la pantalla inferior
   - Calcular ángulo entre el cañón y el punto del drag
   - Renderizar línea de trayectoria con `Line2D` (apuntando + primer rebote contra paredes)
   - On release: instanciar burbuja en posición del cañón con velocidad en la dirección apuntada
3. Crear `scripts/gameplay/bubble.gd` (script en el bubble.tscn):
   - Move_and_slide o KinematicBody2D con velocidad
   - Rebotar en paredes laterales (no techo)
   - Al impactar grid o techo: detener, snap a la celda hexagonal más cercana
4. Cola de 2 burbujas:
   - "Current" en el cañón
   - "Next" preview a un costado del cañón
   - Tap en el cañón → swap current ↔ next
5. Después de disparar: la próxima burbuja se mueve a current, se genera una nueva next

### Deliverable
Podés apuntar con drag, ver la trayectoria con primer rebote, soltar, ver la burbuja viajar y acomodarse en el grid o pegarse al techo. La cola se actualiza correctamente. Tap en cañón cambia la burbuja current con la next.

### Validación rápida
- ¿La trayectoria se ve correcta antes de disparar?
- ¿La burbuja rebota bien en paredes?
- ¿Se acomoda en una celda válida del grid (snap correcto)?
- ¿No hay glitches al disparar rápido (fire rate)?

---

## Chunk 3 — Match detection y drops de flotantes

**Duración:** 3-4 días
**Bloquea a:** Chunk 4

### Objetivo
Cuando 3+ burbujas del mismo color se conectan, explotan. Las burbujas que pierden conexión al techo caen.

### Tareas

1. Crear `scripts/gameplay/match_detector.gd`:
   - Función `find_connected_same_color(start_col, start_row) -> Array` (flood fill BFS)
   - Llamada después de cada disparo, en la posición donde aterrizó la burbuja
   - Si retorna 3+, marca esas posiciones como "to_remove"
2. Implementar animación de explosión en `bubble.gd`:
   - Scale 1.0 → 1.2 → 0 con tween
   - Particle simple de placeholder
   - On animation end: `queue_free()`
3. Detectar burbujas flotantes:
   - Después de remover los matches, hacer otro flood fill desde cada burbuja en la fila superior (techo)
   - Cualquier burbuja que NO sea alcanzable desde el techo está flotando
   - Animación de drop: tween hacia abajo con gravity, fade out al salir de pantalla
4. Cadena de drops:
   - Si tras un drop, otra burbuja queda flotante, también cae
   - Recursión hasta que no queden flotantes
5. Score básico:
   - Cada burbuja explotada: 10 puntos
   - Cada burbuja caída: 15 puntos (1.5x)
   - Display temporal del score con `Label` en esquina

### Deliverable
Disparar 3 del mismo color → animación de explosión → si quedaron burbujas flotantes, caen → score actualiza. Un combo en cadena (drop → más flotantes → más drops) funciona correctamente.

### Validación rápida
- ¿Match de exactamente 3 funciona? ¿Y de 4, 5, 10?
- ¿Match en diagonal hexagonal funciona?
- ¿Drops de flotantes son correctos en casos complejos (pirámide pegada al techo por 1 burbuja)?

---

## Chunk 4 — Win/lose conditions

**Duración:** 2-3 días
**Bloquea a:** Chunks 5, 6

### Objetivo
Cada nivel tiene disparos limitados y un objetivo. Al cumplir el objetivo se gana, al quedarse sin disparos se pierde.

### Tareas

1. Hardcodear temporalmente: `max_shots = 25`, objetivo = "limpiar todas las burbujas"
2. Display HUD básico:
   - Top de pantalla: contador de disparos restantes
   - Top: texto del objetivo
3. Detectar victoria:
   - Si el grid queda vacío de burbujas, victoria
   - Mostrar pantalla básica "¡GANASTE!" con botón "Reintentar"
4. Detectar derrota:
   - Si los disparos llegan a 0 sin victoria, derrota
   - Mostrar pantalla básica "PERDISTE" con botón "Reintentar"
5. Reset:
   - Al hacer click en "Reintentar", el nivel se carga desde cero
   - Implementar como reload de la escena gameplay

### Deliverable
Loop jugable completo: empezás un nivel → disparás → ganás (limpiando todo) o perdés (sin disparos) → click reintentar → empezás de nuevo.

### Validación rápida
- ¿La victoria se detecta en el momento exacto de la última burbuja?
- ¿La derrota se detecta solo cuando se acabaron disparos Y no hay más posibilidades?
- ¿El reset funciona limpio sin memory leaks?

---

## Chunk 5 — Loader de niveles desde JSON

**Duración:** 2-3 días
**Bloquea a:** Chunk 6

### Objetivo
Los niveles se definen en archivos JSON externos y se cargan en runtime. Esto desbloquea el flujo de Diego + Claude para generación AI de niveles.

### Tareas

1. Definir formato JSON según GDD sección 14.4. Ejemplo en `data/levels/001.json`:

```json
{
  "id": 1,
  "chapter": 1,
  "name": "Primer encuentro",
  "objective": {
    "type": "rescue",
    "target_creature_id": "coqui",
    "trapped_position": [4, 6]
  },
  "max_shots": 22,
  "grid": {
    "width": 11,
    "height": 8,
    "bubbles": [
      [0, "red"], [1, "blue"], [2, "yellow"]
    ]
  },
  "obstacles": [],
  "available_colors": ["red", "blue", "yellow", "green"],
  "rainbow_chance": 0.0
}
```

2. Implementar el ya stubeado `LevelManager.load_level(level_id)`:
   - Lee el archivo, parsea JSON
   - Valida campos requeridos
   - Retorna Dictionary con los datos del nivel

3. Modificar `gameplay.gd` para inicializar el grid desde el Dictionary cargado:
   - Spawn de burbujas en las posiciones especificadas en `grid.bubbles`
   - Setear `max_shots` desde el JSON
   - Setear el objetivo desde el JSON

4. Botones temporales de navegación de niveles (debug):
   - Botón "← Prev Level" y "Next Level →" en pantalla
   - Permite saltar entre niveles para testear

5. Implementar el objetivo "rescue":
   - El nivel tiene una posición específica donde está atrapada una criatura (placeholder: estrella amarilla grande en esa posición del grid)
   - Victoria cuando esa burbuja específica queda libre / se elimina

### Deliverable
Hay 1 nivel cargable desde JSON. Podés cambiar el JSON y el nivel cambia sin re-compilar. Botones de prev/next funcionan para iterar.

### Validación rápida
- ¿Modificar el JSON y re-correr el juego refleja el cambio?
- ¿El level loader maneja bien JSONs malformados (error claro, no crash)?
- ¿La criatura "atrapada" se ve diferente del resto del grid?

---

## Chunk 6 — Crear los 5 niveles del prototipo

**Duración:** 3-4 días
**Bloquea a:** Chunk 7

### Objetivo
Tener 5 niveles distintos jugables, con dificultad creciente, todos definidos en JSON.

### Tareas

1. Diseñar 5 niveles a mano siguiendo curva:
   - Nivel 1: muy fácil, 4-5 burbujas a romper, 22 disparos. Onboarding implícito.
   - Nivel 2: fácil, primer rescate de criatura, 24 disparos
   - Nivel 3: introduce rescate más difícil con criatura más enterrada, 26 disparos
   - Nivel 4: medio, grid más grande (más burbujas), 28 disparos
   - Nivel 5: más difícil, configuración astuta que requiere planeo de varios disparos, 30 disparos
2. Para cada nivel: dibujar el layout en papel/Figma → trasladar a JSON → testear que se puede ganar
3. Iterar el balance de disparos máximos (regla: óptimos × 1.3) hasta que el nivel se sienta justo
4. Verificar que el progreso entre niveles está guardado de alguna forma (incluso un global var es OK para prototipo) — ganaste el 3 → arranca el 4

### Deliverable
5 niveles encadenados, jugables uno tras otro, con dificultad creciente perceptible.

### Validación rápida (haz vos primero)
- ¿Pudiste ganar los 5 sin frustrarte excesivamente?
- ¿La dificultad creció gradualmente o se siente plana / hay saltos abruptos?
- ¿Cada nivel se sintió distinto del anterior?

---

## Chunk 7 — Validation playtest

**Duración:** 1-2 días
**Bloquea a:** decisión de pasar a Fase 2 (MVP) o replantear

### Objetivo
Validar con jugadores reales (no Diego) que el prototipo es divertido.

### Tareas

1. Build standalone para macOS (Project → Export → macOS)
2. Compartir el .app a 3-5 personas de tu red personal:
   - Mezcla de perfiles: ideal incluye al menos 1 mujer 25-45 (audiencia objetivo), 1 jugador casual, 1 jugador hardcore
3. Sentarte con cada uno (idealmente en persona o video call) sin dar instrucciones
4. **Observar silencioso** mientras juegan los 5 niveles. Tomar notas:
   - ¿Sonríen, fruncen el ceño?
   - ¿Dónde se atascaron, qué les confundió?
   - ¿Abandonaron antes de terminar?
   - ¿Quisieron seguir jugando después del nivel 5?
5. Después del playtest, preguntar:
   - "Si esto fuera un juego real con muchos más niveles, ¿lo seguirías jugando?"
   - "¿Qué fue lo más divertido?"
   - "¿Qué fue lo más frustrante?"
   - Escala 1-10: ¿qué tan divertido fue?

### Decisión post-playtest

Tras los 3-5 playtests, evaluar:

| Resultado | Acción |
|---|---|
| Promedio diversión ≥7, mayoría querría seguir jugando | **Pasar a Fase 2 (MVP).** El core es sólido. |
| Promedio 5-7, sentimiento mixto | **Iterar mecánicas core 1-2 semanas más** antes de decidir. Identificar lo más roto. |
| Promedio <5, mayoría no quiere seguir | **Replantear.** Algo del core no funciona. Volver a Fase 0 o cancelar. |

Esta es **la decisión más importante del proyecto**. No la skipees por ansiedad de avanzar.

---

## Trabajo en paralelo posible durante Fase 1

Mientras codeás los chunks, hay tareas no-código que podés avanzar en paralelo en sesiones separadas (ideal para días que no estés con energía para programar):

- **Wireframes en Figma** (Tarea #3 ejecución) — el spec ya está, solo es ejecutarlo visualmente
- **Setup de cuentas:** Apple Developer ($99/año), Google Play Console ($25 una vez)
- **Verificación de "Coralia"** y "myappcube" en stores y dominios (decisión administrativa pendiente)
- **Investigar AI gen para arte:** explorar Midjourney/Stable Diffusion/Flux para concept art de Marina y las criaturas

---

## Métricas de éxito de Fase 1

Al final de las 3-4 semanas:

- ✅ Los 7 chunks completados
- ✅ Build standalone funcional
- ✅ 3-5 playtests con feedback estructurado documentado
- ✅ Decisión clara: avanzar a Fase 2 / iterar / replantear
- ✅ Si vamos a Fase 2: lista priorizada de "qué arreglar primero" basada en feedback

---

## Cambios y versiones

| Versión | Fecha | Cambios |
|---|---|---|
| 0.1 | 2026-05-01 | Plan inicial: 7 chunks, 3-4 semanas, validation playtest como decisión central |
| 0.2 | 2026-05-01 | **Fase 1 completada.** Chunks 1-6 ejecutados en una sesión. Chunk 7 (validation playtest) skipped por decisión del solo dev — guía y templates creados en `05_Playtest_Guide_Coralia.md` para hacerlo informalmente antes del global launch. Trabajo posterior tracked en GitHub issues vía `06_Backlog_GitHub_Issues.md`. |

## Resultado final de Fase 1

| Chunk | Estado | Deliverable |
|---|---|---|
| 1. Grid hexagonal | ✅ | 84 burbujas en hex grid funcional |
| 2. Cañón + disparo | ✅ | Drag aim + trayectoria + snap a grid |
| 3. Match + drops | ✅ | Flood-fill + gravity drops + score |
| 4. Win/lose | ✅ | HUD + modal + retry |
| 5. JSON levels | ✅ | LevelManager + rescue objective + nav debug |
| 6. 5 niveles | ✅ | Curva de dificultad + smart queue + color shuffle + queue rotation animation |
| 7. Validation playtest | ⏭️ Skipped | Templates listos en `docs/05_Playtest_Guide_Coralia.md` |

## Próximo paso

Trabajo posterior se gestiona desde `06_Backlog_GitHub_Issues.md`. Convertir esos issues a GitHub issues reales y continuar con Claude Code en CLI.
