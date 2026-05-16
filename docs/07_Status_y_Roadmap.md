# Coralia — Status y Roadmap

**Última actualización:** 2026-05-06

Documento maestro de estado del proyecto. Si abrís uno solo de los docs, **abrí este**.

---

## Estado actual

| Fase | Estado | Notas |
|---|---|---|
| **Fase 0** — Pre-producción | ✅ Completada | Concept, GDD v0.6, Wireframes spec, Plan Fase 1, setup técnico |
| **Fase 1** — Prototipo jugable | ✅ Completada | Mecánicas core validadas (Godot). Migración a Defold completada 2026-05-06. |
| **Fase 2** — MVP | 🔄 En progreso | Base Defold lista (splash + level map). Gameplay pendiente. |
| **Fase 3** — Soft Launch | ⏳ Pendiente | Tras MVP completo |
| **Fase 4** — Global Launch | ⏳ Pendiente | Tras soft launch validado |

---

## Qué tenemos hoy

**Proyecto Defold limpio y funcional** (motor: Defold + Lua):

### Infraestructura base ✅
- `game.project` con display 1080x1920 portrait, bundles para iOS + Android
- Input binding (touch + back)
- Bootstrap: main.collection → routing por collection proxies
- 4 módulos reutilizables: config, router, save_manager, level_manager

### Pantallas implementadas ✅
- **Splash 1**: logo estudio (myappcube), fade in/out 2.7s, skip on tap
- **Splash 2**: logo juego (Coralia), versión dinámica desde game.project, dots animation, MIN_SHOW 2s
- **Level Map**: scroll vertical inverso (capítulos nuevos arriba), 4 columnas, estados locked/open/done dinámicos según progreso guardado

### Datos y assets ✅
- 20 niveles JSON (capítulo 1: lvl 1-10, capítulo 2: lvl 11-20)
- 7 bubbles idle PNGs (200x200) en bubbles.atlas
- logos.atlas (logo estudio + logo juego)
- coralia_ui.font (distance field, vera_mo_bd.ttf)
- translations.csv con 50+ keys en 6 idiomas — pendiente activar en Defold

### Referencia de mecánicas (del prototipo Godot)
Las siguientes mecánicas ya fueron validadas en Godot y están listas para portar a Defold:
- Grid hexagonal de 84 burbujas con físicas correctas
- Cañón con drag aim, trayectoria con primer rebote, cola de 2 burbujas
- Smart queue: solo da colores que existen en el grid
- Color shuffle al cargar nivel (posiciones fijas, colores random)
- Match detection (flood-fill BFS) y drops por gravedad
- Win/lose conditions con modales

---

## Lo que falta (priorizado por valor)

Ver `06_Backlog_GitHub_Issues.md` para detalles. Aquí el resumen ordenado por impacto:

### Top 5 cosas que más mejorarían el juego ahora mismo

1. **Gameplay Defold** — portar cañón + grid + match + HUD a Defold. Es el core. Estimación: 3-5 días.
2. **Audio** — placeholders gratuitos. Mejora 10x el feel. Estimación: 1-2 días.
3. **Sistema de vidas + monedas + gemas** — base de monetización. 2-3 días.
4. **Onboarding tutorial** — para que jugadores nuevos no se confundan. 2 días.
5. **Pantalla de Settings** — 4 secciones, 3 sliders de audio, vibración. 1-2 días.

### Lo que NO bloquea ahora pero es necesario para lanzar

- Cloud save (Firebase)
- Ads (AdMob + AppLovin MAX)
- IAP (RevenueCat)
- Battle Pass v1
- Asset pass real (reemplazar placeholders)
- Localización activa
- Verificar disponibilidad de "Coralia"
- Setup cuentas: Apple Dev, Google Play, Firebase, AdMob, RevenueCat

---

## Mapa de documentos

```
docs/
├── Plan_Maestro_Bubble_Shooter.docx     ← El plan original (immutable)
├── 01_Concepto_Inicial.md (v0.3)        ← Visión, decisiones lockeadas
├── 02_GDD_Coralia.md (v0.6)             ← Game Design Document (17 secciones)
├── 03_Wireframes_Coralia.md (v0.2)      ← Spec de las 17 pantallas
├── 04_Plan_Fase1_Coralia.md (v0.2)      ← Plan de Fase 1 (✅ completada)
├── 05_Playtest_Guide_Coralia.md (v0.1)  ← Guía para hacer playtest informal
├── 06_Backlog_GitHub_Issues.md (v0.1)   ← Backlog para crear issues en GitHub
├── 07_Status_y_Roadmap.md (este doc)    ← Status general del proyecto
└── templates/
    ├── playtest_form_per_tester.md
    └── playtest_results_summary.md
```

Plus en root del repo: `CHANGELOG.md` con historial de cambios por chunk.

