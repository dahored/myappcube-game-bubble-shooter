# Coralia

> Cozy underwater Bubble Shooter — iOS + Android

**Estudio:** myappcube  
**Engine:** Defold (Lua)  
**Plataformas:** Android + iOS  
**Estado:** Fase 2 — MVP en progreso

## Sobre el juego

Coralia es un Bubble Shooter cozy submarino donde Marina, una joven sirena, restaura un arrecife de coral apagado rescatando criaturas marinas atrapadas en burbujas. Cada nivel completado devuelve color y vida al arrecife.

Modelo F2P híbrido (Ads + IAP + Battle Pass), audiencia objetivo: mujeres 25-45 casual.

## Requisitos para desarrollar

- **Defold** ([download](https://defold.com/download/)) — editor + build tools
- Mac con Xcode para builds iOS
- Android SDK para builds Android

## Estructura del proyecto

```
coralia/
├── game.project          # Configuración Defold (display, bootstrap, bundles)
├── input/                # Input bindings (touch, back)
├── main/                 # Bootstrap: collection + go + script de routing
├── splash1/              # Pantalla 1: logo del estudio (myappcube)
├── splash2/              # Pantalla 2: logo del juego + versión + cargando
├── level_map/            # Mapa de niveles scrolleable por capítulos
├── gameplay/             # (próximo) Partida principal
├── modules/              # Módulos Lua reutilizables (config, router, save, levels)
├── assets/               # Sprites, audio, fuentes, atlas
│   ├── atlas/            # logos.atlas, bubbles.atlas
│   ├── fonts/            # vera_mo_bd.ttf + coralia_ui.font
│   ├── images/logos/     # logo.png (juego) + logo_myappcube.png (estudio)
│   └── sprites/bubbles/  # v1 spritesheets + idle PNGs
├── data/levels/          # 001-020.json (20 niveles, 2 capítulos)
├── localization/         # translations.csv (6 idiomas: es, en, it, fr, de, pt)
└── docs/                 # GDD, wireframes, backlog, status
```

## Flujo de pantallas (actual)

```
Splash 1 (logo estudio, ~2.7s) → Splash 2 (logo juego + cargando, ~2s) → Mapa de niveles
```

## Módulos reutilizables

| Módulo | Responsabilidad |
|---|---|
| `modules/config.lua` | Constantes globales (grid, física, colores, economía) |
| `modules/router.lua` | Navegación: `router.go(scene_name)` |
| `modules/save_manager.lua` | `save_mgr.load()` / `.save(data)` |
| `modules/level_manager.lua` | `level_mgr.load(id)` / `.load_all()` — caché de JSONs |

## Setup inicial

1. Clonar el repositorio
2. Abrir Defold → `Open Project` → seleccionar `game.project`
3. Esperar a que Defold importe los assets
4. Build → Run (Cmd+B) para correr en desktop

## Documentación

- `docs/02_GDD_Coralia.md` — Game Design Document completo
- `docs/03_Wireframes_Coralia.md` — Especificación de las 17 pantallas
- `docs/06_Backlog_GitHub_Issues.md` — Backlog de issues
- `docs/07_Status_y_Roadmap.md` — Estado actual + roadmap

## Licencia

Propiedad de myappcube. No redistribuir.
