# Coralia — Status y Roadmap

**Última actualización:** 2026-08-15

Documento maestro de estado del proyecto. Si abrís uno solo de los docs, **abrí este**.

⚠️ Este doc estuvo desactualizado (Defold) desde 2026-05-06 hasta hoy — el proyecto migró a **Unity 6** en mayo 2026 y siguió avanzando sin que nadie lo volcara acá. Esta versión refleja el código real a la fecha de arriba.

---

## Estado actual

| Fase | Estado | Notas |
|---|---|---|
| **Fase 0** — Pre-producción | ✅ Completada | Concept, GDD v0.6, Wireframes spec, Plan Fase 1, setup técnico |
| **Fase 1** — Prototipo jugable | ✅ Completada | Mecánicas core validadas en Godot 4 (referencia, no se porta 1:1) |
| **Fase 2** — MVP | 🔄 En progreso | Motor: **Unity 6**. Splash, Home, Level Map, Settings y HUD superior implementados. Gameplay (cañón/grid/match) todavía no existe. |
| **Fase 3** — Soft Launch | ⏳ Pendiente | Tras MVP completo |
| **Fase 4** — Global Launch | ⏳ Pendiente | Tras soft launch validado |

**Historial de motor:** Godot 4 (prototipo, abril-mayo 2026) → Defold (migración breve, no llegó a tener gameplay) → **Unity 6** (`fb0a6da`, mayo 2026, motor actual). Si ves referencias a Defold o `.gd`/`.lua` en cualquier doc, son históricas.

---

## Qué tenemos hoy

**Proyecto Unity 6 (URP)**, portrait 1080×1920, C# sin namespace.

### Infraestructura base ✅
- Managers estáticos (no MonoBehaviour singleton): `SaveManager` (PlayerPrefs), `LocaleManager` (6 idiomas), `AudioManager`, `SceneLoader` + `SceneTransition`
- `SceneTransition` se auto-instancia (`DontDestroyOnLoad`) — fade + animación de burbujas entre escenas
- Haptics vía plugin `MOST_HapticFeedback` (bridge nativo Android/iOS)

### Pantallas implementadas ✅
- **Splash Studio + Splash Game**: logo estudio → logo juego + loading
- **HomeGame**: lobby mínimo — botón Jugar → Level Map + música de fondo. (Ojo: esto es más simple que el "Santuario" del GDD sección 5 — no tiene criaturas nadando, shop, battle pass, etc. todavía. Evaluar si el Santuario se construye sobre esta escena o es una pantalla nueva.)
- **Level Map**: `LevelMapController` + `LevelNodeView` + `ScrollPinController` — path de perlas Bezier, nodos circulares, estados locked/open/done, `PlayerCard`/`AvatarDisplay` en el nodo actual
- **TopPanel (HUD superior)**: en progreso — `TopPanelController` (padding de safe area) + `ResourcePillView` (componente reutilizable coins/vidas: ícono, valor, timer, badge, botón "+"). Ver detalle abajo.
- **Settings**: completo — 4 secciones (Preferencias / Cuenta y asistencia / Comunidad / Legal), 3 sliders de audio, toggle de vibración, dropdown de idioma

### HUD — ResourcePillView (nuevo, 2026-08) 🔄
Componente genérico para mostrar recursos (coins, vidas) en un pill con ícono + valor + botón "+":
- `Assets/Scripts/UI/ResourcePillView.cs` — API: `SetIcon`, `SetValue`, `SetFull`, `SetTimer`, `SetInfinite`, `SetBadge`/`SetBadgeInfinite`/`HideBadge`, evento `OnPlusClicked`
- Prefab base `ResourcePill.prefab` + variants `ResourcePillCoins Variant` / `ResourcePillLives Variant` en `Prefabs/UI/game/`
- Sprites propios exportados: `panel_top/bottom.png`, `pill_panel.png`, `button_plus.png`, íconos en `Sprites/UI/Icons/` y `Sprites/UI/Letters/` (incluye `infinite_letter` para el badge de vidas infinitas)
- **Pendiente:** todavía muestra valores hardcodeados en el Editor (`999999`) — no hay `SaveManager.Coins`/`SaveManager.Lives` ni gameplay que los modifique, así que no tiene sentido conectarlo a datos reales todavía. Se conecta cuando exista el sistema de vidas/economía (ver backlog).

