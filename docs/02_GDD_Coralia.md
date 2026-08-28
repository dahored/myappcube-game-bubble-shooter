# Game Design Document — Coralia

**Versión:** 0.1 (en construcción)
**Fecha:** 2026-04-30
**Autor:** Diego
**Documento vivo:** sí — se actualiza durante todo el desarrollo

---

## Cómo leer este documento

Este GDD es la fuente única de verdad para Coralia. Cada decisión de diseño, mecánica, número, sistema o flujo del juego vive acá. Cuando una respuesta no esté en el GDD, la decidimos y la añadimos antes de codificar.

El concepto creativo (visión, tema, pilares, audiencia) ya está fijado en `01_Concepto_Inicial.md`. Este GDD asume ese concepto y se enfoca en cómo se construye.

## Tabla de contenidos

1. **Mecánicas Core** — el motor del juego (disparo, grid, match, win/lose)
2. **Diseño de niveles** — tipos de objetivos, obstáculos, curva de dificultad
3. **Power-ups** — qué hace cada uno, cuándo se usan, qué cuestan
4. **Sistemas de progresión del jugador** — XP, niveles, desbloqueos
5. **Meta-juego: el Santuario** — restauración del arrecife, criaturas rescatadas
6. **Economía** — monedas, gemas, vidas, drops, costos
7. **Sistemas de retención** — racha, daily, misiones, eventos, leaderboards
8. **Battle Pass** — temporadas, tracks, recompensas
9. **Monetización** — IAP, ads, suscripción
10. **UI / Pantallas** — las 16 pantallas del MVP
11. **Arte y dirección visual** — paleta, estilo, lista de assets
12. **Audio** — música y SFX
13. **Narrativa y personajes** — Marina, criaturas hero, antagonista
14. **Stack técnico y arquitectura** — Godot 4, librerías, servicios
15. **Analytics** — eventos a trackear
16. **Roadmap Post-MVP / Opcionales** — features deferidas, fase 2+, ideas en radar
17. **Apéndices** — glosario, referencias, archivos

---

## 1. Mecánicas Core

### 1.1 Vista general del gameplay

El jugador toca y arrastra desde la parte inferior de la pantalla para apuntar un cañón ubicado en el centro-bajo. Una **línea de trayectoria** muestra hacia dónde irá la burbuja, incluyendo el primer rebote contra paredes. Al soltar el dedo, dispara la burbuja. Si la burbuja toca otras dos o más burbujas del mismo color al impactar, todas explotan. Las burbujas que queden flotando sin conexión al techo caen y otorgan puntos extra. El nivel se completa cuando se cumple el **objetivo** (sección 2) antes de que se acaben los disparos.

### 1.2 El grid

| Parámetro | Valor | Notas |
|---|---|---|
| Forma del grid | **Hexagonal** | Estándar de la industria. Las filas alternas se desplazan medio diámetro de burbuja. Permite hasta 6 vecinos por celda en lugar de 4. |
| Ancho del grid | 11 columnas en filas pares, 10 en filas impares | Cabe cómodo en pantalla portrait con margen lateral. |
| Alto inicial visible | 8 filas | El resto va apareciendo conforme el jugador dispara o por movimiento del techo. |
| Anclaje | Las burbujas cuelgan del **techo** | Si una burbuja queda sin conexión al techo, cae. |

### 1.3 El cañón

| Parámetro | Valor | Notas |
|---|---|---|
| Posición | Centro-bajo de la pantalla, sobre una "concha" o "almeja" temática | Visual cozy submarino. |
| Aim | Drag desde cualquier punto de la pantalla inferior | Más accesible y preciso que tap-to-aim. |
| Indicador de trayectoria | Línea punteada con primer rebote visible | Estándar moderno. Sin trayectoria los jugadores casuales sufren. |
| Disparo | Al soltar el dedo | Más tactile que tap separado. |
| Cola de burbujas | **2 visibles**: la actual y la siguiente | Permite planeo táctico sin saturar UI. |
| Color swap | Tap en la burbuja actual para intercambiarla con la siguiente | Mecánica gratis básica, no power-up. |

### 1.4 Reglas de match

- **Mínimo para explotar:** 3 burbujas conectadas del mismo color (incluida la recién disparada).
- **Drop de flotantes:** cualquier grupo de burbujas que pierda conexión al techo cae automáticamente y otorga puntos bonus (1.5x por burbuja caída vs. burbuja explotada).
- **Cadenas:** si un drop a su vez deja flotantes a otras burbujas, esas también caen (combo en cadena).
- **Cap de combo:** sin cap; los combos largos suben score multiplicador y disparan animación especial (visual reward).

### 1.5 Físicas

| Parámetro | Valor | Notas |
|---|---|---|
| Velocidad de disparo | ~1500 px/s en pantalla 1080p | Suficientemente rápida para feel responsivo, suficientemente lenta para ver trayectoria. |
| Rebotes en paredes | Sí | Verticales (izquierda/derecha) rebotan, techo no rebota (queda anclado al impactar). |
| Snap a grid | Sí, al impactar | La burbuja se acomoda en la celda hex más cercana al punto de contacto. |
| Animación de explosión | 0.25s con partículas + sonido | Feedback inmediato; el siguiente disparo ya está disponible durante la animación. |

### 1.6 Condiciones de victoria y derrota

**Victoria:** se cumple el objetivo del nivel (definido en sección 2) antes de que se acaben los disparos disponibles.

**Derrota:** ocurre por una de dos razones:
1. Se acaban los disparos disponibles sin cumplir el objetivo.
2. El "techo" del grid llega a la línea de muerte (en niveles donde el techo desciende cada N disparos — opcional por nivel).

Al perder, se ofrece **continuar** viendo un anuncio (rewarded ad → +5 disparos) o gastando gemas (configurable, ~10-15 gemas).

### 1.7 Decisiones lockeadas (2026-04-30)

| Decisión | Valor | Razón |
|---|---|---|
| Sistema de límite | **Disparos limitados** por nivel | Puzzle-feel sin presión de tiempo. Calza con tono cozy y audiencia 25-45. |
| Burbuja arcoíris | **Sí**, aparece como burbuja rara (~5% de tiros) | Estándar del género; momentos satisfactorios para casuales. |
| Fire rate | **Rápido** — siguiente disparo disponible apenas el anterior sale del cañón | Feel moderno y responsivo. |

---

## 2. Diseño de niveles

### 2.1 Estructura general

El MVP tiene **90 niveles**, agrupados en 6 **capítulos** de 15 niveles cada uno (3 fáciles, 5 normales, 4 difíciles, 2 wall/boss, 1 bonus). Cada capítulo desbloquea una **zona** del arrecife que se va llenando de color y vida en la pantalla del santuario (ver sección 5).

| Capítulo | Niveles | Zona del arrecife | Tema visual |
|---|---|---|---|
| 1. La Cala Apagada | 1–10 | Cala de entrada | Gris-azulado pálido, rocas apagadas |
| 2. Jardín de Anémonas | 11–20 | Jardín costero | Pasteles suaves rosados |
| 3. Bosque de Algas | 21–30 | Kelp forest | Verdes esmeralda y dorados |
| 4. Cueva de Cristales | 31–40 | Cuevas iluminadas | Violetas, cyan, brillos |
| 5. Profundidades de Coral | 41–50 | Arrecife profundo | Naranjas, púrpuras, vivos |
| 6. Ciudad de las Perlas | 51–60 | Final reveal | Dorados, blanco perlado, magia |

### 2.2 Tipos de objetivos por nivel

Los niveles no son todos "limpia el tablero". La variedad de objetivos es lo que mantiene fresco al jugador a lo largo de 60+ niveles. Diseño 5 tipos de objetivos para Coralia:

| Tipo | Descripción | Cuándo se introduce |
|---|---|---|
| **Rescate** | Liberar a una criatura atrapada eliminando las burbujas que la rodean. La criatura cae cuando queda libre. | Nivel 1 (objetivo principal del juego) |
| **Limpia el techo** | Eliminar todas las burbujas de la fila superior. Una vez vacía la fila, victoria. | Nivel 5 |
| **Caza de color** | Eliminar X burbujas de un color específico (mostrado en HUD). | Nivel 11 |
| **Conducir al fondo** | Llevar una criatura pesada (ej. tortuga) hasta una línea inferior empujándola con explosiones. | Nivel 21 |
| **Multi-rescate** | Liberar a 3-5 criaturas en el mismo nivel. | Nivel 31 |

El **objetivo "Rescate"** es la mecánica narrativa central del juego (cada criatura rescatada se une al santuario), por lo que aparece en al menos el **60% de los niveles**. Los otros tipos varían el ritmo.

### 2.3 Obstáculos

Los obstáculos suben la dificultad y aportan variedad puzzle. Se introducen progresivamente:

| Obstáculo | Comportamiento | Cuándo se introduce |
|---|---|---|
| **Burbuja de hielo** | Cubierta de hielo. Necesita 2 impactos: el primero rompe el hielo, el segundo rompe la burbuja. | Nivel 11 |
| **Jaula de coral** | Encierra a una criatura. Se rompe al hacer match adyacente. | Nivel 16 |
| **Burbuja pegajosa** | No cae aunque pierda conexión al techo. Solo se elimina por match directo. | Nivel 21 |
| **Generador de algas** | Cada 5 disparos produce una burbuja nueva en una posición fija. | Nivel 26 |
| **Burbuja-bomba** | Si no se elimina en X turnos, explota y dispersa burbujas aleatorias. Tensión adicional. | Nivel 36 |
| **Cadena viva** | Dos burbujas conectadas por un hilo. Solo se rompen ambas a la vez. | Nivel 46 |

### 2.4 Curva de dificultad

La curva está calibrada para que el **D7 retention** sea alto (jugadores que llegan al día 7 son los que monetizan). Sigue el principio del Plan Maestro: enganchar fácil, introducir gradualmente, walls de pago estratégicos al final.

```
Dificultad  ──────────────────────────────────────────────────────
            ███
            ███████                                          ████
            ███████████                                  ████████
            ████████████████                         ████████████
            ███████████████████████          ████████████████████
Nivel       1   5   10  15  20  25  30  35  40  45  50  55  60
            └─Cap 1─┘└─Cap 2─┘└─Cap 3─┘└─Cap 4─┘└─Cap 5─┘└─Cap 6─┘
              fácil   intro    media    media    difícil  difícil
                              mecánica
```

| Tramo | Niveles | Tasa de éxito target | Notas |
|---|---|---|---|
| Onboarding | 1–10 | 90-95% | Muy fáciles. El objetivo es enganchar y enseñar mecánicas básicas. Ningún jugador debería quedarse atascado. |
| Introducción | 11–25 | 70-80% | Se introducen mecánicas nuevas (un obstáculo cada ~5 niveles). Algunos retries esperados. |
| Media | 26–40 | 50-60% | Combinaciones de mecánicas. Aquí aparecen las primeras tentaciones de power-ups. |
| Difícil | 41–55 | 30-40% | Walls de pago estratégicos. Estos niveles son los que monetizan: continuar con ad o gemas. |
| Climax | 56–60 | 20-30% | Niveles "hito" antes de revelar la Ciudad de las Perlas. Difíciles pero satisfactorios cuando se ganan. |

**Niveles "muro" estratégicos:** 7, 15, 23, 35, 45, 55. Son ligeramente más difíciles que los vecinos para crear momentos donde el jugador considera usar un power-up o ver un anuncio. Validar tras soft launch con datos reales.

### 2.5 Cantidad de disparos por nivel

Regla general: **disparos disponibles = disparos óptimos × 1.3**, donde "óptimos" es la solución mínima encontrada al diseñar el nivel. Esto da margen para errores del jugador casual sin volverlo trivial.

| Capítulo | Disparos típicos por nivel |
|---|---|
| 1. La Cala Apagada | 18-25 |
| 2. Jardín de Anémonas | 22-28 |
| 3. Bosque de Algas | 25-32 |
| 4. Cueva de Cristales | 28-35 |
| 5. Profundidades de Coral | 30-38 |
| 6. Ciudad de las Perlas | 32-42 |

### 2.6 Editor de niveles (herramienta interna)

Como advierte el Plan Maestro: sin editor de niveles, hacer 60 niveles a mano es una pesadilla y mata el proyecto. La decisión original de construir un editor visual en Fase 1 (época Godot) nunca se ejecutó — los niveles se siguen escribiendo/editando a mano en JSON (ver skill `level-designer`). Queda como issue separado para más adelante, no bloquea el resto del roadmap.

### 2.7 Estrategia híbrida de creación de niveles (mano + AI)

Coralia usará un flujo híbrido para crear y mantener niveles. Esto hace sostenible el LiveOps post-launch (10-15 niveles/semana) sin quemar al solo dev.

| Fase | Cantidad | Approach | Razón |
|---|---|---|---|
| Tutorial + onboarding (niveles 1-15) | 15 | Hand-design por Diego | Establecen la "voz" del juego. Críticos para D1 retention. Cada nivel necesita iteración humana fina. |
| MVP grueso (niveles 16-60) | 45 | AI-assisted (Claude genera drafts, Diego polish) | Velocidad de producción + variedad mantenida con constraints explícitos. |
| LiveOps post-launch | 10-15 por semana | AI-assisted sostenible | El ritmo realista para un solo dev sin equipo de level design. |

**Implicación arquitectónica clave:** el formato del archivo de nivel debe ser texto estructurado y editable para permitir generación AI. Decisión final (vigente): JSON, uno por nivel, bajo `coralia/Assets/Resources/Levels/Chapter_N/` — ver skill `level-designer` para el schema exacto. Si fuera binario o solo creable desde un editor visual, perderíamos esta capacidad.

