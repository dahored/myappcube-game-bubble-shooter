#!/usr/bin/env python3
"""Saca de los assets de Unity lo que el editor web necesita: los catalogos resueltos
a rutas de imagen, los sprites recortados y los 210 niveles en un solo archivo.

Tambien saca lo que NO es arte y el editor necesita para no inventarselo: que boosters
existen y cuales son de inicio, que tutoriales conoce TutorialPanel de verdad, y las
constantes de puntaje y de dificultad. Escritas a mano en el editor serian una segunda
copia de las reglas del juego, y la copia que se queda vieja es la que miente.

Se ejecuta a mano cuando cambie un catalogo. No toca nada del proyecto: solo lee.
"""
import csv, json, re, sys
from pathlib import Path
from PIL import Image

ASSETS = Path("/Users/daho/Projects/code/games/app-bubble-shooter/coralia/Assets")
OUT    = Path(sys.argv[1] if len(sys.argv) > 1 else "build")
ART    = OUT / "art"
SIZE   = 128           # el tablero dibuja a ~40 px; 128 aguanta pantallas densas

# El orden del enum BubbleColor, que es el indice que escribe BubbleCatalog.
COLORS = ["red", "blue", "yellow", "green", "purple", "orange", "pink", "grey",
          "brown", "black", "red_wine", "mint_green", "dark_blue", "dark_grey"]

ART.mkdir(parents=True, exist_ok=True)

# ---------------------------------------------------------------- traducciones

# La columna del espanol, que es el idioma en el que se disena. Con el modulo csv y no a
# regex: hay filas con comas dentro de las comillas.
ES = {}
with (ASSETS / "Resources" / "translations.csv").open(encoding="utf-8") as fh:
    for row in csv.reader(fh):
        if len(row) >= 2 and row[0]:
            ES[row[0].strip()] = row[1]

def tr(key, fallback=""):
    return ES.get(key) or fallback

# ---------------------------------------------------------------- guid -> archivo

guid_of = {}
for meta in ASSETS.rglob("*.png.meta"):
    text = meta.read_text(encoding="utf-8", errors="ignore")
    m = re.search(r"^guid: (\w+)", text, re.M)
    if m:
        guid_of[m.group(1)] = (meta.with_suffix(""), text)   # .png.meta -> .png

print(f"indexados {len(guid_of)} png")

# ---------------------------------------------------------------- recorte

def sprite_rect(meta_text, file_id):
    """El rectangulo del sub-sprite con ese internalID, o None si es la textura entera."""
    if str(file_id) == "21300000":
        return None
    # Cada sprite del sheet es un bloque con su rect y su internalID mas abajo.
    for block in meta_text.split("- serializedVersion:")[1:]:
        if f"internalID: {file_id}" not in block:
            continue
        r = re.search(r"rect:\s*\n\s*serializedVersion: \d+\s*\n\s*x: ([-\d.]+)\s*\n\s*y: ([-\d.]+)"
                      r"\s*\n\s*width: ([-\d.]+)\s*\n\s*height: ([-\d.]+)", block)
        if r:
            return tuple(int(float(v)) for v in r.groups())
    return None

cache = {}

def emit(guid, file_id, name):
    """Deja <name>.png en art/ y devuelve su ruta relativa."""
    key = (guid, str(file_id))
    if key in cache:
        return cache[key]
    if guid not in guid_of:
        print(f"  ! sin archivo para {name} (guid {guid})")
        return None

    png, meta_text = guid_of[guid]
    img = Image.open(png).convert("RGBA")

    rect = sprite_rect(meta_text, file_id)
    if rect:
        x, y, w, h = rect
        # Unity mide la Y desde abajo; PIL desde arriba.
        img = img.crop((x, img.height - y - h, x + w, img.height - y))

    img.thumbnail((SIZE, SIZE), Image.LANCZOS)
    img.save(ART / f"{name}.png", optimize=True)

    cache[key] = f"art/{name}.png"
    return cache[key]

# ---------------------------------------------------------------- catalogos

def entries_of(asset, key="entries:", first=r"id:|color:"):
    """Parte un .asset en bloques, uno por entrada de la lista 'key'."""
    text = (ASSETS / "Resources" / asset).read_text(encoding="utf-8")
    body = text.split(key, 1)[1] if key in text else ""
    return text, [b for b in re.split(rf"\n  - (?={first})", body)[1:]]

def first_ref(block, field):
    m = re.search(rf"{field}: {{fileID: (-?\d+), guid: (\w+)", block)
    return (m.group(2), m.group(1)) if m else None

catalog = {"colors": [], "materials": [], "creatures": [], "objects": [],
           "boosters": [], "tutorials": [], "recipes": []}

