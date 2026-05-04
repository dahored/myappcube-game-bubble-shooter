# Handoff a Claude Code

Esta es tu guía para arrancar a trabajar con Claude Code en CLI tras pasar de Cowork. Tenés tres cosas listas:

1. **`CLAUDE.md`** en root del repo — Claude Code lo carga automáticamente al iniciar una sesión en este directorio
2. **`docs/06_Backlog_GitHub_Issues.md`** — backlog completo de issues
3. **Este documento** — sample prompts para arrancar y mantener buen flow

---

## Setup inicial (una sola vez)

### 1. Cerrar el commit pendiente del prototipo

```bash
cd /Users/daho/Projects/code/games/app-bubble-shooter
rm -f .git/index.lock
git add -A
git commit -m "feat: chunk A persistence + docs reorganization

- SaveManager full implementation
- Boot loads next-to-play level (highest_completed + 1)
- Best score in HUD, new record popup
- Reset Save debug button
- CLAUDE.md for Claude Code context
- docs reorganization: 06_Backlog, 07_Status_Roadmap, HANDOFF
- Phase 1 plan v0.2 marked complete"
```

### 2. Crear repo GitHub privado y push

```bash
# Si tenés gh CLI instalado:
gh repo create coralia --private --source=. --push

# Si no, crealo manualmente en github.com → New Repo (privado)
git remote add origin git@github.com:<tu-usuario>/coralia.git
git branch -M main
git push -u origin main
```

### 3. Importar issues a GitHub

Tenés tres opciones, ordenadas de más fácil a más automatizado:

**Opción A (manual):** copy-paste cada sección H2 de `docs/06_Backlog_GitHub_Issues.md` a GitHub UI → New Issue. ~30-45 min para los 30 issues.

**Opción B (semi-auto con `gh` CLI):** abrí el archivo y ejecutá:

```bash
# Primero crear los labels una sola vez:
gh label create "phase-1" --color 5BC0EB
gh label create "phase-2" --color 9BC53D
gh label create "phase-3" --color FDE74C
gh label create "phase-4" --color FA7921
gh label create "backlog" --color C0C0C0
gh label create "feat" --color 00ff00
gh label create "bug" --color ff0000
gh label create "chore" --color 6c757d
gh label create "polish" --color e83e8c
gh label create "tech" --color 17a2b8
gh label create "priority-high" --color FF0000
gh label create "priority-medium" --color FFA500
gh label create "priority-low" --color FFFF00
gh label create "size-XS" --color C0C0C0
gh label create "size-S" --color 90EE90
gh label create "size-M" --color FFD700
gh label create "size-L" --color FF8C00
gh label create "size-XL" --color DC143C
```

Después por cada issue:
```bash
gh issue create --title "[Phase 2] Audio: música + SFX placeholders" \
  --body-file <(awk '/^## \[Phase 2\] Audio/,/^## /{if(/^## /&&NR>1)exit; print}' docs/06_Backlog_GitHub_Issues.md) \
  --label "phase-2,feat,audio,priority-high,size-M"
```

(Es manual por issue pero más rápido que copy-paste UI.)

**Opción C (automatizado con Claude Code):** primer mensaje a Claude Code:

> Lee `docs/06_Backlog_GitHub_Issues.md` y creá un GitHub issue por cada sección H2 usando `gh issue create`, asignando los labels que están en cada sección bajo "Labels:". Antes de empezar, asegurate de que los labels existen (creá los que falten con `gh label create`). Reportame cuando termines con el conteo de issues creados.

Esa Opción C te ahorra todo el laburo manual.

---

## Primer prompt a Claude Code (sesión 1)

Cuando abras Claude Code en el directorio del proyecto, pegá esto como primer mensaje:

```
Hola Claude. Estoy continuando el desarrollo de Coralia (mobile bubble shooter en Godot 4).
Acabo de pasar de trabajar en Cowork a trabajar acá en CLI con vos.

Por favor leé `CLAUDE.md` para contexto general del proyecto.
Después leé `docs/07_Status_y_Roadmap.md` para entender dónde estamos.

Una vez que tengas contexto, decime:
1. ¿Cuál es el issue más prioritario para arrancar según el backlog?
2. ¿Tenés alguna duda sobre el estado actual antes de empezar?
3. ¿Hay algo en el código que cambiarías antes de seguir agregando features?
```

Claude Code va a leer los docs y darte una respuesta orientada. Ahí ya podés decir "OK, arranquemos con el issue X" y trabajar normal.

---

## Patrón de prompts para sesiones siguientes

### Para arrancar un issue específico