### Datos y assets ✅
- Niveles en `Resources/Levels/Chapter_1/2/3/*.json` → deserializados a `LevelData.cs` (reemplaza el viejo `data/levels/*.json` de Godot)
- `Resources/translations.csv` — 6 idiomas, cargado por `LocaleManager`
- Fuentes Fredoka (SDF) + Quicksand/Nunito según specs de `08_Arte_Assets_Specs.md`
- Sprites UI exportados en `design/exported/` → importados a `coralia/Assets/Sprites/`

### Referencia de mecánicas (del prototipo Godot, sin portar)
Estas mecánicas fueron validadas en Godot 4 pero **no existen todavía en Unity** — `Scripts/Gameplay/` está vacío:
- Grid hexagonal, cañón con drag aim, smart queue, color shuffle, match detection (flood-fill), win/lose

---

## Lo que falta (priorizado por valor)

Ver `06_Backlog_GitHub_Issues.md` para detalles — ese doc también tiene secciones desactualizadas (Defold), usarlo para leer las acceptance criteria de diseño, no las notas técnicas de implementación.

### Top 5 cosas que más mejorarían el juego ahora mismo

1. **Gameplay en Unity** — cañón + grid hexagonal + match + win/lose. Sigue siendo el core faltante. `Scripts/Gameplay/` vacío.
2. **Sistema de vidas + monedas** — `SaveManager` no tiene esos campos todavía. El HUD (`ResourcePillView`) ya está listo del lado visual, solo falta la data real.
3. **Audio in-game** — `AudioManager` ya reproduce música de lobby; falta SFX de gameplay (pop, drop, win/lose) que dependen de que exista gameplay.
4. **Onboarding tutorial** — no implementado.
5. **Santuario real** — definir si `HomeGame` se expande a la pantalla completa del GDD sección 5 (criaturas, shop, battle pass) o si se arma como pantalla nueva.

### Lo que NO bloquea ahora pero es necesario para lanzar

- Cloud save (Firebase)
- Ads (AdMob + AppLovin MAX)
- IAP (RevenueCat)
- Battle Pass v1
- Asset pass real (reemplazar placeholders — en progreso, HUD ya tiene sprites propios)
- Verificar disponibilidad de "Coralia"
- Setup cuentas: Apple Dev, Google Play, Firebase, AdMob, RevenueCat

---

## Mapa de documentos

```
docs/
├── Plan_Maestro_Bubble_Shooter.docx     ← El plan original (immutable)
├── 01_Concepto_Inicial.md (v0.3)        ← Visión, decisiones lockeadas
├── 02_GDD_Coralia.md (v0.6)             ← Game Design Document (17 secciones)
├── 03_Wireframes_Coralia.md (v0.3)      ← Spec de las 17 pantallas
├── 04_Plan_Fase1_Coralia.md             ← Plan de Fase 1 (✅ completada, Godot)
├── 05_Playtest_Guide_Coralia.md         ← Guía para hacer playtest informal
├── 06_Backlog_GitHub_Issues.md          ← Backlog (⚠️ notas técnicas en Defold, desactualizado)
├── 07_Status_y_Roadmap.md (este doc)    ← Status general del proyecto
├── 08_Arte_Assets_Specs.md              ← Specs de producción de arte (vigente)
└── templates/
    ├── playtest_form_per_tester.md
    └── playtest_results_summary.md
```

Plus en root del repo: `CLAUDE.md` (contexto Unity actualizado 2026-08-11) y `CHANGELOG.md` (⚠️ solo cubre hasta Fase 1 Godot — los commits de Unity no están volcados ahí, usar `git log`).

