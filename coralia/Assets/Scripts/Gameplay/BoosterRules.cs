using System.Collections.Generic;
using UnityEngine;

// Las reglas de los boosters, en un solo lugar. Mismo criterio que ScoreRules.
//
// El radio NO es un campo del Inspector a propósito. Lo necesitan tres sitios —la explosión
// (GameplayController), la zona que se marca al apuntar (TrajectoryLine) y la demostración del
// tutorial (TutorialBombDemo)— y los tres tienen que decir lo mismo: si la mira promete un
// hexágono y la explosión hace otro, el jugador deja de confiar en la mira. Con un campo por
// componente eso se arregla acordándose de cambiar los tres, que es justo lo que no pasa.
public static class BoosterRules
{
    // Cuántos boosters se ofrecen en la pantalla previa. Fijo, no una fila que crece: tres huecos
    // siempre en el mismo sitio se reconocen de un vistazo, y coincide con el cap de equipados del
    // GDD §3.5. Un nivel que ofrezca menos deja los que sobran con candado.
    public const int SLOTS = 3;

    // Cuántos anillos hexagonales revienta la Bomba de Coral alrededor del impacto.
    //   radio 1 = la celda + sus 6 vecinas =  7 burbujas
    //   radio 2 = un anillo más            = 19 burbujas  (definido por Diego)
    // Contra la pared del tablero el disco se recorta solo.
    public const int BOMB_RADIUS = 2;

    // Cuántas burbujas comodín da la Perla Arcoíris en todo el nivel, contando la primera.
    //
    // Una CANTIDAD y no una probabilidad: el jugador tiene que saber qué compra antes de pagar, y
    // "tres perlas" vale lo mismo en un nivel de 20 disparos que en uno de 45. Con un porcentaje,
    // el mismo precio compraba el doble en el nivel largo.
    //
    // El cuándo sí es al azar, repartido a lo largo de la partida — ver CannonController.RollColor.
    public const int RAINBOW_COUNT = 3;

    // Y la probabilidad de que caiga alguna DE MÁS en cada disparo, una vez repartidas las tres.
    //
    // Las tres son el piso, no el techo: sin esto el valor del booster se diluye en los niveles
    // largos —tres perlas en 45 disparos es una cada quince— y es justo donde el jugador más las
    // necesita. Con el extra, un nivel largo da más que uno corto, que es lo que corresponde.
    public const float RAINBOW_BONUS_CHANCE = 0.05f;

    // Cuántos se regalan al presentar un booster por primera vez. DOS y no uno: el primero se
    // carga solo en el cañón para que el jugador lo dispare sin tener que descubrir el ícono del
    // HUD, y el segundo queda en el inventario para que pueda volver a usarlo cuando ÉL quiera
    // — que es cuando de verdad se aprende para qué sirve. Con uno solo, el único uso sería el
    // que el juego le puso en la mano.
    public const int TUTORIAL_GRANT = 2;