# --- burbujas de color
text, blocks = entries_of("BubbleCatalog.asset")
for block in blocks:
    idx = int(re.search(r"color: (\d+)", block).group(1))
    if idx >= len(COLORS):
        continue
    name = COLORS[idx]
    ref = first_ref(block, "frames:\n    - ") or re.search(
        r"frames:\s*\n\s*- {fileID: (-?\d+), guid: (\w+)", block)
    if hasattr(ref, "group"):
        ref = (ref.group(2), ref.group(1))
    if ref:
        catalog["colors"].append({"id": name, "art": emit(*ref, f"bubble_{name}")})

glow = re.search(r"sharedGlow: {fileID: (-?\d+), guid: (\w+)", text)
catalog["glow"] = emit(glow.group(2), glow.group(1), "glow") if glow else None

# --- materiales de caparazon
text, blocks = entries_of("ObstacleCatalog.asset")
for block in blocks:
    mid = re.search(r"id: (\w+)", block).group(1)
    layers = []
    seg = block.split("layers:", 1)[1].split("breakClip", 1)[0] if "layers:" in block else ""
    for n, m in enumerate(re.finditer(r"{fileID: (-?\d+), guid: (\w+)", seg), start=1):
        layers.append(emit(m.group(2), m.group(1), f"{mid}_{n}"))
    catalog["materials"].append({"id": mid, "name": tr(f"obstacle.{mid}.name", mid),
                                 "layers": [l for l in layers if l]})

# --- criaturas y objetos
# El prefijo de la clave de traduccion es el del catalogo (RescuableCatalog.KeyPrefix):
# las criaturas son creature.* y los objetos object.*.
for asset, key, prefix in [("CreatureCatalog.asset", "creatures", "creature"),
                           ("ObjectCatalog.asset", "objects", "object")]:
    text, blocks = entries_of(asset)
    for block in blocks:
        rid = re.search(r"id: (\S+)", block).group(1)
        ref = first_ref(block, "icon")
        catalog[key].append({"id": rid, "name": tr(f"{prefix}.{rid}.name", rid),
                             "art": emit(*ref, f"{prefix}_{rid}") if ref else None})

# --- recetas de burbuja de rescate
#
# El nivel puede nombrar una en vez de escribir a mano que lleva dentro y bajo que capas.
# Son pocas a proposito (una por combinacion que se use de verdad), pero el editor tiene
# que ofrecerlas o ese campo del JSON no se puede poner desde aqui.
_, recipes = entries_of("RescueBubbleCatalog.asset", key="recipes:")
for block in recipes:
    rid = re.search(r"id: (\S+)", block).group(1)
    holds = re.search(r"holds: *(\S*)", block)
    obst  = re.search(r"obstacle: *(\S*)", block)
    lay   = re.search(r"layers: *(\d+)", block)
    catalog["recipes"].append({
        "id": rid,
        "holds": holds.group(1) if holds else "",
        "obstacle": obst.group(1) if obst else "",
        "layers": int(lay.group(1)) if lay else 0,
    })

# --- boosters
#
# El numero que guarda el asset es el valor del enum, y el nombre que se escribe en el
# JSON sale de la tabla NAMES de BoosterRules. Se leen los dos del codigo en vez de
# copiarlos aqui: una tabla duplicada es una tabla que se queda vieja sin avisar.
enum_src  = (ASSETS / "Scripts" / "Gameplay" / "Booster.cs").read_text(encoding="utf-8")
rules_src = (ASSETS / "Scripts" / "Gameplay" / "BoosterRules.cs").read_text(encoding="utf-8")

values = {m.group(1): int(m.group(2))
          for m in re.finditer(r"^\s*(\w+)\s*=\s*(\d+),", enum_src, re.M)}
names  = {values[m.group(1)]: m.group(2)
          for m in re.finditer(r"\(Booster\.(\w+),\s*\"(\w+)\"\)", rules_src)
          if m.group(1) in values}

# BoosterCatalog.Family: InGame = 0, Start = 1. Solo los de inicio valen en el JSON del
# nivel — los de gameplay estan siempre en el HUD (GDD 3.5).
_, blocks = entries_of("BoosterCatalog.asset", first="booster:")
for block in blocks:
    num = int(re.search(r"booster: (\d+)", block).group(1))
    fam = int(re.search(r"family: (\d+)", block).group(1))
    bid = names.get(num)
    if not bid:
        print(f"  ! booster {num} sin nombre en BoosterRules.NAMES")
        continue
    ref = first_ref(block, "icon")
    catalog["boosters"].append({
        "id": bid,
        "name": tr(f"ui.booster.{bid}.name", bid),
        "start": fam == 1,
        "art": emit(*ref, f"booster_{bid}") if ref else None,
    })

