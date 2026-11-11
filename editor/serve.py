#!/usr/bin/env python3
"""Taller de Niveles — servidor local.

    python3 editor/serve.py

Abre http://localhost:8723 y sirve el editor leyendo los niveles y los catálogos del
proyecto. Guardar escribe el JSON directamente en Assets/Resources/Levels/Chapter_N/.

Va aparte del editor de Unity a propósito: aquel sigue siendo donde se calibra la
dificultad y se prueba jugando (LevelDifficultyEstimator, LevelShapeLibrary). Este es
para pintar, que es lo que una ventana de Unity hace peor que un navegador.

Solo escribe dentro de Assets/Resources/Levels, y solo archivos NNN.json. Nada más.
"""
import http.server, json, re, socketserver, sys, webbrowser
from pathlib import Path

HERE    = Path(__file__).resolve().parent
ROOT    = HERE.parent
ASSETS  = ROOT / "coralia" / "Assets"
LEVELS  = ASSETS / "Resources" / "Levels"
PORT    = 8723

# Nombres que se aceptan para escribir. Cualquier otra cosa se rechaza: el servidor
# escucha en la máquina de quien lo arranca, pero eso no es razón para aceptar rutas.
SAFE_FOLDER = re.compile(r"^Chapter_\d+$")
SAFE_FILE   = re.compile(r"^\d{3}\.json$")


def build():
    """Regenera catalog.json, levels.json y art/ desde los assets de Unity."""
    import subprocess
    subprocess.run([sys.executable, str(HERE / "extract.py"), str(HERE / "build")], check=True)


# De dónde sale el catálogo. Si algo de esto es más nuevo que lo extraído, hay que volver a
# leerlo: una criatura añadida al catálogo tiene que aparecer en el editor sin reiniciar nada.
SOURCES = [
    (ASSETS / "Resources",                  "*.asset"),
    (ASSETS / "Resources",                  "translations.csv"),
    (ASSETS / "Prefabs" / "UI" / "panels",  "TutorialPanel.prefab"),
    (ASSETS / "Scripts" / "Gameplay",       "*.cs"),
    (ASSETS / "Editor",                     "LevelDifficultyEstimator.cs"),
    (ASSETS / "Sprites",                    "**/*.png"),
]


def stale():
    """Si algún asset del que sale el catálogo cambió después de extraerlo."""
    built = HERE / "build" / "catalog.json"
    if not built.exists():
        return True

    when = built.stat().st_mtime
    for folder, pattern in SOURCES:
        for path in folder.glob(pattern):
            if path.stat().st_mtime > when:
                print(f"  catálogo viejo: {path.name} cambió, releyendo Unity…")
                return True
    return False


# El fuente del editor es el mismo archivo que se publica como artifact, y alli la
# plataforma le pone el <head> por encima. Servido a pelo no lo lleva, y sin charset el
# navegador adivina latin-1: "Obstáculo" sale como "ObstÃ¡culo".
SKELETON = """<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<style>
  :root { color-scheme: light; }
  body { margin: 0; font: 14px system-ui, sans-serif; }
  img { max-width: 100%%; }
  [hidden] { display: none !important; }
</style>
%s
</head>
<body>
%s
</body>
</html>
"""


def page():
    """Envuelve editor.html en un documento completo y lo deja en build/index.html."""
    src = (HERE / "editor.html").read_text(encoding="utf-8")

    # El <title>, los <link> de tipografia y el <style> van en la cabeza; el resto, al
    # cuerpo. Se parte por el cierre del bloque de estilos, que es donde acaba la cabeza.
    cut = src.index("</style>") + len("</style>")
    head, body = src[:cut], src[cut:]

    (HERE / "build").mkdir(exist_ok=True)
    (HERE / "build" / "index.html").write_text(SKELETON % (head, body), encoding="utf-8")


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **kw):
        super().__init__(*a, directory=str(HERE / "build"), **kw)

    # Solo se anotan las llamadas a la API: el resto es ruido. A str() porque un error
    # (el /favicon.ico que pide el navegador) llega con un HTTPStatus en vez de texto, y el
    # "in" reventaba el manejo de la peticion con un traceback por pantalla.
    def log_message(self, fmt, *args):
        if args and "api/" in str(args[0]):
            super().log_message(fmt, *args)

    # Sin charset declarado el navegador adivina, y adivina mal: los acentos y las
    # estrellas del arbol de niveles salen como mojibake.
    def guess_type(self, path):
        kind = super().guess_type(path)
        base = kind.split(";")[0]
        if base in ("text/html", "text/css", "text/javascript",
                    "application/javascript", "application/json", "text/plain"):
            return base + "; charset=utf-8"
        return kind

    # ---------------------------------------------------------------- lectura

    def do_GET(self):
        if self.path == "/api/catalog":
            # Se comprueba en cada arranque de página, no solo al levantar el servidor: una
            # criatura nueva en el catálogo tiene que salir recargando, no reiniciando.
            if stale():
                try:
                    build()
                except Exception as err:
                    print(f"  ! no se pudo releer Unity: {err}")
            return self.send_json(json.loads((HERE / "build" / "catalog.json").read_text("utf-8")))

        if self.path == "/api/levels":
            # Se leen del disco en cada arranque de pagina, no del paquete: asi un nivel
            # tocado a mano o por el editor de Unity aparece sin regenerar nada.
            out = []
            for path in sorted(LEVELS.rglob("*.json")):
                try:
                    out.append({"file": path.name, "folder": path.parent.name,
                                "data": json.loads(path.read_text("utf-8"))})
                except Exception as err:
                    print(f"  ! {path.name}: {err}")
            out.sort(key=lambda l: (l["data"].get("chapter", 0), l["data"].get("id", 0)))
            return self.send_json(out)

        if self.path == "/":
            self.path = "/index.html"
        return super().do_GET()

    # ---------------------------------------------------------------- escritura

    def do_POST(self):
        if self.path != "/api/save":
            return self.send_error(404)

        try:
            size = int(self.headers.get("Content-Length", 0))
            body = json.loads(self.rfile.read(size))

            folder, file, text = body["folder"], body["file"], body["text"]

            if not SAFE_FOLDER.match(folder):
                raise ValueError(f"carpeta no válida: {folder}")
            if not SAFE_FILE.match(file):
                raise ValueError(f"nombre no válido: {file} (se espera NNN.json)")

            json.loads(text)   # no se escribe un JSON roto sobre un nivel que funcionaba

            target = LEVELS / folder / file
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(text, encoding="utf-8")

            print(f"  guardado {folder}/{file}")
            return self.send_json({"ok": True, "path": f"{folder}/{file}"})

        except Exception as err:
            self.send_response(400)
            self.send_header("Content-Type", "text/plain; charset=utf-8")
            self.end_headers()
            self.wfile.write(str(err).encode("utf-8"))

    def send_json(self, data):
        raw = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(raw)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(raw)


if __name__ == "__main__":
    if not LEVELS.is_dir():
        sys.exit(f"No encuentro {LEVELS}. Ejecuta esto desde el repo de Coralia.")

    if "--no-build" not in sys.argv and stale():
        print("Leyendo catálogos y arte de Unity…")
        build()

    page()

    socketserver.TCPServer.allow_reuse_address = True
    with socketserver.TCPServer(("127.0.0.1", PORT), Handler) as httpd:
        url = f"http://localhost:{PORT}"
        print(f"\nTaller de Niveles en {url}")
        print("Guardar escribe en Assets/Resources/Levels/. Ctrl+C para salir.\n")
        webbrowser.open(url)
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\nHasta luego.")