    // Qué celdas se lleva un poder al impactar en 'cell'. Lista vacía = no revienta nada por área.
    //
    // Es UN solo sitio y no un radio porque no todos los poderes tienen forma de disco: la Raya Eléctrica
    // barre una fila y no hay radio que la describa. Lo consultan la marca de la mira y la
    // explosión de verdad, así que lo que se promete al apuntar y lo que pasa al disparar no
    // pueden separarse — si se separan, el jugador deja de confiar en la mira.
    //
    // Las celdas se filtran contra el tablero en HexGridMath, así que contra la pared el efecto se
    // recorta solo en vez de devolver celdas que no existen.
    // 'struck' es la burbuja CONTRA la que chocó el disparo, que no es donde acaba posado: la
    // burbuja se pega en el hueco de al lado, casi siempre una fila más abajo. Los poderes que
    // afectan a una zona alrededor del impacto ignoran el dato; la Raya Eléctrica no puede, porque esa
    // fila de abajo suele estar vacía y barrerla no hacía nada (reportado por Diego). Tampoco
    // sirve deducirla del tablero: una sola burbuja suelta en la fila de abajo bastaba para que
    // la heurística se quedara ahí en vez de subir a la hilera a la que el jugador apuntaba.
    //
    // Nulo = no se golpeó ninguna burbuja (el techo, o un tutorial que no tiene tablero): se
    // resuelve sobre la celda de aterrizaje, que es lo correcto en los dos casos.
    // 'board' solo lo miran los poderes cuya forma depende de lo que HAY en el tablero y no solo
    // de dónde pegaron. Sin él devuelven lo que puedan: la bomba y la raya, su forma entera; el
    // la Madre, la burbuja que tocó y nada más.
    public static List<Vector2Int> CellsHitBy(Booster booster, Vector2Int cell, Vector2Int? struck = null, IBoardView board = null) => booster switch
    {
        Booster.Bomb        => HexGridMath.CellsWithinRadius(cell, BOMB_RADIUS),
        Booster.ElectricRay => HexGridMath.CellsInRowFrom((struck ?? cell).y, (struck ?? cell).x),
        Booster.Mother      => MotherCells(struck ?? cell, board),
        _                   => new List<Vector2Int>(),
    };

    // De qué partes del catálogo tira cada poder. Sirve para que el Inspector no enseñe ajustes
    // que ese poder no va a mirar nunca — el contagio en la bomba, el zumbido de recámara en uno
    // que no se carga.
    //
    // Vive AQUÍ y no en el editor porque aquí ya está todo lo que hay que escribir para un poder
    // nuevo: su nombre, sus celdas, su familia. Un archivo de editor aparte sería un cuarto sitio
    // que recordar, y el que se olvidaría.
    //
    // El default es TODO lo normal a propósito: olvidarse de añadir un poder a esta tabla enseña
    // ajustes de más, que se ve y se arregla. Esconder uno que sí hacía falta no se ve.
    [System.Flags]
    public enum Uses
    {
        None   = 0,
        Bubble = 1 << 0,   // aparece dibujado en el tablero
        Loaded = 1 << 1,   // se queda cargado en la recámara esperando el disparo
        Burst  = 1 << 2,   // estalla al impactar
        Claim  = 1 << 3,   // tiñe las burbujas antes de que estallen
    }

    const Uses CANNON = Uses.Bubble | Uses.Loaded | Uses.Burst;

    public static Uses UsesOf(Booster booster) => booster switch
    {
        // Reparte comodines por el nivel: se dibujan en el tablero y contagian su color al
        // estallar, pero nunca se queda cargado esperando — se aplica solo al abrir.
        Booster.Rainbow => Uses.Bubble | Uses.Burst | Uses.Claim,

        _ => CANNON,
    };

    // Qué marca la MIRA, que no siempre es todo lo que el poder se va a llevar.
    //
    // La Madre elige sus dos objetivos sola. Enseñarlos antes de disparar convierte el poder en
    // un cálculo —el jugador comprueba si le conviene el reparto y si no, apunta a otro sitio— y
    // lo que tiene que ser es una sorpresa que se entiende al verla ocurrir. Lo que sí se promete
    // es dónde va a pegar, que es lo único que el jugador decide.
    public static List<Vector2Int> CellsAimedBy(Booster booster, Vector2Int cell, Vector2Int? struck = null, IBoardView board = null) =>
        booster == Booster.Mother
            ? new List<Vector2Int> { struck ?? cell }
            : CellsHitBy(booster, cell, struck, board);

    // Cuántas burbujas sueltas sale a buscar la Madre, además de la que toca.
    public const int MOTHER_TARGETS = 2;

