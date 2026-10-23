using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LevelData
{
    public int id;
    public int chapter;

    // El nombre EN ESPAÑOL, que es lo que se escribe en el editor de niveles. No es el que va a
    // pantalla: para mostrarlo se usa DisplayName, que busca la traducción.
    public string name;

    // Clave de traducción derivada del id, así los 180 JSON no tienen que repetirla. La exporta
    // a translations.csv el menú Coralia → Exportar nombres de nivel a traducciones.
    public string NameKey => $"level.{id}.name";

    // Lo que se muestra al jugador. Mientras la clave no esté en el CSV cae al nombre en
    // español, así que traducir de a poco nunca deja una pantalla en blanco ni con la clave cruda.
    public string DisplayName => LocaleManager.Get(NameKey, name);
    public int max_shots;
    public int min_shots_to_clear;    // mínimo de disparos jugando óptimo (definido a mano por el
                                       // diseñador al calibrar el nivel, jugándolo — 0 = todavía no
                                       // calibrado). max_shots - min_shots_to_clear es el margen real
                                       // que determina si el nivel es fácil/difícil, y de ahí salen
                                       // los star_thresholds — ver GameplayController.CalculateStars.
    // SIN USO. La arcoíris salía del cañón con esta probabilidad, pero como burbuja no funciona:
    // conecta con cualquier color, así que un solo disparo se llevaba el grid entero. Pasa a ser
    // un booster. El campo sigue acá porque está escrito en los 180 JSON; el día que exista el
    // booster se borra de todos de una vez.
    public float rainbow_chance;
    public List<string> available_colors;
    public List<int> star_thresholds; // [1 estrella, 2 estrellas, 3 estrellas]
    public List<string> obstacles;
    // Qué tutoriales explica este nivel, por id (los mismos que lista TutorialPanel). Van en el
    // JSON y no en código porque son una decisión de diseño de niveles: qué mecánica aparece en
    // cuál. El panel ya se encarga de no repetirlos, así que escribir el mismo id en varios
    // niveles es correcto — sale en el primero al que el jugador llegue.
    public List<string> tutorials;

    // Qué boosters DE INICIO se ofrecen en la pantalla previa de este nivel, por nombre
    // ("rainbow", ...), y en el orden en que se muestran. Decisión de diseño de niveles igual que
    // los tutoriales: qué ayudas tienen sentido para este objetivo. Un nivel de rescate no ofrece
    // lo mismo que uno de limpiar el tablero.
    //
    // Solo los de INICIO, y por eso el nombre lo dice: son los que se pagan por partida y hay que
    // elegir tres. Los de gameplay no se limitan por nivel (GDD §3.5) — están todos en el HUD
    // siempre, y lo único que los retiene es su candado hasta que el jugador sepa qué hacen. Un
    // nombre de gameplay escrito acá se descarta con un aviso por consola.
    //
    // Opcional: vacío o ausente usa el trío por defecto (BoosterRules.AllowedFor), así los JSON
    // ya escritos no hay que tocarlos. Los huecos salen con candado.
    public List<string> allowed_boosters_start;

    public ObjectiveData objective;
    public List<BubbleEntry> bubbles;
}

[Serializable]
public class ObjectiveData
{
    public string type;               // "clear_all" | "rescue"

    // Las burbujas de rescate del nivel: dónde va cada una y con qué receta. Lista PLANA, sin
    // agrupar por criatura: la receta ya dice qué lleva dentro, y escribirlo otra vez al lado
    // sería poder contradecirse.
    //
    // El hub las agrupa solo por lo que llevan, así que tres coquíes en tres recetas distintas
    // salen en un único hueco con un 3. Tres criaturas distintas, en tres huecos (GDD §2.2).
    public List<CellPos> rescues;

    // --- Forma vieja, de cuando el rescate era una sola criatura ---
    //
    // Se siguen leyendo porque están escritos en los JSON de prueba y porque el editor de niveles
    // todavía los produce. Rescues() los normaliza, así que el resto del código no sabe por cuál
    // de las dos formas vino.
    public string creature_id;
    public List<int> creature_position; // [fila, col]

    // Las dos formas, resueltas en una sola lista. Vacía si este nivel no rescata nada.
    public List<CellPos> Rescues()
    {
        if (rescues != null && rescues.Count > 0) return rescues;

        if (creature_position == null || creature_position.Count != 2) return new List<CellPos>();

        return new List<CellPos>
        {
            new() { row = creature_position[0], col = creature_position[1], holds = creature_id },
        };
    }
}

// Una burbuja de rescate puesta en el tablero.
//
// No hace falta que haya una entrada suya en 'bubbles': esta burbuja NO es de color, se crea
// desde el objetivo. Escribirla en los dos sitios sería poder contradecirse.
[Serializable]
public class CellPos
{
    public int row;
    public int col;

    // Qué receta del RescueBubbleCatalog usa. Es lo normal: la receta trae qué lleva dentro, el
    // fondo, el brillo y las capas, así que el nivel solo dice dónde va.
    public string bubble;

    // Cómo SE VE: su cuerpo es el de ese color del BubbleCatalog. Solo apariencia.
    //
    // Vacío no dibuja cuerpo — lo que se ve es el fondo de la receta, la criatura y el brillo.
    public string color;

    // Con quién MATCHEA. Es lo que decide cómo se juega, y va aparte del color a propósito: una
    // criatura puede verse verde y encajar con las verdes, o verse verde y abrirse con cualquier
    // match. Son dos preguntas distintas y mezclarlas obligaba a elegir las dos a la vez.
    //
    //   con match  — encaja solo con ese color, como una burbuja normal.
    //   sin match  — la abre CUALQUIER match que ocurra a su lado, sea del color que sea.
    //
    // Y en los dos casos las capas son golpes: un match que la alcanza le quita una en vez de
    // llevársela.
    public string match;

    // Qué lleva dentro, escrito a mano. Es la EXCEPCIÓN: sirve para una burbuja suelta sin
    // receta, y escrito manda sobre la de la receta.
    [CreatureId]
    public string holds;

    public const int MAX_LAYERS = 3;

    public Vector2Int Cell => new(col, row);

    public RescueBubbleCatalog.Recipe Recipe => RescueBubbleCatalog.Get(bubble);

    // Los dos colores, ya parseados. Null cuando no se declaró ninguno.
    public BubbleColor? Color() => Parse(color, "color");
    public BubbleColor? Match() => Parse(match, "match");

    static BubbleColor? Parse(string value, string campo)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        try   { return BubbleColorExtensions.Parse(value); }
        catch { Debug.LogWarning($"[LevelData] Una burbuja de rescate pide '{value}' en '{campo}' y ese color no existe: se ignora."); return null; }
    }

    // Qué lleva dentro, resuelto. En un solo sitio para que no haya dos respuestas a la misma
    // pregunta: con la mezcla hecha en dos sitios, el tablero y el hub podrían mostrar cosas
    // distintas de la misma celda.
    public string Holds()
    {
        if (!string.IsNullOrEmpty(holds)) return holds;

        return Recipe?.holds ?? "";
    }
}

[Serializable]
public class BubbleEntry
{
    public int row;
    public int col;
    public string color;
}
