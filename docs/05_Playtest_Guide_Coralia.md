# Guía de Playtest — Coralia

**Versión:** 0.2 — 2026-10-03
**Objetivo:** validar con 3-5 jugadores reales si el juego es **divertido** antes de seguir invirtiendo.

> El playtest previsto para la Fase 1 nunca se hizo. Ahora el loop de juego está completo de punta
> a punta, así que ya hay algo que poner en manos de alguien — y sigue siendo el riesgo abierto más
> grande del proyecto (ver `07_Status_y_Roadmap.md`).

---

## Por qué este playtest es la decisión más importante de Fase 1

Hasta ahora todo el feedback fue tuyo. Sos el creador del juego: tu juicio sobre si es divertido es **inherentemente sesgado** porque conocés cada mecánica, cada nivel, cada decisión. Necesitás ojos frescos.

Si los playtesters te dicen "no es divertido", eso vale **muchísimo más** que tu intuición de que sí lo es. Mejor saberlo en la semana 5 de Fase 1 que en el mes 7 cuando ya invertiste en arte, audio, monetización.

**Regla fundamental durante el playtest:** vos no hablás. Solo observás y tomás notas. Si el jugador no entiende algo, **NO le ayudes**. Esa confusión es un dato. Si necesitás explicarle cómo funciona, eso significa que el juego no se enseña a sí mismo bien.

---

## Paso 1: Build para el tester

### Opción A — en el móvil del tester (recomendada)

Es un juego mobile: probarlo con el dedo, en vertical y en la pantalla real es parte de lo que se
está midiendo.

**Android:** `File → Build Settings → Android → Build`, y le pasas el `.apk`. El tester tiene que
permitir instalar de orígenes desconocidos.

**iOS:** necesita cuenta de Apple Developer y TestFlight, que todavía no está montado (ver las
decisiones administrativas en `07_Status_y_Roadmap.md`). Mientras tanto, Android o la opción B.

### Opción B — standalone de escritorio

1. `File → Build Settings → macOS` (o Windows) → **Build**
2. Guárdalo como `Coralia-playtest`
3. Ábrelo tú primero: tiene que arrancar igual que en Play mode
4. Comprime la carpeta a `.zip` para compartirla

**Si en otra Mac dice "App is damaged":** macOS bloquea apps sin firmar. El tester entra a
**Ajustes → Privacidad y seguridad → "Abrir de todos modos"**, o ejecuta
`sudo xattr -cr /ruta/a/Coralia.app`. Es esperable: no firmamos el build.

**Deja el juego en un estado limpio antes de pasarlo.** Si tu partida tiene 99 bombas y el nivel 40
desbloqueado, el tester no va a ver lo que ve un jugador nuevo — que es justo lo que quieres medir.
El botón de reset está en Settings.

---

## Paso 2: Reclutar 3-5 testers

Mezcla ideal de perfiles:

| Perfil | Cantidad | Por qué importa |
|---|---|---|
| Mujer 25-45, juega móviles casuales | 1-2 | **Audiencia primaria del juego.** Su feedback tiene 2x peso. |
| Jugador casual general (cualquier edad) | 1-2 | Representa al jugador "promedio" que descarga apps |
| Jugador hardcore (RPGs, FPS, etc.) | 1 | Ojos críticos, detecta bugs y problemas de diseño que casuals no ven |
| (Opcional) Adulto mayor 50+ | 0-1 | Audiencia secundaria importante en mobile |
| (Opcional) Niño 8-12 con permiso de padre | 0-1 | Solo si te interesa expandir audiencia más adelante |

No uses 5 testers del mismo perfil — el sesgo te da feedback unidimensional.

---

## Paso 3: Protocolo de la sesión (15-20 min por tester)

### Antes de empezar (2 min)

Decile al tester:

> "Voy a darte un prototipo de un juego que estoy desarrollando. Quiero que lo juegues como si lo hubieras descargado del App Store. **Yo no te voy a dar instrucciones ni explicaciones** — quiero ver qué entendés vos solo. Voy a tomar notas mientras jugás. Si te trabás, intentá descifrarlo. Si no podés y te frustrás, decime y paso al siguiente nivel. ¿Listo?"

Es importante decir esto explícitamente. El tester por default va a pedirte ayuda; vos tenés que aclarar que NO vas a dar.

### Durante la sesión (10-15 min)

**Vos:**
- Abrí el .app en su Mac (o pasale el zip y que lo abra, mejor aún para simular descarga real)
- Sentate cerca pero **NO les dirijas la mirada al monitor**
- Cuaderno y lapicera (o app de notas) listos
- Cronometrá cuánto tarda cada nivel
- **NO HABLÁS.** Si te preguntan "¿qué tengo que hacer?", respondé "lo que vos creas". Si insisten, "no te puedo decir, parte del test es ver si lo descifrás solo".