# --- tutoriales
#
# Los que TutorialPanel conoce DE VERDAD. Escribir en un nivel un id que el panel no
# tiene no da error: Find devuelve null, Show devuelve false y no sale nada, en silencio.
prefab = (ASSETS / "Prefabs" / "UI" / "panels" / "TutorialPanel.prefab").read_text(encoding="utf-8")
tail = prefab.split("\n  entries:", 1)[1] if "\n  entries:" in prefab else ""
stop = re.search(r"\n  [a-zA-Z_]+:", tail)
tail = tail[:stop.start()] if stop else tail
for m in re.finditer(r"- id: (\S+)[ \t]*\n[ \t]*titleKey:[ \t]*(\S*)", tail):
    tid, key = m.group(1), m.group(2)
    catalog["tutorials"].append({"id": tid, "name": tr(key, tid)})

# ---------------------------------------------------------------- reglas

# Las constantes de puntaje y de dificultad, leidas del codigo. El editor estima igual que
# LevelDifficultyEstimator, asi que los umbrales que sugiere aqui son los mismos que
# escribe el menu de Unity. Copiadas a mano en JS, las dos estimaciones se separarian en
# cuanto Diego tocara una formula, y nadie lo notaria hasta jugar el nivel.
def consts(path, *names):
    src = (ASSETS / path).read_text(encoding="utf-8")
    out = {}
    for name in names:
        m = re.search(rf"\b{name}\s*=\s*([\d_]+(?:\.\d+)?)f?\s*;", src)
        if not m:
            print(f"  ! no encontre la constante {name} en {path}")
            continue
        raw = m.group(1).replace("_", "")
        out[name] = float(raw) if "." in raw else int(raw)
    return out

rules = {}
rules.update(consts("Scripts/Gameplay/ScoreRules.cs",
                    "POINTS_PER_POP_BASE", "POINTS_PER_DROP", "POINTS_PER_REMAINING_SHOT",
                    "STAR_3_RATIO_OF_BAR"))
rules.update(consts("Editor/LevelDifficultyEstimator.cs",
                    "MARGIN_HARD", "MARGIN_MEDIUM", "MARGIN_EASY",
                    "SUGGEST_HARD", "SUGGEST_MEDIUM", "SUGGEST_EASY",
                    "WASTE_PER_EXTRA_COLOR", "MISS_ALLOWANCE",
                    "BAR_STAR_1", "BAR_STAR_2", "BAR_STAR_3"))
rules.update(consts("Scripts/Gameplay/BoosterRules.cs", "SLOTS"))
rules.update(consts("Scripts/Gameplay/ObstacleCatalog.cs", "MAX_LAYERS"))

# El trio que se ofrece cuando el nivel no dice nada (BoosterRules.DEFAULT_ALLOWED).
default = re.search(r"DEFAULT_ALLOWED\s*=\s*{([^}]*)}", rules_src)
rules["default_boosters"] = ([names.get(values.get(x, -1), "") for x in
                              re.findall(r"Booster\.(\w+)", default.group(1))]
                             if default else [])

# El ritmo de cada decena (LevelDifficultyEstimator.TenLevelBeat), tal cual.
beat = re.search(r"TenLevelBeat\s*=\s*{(.*?)};",
                 (ASSETS / "Editor" / "LevelDifficultyEstimator.cs").read_text(encoding="utf-8"),
                 re.S)
rules["beat"] = re.findall(r"Difficulty\.(\w+)", beat.group(1)) if beat else []

catalog["rules"] = rules

# ---------------------------------------------------------------- capitulos

# Los nombres buenos son los de translations.csv, no los del GDD: el GDD lista otros.
catalog["chapters"] = {}
for key, value in ES.items():
    m = re.fullmatch(r"chapter\.(\d+)\.name", key)
    if m:
        catalog["chapters"][m.group(1)] = value

# ---------------------------------------------------------------- niveles

levels = []
for path in sorted((ASSETS / "Resources" / "Levels").rglob("*.json")):
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except Exception as err:
        print(f"  ! {path.name}: {err}")
        continue
    levels.append({"file": path.name,
                   "folder": path.parent.name,
                   "data": data})

levels.sort(key=lambda l: (l["data"].get("chapter", 0), l["data"].get("id", 0)))

(OUT / "levels.json").write_text(json.dumps(levels, ensure_ascii=False, separators=(",", ":")),
                                 encoding="utf-8")
(OUT / "catalog.json").write_text(json.dumps(catalog, ensure_ascii=False, indent=1),
                                  encoding="utf-8")

art_bytes = sum(p.stat().st_size for p in ART.glob("*.png"))
print(f"\ncolores {len(catalog['colors'])} · materiales {len(catalog['materials'])} · "
      f"criaturas {len(catalog['creatures'])} · objetos {len(catalog['objects'])}")
print(f"boosters {len(catalog['boosters'])} ({sum(1 for b in catalog['boosters'] if b['start'])} de inicio) · "
      f"tutoriales {len(catalog['tutorials'])} · recetas {len(catalog['recipes'])}")
print(f"niveles {len(levels)} ({(OUT/'levels.json').stat().st_size/1e6:.1f} MB)")
print(f"arte {len(list(ART.glob('*.png')))} archivos ({art_bytes/1e6:.1f} MB)")
