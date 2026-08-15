# Coralia — Contexto para Claude Code

Este archivo se carga automáticamente al iniciar una sesión de Claude Code en este repo. Contiene el contexto mínimo que Claude debe conocer para trabajar bien sin re-explicación cada vez.

## Qué es Coralia

**Coralia** es un Bubble Shooter mobile cozy submarino para Android + iOS. Estudio: **myappcube** (Diego). Stack: **Unity 6 + C#**. Modelo: **F2P híbrido** (Ads + IAP + Battle Pass + Suscripción fase 2). Audiencia: mujeres 25-45 casual.

Protagonista: **Marina** (sirena joven). Antagonista: **La Sombra Profunda** (criatura herida que Marina libera con compasión). Estructura: 6 capítulos × 10 niveles = 60 niveles MVP. 6 idiomas al lanzamiento (es, en, it, fr, de, pt).

> El proyecto arrancó como prototipo en **Godot 4**, pasó brevemente por **Defold**, y migró a **Unity 6** en mayo 2026 (`fb0a6da chore: migrar a Unity 6 — eliminar Defold`). Todo el código activo hoy es Unity/C#. Si encontrás referencias a Godot o Defold en `docs/`, `README.md` o `CHANGELOG.md`, son históricas y NO reflejan el estado actual — no confíes en sus secciones técnicas de engine, solo en las de diseño (GDD, wireframes, narrativa).

## Motor: Unity 6

Proyecto Unity en `coralia/`. Tipos de archivo clave:
- `.unity` — escena
- `.prefab` — game object reutilizable con sus componentes
- `.cs` — script C# (MonoBehaviour, o clase estática para managers/utils)
- `.asset` — ScriptableObject / config asset (ej. RenderPipeline settings)
- `ProjectSettings/` — configuración del proyecto (nunca editar a mano, usar el editor)

Render pipeline: URP. Orientación: portrait 1080×1920 (`defaultScreenWidth/Height` en ProjectSettings están al revés porque es el valor base landscape de Unity — no tocar).

## Documentos de referencia (orden de lectura)

⚠️ Estos docs describen diseño y roadmap con precisión, pero sus secciones de **arquitectura técnica / engine están desactualizadas** (escritas para Godot o Defold). Usalos para game design, no para convenciones de código.

1. **`docs/07_Status_y_Roadmap.md`** — punto de partida, pero su sección de engine es vieja. Verificar estado real contra el código antes de asumir algo.
2. **`docs/06_Backlog_GitHub_Issues.md`** — el backlog. Cada H2 es un issue con acceptance criteria.
3. **`docs/02_GDD_Coralia.md`** — GDD completo (17 secciones). Consultar antes de implementar features:
   - Mecánicas → secciones 1-4
   - Economía → sección 6
   - Retención (daily, missions, achievements) → sección 7
   - Battle Pass → sección 8
   - Monetización (ads, IAP) → sección 9
   - UI/pantallas → sección 10
   - Arte → sección 11
   - Audio → sección 12
   - Narrativa (criaturas, antagonista) → sección 13
   - Arquitectura técnica → sección 14 (⚠️ vieja, no confiar)
4. **`docs/03_Wireframes_Coralia.md`** — spec textual de las pantallas.
5. **`docs/08_Arte_Assets_Specs.md`** — specs de producción de arte (tamaños, colores, prompts) para sprites/iconos/UI. Vigente y últil al exportar assets nuevos.
6. **`CHANGELOG.md`** — histórico de chunks, pero solo cubre hasta Fase 1 Godot. Los commits de Unity no están volcados ahí — usar `git log` para historial reciente.

## Estructura de archivos (Unity)

```
coralia/
  Assets/
    Scenes/
      Splash/           ← SplashStudio.unity, SplashGame.unity
      Home/              ← HomeGame.unity
      Game/              ← LevelMap.unity, (próximo) Gameplay.unity
    Scripts/
      Core/              ← managers estáticos: SaveManager, LocaleManager, AudioManager,
                            SceneLoader (constantes de nombres de escena), SceneTransition
      UI/                ← componentes de UI reutilizables (ButtonPop, UIPanel, SettingsToggle,
                            LocalizedText, ResponsiveLayout, SafeAreaPanel, TopPanelController…)
      Home/              ← HomeGame.cs
      Splash/            ← SplashStudio.cs, SplashGame.cs
      LevelMap/          ← LevelMapController.cs, LevelNodeView.cs, ScrollPinController.cs
      Gameplay/          ← (vacío todavía — cañón/grid/match pendiente)
      Data/              ← LevelData.cs (modelo serializable de nivel)
    Prefabs/
      UI/buttons/ panels/ user/ game/ inputs/
      Gameplay/
    Resources/
      translations.csv   ← 6 idiomas, cargado por LocaleManager
      Audio/              ← AudioManager.prefab
      Levels/Chapter_1/ Chapter_2/ Chapter_3/  ← JSONs de niveles (deserializados a LevelData)
    Sprites/UI/          ← Buttons, Pines, Panels, Banners, Inputs
    Data/                ← ScriptableObjects de configuración
design/exported/          ← exports de diseño (PNGs a distintas densidades) antes de importarlos a Unity
```

## Convenciones de código (C#)