    // La Burbuja Madre lleva tres dentro y al impactar las suelta: una revienta la que tocó y las
    // otras dos salen a por las burbujas MÁS AISLADAS de ese mismo color.
    //
    // Aisladas y no cercanas, que es lo que la salva de ser un poder malo: yendo a las próximas
    // sería una bomba pequeña, y la bomba ya se lleva diecinueve. Yendo a las que menos compañía
    // de su color tienen, ataca justo lo que el jugador no puede resolver por su cuenta —esas
    // tres sueltas en esquinas distintas que nunca va a juntar— y no necesita entender la regla:
    // ve que las hijas van a buscar las sueltas.
    static List<Vector2Int> MotherCells(Vector2Int struck, IBoardView board)
    {
        var result = new List<Vector2Int>();

        if (board == null || !board.TryGetColor(struck, out _))
        {
            result.Add(struck);
            return result;
        }

        // La burbuja que toca revienta CON SU RACIMO, como un match normal. Llevarse solo esa
        // dejaba al poder en cuatro burbujas por el precio de una bomba que se lleva diecinueve
        // — y además contradecía lo que dice que hace: si viene a juntar un color disperso, tiene
        // que llevarse también el montón que sí estaba junto (pedido de Diego).
        //
        // Sin mínimo de tres: lo que el jugador compró es que esto reviente, no un match.
        result.AddRange(GroupAt(struck, board));

        // Y las hijas van a las más solas DEL TABLERO, de cualquier color (pedido de Diego).
        //
        // Atarlas al color que se tocó las dejaba sin nada que hacer justo cuando más falta
        // hacen: al final del nivel lo que queda son restos sueltos de varios colores, y con el
        // filtro por color el poder era débil exactamente ahí. Que la hija salga del color de su
        // objetivo ya mantiene legible a dónde va cada una, así que no se pierde nada.
        // Solo valen las que van a SEGUIR AHÍ cuando la hija llegue. El racimo que la Madre
        // revienta puede desprender lo que colgaba de él, y una hija mandada a una de esas
        // aterriza en el hueco donde ya no hay nada (reportado por Diego).
        var standing = StandingWithout(board, result);

        var ranked = new List<(Vector2Int cell, int friends, int far)>();

        foreach (var other in standing)
        {
            if (!board.TryGetColor(other, out var otherColor)) continue;

            // Un comodín suelto no es un resto que estorbe: es la pieza que desatasca el tablero,
            // y gastar una hija en él sería quitarle al jugador su mejor carta.
            if (otherColor == BubbleColor.Rainbow) continue;

            ranked.Add((other, FriendsOf(other, board, otherColor), (other - struck).sqrMagnitude));
        }

        // Menos compañía primero. A igualdad, la más LEJOS: entre dos burbujas igual de solas, la
        // del otro extremo es la que el jugador no iba a alcanzar nunca, y además se ve viajar.
        ranked.Sort((a, b) =>
        {
            int byFriends = a.friends.CompareTo(b.friends);
            if (byFriends != 0) return byFriends;

            int byFar = b.far.CompareTo(a.far);
            if (byFar != 0) return byFar;

            // Desempate por posición. No decide nada de juego, pero hace la elección REPETIBLE:
            // la mira y la explosión son dos llamadas distintas, y si el orden dependiera del
            // recorrido del diccionario podrían señalar burbujas diferentes.
            return a.cell.y != b.cell.y ? a.cell.y.CompareTo(b.cell.y) : a.cell.x.CompareTo(b.cell.x);
        });

        // Se cuentan las ELEGIDAS, no el tamaño de la lista: con el racimo dentro, 'result' ya
        // pasa de dos antes de empezar y las hijas no salían nunca.
        var chosen = new List<Vector2Int>();

        // Dos objetivos pegados son el MISMO problema, no dos: la Madre acabaría haciendo de bomba
        // pequeña en un rincón, que es justo lo que no tiene que ser. Primera pasada exigiendo que
        // cada uno esté lejos de los otros y del racimo que ya revienta.
        foreach (var (cell, _, _) in ranked)
        {
            if (chosen.Count == MOTHER_TARGETS) break;
            if (FarFromAll(cell, result) && FarFromAll(cell, chosen)) chosen.Add(cell);
        }

        // Y si el tablero no da para tanto, se completa con las mejores que queden. Media ayuda
        // es mejor que ninguna, y dejar hijas sin salir se vería como que el poder falló.
        foreach (var (cell, _, _) in ranked)
        {
            if (chosen.Count == MOTHER_TARGETS) break;
            if (!chosen.Contains(cell)) chosen.Add(cell);
        }

        // Al final, que es de donde TravelersOf las saca.
        result.AddRange(chosen);

        return result;
    }