---

## Mapa de código

```
coralia/
├── game.project               ← Config Defold (1080x1920 portrait, iOS+Android)
├── input/
│   └── game.input_binding     ← touch + back
├── main/                      ← Bootstrap: routing + collection proxies
│   ├── main.collection        ← socket "main"
│   ├── main.go                ← proxies de splash1, splash2, level_map
│   └── main.script            ← maneja "go_to" → disable/unload/async_load
├── splash1/                   ← socket "splash1" — logo estudio
├── splash2/                   ← socket "splash2" — logo juego + loading
├── level_map/                 ← socket "level_map" — mapa scrolleable
├── gameplay/                  ← (próximo) socket "gameplay" — partida
├── modules/
│   ├── config.lua             ← constantes (grid, física, colores, economía)
│   ├── router.lua             ← router.go(scene_name)
│   ├── save_manager.lua       ← sys.save / sys.load wrapper
│   └── level_manager.lua      ← carga + caché de niveles JSON
├── assets/
│   ├── atlas/                 ← logos.atlas, bubbles.atlas
│   ├── fonts/                 ← coralia_ui.font (vera_mo_bd.ttf distance field)
│   └── sprites/bubbles/       ← idle/ (7 PNGs) + v1/ (spritesheets originales)
├── data/levels/               ← 001-020.json (20 niveles, 2 capítulos)
└── localization/              ← translations.csv (6 idiomas, 50+ keys) — pendiente activar
```

---

## Cómo continuar con Claude Code

### Workflow por issue

Por cada issue de `06_Backlog_GitHub_Issues.md`:
1. `git checkout -b issue-N-short-description`
2. Decirle a Claude Code el número de issue — lee el AC
3. Lee GDD sección relevante antes de implementar
4. Implementa en Defold (Lua + .collection / .gui / .script)
5. Prueba en Defold desktop (Cmd+B)
6. Commit + push + PR → mergear + cerrar issue
7. Update CHANGELOG.md

### Setup de GitHub (pendiente)

```bash
gh repo create coralia --private --source=. --push
```

O manualmente: github.com → New Repo → push manual.

### Cadencia recomendada

- **Semana 1:** Gameplay Defold (cañón + grid + match)
- **Semana 2:** Audio + HUD
- **Semana 3:** Sistema de vidas + monedas
- **Semana 4:** Onboarding tutorial + Settings
- **Semana 5+:** Santuario, battle pass, ads/IAP...

Probable **6-10 semanas** para MVP completo.

---

## Decisiones administrativas pendientes (sin código)

Antes del soft launch hay que cerrar:

1. **Verificar "Coralia" disponibilidad** (App Store, Google Play, dominios, redes)
2. **Setup de cuentas:**
   - Apple Developer Program ($99/año)
   - Google Play Console ($25 una vez)
   - Firebase project (free tier)
   - AdMob, AppLovin MAX, RevenueCat
3. **Crear repo GitHub privado** (issue tracking + version control)
4. **Comprar dominio** (coralia.app o similar)
5. **Crear cuentas de redes** (@coraliagame en Instagram, TikTok, X)

Estos no requieren código pero son cuello de botella si los dejás para el final.

---

## Riesgos abiertos

1. **Sin playtest externo** — el riesgo más grande. Puede que el juego no sea tan divertido como pensamos. Mitigación: hacer playtest informal antes de invertir en arte. Guía en `docs/05_Playtest_Guide_Coralia.md`.

2. **Solo dev sin equipo de arte** — cuello de botella en Fase 2 cuando haya que reemplazar placeholders. Mitigación: estrategia híbrida AI gen + freelancer (definida en GDD sección 11.5, presupuesto ~$2000-4000).

3. **Sin balance económico validado** — los números de la economía (drops, costos, IAP packs) son hipótesis. Validar con datos reales en soft launch (GDD sección 6.10).

4. **Saturación del género** — Bubble Shooters hay muchos. La diferenciación viene del concepto cozy submarino + narrativa con criaturas hero + meta-progresión visual del santuario. Ejecutar bien esos pilares es crítico.

---

## Cómo medir éxito

Por fase:

- **Fase 1 ✅:** prototipo jugable end-to-end (logrado)
- **Fase 2 (MVP):** build production-ready con todas las features core. KPI: jugadores externos pueden completar 30 niveles sin bugs bloqueantes.
- **Fase 3 (soft launch):** validar KPIs target en mercados pequeños:
  - D1 retention ≥40%
  - D7 retention ≥20%
  - D30 retention ≥8%
  - ARPDAU $0.10-0.25
  - Conversion rate ≥3%
- **Fase 4 (global launch):** escalar lo que validó el soft launch.

LTV target del usuario (Plan Maestro): $1.50-$3.50. Esto define cuánto se puede invertir en marketing por install.
