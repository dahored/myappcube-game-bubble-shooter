# Backlog — GitHub Issues para Coralia

**Versión:** 0.1 — 2026-05-01
**Propósito:** este documento traduce el plan del proyecto a issues de GitHub. Cada sección H2 (`##`) es un issue independiente — copy-pasteable directo a "New Issue" en GitHub.

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

**Fase 1 — Prototipo:** ✅ Completada 2026-05-01 (Chunk 7 skipped — playtest pendiente)
**Fase 2 — MVP:** 🔄 En progreso. Chunk A (Persistencia) completado.

Los issues debajo están agrupados por fase y prioridad.

---

# Fase 2 — MVP (en progreso)

## [Phase 2] Audio: música + SFX placeholders

**Labels:** `phase-2`, `feat`, `audio`, `priority-high`, `size-M`

### Descripción
Aunque sea con placeholders gratuitos, agregar audio mejora 10x el feel del juego. Sin sonido el prototipo se siente vacío y poco profesional. Implementar `AudioManager` autoload (ya stubbed) con reproducción real, y agregar los SFX y música mínimos.

### Acceptance criteria
- [ ] Música de fondo loop en gameplay (1 track placeholder de Freesound o similar)
- [ ] SFX de pop de burbuja al hacer match (con variación leve de pitch)
- [ ] SFX de drop de flotantes
- [ ] SFX de victoria al completar nivel
- [ ] SFX de derrota al perder
- [ ] SFX de tap de botones (UI)
- [ ] `AudioManager` con 3 canales/buses configurables: `music`, `ui_fx`, `bubble_pop` (matching los 3 sliders del Settings que define el GDD)
- [ ] Volúmenes leídos de `SaveManager.data.settings` (ya están en el schema)
- [ ] Vibración (toggle) implementada con `Input.vibrate_handheld()` para mobile

### Referencias
- GDD sección 12 (Audio)
- GDD sección 12.4 (Mix y mastering)
- `scripts/autoloads/audio_manager.gd` ya tiene la estructura, falta implementación real

---

## [Phase 2] Primer power-up: Bomba de Coral

**Labels:** `phase-2`, `feat`, `gameplay`, `priority-medium`, `size-M`

### Descripción
Implementar el primer power-up del GDD: la Bomba de Coral. Explota una zona 3x3 (en hex grid: la celda + 6 vecinos hexagonales) alrededor del impacto. Es el power-up más simple y sirve para establecer el patrón de implementación de los otros 5.

### Acceptance criteria
- [ ] UI de selección de power-up en pantalla pre-level (slot equipable)
- [ ] Power-up consumible (1 uso por nivel)
- [ ] Activación: tap en ícono del power-up en HUD durante gameplay → próximo disparo es bomba
- [ ] Visual distintivo de la burbuja-bomba en el cañón (marcador rojo o ícono)
- [ ] Al impactar grid, explota celda + 6 hex vecinos
- [ ] Animación de explosión con partículas/shake
- [ ] Costo: 8 gemas (placeholder hasta implementar economía)
- [ ] Counter de power-ups disponibles guardado en SaveManager

### Referencias
- GDD sección 3.2 (Power-ups del MVP)
- GDD sección 3.3 (Activación)

---

## [Phase 2] Sistema de vidas (5 vidas, regen 30 min)

**Labels:** `phase-2`, `feat`, `economy`, `gameplay`, `priority-high`, `size-M`

### Descripción
Implementar el sistema de vidas que es base de la monetización F2P. 5 vidas máximo, una se regenera cada 30 minutos, se pierde al fallar un nivel. HUD muestra vidas actuales + countdown de la próxima.

### Acceptance criteria
- [ ] HUD muestra ❤️ N (con N = vidas actuales 0-5)
- [ ] Si N < 5, muestra countdown formato "MM:SS hasta próxima vida"
- [ ] Vidas almacenadas en SaveManager con `lives_last_regen` timestamp
- [ ] Al perder un nivel, decrementa vidas en 1
- [ ] Al ganar un nivel, NO consume vida
- [ ] Si vidas = 0 al intentar jugar nivel: popup "Sin vidas" con opciones (esperar / pagar gemas / ver ad — placeholders por ahora)
- [ ] Cálculo de regen: cada 30 min real desde `lives_last_regen`, hasta cap de 5
- [ ] Persiste correctamente al cerrar/reabrir el juego

