# Backlog — GitHub Issues para Coralia

**Versión:** 0.2 — 2026-08-15 (revisión Unity — v0.1 era 2026-05-01, escrita para Defold)
**Propósito:** este documento traduce el plan del proyecto a issues de GitHub. Cada sección H2 (`##`) es un issue independiente — copy-pasteable directo a "New Issue" en GitHub.

⚠️ **Nota de esta revisión:** el proyecto migró de Defold a **Unity 6** en mayo 2026. Las acceptance criteria de diseño/producto de cada issue siguen siendo válidas — se actualizaron las referencias técnicas (nombres de módulo, rutas de archivo, APIs) que apuntaban a Defold/Lua. Los issues ya completados quedan marcados con **✅ COMPLETADO** debajo del título, con una nota de qué se hizo realmente.

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

**Fase 1 — Prototipo:** ✅ Completada en Godot 4 (2026-05-01). Referencia de diseño, no se portó código 1:1.
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
- [x] Vibración (toggle) implementada — vía plugin `MOST_HapticFeedback` (bridge nativo Android/iOS), no `sys.vibrate()` (eso era Defold)
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
- [ ] Costo: 8 gemas (placeholder hasta implementar economía)
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
- [ ] Si vidas = 0 al intentar jugar nivel: popup "Sin vidas" con opciones (esperar / pagar gemas / ver ad — placeholders por ahora)
- [ ] Cálculo de regen: cada 30 min real desde `LivesLastRegen`, hasta cap de 5 — conectar con `ResourcePillView.SetTimer()`
- [ ] Persiste correctamente al cerrar/reabrir el juego

### Referencias
- GDD sección 6.2 (Sistema de vidas)
- `Scripts/UI/ResourcePillView.cs` (HUD ya implementado)
- `Scripts/Core/SaveManager.cs` (agregar campos acá, sigue el patrón de propiedades existente)

---

## [Phase 2] Sistema de monedas y gemas con drops por nivel

**Labels:** `phase-2`, `feat`, `economy`, `priority-high`, `size-M`

### Descripción
Implementar el sistema dual de monedas (soft) + gemas (hard) según GDD sección 6. **El HUD de monedas ya está listo** (`ResourcePillCoins Variant.prefab`) mostrando un valor hardcodeado — falta conectar datos reales y los drops al completar niveles. Sin compras IAP todavía (eso viene en chunk separado).

### Acceptance criteria
- [x] HUD muestra el pill de monedas con ícono + valor + botón "+" — `ResourcePillCoins Variant.prefab`
- [ ] `SaveManager` no tiene campos de economía todavía — agregar `Coins` (int) y `Gems` (int)
- [ ] Drops por nivel completado (bloqueado por gameplay):
  - 50-100 monedas según capítulo (GDD 6.6)
  - 1-3 gemas con probabilidad ~30%
  - +50% bonus en monedas para primera completación de un nivel
- [ ] Animación de drop de currencies al final del nivel (caen del modal a los HUD counters)
- [ ] Persistencia en `SaveManager`
- [ ] Tracking de "primera completación" por nivel para bonus
- [ ] Botón "+" del pill de monedas (`ResourcePillView.OnPlusClicked`) abre la Shop — todavía no implementado

### Referencias
- GDD sección 6.3 (Monedas)
- GDD sección 6.4 (Gemas)
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
  - Día 3: 5 gemas
  - Día 4: 1 power-up aleatorio
  - Día 5: 200 monedas + 1 vida
  - Día 6: 10 gemas
  - Día 7: 25 gemas + 1 power-up raro
- [ ] Detección de "primer login del día" (no spam)
- [ ] Tracking en `SaveManager`: racha actual, racha máxima, último día reclamado, último login
- [ ] Si pasa más de 1 día sin login, racha rota (current=0, mostrar mensaje al volver)
- [ ] Streak Shield (50 gemas) — opcional para fase posterior

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
- [ ] HUD top: monedas, gemas, vidas con countdown — reutilizar `ResourcePillView` ya construido
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
Integrar RevenueCat (cross-platform IAP) con los 6 packs de gemas + Starter Pack + Battle Pass según GDD sección 6.5. Configurar productos en App Store Connect y Google Play Console. No implementado todavía.

### Acceptance criteria
- [ ] RevenueCat Unity SDK configurado
- [ ] Productos definidos en RevenueCat dashboard:
  - Burbujita ($0.99 / 80 gemas)
  - Concha ($4.99 / 450 gemas)
  - Coral ($9.99 / 1000 gemas)
  - Tesoro ($19.99 / 2200)
  - Perla Real ($49.99 / 6000)
  - Cofre Mítico ($99.99 / 13000)
  - Starter Pack ($2.99) — 7 días desde install, 1 sola vez
  - Battle Pass S1 ($4.99)
- [ ] Pantalla Shop implementada con tabs (gemas, vidas, power-ups, especiales) — reutilizar `ResourcePillView` para el header de balance
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
- [ ] Botón comprar premium $4.99 (vía RevenueCat) o 800 gemas
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
Antes del global launch hay que hacer al menos un playtest informal para validar diversión con audiencia objetivo. **Bloqueado hasta que exista gameplay jugable en Unity** — el playtest del prototipo Godot no cuenta porque el gameplay no se portó.

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

## [Backlog] Power-ups restantes (Rayo de Luz, Cambio de Color, Mira Láser, Pez Explorador, Burbuja de Aire)

**Labels:** `backlog`, `feat`, `gameplay`, `priority-low`, `size-L`

### Descripción
Después del primer power-up (Bomba de Coral), implementar los otros 5 según GDD sección 3.2.

### Acceptance criteria
- [ ] Rayo de luz: elimina columna entera (10 gemas)
- [ ] Cambio de color: cambia color del cañón al elegido (6 gemas)
- [ ] Mira láser: trayectoria con todos los rebotes durante 3 disparos (7 gemas)
- [ ] Pez explorador: pez nada por grid eliminando 5 burbujas del color elegido (12 gemas)
- [ ] Burbuja de aire: +1 disparo al límite del nivel (5 gemas)

### Referencias
- GDD sección 3.2

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
- [ ] Recompensas otorgadas al desbloquear (gemas, monedas, power-ups, skins)
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
- [ ] Recompensas según GDD (monedas, gemas, power-ups, BP XP)
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
Lanzar suscripción mensual tras 3-6 meses post-launch (audiencia base estable). Beneficios: sin ads + 50 gemas/día + vidas infinitas + skin exclusiva mensual + early access a niveles. El HUD de vidas ya soporta el estado "infinito" (`ResourcePillView.SetBadgeInfinite()`), listo para cuando se implemente esto.

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
En clear_all, si quedan ≥1 burbujas huérfanas que no pueden formar matches con ningún otro color en grid, el nivel se vuelve unwinnable. Este bug viene del prototipo Godot — hay que verificar si aplica de nuevo cuando se implemente el smart queue en Unity. **No aplicable todavía: no hay gameplay en Unity.**

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