    // A cuántos diámetros de burbuja tienen que estar los objetivos entre sí. Dos y pico es lo
    // justo para que no compartan vecinos: por debajo, reventar uno ya deja al otro suelto.
    const float MOTHER_SPREAD = 2.5f;

    static bool FarFromAll(Vector2Int cell, List<Vector2Int> chosen)
    {
        Vector2 at    = HexGridMath.CellToLocalPos(cell);
        float   apart = HexGridMath.BubbleDiameter * MOTHER_SPREAD;

        foreach (var other in chosen)
            if (Vector2.Distance(at, HexGridMath.CellToLocalPos(other)) < apart) return false;

        return true;
    }

    // Qué sigue colgando del techo si se quitan esas celdas. Mismo criterio que la caída del
    // tablero —una burbuja se sostiene por su cadena de vecinos hasta la fila 0— calculado aquí
    // porque hay que saberlo ANTES de reventar nada, para no elegir un objetivo que se va a caer
    // solo por el camino.
    static List<Vector2Int> StandingWithout(IBoardView board, List<Vector2Int> removed)
    {
        var gone    = new HashSet<Vector2Int>(removed);
        var visited = new HashSet<Vector2Int>();
        var queue   = new Queue<Vector2Int>();

        foreach (var cell in board.Occupied)
            if (cell.y == 0 && !gone.Contains(cell) && visited.Add(cell)) queue.Enqueue(cell);

        var standing = new List<Vector2Int>();

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            standing.Add(cell);

            foreach (var neighbor in HexGridMath.GetNeighbors(cell))
            {
                if (gone.Contains(neighbor) || !board.TryGetColor(neighbor, out _)) continue;
                if (visited.Add(neighbor)) queue.Enqueue(neighbor);
            }
        }