### Referencias
- GDD sección 6.2 (Sistema de vidas)
- `EconomyManager` autoload ya tiene `consume_life()`, `add_life()` placeholders

---

## [Phase 2] Sistema de monedas y gemas con drops por nivel

**Labels:** `phase-2`, `feat`, `economy`, `priority-high`, `size-M`

### Descripción
Implementar el sistema dual de monedas (soft) + gemas (hard) según GDD sección 6. Display en HUD. Drops automáticos al completar niveles. Sin compras IAP todavía (eso viene en chunk separado).

### Acceptance criteria
- [ ] HUD top-left muestra `🪙 X    💎 Y` (monedas y gemas)
- [ ] Drops por nivel completado:
  - 50-100 monedas según capítulo (GDD 6.6)
  - 1-3 gemas con probabilidad ~30%
  - +50% bonus en monedas para primera completación de un nivel
- [ ] Animación de drop de currencies al final del nivel (caen del modal a los HUD counters)
- [ ] Persistencia en SaveManager
- [ ] Tracking de "primera completación" por nivel para bonus

### Referencias
- GDD sección 6.3 (Monedas)
- GDD sección 6.4 (Gemas)
- GDD sección 6.6 (Drops por nivel)

---

## [Phase 2] Más niveles: subir de 5 a 20+ con AI gen

**Labels:** `phase-2`, `feat`, `levels`, `priority-high`, `size-L`

### Descripción
Pasar de 5 niveles del prototipo a 20+ niveles con curva de dificultad coherente. Usar Claude para generar borradores de niveles 6-25 según GDD sección 2.4 (curva de dificultad). Cada nivel se valida manualmente — solver script + playtest del propio dev.

### Acceptance criteria
- [ ] 15+ niveles nuevos en `data/levels/` (006.json a 020.json o más)
- [ ] Curva: niveles 6-10 fáciles intro, 11-20 medios con primeros obstáculos
- [ ] Variedad en tipos de objetivos: rescue, clear_all, color_count
- [ ] Variedad en posiciones de criatura (top, middle, deep) y columnas
- [ ] Cada nivel verificado como ganable por el dev
- [ ] Botón Next del HUD funciona hasta el último nivel disponible
- [ ] LevelManager.get_total_levels() refleja el nuevo conteo

### Referencias
- GDD sección 2.4 (Curva de dificultad)
- GDD sección 2.7 (Estrategia híbrida hand + AI)

---

## [Phase 2] Localización activada en runtime

**Labels:** `phase-2`, `feat`, `i18n`, `priority-medium`, `size-M`

### Descripción
Activar la localización i18n: cargar `localization/translations.csv` (ya existe con 50+ keys), reemplazar strings hardcodeados en UI por keys (`tr("ui.button.play")`), permitir cambiar idioma en runtime, persistir preferencia.

### Acceptance criteria
- [ ] Re-activar `locale/translations` en `project.godot` (comentado actualmente)
- [ ] Setup correcto del CSV import en Godot 4 (Force Compress, Loader=Translations)
- [ ] LocaleManager autoload usa `TranslationServer.set_locale()` correctamente
- [ ] Todos los strings de HUD reemplazados por `tr()` keys (Score, Disparos, Objetivo, etc.)
- [ ] Selector de idioma funcional (provisional: botón debug que cicla entre los 6)
- [ ] Cambio de idioma en runtime sin reiniciar
- [ ] Preferencia guardada en SaveManager.settings.language
- [ ] Por idioma del SO al primer abrir el juego

### Referencias
- GDD sección 12.2bis (Localización)
- `localization/translations.csv` con keys starter
- `LocaleManager` autoload con stubs

---

## [Phase 2] Onboarding tutorial (los 3 pasos del GDD)

