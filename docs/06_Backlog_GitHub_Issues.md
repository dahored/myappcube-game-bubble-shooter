# Backlog — GitHub Issues para Coralia

**Versión:** 0.3 — 2026-10-03
**Propósito:** este documento traduce el plan del proyecto a issues de GitHub. Cada sección H2 (`##`) es un issue independiente — copy-pasteable directo a "New Issue" en GitHub.

Los issues ya completados quedan marcados con **✅ COMPLETADO** debajo del título, con una nota de qué se hizo realmente.

---

## Cómo usar este doc

Para cada issue:
1. **Título** = el H2 (sin el prefijo `## `)
2. **Body** = todo el contenido bajo el H2 hasta el siguiente H2
3. **Labels** = los listados en `Labels:` de cada issue
4. **Milestone** = `Phase 1`, `Phase 2 (MVP)`, `Phase 3 (Soft Launch)`, `Phase 4 (Global Launch)`, o `Backlog` para los de largo plazo

Recomendado crear estos labels en GitHub primero:
- **Phases:** `phase-1`, `phase-2`, `phase-3`, `phase-4`, `backlog`
- **Types:** `feat`, `bug`, `chore`, `docs`, `tech`, `polish`
- **Areas:** `gameplay`, `ui`, `audio`, `economy`, `levels`, `social`, `monetization`, `narrative`, `art`, `i18n`, `infra`
- **Priorities:** `priority-high`, `priority-medium`, `priority-low`
- **Sizes:** `size-XS` (<2h), `size-S` (~half day), `size-M` (1-2 days), `size-L` (3-5 days), `size-XL` (1+ week)

---

# Estado actual (referencia, no es un issue)

**Fase 1 — Prototipo:** ✅ Completada. Referencia de diseño; el código no se portó 1:1.
**Fase 2 — MVP:** 🔄 En progreso, motor **Unity 6**. Completado: Settings, Localización runtime, Level Map/Level Select, HUD superior (`ResourcePillView` — visual listo, datos pendientes). Sigue faltando: gameplay (cañón/grid/match), sistema de vidas/monedas con datos reales, Santuario, onboarding.

Ver `07_Status_y_Roadmap.md` para el detalle completo y actualizado.

Los issues debajo están agrupados por fase y prioridad.

---

# Fase 2 — MVP (en progreso)

## [Phase 2] Audio: música + SFX in-game