---

## Mapa de código

```
coralia/
  Assets/
    Scenes/Splash/ Home/ Game/         ← SplashStudio, SplashGame, HomeGame, LevelMap
    Scripts/
      Core/        ← SaveManager, LocaleManager, AudioManager, SceneLoader, SceneTransition
      UI/          ← ButtonPop, UIPanel, SettingsToggle/Panel/Content, LocalizedText,
                      ResponsiveLayout, SafeAreaPanel, TopPanelController, ResourcePillView
      Home/        ← HomeGame.cs
      Splash/      ← SplashStudio.cs, SplashGame.cs
      LevelMap/    ← LevelMapController.cs, LevelNodeView.cs, ScrollPinController.cs
      Gameplay/    ← VACÍO — cañón/grid/match pendiente
      Data/        ← LevelData.cs
    Prefabs/UI/    ← buttons/ panels/ user/ game/ inputs/
    Resources/
      translations.csv
      Levels/Chapter_1/2/3/*.json
    Sprites/UI/    ← Buttons, Icons, Letters, Panels, Pines, Banners, Inputs
design/exported/    ← exports de diseño antes de importar a Unity
```

---

## Cómo continuar con Claude Code

### Workflow por issue

1. `git checkout -b issue-N-short-description`
2. Decirle a Claude Code el número de issue — lee el AC (validar contra código real, el backlog tiene partes desactualizadas)
3. Lee GDD sección relevante antes de implementar
4. Implementa en C# siguiendo convenciones de `CLAUDE.md`
5. Prueba en Unity Editor (Play mode)
6. Commit + push + PR → mergear + cerrar issue
7. Update CHANGELOG.md (no se viene haciendo desde la migración — retomar si Diego lo pide)

### Cadencia recomendada (revisada)

- **Ahora:** Gameplay Unity (cañón + grid + match) — sigue siendo el bloqueante mayor
- **Después:** Sistema de vidas + monedas (conecta con el HUD ya construido) + Audio in-game
- **Luego:** Onboarding tutorial + definir Santuario real
- **Más adelante:** Battle pass, ads/IAP, social

---

## Decisiones administrativas pendientes (sin código)

Sin cambios desde la última revisión — nada de esto se resolvió todavía:

1. **Verificar "Coralia" disponibilidad** (App Store, Google Play, dominios, redes)
2. **Setup de cuentas:** Apple Developer Program, Google Play Console, Firebase, AdMob, AppLovin MAX, RevenueCat
3. **Crear repo GitHub privado** (issue tracking + version control) — el repo local sigue sin remoto
4. **Comprar dominio** (coralia.app o similar)
5. **Crear cuentas de redes** (@coraliagame en Instagram, TikTok, X)

---

## Riesgos abiertos

1. **Sin playtest externo** — sigue siendo el riesgo más grande, ahora agravado porque tampoco hay gameplay jugable en Unity todavía para testear.
2. **Solo dev sin equipo de arte** — mitigado parcialmente: ya se está exportando arte propio (íconos HUD, panels, botones) en vez de placeholders genéricos.
3. **Sin balance económico validado** — sigue siendo hipótesis, sin datos.
4. **Tres migraciones de motor en un año** (Godot → Defold → Unity) — riesgo de que la documentación se vuelva a desincronizar del código si no se actualiza al cerrar cada chunk. Mitigación: usar `graphify` (`graphify-out/graph.json`) para detectar discrepancias doc↔código en vez de confiar ciegamente en los docs.
5. **Saturación del género** — sin cambios, la diferenciación sigue dependiendo del cozy submarino + narrativa + santuario.

---

## Cómo medir éxito

Sin cambios respecto a la versión anterior — ver GDD y Plan Maestro para el detalle de KPIs por fase (D1/D7/D30 retention, ARPDAU, conversion rate, LTV target $1.50-$3.50).