**Qué observar y anotar:**

| Categoría | Qué observar |
|---|---|
| **Onboarding** | ¿Entiende qué hacer en el nivel 1 sin que le digan? ¿Cuánto tarda en hacer su primer disparo? |
| **Mecánica de apuntado** | ¿Le sale natural el drag? ¿Apunta con la trayectoria visible? ¿Se confunde con la línea? |
| **Match detection** | ¿Sonríe cuando explotan burbujas? ¿Se da cuenta cuando hace combo? |
| **Rescue objective (niveles 2-5)** | ¿Entiende qué es la estrella dorada? ¿Sabe que tiene que rescatarla? Si no, ¿cuándo se da cuenta? |
| **Cola del cañón** | ¿Usa el color swap (tap)? ¿Anticipa el siguiente color? |
| **Frustración** | Cara, suspiros, "uff", silencios largos. ¿En qué nivel/momento? |
| **Diversión** | Sonrisa, "ohh", "yes!", "ja". ¿Qué momentos generan eso? |
| **Abandono** | Si te dice "ya está, no quiero más" → ¿en qué nivel? ¿por qué? |

### Después de cada nivel

Anotá:
- ¿Ganó o perdió?
- Tiempo total
- Disparos usados vs disponibles
- Comentario espontáneo si lo hizo

### Después de los 5 niveles (preguntas estructuradas)

Después de jugar todos los niveles (o cuando el tester abandone), hacé estas preguntas en orden:

1. **"¿Cómo te fue? ¿Te divertiste?"** (abierta — anotá su respuesta literal)
2. **"En una escala 1 a 10, ¿qué tan divertido fue?"**
3. **"Si esto fuera un juego real con muchos más niveles, ¿lo seguirías descargando y jugando?"** (sí/no/depende)
4. **"¿Qué fue lo más divertido?"**
5. **"¿Qué fue lo más frustrante?"**
6. **"¿En algún momento no entendiste qué tenías que hacer?"** (si dice sí: ¿cuándo?)
7. **"¿La estrella dorada — entendiste qué significa?"** (audiencia primaria suele entenderlo, hardcore puede ser literal)
8. **"¿Algo del visual te confundió o no te gustó?"**

---

## Paso 4: Decisión post-playtest

Después de los 3-5 testers, sentate con todas las notas y respondé estas 3 preguntas con **honestidad despiadada**:

### Pregunta 1: ¿Es divertido?

| Resultado | Acción |
|---|---|
| Promedio diversión ≥7/10 Y mayoría querría seguir jugando | ✅ **AVANZAR a Fase 2 (MVP).** El core mecánico es sólido. |
| Promedio 5-7/10, sentimiento mixto (algunos sí, otros no) | ⚠️ **ITERAR 1-2 semanas.** Identificar lo más roto, ajustar, re-playtest con 2-3 testers más. |
| Promedio <5/10, mayoría no quiere seguir | 🛑 **REPLANTEAR o CANCELAR.** Algo del core está roto. Volver a Fase 0 o pivotar concepto. |

### Pregunta 2: ¿Qué hay que arreglar urgente?

Listar los 3 problemas más comunes mencionados por los testers:

1. _____ (problema mencionado por X de Y testers)
2. _____
3. _____

Estos son los primeros candidatos para iteración en Fase 2.

### Pregunta 3: ¿Hay sorpresas?

Cosas que NO esperabas:
- Mecánicas que les costó entender más de lo previsto
- Mecánicas que les encantaron más de lo previsto
- Bugs nuevos que vos no habías visto

---

## Documentar resultados

Crear `docs/06_Playtest_Results.md` con:

- Fecha y participantes (anonimizados)
- Resumen por tester
- Tabla de scores
- Quotes destacadas
- Decisión final
- Plan de acción

Esto queda como evidencia para el futuro — cuando el juego esté lanzado y veas la data real, vas a poder comparar contra estas observaciones tempranas.

---

## Tips importantes

- **Grabá audio (con permiso)** si es posible. Los gestos faciales se pierden, pero los comentarios y silencios capturados en audio son oro.
- **No interpretes en el momento.** Apuntá observaciones objetivas ("se quedó 30 segundos sin disparar en el nivel 3"), no interpretaciones ("le aburrió"). La interpretación viene después al cruzar varios testers.
- **No des nada por sentado.** Si te dicen "está bueno" cuando claramente estaban aburridos, es cortesía social. La data real está en el lenguaje corporal y en cuánto rato jugaron sin pedir parar.
- **Recibí feedback negativo con gracia.** Tu primer instinto va a ser defender el juego ("no, es que tenés que entender X..."). Aguantáte. Si lo defendés, perdés feedback futuro.
