# Concepto Inicial — Coralia (Bubble Shooter Submarino)

> Documento vivo de pre-producción. Para el detalle completo de mecánicas, sistemas, economía y arquitectura técnica ver `02_GDD_Coralia.md`.


**Estado:** Borrador v0.3 — 2026-04-30
**Nombre del juego:** **Coralia**
**Estudio:** **myappcube**
**Autor:** Diego

---

## Pitch en una línea

Un Bubble Shooter cozy submarino donde Marina, una joven sirena, restaura un arrecife de coral apagado rescatando criaturas marinas atrapadas en burbujas, mientras el mundo cobra vida visualmente con cada nivel completado.

## Premisa

Marina vive en un arrecife de coral que alguna vez fue el más vibrante del océano. Una corriente oscura dispersó las **Burbujas de Vida** que mantenían el ecosistema, y el arrecife empezó a apagarse: los corales perdieron color, los peces emigraron, la magia desapareció. Marina recorre el arrecife recuperando burbujas y rescatando a las criaturas marinas que quedaron atrapadas, devolviendo vida y color a su hogar.

## Decisiones de diseño locked-in

| Eje | Decisión |
|---|---|
| Estudio | myappcube |
| Nombre del juego | Coralia |
| Género | Bubble Shooter |
| Tema | Mundo submarino / arrecife de coral |
| Protagonista | Marina, joven sirena |
| Tono visual | Cozy / wholesome — pastel, luminoso, suave |
| Tema de UI | Modo Arrecife (claro) + Modo Profundidades (oscuro) + Automático |
| Escala del mundo | Un solo arrecife progresivo que se expande y restaura |
| Audiencia objetivo | Mujeres 25-45, casual |
| Lanzamiento | LATAM-first → global (estrategia de mercado, no estética) |
| Localización | 6 idiomas al MVP (es, en, it, fr, de, pt) — gestión $0 con AI + red personal |
| Stack | Unity 6 + C# |
| Modelo | F2P híbrido (Ads + IAP + Battle Pass) |

## Loop emocional por nivel

1. **Pre-nivel** — aparece una criatura marina asustada (bebé tortuga, caballito de mar, pulpo, anémona, medusa, pez payaso, estrella de mar, etc.) pidiendo ayuda
2. **Gameplay** — Marina dispara burbujas de colores para liberarla
3. **Post-nivel** — la criatura se une al santuario; animación corta de cómo se acomoda en el arrecife
4. **Cada 10 niveles** — una zona del arrecife revive visualmente: color, plantas, criaturas pueblan; se desbloquea siguiente capítulo

## Tres pilares de retención

### 1. Narrativa
Arco emocional ligado al rescate de criaturas y restauración del arrecife. Cada criatura tiene mini-historia (1-2 líneas de diálogo). Tono: tierno, sin villano explícito al inicio (la corriente oscura es ambiental, no antagónica).

### 2. Meta-progresión visual
**El mapa de niveles ES el arrecife restaurándose.** Empieza gris/marchito y se va llenando de color, plantas, criaturas y luz a medida que el jugador avanza. Cada 10 niveles desbloquea una zona nueva. El último capítulo del MVP revela la **Ciudad de las Perlas** (la "fortaleza" final tras 60+ niveles).

### 3. Coop asíncrono
- Enviar/pedir burbujas (vidas) a amigos
- Visitas al santuario: amigos pueden ver tus criaturas rescatadas
- Leaderboard semanal *Marea Alta* — top 10 reciben recompensas
- Eventos de temporada: *Festival de Coral*, *Luna Llena Submarina*

## Diferenciador concreto vs competidores

| Competidor | Su propuesta | Cómo nos diferenciamos |
|---|---|---|
| Panda Pop | Rescate animal terrestre cute | Rescate marino con meta-restauración visual del mundo |
| Bubble Witch | Brujería medieval, paleta oscura | Cozy luminoso submarino, paleta pastel |
| Bubble Shooter Classic | Sin tema, gameplay puro | Narrativa fuerte, arco emocional, meta-progresión |

**Coherencia mecánica/temática:** Las burbujas tienen sentido narrativo natural en un mundo submarino — no son una metáfora forzada como en otros temas. Esto hace que la experiencia se sienta integrada.

## Audiencia objetivo (refinada)

