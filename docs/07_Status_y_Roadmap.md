# Coralia — Status y Roadmap

**Última actualización:** 2026-05-01

Documento maestro de estado del proyecto. Si abrís uno solo de los docs, **abrí este**.

---

## Estado actual

| Fase | Estado | Notas |
|---|---|---|
| **Fase 0** — Pre-producción | ✅ Completada | Concept, GDD v0.6, Wireframes spec, Plan Fase 1, setup técnico Godot |
| **Fase 1** — Prototipo jugable | ✅ Completada | 6 de 7 chunks ejecutados. Chunk 7 (playtest) skipped — pendiente informal |
| **Fase 2** — MVP | 🔄 En progreso | Chunk A (Persistencia) implementado. Resto en backlog |
| **Fase 3** — Soft Launch | ⏳ Pendiente | Tras MVP completo |
| **Fase 4** — Global Launch | ⏳ Pendiente | Tras soft launch validado |

---

## Qué tenemos hoy

Un **Bubble Shooter funcional jugable** en Godot 4 con:

- Grid hexagonal de 84 burbujas, físicas correctas
- Cañón con drag aim, línea de trayectoria con primer rebote, cola de 2 burbujas
- Smart queue (Candy Crush style): solo da colores que existen en el grid
- Color shuffle al cargar nivel: posiciones fijas, colores random (replayability)
- Match detection (flood-fill BFS) y drops por gravedad
- Score, win/lose con modales
- 5 niveles JSON con curva de dificultad
- 2 tipos de objetivos: clear_all y rescue (con criatura marcada con estrella)
- Animación de rotación de cola (drop + slide + fade-in del nuevo preview)
- **Persistencia**: progreso entre sesiones (last_played, best_scores, creatures_rescued, currencies, settings)
- 11 autoload stubs listos para crecer (Audio, Economy, BattlePass, Ads, IAP, Analytics, Firebase, Locale, Level, Save, Game)
- Localización CSV con 50+ keys en 6 idiomas (es, en, it, fr, de, pt) — pendiente activar
- Documentación completa: Plan Maestro, Concept, GDD, Wireframes, Plan Fase 1, Playtest Guide, Backlog

---

## Lo que falta (priorizado por valor)

Ver `06_Backlog_GitHub_Issues.md` para detalles. Aquí el resumen ordenado por impacto:

### Top 5 cosas que más mejorarían el juego ahora mismo

1. **Audio** — placeholders gratuitos. Mejora 10x el feel. Estimación: 1-2 días.
2. **Más niveles** (subir de 5 a 20+) — más contenido, más sensación de "juego real". 3-5 días.
3. **Sistema de vidas + monedas + gemas** — base de monetización. 2-3 días.
4. **Onboarding tutorial** — para que jugadores nuevos no se confundan. 2 días.
5. **Santuario y Level Select** — pantallas críticas faltantes. 5-7 días.

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
├── project.godot              ← Config Godot 4 (mobile portrait, autoloads)
├── scenes/
│   ├── main/boot.tscn         ← Pantalla inicial — routea según save state
│   └── gameplay/
│       ├── gameplay.tscn      ← Escena principal del juego (Chunk 1-6)
│       ├── canon.tscn         ← Cañón con cola de 2 burbujas
│       └── bubble.tscn        ← Burbuja individual
├── scripts/
│   ├── autoloads/             ← 11 singletons globales (Game, Audio, Save, Economy, etc.)
│   ├── gameplay/              ← grid_logic, grid, bubble, canon, gameplay
│   └── main/                  ← boot
├── data/levels/               ← 001-005.json (5 niveles del prototipo)
├── localization/              ← translations.csv (6 idiomas, 50+ keys)
└── assets/                    ← Placeholder hasta arte final (vacío)
```

---

## Cómo continuar con Claude Code

Diego planea continuar el desarrollo con Claude Code en CLI en lugar de Cowork. Pasos sugeridos:

### 1. Setup de GitHub

```bash
cd /Users/daho/Projects/code/games/app-bubble-shooter

# Si aún no committeaste el último chunk:
rm -f .git/index.lock
git add -A
git commit -m "feat: chunk A persistence + docs reorganization"

# Crear repo privado en GitHub
gh repo create coralia --private --source=. --push
# o manualmente en github.com → New Repo → push manual
```

### 2. Crear los issues en GitHub

Convertir cada sección H2 (`##`) de `06_Backlog_GitHub_Issues.md` en un GitHub issue. Hay ~30+ issues definidos. Opciones:

**Manual:** copy-paste cada sección a GitHub UI. Lleva ~30-45 min para los 30 issues.

**Semi-automático con `gh` CLI:** un script que lea las secciones del MD y use `gh issue create`. Si querés, le pedís a Claude Code que te lo arme — es un script rápido en bash.

**Automático con Claude Code:** decirle "lee `docs/06_Backlog_GitHub_Issues.md` y creá un issue de GitHub por cada sección H2, usando los labels que están listados". Probablemente lo hace en 2-3 minutos.

### 3. Workflow recomendado con Claude Code

Por cada issue:
1. Crear branch: `git checkout -b issue-N-short-description`
2. Pedirle a Claude Code que implemente el issue, dándole referencia al MD
3. Test en Godot
4. Commit + push del branch
5. PR contra main, mergear, cerrar el issue
6. Próximo issue

Para mantener el contexto, en cada sesión nueva de Claude Code:
- Le pasás el link al repo
- Le decís en qué issue estás trabajando
- Le pedís que lea el GDD section relevante antes de codear

### 4. Cadencia recomendada

- **Semana 1:** Audio (issue 1) + Más niveles (issue 4)
- **Semana 2:** Sistema de vidas + monedas (issues 3 y 5)
- **Semana 3:** Onboarding + Santuario (issues 7 y 11)
- **Semana 4:** Level Select + Pre-level (issues 12 y 13)
- ...continúa hasta MVP completo

Probable que te lleve **6-10 semanas** si avanzás 1 issue grande o 2 medianos por semana.

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