**Labels:** `phase-2`, `feat`, `ui`, `priority-medium`, `size-M`

### Descripción
Implementar el tutorial interactivo de 3 pasos descrito en GDD sección 10.3 Pantalla 3 (Onboarding). Solo se ejecuta una vez por jugador (flag `tutorial_completed` en save). Bocadillos de Marina + puntero animado.

### Acceptance criteria
- [ ] Escena `scenes/main/onboarding.tscn`
- [ ] Pasos 1-3 según GDD: apuntar, soltar, explicar match
- [ ] Bocadillos de Marina con texto via i18n (keys nuevas en CSV)
- [ ] Puntero animado / flecha que indica la acción
- [ ] Botón "Saltar tutorial" oculto los primeros 2s, después aparece con fade
- [ ] Al completar paso 3, marca `tutorial_completed = true` y va a Santuario (o Gameplay temporalmente)
- [ ] Boot.gd respeta `tutorial_completed`: si false, va a onboarding; si true, va a santuario/gameplay

### Referencias
- GDD sección 10.3 Pantalla 3 (Onboarding)
- `boot.gd` ya tiene el TODO para el routing

---

## [Phase 2] Daily reward y streak (racha de 7 días)

**Labels:** `phase-2`, `feat`, `retention`, `priority-medium`, `size-M`

### Descripción
Implementar la racha diaria con loop de 7 días según GDD sección 7.2. Pop-up al primer login del día con la recompensa correspondiente. Indicador de racha en HUD del santuario.

### Acceptance criteria
- [ ] Pantalla 5 (Daily Rewards) según GDD sección 10.3
- [ ] Recompensas día 1-7 según GDD 7.2:
  - Día 1: 50 monedas
  - Día 2: 100 monedas
  - Día 3: 5 gemas
  - Día 4: 1 power-up aleatorio
  - Día 5: 200 monedas + 1 vida
  - Día 6: 10 gemas
  - Día 7: 25 gemas + 1 power-up raro
- [ ] Detección de "primer login del día" (no spam)
- [ ] Tracking en SaveManager: `streak.current`, `streak.longest`, `streak.last_claim_day`, `streak.last_login_timestamp`
- [ ] Si pasa más de 1 día sin login, racha rota (current=0, mostrar mensaje al volver)
- [ ] Streak Shield (50 gemas) — opcional para fase posterior

### Referencias
- GDD sección 7.2 (Racha diaria)
- GDD sección 10.3 Pantalla 5 (Daily Rewards)

---

## [Phase 2] UI polish con assets placeholder profesionales

**Labels:** `phase-2`, `polish`, `ui`, `priority-medium`, `size-L`

### Descripción
Pasar de UI con `_draw()` y placeholders a UI con primer pase de assets reales: botones con frames decorativos, fonts Quicksand y Nunito (Google Fonts), iconos de Phosphor o similar, animaciones de transiciones entre pantallas.

### Acceptance criteria
- [ ] Fonts Quicksand Bold (títulos) y Nunito (body) cargadas en theme global
- [ ] Botón primary: gradient coral pink, border radius 32px (según wireframes framework)
- [ ] Botón secondary: blanco con borde coral
- [ ] Iconos UI: settings, profile, daily, shop, battle pass
- [ ] Theme único de Godot que se aplica al proyecto (no overrides individuales)
- [ ] Transición de scenes con slide horizontal 300ms ease-out
- [ ] Popups con scale-in animation

### Referencias
- `docs/03_Wireframes_Coralia.md` Framework section
- GDD sección 11.3 (Tipografías)

---

## [Phase 2] Santuario (pantalla principal del juego)

**Labels:** `phase-2`, `feat`, `ui`, `narrative`, `priority-high`, `size-XL`

### Descripción
Implementar la Pantalla 4 (Santuario) que es la pantalla principal del juego. Vista panorámica del arrecife con criaturas rescatadas nadando idle. Botón JUGAR. Acceso a Shop, Battle Pass, Daily, Settings, Profile, etc.