- **Sin namespace** — las clases van a global namespace (así está todo el código existente).
- **Naming clases/archivos:** `PascalCase.cs`, un archivo por clase pública.
- **Naming campos serializados:** `camelCase` con `[SerializeField]`, alineados en columnas cuando hay varios seguidos (ver `SettingsToggle.cs`, `ButtonPop.cs` como referencia de estilo).
- **Naming campos privados no serializados:** `_camelCase` con guión bajo.
- **Constantes:** `SCREAMING_SNAKE_CASE` o `PascalCase` según visibilidad (`const string KEY_LANGUAGE`, pero `public const string SPLASH_STUDIO`).
- **Managers globales:** clases estáticas (no singletons MonoBehaviour) — `SaveManager`, `LocaleManager`, `SceneLoader`. Persistencia vía `PlayerPrefs`.
- **Comentarios:** solo cuando el WHY no es obvio — sin docstrings.
- **i18n:** todo string visible → `Resources/translations.csv`, acceso vía `LocaleManager.Get(key)`. Componente `LocalizedText.cs` para texto estático en UI; `LocaleManager.OnLanguageChanged` para refrescar dinámico.
- **Niveles en JSON:** NUNCA hardcodear niveles en código — van en `Resources/Levels/Chapter_N/*.json`, modelo en `LevelData.cs`.

### Routing (cambio de escena)
- `SceneLoader.GoTo(SceneLoader.LEVEL_MAP)` (constantes definidas en `SceneLoader.cs`) → delega a `SceneTransition.GoTo(sceneName)`.
- `SceneTransition` es una clase que se auto-instancia (`DontDestroyOnLoad`) la primera vez que se usa; maneja el fade + animación de burbujas entre escenas y el `SceneManager.LoadSceneAsync` por debajo.

## Convenciones cross-proyecto (app-impostor)

- **Two-splash pattern**: splash 1 (studio logo) + splash 2 (game logo + versión + loading) ✅
- **Settings de 4 secciones**: Preferencias / Cuenta y asistencia / Comunidad / Legal
- **3 sliders de audio**: Sonidos del juego / Efectos interfaz / Sonidos pop
- **Vibración** como toggle separado
- **6 idiomas**: es, en, it, fr, de, pt

## Workflow de Git

- **Un commit por chunk/issue**
- **Prefijo en commits**: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `polish:`
- **Branch por issue**: `git checkout -b issue-N-short-description`
- **Mergear a main vía PR**
- **Actualizar CHANGELOG.md** al cerrar cada chunk (nota: no se viene haciendo desde la migración a Unity — retomar si Diego lo pide)

## Workflow de issues

1. Diego dice "trabajemos en el issue N" → leer `06_Backlog_GitHub_Issues.md`
2. Verificar dependencias resueltas
3. Leer sección relevante del GDD
4. Branch → implementar según acceptance criteria
5. Probar en Unity Editor (Play mode)
6. Commit + push + PR → mergear + cerrar issue
7. Update CHANGELOG.md

## Estado actual del código (2026-08-11)

### Implementado ✅
- Proyecto Unity 6 (URP), portrait 1080×1920
- Managers Core: `SaveManager` (PlayerPrefs), `LocaleManager` (6 idiomas), `AudioManager`, `SceneLoader` + `SceneTransition` (fade + burbujas animadas entre escenas)
- Splash Studio + Splash Game
- HomeGame (lobby)
- Level Map: `LevelMapController`, `LevelNodeView`, `ScrollPinController`, `PlayerCard`/`AvatarDisplay` en el nodo actual
- Settings panel completo (sliders de audio, toggles, dropdown de idioma)
- Niveles en `Resources/Levels/Chapter_1/2/3` (JSON → `LevelData`)
- `TopPanelController` (safe area del panel superior) — en progreso

### Pendiente (priorizado)
Ver `docs/06_Backlog_GitHub_Issues.md` (con la salvedad de que fue escrito para Defold — validar contra el código real antes de asumir qué falta). Top conocido:
1. Gameplay: cañón + grid hexagonal + match + win/lose (carpeta `Scripts/Gameplay/` está vacía)
2. Audio: música + SFX in-game
3. Sistema de vidas (5 vidas, regen 30 min)
4. HUD en gameplay (score, shots, lives)
5. Componentes de HUD/Shop reutilizables (resource pills, currency bar)

## Cosas a NO hacer

- ❌ NO crear pantallas o features fuera del GDD / backlog
- ❌ NO modificar el GDD sin discutir con Diego
- ❌ NO formatos binarios para niveles (JSON siempre)
- ❌ NO hardcodear strings UI (van a `Resources/translations.csv`)
- ❌ NO commitear: `*.keystore`, `google-services.json`, `GoogleService-Info.plist`
- ❌ NO editar `ProjectSettings/` a mano — pasar por el editor de Unity
- ❌ NO asumir que las secciones de engine/arquitectura de `docs/` son ciertas sin verificar contra el código — están escritas para Godot/Defold

## Cómo Diego prefiere trabajar

- Directo y conciso — no explicar de más
- Confirmar decisiones críticas antes de implementar
- Spanglish OK (mezcla español + términos técnicos en inglés)
- Solo dev sin equipo de arte — soluciones simples, no-artista friendly
- Pushback OK si una decisión técnica parece mal

## Decisiones administrativas pendientes

1. Verificar "Coralia" disponibilidad (App Store, Google Play, dominios, redes)
2. Apple Developer Program ($99/año)
3. Google Play Console ($25 una vez)
4. Firebase project (free tier)
5. AdMob + AppLovin MAX accounts
6. RevenueCat account
7. Repo GitHub privado + push
