# Coralia — Contexto para Claude Code

Este archivo se carga automáticamente al iniciar una sesión de Claude Code en este repo. Contiene el contexto mínimo que Claude debe conocer para trabajar bien sin re-explicación cada vez.

## Qué es Coralia

**Coralia** es un Bubble Shooter mobile cozy submarino para Android + iOS. Estudio: **myappcube** (Diego). Stack: **Godot 4 + GDScript**. Modelo: **F2P híbrido** (Ads + IAP + Battle Pass + Suscripción fase 2). Audiencia: mujeres 25-45 casual. Estado actual: prototipo Fase 1 jugable, arrancando Fase 2 (MVP).

Protagonista: **Marina** (sirena joven). Antagonista: **La Sombra Profunda** (no es villano — es una criatura herida que Marina libera con compasión). Estructura: 6 capítulos × 10 niveles = 60 niveles MVP. 6 idiomas al lanzamiento (es, en, it, fr, de, pt) gestionados con AI translation a $0.

## Documentos de referencia (orden de lectura)

Cuando arranques una sesión, leé en este orden según lo que vayas a hacer:

1. **`docs/07_Status_y_Roadmap.md`** — siempre primero. Te dice dónde estamos y qué queda.
2. **`docs/06_Backlog_GitHub_Issues.md`** — el backlog de trabajo. Cada sección H2 es un issue independiente con acceptance criteria. Si Diego te dice "trabajemos en el issue X", buscalo acá.
3. **`docs/02_GDD_Coralia.md`** — el Game Design Document completo (17 secciones). Consultá la sección relevante antes de implementar features. Por ejemplo:
   - Para mecánicas → secciones 1-4
   - Para economía → sección 6
   - Para retención (daily, missions, achievements) → sección 7
   - Para Battle Pass → sección 8
   - Para monetización (ads, IAP) → sección 9
   - Para UI/pantallas → sección 10
   - Para arte → sección 11
   - Para audio → sección 12
   - Para narrativa (criaturas, antagonista) → sección 13
   - Para arquitectura técnica → sección 14
4. **`docs/03_Wireframes_Coralia.md`** — spec textual de las 17 pantallas. Para cualquier UI work.
5. **`docs/wireframes/styled_mockups.html`** — mockups visuales estilizados. Útil para entender el "look and feel" deseado.
6. **`CHANGELOG.md`** — historial de cambios por chunk para entender qué se hizo.

## Convenciones del proyecto

### Código
- **Naming archivos:** `snake_case.gd`
- **Naming clases:** `PascalCase` (con `class_name`)
- **Naming variables:** `snake_case`
- **Naming constantes:** `SCREAMING_SNAKE_CASE`
- **Naming signals:** verbo en pasado (`level_completed`, `bubble_popped`)
- **Tipado estático** cuando sea posible: `var lives: int = 5`
- **Comentarios:** docstring en funciones públicas con 3+ líneas
- **i18n:** todo string visible al usuario va a `localization/translations.csv`, NUNCA hardcoded

### Arquitectura
- 11 autoloads ya stubeados en `scripts/autoloads/` — NO crear nuevos sin discusión. Ampliar los existentes (Audio, Save, Economy, BattlePass, Ads, IAP, Analytics, Firebase, Locale, Level, Game).
- **Bus central de signals** en `GameManager` — la comunicación entre autoloads pasa por ahí, no entre autoloads directamente.
- **Niveles en JSON** en `data/levels/` — formato definido en GDD 14.4. NO hardcodear niveles en código.
- **Color shuffle** ya implementado en `grid.setup_from_level` — al cargar un nivel, los colores se randomizan preservando posiciones (estilo Candy Crush).
- **Smart queue** ya implementado en `canon._random_type` — solo da colores con 1+ instancias en grid.
- **Save format** documentado en GDD 14.5. Schema en `save_manager.gd::_default_save()`.

### Convenciones cross-proyecto con app-impostor
Diego tiene otra app llamada Impostor. Para mantener consistencia entre apps del estudio, Coralia hereda:

