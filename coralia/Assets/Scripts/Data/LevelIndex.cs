using System;

// Lo mínimo que el mapa necesita saber de un nivel: qué número es, a qué capítulo pertenece y
// dónde está su archivo.
//
// El mapa dibuja nodos, no burbujas. De cada JSON de nivel usaba 'id' y 'chapter' — unos pocos
// bytes — pero para leerlos cargaba el archivo entero, y las burbujas son el 95% de su peso. Con
// 180 niveles eso son varios MB parseados al abrir el mapa, y con 500 deja de ser viable.
//
// El índice se genera desde el editor (Coralia -> Regenerar índice de niveles) y hay que
// regenerarlo al agregar, quitar o renumerar niveles.
[Serializable]
public class LevelIndexEntry
{
    public int    id;
    public int    chapter;
    public string name;

    // Ruta para Resources.Load, sin extensión: "Levels/Chapter_1/001". Se guarda en vez de
    // armarla con el id porque nada garantiza que el archivo se llame como su id, y una
    // suposición así falla en silencio el día que dejen de coincidir.
    public string path;
}

[Serializable]
public class LevelIndex
{
    public LevelIndexEntry[] levels;
}