### Acceptance criteria
- [ ] Escena `scenes/santuario/santuario.tscn`
- [ ] Background del arrecife con animación leve
- [ ] Criaturas rescatadas (de SaveManager.creatures_rescued) aparecen nadando idle
- [ ] HUD top: monedas, gemas, vidas con countdown
- [ ] HUD top-left: Settings icon
- [ ] HUD top-right: Profile icon
- [ ] HUD top-center: Events banner (si hay evento activo)
- [ ] Botón JUGAR grande centrado → Level Select
- [ ] Acceso rápido bottom: Shop, Battle Pass, Daily Rewards
- [ ] Indicador de racha visible
- [ ] Pull-to-refresh para actualizar estado

### Referencias
- GDD sección 5 (Meta-juego: el Santuario)
- GDD sección 10.3 Pantalla 4

---

## [Phase 2] Level Select (mapa de niveles tipo Candy Crush)

**Labels:** `phase-2`, `feat`, `ui`, `priority-high`, `size-L`

### Descripción
Mapa serpenteante vertical con scroll que muestra todos los niveles. Niveles desbloqueados navegables. Niveles bloqueados con candado. Niveles ganados con criatura rescatada.

### Acceptance criteria
- [ ] Escena `scenes/ui/level_select.tscn`
- [ ] Mapa serpenteante con nodos
- [ ] Estados de nodos: completado (verde + criatura), actual (pulsante coral), bloqueado (gris + candado)
- [ ] Tap en nivel desbloqueado → Pre-level
- [ ] Tap en nivel bloqueado → tooltip "Completá el anterior"
- [ ] Mejor score de cada nivel visible
- [ ] Scroll vertical fluido
- [ ] Decoración temática según capítulo

### Referencias
- GDD sección 10.3 Pantalla 12 (Level Select)
- Wireframes layout en `docs/03_Wireframes_Coralia.md`

---

## [Phase 2] Pre-level screen con selección de power-ups

**Labels:** `phase-2`, `feat`, `ui`, `gameplay`, `priority-medium`, `size-M`

### Descripción
Pantalla intermedia entre level select y gameplay donde el jugador ve la criatura a rescatar, los disparos disponibles, y equipa hasta 3 power-ups antes de empezar.

### Acceptance criteria
- [ ] Escena `scenes/ui/pre_level.tscn`
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
Sincronizar el save local con Firebase Firestore. Anonymous auth al primer abrir, opción de vincular cuenta (Apple ID / Google / Facebook). Sin esto el jugador pierde progreso al cambiar de dispositivo.

### Acceptance criteria
- [ ] Plugin de Firebase para Godot 4 instalado y configurado
- [ ] `google-services.json` y `GoogleService-Info.plist` agregados (en .gitignore)
- [ ] Anonymous auth al primer abrir
- [ ] Auto-sync del save a Firestore cada 60s o on critical events (level win, IAP)
- [ ] Resolución de conflictos: si cloud > local, prevalece cloud (con prompt)
- [ ] Botón "Vincular cuenta" en Settings → Profile (Apple/Google/Facebook OAuth)
- [ ] Restore al cambiar de device

### Referencias
- GDD sección 14.5 (Save format) y 14.6 (Servicios Firebase)
- `FirebaseManager` autoload con stubs

---

## [Phase 2] Ads: AdMob rewarded + interstitial

**Labels:** `phase-2`, `feat`, `monetization`, `priority-medium`, `size-L`

### Descripción
Integrar AdMob (vía plugin Godot) con AppLovin MAX como mediación. Implementar los 5 placements de rewarded ads + 1 interstitial según GDD sección 9.2.

### Acceptance criteria
- [ ] Plugin AdMob de Godot instalado
- [ ] AppLovin MAX configurado como mediación
- [ ] Test ads en development (NUNCA con AdMob real durante desarrollo)
- [ ] Rewarded placements implementados:
  - Vida extra (3/día)
  - Continuar nivel (5/día) → +5 disparos
  - Duplicar recompensa (10/día) → x2 al final del nivel
  - Daily chest extra (1/día)
  - Power-up gratis pre-level (3/día)
