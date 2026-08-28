# Coralia

> Cozy underwater Bubble Shooter — iOS + Android

**Estudio:** myappcube
**Engine:** Unity 6 (C#), URP
**Plataformas:** Android + iOS
**Estado:** Fase 2 — MVP en progreso

## Sobre el juego

Coralia es un Bubble Shooter cozy submarino donde Marina, una joven sirena, restaura un arrecife de coral apagado rescatando criaturas marinas atrapadas en burbujas. Cada nivel completado devuelve color y vida al arrecife.

Modelo F2P híbrido (Ads + IAP + Battle Pass), audiencia objetivo: mujeres 25-45 casual.

> El proyecto arrancó como prototipo en Godot 4, pasó brevemente por Defold, y migró a Unity 6 en mayo 2026. Todo el código activo hoy es Unity/C#.

## Requisitos para desarrollar

- **Unity 6** (LTS) con soporte Android + iOS instalado
- Mac con Xcode para builds iOS
- Android SDK/NDK (vía Unity Hub) para builds Android

## Estructura del proyecto

```
coralia/
├── Assets/
│   ├── Scenes/{Splash,Home,Game}/       # .unity — SplashStudio, SplashGame, HomeGame, LevelMap, Gameplay
│   ├── Scripts/
│   │   ├── Core/                        # managers estáticos: SaveManager, LocaleManager, AudioManager,
│   │   │                                #   SceneLoader + SceneTransition
│   │   ├── UI/                          # componentes reutilizables: ButtonPop, UIPanel, SettingsToggle,
│   │   │                                #   LocalizedText, SafeAreaPanel, TopPanelController...
│   │   ├── Home/ Splash/ LevelMap/       # controllers por pantalla
│   │   ├── Gameplay/                    # cañón, grid hexagonal, match, win/lose
│   │   └── Data/                        # LevelData.cs (modelo de nivel), LevelLoader.cs
│   ├── Prefabs/                         # UI/buttons, panels, user, game, inputs + Gameplay
│   ├── Resources/
│   │   ├── translations.csv             # 6 idiomas, cargado por LocaleManager
│   │   └── Levels/Chapter_1/2/3/         # niveles JSON reales (LevelData.cs)
│   └── Sprites/                         # UI, burbujas, fondos, etc.
├── design/exported/                     # exports de diseño antes de importar a Unity
└── docs/                                # GDD, wireframes, backlog, status
```

## Flujo de pantallas (actual)

```
Splash Studio (logo estudio) → Splash Game (logo juego + cargando) → Home → Level Map → Gameplay
```

## Managers (estáticos, `Scripts/Core/`)

| Manager | Responsabilidad |
|---|---|
| `SaveManager` | Persistencia vía `PlayerPrefs` — vidas, gemas, nivel máximo desbloqueado, idioma, volúmenes |
| `LocaleManager` | Diccionario de traducciones cargado de `Resources/translations.csv`, `Get(key)` |
| `AudioManager` | SFX/música, tolera clips vacíos sin romper (`Instance?.PlaySfx(...)`) |
| `SceneLoader` + `SceneTransition` | Constantes de escenas + fade/transición animada entre ellas |

## Setup inicial

1. Clonar el repositorio
2. Abrir Unity Hub → `Add project from disk` → seleccionar la carpeta `coralia/`
3. Abrir con Unity 6 (la versión indicada en `ProjectSettings/ProjectVersion.txt`)
4. Abrir `Assets/Scenes/Splash/SplashStudio.unity` y darle Play para probar el flujo completo

## Documentación

- `CLAUDE.md` — contexto del proyecto para asistencia con IA, estado real del código
- `docs/02_GDD_Coralia.md` — Game Design Document completo (§14 arquitectura es de la era Godot, no confiar en esa sección)
- `docs/03_Wireframes_Coralia.md` — Especificación de las pantallas
- `docs/06_Backlog_GitHub_Issues.md` — Backlog de issues (verificar contra `gh issue list`)
- `docs/07_Status_y_Roadmap.md` — Estado actual + roadmap

## Licencia

Propiedad de myappcube. No redistribuir.
