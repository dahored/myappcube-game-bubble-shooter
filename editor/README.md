# Taller de Niveles

Un editor de niveles que corre en el navegador y escribe directamente en
`coralia/Assets/Resources/Levels/`.

```bash
python3 editor/serve.py
```

Abre `http://localhost:8723` solo. **Guardar** escribe el JSON del nivel en el proyecto; no
hay que copiar ni pegar nada.

Requiere Python 3 y Pillow (`pip install pillow`), que es lo que recorta los sprites.

## Por qué existe, teniendo el de Unity

No lo reemplaza: se reparten el trabajo.

- **Este** es para **pintar**. Un tablero grande con el arte de verdad, deshacer de 150 pasos,
  espejado, zoom y los avisos de lo que no cuadra. Es lo que una ventana de Unity hace peor
  que un navegador.
- **El de Unity** (`Coralia → Editor de niveles`) es para **calibrar jugando**:
  `LevelDifficultyEstimator`, `LevelShapeLibrary` y el botón de probar el nivel en Play.

Los dos escriben el mismo JSON, así que se puede ir y volver.

## Qué se puede editar

Todo lo que el nivel lleva hoy, que es todo lo que `LevelData` sabe leer:

| | |
|---|---|
| **Tablero** | burbujas de color, obstáculos con material y capas, burbujas de rescate (con receta o a mano) |
| **Ficha** | nombre, id, capítulo, objetivo, disparos, mínimo óptimo, umbrales de estrellas, colores del nivel |
| **Tutoriales** | los que `TutorialPanel` conoce de verdad, con un contador de en cuántos niveles está ya cada uno |
| **Boosters** | los tres huecos de inicio, solo con los de esa familia |

## La pestaña Balance

Lleva portado el simulador de `LevelDifficultyEstimator`: disparos óptimos y realistas, margen
y dificultad contra la que toca por su sitio en la decena, colapso del layout, puntaje de una
partida buena y los umbrales de estrellas sugeridos.

**Las constantes no están escritas en el editor**: `extract.py` las lee del propio código del
juego (`ScoreRules`, `LevelDifficultyEstimator`, `BoosterRules`, `ObstacleCatalog`), así que
calcula con la misma fórmula que el menú *Recalcular umbrales* de Unity. Si Diego toca una
fórmula, no hay una segunda copia que se quede vieja sin avisar.

Igual que la de Unity, la simulación mira **solo `bubbles`**: ni los caparazones ni las burbujas
de rescate entran en la cuenta, así que en un nivel con obstáculos los disparos salen cortos.

## Las piezas

| | |
|---|---|
| `serve.py` | el servidor. Sirve la página, lee los niveles del disco en cada carga y escribe al guardar. Solo acepta `Chapter_N/NNN.json`: cualquier otra ruta se rechaza |
| `extract.py` | lee los `.asset`, el prefab de tutoriales, el CSV y los sprites de Unity, y deja en `build/` los catálogos resueltos, el arte recortado y los niveles |
| `editor.html` | la página entera, sin dependencias |
| `build/` | generado, ignorado por git. Se rehace solo cuando algún asset es más nuevo que lo extraído |

No hace falta regenerar nada a mano: al pedir el catálogo, el servidor compara fechas y vuelve
a leer Unity si algo cambió. Una criatura nueva aparece recargando la página.

## Guardar fuera del servidor

La página también funciona suelta (publicada como artifact, por ejemplo). Ahí **Guardar** pide
la carpeta de niveles una vez y escribe con el permiso del navegador; si eso no está disponible,
copia el JSON al portapapeles.