- [ ] Interstitial entre niveles (1 cada 3 niveles ganados)
- [ ] Caps diarios respetados con `AdsManager`
- [ ] Anti-fatigue: si ignora 5 ads consecutivos, suspender 24h
- [ ] Compliance: ATT en iOS al primer abrir
- [ ] NO ads durante gameplay activo

### Referencias
- GDD sección 9.2 (Anuncios)
- `AdsManager` autoload con stubs

---

## [Phase 2] IAP: integrar RevenueCat con productos del Shop

**Labels:** `phase-2`, `feat`, `monetization`, `priority-medium`, `size-L`

### Descripción
Integrar RevenueCat (cross-platform IAP) con los 6 packs de gemas + Starter Pack + Battle Pass según GDD sección 6.5. Configurar productos en App Store Connect y Google Play Console.

### Acceptance criteria
- [ ] RevenueCat SDK configurado
- [ ] Productos definidos en RevenueCat dashboard:
  - Burbujita ($0.99 / 80 gemas)
  - Concha ($4.99 / 450 gemas)
  - Coral ($9.99 / 1000 gemas)
  - Tesoro ($19.99 / 2200)
  - Perla Real ($49.99 / 6000)
  - Cofre Mítico ($99.99 / 13000)
  - Starter Pack ($2.99) — 7 días desde install, 1 sola vez
  - Battle Pass S1 ($4.99)
- [ ] Pantalla 7 (Shop) implementada con tabs (gemas, vidas, power-ups, especiales)
- [ ] Botón "Restaurar compras" en Settings funcional
- [ ] Validación server-side de recibos
- [ ] Tracking de IAP history en SaveManager

### Referencias
- GDD sección 6.5 (IAP packs)
- GDD sección 10.3 Pantalla 7 (Shop)
- `IAPManager` autoload con stubs

---

## [Phase 2] Battle Pass v1 con free + premium tracks

**Labels:** `phase-2`, `feat`, `monetization`, `priority-medium`, `size-XL`

### Descripción
Implementar el Battle Pass de 30 días con 40 tiers, dos tracks (free y premium $4.99). Dado que es una temporada continua, este chunk requiere setup de la primera temporada y todo el sistema.

### Acceptance criteria
- [ ] Pantalla 6 (Battle Pass) según GDD 10.3
- [ ] Sistema de XP que tracker XP por acción (50 por nivel ganado, etc. según GDD 8.3)
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
La especificación de los wireframes está completa en `docs/03_Wireframes_Coralia.md` con framework + 17 pantallas detalladas. Falta ejecutar el diseño visual en Figma siguiendo ese spec.

### Acceptance criteria
- [ ] Archivo Figma del proyecto creado
- [ ] Framework de Figma con: estilos de color, tipografías, componentes (btn_primary, btn_secondary, popup_container, hud_currency, etc.)
- [ ] 17 pantallas dibujadas en Figma siguiendo los layouts ASCII del spec
- [ ] Versión Light Mode (Modo Arrecife) y Dark Mode (Modo Profundidades)
- [ ] Link al Figma agregado a README.md

### Referencias
- `docs/03_Wireframes_Coralia.md` (spec completo)

---

## [Phase 1] Validation playtest informal con 1-3 testers

**Labels:** `phase-1`, `chore`, `priority-low`, `size-S`

### Descripción
Aunque saltamos Chunk 7 oficialmente, antes del global launch DEBEMOS hacer al menos un playtest informal para validar diversión con audiencia objetivo. La guía y formularios ya están armados.

### Acceptance criteria
- [ ] Build standalone macOS de Coralia compartido a 1-3 personas
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
Antes de invertir en arte y publicar en stores, validar que el nombre "Coralia" está disponible en App Store, Google Play, dominios y redes sociales. Si está tomado, decidir nombre alternativo.

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
Antes del soft launch necesitás:

### Acceptance criteria
- [ ] Apple Developer Program activado ($99/año)
- [ ] Google Play Console activado ($25 una vez)
- [ ] Firebase project creado (free tier)
- [ ] AdMob app registrada
- [ ] AppLovin MAX cuenta creada
- [ ] RevenueCat cuenta creada
- [ ] GitHub repo del proyecto privado creado y push del código

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