✅ **PARCIALMENTE COMPLETADO** — `AudioManager` (clase estática C#, `Scripts/Core/AudioManager.cs`) ya existe con 4 canales (`music`, `sfx`, `ui`, `pop`), volúmenes leídos de `SaveManager`, y música de lobby sonando. Falta: SFX de gameplay (pop, drop, win/lose) porque el gameplay todavía no existe.

**Labels:** `phase-2`, `feat`, `audio`, `priority-high`, `size-S`

### Descripción
Conectar los SFX de gameplay al `AudioManager` ya existente cuando se implemente el cañón/grid/match. La infraestructura de audio (canales, volúmenes, mute) ya está lista — este issue queda reducido a "agregar los clips y llamar `AudioManager.Instance.PlaySfx(...)` en los momentos correctos".

### Acceptance criteria
- [x] `AudioManager` con canales configurables: `music`, `sfx` (ui_fx), `ui`, `pop` (bubble_pop) — ya implementado, un canal más que el GDD original (separa UI de gameplay SFX)
- [x] Volúmenes leídos de `SaveManager` (`MusicVolume`, `SfxVolume`, `UiVolume`, `PopVolume`)
- [x] Vibración (toggle) implementada — vía plugin `MOST_HapticFeedback` (bridge nativo Android/iOS)
- [x] Música de fondo loop en el lobby (`AudioManager.PlayLobbyMusic()`)
- [ ] SFX de pop de burbuja al hacer match (con variación leve de pitch) — bloqueado por gameplay
- [ ] SFX de drop de flotantes — bloqueado por gameplay
- [ ] SFX de victoria al completar nivel — bloqueado por gameplay
- [ ] SFX de derrota al perder — bloqueado por gameplay
- [ ] SFX de tap de botones (UI) — `AudioManager.PlayUi()` ya existe, falta asignar clips a los botones

### Referencias
- GDD sección 12 (Audio)
- GDD sección 12.4 (Mix y mastering)
- `Scripts/Core/AudioManager.cs` (implementación real)

---

## [Phase 2] Primer power-up: Bomba de Coral

**Labels:** `phase-2`, `feat`, `gameplay`, `priority-medium`, `size-M`

### Descripción
Implementar el primer power-up del GDD: la Bomba de Coral. Explota una zona 3x3 (en hex grid: la celda + 6 vecinos hexagonales) alrededor del impacto. Es el power-up más simple y sirve para establecer el patrón de implementación de los otros 5. **Bloqueado hasta que exista el gameplay base** (`Scripts/Gameplay/` está vacío).

### Acceptance criteria
- [ ] UI de selección de power-up en pantalla pre-level (slot equipable)
- [ ] Power-up consumible (1 uso por nivel)
- [ ] Activación: tap en ícono del power-up en HUD durante gameplay → próximo disparo es bomba
- [ ] Visual distintivo de la burbuja-bomba en el cañón (marcador rojo o ícono)
- [ ] Al impactar grid, explota celda + 6 hex vecinos
- [ ] Animación de explosión con partículas/shake
- [ ] Costo: 8 monedas (placeholder hasta implementar economía)
- [ ] Counter de power-ups disponibles guardado en `SaveManager`

### Referencias
- GDD sección 3.2 (Power-ups del MVP)
- GDD sección 3.3 (Activación)

---

## [Phase 2] Sistema de vidas (5 vidas, regen 30 min)

**Labels:** `phase-2`, `feat`, `economy`, `gameplay`, `priority-high`, `size-M`

### Descripción
Implementar el sistema de vidas que es base de la monetización F2P. 5 vidas máximo, una se regenera cada 30 minutos, se pierde al fallar un nivel. **El HUD ya está listo** (`ResourcePillLives`, prefab variant de `ResourcePillView` con soporte para número, "Full", timer `mm:ss` y badge de ∞) — este issue es agregar los datos reales en `SaveManager` y llamar a los métodos del componente (`SetValue`, `SetFull`, `SetTimer`, `SetBadgeInfinite`).

### Acceptance criteria
- [x] HUD muestra el pill de vidas con ícono + valor + botón "+" — `ResourcePillLives Variant.prefab`
- [x] Componente soporta modo número, "Full", timer `mm:ss` y badge infinito — `ResourcePillView.cs`
- [ ] `SaveManager` no tiene campos de vidas todavía — agregar `Lives` (int, PlayerPrefs) y `LivesLastRegen` (timestamp)
- [ ] Al perder un nivel, decrementa vidas en 1 (bloqueado por gameplay)
- [ ] Al ganar un nivel, NO consume vida
- [ ] Si vidas = 0 al intentar jugar nivel: popup "Sin vidas" con opciones (esperar / pagar monedas / ver ad — placeholders por ahora)
- [ ] Cálculo de regen: cada 30 min real desde `LivesLastRegen`, hasta cap de 5 — conectar con `ResourcePillView.SetTimer()`
- [ ] Persiste correctamente al cerrar/reabrir el juego

### Referencias
- GDD sección 6.2 (Sistema de vidas)
- `Scripts/UI/ResourcePillView.cs` (HUD ya implementado)
- `Scripts/Core/SaveManager.cs` (agregar campos acá, sigue el patrón de propiedades existente)

---

## [Phase 2] Sistema de monedas con drops por nivel

**Labels:** `phase-2`, `feat`, `economy`, `priority-high`, `size-M`

### Descripción
Implementar las fuentes de monedas según GDD 6.3. **Ojo: el GDD describía dos monedas y las gemas se eliminaron en 2026-10** — hay una sola, y hoy completar un nivel NO da monedas a propósito (ver GDD 6.3), así que este issue es sobre las otras fuentes: santuario, dailies, misiones y logros. **El HUD de monedas ya está listo** (`ResourcePillCoins Variant.prefab`) mostrando un valor hardcodeado — falta conectar datos reales y los drops al completar niveles. Sin compras IAP todavía (eso viene en chunk separado).

### Acceptance criteria
- [x] HUD muestra el pill de monedas con ícono + valor + botón "+" — `ResourcePillCoins Variant.prefab`
- [ ] `SaveManager` no tiene campos de economía todavía — agregar `Coins` (int) y `Gems` (int)
- [ ] Drops por nivel completado (bloqueado por gameplay):
  - 50-100 monedas según capítulo (GDD 6.6)
  - 1-3 monedas con probabilidad ~30%
  - +50% bonus en monedas para primera completación de un nivel
- [ ] Animación de drop de currencies al final del nivel (caen del modal a los HUD counters)
- [ ] Persistencia en `SaveManager`
- [ ] Tracking de "primera completación" por nivel para bonus
- [ ] Botón "+" del pill de monedas (`ResourcePillView.OnPlusClicked`) abre la Shop — todavía no implementado

### Referencias
- GDD sección 6.3 (Monedas)
- GDD sección 6.4 (Monedas)
- GDD sección 6.6 (Drops por nivel)
- `Scripts/UI/ResourcePillView.cs` (HUD ya implementado)

---

## [Phase 2] Más niveles: de 30 a 60 con AI gen

✅ **PARCIALMENTE COMPLETADO** — hay 30 niveles reales en `Resources/Levels/Chapter_1/2/3/` (10 por capítulo), más de los 20 originales del plan.

**Labels:** `phase-2`, `feat`, `levels`, `priority-medium`, `size-L`

### Descripción
Completar de 30 a 60 niveles con curva de dificultad coherente para llegar a los 6 capítulos × 10 niveles del MVP. Usar Claude para generar borradores según GDD sección 2.4 (curva de dificultad). Cada nivel se valida manualmente.

### Acceptance criteria
- [x] 30 niveles en `Resources/Levels/Chapter_1/Chapter_2/Chapter_3/*.json` (modelo: `LevelData.cs`)
- [ ] 30 niveles más para completar capítulos 4, 5 y 6 (`Chapter_4/5/6`)
- [ ] Variedad en tipos de objetivos: rescue, clear_all, color_count
- [ ] Variedad en posiciones de criatura (top, middle, deep) y columnas
- [ ] Cada nivel verificado como ganable — bloqueado hasta que exista gameplay jugable
- [ ] Sistema de carga refleja el nuevo conteo de niveles/capítulos

### Referencias
- GDD sección 2.4 (Curva de dificultad)
- GDD sección 2.7 (Estrategia híbrida hand + AI)
- `Scripts/Data/LevelData.cs`

---

## [Phase 2] Localización activada en runtime

✅ **COMPLETADO** — `LocaleManager` + `LocalizedText` implementados y funcionando.

**Labels:** `phase-2`, `feat`, `i18n`, `priority-medium`, `size-M`

### Descripción
~~Activar la localización i18n~~ Ya está activa: `LocaleManager` carga `Resources/translations.csv` (6 idiomas), `LocalizedText.cs` refresca texto estático en UI, `LocaleManager.OnLanguageChanged` para texto dinámico, selector de idioma funcional en Settings (dropdown), preferencia persistida en `SaveManager.Language` con detección automática del idioma del sistema al primer abrir.

### Acceptance criteria
- [x] `LocaleManager` carga `translations.csv` vía `Resources.Load<TextAsset>`
- [x] `LocalizedText.cs` para texto estático, evento `OnLanguageChanged` para dinámico
- [x] Selector de idioma funcional en Settings (dropdown real, no debug cycler)
- [x] Cambio de idioma en runtime sin reiniciar (`LocaleManager.Reload()`)
- [x] Preferencia guardada en `SaveManager.Language`
- [x] Detecta idioma del sistema al primer abrir (`SaveManager.DetectLanguage()`)
- [ ] Auditar que TODOS los strings de HUD/gameplay futuro usen `LocaleManager.Get()` — pendiente a medida que se agreguen pantallas nuevas

### Referencias
- GDD sección 12.2bis (Localización)
- `Scripts/Core/LocaleManager.cs`, `Scripts/UI/LocalizedText.cs`

---

## [Phase 2] Onboarding tutorial (los 3 pasos del GDD)

**Labels:** `phase-2`, `feat`, `ui`, `priority-medium`, `size-M`

### Descripción
Implementar el tutorial interactivo de 3 pasos descrito en GDD sección 10.3 Pantalla 3 (Onboarding). Solo se ejecuta una vez por jugador (flag en save). Bocadillos de Marina + puntero animado. No implementado todavía.

### Acceptance criteria
- [ ] Escena `Onboarding.unity` + script `Onboarding.cs`
- [ ] Pasos 1-3 según GDD: apuntar, soltar, explicar match
- [ ] Bocadillos de Marina con texto vía `LocaleManager.Get()` (keys nuevas en `translations.csv`)
- [ ] Puntero animado / flecha que indica la acción
- [ ] Botón "Saltar tutorial" oculto los primeros 2s, después aparece con fade
- [ ] Al completar paso 3, marca `tutorial_completed = true` en `SaveManager` y va a Santuario (o Gameplay temporalmente)
- [ ] `SceneLoader`/routing inicial respeta `tutorial_completed`: si false, va a onboarding; si true, va a home/gameplay

### Referencias
- GDD sección 10.3 Pantalla 3 (Onboarding)
- `Scripts/Core/SceneLoader.cs` (routing)

---

## [Phase 2] Daily reward y streak (racha de 7 días)

**Labels:** `phase-2`, `feat`, `retention`, `priority-medium`, `size-M`

### Descripción
Implementar la racha diaria con loop de 7 días según GDD sección 7.2. Pop-up al primer login del día con la recompensa correspondiente. Indicador de racha en HUD del santuario. No implementado todavía.

### Acceptance criteria
- [ ] Pantalla Daily Rewards según GDD sección 10.3 Pantalla 5
- [ ] Recompensas día 1-7 según GDD 7.2:
  - Día 1: 50 monedas
  - Día 2: 100 monedas
  - Día 3: 5 monedas
  - Día 4: 1 power-up aleatorio
  - Día 5: 200 monedas + 1 vida
  - Día 6: 10 monedas
  - Día 7: 25 monedas + 1 power-up raro
- [ ] Detección de "primer login del día" (no spam)
- [ ] Tracking en `SaveManager`: racha actual, racha máxima, último día reclamado, último login
- [ ] Si pasa más de 1 día sin login, racha rota (current=0, mostrar mensaje al volver)
- [ ] Streak Shield (50 monedas) — opcional para fase posterior

### Referencias
- GDD sección 7.2 (Racha diaria)
- GDD sección 10.3 Pantalla 5 (Daily Rewards)

---

## [Phase 2] UI polish con assets propios

✅ **PARCIALMENTE COMPLETADO** — ya no son placeholders genéricos: fuentes Fredoka (SDF) importadas, sprites propios exportados y en uso (panels, botón "+", íconos de HUD).

**Labels:** `phase-2`, `polish`, `ui`, `priority-medium`, `size-L`

### Descripción
Pasar de UI con placeholders a UI con assets reales: botones con frames decorativos, fonts propias, íconos de HUD, animaciones de transición entre pantallas. Bastante avanzado ya en la parte de HUD/settings; falta el resto de las pantallas (Santuario, Shop, etc. — no existen todavía).

### Acceptance criteria
- [x] Fuente Fredoka (SDF, variable) importada y en uso (`Assets/Fonts/fredoka/`)
- [x] Sprites propios de HUD: `panel_top/bottom.png`, `pill_panel.png`, `button_plus.png`, íconos en `Sprites/UI/Icons/` y `Sprites/UI/Letters/`
- [x] `ButtonPop.cs` — feedback de scale-bounce + sonido + haptic al tocar, usado en todos los botones
- [x] Transiciones entre escenas con fade + animación de burbujas (`SceneTransition.cs`)
- [ ] Botón primary/secondary con estilo definitivo (gradient, border radius) — verificar contra `08_Arte_Assets_Specs.md`
- [ ] Íconos UI restantes: profile, daily, shop, battle pass — no existen todavía porque esas pantallas no existen
- [ ] Popups con scale-in animation — `UIPanel.cs` ya tiene open/close animado, verificar que todos los popups lo usen

### Referencias
- `docs/03_Wireframes_Coralia.md`, `docs/08_Arte_Assets_Specs.md` (specs vigentes)
- GDD sección 11.3 (Tipografías)

---

## [Phase 2] Santuario (pantalla principal del juego)

**Labels:** `phase-2`, `feat`, `ui`, `narrative`, `priority-high`, `size-XL`

### Descripción
Implementar la pantalla Santuario que es la pantalla principal del juego (GDD sección 5). Vista panorámica del arrecife con criaturas rescatadas nadando idle. Botón JUGAR. Acceso a Shop, Battle Pass, Daily, Settings, Profile, etc. **Estado actual: existe `HomeGame.unity`/`HomeGame.cs` pero es un lobby mínimo** — solo tiene botón Jugar y música de fondo, ninguna de las features del Santuario del GDD. Definir si se expande esta escena o se arma una nueva.

### Acceptance criteria
- [ ] Decidir: ¿`HomeGame` se convierte en el Santuario, o es una escena separada?
- [ ] Background del arrecife con animación leve
- [ ] Criaturas rescatadas (de save de criaturas) aparecen nadando idle
- [ ] HUD top: monedas y vidas con countdown — reutilizar `ResourcePillView` ya construido
- [ ] HUD top-left: Settings icon (ya existe como `OpenSettingsButton` en el TopPanel de LevelMap, evaluar si se replica acá)
- [ ] HUD top-right: Profile icon
- [ ] HUD top-center: Events banner (si hay evento activo)
- [ ] Botón JUGAR grande centrado → Level Map (ya existe este link vía `HomeGame.OnPlay()`)
- [ ] Acceso rápido bottom: Shop, Battle Pass, Daily Rewards
- [ ] Indicador de racha visible
- [ ] Pull-to-refresh para actualizar estado

### Referencias
- GDD sección 5 (Meta-juego: el Santuario)
- GDD sección 10.3 Pantalla 4
- `Scripts/Home/HomeGame.cs` (punto de partida actual)
- `Scripts/UI/ResourcePillView.cs` (HUD reutilizable)

---

## [Phase 2] Level Select (mapa de niveles tipo Candy Crush)

✅ **COMPLETADO** — implementado como `LevelMap.unity` con `LevelMapController`, `LevelNodeView`, `ScrollPinController`.

**Labels:** `phase-2`, `feat`, `ui`, `priority-high`, `size-L`

### Descripción
~~Mapa serpenteante vertical con scroll~~ Ya implementado: path de perlas con curva Bezier, nodos circulares, estados locked/open/done dinámicos, `PlayerCard`/`AvatarDisplay` en el nodo actual con animación de pulse/ripple.

### Acceptance criteria
- [x] Escena `Scenes/Game/LevelMap.unity`
- [x] Mapa con path de perlas (Bezier) y nodos circulares — `LevelMapController.cs`, `LevelNodeView.cs`
- [x] Scroll vertical fluido con pin — `ScrollPinController.cs`
- [x] `PlayerCard`/`AvatarDisplay` marcando el nodo actual con pulse/ripple
- [x] TopPanel con HUD de recursos (`ResourcePillLives`/`ResourcePillCoins`) y botón Settings
- [ ] Estados de nodos: verificar que completado/actual/bloqueado tengan el tratamiento visual final (candado, criatura, pulsante) — validar contra wireframes
- [ ] Tap en nivel desbloqueado → Pre-level (Pre-level no existe todavía, hoy probablemente salta directo a Gameplay que tampoco existe)
- [ ] Mejor score de cada nivel visible
- [ ] Decoración temática según capítulo

### Referencias
- GDD sección 10.3 Pantalla 12 (Level Select)
- `Scripts/LevelMap/LevelMapController.cs`, `ScrollPinController.cs`, `LevelNodeView.cs`

---

## [Phase 2] Pre-level screen con selección de power-ups

**Labels:** `phase-2`, `feat`, `ui`, `gameplay`, `priority-medium`, `size-M`

### Descripción
Pantalla intermedia entre level select y gameplay donde el jugador ve la criatura a rescatar, los disparos disponibles, y equipa hasta 3 power-ups antes de empezar. No implementada todavía.

### Acceptance criteria
- [ ] Escena `PreLevel.unity`
- [ ] Imagen y nombre de la criatura a rescatar (si rescue)
- [ ] Diálogo característico de la criatura
- [ ] Counter de disparos disponibles
- [ ] 3 slots equipables de power-ups
- [ ] Tap en slot vacío → bottom sheet con power-ups disponibles + costo
- [ ] Botón "🎬 Power-up gratis" (rewarded ad placeholder)
- [ ] Botón JUGAR grande
- [ ] Si vidas = 0: popup "Sin vidas" con opciones

### Referencias
- GDD sección 10.3 Pantalla 13 (Pre-level)
- GDD sección 3.3 (Activación de power-ups)

---

## [Phase 2] Cloud save con Firebase Auth + Firestore

**Labels:** `phase-2`, `feat`, `infra`, `priority-low`, `size-XL`

### Descripción
Sincronizar el save local (`PlayerPrefs` vía `SaveManager`) con Firebase Firestore. Anonymous auth al primer abrir, opción de vincular cuenta (Apple ID / Google / Facebook). Sin esto el jugador pierde progreso al cambiar de dispositivo. No implementado todavía.

### Acceptance criteria
- [ ] Firebase Unity SDK instalado y configurado
- [ ] `google-services.json` y `GoogleService-Info.plist` agregados (en `.gitignore` — regla ya está en CLAUDE.md)
- [ ] Anonymous auth al primer abrir
- [ ] Auto-sync del save a Firestore cada 60s o on critical events (level win, IAP)
- [ ] Resolución de conflictos: si cloud > local, prevalece cloud (con prompt)
- [ ] Botón "Vincular cuenta" en Settings → Cuenta y asistencia (Apple/Google/Facebook OAuth)
- [ ] Restore al cambiar de device

### Referencias
- GDD sección 14.5 (Save format) y 14.6 (Servicios Firebase)
- `Scripts/Core/SaveManager.cs` (base de PlayerPrefs a sincronizar)

---

## [Phase 2] Ads: AdMob rewarded + interstitial

**Labels:** `phase-2`, `feat`, `monetization`, `priority-medium`, `size-L`

### Descripción
Integrar AdMob (Google Mobile Ads Unity SDK) con AppLovin MAX como mediación. Implementar los 5 placements de rewarded ads + 1 interstitial según GDD sección 9.2. No implementado todavía.

### Acceptance criteria
- [ ] Google Mobile Ads Unity SDK instalado
- [ ] AppLovin MAX configurado como mediación
- [ ] Test ads en development (NUNCA con AdMob real durante desarrollo)
- [ ] Rewarded placements implementados:
  - Vida extra (3/día)
  - Continuar nivel (5/día) → +5 disparos
  - Duplicar recompensa (10/día) → x2 al final del nivel
  - Daily chest extra (1/día)
  - Power-up gratis pre-level (3/día)
- [ ] Interstitial entre niveles (1 cada 3 niveles ganados)
- [ ] Caps diarios respetados con clase `AdsManager` (C#, estática — sigue el patrón de `AudioManager`/`LocaleManager`)
- [ ] Anti-fatigue: si ignora 5 ads consecutivos, suspender 24h
- [ ] Compliance: ATT en iOS al primer abrir
- [ ] NO ads durante gameplay activo

### Referencias
- GDD sección 9.2 (Anuncios)

---

## [Phase 2] IAP: integrar RevenueCat con productos del Shop

**Labels:** `phase-2`, `feat`, `monetization`, `priority-medium`, `size-L`

### Descripción
Integrar RevenueCat (cross-platform IAP) con los 6 packs de monedas + Starter Pack + Battle Pass según GDD sección 6.5. Configurar productos en App Store Connect y Google Play Console. No implementado todavía.

### Acceptance criteria
- [ ] RevenueCat Unity SDK configurado
- [ ] Productos definidos en RevenueCat dashboard:
  - Burbujita ($0.99 / 80 monedas)
  - Concha ($4.99 / 450 monedas)
  - Coral ($9.99 / 1000 monedas)
  - Tesoro ($19.99 / 2200)
  - Perla Real ($49.99 / 6000)
  - Cofre Mítico ($99.99 / 13000)
  - Starter Pack ($2.99) — 7 días desde install, 1 sola vez
  - Battle Pass S1 ($4.99)
- [ ] Pantalla Shop implementada con tabs (monedas, vidas, power-ups, especiales) — reutilizar `ResourcePillView` para el header de balance
- [ ] Botón "Restaurar compras" en Settings → Cuenta y asistencia funcional
- [ ] Validación server-side de recibos
- [ ] Tracking de IAP history en `SaveManager`

### Referencias
- GDD sección 6.5 (IAP packs)
- GDD sección 10.3 Pantalla 7 (Shop)
- `Scripts/UI/ResourcePillView.cs` (header de balance reutilizable)

---

## [Phase 2] Battle Pass v1 con free + premium tracks

**Labels:** `phase-2`, `feat`, `monetization`, `priority-medium`, `size-XL`

### Descripción
Implementar el Battle Pass de 30 días con 40 tiers, dos tracks (free y premium $4.99). No implementado todavía.

### Acceptance criteria
- [ ] Pantalla Battle Pass según GDD 10.3 Pantalla 6
- [ ] Sistema de XP que trackea XP por acción (50 por nivel ganado, etc. según GDD 8.3)
- [ ] 40 tiers con recompensas free + premium para temporada 1 "Despertar del Coral"
- [ ] Premium track elimina ads durante 30 días
- [ ] Botón comprar premium $4.99 (vía RevenueCat) o 800 monedas
- [ ] Hero image y branding de la temporada
- [ ] Countdown de días restantes
- [ ] Reset al final de la temporada con auto-start de la siguiente
- [ ] Notificación push al inicio de temporada nueva

### Referencias
- GDD sección 8 completa (Battle Pass)

---

# Fase 1 — Cleanup pendiente

## [Phase 1] Wireframes detallados en Figma

**Labels:** `phase-1`, `chore`, `ui`, `priority-medium`, `size-L`

### Descripción
La especificación de los wireframes está completa en `docs/03_Wireframes_Coralia.md` con framework + 17 pantallas detalladas. Falta ejecutar el diseño visual en Figma siguiendo ese spec — o, dado que ahora se está exportando arte real directo a Unity (ver `08_Arte_Assets_Specs.md`), evaluar si este paso sigue siendo necesario o se saltea yendo directo a producción de assets.

### Acceptance criteria
- [ ] Decidir si este paso sigue vigente o se reemplaza por el flujo actual (export directo a `design/exported/` → Unity)
- [ ] Si sigue vigente: archivo Figma del proyecto creado, framework con estilos/tipografías/componentes, 17 pantallas dibujadas

### Referencias
- `docs/03_Wireframes_Coralia.md` (spec completo)
- `docs/08_Arte_Assets_Specs.md` (flujo de producción actual)

---

## [Phase 1] Validation playtest informal con 1-3 testers

**Labels:** `phase-1`, `chore`, `priority-low`, `size-S`

### Descripción
Antes del global launch hay que hacer al menos un playtest informal para validar diversión con audiencia objetivo. El loop de juego ya está completo, así que esto ya se puede hacer.

### Acceptance criteria
- [ ] Build standalone (macOS o mobile) de Coralia con gameplay funcional compartido a 1-3 personas
- [ ] Sesiones grabadas (con permiso) o notas detalladas
- [ ] `docs/06_Playtest_Results.md` creado con resumen
- [ ] Decisión documentada: avanzar / iterar / replantear

### Referencias
- `docs/05_Playtest_Guide_Coralia.md`
- `docs/templates/playtest_*.md`

---

# Decisiones administrativas pendientes

## [Admin] Verificar disponibilidad de "Coralia" como marca

**Labels:** `chore`, `priority-medium`, `size-S`

### Descripción
Antes de invertir en arte y publicar en stores, validar que el nombre "Coralia" está disponible en App Store, Google Play, dominios y redes sociales. Si está tomado, decidir nombre alternativo. Sin cambios — sigue pendiente.

### Acceptance criteria
- [ ] App Store: search "Coralia" — si hay otra app con ese nombre, evaluar diferenciación
- [ ] Google Play: idem
- [ ] Dominios: `coralia.app`, `coralia.com`, `coraliagame.com` — verificar disponibilidad
- [ ] Redes: Instagram @coraliagame (o similar), TikTok, X — verificar disponibilidad
- [ ] Decisión documentada: usar Coralia o alternativa

---

## [Admin] Setup de cuentas de developer y backend

**Labels:** `chore`, `infra`, `priority-medium`, `size-S`

### Descripción
Antes del soft launch necesitás cuentas de developer y backend configuradas. Sin cambios — sigue pendiente.

### Acceptance criteria
- [ ] Apple Developer Program activado ($99/año)
- [ ] Google Play Console activado ($25 una vez)
- [ ] Firebase project creado (free tier)
- [ ] AdMob app registrada
- [ ] AppLovin MAX cuenta creada
- [ ] RevenueCat cuenta creada
- [ ] GitHub repo del proyecto privado creado y push del código — el repo local sigue sin remoto configurado

---

# Fase 3 — Soft Launch (backlog)

## [Phase 3] Soft launch en 2-3 países

**Labels:** `phase-3`, `chore`, `priority-low`, `size-XL`

### Descripción
Lanzar Coralia en 2-3 países pequeños (Filipinas, Colombia, México por ejemplo) para iterar con data real antes del lanzamiento global. 6-8 semanas de tracking de KPIs.

### Acceptance criteria
- [ ] Build production firmado con keystore propio
- [ ] Submission a Google Play Open Beta en países seleccionados
- [ ] Submission a Apple App Store en países seleccionados
- [ ] Analytics configurado (Firebase + GameAnalytics)
- [ ] Crashlytics activo
- [ ] Marketing budget asignado ($300-2000) para campañas low-CPI
- [ ] Tracking de KPIs: D1/D7/D30 retention, ARPDAU, conversion rate, eCPM
- [ ] Iteración semanal basada en data: ajustar drops, dificultad, ofertas
- [ ] Decisión documentada al fin de soft launch: ir a global / iterar más

### Referencias
- GDD sección 15 (Analytics)
- Plan Maestro Parte 6 (Roadmap)

---

# Fase 4 — Global Launch (backlog)

## [Phase 4] Global launch + ASO + marketing

**Labels:** `phase-4`, `chore`, `priority-low`, `size-XL`

### Descripción
Lanzamiento mundial coordinado tras validar con soft launch. Pulido final basado en data, ASO optimizado, campañas de marketing en Facebook/TikTok/Google Ads.

### Acceptance criteria
- [ ] ASO optimizado: keywords, screenshots por idioma (6), video promocional, descripción
- [ ] App Store y Google Play en todos los mercados objetivo
- [ ] Compliance ATT, GDPR, CCPA, LGPD verificado
- [ ] Marketing budget asignado ($1000-10000) para global launch
- [ ] Press kit preparado para creadores
- [ ] Métricas D1/D7/D30 monitoreadas semanalmente
- [ ] Plan de LiveOps activo (Battle Pass mensual, 5-10 niveles/semana, eventos)

---

# Backlog post-MVP (largo plazo)

## [Design] Recalibrar los precios de la economía tras eliminar las gemas

**Labels:** `design`, `economy`, `priority-medium`, `size-M`

### Descripción

Las gemas se eliminaron (GDD 6.3, actualizado 2026-10-02): el código nunca tuvo una segunda
moneda y todo quedó en `SaveManager.Coins`. Al hacerlo, las cifras del GDD se quedaron de dos
escalas incompatibles: la de monedas decía **200 por vida** y la de gemas **25 por la misma
vida**, y la implementada no es ninguna de las dos — es **4 por vida**.

Hoy sobreviven tres precios reales, y son el único anclaje válido:

| Compra | Precio | Dónde |
|---|---|---|
| Refill de las 5 vidas | 20 (4 por vida que falte) | `RefillLivesPanel` |
| Vidas infinitas 3 h | 47 | `RefillLivesPanel` |
| Continuar el nivel (+5 disparos) | 10, +5 por reintento | `NoMoreMovesPanel` |

Y hay un agujero del que depende todo lo demás: **no existe ninguna forma de ganar monedas**.
`GameplayController.CalculateAwards` devuelve una lista vacía a propósito (una sola moneda no
puede llover por jugar Y trabar las ofertas a la vez), y las fuentes previstas —santuario,
dailies, misiones, logros— no están implementadas. El único saldo posible es el inicial de 50.

### A decidir
- [ ] Precio de cada uno de los seis power-ups, contra los tres anclajes de arriba
- [ ] Qué fuentes de monedas se implementan primero, y cuánto dan
- [ ] Rehacer las cantidades de los packs IAP (GDD 6.5): están escritas en gemas, ~5× la escala actual
- [ ] Rehacer los montos de recompensas de retención y Battle Pass (GDD 7 y 8), que perdieron su
      componente en gemas al consolidar

### Acceptance criteria
- [ ] GDD 6.3 con la tabla completa de precios, sin "por definir"
- [ ] Al menos una fuente de monedas implementada, para que los precios signifiquen algo
- [ ] Los avisos de "sin recalibrar" de GDD 6.5, 6.6, 6.8, 7 y 8 retirados

---

## [Design] ✅ RESUELTO — Las dos clases de booster

**Labels:** `design`, `gameplay`, `done`

Resuelto el 2026-10-03. La decisión está escrita en **GDD §3.2**: diez power-ups en dos familias,
cinco de inicio y cinco de gameplay, repartidos por una sola regla — **si el jugador tiene que
elegir dónde o cuándo, es de gameplay; si se aplica solo, es de inicio.**

El cap del GDD §3.5 pasa a ser **3 de inicio equipados, y todos los de gameplay desbloqueados**:
los de inicio se pagan por partida y hay que elegir; los de gameplay solo cuestan si los usas, así
que limitarlos no protege de nada.

La implementación de cada uno vive en los issues de abajo.

---

## [Feature] Promociones de boosters (2x1 y similares) activables

**Labels:** `feature`, `economy`, `liveops`, `priority-low`, `size-M`

### Descripción

Ofertas puntuales sobre los packs de booster — 2x1, descuento extra, pack doble — que se puedan
**encender y apagar sin tocar código ni publicar una build**.

Hoy `BoosterCatalog.Pack` ya tiene `price` y `discountPercent`, y el precio final se calcula solo
(`Pack.Discounted`). Una promoción es, en esencia, un segundo descuento encima o una cantidad
distinta por el mismo precio. Lo que falta es **la ventana**: cuándo empieza, cuándo acaba y quién
lo decide.

Esto encaja en la cadencia de ofertas del GDD 6.10, que ya prevé Weekend Deals y Flash Sales. La
diferencia es que aquella tabla habla de packs de moneda y esto es sobre boosters concretos.

### A decidir
- [ ] **Qué activa la promoción.** Tres niveles, de menos a más trabajo:
      un check en el catálogo (lo enciende Diego y hay que publicar build);
      una fecha de inicio y fin en el catálogo (se programa por adelantado, sigue necesitando build);
      Firebase Remote Config (se enciende sin publicar — lo correcto a futuro, pero Firebase no
      está integrado, ver GDD 14.6).
- [ ] **Qué forma tiene la promoción.** 2x1 es "misma cantidad, doble de unidades", que no es lo
      mismo que un descuento: hay que decidir si se modela como un pack alternativo o como un
      multiplicador sobre el existente.
- [ ] **Cómo se anuncia.** Hoy la etiqueta del `RefillBoosterItemView` muestra el `discountPercent`.
      Un 2x1 no es un porcentaje, así que o se traduce a uno (50%) o la tarjeta necesita otra marca.
- [ ] **Si aplica a todos los boosters o a uno.** Una promo sobre la Bomba vende distinto que una
      sobre todo el catálogo.

### Acceptance criteria
- [ ] La promoción se enciende y se apaga sin recompilar
- [ ] El precio cobrado y el anunciado salen del mismo cálculo, como ahora
- [ ] `RefillBoosterPanel` muestra la oferta y se refresca cuando cambia
- [ ] Al terminar la ventana, los precios vuelven solos a los del catálogo

### Dependencias
Bloqueado de hecho por el issue de recalibración de precios: sin precios base cerrados, un
descuento sobre ellos no significa nada.

---

## [Feature] Rescate — multi-rescate, objetos y la cadena hasta el santuario

**Labels:** `feature`, `gameplay`, `narrative`, `priority-medium`, `size-L`

### Descripción

El objetivo `rescue` **está implementado**: el JSON del nivel declara `creature_position` y
`creature_id`, la burbuja marcada lleva su icono, y el nivel se gana cuando esa celda desaparece —
por match directo o por caída, sin flag aparte.

Lo que falta es todo lo que el GDD construye encima:

| Qué | Estado | Dónde |
|---|---|---|
| Rescate simple | ✅ implementado | `GameplayController`, `BubbleView.SetCreatureMarker` |
| **Multi-rescate** (3-5 criaturas en un nivel) | sin implementar | GDD 2.2, previsto para el nivel 31 |
| **Criaturas con identidad** (12 hero, bestiario) | sin implementar | GDD 5.3 — ver su propio issue |
| **Que la criatura llegue al santuario** | sin implementar | GDD 5.2. Hoy se rescata y no va a ninguna parte |
| **Objetos rescatables** (no criaturas) | **no está en el GDD** | pedido nuevo de Diego |

### Lo que hay que decidir primero

**Los objetos son una ampliación del diseño, no una tarea pendiente.** El GDD solo contempla
criaturas, y el rescate es la mecánica narrativa central —cada una se une al santuario, GDD 2.2—.
Un objeto que se rescata necesita responder a dónde va y qué significa, o compite con la criatura
sin aportar historia.

- [ ] ¿Qué es un objeto rescatable y para qué sirve? (¿decoración del santuario? ¿moneda?
      ¿fragmento coleccionable?)
- [ ] ¿Se rescata igual que una criatura —liberar la burbuja— o de otra forma?
- [ ] ¿Convive con la criatura en el mismo nivel o son objetivos excluyentes?
- [ ] Multi-rescate: ¿se gana al liberar todas, o cada una suma a un contador?

### Acceptance criteria
- [ ] `ObjectiveData` admite varias posiciones en vez de una
- [ ] El HUD muestra cuántas quedan cuando hay más de una
- [ ] Lo rescatado se registra en `SaveManager` y sobrevive al nivel
- [ ] GDD 2.2 actualizado con lo que se decida sobre los objetos

---

## [Feature] Obstáculos (6 tipos) — el campo del JSON existe y no hace nada

**Labels:** `feature`, `gameplay`, `level-design`, `priority-medium`, `size-XL`

### Descripción

`LevelData.obstacles` existe desde el principio y **está vacío en los 30 niveles**. El GDD 2.3 ya
define los seis tipos y en qué nivel entra cada uno, pero no hay nada implementado: ni el parseo,
ni el comportamiento, ni el arte.

| Obstáculo | Comportamiento (GDD 2.3) | Nivel |
|---|---|---|
| **Burbuja de hielo** | Dos impactos: el primero rompe el hielo, el segundo la burbuja | 11 |
| **Jaula de coral** | Encierra una criatura. Se rompe con un match adyacente | 16 |
| **Burbuja pegajosa** | No cae aunque pierda la conexión al techo. Solo muere por match directo | 21 |
| **Generador de algas** | Cada 5 disparos produce una burbuja nueva en una posición fija | 26 |
| **Burbuja-bomba** | — | 36 |
| **Cadena viva** | Dos burbujas unidas. Solo se rompen las dos a la vez | 46 |

El **crustáceo pegado** que menciona Diego encaja con la burbuja pegajosa, pero como criatura en vez
de como textura: un percebe agarrado a la burbuja explica por qué esa no cae, y es más del mundo
del juego que "una burbuja pegajosa". Decidir si es un renombrado o un obstáculo aparte.

### Por qué es XL

Cada obstáculo toca sitios distintos del núcleo, y varios rompen supuestos que hoy están dados por
hechos:

- **El hielo** necesita vida por burbuja. Hoy una burbuja se quita o no se quita.
- **La pegajosa** rompe `FindUnreachableFromCeiling`: la caída es una propiedad de grafo recalculada
  desde cero, y esto introduce excepciones por celda.
- **El generador** añade burbujas a mitad de partida, con lo que el grid deja de ser algo que solo
  mengua — afecta al scroll de retirada y a la condición de derrota.
- **La cadena** une dos celdas, y hoy cada celda es independiente.

Conviene **partirlo en un issue por obstáculo** y empezar por el hielo, que es el que menos
supuestos rompe y además es el primero que ve el jugador.

### A decidir
- [ ] Formato en el JSON: `obstacles` es `List<string>` y ninguno de estos cabe en un string suelto
      (hace falta al menos tipo + posición)
- [ ] Si la burbuja-bomba del nivel 36 es un obstáculo o una variante de la Bomba de Coral — el GDD
      no describe su comportamiento
- [ ] El crustáceo: renombrado de la pegajosa, o séptimo tipo

### Acceptance criteria
- [ ] `LevelData` modela los obstáculos con tipo y posición
- [ ] Un issue por obstáculo, cada uno con su tutorial y su arte
- [ ] El skill `level-designer` documenta el formato nuevo

---

## [Feature] Power-ups de inicio (4 restantes) — se aplican solos al empezar el nivel

**Labels:** `feature`, `gameplay`, `economy`, `priority-medium`, `size-L`

### Descripción

Los cuatro que faltan de la familia "de inicio" (GDD §3.2). Se eligen en la pantalla previa —que
ya existe y funciona, `StartGamePanel` + `BoosterItemView`— y se aplican al abrir el nivel, sin
pedir ninguna decisión durante la partida. Cada uno toca una palanca distinta.

La **Perla Arcoíris** ya está implementada y es la plantilla: `GameplayController.ApplyStartBoosters`
recorre lo equipado, consume el que sea de inicio y lo aplica. Un poder nuevo de esta familia solo
añade su caso a ese `switch`.

| Power-up | Efecto | Dónde se engancha |
|---|---|---|
| **Reserva de Oxígeno** | +5 al límite de disparos del nivel | `GameplayController._shotsRemaining`, antes del primer tiro |
| ~~**Perla Arcoíris**~~ ✅ | Al menos 3 comodines repartidos por el nivel, el primero al empezar | Hecho — `CannonController.MakeCurrentRainbow` + el reparto en `RollColor` |
| **Marea Baja** | Al empezar, borra la fila más baja del tablero | `GridController` + `CollapseFloating` y la animación de pop que ya existen |
| **Agua Clara** | El color con menos burbujas se CONVIERTE al color con más, y deja de salir del cañón | `ColorsOnGrid` + `BubbleView.SetColor` (ya existe para la arcoíris) |
| **Corriente Favorable** | Durante todo el nivel, el cañón favorece los colores que más abundan | sesgar `CannonController.RollColor` |

**Agua Clara convierte, no elimina** — borrar dispararía la caída de todo lo que colgara de esas
burbujas, un efecto que nadie previó al balancear el nivel, y resolvería parte del objetivo solo.
Convirtiendo, el racimo grande lo revienta el jugador con su disparo.

### Acceptance criteria
- [ ] Los cuatro en el enum `Booster` (con valor explícito, añadidos AL FINAL) y en `BoosterRules.NAMES`
- [ ] Entrada en `BoosterCatalog` con `Family = Start`, icono, packs y clip
- [ ] `ui.booster.X.name` y `.description` en los 6 idiomas
- [ ] Un tutorial por cada uno, con su id = el nombre del booster
- [ ] Su caso en el `switch` de `GameplayController.ApplyStartBoosters`
- [ ] El cap de 3 equipados se respeta en la pantalla previa

### Dependencias
Ninguna: el camino entero está abierto desde la Perla. `BoosterLoadout` guarda lo equipado,
`StartGamePanel` lo reparte y `ApplyStartBoosters` lo consume al abrir el nivel.

---

## [Feature] Power-ups de gameplay (3 restantes) — se activan durante la partida

**Labels:** `feature`, `gameplay`, `priority-medium`, `size-L`

### Descripción

Los tres que faltan de la familia "de gameplay" (GDD §3.2). Hay **dos** implementados que sirven
de plantilla, y entre los dos cubren casi todo lo que un poder nuevo puede necesitar:

- **Bomba de Coral** — el camino base: enum `Booster`, `SpecialBubbleSkin`, la marca de zona en
  `TrajectoryLine`, el `BoosterButton` del HUD y el consumo en `CannonController.Fire`.
- **Raya Eléctrica** — el poder que probó que el camino aguanta uno nuevo sin tocarse. Entró sin
  modificar `BoosterHudList`, `SpecialBubbleSkin`, `BoosterButton`, `BoosterItemView` ni
  `RefillBoosterPanel`.

| Power-up | Efecto | Qué reusa |
|---|---|---|
| ~~**Raya Eléctrica**~~ ✅ | Barre entera la fila horizontal que golpea | Hecho |
| **Pez Explorador** | Al impactar se divide en tres: revienta la burbuja tocada y las dos de ese color **con menos vecinos del mismo color** | `CellsHitBy` + una búsqueda por el grid |
| **Tinta de Pulpo** | El pulpo salpica 4 burbujas al azar dentro del radio, tiñéndolas del **color que vuelve a la recámara** | `HexGridMath.CellsWithinRadius` + `BubbleView.SetColor` |
| **Pinza de Langosta** | Tocas una burbuja del tablero y la destruye. No gasta disparo | **Nada: necesita una pieza nueva** |

**Dos de los tres son variaciones del mismo camino** — se cargan en el cañón, se apuntan, y lo
único que cambia es qué le pasa al grid al impactar. La Pinza es la única que pide un modo de
selección sobre el tablero, que no existe.

### Lo que la Raya Eléctrica dejó montado

Cambios transversales que un poder nuevo hereda gratis. Están aquí para que no se reimplementen:

- **`BoosterRules.CellsHitBy(booster, landed, struck)`** — un poder declara QUÉ CELDAS se lleva, y
  de ahí salen a la vez la marca de la mira, el orden de los pops y la explosión. No hay forma de
  que la mira prometa una cosa y pase otra.
- **`struck` es la burbuja golpeada, que no es donde se posa.** Disparando desde abajo la burbuja
  se pega una fila por debajo de la que tocó. Todo poder cuya zona dependa de a qué apuntaba el
  jugador tiene que resolver sobre `struck`, no sobre la celda de aterrizaje.
- **La burbuja del propio poder siempre se va con él** (`hit.Insert(0, center)`), aunque caiga
  fuera de la zona que afecta. Sin eso se quedaba colgando y caía al fondo en vez de estallar.
- **`HexGridMath.CellsInRowFrom(row, fromCol)`** — una fila ordenada desde el impacto hacia los dos
  lados. Quien escalone por el índice obtiene el efecto en abanico sin pedirlo.
- **`Entry.burstPerBubble`** — en vez de una onda desde el centro, cada burbuja pone la suya al
  ritmo de su propio pop. Para cualquier poder que barra en línea.
- **`Lightning`** — una descarga que recorre una fila, tramo a tramo, con texturas procedurales.
- **`SpecialBubbleSkin.Spark`** — el chisporroteo de dentro de la burbuja, también procedural: dos
  brazos independientes y un destello con su propio latido. Sin arte que exportar.
- **`BombBlast.Settings.waveCount` llega a 0** — para un poder que ya trae su efecto y al que los
  aros solo le estorban.

Dos detalles que no son opcionales:

- **El Pez va a las aisladas, no a las cercanas.** Yendo a las próximas sería una bomba pequeña y
  peor; yendo a las que menos vecinos de su color tienen, ataca lo que el jugador no puede
  resolver solo.
- **La Tinta tiñe del color que vuelve a la recámara, no del disparado.** Con el disparado, los 4
  teñidos más la recién pegada hacen match al instante: un efecto, no una jugada. Y como ese color
  queda tapado por el pulpo, **la marca de zona de la mira se pinta de él** — lo que de paso cierra
  el mismo hueco que la bomba tiene hoy.

### Acceptance criteria
- [ ] Los tres en el enum `Booster` (con valor explícito, añadidos AL FINAL) y en `BoosterRules.NAMES`
- [ ] Entrada en `BoosterCatalog` con `Family = In Game`, icono, frames, packs y clips
- [ ] Un `BoosterButton` por poder en `Gameplay.unity` — el HUD no los instancia, los enciende
- [ ] `ui.booster.X.name` y `.description` en los 6 idiomas
- [ ] Un tutorial por cada uno, con su id = el nombre del booster
- [ ] La marca de zona de la mira refleja el efecto real de cada uno
- [ ] Modo de selección sobre el tablero para la Pinza
- [ ] Todos aparecen en el HUD vía `BoosterHudList` sin tocar ese script

---

## [Backlog] 60 niveles totales (de 30 a 60) para MVP

**Labels:** `backlog`, `feat`, `levels`, `priority-medium`, `size-XL`

### Descripción
Completar los 60 niveles del MVP siguiendo la curva del GDD sección 2.4. Estos son los 6 capítulos × 10 niveles — hoy hay 30 (capítulos 1-3). Estrategia híbrida hand + AI.

### Acceptance criteria
- [ ] 30 niveles más (capítulos 4, 5 y 6 en `Resources/Levels/`)
- [ ] Capítulos según GDD 2.1: Cala Apagada (1-10, ✅), Jardín de Anémonas (11-20, ✅), Bosque de Algas (21-30, ✅), Cueva de Cristales (31-40), Profundidades de Coral (41-50), Ciudad de las Perlas (51-60)
- [ ] Curva difícil con walls de pago en 35, 45, 55
- [ ] Variedad de objetivos (rescue, clear_all, color_count, drop_creature, multi_rescue)
- [ ] Obstáculos progresivos (hielo, jaulas, pegajosas, generadores, bombas)
- [ ] Solver script que valida solubilidad de cada nivel

### Referencias
- GDD sección 2 completa

---

## [Backlog] 12 criaturas hero con personalidades, diálogos y bestiario

**Labels:** `backlog`, `feat`, `narrative`, `art`, `priority-medium`, `size-L`

### Descripción
Implementar las 12 criaturas hero del MVP según GDD sección 13.3 con sus personalidades, diálogos, animaciones idle, y entradas de bestiario.

### Acceptance criteria
- [ ] 12 sprites de criaturas con animación idle (Coquí, Burbujín, Lúa, Caracol, Espina, Aletita, Glissa, Chispín, Lumi, Marino, Iris, Perla)
- [ ] Diálogos en 6 idiomas en CSV
- [ ] Bestiario en Profile screen con info por criatura
- [ ] Recompensa pasiva por criatura en santuario (monedas/hora según tier)
- [ ] La Sombra Profunda (antagonista) implementada con apariciones progresivas

### Referencias
- GDD sección 13 completa (Narrativa y personajes)

---

## [Backlog] Achievements (40 logros)

**Labels:** `backlog`, `feat`, `retention`, `priority-low`, `size-L`

### Descripción
Implementar los 40 logros del GDD sección 7.5 con 3 tiers (bronce, plata, oro). Cada logro otorga recompensa.

### Acceptance criteria
- [ ] 40 logros implementados (12 bronce + 18 plata + 10 oro)
- [ ] Categorías: progresión, coleccionismo, skill, restauración, generosidad, eficiencia, constancia
- [ ] Detección automática de logros desbloqueados
- [ ] Pantalla Profile muestra grid 4×4 con barras de progreso
- [ ] Recompensas otorgadas al desbloquear (monedas, power-ups, skins)
- [ ] Notificación de logro desbloqueado durante gameplay (toast)

### Referencias
- GDD sección 7.5 (Logros)

---

## [Backlog] Daily missions (3/día) y Weekly missions (5/semana)

**Labels:** `backlog`, `feat`, `retention`, `priority-medium`, `size-M`

### Descripción
Sistema de misiones diarias (3 que resetean cada 24h) y semanales (5 que resetean cada lunes) según GDD secciones 7.3 y 7.4.

### Acceptance criteria
- [ ] Pool de ~20 templates de misiones diarias
- [ ] Pool de templates de misiones semanales
- [ ] Selección automática al reset
- [ ] Recompensas según GDD (monedas, power-ups, BP XP)
- [ ] Bonus por completar todas las del día/semana
- [ ] UI accesible desde santuario

### Referencias
- GDD sección 7.3 y 7.4

---

## [Backlog] Sistema social: friends + send/receive lives + sanctuary visits

**Labels:** `backlog`, `feat`, `social`, `priority-low`, `size-XL`

### Descripción
Pilar #3 de retención. Conectar via Facebook / Apple ID, friends list, enviar/recibir vidas, visitar santuario de amigos.

### Acceptance criteria
- [ ] OAuth con Facebook, Apple ID, Google
- [ ] Friends list importada desde Facebook + búsqueda por código de amigo
- [ ] Enviar vida (cap 5/día), pedir vida (cap 5/día)
- [ ] Visitar santuario de amigo (read-only)
- [ ] Botón "dejar regalo" → notificación al amigo + 50 monedas para vos
- [ ] Compartir victoria en redes con imagen generada

### Referencias
- GDD sección 7.9 (Sistema social)

---

## [Backlog] Eventos temporales (Festival de Coral, Luna Llena, etc.)

**Labels:** `backlog`, `feat`, `retention`, `priority-low`, `size-L`

### Descripción
1-2 eventos pequeños/mes + estacionales grandes. Mecánicas y estructura según GDD sección 7.8.

### Acceptance criteria
- [ ] Festival de Coral (mensual, 5 días)
- [ ] Luna Llena Submarina (3 días, drop rates duplicados)
- [ ] Marea de Coleccionables (7 días, criatura mítica única)
- [ ] Holiday Events (Halloween, Navidad, Verano, Año Nuevo)
- [ ] Cada evento con su propio leaderboard, objetivos, paquete de recompensas
- [ ] Boost del evento ($1.99-$4.99) opcional para acelerar progreso

### Referencias
- GDD sección 7.8 (Eventos temporales)

---

## [Backlog] Solver automático de niveles para validación

**Labels:** `backlog`, `tech`, `infra`, `priority-medium`, `size-L`

### Descripción
Script que simula N partidas de un nivel para validar solubilidad y estimar dificultad real. Crítico para el flujo de generación de niveles AI-assisted. Puede vivir fuera de Unity (Python standalone leyendo los JSON de `Resources/Levels/`) o como Editor tool en C# — evaluar cuál es más simple para un solo dev.

### Acceptance criteria
- [ ] Script que valida un nivel JSON (Python standalone o Unity Editor tool)
- [ ] Simula partidas con bot que dispara semi-aleatorio
- [ ] Reporta tasa de éxito y promedio de disparos óptimos
- [ ] Ejecutable en batch para validar todos los niveles de golpe
- [ ] Output formato CSV para análisis

### Referencias
- GDD sección 2.7 (Generación AI)

---

## [Backlog] Suscripción premium ($4.99/mes "Coralia Plus")

**Labels:** `backlog`, `feat`, `monetization`, `priority-low`, `size-L`

### Descripción
Lanzar suscripción mensual tras 3-6 meses post-launch (audiencia base estable). Beneficios: sin ads + 50 monedas/día + vidas infinitas + skin exclusiva mensual + early access a niveles. El HUD de vidas ya soporta el estado "infinito" (`ResourcePillView.SetBadgeInfinite()`), listo para cuando se implemente esto.

### Acceptance criteria
- [ ] Producto suscripción en RevenueCat ($4.99/mes o $39.99/año)
- [ ] Pantalla de pitch en Settings → Suscripción
- [ ] Beneficios activos mientras la sub está activa
- [ ] Manejo de cancelación / expiration
- [ ] Skin mensual rotativo
- [ ] Conectar vidas infinitas con `ResourcePillView.SetBadgeInfinite()` (ya implementado del lado visual)

### Referencias
- GDD sección 9.3 (Suscripción)
- Plan Maestro Capa 4
- `Scripts/UI/ResourcePillView.cs`

---

# Bugs y polish menor (registrar a medida que aparezcan)

## [Bug] Edge case: si grid queda con 1 burbuja huérfana, smart queue podría dar colores no matcheables

**Labels:** `bug`, `gameplay`, `priority-low`, `size-S`

### Descripción
En clear_all, si quedan ≥1 burbujas huérfanas que no pueden formar matches con ningún otro color en grid, el nivel se vuelve unwinnable. Viene del prototipo original. Hay que verificar si aplica al smart queue actual.

### Acceptance criteria
- [ ] Al implementar el smart queue en Unity, detectar caso "no hay matches posibles"
- [ ] Si detectado, ofrecer un "anti-stuck": mover bubbles aleatorias o regenerar grid
- [ ] O: nunca generar layouts con bubbles huérfanas (responsabilidad del nivel diseñado)

---

## [Polish] Sin ningún tipo de feedback visual al fallar un disparo (no match)

**Labels:** `polish`, `gameplay`, `ux`, `priority-low`, `size-S`

### Descripción
Cuando el jugador dispara y no matchea, la burbuja simplemente aterriza sin feedback. Algunos juegos hacen un pequeño "shake" o sonido suave. Considerar para mejorar feel. **No aplicable todavía: no hay gameplay en Unity.**

### Acceptance criteria
- [ ] Shake leve de la burbuja al aterrizar sin match
- [ ] Sonido suave distinto del pop de match (`AudioManager.PlayPop()` ya existe, falta el clip y la llamada)
- [ ] (Opcional) Indicador sutil de "near miss" si quedó cerca de un match

---

# Notas

- Este backlog está vivo. Agregá nuevos issues o reordená prioridades a medida que avances.
- Cada issue debe linkear al PR cuando se trabaje, y al commit cuando se cierre.
- Para issues grandes (XL), considerar dividir en sub-issues antes de empezar.
- Cuando crees el repo en GitHub, podrías importar este doc directamente con `gh issue create` por sección.
- **Mantenimiento:** la revisión 2026-05→2026-08 quedó 3 meses sin sincronizar con el código real (migración de motor incluida). Para que no vuelva a pasar: actualizar este doc y `07_Status_y_Roadmap.md` al cerrar cada chunk grande, o al menos correr `/graphify --update` + comparar contra el código antes de asumir que un issue sigue pendiente.
