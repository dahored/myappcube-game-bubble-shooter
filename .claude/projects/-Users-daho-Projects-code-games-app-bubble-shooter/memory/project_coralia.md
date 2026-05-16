---
name: Coralia project context
description: Stack, estado, decisiones clave y convenciones del proyecto Coralia
type: project
---

Proyecto Bubble Shooter mobile cozy submarino (Android + iOS).
Estudio: myappcube. Dev: Diego (solo dev).

**Motor migrado de Godot 4 → Defold el 2026-05-06.**
Why: mejor performance, build size más chico, mejor soporte de monetización mobile.

Estructura de código activo (Defold/Lua):
- `game.project` — config del proyecto Defold
- `main/` — script raíz, gestión de colecciones (escenas)
- `gameplay/` — bubble.script, grid.script, canon.script, gameplay.script
- `modules/` — config.lua, grid_logic.lua, level_manager.lua, save_manager.lua, economy_manager.lua, game_manager.lua
- `input/` — input bindings

Código legacy Godot (referencia, no activo):
- `scripts/` — GDScript original
- `scenes/` — escenas .tscn originales

Assets que se reutilizan del trabajo Godot:
- `assets/sprites/bubbles/v1/` y `v2/` — spritesheets PNG 1280×160 (8 frames)
- `data/levels/` — JSONs de niveles (mismo formato, compatible)
- `localization/translations.csv` — strings UI
- `tools/` — scripts Python de generación de sprites

**How to apply:** Al trabajar en código, usar Lua y API de Defold, NO GDScript. Mensajes con msg.post() en lugar de señales. Factory para instanciar game objects. Los módulos se importan con require("modules.X").

GitHub repo: 33 issues creados en el repo privado (ver 06_Backlog_GitHub_Issues.md).