```
Trabajemos en el issue [N] del backlog: [título].

Leé la sección correspondiente en `docs/06_Backlog_GitHub_Issues.md` y la sección
relevante del GDD que se referencia. Antes de codificar, contame tu plan en 3-5 puntos
con archivos que vas a tocar y signals/funciones que vas a agregar.
```

### Para continuar work-in-progress

```
Sigamos con [issue/tarea]. Hicimos [resumen breve de lo último]. Lo siguiente es [próximo paso].
Verificá que el código actual compila en Godot antes de seguir.
```

### Para hacer un PR/commit

```
Hicimos [descripción]. Está todo testeado en Godot. Armame:
1. El mensaje de commit con descripción
2. Update al CHANGELOG.md en sección apropiada
3. Comandos exactos para crear branch, commit, push y PR
```

### Para review crítico

```
Antes de mergear, revisá tu trabajo crítico:
- ¿Algún hardcoded string que debería ir a translations.csv?
- ¿Algún número mágico que debería ser const con nombre?
- ¿Hay tests posibles para esto?
- ¿El código respeta las convenciones de CLAUDE.md?
```

---

## Patrones que te conviene mantener

### 1. Una sesión = un issue

No mezclar varios issues en una sesión. Mejor:
- Sesión 1: issue de Audio (1-2 días → commit → PR → merge)
- Sesión 2: issue de Vidas (1-2 días → commit → PR → merge)

Mantener PRs pequeños hace que sean más fáciles de revertir si algo se rompe.

### 2. Validación visual frecuente

Después de cada feature significativa:
1. Corré el juego en Godot
2. Tomá screenshot
3. Pegáselo a Claude Code preguntando "¿se ve como esperabas?"

### 3. Update CHANGELOG.md siempre

Antes del commit final, asegurate de que `CHANGELOG.md` refleja lo que cambió. Claude Code puede escribir la entrada por vos:

```
Update CHANGELOG.md con lo que hicimos en esta sesión.
Formato: agregar una entrada bajo "[Unreleased] / Fase 2" con:
- Título del chunk/issue
- Lista bullet de cambios concretos por archivo
- Fecha
```

### 4. Re-leer CLAUDE.md cada sesión

Si Claude Code parece perder contexto entre sesiones, recordale:

```
Por favor releé `CLAUDE.md` antes de continuar — es el contexto del proyecto.
```

### 5. Mantener limpio el branch principal

- `main` siempre debería compilar y correr sin errores
- Cualquier work-in-progress va en branches `issue-N-...`
- Mergear via PR (incluso solo dev — fuerza review mental)

---

## Si algo se rompe

### Caso 1: Claude Code te propone algo que va contra CLAUDE.md

Pushback:

```
Eso va contra la convención en CLAUDE.md sección [X]. Releeme la convención y pensá una alternativa que la respete.
```

### Caso 2: Implementación se siente "off"

```
Antes de continuar, frenamos. Mostrame el diff exacto de lo que hiciste.
Hay algo en el approach que no me cierra. Quiero ver el código antes de seguir.
```

### Caso 3: El proyecto deja de compilar

```
El juego no abre en Godot. ¿Podés revertir el último cambio y tirar git diff
contra el último commit funcional para identificar qué rompió?
```

### Caso 4: Diego no entiende algo

```
Antes de avanzar, explicame el cambio que hiciste como si yo no fuera programador.
¿Por qué este approach y no otro? ¿Qué trade-offs tiene?
```

---

## Los 5 issues que más rinde atacar primero

Si querés ROI alto en las primeras 2-3 semanas con Claude Code, este es el orden recomendado:

| # | Issue | Por qué primero | Effort |
|---|---|---|---|
| 1 | **Audio (música + SFX)** | 10x al feel del juego con poco effort | 1-2 días |
| 2 | **Sistema de vidas** | Base de retención, prerequisito de monetización | 1-2 días |
| 3 | **Sistema de monedas/gemas** | Idem, con drops por nivel | 1-2 días |
| 4 | **Más niveles (5 → 20)** | Más contenido, sensación de juego "real" | 3-5 días |
| 5 | **Onboarding tutorial** | Hace al juego accesible para playtesters | 2 días |

Después de estos 5, el juego ya se siente como un MVP temprano. Podés hacer un playtest informal antes de seguir con cosas más grandes (Santuario, Battle Pass, Ads, etc.).

---

## Última cosa: backup mental

Si en algún momento estás perdido o confundido sobre el estado del proyecto:

1. Abrí `CLAUDE.md` (root del repo)
2. Abrí `docs/07_Status_y_Roadmap.md`
3. Abrí `CHANGELOG.md`

Esos 3 archivos te dicen TODO lo que necesitás para retomar contexto.

---

¡Suerte con el desarrollo! El prototipo ya está en buena forma. Lo que sigue es agregar capa por capa hasta llegar al MVP.
