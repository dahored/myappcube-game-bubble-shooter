# Coralia — Status y Roadmap

**Última actualización:** 2026-10-03

Documento maestro de estado del proyecto. Si abres uno solo de los docs, **abre este**.

---

## Estado actual

| Fase | Estado | Notas |
|---|---|---|
| **Fase 0** — Pre-producción | ✅ Completada | Concept, GDD, wireframes, setup técnico |
| **Fase 1** — Prototipo jugable | ✅ Completada | Mecánicas core validadas |
| **Fase 2** — MVP | 🔄 En progreso | El loop de juego está completo de punta a punta. Falta economía real, nueve power-ups y los servicios externos |
| **Fase 3** — Soft Launch | ⏳ Pendiente | Tras MVP completo |
| **Fase 4** — Global Launch | ⏳ Pendiente | Tras soft launch validado |

---

## Qué tenemos hoy

**Unity 6 (6000.6.1f1), URP, portrait 1080×1920, C# sin namespace.** 119 scripts.

### El loop de juego, completo ✅

Se entra desde el mapa, se juega el nivel, se gana o se pierde y se vuelve. Incluye:

- **Cañón** con apuntado por arrastre, cola de dos burbujas y cambio por toque
- **Grid hexagonal** (`HexGridMath`), colocación por celda exacta, rebote en paredes
- **Match** por flood-fill con la arcoíris como comodín, caída de las que quedan sueltas en cascada
- **Puntuación** con racha de combos (`ScoreRules`), popups, estrellas y récord por nivel
- **Objetivos** `clear_all` y `rescue`
- **Scroll de retirada**: el grid se aleja del cañón al llenarse
- **Final de partida**: victoria, derrota, sin movimientos (con oferta de seguir pagando), pausa, abandono

### Economía y progresión ✅ parcial

- **Vidas**: 5 máximo, regeneración de 30 min por vida calculada por tiempo real, vidas infinitas por tiempo, refill por monedas con descuento alterno
- **Monedas**: moneda única (`SaveManager.Coins`). ⚠️ **Sin ninguna fuente**: completar un nivel no da (decisión de GDD 6.3) y el resto de fuentes no existe. Hay un botón de desarrollo en Settings para darse monedas
- **Progreso**: nivel máximo, estrellas, récords, tutoriales vistos, inventario y desbloqueo de boosters
- **Cerrar la app a mitad de nivel cuesta una vida**; minimizarla no

### Boosters ✅ el sistema, 1 de 10 implementado

- `BoosterCatalog` en `Resources` concentra el arte de cada poder
- `BoosterRules` concentra radio, nombres y desbloqueo — que va por tutorial visto
- `LevelData.allowed_boosters` decide qué ofrece cada nivel
- Selección pre-nivel, HUD de gameplay, panel de recarga con packs y descuentos
- **Bomba de Coral** completa: carga de luz, destello, onda expansiva, sonido y vibración

### Pantallas ✅

Dos splash, Home, Level Map (tres variantes de escena: plana, curva y esférica), Settings completo,
pantalla previa al nivel, y todos los paneles de partida.

### Tutoriales ✅

`TutorialPanel` con una entrada por mecánica, demostraciones animadas que usan la misma geometría
que el gameplay, y "una sola vez en la vida" por id.

### Infraestructura ✅

- Managers estáticos: `SaveManager`, `LocaleManager` (6 idiomas), `SceneLoader`
- `AudioManager` con cuatro canales de efectos más uno de bucle
- Transiciones de escena con fade y burbujas
- Háptica nativa Android/iOS
- Resolución dinámica adaptativa
- Niveles en JSON (`Resources/Levels/Chapter_1..3`), 30 escritos de los 60 del MVP

---

## Lo que falta

### Bloqueante de todo lo demás

**No existe ninguna forma de ganar monedas.** Mientras no la haya, ningún precio significa nada y
el panel de recarga se agota solo. Ver el issue de recalibración de la economía en el backlog.