        return standing;
    }

    // El racimo entero al que pertenece esa celda, por color. Mismo criterio que el match del
    // tablero —LinksWith, con el comodín enlazando en las dos direcciones— para que lo que la
    // Madre se lleva sea exactamente lo que el jugador habría reventado con un disparo normal.
    static List<Vector2Int> GroupAt(Vector2Int start, IBoardView board)
    {
        var group   = new List<Vector2Int> { start };
        var visited = new HashSet<Vector2Int> { start };
        var queue   = new Queue<Vector2Int>();

        queue.Enqueue(start);
        board.TryGetColor(start, out var color);

        while (queue.Count > 0)
            foreach (var neighbor in HexGridMath.GetNeighbors(queue.Dequeue()))
            {
                if (!visited.Add(neighbor)) continue;
                if (!board.TryGetColor(neighbor, out var other) || !other.LinksWith(color)) continue;

                group.Add(neighbor);
                queue.Enqueue(neighbor);
            }

        return group;
    }

    // Cuántas de las celdas que devuelve CellsHitBy se alcanzan A DISTANCIA, o sea cuántas hijas
    // salen a buscarlas. Van al FINAL de la lista, que es lo que permite decirlo con un número.
    //
    // Hace falta un dato y no una medida de "está lejos": el racimo de la Madre puede estirarse
    // varias burbujas, y por distancia a secas le habría salido una hija a la otra punta de su
    // propio racimo.
    public static int TravelersOf(Booster booster) => booster == Booster.Mother ? MOTHER_TARGETS : 0;

    // Cuántos vecinos de su mismo color tiene. Es la medida de "qué tan sola está": cero vecinos
    // es una burbuja que no se puede juntar con nada desde donde está. Aquí sí vale LinksWith —
    // un comodín al lado la acompaña de verdad, porque con él SÍ puede hacer match.
    static int FriendsOf(Vector2Int cell, IBoardView board, BubbleColor color)
    {
        int count = 0;

        foreach (var neighbor in HexGridMath.GetNeighbors(cell))
            if (board.TryGetColor(neighbor, out var other) && other.LinksWith(color)) count++;

        return count;
    }

    // Qué booster explica cada tutorial. El id del tutorial que presenta un poder ES el nombre
    // del poder, así que no hay tabla que mantener: dos nombres para la misma cosa en el mismo
    // JSON del nivel ('tutorials' y 'allowed_boosters') es justo lo que se desincroniza.
    //
    // Sin aviso por consola a propósito: la mayoría de los tutoriales no presentan ningún booster
    // ('first_steps', 'rescue'), y para esos None es la respuesta correcta, no un error.
    //
    // Existe para poder REGALAR uno al explicarlo: un tutorial que enseña un poder que el jugador
    // no tiene lo deja leyendo sobre algo que no puede probar, y la primera impresión de un
    // booster es justo lo que decide si después lo compra.
    public static Booster BoosterForTutorial(string tutorialId) => Parse(tutorialId, warnIfUnknown: false);

    // Un poder que el jugador todavía no conoce se muestra bloqueado, aunque el nivel lo ofrezca.
    // El filtro va acá y no en cada JSON para que el diseñador de niveles no tenga que repetir en
    // 60 archivos una regla que es siempre la misma: el nivel dice qué ayudas tienen sentido para
    // su objetivo, no a partir de cuándo existen.
    static Booster Offered(Booster booster) => IsUnlocked(booster) ? booster : Booster.None;

    // El trío que se ofrece cuando el JSON del nivel no dice nada. Existe para que los niveles ya
    // escritos sigan funcionando sin editarlos uno por uno; un nivel que quiera otra cosa lo pone
    // en su 'allowed_boosters'.
    static readonly Booster[] DEFAULT_ALLOWED = { Booster.Bomb, Booster.None, Booster.None };

    // Siempre devuelve SLOTS posiciones, rellenando con None lo que el nivel no ofrezca — así
    // quien lo muestre no tiene que decidir qué hacer con una lista corta: un hueco en None es
    // exactamente lo que el ítem dibuja como candado.
    public static Booster[] AllowedFor(LevelData level)
    {
        var slots = new Booster[SLOTS];

        var names = level?.allowed_boosters;
        if (names == null || names.Count == 0)
        {
            for (int i = 0; i < SLOTS; i++) slots[i] = Offered(DEFAULT_ALLOWED[i]);
            return slots;
        }

        for (int i = 0; i < names.Count; i++)
        {
            if (i >= SLOTS)
            {
                Debug.LogWarning($"[BoosterRules] El nivel {level.id} ofrece {names.Count} boosters y solo hay {SLOTS} huecos: '{names[i]}' no se va a ver.");
                continue;
            }

            slots[i] = Offered(Parse(names[i]));
        }

        return slots;
    }

    // Los nombres tal cual se escriben en el JSON, en UNA tabla y no en dos switches inversos:
    // Parse y NameOf son la misma correspondencia leída en direcciones opuestas, y mantenerlas
    // como dos listas es tener dónde desincronizarlas.
    //
    // Los nombres no se derivan del enum (booster.ToString()) para que el JSON no quede atado al
    // nombre en C#: renombrar el enum no debería obligar a reescribir 60 niveles.
    static readonly (Booster booster, string name)[] NAMES =
    {
        (Booster.Rainbow,     "rainbow"),
        (Booster.Bomb,        "bomb"),
        (Booster.ElectricRay, "electric_ray"),
        (Booster.Mother,      "mother"),
    };

    public static string NameOf(Booster booster)
    {
        foreach (var (candidate, name) in NAMES)
            if (candidate == booster) return name;

        return "";
    }

    // El aviso se apaga para quien pregunta por algo que LEGÍTIMAMENTE puede no ser un booster,
    // como el id de un tutorial.
    public static Booster Parse(string name, bool warnIfUnknown = true)
    {
        string clean = name?.Trim().ToLowerInvariant() ?? "";

        if (clean.Length == 0 || clean == "none") return Booster.None;

        foreach (var (booster, candidate) in NAMES)
            if (candidate == clean) return booster;

        if (warnIfUnknown) Debug.LogWarning($"[BoosterRules] '{name}' no es ningún booster conocido: el hueco se muestra bloqueado.");

        return Booster.None;
    }

    // El catálogo de arte, cargado una vez. Se guarda aunque venga nulo —con el bool aparte— para
    // no reintentar un Resources.Load fallido en cada refresco de cada ícono.
    static BoosterCatalog _catalog;
    static bool           _catalogLoaded;

    public static BoosterCatalog Catalog
    {
        get
        {
            if (_catalogLoaded) return _catalog;

            _catalogLoaded = true;
            _catalog       = Resources.Load<BoosterCatalog>(BoosterCatalog.RESOURCE_PATH);

            if (_catalog == null)
                Debug.LogWarning($"[BoosterRules] No hay '{BoosterCatalog.RESOURCE_PATH}' en Resources: los boosters se van a ver sin arte.");

            return _catalog;
        }
    }

    // Las claves de i18n se DERIVAN del nombre en vez de guardarse en el catálogo, igual que
    // LevelData.NameKey: son siempre la misma fórmula, y un campo por texto es un campo donde
    // escribir mal una clave y quedarse con el texto en crudo en pantalla.
    public static string NameKeyFor(Booster booster)        => $"ui.booster.{NameOf(booster)}.name";
    public static string DescriptionKeyFor(Booster booster) => $"ui.booster.{NameOf(booster)}.description";

    public static Sprite IconFor(Booster booster)         => Catalog != null ? Catalog.IconFor(booster)         : null;
    public static Sprite BubbleSpriteFor(Booster booster) => Catalog != null ? Catalog.BubbleSpriteFor(booster) : null;
    public static Sprite GlowFor(Booster booster)         => Catalog != null ? Catalog.GlowFor(booster)         : null;

    public static BoosterCatalog.Entry ArtFor(Booster booster) => Catalog != null ? Catalog.EntryFor(booster) : null;

    // De qué familia es. Sin entrada en el catálogo se asume de gameplay: un poder sin configurar
    // que aparece en el HUD se ve enseguida; uno que se aplicara solo al empezar no se vería nunca.
    public static BoosterCatalog.Family FamilyOf(Booster booster) =>
        ArtFor(booster)?.family ?? BoosterCatalog.Family.InGame;

    public static bool IsInGame(Booster booster) => FamilyOf(booster) == BoosterCatalog.Family.InGame;
    public static bool IsStart(Booster booster)  => booster != Booster.None && FamilyOf(booster) == BoosterCatalog.Family.Start;

    // Si el jugador ya conoce este poder. Un booster que aparece en la pantalla previa antes de
    // que nadie le haya explicado qué hace es un ícono que no significa nada — y encima con un
    // '+' que lleva a comprarlo.
    //
    // NO se mide por "¿le queda alguno?": gastar el último volvería a bloquearlo, y entonces no
    // habría forma de conseguir más porque el hueco ni siquiera se muestra. Una vez conocido,
    // sin existencias se ve el '+', que es justo el camino para reponerlo.
    //
    // El tutorial visto cuenta además del bit de SaveManager para cubrir un tutorial que explique
    // un poder sin regalarlo. Hoy siempre regala, pero el id del tutorial ES el nombre del
    // booster, así que la comprobación sale gratis.
    public static bool IsUnlocked(Booster booster)
    {
        if (booster == Booster.None) return false;

        return SaveManager.IsBoosterUnlocked(booster) || SaveManager.HasSeenTutorial(NameOf(booster));
    }
}