- **Primaria:** mujeres 25-45, casual gamers, buscando escape relajante en sesiones cortas (2-10 min)
- **Secundaria:** familias, jugadores de cozy games (Stardew, Animal Crossing) que valoran progresión visual y temas wholesome
- **Mercado de prueba:** LATAM (CPI bajo, afinidad cultural con cozy/cute)

## Inspiración visual y de tono

Para alinear arte y mood (referencias para AI gen y briefs a freelancers):
- **Estilo y paleta:** Animal Crossing: New Horizons, Stardew Valley, Cocoon, Coral (PS4)
- **Submarino cozy:** Finding Nemo (paleta), Octonauts (cute marine creatures), My Octopus Teacher (mood contemplativo)
- **Sirenas no-Disney:** estética suave, no princesa hiperestilizada — más folk/natural

## Riesgos identificados

1. **Ejecución de arte como solo dev** — mitigación: priorizar AI gen + asset stores + freelancer puntual para personajes principales (Marina + 6 colores de burbujas + 8-10 criaturas hero). Presupuesto art: $2,000-4,000 (ver GDD sección 11.5).
2. **Diferenciación percibida en stores** — mitigación: screenshot principal debe mostrar restauración visual del arrecife (antes/después), no solo gameplay.
3. **Escalabilidad narrativa** — mitigación: cada criatura tiene 1-2 líneas, no historias largas; reutilizable en LiveOps.
4. **Calidad de localización con AI translation** — francés y alemán típicamente requieren más cuidado que italiano/portugués. Mitigación: priorizar revisión por hablantes nativos de la red personal específicamente para esos dos idiomas antes del lanzamiento (presupuesto $0 — solo favores de la red).
5. **Doble paleta UI (Light + Dark)** — el tema oscuro implica que cada elemento de UI necesita variantes claro/oscuro. Mitigación: definir tokens de color como assets compartidos (ScriptableObject o material shared) en Unity; muchos elementos solo cambian de paleta sin redibujarse.

## Convenciones cross-proyecto con myappcube

Coralia hereda patrones establecidos en otra app del estudio (Impostor) para mantener coherencia entre productos. Esto se documenta acá explícitamente para que cualquier persona que abra este doc en el futuro entienda el origen de las decisiones:

| Convención | Cómo se aplica en Coralia | Detalle en GDD |
|---|---|---|
| **Two-splash pattern** | Pantalla 1 Company Splash (logo myappcube) + Pantalla 2 Loading Splash (logo Coralia + barra de carga + versión) | Sección 10.3 |
| **Settings de 4 secciones** | Preferencias del juego / Cuenta y asistencia / Comunidad / Legal | Sección 10.3 (Pantalla 8) |
| **3 sliders de audio + vibración** | Sonidos del juego / Efectos interfaz / Sonidos pop / toggle de vibración | Sección 12.4 |
| **6 idiomas al MVP** | es, en, it, fr, de, pt — gestión $0 con AI + red personal | Sección 12.2bis |
| **Tema Light/Dark/Auto** | Modo Arrecife / Modo Profundidades / sigue al SO | Sección 10.3 (Pantalla 8) |

## Estado de las decisiones de Fase 0

Todas las decisiones de diseño que originalmente quedaron pendientes en este concept doc se resolvieron durante la redacción del GDD (`02_GDD_Coralia.md`). Para detalles puntuales:

| Tema | Dónde está la decisión |
|---|---|
| Paleta de colores específica | GDD sección 11.2 (12 tokens de color) |
| Los 6 colores de burbujas iniciales | GDD sección 11.2 (azul, amarillo, verde, púrpura, rojo + arcoíris) |
| Las 12 criaturas hero del MVP | GDD sección 13.3 (con nombre, especie, personalidad y diálogo) |
| Identidad del antagonista | GDD sección 13.4 (La Sombra Profunda — herida, no malvada) |
| Voice y diálogos de criaturas | GDD sección 13.6 |
| Estructura de capítulos | GDD sección 2.1 (6 capítulos × 10 niveles) |
| Economía completa | GDD sección 6 |

**Decisión administrativa pendiente:** verificar disponibilidad de "Coralia" en App Store, Google Play, dominios .com/.app y redes sociales antes de avanzar a producción.