**Validación de niveles AI-generated requiere:**
1. Un **solver automático** (script que simule N partidas para verificar solubilidad y dificultad real — issue #30 en el backlog, todavía no construido)
2. **Playtest humano de cada nivel** antes de marcarlo como aprobado (Claude no puede juzgar diversión)
3. Tasa esperada de aceptación: ~70-80% usables con ajustes menores, 20-30% descartados o rehechos

## 3. Power-ups

### 3.1 Filosofía de power-ups

Los power-ups en Coralia tienen tres propósitos: **(1)** dar al jugador casual herramientas para superar walls de pago sin frustrarse, **(2)** crear momentos de monetización (gemas o ads), **(3)** enriquecer el puzzle con variedad de soluciones. Se evita el pay-to-win: ningún power-up resuelve un nivel automáticamente; solo facilitan.

### 3.2 Power-ups del MVP

Seis power-ups iniciales, cubriendo distintos casos de uso:

| Power-up | Efecto | Costo (gemas) | Cuándo se desbloquea |
|---|---|---|---|
| **Bomba de coral** | Explota una zona 3x3 alrededor del impacto. Útil para limpiar racimos densos. | 8 | Nivel 5 |
| **Rayo de luz** | Elimina una columna entera de burbujas en línea recta. | 10 | Nivel 12 |
| **Cambio de color** | Cambia la burbuja actual del cañón al color que el jugador elija. | 6 | Nivel 18 |
| **Mira láser** | Muestra trayectoria extendida con TODOS los rebotes durante 3 disparos. | 7 | Nivel 24 |
| **Pez explorador** | Un pez nada por el grid eliminando 5 burbujas aleatorias del color que elijas. | 12 | Nivel 32 |
| **Burbuja de aire** | Una burbuja adicional al cañón. Suma +1 disparo al límite del nivel. | 5 | Nivel 8 |

### 3.3 Activación

Power-ups se compran y equipan en la pantalla **pre-nivel** (antes de empezar). Máximo **3 power-ups equipados por nivel**. Una vez equipados, durante el gameplay el jugador los activa tocando el ícono correspondiente en el HUD inferior. Activar consume el power-up (no es consumible permanente, es un único uso).

### 3.4 Adquisición no-monetaria

Para evitar sentirse pay-to-win:

- **Drops naturales:** algunos niveles dropean 1 power-up gratis al completarlos (~10% de niveles).
- **Battle Pass:** el track free incluye power-ups en hitos.
- **Daily rewards:** el día 4 y 7 incluyen power-ups.
- **Logros:** desbloquear un logro otorga power-ups específicos.
- **Daily missions:** algunas misiones recompensan con power-ups.

### 3.5 Decisiones lockeadas

| Decisión | Valor | Razón |
|---|---|---|
| Total power-ups MVP | **6** | Plan Maestro recomienda 4-6. Seis cubre todos los casos sin saturar UI. |
| Activación | Pre-nivel + tap durante gameplay | Estándar moderno. |
| Cap por nivel | **3 equipados** | Balance entre opciones tácticas y simplicidad. |
| Duración | Single-use por nivel | No hay power-ups permanentes (anti-P2W). |

## 4. Sistemas de progresión del jugador

### 4.1 Filosofía: una sola progresión visible

A diferencia de RPGs donde el jugador tiene XP, nivel de personaje, árboles de skills, etc., en Coralia la progresión visible es **una sola**: el número del nivel actual del mapa. Esto sigue el patrón de Candy Crush, Panda Pop, Bubble Witch — formatos donde la simplicidad refuerza la sensación de avance constante.

No hay "XP del jugador", "nivel de personaje" ni "estrellas por nivel" que se sumen a un total global. El número de nivel **es** la métrica de progreso, y se acompaña de:

| Sistema secundario | Qué progresa | Visualización |
|---|---|---|
| **Santuario** | Criaturas rescatadas + zonas desbloqueadas | Pantalla principal (sección 5) |
| **Battle Pass** | Tier dentro de la temporada actual | Pantalla del Battle Pass (sección 8) |
| **Logros** | Logros desbloqueados | Pantalla de Profile |
| **Racha diaria** | Días consecutivos jugando | HUD del santuario |

### 4.2 Estrellas por nivel (implementado en MVP)

Cada nivel otorga 1-3 estrellas basadas en **score final**. El score final incluye un bonus por tiros sobrantes que premia la eficiencia.

#### Mecánica de score final

```
score_final = score_base + (tiros_sobrantes × BONUS_PER_REMAINING_SHOT)
BONUS_PER_REMAINING_SHOT = 10  (constante a definir en el sistema de score — no implementado todavía en Unity)
```

- `score_base`: puntos acumulados durante el nivel (matches, drops, etc.)
- `tiros_sobrantes`: `max_shots - tiros_usados` al ganar
- El bonus se aplica **solo en victoria** — si perdés no hay bonus

Este sistema hace que la eficiencia importe sin añadir una métrica separada. Un jugador que borra el nivel en pocos tiros obtiene más estrellas que uno que lo pasa justo.

#### Visualización

- **Durante el nivel:** HUD muestra `☆☆☆` → va llenando `★` en tiempo real conforme sube el score (sin bonus todavía — el bonus se aplica al ganar)
- **Pantalla de victoria:** estrellas finales + desglose `score_base + bonus = total`
- **Level Select:** nodos completados muestran `★★☆` (estrellas obtenidas)

#### Workflow de diseño de niveles

Al diseñar cada nivel, el diseñador debe determinar los 3 thresholds playtestando:

1. **Jugá el nivel 3 veces:** como experto (mínimo tiros), normal, y usando todos los tiros
2. **Anotá los 3 scores finales** (ya incluyen bonus automáticamente porque jugás con el juego real)
3. **Esos scores son tus thresholds** en el JSON

```
Ejemplo — nivel con 20 burbujas, max_shots=30:

Experto   (10 tiros usados, 20 sobrantes): base ~400 + 200 = 600  → threshold 3★
Promedio  (22 tiros usados,  8 sobrantes): base ~350 +  80 = 430  → threshold 2★
Just pass (30 tiros usados,  0 sobrantes): base ~280 +   0 = 280  → threshold 1★

"star_thresholds": [280, 430, 600]
```

El threshold de 1★ debe ser alcanzable por cualquier jugador que complete el nivel (incluso usando todos los tiros). El threshold de 3★ requiere terminar con tiros sobrantes significativos.

#### Nota sobre la decisión de implementar en MVP

La decisión original era posponer. Se implementó porque:
- El sistema de bonus simplificó el balance (eficiencia → más puntos, no métrica separada)
- Los thresholds por nivel en JSON son fáciles de ajustar post-lanzamiento con data real
- La frustración "no llegué a 3★" se mitiga con el diseño cozy: 1★ siempre permite avanzar

### 4.3 Desbloqueos por nivel del mapa

Las features se desbloquean a medida que el jugador avanza, evitando saturarlo al inicio:

| Feature | Se desbloquea en nivel |
|---|---|
| Bomba de coral (power-up) | 5 |
| Burbuja de aire (power-up) | 8 |
| Caza de color (objetivo) | 11 |
| Burbuja de hielo (obstáculo) | 11 |
| Rayo de luz (power-up) | 12 |
| Jaula de coral (obstáculo) | 16 |
| Cambio de color (power-up) | 18 |
| Conducir al fondo (objetivo) | 21 |
| Burbuja pegajosa (obstáculo) | 21 |
| Mira láser (power-up) | 24 |
| Generador de algas (obstáculo) | 26 |
| Multi-rescate (objetivo) | 31 |
| Pez explorador (power-up) | 32 |
| Burbuja-bomba (obstáculo) | 36 |
| Cadena viva (obstáculo) | 46 |
| Battle Pass (acceso) | 5 |
| Tienda completa | 10 |
| Leaderboards | 15 |
| Eventos especiales | 20 |
| Daily missions | 8 |

## 5. Meta-juego: el Santuario

### 5.1 Concepto

El **Santuario de Marina** es la pantalla de meta-progresión visual (pilar #2 de retención). Es donde el jugador *vive* el resultado emocional de su progreso: cada criatura rescatada aparece nadando en el santuario, cada zona desbloqueada cobra color, y el conjunto se va transformando de un arrecife gris a un ecosistema vibrante.

El Santuario **es** la pantalla principal del juego (Main Menu). No hay un menú principal aparte: al abrir el juego, el jugador ve su santuario y desde ahí accede a todas las funciones (level select, shop, profile, etc.).

### 5.2 Estructura visual

El santuario es una vista panorámica del arrecife dividida en 6 zonas correspondientes a los 6 capítulos. Las zonas no completadas se ven grises, vacías y apagadas. Cuando el jugador completa el último nivel de un capítulo, la zona correspondiente se "ilumina" en una **animación de restauración** de 5-8 segundos: corales toman color, plantas crecen, criaturas pueblan, partículas de luz aparecen.

Esta animación es uno de los **momentos más memorables** del juego. Bien ejecutada, es el equivalente al "levelup completo" que los jugadores comparten en redes.

### 5.3 Criaturas rescatadas

Cada criatura rescatada tiene un slot fijo en su zona del santuario y nada con animación idle. Tap en una criatura abre un **bestiario** con:

- Nombre de la criatura (ej. "Coquí, el caballito de mar")
- Especie y datos curiosos (educativo light, valor para audiencia familiar)
- Diálogo corto (1-2 líneas con personalidad)
- Recompensa pasiva que da al santuario (ver 5.4)

### 5.4 Recompensas pasivas del santuario

Cada criatura genera **monedas suaves por hora** mientras el jugador no está jugando. Se acumulan hasta un cap de 8 horas y se reclaman al volver al santuario. Esto crea un loop de retorno diario sin que el jugador necesite jugar mucho.

| Tier de criatura | Monedas/hora | Cap acumulado |
|---|---|---|
| Común | 5 | 40 |
| Rara | 12 | 96 |
| Épica | 25 | 200 |

Tiers se asignan a criaturas: pez payaso (común), caballito de mar (rara), pulpo (épica), tortuga matriarca (épica), narval mítico (épica).

### 5.5 Visitas de amigos

Pilar #3 de retención (coop asíncrono). Los amigos pueden:

- **Visitar tu santuario** y ver tus criaturas
- **Dejar un regalo** (genera notificación push para ti)
- **Recibir vidas** que envías
- **Ver tu progreso comparativo** (zonas desbloqueadas)

Las visitas no son sincrónicas — son snapshots. No requiere multiplayer real-time, lo cual mantiene la complejidad técnica baja.

### 5.6 Decisiones lockeadas

| Decisión | Valor | Razón |
|---|---|---|
| Pantalla principal | El Santuario | El santuario *es* el main menu. Refuerza meta-progresión visual. |
| Zonas | 6 zonas (correspondientes a 6 capítulos) | Sincroniza con estructura de niveles. |
| Recompensa pasiva | Monedas suaves por hora con cap 8h | Loop de retorno diario sin requerir mucho juego. |
| Bestiario | Sí, con datos curiosos | Valor educativo light, audiencia familiar. |

## 6. Economía

### 6.1 Filosofía económica

La economía de Coralia está diseñada bajo los tres principios validados por la industria en 2026 y citados en el Plan Maestro: **choice** (el jugador elige cuándo y cómo gastar), **fairness** (todo se puede ganar jugando, nada está bloqueado solo por pago), **perceived value** (el jugador siente que recibe más de lo que paga). El modelo es **F2P generoso**: los jugadores que nunca pagan tienen una experiencia completa, y los que pagan obtienen conveniencia y velocidad, no ventaja competitiva.

Esto se traduce en reglas concretas:
- **Anti pay-to-win:** los power-ups y boosts solo facilitan, nunca resuelven automáticamente. Un jugador free puede ganar cualquier nivel.
- **Catch-up sano:** los free players reciben drops de gemas regulares (~1 por nivel ganado) que se acumulan a lo largo del tiempo.
- **Sin presión agresiva:** los popups de tienda aparecen solo en momentos relevantes (perdiste un nivel, vidas en cero), nunca interrumpiendo el juego.

### 6.2 Sistema de vidas

| Parámetro | Valor | Notas |
|---|---|---|
| Vidas máximas | 5 | Plan Maestro estándar |
| Tiempo de regeneración | 30 minutos por vida | 2.5 horas para llenar de cero |
| Vida perdida por | Fallar un nivel | Ganar no consume vidas |
| Vida regalada por amigo | +1 (cap diario: 5 vidas recibidas) | Pilar social asíncrono |
| Vida vía rewarded ad | 1 vida cada 2 horas (cap 3/día) | Driver de ad revenue |
| Refill instantáneo | 100 gemas (las 5 vidas) o 25 gemas por unidad | Conveniencia premium |

### 6.3 Monedas (soft currency)

Las **monedas** son la moneda casual del juego. Se ganan jugando y se gastan en compras frecuentes y de bajo valor. Visualmente representadas por **perlas pequeñas**.

**Cómo se ganan:**

| Fuente | Cantidad típica |
|---|---|
| Completar nivel (capítulo 1) | 50 monedas |
| Completar nivel (capítulo 2-3) | 75 monedas |
| Completar nivel (capítulo 4-6) | 100 monedas |
| Primera completación (bonus único por nivel) | +50% sobre la base |
| Drop pasivo del santuario | 5-25 monedas/hora (ver sección 5.4) |
| Daily rewards | 50-200 monedas (ver sección 7) |
| Misiones diarias | 100-300 monedas |

**Cómo se gastan:**

| Compra | Costo |
|---|---|
| 1 vida | 200 monedas |
| Retry inmediato sin perder vida (solo dentro del nivel actual) | 150 monedas |
| Skin común | 1,500-3,000 monedas |
| Decoraciones del santuario (cosmético) | 500-2,000 monedas |

### 6.4 Gemas (hard currency)

Las **gemas** son la moneda premium. Se obtienen lentamente jugando o comprándolas con dinero real. Visualmente representadas por **perlas iridiscentes / cristales**.

**Cómo se ganan en juego:**

| Fuente | Cantidad típica |
|---|---|
| Completar nivel (random drop ~30%) | 1-3 gemas |
| Primera completación de nivel | +1-3 gemas |
| Daily rewards (días 3, 6, 7) | 5, 10, 25 gemas |
| Logros | 5-50 gemas según rareza |
| Battle Pass (free + premium tracks) | Ver sección 8 |
| Eventos especiales | Variable |

**Promedio aproximado de gema-rate para un free player activo:** ~30-50 gemas por semana sin gastar dinero. Suficientes para usar power-ups ocasionalmente o comprar 1-2 vidas, pero no para cubrir todas las necesidades cuando el juego se pone difícil — ahí entra la monetización.

**Cómo se gastan:**

| Compra | Costo en gemas |
|---|---|
| 1 vida | 25 |
| Refill completo (5 vidas) | 100 |
| Continuar nivel (+5 disparos al fallar) | 15 |
| Bomba de coral (power-up) | 8 |
| Rayo de luz | 10 |
| Cambio de color | 6 |
| Mira láser | 7 |
| Pez explorador | 12 |
| Burbuja de aire | 5 |
| Skip de timer (Battle Pass o evento) | 1 gema/hora |
| Premium Battle Pass (alternativa al pago directo) | 500-900 gemas |

### 6.5 IAP packs (compra de gemas con dinero real)

Tabla de packs siguiendo el principio de **valor creciente** — cada tier ofrece más gemas por dólar que el anterior, incentivando compras grandes:

| Pack | Precio USD | Gemas | Bonus | Razón comercial |
|---|---|---|---|---|
| Burbujita | $0.99 | 80 | — | Entry point. Pago bajo de fricción. |
| Concha | $4.99 | 450 | +13% | Sweet spot de conversión casual. |
| Coral | $9.99 | 1,000 | +25% | Punto de mid-tier spender. |
| Tesoro | $19.99 | 2,200 | +38% | Compromiso medio. |
| Perla Real | $49.99 | 6,000 | +50% | Whale pack. ~5% de spenders. |
| Cofre Mítico | $99.99 | 13,000 | +63% | Whale absoluto. ~1% de spenders. |

**Starter Pack** (oferta única primera semana): **$2.99 → 250 gemas + 5 vidas full + 3 power-ups variados**, con etiqueta "Valor $9.99". Conversión esperada: 5-10% de nuevos usuarios (Plan Maestro). Este pack es **la oferta más importante del juego** porque convierte a free players en spenders por primera vez, y un spender de $2.99 tiene ~40% de chance de hacer un segundo IAP en los siguientes 30 días.

**Reglas del Starter Pack:**
- Aparece como popup durante los **primeros 7 días** desde install
- Se muestra repetidamente: al abrir el juego, tras ganar un nivel, al quedarse sin vidas
- Lleva un **timer visible** ("expira en 4d 12h") que crea urgencia
- **Comprable una sola vez por jugador**. Tras compra o expiración del timer, desaparece para siempre
- No se puede recomprar bajo ninguna circunstancia (canibalizaría las demás IAP)

**Vidas infinitas** (oferta de tiempo):
- 1 hora: $1.99
- 24 horas: $4.99
- 7 días: $14.99 (target jugadores que se atascan en walls)

### 6.6 Drops por nivel — economía esperada

Tabla de cuánto recibe en promedio un jugador que completa un nivel. Calibrada para que un jugador free pueda jugar 60-80 niveles sin nunca pagar y sentirse satisfecho:

| Recurso | Drop típico (por nivel completado) |
|---|---|
| Monedas | 50-100 (según capítulo) |
| Gemas (random ~30% de niveles) | 1-3 |
| Power-up gratis (random ~10% de niveles) | 1 power-up aleatorio |
| Battle Pass XP | 50-100 |
| Criatura (cuando aplica al objetivo) | 1 criatura nueva al santuario |

**Promedio acumulado tras 60 niveles completados (jugador free):**
- ~4,500 monedas
- ~50-80 gemas (sin contar daily, achievements, battle pass)
- ~6 power-ups gratis
- 30-40 criaturas en el santuario

### 6.7 Continuar tras fallar un nivel

Momento clave de monetización. Cuando el jugador se queda sin disparos antes de cumplir el objetivo:

| Opción | Recompensa | Costo |
|---|---|---|
| **Aceptar derrota** | Pierde 1 vida | Gratis (default) |
| **Ver rewarded ad** | +5 disparos al nivel actual | Gratis (cap 5 ads/día) |
| **Pagar gemas** | +5 disparos | 15 gemas |

La pantalla muestra primero la opción del ad (más prominente), luego la opción de gemas. Esto sigue principio de fairness: los free players siempre tienen el ad como salida.

### 6.8 Sources vs Sinks (balance económico)

Para que la economía no se rompa (jugadores acumulando demasiado o muy poco), las fuentes y los sinks deben estar balanceados. Versión simplificada del balance esperado:

**Monedas:**
- Sources/semana (jugador casual D7+): ~6,000 monedas (drops + santuario + dailies)
- Sinks/semana esperados: ~4,000-5,000 monedas (vidas ocasionales + 1 cosmético)
- Buffer mensual: positivo, jugador siente "abundancia" en monedas

**Gemas (free player):**
- Sources/semana: ~30-50 gemas
- Sinks/semana: ~40-60 gemas (1-2 power-ups/sesión, vida ocasional)
- Buffer mensual: ligeramente negativo → presión orgánica para comprar gemas en momentos de wall

**Gemas (paying player que compra Concha $4.99/mes):**
- Sources/semana: ~30-50 + (450 gemas/mes) = ~140 gemas/semana
- Sinks/semana: ~100-130 (compra de power-ups con menos restricción)
- Buffer: positivo, sensación de holgura

### 6.9 Decisiones lockeadas

| Decisión | Valor | Razón |
|---|---|---|
| Dual currency | Monedas (soft) + Gemas (hard) | Estándar mobile F2P. Permite gating fino. |
| Vidas máximas | 5 | Plan Maestro |
| Regen vida | 30 min | Plan Maestro |
| Pricing tiers IAP | $0.99 / $4.99 / $9.99 / $19.99 / $49.99 / $99.99 | Estándar mobile + bonus creciente |
| Starter Pack | $2.99 → "valor $9.99" en primera semana | Plan Maestro |
| Continuar nivel | 15 gemas o ad gratis (cap 5/día) | Balance fairness + ad revenue |
| Filosofía | Anti pay-to-win, generoso con free | Estándar 2026 |

### 6.10 Cadencia de ofertas (offer rotation)

El Starter Pack es la oferta de bienvenida, pero el juego mantiene una rotación constante de ofertas a lo largo de la vida del jugador para sostener engagement y revenue. El jugador nunca debe abrir el juego sin tener **algo** disponible para comprar — pero las ofertas grandes son raras para preservar su valor.

| Tipo de oferta | Cuándo aparece | Frecuencia | Precio típico |
|---|---|---|---|
| Starter Pack | Días 1-7 del jugador nuevo | 1 sola vez | $2.99 |
| Battle Pass premium | Cada temporada de 30 días | 12/año | $4.99 |
| Welcome Back Pack | Tras 7+ días de inactividad | Cuando aplica | $1.99 |
| Progress Packs | Hitos: nivel 25, 50, 100, 200 | ~5-8/año | $1.99-$4.99 |
| Weekend Deal | Viernes-domingo cada semana | 52/año | $0.99-$2.99 |
| Holiday/Event Packs | Eventos estacionales | 5-7/año | $4.99-$9.99 |
| Flash Sale (24h) | Aleatorio | ~6-10/año | Variable |
| Whale Pack personalizado | Para spenders ya identificados | Variable | $19.99-$99.99 |

**Reglas generales de la cadencia:**

- **Solapamiento controlado:** máximo 2 ofertas activas simultáneamente para no saturar al jugador.
- **Sin oferta entre oferta:** cuando termina una promoción, hay un gap de al menos 24h antes de la siguiente para no agotar la sensibilidad al precio.
- **Personalización post-data:** una vez que tengamos analytics, las ofertas se ajustan al perfil (ej. casuals ven Weekend Deal, mid-tier ven Progress Pack, whales ven Whale Pack). Implementación detallada en LiveOps post-launch.
- **Second Chance Starter Pack:** considerar para fase post-launch — una oferta especial similar al Starter (pero menos generosa) para jugadores muy engaged que no compraron en su primera semana. Rescata conversiones perdidas. Decisión final tras analizar datos.

### 6.11 Decisiones por validar tras soft launch

Estos números son **hipótesis** que se validan con datos reales en Fase 3 (soft launch en 2-3 países pequeños). Los KPI a observar:

- **ARPDAU** (Average Revenue Per Daily Active User) target: $0.10-0.25 para casual puzzle
- **Conversion rate** (% de usuarios que pagan al menos una vez): target 3-5%
- **D1 / D7 / D30 retention:** 40% / 20% / 8% mínimo viable
- **Wall fail rate** en niveles 35, 45, 55: deben ser difíciles pero no rage-quit

Si la data muestra que los drops de gemas son demasiado generosos (jugadores no necesitan comprar), se reduce. Si son muy restrictivos (alta uninstall en walls), se afloja. El balance final solo se conoce con jugadores reales.

## 7. Sistemas de retención

### 7.1 Filosofía de retención

Los sistemas de retención son lo que separa un juego mediocre de uno adictivo. **Sin retención no hay monetización.** El Plan Maestro lo deja claro: D7 retention >25% es el mínimo viable para que un F2P sea económicamente sano. Todo lo de esta sección está diseñado para hacer que el jugador vuelva mañana, pasado, dentro de una semana.

Tres mecánicas psicológicas guían el diseño:

| Mecánica | Cómo se usa en Coralia |
|---|---|
| **Loss aversion** (perder duele más que ganar) | Racha diaria — perderla cuesta emocionalmente. Vidas que regeneran (no perder oportunidad). |
| **Variable rewards** (recompensa variable es más adictiva que fija) | Drops aleatorios de gemas y power-ups. Cofres con contenido sorpresa. |
| **Sunk cost** (más invertido = menos quiero abandonar) | Santuario que crece, criaturas coleccionables, Battle Pass que avanza. |

### 7.2 Racha diaria (Daily Streak)

Loop de 7 días con recompensa creciente. Rompe la racha si pierde un día.

| Día | Recompensa | Valor estimado |
|---|---|---|
| 1 | 50 monedas | Bajo, friendly entrance |
| 2 | 100 monedas | |
| 3 | 5 gemas | Primera gema "gratis" |
| 4 | 1 power-up aleatorio | |
| 5 | 200 monedas + 1 vida | |
| 6 | 10 gemas | |
| 7 | **25 gemas + 1 power-up raro + 1 skin de burbuja** | Gran recompensa, lo que mantiene la racha viva |

**Rotación:** tras el día 7, vuelve al día 1. La recompensa del día 7 escala ligeramente con el ciclo (semana 2 día 7 da 30 gemas, semana 3 día 7 da 35, etc., con cap en 50).

**Streak Shield:** el jugador puede comprar (50 gemas) o ganar (logros) un "escudo de racha" que protege de perder un día si no abre el juego. Máximo 1 escudo activo a la vez.

**HUD:** la racha aparece como icono persistente en el santuario. Indicador de tiempo restante en las últimas 4 horas del día ("Tu racha vence en 3h 12m") con animación de urgencia.

### 7.3 Daily missions (3 por día)

Reset cada 24h a la medianoche local del jugador. El sistema selecciona 3 misiones de un pool de ~20 templates, ponderadas por dificultad.

**Ejemplos de templates:**

| Misión | Recompensa típica |
|---|---|
| Gana 3 niveles | 100 monedas |
| Pop 100 burbujas | 100 monedas |
| Rescata 2 criaturas | 150 monedas + 1 gema |
| Usa 5 power-ups | 100 monedas |
| Gana 1 nivel sin perder vida | 200 monedas |
| Logra un combo x5 o más | 150 monedas + 2 gemas |
| Completa 1 nivel del capítulo más alto desbloqueado | 200 monedas |
| Visita el santuario de un amigo | 100 monedas |

**Bonus por completar las 3:** un cofre extra con 5 gemas + 1 power-up.

### 7.4 Weekly missions (5 por semana)

Reset cada lunes 00:00 local. Misiones más grandes que las daily, recompensas mejores, pueden requerir varios días de juego.

**Ejemplos:**

| Misión | Recompensa típica |
|---|---|
| Gana 30 niveles | 20 gemas |
| Acumula 5,000 monedas | 15 gemas |
| Rescata 15 criaturas | 25 gemas + 1 power-up raro |
| Usa 25 power-ups | 15 gemas |
| Logra 5 primeras completaciones | 30 gemas |

**Bonus por completar las 5:** cofre semanal con 50 gemas + 3 power-ups + 1 burbuja arcoíris consumible.

### 7.5 Logros (Achievements)

40 logros en MVP (centro del rango 30-50 que recomienda Plan Maestro). Tres tiers: **bronce** (12), **plata** (18), **oro** (10). Cada logro otorga recompensa única.

**Categorías de logros:**

| Categoría | Ejemplos | Tier |
|---|---|---|
| Progresión | Gana 10/50/200 niveles | Bronce/Plata/Oro |
| Coleccionismo | Rescata 10/30/60 criaturas | Bronce/Plata/Oro |
| Skill | Combo x5 / x10 / x20 | Bronce/Plata/Oro |
| Restauración | Completa zona 1 / 3 / 6 del santuario | Bronce/Plata/Oro |
| Generosidad social | Envía 10 / 50 / 200 vidas | Bronce/Plata/Oro |
| Eficiencia | Gana nivel sin power-ups / con disparos perfectos | Plata |
| Constancia | Racha de 7 / 30 / 100 días | Bronce/Plata/Oro |

**Recompensas:**
- Bronce: 5-10 gemas + 100 monedas
- Plata: 15-25 gemas + 1 power-up
- Oro: 50 gemas + skin exclusiva + entrada en hall of fame

Todos los logros aparecen en pantalla de Profile, con barra de progreso.

### 7.6 Notificaciones push inteligentes

Las push son armas de doble filo: bien usadas traen al jugador de vuelta, mal usadas generan uninstall. **Reglas estrictas:**

- Cap **2 push notifications por día**, ningún jugador recibe más
- Ninguna entre 22:00 y 09:00 hora local del jugador
- El jugador puede deshabilitar tipos específicos en Settings
- Si el jugador desinstala o ignora 5 push consecutivas, el sistema reduce a 1/semana

**Tipos de push:**

| Tipo | Trigger | Mensaje ejemplo |
|---|---|---|
| Vidas llenas | 2.5h después de quedarse en 0 vidas | "Tus vidas están llenas. Marina te necesita." |
| Racha en peligro | 4h antes de que termine el día sin haber jugado | "Tu racha de 12 días vence en 4 horas." |
| Daily reset | 09:00 local | "Nuevas misiones diarias en el arrecife." |
| Battle Pass | Tier nuevo casi alcanzable | "Estás a 2 niveles del próximo premio del Battle Pass." |
| Evento | Evento empieza o está por terminar | "Festival de Coral comienza hoy." |
| Criatura solitaria | Criatura rescatada con personalidad | "Coquí extraña a Marina en el santuario." |
| Welcome back | Jugador inactivo 7+ días | "Te extrañamos. Hay un regalo esperándote." |

### 7.7 Leaderboards

Plan Maestro: "Per nivel y global, reset semanal". Adoptado.

| Tipo | Reset | Reward |
|---|---|---|
| **Global semanal** | Cada lunes 00:00 UTC | Top 10 → 100 gemas + skin / Top 100 → 50 gemas / Top 1000 → 25 gemas |
| **Amigos semanal** | Cada lunes | Top 1 entre amigos → 25 gemas y bragging rights |
| **Per-nivel (high score)** | Persistente | No premia con gemas. Es solo el record del jugador, comparable con amigos. |

**Cómo se acumulan puntos para el global:** suma de scores de niveles ganados durante la semana. Esto incentiva variedad (jugar muchos niveles) y skill (sacar combos largos para más puntos).

**Anti-cheat:** validación server-side de scores improbables. Score que supere techo razonable se descarta (clientes hackeados son la principal fuente de top fraudulentos en el género).

### 7.8 Eventos temporales

Eventos cortos (3-7 días) que rompen la rutina y dan razones extra para jugar. Plan Maestro: "Eventos temporales semanales".

**Cadencia recomendada:** 1-2 eventos pequeños por mes + 1 evento grande estacional.

**Tipos de eventos:**

| Evento | Duración | Mecánica |
|---|---|---|
| Festival de Coral (mensual) | 5 días | Niveles con bonificación de gemas. Leaderboard exclusivo del evento. |
| Luna Llena Submarina | 3 días | Drop rates de power-ups duplicados. Aparece cada luna llena real (~mensual). |
| Marea de Coleccionables | 7 días | Una criatura mítica solo aparece durante el evento. Si te la pierdes, vuelve solo en el siguiente ciclo (varios meses). |
| Holiday Events | Halloween, Navidad, Verano, Año Nuevo | Re-skin temporal del arrecife. Niveles especiales temáticos. |

**Estructura interna de los eventos:** cada evento tiene su propio leaderboard, sus propios objetivos (separados de daily missions), y su propio paquete de recompensas (cofre del evento). El jugador puede comprar un "boost" del evento ($1.99-$4.99) para acelerar progreso — fuente extra de revenue.

### 7.9 Sistema social

Plan Maestro: conectar con Facebook/Game Center, ver amigos, enviar vidas, leaderboard semanal con reset y recompensas para top 10. Más tarde: "Compartir victoria en redes con imagen generada".

**Funcionalidad MVP:**

| Feature | Implementación |
|---|---|
| Auth con Facebook / Game Center / Apple ID | Permite recuperar progreso entre devices y socializar |
| Friends list | Importada desde Facebook + búsqueda por código de amigo |
| Enviar vida | Tap en amigo → "enviar vida". El amigo recibe push notification. Cap 5 envíos/día por jugador. |
| Pedir vida | El jugador puede pedir vidas a amigos al quedarse en 0. Cap 5 pedidos/día. |
| Visitar santuario | Tap en amigo → ver su santuario en read-only. Decoraciones, criaturas rescatadas. Botón "dejar regalo" (genera notif al amigo, da 50 monedas para vos). |
| Compartir victoria | Generar imagen automática "Marina rescató al pulpo Lumi en el nivel 32" + botón compartir |

**Funcionalidad post-MVP:** chat 1:1, gremios/clanes, partidas competitivas asíncronas (vos juegas un nivel, mandás el score y un amigo intenta superarlo dentro de 24h).

### 7.10 Decisiones lockeadas

| Decisión | Valor | Razón |
|---|---|---|
| Racha diaria | 7-day loop con escalado en ciclos siguientes | Plan Maestro + best practice |
| Daily missions | 3 por día, pool de 20 templates | Variedad sin saturar |
| Weekly missions | 5 por semana | Balance commitment + reward |
| Logros | 40 (12 bronce + 18 plata + 10 oro) | Centro del rango Plan Maestro |
| Push cap | 2/día, 22:00-09:00 silencio | Anti-spam |
| Leaderboard | Semanal global + amigos + per-nivel | Plan Maestro |
| Eventos | 1-2/mes + estacionales | Cadencia industria casual |
| Auth social | Facebook + Apple ID + Game Center | Cross-platform y cross-device |

## 8. Battle Pass

### 8.1 Por qué Battle Pass es crítico

El Plan Maestro lo nombra como **la innovación monetaria más importante de 2026**. Los datos lo confirman: el Battle Pass convierte entre 12-18% de usuarios activos a paying, comparado con solo 2-5% del IAP tradicional. Esto se debe a que combina tres elementos psicológicos potentes:

1. **Compromiso temporal:** "lo gané, no quiero perderlo" — efecto sunk cost
2. **Progreso visible:** cada nivel jugado avanza el track, gratificación constante
3. **FOMO temporal:** "si no lo termino, no recupero estas recompensas"

Pero el factor decisivo de conversión en Coralia es el **modo sin anuncios mientras dure la temporada**. Los jugadores que odian los ads pero no quieren pagar por una eliminación permanente convierten masivamente al Battle Pass.

### 8.2 Estructura de temporada

| Parámetro | Valor |
|---|---|
| Duración | **30 días** por temporada |
| Tiers totales | **40 tiers** |
| Tracks | **2: Free track** (gratis para todos) + **Premium track** (pagado) |
| Tema | Cada temporada tiene tema visual único (re-skin del HUD del Battle Pass) |
| Continuidad | Al terminar, las recompensas no reclamadas se pierden |
| Inicio de siguiente | El día siguiente al fin de la actual, sin gap |

### 8.3 Sistema de XP

Cada acción del jugador otorga XP del Battle Pass. La curva está calibrada para que un jugador casual (15-25 niveles por semana) pueda terminar el track free al final de los 30 días, y un jugador comprometido pueda terminar el premium track con tiempo de sobra.

| Acción | XP otorgado |
|---|---|
| Ganar un nivel | 50 XP |
| Primera completación de un nivel | +25 XP bonus |
| Completar daily mission | 50 XP por misión + 100 XP de bonus por las 3 |
| Completar weekly mission | 100 XP por misión + 250 XP de bonus por las 5 |
| Logro desbloqueado | 100-300 XP según tier del logro |
| Evento mensual completado | 500 XP |

**XP por tier:** 1,000 XP. Total para terminar 40 tiers: 40,000 XP. Con 4 semanas de juego casual (~25 niveles/semana + dailies + weeklies), un jugador acumula ~38,000-45,000 XP — diseñado para que la mayoría termine el track con esfuerzo razonable, no automáticamente.

### 8.4 Free track (todos los jugadores)

40 recompensas pequeñas con hitos cada 5 tiers. Total estimado de valor:

| Hito | Recompensa típica |
|---|---|
| Tier 1-5 | 50-100 monedas por tier |
| Tier 5 | 5 gemas |
| Tier 10 | 1 power-up común |
| Tier 15 | 10 gemas |
| Tier 20 | 1 skin de burbuja exclusiva (free) |
| Tier 25 | 15 gemas |
| Tier 30 | 1 power-up raro |
| Tier 35 | 20 gemas |
| Tier 40 | **Decoración exclusiva del santuario** (cosmético free, da bragging rights) |

**Valor total free track aproximado:** 2,500 monedas + 50 gemas + 5 power-ups + 2 cosméticos exclusivos.

### 8.5 Premium track ($4.99)

Mismas 40 tiers, pero cada tier tiene una recompensa adicional sustancial. Más el **modo sin anuncios** durante toda la temporada.

| Hito | Recompensa adicional (sobre free) |
|---|---|
| Activación inmediata al comprar | **No más ads** durante 30 días + 50 gemas instantáneas + 1 vida full + skin "Recién Llegada" |
| Tier 1-10 | 10-25 gemas adicionales por tier |
| Tier 5 | Power-up raro extra |
| Tier 10 | **Skin del cañón exclusiva** |
| Tier 15 | 1 burbuja arcoíris consumible (rara) |
| Tier 20 | **Criatura skin exclusiva del santuario** (la criatura del tema de la temporada) |
| Tier 25 | 100 gemas |
| Tier 30 | Pack de 5 power-ups raros |
| Tier 35 | 150 gemas |
| Tier 40 | **Skin de Marina exclusiva del tema de la temporada** + 200 gemas + título cosmético |

**Valor total premium track:** ~700-900 gemas + 15 power-ups + 4 skins exclusivas + 30 días sin ads.

**ROI percibido:** un jugador que paga $4.99 obtiene contenido valuado en aproximadamente $25-30 si se comprara como IAP individual. Esta percepción de "obtuve mucho más de lo que pagué" es lo que hace funcionar el modelo.

### 8.6 Battle Pass Pro ($9.99) — opcional fase 2

Para fases posteriores, se puede agregar un tier superior:

- Todo lo del premium ($4.99)
- **+10 tiers automáticos al activarlo** (catch-up para jugadores que se atrasaron)
- 200 gemas adicionales instantáneas
- Skin "Pro" exclusiva del tema
- 7 días de "Auto Daily Mission Complete" (las daily missions se completan solas durante 1 semana)

**Decisión MVP:** lanzar solo el premium $4.99 inicialmente. Agregar Battle Pass Pro tras 3-6 meses cuando hay base de usuarios leales, según data de spending.

### 8.7 Temas de las primeras temporadas

Cada temporada tiene un tema visual y narrativo. Los tematices proponen:

| Temporada | Tema | Skin de Marina | Criatura especial |
|---|---|---|---|
| 1: Despertar del Coral | Lanzamiento — colores pasteles luminosos | Marina "Despertar" (vestido coral) | Pez payaso real |
| 2: Festival de Mareas | Tropical, plumas, máscaras | Marina "Festival" (corona de algas) | Caballito de mar dorado |
| 3: Luna Llena Submarina | Nocturno, fosforescencia | Marina "Lunar" (vestido azul fosforescente) | Medusa lunar |
| 4: Tesoro Hundido | Aventura, oro, perlas | Marina "Cazatesoros" | Cangrejo dorado |
| 5: Jardín Profundo | Botánico submarino, anémonas grandes | Marina "Florista" | Pulpo florido |
| 6: Eclipse Coral | Misterio, sombras, contraluz | Marina "Eclipse" | Mantaraya espectral |

12 temporadas/año = 12 conjuntos cosméticos. Reciclables como contenido nostálgico en aniversarios.

### 8.8 Activación y compra

**Cuándo se ofrece:**
- Popup al inicio de cada temporada (día 1-3) cuando el jugador abre el juego
- Botón siempre visible en el HUD del santuario durante la temporada
- Recordatorio en el día 25 ("últimos 5 días!") para los que aún no compraron
- Recordatorio en el día 28 ("últimas 48 horas") con animación de urgencia

**Compra con gemas (alternativa al pago directo):**
- Premium track ($4.99 equivalente) → **800 gemas**
- Esto permite a free players hyperactivos pagarlo con gemas acumuladas (~3-4 semanas de ahorro)
- Drena su stock de gemas → presión orgánica para comprar más gemas en próximo ciclo

### 8.9 Decisiones lockeadas

| Decisión | Valor | Razón |
|---|---|---|
| Duración | 30 días por temporada | Plan Maestro estándar industria |
| Tracks | 2: Free + Premium | Plan Maestro |
| Premium price | $4.99 (USD) | Sweet spot conversión casual |
| Tiers | 40 | Balance commitment vs achievability |
| XP por tier | 1,000 | ~38-45k XP en 4 semanas casuales |
| Sin ads premium | Sí, durante 30 días | Killer feature que convierte ad-haters |
| Battle Pass Pro | Posponer a fase 2 (3-6 meses post-launch) | Foco MVP, agregar cuando hay data |
| Compra con gemas | 800 gemas | Permite a free hyperactivos convertir, drena stock |

## 9. Monetización (estrategia integral)

### 9.1 Modelo híbrido de 4 capas

Coralia sigue el modelo del Plan Maestro: F2P híbrido con cuatro capas que capturan revenue de free players, casual spenders, mid-tier y whales sin romper la experiencia. Cada capa fue desarrollada en su propia sección; este capítulo resume y articula cómo se integran.

| Capa | Sección de detalle | Target user | Revenue esperado |
|---|---|---|---|
| 1. Anuncios | 9.2 | Free players (95% de usuarios) | 25-35% del total |
| 2. IAP packs | 6.5 | Casual + mid-tier spenders | 25-35% del total |
| 3. Battle Pass | 8 | Mid-tier comprometidos (12-18% conversion) | 25-35% del total |
| 4. Suscripción | 9.3 (fase 2) | Whales y leales | 5-15% del total |

### 9.2 Anuncios (Capa 1)

Los anuncios en 2026 funcionan **solo si son por elección del jugador o no intrusivos**. Anuncios forzados durante gameplay = uninstall instantáneo.

**Tipos de ads y reglas:**

| Tipo | Cuándo aparece | Cap diario | Recompensa al jugador |
|---|---|---|---|
| **Rewarded — vida extra** | Tras quedarse en 0 vidas, popup ofrece ad | 3/día | +1 vida |
| **Rewarded — continuar nivel** | Al fallar un nivel | 5/día | +5 disparos para terminar |
| **Rewarded — duplicar recompensa** | Al completar un nivel, antes de mostrar drop | 10/día | x2 sobre coins, gemas, power-ups del drop |
| **Rewarded — cofre extra** | Daily reward popup | 1/día | Abre cofre adicional con drop random |
| **Rewarded — power-up gratis** | Pre-nivel, en pantalla de selección de power-ups | 3/día | 1 power-up gratis equipable solo en ese nivel |
| **Interstitial** | Entre niveles, **máximo 1 cada 3 niveles ganados** | Sin cap | (Sin recompensa, intrusión leve) |
| **Banner** | **No usar.** Plan Maestro lo descarta. | — | — |

**Reglas globales:**
- Nunca durante gameplay activo (ni siquiera cuando se pierde un combo)
- Nunca al abrir el juego (la primera pantalla siempre es santuario, no ad)
- Premium Battle Pass elimina **todos los ads** durante 30 días — ver sección 8
- Si el jugador ignora 5 rewarded ads ofrecidos consecutivos, no se muestran nuevos durante 24h (anti-fatigue)

**SDKs de ads:**

Stack recomendado por Plan Maestro:

| SDK | Rol | Por qué |
|---|---|---|
| **Google AdMob** | Network base | Mayor inventario, mejor pago en LATAM |
| **AppLovin MAX** | Mediación | Mete a varias networks (AdMob, Unity, IronSource, Meta) en bidding competitivo. Maximiza eCPM 30-50% vs solo AdMob |
| **GameAnalytics o Adjust** | Analytics de ads | Mide eCPM, fill rate, ARPDAU por ad placement. Crítico para optimizar |

**eCPM esperado para casual puzzle en 2026:** $8-15 USD en LATAM, $20-40 en US/EU. Diferencias importantes — el grueso del revenue vendrá de jugadores en mercados desarrollados aunque la masa esté en LATAM.

### 9.3 Suscripción premium (Capa 4 — fase 2)

**No lanzar en MVP.** Plan Maestro recomienda implementar tras **3-6 meses** cuando hay base de usuarios leales.

**Cuando se lance, propuesta:**

| Tier | Precio | Contenido |
|---|---|---|
| **Coralia Plus** | $4.99/mes o $39.99/año (33% descuento anual) | Sin ads + 50 gemas/día + vidas infinitas + skin exclusiva mensual + early access a niveles nuevos |

**Por qué post-launch y no en MVP:**
- Suscripciones requieren retención probada (jugadores leales)
- La base inicial debe descubrir el juego antes de que se les ofrezca compromiso recurrente
- Apple/Google tienen reviews más estrictos para suscripciones (validar bien post-MVP)

### 9.4 Pricing strategy resumen

Visión consolidada de cuánto puede gastar un jugador a lo largo de su vida en Coralia (LTV):

| Perfil | Gasto típico/mes | Notas |
|---|---|---|
| Free player | $0 | 95% de usuarios. Genera revenue solo via ads. |
| Casual spender | $2.99-$9.99 | Compra Starter Pack + ocasional weekend deal. |
| Mid-tier | $10-$30 | Battle Pass mensual + 1-2 IAP packs medianos. |
| Whale | $50-$300+ | Battle Pass + Pro + packs grandes + holiday packs. |

**Lifetime value (LTV) target:** $1.50-$3.50 promedio por usuario instalado, según data de la industria casual puzzle 2026. Esto define cuánto se puede pagar por adquisición (CPI).

### 9.5 Anti-patrones que evitamos explícitamente

Errores comunes en F2P que matan la confianza del jugador y queman LTV — **NO** lo hacemos en Coralia:

1. **Pay-to-win:** ningún power-up resuelve niveles automáticamente
2. **Walls infranqueables sin pago:** todo nivel es ganable con ad gratis
3. **Anuncios durante gameplay:** nunca interrumpimos un nivel en curso
4. **Pop-ups de tienda al abrir el juego:** la primera pantalla siempre es santuario
5. **Falsa urgencia constante:** los timers son reales (Starter Pack 7 días, Battle Pass 30 días), no resets fake
6. **Ofertas predatorias dirigidas a niños:** la audiencia es 25-45, pero hay menores que llegan; no diseñamos targeting agresivo a perfiles juveniles
7. **Loot boxes con probabilidades opacas:** los cofres muestran tabla de drop rates explícita (compliance con regulaciones LATAM y EU)

### 9.6 Decisiones lockeadas

| Decisión | Valor | Razón |
|---|---|---|
| Modelo | F2P híbrido 4 capas (Ads + IAP + Battle Pass + Suscripción) | Plan Maestro |
| Banner ads | NO | Plan Maestro: poco revenue, daña estética |
| Ad SDKs MVP | AdMob + AppLovin MAX | Plan Maestro |
| Suscripción | Fase 2, post 3-6 meses | Plan Maestro |
| Cap ads/día | Aplicado por tipo (3-10 según) | Anti-fatigue |
| Drop rates de cofres | Públicos | Compliance regulatorio + confianza |

## 10. UI / Pantallas

### 10.1 Mapa de pantallas del MVP

Las 17 pantallas mínimas del MVP se agrupan en cuatro flujos lógicos. Se sigue el patrón cross-proyecto de Diego: **dos splashes** (company + loading) en el boot.

| Flujo | Pantallas |
|---|---|
| **Boot** | 1. Company Splash · 2. Loading Splash · 3. Onboarding |
| **Hub principal** | 4. Santuario (main menu) · 5. Daily Rewards · 6. Battle Pass · 7. Shop · 8. Settings · 9. Profile · 10. Leaderboard · 11. Events |
| **Ciclo de partida** | 12. Level Select · 13. Pre-level · 14. Gameplay · 15. Pause · 16. Game Over/Victory · 17. Post-level |
| **Total** | 17 pantallas |

### 10.2 Diagrama de navegación

```
[Company Splash] → [Loading Splash] → [Onboarding (solo primera vez)] → [Santuario] ←──┐
                                                                              │          │
                                                                              ├──→ [Daily Rewards]
                                                                              ├──→ [Battle Pass]
                                                                              ├──→ [Shop]
                                                                              ├──→ [Settings]
                                                                              ├──→ [Profile]
                                                                              ├──→ [Leaderboard]
                                                                              ├──→ [Events]
                                                                              └──→ [Level Select]
                                                                                        │
                                                                                        └→ [Pre-level]
                                                                                              │
                                                                                              └→ [Gameplay] ─→ [Pause]
                                                                                                      │           │
                                                                                                      ├→ [Game Over]
                                                                                                      └→ [Victory] → [Post-level] ─→
```

### 10.3 Detalles por pantalla

Para cada pantalla: **propósito**, **elementos clave**, **navegación in/out**, **estados especiales**.

#### Pantalla 1 — Company Splash

- **Propósito:** mostrar la marca del estudio **myappcube** que produjo el juego (presentación corporativa)
- **Elementos:** logo de myappcube + opcional tagline corto del estudio
- **Navegación:** auto-transición tras 1.5-2 segundos → Loading Splash
- **Sin barra de carga:** es presentación, no carga
- **Estados:** ninguno (pantalla pasiva)

#### Pantalla 2 — Loading Splash

- **Propósito:** carga inicial de assets del juego, branding del producto
- **Elementos:**
  - Logo de Coralia + nombre del juego centrado
  - Marina silueta animada de fondo (idle gentle)
  - Barra de progreso de carga animada
  - Versión de la app en esquina inferior (ej. "v1.0.0")
  - Crédito breve opcional "By Diego" en footer
- **Navegación:** se cierra al terminar carga → Onboarding (primera vez) o Santuario
- **Duración esperada:** hasta que termine carga, target 1.5-3 segundos
- **Estados:** carga normal / error de carga (mensaje "Reintentar")

#### Pantalla 3 — Onboarding

- **Propósito:** tutorial interactivo de los primeros 3 niveles para jugadores nuevos
- **Elementos:** los primeros 3 niveles del juego con guía sobreimpresa (flechas, manitas tap, textos cortos), Marina aparece para introducir contexto narrativo
- **Navegación:** se ejecuta una sola vez, después salta directo al Santuario
- **Skip:** botón "saltar tutorial" en esquina (oculto por 2 segundos para evitar tap accidental)
- **Estados:** paso 1 (apuntar) → paso 2 (disparar) → paso 3 (rescate)

#### Pantalla 4 — Santuario (main menu)

- **Propósito:** pantalla principal del juego, hub central
- **Elementos:**
  - Vista panorámica del arrecife con criaturas rescatadas nadando idle
  - HUD top: monedas, gemas, nivel actual del jugador
  - HUD bottom: botón **JUGAR** (lleva a Level Select), botones de acceso rápido a Shop, Battle Pass, Eventos
  - Iconos esquinas: Settings, Profile, Daily Rewards (si hay disponible), Friends
  - Indicador de racha visible en top-right
- **Navegación:** desde Splash; salida hacia cualquier otra pantalla del hub
- **Estados:** evento activo (banner superior animado), Battle Pass nuevo (popup automático), Welcome Back (si volviste tras 7+ días)

#### Pantalla 5 — Daily Rewards

- **Propósito:** mostrar racha diaria y reclamar premio del día
- **Elementos:** carrusel de 7 días con premio de cada uno, día actual destacado, animación al reclamar
- **Navegación:** popup al primer login del día, o desde Santuario manual
- **Estados:** premio reclamable hoy / ya reclamado / racha rota (si perdiste un día)

#### Pantalla 6 — Battle Pass

- **Propósito:** mostrar progreso de la temporada y vender premium
- **Elementos:**
  - Track free + premium en paralelo (vertical scroll)
  - 40 tiers con icono de recompensa cada uno
  - Tier actual destacado, barra de XP hacia siguiente tier
  - Botón "comprar premium $4.99" (si no es premium)
  - Tema de la temporada como background
  - Días restantes de la temporada
- **Estados:** free user / premium user / temporada terminando (urgencia visual <72h)

#### Pantalla 7 — Shop

- **Propósito:** venta de gemas, vidas, packs
- **Elementos:**
  - Tabs: Gemas, Vidas, Power-ups, Especiales (ofertas activas), Cosméticos
  - Cards con cada producto: imagen, contenido, precio, "best value" badge
  - Starter Pack destacado en top con timer (si aún disponible)
- **Estados:** Starter Pack activo / Weekend Deal activo / Holiday Pack activo / sin ofertas especiales

#### Pantalla 8 — Settings

- **Propósito:** configuración del juego
- **Estructura:** 4 secciones (convención cross-proyecto con app-impostor)

**Sección 1 — Preferencias del juego**
- Sonidos del juego (slider 0-100%)
- Efectos interfaz (slider 0-100%)
- Sonidos pop (slider 0-100%)
- Vibración (toggle on/off)

**Sección 2 — Cuenta y asistencia**
- Perfil (link a pantalla 9)
- Suscripción (en MVP: badge "Próximamente"; activo en fase 2 con Coralia Plus)
- Cómo jugar (replay del onboarding/tutorial)
- Idioma (selector entre 6 idiomas: español, inglés, italiano, francés, alemán, portugués)
- Tema (selector de modo visual: **Claro** / **Oscuro** / **Automático** — sigue tema del SO):
  - **Claro (default):** "Modo Arrecife" — paleta luminosa pastel, día submarino
  - **Oscuro:** "Modo Profundidades" — paleta oscura azul-violeta profundo, noche submarina con bioluminiscencia más visible
  - **Automático:** sigue la preferencia del sistema operativo del dispositivo
- Ayuda (FAQ + email de soporte)
- Restaurar compras (botón, dispara RevenueCat restorePurchases)

**Sección 3 — Comunidad**
- Valorar (link a página del juego en App Store / Google Play)
- Compartir (share sheet nativo del SO)
- Redes (links a Instagram, TikTok, X/Twitter del juego)
- Sitio web (link a coralia.app o el dominio que se registre)

**Sección 4 — Legal**
- Políticas de privacidad (link a página web)
- Condiciones del servicio (link a página web)

**Notas:**
- En MVP, "Suscripción" muestra "Próximamente" mientras Coralia Plus no esté lanzada (fase 2)
- Las redes sociales y sitio web requieren ser creados antes del soft launch — owners pendientes
- Vincular cuenta (Facebook/Apple/Google) está dentro de "Perfil" (sección 2 → pantalla 9), no en Settings directamente
- **Estados:** ninguno significativo

#### Pantalla 9 — Profile

- **Propósito:** estadísticas del jugador y logros
- **Elementos:**
  - Avatar editable (skin de Marina)
  - Username editable
  - Estadísticas: niveles ganados, criaturas rescatadas, racha actual y máxima, días jugados, gemas/monedas gastadas
  - Logros con barra de progreso (40 logros)
  - Código de amigo
- **Estados:** logro recién desbloqueado (badge "nuevo")

#### Pantalla 10 — Leaderboard

- **Propósito:** ranking semanal global y entre amigos
- **Elementos:**
  - Tabs: Global, Amigos, Por nivel
  - Lista top 100 con avatar + nombre + score
  - Tu posición destacada
  - Tiempo restante hasta reset
  - Recompensas del top 10/100/1000
- **Estados:** dentro del top 10 / dentro del top 100 / fuera del top 1000

#### Pantalla 11 — Events

- **Propósito:** mostrar eventos activos
- **Elementos:**
  - Card del evento principal con countdown
  - Mecánica explicada brevemente
  - Progreso del jugador en el evento
  - Premios desbloqueables
  - Botón "jugar evento" → Level Select del evento
- **Estados:** evento activo / próximo / terminado

#### Pantalla 12 — Level Select

- **Propósito:** mapa de niveles tipo Candy Crush
- **Elementos:**
  - Camino serpenteante con nodos (cada nodo = un nivel)
  - Niveles ganados marcados con criatura rescatada
  - Nivel actual destacado con animación pulsante
  - Niveles bloqueados grises
  - Indicador de zona/capítulo
  - Scroll vertical
- **Estados:** primera vez en zona nueva (animación de revelación), niveles con evento activo (badge especial)

#### Pantalla 13 — Pre-level

- **Propósito:** info del nivel + selección de power-ups
- **Elementos:**
  - Número del nivel + objetivo (texto + ícono)
  - Disparos disponibles
  - Slot para 3 power-ups equipables
  - Botón "JUGAR"
  - Botón "ver rewarded ad para power-up gratis" (cap 3/día)
  - Costo de power-ups en gemas si no se tienen
- **Estados:** sin vidas (popup ofrece comprar/ad/esperar)

#### Pantalla 14 — Gameplay

- **Propósito:** la pantalla principal del juego
- **Elementos:**
  - Grid hexagonal con burbujas
  - Cañón con burbuja actual + preview de siguiente
  - HUD top: objetivo del nivel + progreso, disparos restantes, score actual, botón pause
  - HUD bottom: power-ups equipados con tap-to-activate
  - Línea de trayectoria al apuntar
  - Animaciones de combo, drops, rescate
- **Estados:** disparando, en cinemática de rescate, animación de victoria/derrota

#### Pantalla 15 — Pause

- **Propósito:** detener el nivel temporalmente
- **Elementos:**
  - Overlay semi-transparente sobre Gameplay
  - Botones: Continuar, Reiniciar nivel, Salir al Santuario
  - Toggle rápido de música y sonido
- **Estados:** ninguno significativo

#### Pantalla 16 — Game Over / Victory

- **Propósito:** resultado del nivel + ofertas de continuar
- **Elementos (Game Over):**
  - Título "Sin disparos" + animación triste de Marina
  - Opciones: ver ad para +5 disparos, pagar 15 gemas, aceptar derrota
  - Pierde 1 vida si acepta
- **Elementos (Victory):**
  - Título "¡Lo lograste!" + animación de rescate
  - Score, estrellas (si las hubiera), criatura rescatada
  - Botón "Continuar" → Post-level
- **Estados:** primera completación del nivel (bonus) / repetición / derrota

#### Pantalla 17 — Post-level

- **Propósito:** mostrar recompensas + opción de duplicar
- **Elementos:**
  - Animación de drop: monedas, gemas, power-ups
  - Battle Pass XP ganado, tier avanzado si aplica
  - Botón "duplicar recompensa viendo ad" (cap 10/día)
  - Botón "siguiente nivel"
  - Botón "salir al santuario"
- **Estados:** completaste el último nivel del capítulo (cinemática de restauración de zona del santuario)

### 10.4 Principios de UI/UX

| Principio | Implementación |
|---|---|
| **Mínimo de taps al gameplay** | Desde Santuario, máximo 3 taps para entrar a un nivel: Jugar → Level → Jugar |
| **Feedback inmediato** | Cada tap genera respuesta visual + sonora + háptica (si está habilitada) |
| **Prefer bottom sheets** | Pickers (selección de power-up, etc.) usan bottom sheet, no modal full-screen |
| **No saturar HUD** | Durante gameplay solo lo esencial visible. Lo demás solo en pausa o pre-level |
| **Settings persistentes** | Sonido, idioma, notificaciones se guardan localmente y nunca se pierden |
| **Portrait orientation** | Toda la UI diseñada para portrait. No soporte de landscape en MVP |

### 10.5 Decisiones lockeadas

| Decisión | Valor | Razón |
|---|---|---|
| Total pantallas MVP | 16 | Plan Maestro |
| Pantalla principal | Santuario | El santuario *es* el main menu (ver sección 5) |
| Orientación | Portrait only | Estándar mobile casual |
| Onboarding | Una sola vez, primeros 3 niveles | Plan Maestro |
| Tutorial skippable | Sí, tras 2s de delay | Anti-rage de jugadores experimentados |
| Idiomas MVP | 6 completos al lanzamiento: Español, Inglés, Italiano, Francés, Alemán, Portugués | Convención cross-proyecto + cobertura mercados clave + decisión Diego v0.4 |

## 11. Arte y dirección visual

### 11.1 Dirección general

**Estilo:** cozy, hand-drawn-looking, ilustración suave 2D. Influencias: Animal Crossing: New Horizons, Stardew Valley, Cocoon (PS4), Sky: Children of the Light, ilustraciones de Studio Ghibli para escenas submarinas.

**No queremos:** estilo Disney princess hiperestilizado, realismo, low-poly geométrico, pixel art, anime kawaii. Coralia tiene su propia identidad: **dulce y luminosa pero no infantil**, **mágica pero no fantasy genérica**, **cute pero no chibi extremo**.

### 11.2 Paleta de colores base

Paleta primaria de la marca y de los assets MVP. Debe sentirse coral submarino al amanecer — luz cálida filtrándose en agua.

| Token | Hex | Uso |
|---|---|---|
| `coral_pink` | `#F4A6A0` | Color del coral principal del santuario, acentos cálidos |
| `coral_deep` | `#D87B7B` | Sombras del coral, niveles del último capítulo |
| `seafoam` | `#A8E0D5` | Agua media, fondos UI suaves |
| `pearl_white` | `#FBF6E9` | Backgrounds claros, perlas, luz de la Ciudad de las Perlas |
| `aqua_deep` | `#4A8FB7` | Agua profunda, gradientes oscuros |
| `bubble_blue` | `#7EC9E2` | Burbuja base color azul |
| `bubble_yellow` | `#F9D85E` | Burbuja amarilla |
| `bubble_green` | `#9ED48A` | Burbuja verde |
| `bubble_purple` | `#B59FD9` | Burbuja morada |
| `bubble_red` | `#EE7A7A` | Burbuja roja |
| `gold_treasure` | `#E5BE5C` | Acentos de tesoro, hitos de Battle Pass |
| `dark_overlay` | `#0F2238` | Pausa overlay, modo derrota |

Cada zona del arrecife (capítulo) tiene su propia subpaleta, derivada de estos colores base. Por ejemplo, capítulo 4 (Cueva de Cristales) usa más `bubble_purple` y `seafoam` con brillos `gold_treasure`.

### 11.3 Tipografías

| Uso | Fuente recomendada | Notas |
|---|---|---|
| Títulos / branding | **Quicksand Bold** o similar rounded | Suave, redondeada, mass-friendly, free en Google Fonts |
| Cuerpo / UI | **Nunito Regular** | Pareja natural de Quicksand, excelente legibilidad mobile |
| Números (HUD score, contadores) | **Nunito Black** o tabular monospace | Tabular para que los números no salten al cambiar |

Decisión final tras concept art. Ambas fuentes son free y soportan español + inglés + acentos.

### 11.4 Lista de assets visuales del MVP

Total estimado por categoría (Plan Maestro Parte 5 ajustado a Coralia):

| Categoría | Cantidad | Notas |
|---|---|---|
| Burbujas (6 colores + arcoíris + comodines) | ~10 sprites | Animaciones idle suave + pop |
| Marina (personaje principal) | 1 + 6 animaciones | Idle, disparo, victoria, derrota, rescate, saludo |
| Skins de Marina (Battle Pass) | 6 (uno por temporada inicial) | Variaciones de colores y accesorios |
| Criaturas hero | 12 con animaciones idle | Las 12 nombradas en sección 13 |
| Criaturas comunes (random en niveles) | 10-15 sprites simples | Animación idle básica |
| Backgrounds del santuario | 6 zonas (apagadas + restauradas = 12 estados) | Cambio visual al completar capítulo |
| Backgrounds de gameplay | 6 (uno por capítulo) | Sutiles, no compiten con gameplay |
| UI completa | ~80 elementos | Botones, popups, iconos, tabs, sliders, etc. |
| Iconos de power-ups | 6 | Coherentes entre sí |
| Iconos de obstáculos | 6 (hielo, jaula, pegajosa, generador, bomba, cadena) | Reconocibles a glance |
| Efectos de partículas | ~15-20 | Pop, drop, victoria, rescate, magia |
| Cinemáticas / animaciones de transición | 6 (una por restauración de zona) | Lo más impactante visualmente |
| Logo + branding | 1 logo + variaciones para iconos de tienda | App Store + Play Store + redes |
| Cosméticos del santuario (Battle Pass) | 12 decoraciones únicas | Una por temporada |
| Sombra Profunda (antagonista) | 6 estados visuales (capítulo 1 a 6) | Ver sección 13.4 |

**Total aproximado:** ~250-300 sprites + animaciones individuales.

### 11.5 Estrategia de producción de assets

Plan Maestro propone 3 opciones (bajo / medio / alto). Para Coralia, **estrategia recomendada:**

**Modelo híbrido (Plan Maestro recomendación):** 
- **Concept art y briefs** generados con Midjourney / Stable Diffusion / Flux para definir look
- **Assets de UI base** desde Kenney.nl + asset stores (botones, frames, iconos genéricos) — gratis o low-cost
- **Personajes y criaturas hero** producidos por **freelancer** especializado en cute/cozy art (Fiverr o Upwork, $50-150 por personaje completo con animaciones)
- **Backgrounds** mezcla AI gen + retoque manual del freelancer
- **Animaciones complejas** (cinemáticas) por freelancer animador

**Presupuesto estimado:** $2,000-$4,000 total (rango medio del Plan Maestro).

### 11.6 Pipeline de assets

1. Brief escrito + referencia visual (3-5 imágenes inspiración)
2. Generación de concept con AI (3-5 variantes)
3. Selección de dirección y refinamiento
4. Brief detallado al freelancer con concept aprobado
5. Iteración 2-3 rondas con freelancer
6. Optimización: PNG con transparencia, máx 1024x1024, sprite atlases para reducir draw calls
7. Importación a Unity (modo Sprite, Multiple si hay sub-sprites) + animaciones en `Animator`
8. Test in-game antes de aprobar final

### 11.7 Decisiones lockeadas

| Decisión | Valor | Razón |
|---|---|---|
| Estilo | Cozy hand-drawn 2D | Diferenciación + audiencia objetivo |
| Paleta | Coral pink + seafoam + pearl white + aqua deep | Coherente con tema submarino |
| Tipografías | Quicksand + Nunito (free Google Fonts) | Soft, legible, multi-idioma |
| Estrategia producción | Híbrido AI + freelancer + asset store | Plan Maestro middle path |
| Presupuesto | $2,000-$4,000 estimado | Plan Maestro |
| Total assets MVP | ~250-300 sprites | Realista para indie |

## 12. Audio

### 12.1 Dirección de audio

**Mood general:** cozy, contemplativo, mágico. La música no debe agitar; debe acompañar. SFX cortos, satisfactorios, juguetones (cada pop de burbuja debe sentirse bien).

**Referencias musicales:** Stardew Valley (Spring/Pelican Town themes), Cocoon, Sky: Children of the Light, Animal Crossing (música submarina al bucear), Studio Ghibli scores acuáticos (Ponyo, El viaje de Chihiro escenas de río).

### 12.2 Música del MVP

**Total tracks: 5** (Plan Maestro Parte 5).

| Track | Duración | Mood | Cuándo se reproduce |
|---|---|---|---|
| `theme_santuario` | 90s loop | Cálido, esperanzador, suave | Pantalla principal del santuario |
| `theme_gameplay_calmo` | 90s loop | Concentración, relajación | Niveles 1-30 |
| `theme_gameplay_intenso` | 90s loop | Pulso ligero, más energía | Niveles 31-60 |
| `theme_victoria` | 8s sting | Triunfo suave | Tras ganar nivel |
| `theme_evento_especial` | 90s loop | Festivo, único por evento | Eventos temporales (variantes según evento) |

**Especificaciones técnicas:** 44.1 kHz, 16-bit. MP3, WAV u OGG Vorbis funcionan bien en Unity — OGG suele dar mejor compresión sin pérdida perceptible. Loops perfectos verificados (sin click al final).

**Fuentes recomendadas:**
- **Epidemic Sound** (subscription $15/mes): catálogo profesional con licencias claras
- **Freelancer compositor** ($300-800 total para 5 tracks originales): identidad única, recomendado si presupuesto lo permite
- **YouTube Audio Library**: gratis pero genérico, evitar si se puede

### 12.2bis Localización (i18n) — gestión 100% local sin costo

Los 6 idiomas (es, en, it, fr, de, pt) **se gestionan completamente sin costo monetario**. Los idiomas elegidos son todos mainstream con excelente cobertura en AI translation, lo que hace viable un pipeline puramente AI-assisted con polish opcional gratuito.

**Pipeline:**

| Paso | Quién | Costo | Calidad esperada |
|---|---|---|---|
| 1. Escribir todos los strings en español (canonical source) | Diego + Claude colaborando | $0 | Source ground truth |
| 2. Generar drafts a en/it/fr/de/pt adaptados al tono y personalidad | Claude (AI) | $0 | 85-95% production-ready |
| 3. Polish opcional por hablantes nativos de la red personal de Diego | Red personal (favores, no pagado) | $0 | Sube a 95-99% si está disponible |
| 4. Test in-app antes de lanzamiento con beta testers que hablen los idiomas | Beta testers de la red | $0 | Catch bugs y errores contextuales |

**Costo total: $0.** Calidad competitiva para juego casual con strings cortos. Si en el futuro Coralia genera revenue suficiente, se puede considerar polish profesional como mejora post-launch — pero **no es necesario para shippear el MVP**.

**Estrategia anti-riesgo:** si un idioma específico tiene calidad menor (típicamente francés y alemán requieren más cuidado que italiano y portugués), priorizar conseguir un hablante nativo de la red personal específicamente para ese idioma antes del lanzamiento.

**Implementación técnica real (Unity):**

- `LocaleManager` (`Scripts/Core/LocaleManager.cs`) — diccionario estático cargado desde `Resources/translations.csv`
- Strings van a `translations.csv` con header `keys,es,en,it,fr,de,pt`
- Cambio de idioma en runtime con `SaveManager.Language = "it"`, dispara `LocaleManager.OnLanguageChanged` para refrescar UI dinámica
- Detección automática del idioma del SO al primer abrir el juego (`SaveManager.Language` cae a `DetectLanguage()` si no hay guardado)
- Format keys consistentes: `ui.button.play`, `creature.coqui.first_dialogue`, etc. — placeholders (`{value}`) se reemplazan a mano con `.Replace(...)`, `LocaleManager.Get()` no lo hace automático

**Plantilla de CSV:**

```csv
keys,es,en,it,fr,de,pt
ui.button.play,Jugar,Play,Gioca,Jouer,Spielen,Jogar
creature.coqui.first_dialogue,"H-hola... ¿de verdad viniste por mí?","H-hello... did you really come for me?","C-ciao... sei venuto davvero per me?","B-bonjour... tu es vraiment venu pour moi ?","H-hallo... bist du wirklich für mich gekommen?","O-olá... vieste mesmo por mim?"
```

### 12.3 SFX (efectos de sonido) del MVP

**Total: ~30 efectos** (Plan Maestro).

**Categorías:**

| Categoría | SFX | Notas |
|---|---|---|
| Burbujas | `bubble_pop`, `bubble_drop`, `bubble_combo_x3`, `bubble_combo_x5`, `bubble_combo_x10`, `bubble_rainbow_pop` | Variaciones leves de pitch para evitar fatiga |
| Cañón | `canon_aim`, `canon_shoot`, `canon_color_swap` | |
| Power-ups | `powerup_bomba`, `powerup_rayo`, `powerup_color`, `powerup_laser`, `powerup_pez`, `powerup_aire` | Cada uno con su firma sonora |
| Obstáculos | `ice_crack`, `cage_break`, `bomb_tick`, `bomb_explode` | |
| UI | `button_tap`, `popup_open`, `popup_close`, `tab_switch`, `purchase_success` | |
| Recompensas | `coin_collect`, `gem_collect`, `creature_rescued`, `level_complete`, `chapter_complete`, `streak_advance` | |
| Notificaciones | `daily_reward_popup`, `battle_pass_tier_up`, `achievement_unlock` | |
| Marina | `marina_humming` (idle vocal sutil), `marina_giggle` (al abrir el juego) | Vocales cortas no específicas de idioma |

**Fuentes:**
- **Freesound.org** (gratis con créditos)
- **Humble Bundle** packs de SFX ($1-15 ocasional)
- **ZapSplat** (free con cuenta)

### 12.4 Mix y mastering

**3 sliders separados de audio + 1 toggle de vibración** (convención cross-proyecto con app-impostor):

| Categoría | Default dB | Slider de usuario | Contenido |
|---|---|---|---|
| **Sonidos del juego** | -10 dB | 0-100% | Música ambiental + audio narrativo (rescate, restauración de zona) |
| **Efectos interfaz** | -8 dB | 0-100% | UI: tap, popup, tab switch, purchase, achievement |
| **Sonidos pop** | -3 dB | 0-100% | Burbujas: pop, drop, combo, rainbow pop, cañón |
| **Vibración** | — | toggle on/off | Háptica al disparar, al rescatar, al ganar/perder nivel |

| Parámetro | Valor |
|---|---|
| Volumen máster | -6 dB |
| Voz/diálogo | n/a en MVP (sin voice acting) |
| Ducking | Sonidos del juego bajan 30% durante eventos importantes (rescate, victoria) |
| LUFS target | -16 a -18 LUFS (estándar mobile) |
| Persistencia | Cada slider y toggle se guarda en local + cloud save |

### 12.5 Audio adaptativo (opcional fase 2)

Sistema donde la música cambia sutilmente según el estado del nivel (intensifica al quedar pocos disparos, suaviza al rescatar). **No para MVP.** Considerar tras 6 meses si la data de retention sugiere que mejoraría engagement.

### 12.6 Decisiones lockeadas

| Decisión | Valor | Razón |
|---|---|---|
| Tracks de música | 5 | Plan Maestro |
| Total SFX | ~30 | Plan Maestro |
| Formato | OGG Vorbis 44.1kHz 16-bit | Compresión + calidad equilibradas |
| Audio adaptativo | Diferido a fase 2 | Scope MVP |
| Voice acting | No en MVP | Costo + complejidad localización |
| Presupuesto | $300-$800 música + $0-$50 SFX | Plan Maestro |

## 13. Narrativa y personajes

### 13.1 Filosofía narrativa

Coralia es un juego cozy. Eso no significa "sin historia" — significa que la historia se sostiene en **empatía y restauración**, no en conflicto y violencia. No hay un villano a vencer, no hay batallas, no hay derrotas trágicas. Hay un mundo herido que Marina cuida, criaturas con personalidad que se unen al santuario, y una resolución emocional al final del MVP que cierra el arco.

La narrativa se entrega en **dosis pequeñas** — 1-2 líneas por criatura cuando se rescata, frases ambientales en pantallas de carga, y una revelación emocional al completar la zona 6. Nunca un wall of text. El jugador casual no está acá para leer, está acá para sentir.

### 13.2 Marina — la protagonista

**Nombre completo:** Marina (sin apellido — invita identificación con la jugadora).

**Edad aparente:** ~16-18 años. Joven adulta, no niña pero tampoco mujer madura. Apela tanto a la audiencia 25-45 (figura empática, hermana menor) como a su propio rango etario.

**Identidad:** sirena del arrecife. No princesa, no realeza, no estrella. Es **una habitante** del arrecife — su rol es de cuidadora y exploradora, no de heroína épica.

**Personalidad:**
- Cálida, optimista, curiosa
- Empática: trata a cada criatura como un individuo, no como objeto a coleccionar
- Determinada pero suave: no se rinde, pero tampoco fuerza nada
- Toque de juguetona: tiene su lado pícaro cuando interactúa con criaturas

**Motivación:** restaurar el arrecife que es su hogar. No por gloria ni por venganza. Por amor al lugar y a quienes lo habitan.

**Voz / tono al hablar:** frases cortas, cálidas, a veces tímidas. Habla con cariño a las criaturas. Nunca usa lenguaje agresivo ni triunfalista.

**Visual brief para arte:**
- Estilo: cozy / Studio Ghibli / Stardew Valley — NO Disney princess
- Cabello: ondulado, color **coral pálido o turquesa suave** (decisión final con concept art)
- Ojos: grandes, expresivos, color marino verde
- Atuendo: top de algas tejidas o conchas pequeñas, sin escote pronunciado (modesto, audiencia femenina)
- Cola: degradado de turquesa a coral con detalles **bioluminiscentes** que cambian sutilmente con la temporada del Battle Pass
- Accesorios: una flor de coral en el cabello, brazaletes de perlas
- Postura: relajada, no heroica
- Expresión default: sonrisa suave

**Animaciones requeridas (mínimo MVP):**
- Idle (flotando suavemente, cabello moviéndose)
- Disparo (gestura del cañón)
- Victoria (giro alegre con burbujitas)
- Derrota (suspiro, no llanto)
- Rescate (abraza a la criatura)
- Saludo (al abrir el juego)

### 13.3 Las 12 criaturas hero del MVP

Doce criaturas distribuidas a lo largo de los 6 capítulos. Cada una tiene un nombre, una especie, una personalidad clara y una frase corta característica. Estas son las **estrellas del santuario**, no las únicas criaturas del juego (cada nivel puede tener "criaturas comunes" sin nombre que también se rescatan, pero las 12 hero son las que el jugador realmente recuerda).

#### Capítulo 1 — La Cala Apagada

**1. Coquí — Caballito de mar (verde menta)**
- Personalidad: tímido y curioso, primer rescate del juego (nivel 3)
- Línea característica: *"H-hola... ¿de verdad viniste por mí?"*
- Recompensa pasiva al santuario: 5 monedas/hora (común)

**2. Burbujín — Pez payaso (naranja)**
- Personalidad: parlanchín, exagerado, rompe el hielo emocional del jugador
- Línea: *"¡Ya estaba pensando hacerme amigo de un alga! Mejor que viniste."*
- Recompensa pasiva: 5 monedas/hora (común)

#### Capítulo 2 — Jardín de Anémonas

**3. Lúa — Anémona (rosada)**
- Personalidad: dulce, soñadora, habla en consejos zen suaves
- Línea: *"Cuando todo se mueve rápido, las raíces son lo que sostiene."*
- Recompensa pasiva: 12 monedas/hora (rara)

**4. Caracol — Caracol marino (violeta)**
- Personalidad: lento, filósofo, bromea sobre su propio ritmo
- Línea: *"Llegué tarde a mi rescate. Como a casi todo, supongo."*
- Recompensa pasiva: 5 monedas/hora (común)

#### Capítulo 3 — Bosque de Algas

**5. Espina — Erizo de mar (verde oscuro)**
- Personalidad: sarcástico, orgulloso, el contraste mordaz que evita que el tono sea empalagoso
- Línea: *"Bah. Tampoco es que necesitara ayuda. Pero gracias, supongo."*
- Recompensa pasiva: 12 monedas/hora (rara)

**6. Aletita — Bebé tortuga (amarillo-marrón)**
- Personalidad: entusiasta, torpe, choca con todo
- Línea: *"¡Wiiii! ¡Espera, ¿dónde estoy? ¡Ya, ya, lo sabía!"*
- Recompensa pasiva: 12 monedas/hora (rara)

#### Capítulo 4 — Cueva de Cristales

**7. Glissa — Medusa lunar (cyan plateada)**
- Personalidad: etérea, misteriosa, habla en metáforas
- Línea: *"La luz no se rompe, solo se desplaza. Como nosotras."*
- Recompensa pasiva: 12 monedas/hora (rara)

**8. Chispín — Gamba bioluminiscente (rojo cereza)**
- Personalidad: hiperactiva, nerviosa, habla rapidísimo
- Línea: *"¡HOLAQUÉTALMUCHOGUSTOPUEDOQUEDARMECONTIGO?"*
- Recompensa pasiva: 12 monedas/hora (rara)

#### Capítulo 5 — Profundidades de Coral

**9. Lumi — Pulpa (violeta profundo)** — *mentora del santuario*
- Personalidad: maternal, sabia, observadora. Es la criatura que "guía" sutilmente a Marina entre capítulos
- Línea: *"Te observé desde la primera burbuja, niña. Lo estás haciendo bien."*
- Recompensa pasiva: 25 monedas/hora (épica)

**10. Marino — Narval bebé (azul claro y blanco)**
- Personalidad: soñador, le fascina la superficie y el cielo (que jamás ha visto)
- Línea: *"¿Las estrellas... brillan así de fuerte allá arriba?"*
- Recompensa pasiva: 25 monedas/hora (épica)

#### Capítulo 6 — Ciudad de las Perlas

**11. Iris — Mantarraya iridiscente (multicolor pastel)**
- Personalidad: majestuosa, antigua, conoce los secretos del arrecife
- Línea: *"Antes de las sombras, este lugar cantaba. Y volverá a cantar."*
- Recompensa pasiva: 25 monedas/hora (épica)

**12. Perla — Ballena bebé (blanco perlado con rosa)** — *criatura culminante del MVP*
- Personalidad: tierna, gigante pero suave, la criatura más impactante visualmente
- Línea: *"Mi madre me cantaba sobre ti. Decía que algún día llegarías."*
- Recompensa pasiva: 25 monedas/hora (épica)

**Distribución de tiers:** 4 comunes (Coquí, Burbujín, Caracol) — me cuento 3, ajustar — 5 raras, 4 épicas. *Ajuste: redistribuir en revisión.* Total recompensa pasiva del santuario completo: ~190 monedas/hora.

### 13.4 El antagonista — La Sombra Profunda

**Decisión narrativa central:** el antagonista **no es un villano**. Es el toque emocional clave del MVP.

**Quién es:** una criatura ancestral del arrecife que fue herida hace mucho tiempo y, en su dolor, dispersó las Burbujas de Vida. No actúa por maldad — actúa por soledad y dolor antiguo. Marina no lo combate; lo escucha, lo entiende, y al final lo libera.

**Cómo se revela en el arco:**

| Capítulo | Apariciones de la Sombra |
|---|---|
| 1 | Mencionada solo como ambient ("una corriente oscura") |
| 2 | Susurros distantes en pantallas de carga |
| 3 | Sombra brevemente vista en niveles 27-30 |
| 4 | La Sombra "habla" por primera vez al jugador (1-2 líneas crípticas) en cinemática corta entre niveles 35 y 36 |
| 5 | Marina empieza a sospechar que la Sombra está sufriendo, no atacando |
| 6 | Revelación: la Sombra es la **antigua guardiana** del arrecife, herida por algo del pasado. Marina la libera con compasión en el nivel 60 |

**Diálogo final de la Sombra (cinemática del nivel 60):**
*"Llevaba tantas mareas en silencio... gracias, pequeña sirena. La luz no se había ido. Solo me había olvidado de cómo verla."*

**Visual brief de la Sombra:**
- Forma indefinida en capítulos 1-4 (humo oscuro azulado)
- Capítulo 5: empieza a tomar forma vagamente reconocible (silueta de ballena gigante o serpiente marina)
- Capítulo 6 final: revelada como una **ballena ancestral colosal** con grietas luminosas en la piel (representando heridas que sanan)
- Paleta: oscuros profundos → al ser sanada, dorado-blanco con luz interna

### 13.5 Estructura del arco narrativo (a lo largo de los 6 capítulos)

| Capítulo | Tema emocional | Descubrimiento de Marina |
|---|---|---|
| 1. La Cala Apagada | "Mi hogar se está apagando" | Decide restaurarlo |
| 2. Jardín de Anémonas | "No estoy sola" | Las criaturas la acompañan, primer sentido de comunidad |
| 3. Bosque de Algas | "Hay algo más, una presencia" | Primera curiosidad sobre la Sombra |
| 4. Cueva de Cristales | "La sombra habla" | Primera duda: ¿es realmente un enemigo? |
| 5. Profundidades de Coral | "Está sufriendo" | Empatía hacia el antagonista, decisión de ayudar |
| 6. Ciudad de las Perlas | "Sanar es la victoria" | Liberación de la Sombra, restauración total del arrecife |

**Mensaje emocional final del MVP:** *no se vence el dolor con fuerza, se libera con compasión*. Es un mensaje cozy adulto que conecta con la audiencia 25-45 sin sonar predicador.

### 13.6 Líneas dialogadas — guidelines de escritura

- **Máximo 2 líneas** por aparición de criatura
- **Cada criatura tiene voz distinta** (no todas hablan igual). Coquí tartamudea, Espina es seco, Lúa es zen, Chispín es hiperactiva, Lumi es maternal. La voz se mantiene consistente en todas sus apariciones.
- **Sin lore dumps:** nunca explicar la historia del arrecife en bloque. Información se filtra en frases ambientales y diálogos.
- **Localización:** todas las líneas se escriben primero en español (mercado primario), después se traducen a los 6 idiomas (español, inglés, italiano, francés, alemán, portugués). **El tono y la personalidad deben mantenerse en cada idioma** — no es traducción mecánica, es adaptación cultural.
- **Inclusivo:** evitar referencias culturales muy locales que no funcionen globalmente (no jergas mexicanas, argentinas, españolas específicas — buscar español neutro mass-market).

### 13.7 Decisiones lockeadas

| Decisión | Valor | Razón |
|---|---|---|
| Tono narrativo | Cozy / empático / sin violencia | Coherente con audiencia y género del juego |
| Antagonista | Sombra Profunda — herida, no malvada | Diferenciador emocional vs Bubble Witch (villana clásica) |
| Resolución del MVP | Liberación, no derrota | Mensaje emocional que conecta con audiencia 25-45 |
| Criaturas hero | 12 distribuidas en 6 capítulos | Una guía clara del arco visual y emocional |
| Voz de Marina | Cálida, no heroica | Identificación con la jugadora |
| Lore | Drip-feed, sin walls of text | Audiencia casual no lee mucho |
| Localización | 6 idiomas MVP completos (es, en, it, fr, de, pt) | Convención cross-proyecto + decisión Diego v0.4 |
| Tema | Claro (Modo Arrecife) + Oscuro (Modo Profundidades) + Automático | Estándar mobile + thematic fit |

## 14. Stack técnico y arquitectura

### 14.1 Stack base

**Estudio:** myappcube. **Producto:** Coralia.

| Capa | Tecnología | Versión / detalle | Por qué |
|---|---|---|---|
| Engine | **Godot** | 4.3+ (LTS al inicio del desarrollo) | Plan Maestro. Open source, gratis, exportable a Android + iOS, lenguaje propio (GDScript) productivo |
| Lenguaje | **GDScript** | Tipado estático cuando sea posible | Plan Maestro. Performance suficiente para casual puzzle. Iteración rápida. |
| Backend / cloud | **Firebase** | Free tier al inicio | Plan Maestro. Auth + Firestore + Cloud Messaging + Remote Config + Analytics + Crashlytics |
| IAP | **RevenueCat** | SDK Godot (community plugin) | Plan Maestro. Maneja IAP cross-platform con un solo backend |
| Ads | **AdMob** + **AppLovin MAX** | Plugins nativos Godot Android/iOS | Plan Maestro. Mediación competitiva |
| Versionado | **Git** + GitHub privado | — | Estándar |
| CI/CD (post-MVP) | GitHub Actions o Jenkins | — | Automatizar builds Android/iOS |

### 14.2 Estructura de carpetas

```
coralia/
├── project.godot
├── icon.svg
├── .gitignore
├── README.md
│
├── scenes/                    # Escenas de Godot (.tscn)
│   ├── main/
│   │   ├── company_splash.tscn
│   │   ├── loading_splash.tscn
│   │   └── onboarding.tscn
│   ├── santuario/
│   │   ├── santuario.tscn
│   │   └── creature_idle.tscn
│   ├── gameplay/
│   │   ├── gameplay.tscn
│   │   ├── grid.tscn
│   │   ├── canon.tscn
│   │   └── bubble.tscn
│   ├── ui/
│   │   ├── shop.tscn
│   │   ├── battle_pass.tscn
│   │   ├── settings.tscn
│   │   ├── profile.tscn
│   │   ├── leaderboard.tscn
│   │   ├── events.tscn
│   │   ├── level_select.tscn
│   │   ├── pre_level.tscn
│   │   ├── pause.tscn
│   │   ├── game_over.tscn
│   │   ├── victory.tscn
│   │   ├── post_level.tscn
│   │   └── daily_rewards.tscn
│   └── tools/                 # Editor de niveles, debug tools
│       └── level_editor.tscn
│
├── scripts/                   # Scripts GDScript (.gd)
│   ├── autoloads/             # Singletons globales
│   │   ├── game_manager.gd
│   │   ├── audio_manager.gd
│   │   ├── save_manager.gd
│   │   ├── economy_manager.gd
│   │   ├── battle_pass_manager.gd
│   │   ├── ads_manager.gd
│   │   ├── iap_manager.gd
│   │   ├── analytics_manager.gd
│   │   └── firebase_manager.gd
│   ├── gameplay/
│   │   ├── grid_logic.gd
│   │   ├── bubble.gd
│   │   ├── match_detector.gd
│   │   ├── physics_helper.gd
│   │   └── level_loader.gd
│   ├── ui/
│   │   └── (un .gd por cada escena UI)
│   ├── data/
│   │   ├── level_data.gd      # Resource class para niveles
│   │   ├── creature_data.gd
│   │   └── powerup_data.gd
│   └── utils/
│       └── (helpers varios)
│
├── resources/                 # Resources de Godot (.tres)
│   ├── creatures/
│   │   ├── coqui.tres
│   │   ├── burbujin.tres
│   │   └── ... (12 criaturas)
│   ├── powerups/
│   │   ├── bomba_coral.tres
│   │   └── ... (6 power-ups)
│   └── battle_passes/
│       └── season_01_despertar.tres
│
├── data/                      # Datos de niveles en JSON
│   └── levels/
│       ├── 001.json
│       ├── 002.json
│       └── ... (60 archivos)
│
├── assets/                    # Assets crudos
│   ├── sprites/
│   │   ├── characters/
│   │   ├── bubbles/
│   │   ├── creatures/
│   │   ├── ui/
│   │   ├── backgrounds/
│   │   └── particles/
│   ├── audio/
│   │   ├── music/
│   │   └── sfx/
│   ├── fonts/
│   └── shaders/
│
├── localization/              # Archivos i18n (6 idiomas convención cross-proyecto)
│   ├── es.csv     # Español
│   ├── en.csv     # English
│   ├── it.csv     # Italiano
│   ├── fr.csv     # Français
│   ├── de.csv     # Deutsch
│   └── pt.csv     # Português
│
├── platform/                  # Configuración específica de plataforma
│   ├── android/
│   │   └── (export presets, signing)
│   └── ios/
│       └── (export presets, certificates)
│
├── docs/                      # Documentación interna
│   ├── Plan_Maestro_Bubble_Shooter.docx
│   ├── 01_Concepto_Inicial.md
│   └── 02_GDD_Coralia.md
│
└── tests/                     # Tests unitarios
    └── (gdUnit4 tests)
```

### 14.3 Autoloads (singletons globales)

Godot maneja servicios globales vía **autoloads** (Project Settings → Autoload). Coralia tiene los siguientes:

| Autoload | Responsabilidad |
|---|---|
| `GameManager` | Estado global del juego, signals, transiciones de pantalla |
| `AudioManager` | Reproducción de música y SFX, volumen, fade in/out |
| `SaveManager` | Guardar/cargar progreso local (JSON) y sincronizar con cloud |
| `EconomyManager` | Monedas, gemas, vidas, transacciones internas |
| `BattlePassManager` | XP, tiers, recompensas, temporada activa |
| `AdsManager` | Wrapper sobre AdMob/AppLovin, tracking de fatigue |
| `IAPManager` | Wrapper sobre RevenueCat, productos disponibles, restore purchases |
| `AnalyticsManager` | Eventos de tracking → Firebase Analytics + GameAnalytics |
| `FirebaseManager` | Auth, Firestore, Cloud Messaging, Remote Config |
| `LevelManager` | Carga niveles desde JSON, valida formato |
| `LocaleManager` | Localización (espñol/inglés), cambio en runtime |

**Regla:** ningún autoload depende de otro autoload directamente (acoplamiento bajo). Comunicación entre autoloads vía **signals** del bus central de `GameManager`.

### 14.4 Formato de archivo de nivel (JSON)

Los niveles son **archivos JSON estructurados** para soportar generación AI-assisted (sección 2.7).

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
      [1, "red"], [2, "blue"], [3, "yellow"],
      [12, "red"], [13, "red"]
    ]
  },
  "obstacles": [
    {"type": "ice", "position": [5, 3]}
  ],
  "available_colors": ["red", "blue", "yellow", "green"],
  "rainbow_chance": 0.05,
  "creature_to_unlock": "coqui",
  "star_thresholds": [280, 430, 600],
  "first_completion_bonus": {
    "coins": 75,
    "gems": 2
  }
}
```

`star_thresholds` es un array de 3 enteros `[1★, 2★, 3★]`. Se calibra playtestando el nivel con el juego real (ver sección 4.2). El score que se compara contra los thresholds es `score_base + tiros_sobrantes × 10`.

```
```

**Validación:** un script `level_validator.gd` carga cada nivel y verifica formato, valida tipos de objetivo, posiciones dentro del grid, etc. Se ejecuta en runtime al cargar y en CI antes de cada release.

### 14.5 Formato de save game

Save local en **JSON encriptado** (AES-256 con key derivada de UUID del dispositivo + salt fijo del juego). Cloud save sincroniza el mismo JSON a Firestore.

```json
{
  "version": "1.0.0",
  "player_id": "uuid-aleatorio",
  "username": "Marina123",
  "current_level": 27,
  "highest_level": 27,
  "creatures_rescued": ["coqui", "burbujin", "lua", ...],
  "currencies": {
    "coins": 4520,
    "gems": 87
  },
  "lives": 3,
  "lives_last_regen": 1730000000,
  "streak": {
    "current": 12,
    "longest": 15,
    "last_claim_day": 12,
    "last_login_timestamp": 1730000000
  },
  "battle_pass": {
    "season": 1,
    "is_premium": true,
    "tier": 18,
    "xp_current_tier": 450
  },
  "achievements": ["bronze_first_win", "bronze_first_creature", ...],
  "settings": {
    "language": "es",
    "music_volume": 0.7,
    "sfx_volume": 1.0,
    "notifications_enabled": true
  },
  "iap_history": ["starter_pack_1", "battle_pass_s1"]
}
```

**Sincronización cloud:**
- Trigger: cada 60 segundos durante gameplay activo (debounced)
- Conflicto: si la versión cloud es más reciente, prevalece cloud
- Ofuscación: el campo `currencies` no se valida solo client-side; transacciones se logean para detectar cheaters

### 14.6 Servicios Firebase

| Servicio | Uso |
|---|---|
| **Authentication** | Login con Apple ID, Google, Facebook. Anonymous auth para usuarios free sin login. |
| **Firestore** | Cloud save (un documento por player_id), leaderboards, friends list |
| **Cloud Messaging (FCM)** | Push notifications |
| **Remote Config** | Tunear drop rates, costos, ofertas sin redeploy. Crítico para LiveOps |
| **Analytics** | Eventos custom (ver sección 15) |
| **Crashlytics** | Crash reporting automático |
| **Cloud Functions** | Validación server-side de scores, anti-cheat, eventos especiales |

**Coste estimado fase 1:** Firebase free tier cubre hasta ~50,000 DAU. Coste se vuelve significativo solo con tracción real.

### 14.7 Anti-cheat (mínimo viable)

| Riesgo | Mitigación |
|---|---|
| Cliente modificado da gemas infinitas | Toda transacción de gemas se loguea en Firestore. Cloud Function detecta jumps imposibles y flagea cuenta |
| Score inflado en leaderboard | Score se computa server-side a partir de "moves history" enviado por el cliente. Si el server no puede reproducir el score, se descarta |
| IAP fake (recibo Android crackeado) | RevenueCat valida recibos contra Apple/Google nativamente |
| Save game editado | Save encriptado client-side; cloud save es source of truth |

### 14.8 Performance targets

| Métrica | Target |
|---|---|
| Frame rate | 60 FPS sostenido en device de gama media (Snapdragon 6 series, iPhone 11+) |
| Tiempo de carga inicial | <3 segundos a Santuario |
| Tiempo de carga de nivel | <1 segundo |
| Memoria usada | <300 MB en runtime |
| Tamaño de instalación | <150 MB MVP |
| Batería | <8% drain por hora de juego activo |

### 14.9 Build y deploy

**Android:**
- Build: **Android App Bundle (AAB)** firmado con keystore propio (NUNCA committearse)
- Distribución: Google Play Console
- Flujo: Internal testing → Closed beta → Open beta → Production
- Min API: 24 (Android 7.0) — cubre 95%+ de devices activos

**iOS:**
- Build: archive vía Xcode (requiere Mac + cuenta Apple Developer)
- Distribución: App Store Connect → TestFlight → App Review → Production
- Min iOS: 14 — cubre iPhone 6s+
- Cumplimiento: SKAdNetwork + ATT obligatorios en 2026

### 14.10 Convenciones de código

| Convención | Regla |
|---|---|
| Naming archivos | `snake_case.gd` |
| Naming clases | `PascalCase` (`class_name`) |
| Naming variables | `snake_case` |
| Naming constantes | `SCREAMING_SNAKE_CASE` |
| Naming signals | `verb_in_past_tense` (`level_completed`, `bubble_popped`) |
| Tipado | Tipado estático cuando posible: `var lives: int = 5` |
| Comentarios | Docstring en funciones públicas con 3+ líneas |
| i18n | Todo string visible al usuario va a `localization/*.csv`, nunca hardcoded |

### 14.11 Decisiones lockeadas

| Decisión | Valor | Razón |
|---|---|---|
| Engine | Godot 4.3+ | Plan Maestro |
| Lenguaje | GDScript con tipado estático | Velocidad de iteración + performance suficiente |
| Backend | Firebase (free tier al inicio) | Plan Maestro |
| IAP | RevenueCat | Plan Maestro, cross-platform |
| Ads | AdMob + AppLovin MAX | Plan Maestro |
| Formato niveles | JSON | Soporta AI gen + diff-friendly en Git |
| Formato save | JSON encriptado AES-256 + sync Firestore | Estándar mobile + cloud save |
| Min Android | API 24 (Android 7.0) | 95%+ cobertura |
| Min iOS | iOS 14 | iPhone 6s+ |
| Orientación | Portrait only | Estándar mobile casual |
| Tamaño MVP | <150 MB | Anti-friction de install |

## 15. Analytics

### 15.1 Filosofía

Sin analytics, **no hay forma de saber qué ajustar después del lanzamiento**. Plan Maestro lo lista entre los errores fatales del indie. Coralia trackea desde el día 1 del MVP.

**Stack:** Firebase Analytics (eventos generales) + GameAnalytics (KPIs específicos de juegos) + Adjust opcional (atribución de marketing post-launch).

### 15.2 KPIs principales a observar

| KPI | Definición | Target casual puzzle 2026 |
|---|---|---|
| **D1 retention** | % de usuarios que vuelven el día 1 tras instalar | ≥40% |
| **D7 retention** | % de usuarios que vuelven el día 7 | ≥20% |
| **D30 retention** | % de usuarios que vuelven el día 30 | ≥8% |
| **DAU / MAU** | Daily / Monthly Active Users | Tracking absoluto |
| **Stickiness** | DAU/MAU ratio | ≥20% (señal de hábito diario) |
| **ARPDAU** | Revenue por DAU | $0.10-0.25 |
| **Conversion rate** | % usuarios que pagan al menos una vez | ≥3% |
| **ARPPU** | Revenue por usuario pagador | $20-50 |
| **eCPM** | Revenue por mil impresiones de ad | $8-40 según mercado |
| **Sesiones por DAU** | Cuántas veces abre el juego/día | 3-5 |
| **Duración promedio sesión** | Minutos | 8-15 |

### 15.3 Eventos a trackear

Lista mínima de eventos custom en MVP. Nombre del evento + parámetros entre paréntesis.

#### Lifecycle

| Evento | Parámetros |
|---|---|
| `app_install` | source, country |
| `app_first_open` | session_id |
| `app_open` | session_id, days_since_install |
| `app_close` | session_duration_seconds |
| `tutorial_step_completed` | step (1, 2, 3) |
| `tutorial_completed` | total_seconds |

#### Gameplay

| Evento | Parámetros |
|---|---|
| `level_start` | level_id, chapter, attempt_number, lives_remaining |
| `level_completed` | level_id, shots_used, shots_max, time_seconds, score, first_completion |
| `level_failed` | level_id, shots_used, reason (out_of_shots / wall_reached) |
| `level_continued` | level_id, method (ad / gems), gems_spent |
| `power_up_used` | level_id, powerup_type |
| `creature_rescued` | creature_id, level_id |
| `chapter_completed` | chapter_id, total_levels_played |
| `combo_achieved` | level_id, combo_size |

#### Economía

| Evento | Parámetros |
|---|---|
| `currency_earned` | type (coins/gems), amount, source |
| `currency_spent` | type (coins/gems), amount, item |
| `iap_initiated` | product_id, price_usd |
| `iap_completed` | product_id, price_usd, transaction_id |
| `iap_failed` | product_id, reason |
| `iap_restored` | product_id |

#### Retención

| Evento | Parámetros |
|---|---|
| `daily_reward_claimed` | day_in_streak, total_streak |
| `streak_broken` | broken_at_day |
| `streak_milestone` | day (7, 30, 100) |
| `daily_mission_completed` | mission_id |
| `weekly_mission_completed` | mission_id |
| `achievement_unlocked` | achievement_id, tier |

#### Battle Pass

| Evento | Parámetros |
|---|---|
| `battle_pass_viewed` | season_id |
| `battle_pass_purchased` | season_id, price_usd, source (gems/iap) |
| `battle_pass_tier_unlocked` | season_id, tier |
| `battle_pass_completed` | season_id, days_to_complete, is_premium |

#### Ads

| Evento | Parámetros |
|---|---|
| `ad_offered` | placement (continue/double_reward/free_life/...), ad_type |
| `ad_started` | placement |
| `ad_completed` | placement, ecpm |
| `ad_skipped` | placement, time_in_ad |
| `ad_failed` | placement, error |

#### Social

| Evento | Parámetros |
|---|---|
| `friend_invited` | method (facebook/code) |
| `friend_added` | total_friends |
| `life_sent` | recipient_id |
| `life_received` | sender_id |
| `sanctuary_visited` | friend_id |

### 15.4 Funnels críticos a monitorear

**Funnel de onboarding:**
1. Install → first_open
2. First_open → tutorial_step_1
3. Tutorial_step_1 → tutorial_step_2
4. Tutorial_step_2 → tutorial_step_3
5. Tutorial_step_3 → first level (4)

Drop-off en cada paso indica problemas específicos. Target: <15% drop entre cada paso.

**Funnel de monetización (primeros 7 días):**
1. App_open → IAP popup viewed
2. IAP popup viewed → IAP_initiated
3. IAP_initiated → IAP_completed

Target conversión global: 3-5%.

**Funnel del Battle Pass:**
1. Season_start → battle_pass_viewed
2. Battle_pass_viewed → battle_pass_purchased
3. Battle_pass_purchased → battle_pass_completed

Target: 12-18% comprar, 80%+ completar si compraron.

### 15.5 Privacidad y compliance

- **GDPR / CCPA / LGPD (Brasil):** consent banner al primer abrir el juego para tracking de analytics y ads. Sin consent, solo trackeo anónimo agregado.
- **Apple ATT:** popup nativo de iOS. Sin permiso, no se trackea IDFA.
- **No PII:** los eventos nunca incluyen email, nombre real, dirección, teléfono. Solo player_id (UUID generado) y atributos agregados (país, idioma, device).

### 15.6 Tools de visualización

| Tool | Uso |
|---|---|
| Firebase Analytics console | Dashboards básicos: DAU, retention, eventos |
| GameAnalytics dashboard | KPIs de juegos, cohorts, ARPDAU detallado |
| Looker Studio (Data Studio) | Dashboards custom conectados a BigQuery export de Firebase |
| Looker / Tableau | Post-launch si la complejidad lo justifica |

### 15.7 Decisiones lockeadas

| Decisión | Valor | Razón |
|---|---|---|
| Stack analytics | Firebase + GameAnalytics | Plan Maestro |
| Tracking desde MVP | Sí, día 1 | Plan Maestro: error fatal no hacerlo |
| Compliance | GDPR + CCPA + LGPD + ATT | Mercados objetivo |
| PII en eventos | NO | Privacy by design |
| Funnels críticos | Onboarding + Monetización + Battle Pass | Drivers de revenue |

## 16. Roadmap Post-MVP / Features Opcionales

Esta sección consolida **todas las features mencionadas a lo largo del GDD que no entran al MVP**. Cualquier mención de "fase 2", "post-launch", "considerar después", "opcional" debe quedar registrada acá para no perder ideas en el desarrollo. Esta sección **se actualiza cada vez que diferimos algo** durante las decisiones de diseño.

### 16.1 Monetización

| Feature | Descripción | Sección original | Cuándo |
|---|---|---|---|
| **Battle Pass Pro ($9.99)** | Tier superior del Battle Pass: incluye todo el premium + 10 tiers automáticos + 200 gemas + skin Pro + 7 días auto daily missions | 8.6 | Fase 2 (3-6 meses post-launch) según data de spending |
| **Suscripción Coralia Plus** | $4.99/mes o $39.99/año: sin ads + 50 gemas/día + vidas infinitas + skin mensual exclusiva + early access | 9.3 | Fase 2 (post 3-6 meses) |
| **Second Chance Starter Pack** | Oferta especial similar al Starter (menos generosa) para jugadores muy engaged que no compraron en su primera semana | 6.10 | Post-launch tras analizar datos de conversión |
| **Personalización de ofertas por segmento** | Casuals ven Weekend Deal, mid-tier ven Progress Pack, whales ven Whale Pack — basado en analytics | 6.10 | LiveOps tras 2-3 meses con data |

### 16.2 Gameplay

| Feature | Descripción | Sección original | Cuándo |
|---|---|---|---|
| **Estrellas por nivel (1-3)** | Sistema tradicional de estrellas según performance del jugador en cada nivel | 4.2 | Solo si la data muestra demanda; complica balance |
| **Editor de niveles públicos** | Liberar editor a jugadores para que creen y compartan niveles propios. Estrategia tipo Mario Maker / Geometry Dash, extiende vida del juego años | Plan Maestro Parte 2.4 | Año 2+ si el juego tiene base sostenida |
| **Mecánicas avanzadas** | Burbujas con efectos especiales nuevos (cadena viva expansiva, burbuja imán, espejo) | — | LiveOps, una nueva por trimestre |

### 16.3 Social

| Feature | Descripción | Sección original | Cuándo |
|---|---|---|---|
| **Chat 1:1 entre amigos** | Mensajería privada con stickers temáticos | 7.9 | Post-MVP fase 2 |
| **Gremios / Clanes** | Grupos de jugadores con leaderboard interno, chat grupal, recompensas colectivas | 7.9 | Post-MVP fase 2 (alta complejidad técnica) |
| **Partidas competitivas asíncronas** | Vos jugás un nivel, mandás el score, un amigo intenta superarlo en 24h | 7.9 | Post-MVP fase 2 |
| **Cooperativo real-time** | Multiplayer sincrónico (dos jugadores en el mismo nivel a la vez) | Plan Maestro Parte 1 | Lejano, requiere arquitectura de servidor compleja |

### 16.4 LiveOps / herramientas internas

| Feature | Descripción | Sección original | Cuándo |
|---|---|---|---|
| **Solver automático de niveles** | Script que simula N partidas de un nivel para verificar solubilidad y balancear dificultad | 2.7 | Construir junto con el editor de niveles, fase MVP-end |
| **A/B testing framework** | Sistema para probar variaciones de pricing, drops, dificultad con cohortes de usuarios | — | Post soft launch cuando hay base de usuarios |
| **Personalización dinámica de dificultad (DDA)** | Ajustar dificultad según el perfil del jugador (rage-quit-prone vs grindy) | — | Post-launch tras 6 meses con data |
| **Tienda de skins ampliada** | Sistema de skins comprables permanentes (no solo Battle Pass) para Marina, cañón, burbujas, fondos del santuario | — | Post-launch, tras temporada 3 |

### 16.5 Plataformas y mercados

| Feature | Descripción | Cuándo |
|---|---|---|
| **Localización a 8+ idiomas** | MVP solo español + inglés. Expandir: portugués, francés, alemán, japonés, coreano, chino simplificado, indonesio, ruso | Antes del global launch (Fase 4) |
| **Versión web (HTML5)** | Port a navegador para Facebook Gaming / web | Año 2 si el mobile tracciona |
| **Versión iPad / tablet** | Adaptación de UI para pantallas grandes | Mes 6 post-launch |

### 16.6 Cómo gestionar este backlog

- Cada feature de esta lista tiene una **estimación de esfuerzo y un trigger de activación** (data, mes calendario, hito de revenue) que se documenta cuando se prioriza
- En cada sprint review post-launch, revisar esta lista y promover 1-2 features al backlog activo
- Si una feature lleva 12+ meses sin activarse, evaluar si sigue siendo relevante o se descarta

## 17. Apéndices

### 17.1 Glosario

| Término | Definición |
|---|---|
| **MVP** | Minimum Viable Product. Versión mínima del juego que se puede lanzar y monetizar. 60 niveles + sistemas core. |
| **F2P** | Free-to-Play. Modelo donde el juego es gratis y monetiza via ads + IAP. |
| **IAP** | In-App Purchase. Compra dentro del juego (gemas, packs, etc.). |
| **ARPDAU** | Average Revenue Per Daily Active User. Métrica clave de revenue. |
| **ARPPU** | Average Revenue Per Paying User. Solo cuenta los que pagan. |
| **eCPM** | Effective Cost Per Mille. Revenue por mil impresiones de anuncios. |
| **CPI** | Cost Per Install. Costo de adquirir un usuario via marketing. |
| **D1/D7/D30** | Retention al día 1, 7, 30 tras instalar. |
| **DAU/MAU** | Daily/Monthly Active Users. |
| **LTV** | Lifetime Value. Revenue total esperado de un usuario en su vida en el juego. |
| **Gacha / Loot box** | Compra con resultado aleatorio. **Coralia no usa loot boxes** por compliance y ética. |
| **LiveOps** | Live Operations. Mantenimiento del juego post-launch: nuevos niveles, eventos, ofertas. |
| **Soft launch** | Lanzamiento limitado en pocos países antes del global, para iterar con data real. |
| **Walls de pago** | Niveles muy difíciles donde el jugador siente presión a pagar power-ups. **Anti-pattern; Coralia lo mitiga.** |
| **ASO** | App Store Optimization. Optimizar keywords, screenshots, descripción para ranking en stores. |
| **SDK** | Software Development Kit. Librería que se integra al juego (AdMob, RevenueCat, Firebase, etc.). |
| **Mediation** | Layer que conecta múltiples ad networks para maximizar eCPM por bidding competitivo. |
| **Rewarded ad** | Anuncio que el jugador elige ver a cambio de recompensa. El más rentable y best practice 2026. |
| **Battle Pass** | Sistema de progresión temporal con tracks free + premium. Convierte 12-18% vs 2-5% IAP. |
| **GDD** | Game Design Document. Este documento. |

### 17.2 Referencias y archivos relacionados

| Documento | Ubicación | Propósito |
|---|---|---|
| Plan Maestro | `docs/Plan_Maestro_Bubble_Shooter.docx` | Planificación original del proyecto (9 partes) |
| Concepto Inicial | `docs/01_Concepto_Inicial.md` | Decisiones creativas de Fase 0 |
| GDD (este documento) | `docs/02_GDD_Coralia.md` | Especificación completa del MVP |

### 17.3 Cambios y versiones

| Versión | Fecha | Cambios |
|---|---|---|
| 0.1 | 2026-04-30 | Primera versión completa: secciones 1-16, todas las decisiones MVP locked-in |
| 0.2 | 2026-04-30 | Sección 10: split del Splash en Company Splash + Loading Splash. Total pantallas pasa de 16 a 17. Convención cross-proyecto con Impostor app. |
| 0.3 | 2026-04-30 | Sección 10 (Settings): adoptada estructura cross-proyecto de 4 secciones (Preferencias / Cuenta / Comunidad / Legal). Sección 12 (audio): 3 sliders separados (sonidos del juego / efectos interfaz / sonidos pop) + toggle de vibración. Sección 14 (localización): expandido a 6 idiomas (es, en, it, fr, de, pt). |
| 0.4 | 2026-04-30 | Decisiones lockeadas: Tema = Claro/Oscuro/Automático (Modo Arrecife / Modo Profundidades). Los 6 idiomas se lanzan completos al MVP. Estudio: nombre confirmado, pendiente especificar. |
| 0.5 | 2026-04-30 | Estudio confirmado: **myappcube**. Localización con pipeline local (Claude + polish opcional) reduce costo de localización de ~$1,500-4,500 a ~$0-300. Sección 12.2bis agrega pipeline detallado de i18n. |
| 0.6 | 2026-04-30 | Localización pasa a costo $0 estricto: solo Claude + red personal (favores). Sin Fiverr ni servicios pagados. |

### 17.4 Decisiones abiertas

- **Disponibilidad de "Coralia" como nombre:** verificar dominios (.com, .app), App Store, Google Play, redes sociales antes de avanzar a producción.
- **Disponibilidad de "myappcube" como estudio:** verificar dominios y redes sociales del estudio (probablemente ya gestionado por Diego desde Impostor app).

### 17.5 Decisiones pendientes que aparecerán en versiones futuras

A medida que avancemos al prototipo, MVP, soft launch y global launch, irán apareciendo decisiones que requieren refinar este GDD:

- **Tras prototipo:** validación de feel del gameplay → ajustes a sección 1
- **Tras MVP interno:** balance real de niveles → ajustes a sección 2
- **Tras soft launch:** datos reales de retention/ARPDAU → ajustes a secciones 6-9
- **LiveOps continuo:** nuevas mecánicas, eventos, temporadas → ajustes a secciones 7-8
- **Promoción de opcionales:** items movidos de sección 16 al backlog activo

Este GDD es **un documento vivo**. Se actualiza cada vez que tomamos una decisión de diseño, cambiamos un número, o promovemos un opcional al MVP.

---

**Fin del documento.**