- **Two-splash pattern**: pantalla 1 Company Splash + pantalla 2 Loading Splash (con versión)
- **Settings de 4 secciones**: Preferencias del juego / Cuenta y asistencia / Comunidad / Legal
- **3 sliders de audio**: Sonidos del juego / Efectos interfaz / Sonidos pop (no music+sfx genérico)
- **Vibración** como toggle separado en Settings
- **6 idiomas default**: es, en, it, fr, de, pt

NO inventar nuevas convenciones de UI sin chequear que app-impostor también las usa o sin acuerdo explícito con Diego.

### Workflow de Git
- **Un commit por chunk/issue**, no múltiples commits pequeños
- **Mensaje de commit** siempre con prefijo: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `polish:`
- **Branch por issue**: `git checkout -b issue-N-short-description`
- **Mergear a main vía PR** (incluso solo dev — fuerza review)
- **Actualizar CHANGELOG.md** al cerrar cada chunk

### Workflow de issues
1. Diego dice "trabajemos en el issue N" → leés `06_Backlog_GitHub_Issues.md` para encontrarlo
2. Verificás que las dependencias están resueltas
3. Leés la sección relevante del GDD para alinear
4. Creás branch, implementás según acceptance criteria
5. Probás en Godot
6. Commit + push + PR
7. Mergear y cerrar issue
8. Update CHANGELOG.md

## Estado actual del código

### Implementado (Fase 1 + parte de Fase 2)
- Grid hexagonal funcional (`grid.gd`, `grid_logic.gd`)
- Cañón con drag aim, trayectoria con primer rebote, cola de 2 burbujas (`canon.gd`)
- Match detection (flood-fill BFS) y drops (`grid.gd`)
- Win/lose conditions con HUD y modal
- 5 niveles JSON en `data/levels/001-005.json`
- Smart queue + color shuffle + queue rotation animation
- Persistencia (save/load) con `SaveManager`

### Pendiente (priorizado)
Ver `06_Backlog_GitHub_Issues.md`, sección "Fase 2 — MVP" para los issues. Top priorities:

1. Audio (música + SFX)
2. Sistema de vidas (5, regen 30 min)
3. Sistema de monedas + gemas con drops
4. Más niveles (subir de 5 a 20+)
5. Onboarding tutorial
6. Santuario y Level Select pantallas

## Cosas a NO hacer

- ❌ NO crear pantallas o features que no estén en el GDD o en el backlog
- ❌ NO modificar el GDD sin discutir con Diego (es contrato)
- ❌ NO usar formatos binarios para niveles (deben quedar editables JSON)
- ❌ NO hardcodear strings en UI (deben ir a translations.csv)
- ❌ NO commitear archivos sensibles: `*.keystore`, `google-services.json`, `GoogleService-Info.plist` (ya en `.gitignore`)
- ❌ NO crear nuevos autoloads sin discusión — los 11 existentes deberían cubrir todo
- ❌ NO mezclar features en un commit. Un chunk = un commit.

## Cómo Diego prefiere trabajar

- **Directo y conciso.** No explicar de más cuando ya lo entiende.
- **Confirmá decisiones críticas antes de implementar** (no asumir).
- **Mostrá screenshots/output cuando sea posible** para validar que el cambio se ve bien.
- **Pushback OK** si una decisión técnica te parece mal — Diego valora el feedback honesto.
- **Spanglish OK** — Diego habla español natural mezclando términos técnicos en inglés.
- Diego es **solo dev, no diseñador** — bias toward soluciones simples para no-artistas (asset stores, AI gen, placeholders bien usados).

## Decisiones administrativas pendientes (sin código pero importantes)

Antes del soft launch hay que cerrar:

1. Verificar disponibilidad de "Coralia" en App Store / Google Play / dominios / redes
2. Apple Developer Program ($99/año)
3. Google Play Console ($25 una vez)
4. Firebase project (free tier al inicio)
5. AdMob + AppLovin MAX accounts
6. RevenueCat account
7. Repo GitHub privado creado y push del código