### Top 5 por valor

1. **Fuentes de monedas** — santuario, dailies, misiones o logros. Cualquiera desbloquea la economía
2. **Los nueve power-ups restantes** — cinco de inicio y cuatro de gameplay (GDD 3.2)
3. **30 niveles más** — de 30 a los 60 del MVP
4. **Audio real** — la infraestructura está, faltan clips
5. **Santuario** — decidir si `HomeGame` se expande o es pantalla nueva

### Necesario para lanzar, no urgente

Cloud save (Firebase), Ads (AdMob + AppLovin MAX), IAP (RevenueCat), Battle Pass, asset pass final,
y las decisiones administrativas de abajo.

---

## Mapa de documentos

```
docs/
├── 01_Concepto_Inicial.md        ← Visión, decisiones lockeadas
├── 02_GDD_Coralia.md             ← Game Design Document (17 secciones)
├── 03_Wireframes_Coralia.md      ← Spec de las pantallas
├── 05_Playtest_Guide_Coralia.md  ← Metodología de playtest
├── 06_Backlog_GitHub_Issues.md   ← Backlog, un issue por H2
├── 07_Status_y_Roadmap.md        ← este documento
└── 08_Arte_Assets_Specs.md       ← Specs de producción de arte
```

En la raíz: `CLAUDE.md`, el contexto que Claude Code carga al abrir el repo.

---

## Mapa de código

```
coralia/Assets/
  Scenes/      Splash/ Home/ Game/
  Scripts/
    Core/      SaveManager, LocaleManager, AudioManager, SceneLoader, SceneTransition
    Data/      LevelData, ChapterData, LevelLoader, LevelIndex
    Gameplay/  cañón, grid, match, boosters, paneles de partida
    LevelMap/  mapa curvo, nodos, decoraciones
    UI/        componentes reutilizables y paneles
    Home/ Splash/
  Prefabs/     UI/ Gameplay/ Animals/ Decorations/
  Resources/   translations.csv, Levels/, Chapters/, BoosterCatalog.asset, Audio/
  Sprites/     Bubbles, UI, Game, Animals, Backgrounds, Decorations
  Editor/      menús de depuración y drawers
design/exported/   exports de arte antes de importarlos
```

Detalle de la arquitectura en **GDD §14**.

---

## Workflow por issue

1. `git checkout -b issue-N-descripcion-corta`
2. Leer el issue en `06_Backlog_GitHub_Issues.md` y la sección del GDD que corresponda
3. Implementar siguiendo las convenciones de `CLAUDE.md` y de GDD §14.7
4. Probar en Play mode
5. Un commit por chunk, PR a `main`

---

## Decisiones administrativas pendientes

1. Verificar "Coralia" (App Store, Google Play, dominios, redes)
2. Cuentas: Apple Developer, Google Play Console, Firebase, AdMob, AppLovin MAX, RevenueCat
3. Dominio (coralia.app o similar)
4. Redes (@coraliagame)

---

## Riesgos abiertos

1. **Sin playtest externo.** Ya hay un juego que se puede poner en manos de alguien, así que el
   riesgo pasó de "no se puede probar" a "no se ha probado".
2. **Economía sin cerrar.** Los precios existen pero no hay forma de ganar monedas, así que el
   balance no se puede validar ni jugando.
3. **Solo dev sin equipo de arte.** Mitigado en parte: ya hay arte propio exportado.
4. **Documentación que se desincroniza del código.** Esta revisión corrigió un documento que
   afirmaba que el gameplay no existía. Mitigación: actualizar al cerrar cada chunk.
5. **Saturación del género.** La diferenciación sigue dependiendo del cozy submarino, la narrativa
   y el santuario.

---

## Cómo medir éxito

Ver GDD y Plan Maestro para los KPIs por fase (D1/D7/D30 retention, ARPDAU, conversion rate, LTV
target $1.50-$3.50).