## [Backlog] 60 niveles totales (de 20 a 60) para MVP

**Labels:** `backlog`, `feat`, `levels`, `priority-medium`, `size-XL`

### Descripción
Completar los 60 niveles del MVP siguiendo la curva del GDD sección 2.4. Estos son los 6 capítulos × 10 niveles. Estrategia híbrida hand + AI.

### Acceptance criteria
- [ ] 40 niveles más (021 a 060)
- [ ] Capítulos según GDD 2.1: Cala Apagada (1-10), Jardín de Anémonas (11-20), Bosque de Algas (21-30), Cueva de Cristales (31-40), Profundidades de Coral (41-50), Ciudad de las Perlas (51-60)
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
- [ ] Pantalla 9 (Profile) muestra grid 4×4 con barras de progreso
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
Script que simula N partidas de un nivel para validar solubilidad y estimar dificultad real. Crítico para el flujo de generación de niveles AI-assisted.

### Acceptance criteria
- [ ] Script en Godot que carga un nivel JSON
- [ ] Simula 1000 partidas con bot que dispara semi-aleatorio
- [ ] Reporta tasa de éxito y promedio de disparos óptimos
- [ ] Ejecutable desde CLI para validar batch de niveles
- [ ] Output formato CSV para análisis

### Referencias
- GDD sección 2.7 (Generación AI)

---

## [Backlog] Suscripción premium ($4.99/mes "Coralia Plus")

**Labels:** `backlog`, `feat`, `monetization`, `priority-low`, `size-L`

### Descripción
Lanzar suscripción mensual tras 3-6 meses post-launch (audiencia base estable). Beneficios: sin ads + 50 gemas/día + vidas infinitas + skin exclusiva mensual + early access a niveles.

### Acceptance criteria
- [ ] Producto suscripción en RevenueCat ($4.99/mes o $39.99/año)
- [ ] Pantalla de pitch en Settings → Suscripción
- [ ] Beneficios activos mientras la sub está activa
- [ ] Manejo de cancelación / expiration
- [ ] Skin mensual rotativo

### Referencias
- GDD sección 9.3 (Suscripción)
- Plan Maestro Capa 4

---

# Bugs y polish menor (registrar a medida que aparezcan)

## [Bug] Edge case: si grid queda con 1 burbuja huérfana, smart queue podría dar colores no matcheables

**Labels:** `bug`, `gameplay`, `priority-low`, `size-S`

### Descripción
En clear_all, si quedan ≥1 burbujas huérfanas que no pueden formar matches con ningún otro color en grid, el nivel se vuelve unwinnable. El smart queue actual tira fallback random cuando no hay colores con 2+ instancias, pero eso no resuelve la unsolvability.

### Acceptance criteria
- [ ] Detectar caso "no hay matches posibles" en gameplay.gd
- [ ] Si detectado, ofrecer un "anti-stuck": mover bubbles aleatorias o regenerar grid
- [ ] O: nunca generar layouts con bubbles huérfanas (responsabilidad del nivel diseñado)

---

## [Polish] Sin ningún tipo de feedback visual al fallar un disparo (no match)

**Labels:** `polish`, `gameplay`, `ux`, `priority-low`, `size-S`

### Descripción
Cuando el jugador dispara y no matchea, la burbuja simplemente aterriza sin feedback. Algunos juegos hacen un pequeño "shake" o sonido suave. Considerar para mejorar feel.

### Acceptance criteria
- [ ] Shake leve de la burbuja al aterrizar sin match
- [ ] Sonido suave distinto del pop de match
- [ ] (Opcional) Indicador sutil de "near miss" si quedó cerca de un match

---

# Notas

- Este backlog está vivo. Agregá nuevos issues o reordená prioridades a medida que avances.
- Cada issue debe linkear al PR cuando se trabaje, y al commit cuando se cierre.
- Para issues grandes (XL), considerar dividir en sub-issues antes de empezar.
- Cuando crees el repo en GitHub, podrías importar este doc directamente con `gh issue create` por sección.
