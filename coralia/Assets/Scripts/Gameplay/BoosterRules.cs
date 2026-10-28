using System.Collections.Generic;
using System.Linq;
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

    // Cuántas burbujas comodín manda la Perla Arcoíris POR LA COLA del cañón, contando la primera
    // (que es la que queda en la recámara). Aparte de estas, siembra en el tablero — ver SeedCells.
    //
    // Una CANTIDAD y no una probabilidad: el jugador tiene que saber qué compra antes de pagar, y
    // "tres perlas" vale lo mismo en un nivel de 20 disparos que en uno de 45. Con un porcentaje,
    // el mismo precio compraba el doble en el nivel largo.
    //
    // El cuándo sí es al azar, repartido a lo largo de la partida — ver CannonController.RollColor.
    public const int RAINBOW_COUNT = 3;

    // Y cuántas burbujas YA PUESTAS en el tablero convierte en comodín al abrir el nivel.
    //
    // Es la otra mitad del poder, y la que se ve: las tres de la cola llegan de a una a lo largo
    // de la partida, así que al pagar no pasa nada en pantalla. Sembrando el tablero, el jugador
    // ve en el acto qué compró y además puede PLANEAR con ellas a la vista, que es lo que la cola
    // no deja hacer (pedido de Diego).
    public const int SEEDS_PER_BAND = 3;

    // Y la banda sobre la que se cuentan. La siembra no es un total repartido por el tablero
    // entero: son TRES CADA DIEZ FILAS, contadas desde la primera ocupada hacia abajo.
    //
    // Con un total a secas, el azar las amontonaba donde más burbujas hubiera —casi siempre
    // arriba— y la mitad de abajo de un tablero largo se quedaba sin nada. Por bandas, cada tramo
    // de diez filas se lleva lo suyo, y un tablero el doble de alto da el doble (pedido de Diego).
    public const int BAND_ROWS = 10;

    // La banda incompleta se lleva su parte proporcional, redondeada. Diez filas dan tres, cinco
    // dan dos, dos filas dan una y una sola fila no da ninguna: un resto tan corto no llega a
    // valer una semilla entera.
    public static int SeedsForRows(int rows) =>
        Mathf.FloorToInt(rows / (float)BAND_ROWS * SEEDS_PER_BAND + 0.5f);

    // A cuántos diámetros se intenta dejarlas entre sí. Tres comodines pegados se gastan en un
    // solo match y valen por uno — menos que repartidos, que es justo lo contrario de lo que el
    // jugador compró. Es una preferencia y no una regla: si el tablero no da, se siembra igual.
    const float SEED_SPREAD = 2f;

    // Qué burbujas del tablero se convierten, banda por banda. Al azar, pero no cualquier azar:
    // salteando las que ya son comodín o mina —convertir una no regala nada— y separándolas entre
    // sí dentro de su banda.
    //
    // Y por TODO el tablero, se vea o no: antes se prefería lo visible porque el destello es media
    // parte del regalo, pero con eso un tablero largo sembraba las tres en el primer pantallazo y
    // el resto del nivel no tenía ninguna (pedido de Diego).
    public static List<Vector2Int> SeedCells(IBoardView board)
    {
        var chosen = new List<Vector2Int>();

        if (board == null) return chosen;

        var pool = new List<Vector2Int>();

        foreach (var cell in board.Occupied)
            if (board.TryGetColor(cell, out var color) && color != BubbleColor.Rainbow) pool.Add(cell);

        if (pool.Count == 0) return chosen;

        // Barajar UNA vez para todas las bandas: así las dos pasadas de cada banda comparten el
        // mismo azar y la segunda completa con las que la primera descartó por juntas.
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        int top = int.MaxValue;
        foreach (var cell in pool) top = Mathf.Min(top, cell.y);

        var bands = new Dictionary<int, List<Vector2Int>>();

        foreach (var cell in pool)
        {
            int band = (cell.y - top) / BAND_ROWS;

            if (!bands.TryGetValue(band, out var list)) bands[band] = list = new List<Vector2Int>();
            list.Add(cell);
        }

        foreach (var band in bands.Values) chosen.AddRange(SeedBand(band));

        return chosen;
    }

    // Lo que le toca a una banda: su alto REAL manda, no el de la banda. Una última banda con dos
    // filas ocupadas no da lo mismo que una con nueve, aunque las dos sean "la banda incompleta".
    static List<Vector2Int> SeedBand(List<Vector2Int> pool)
    {
        var chosen = new List<Vector2Int>();

        int first = int.MaxValue, last = int.MinValue;

        foreach (var cell in pool)
        {
            first = Mathf.Min(first, cell.y);
            last  = Mathf.Max(last,  cell.y);
        }

        int count = SeedsForRows(last - first + 1);

        foreach (var cell in pool)
        {
            if (chosen.Count == count) break;
            if (FarFromAll(cell, chosen, SEED_SPREAD)) chosen.Add(cell);
        }

        // Y si la banda no daba para tanto, se completa con lo que haya. Sembrar dos juntas es
        // peor que separadas, pero sembrar una sola cuando se pagaron tres es un booster roto.
        foreach (var cell in pool)
        {
            if (chosen.Count == count) break;
            if (!chosen.Contains(cell)) chosen.Add(cell);
        }

        return chosen;
    }

    // Y la probabilidad de que caiga alguna DE MÁS en cada disparo, una vez repartidas las tres.
    //
    // Las tres son el piso, no el techo: sin esto el valor del booster se diluye en los niveles
    // largos —tres perlas en 45 disparos es una cada quince— y es justo donde el jugador más las
    // necesita. Con el extra, un nivel largo da más que uno corto, que es lo que corresponde.
    public const float RAINBOW_BONUS_CHANCE = 0.05f;

    // Cuántos disparos suma la Reserva de Oxígeno (GDD §3.2).
    //
    // Cinco y no un porcentaje del nivel: el jugador tiene que saber qué compra antes de pagar, y
    // "+5" se entiende sin mirar cuántos traía el nivel. Un porcentaje valdría el doble en el
    // nivel largo por el mismo precio, y además no se puede anunciar en una línea.
    public const int OXYGEN_SHOTS = 5;

    // Las filas de abajo del todo, que es lo que se lleva la Marea Baja (GDD §3.2).
    //
    // Las más bajas OCUPADAS, no las últimas del tablero: un nivel que no llega abajo no tiene por
    // qué gastar el poder en aire. Y se cuentan hacia arriba desde ahí, así que un tablero con
    // huecos no se come una fila de regalo.
    //
    // Ordenadas de abajo hacia arriba y, dentro de cada fila, por columna: ese es el orden en el
    // que el barrido las recorre, y es lo que lo hace leerse como una ola que se retira.
    //
    // Se lleva FILAS y no un número de burbujas porque lo que el jugador compra es ESPACIO contra
    // el cañón: lo que aprieta es cuánto ha bajado el tablero, no cuántas burbujas hay.
    public static List<Vector2Int> LowTideCells(IBoardView board, int rows)
    {
        var cells = new List<Vector2Int>();

        if (board == null || rows <= 0) return cells;

        int lowest = -1;
        foreach (var cell in board.Occupied) if (cell.y > lowest) lowest = cell.y;

        if (lowest < 0) return cells;

        // Las filas ocupadas de abajo hacia arriba, saltándose las vacías: contar a secas
        // 'lowest-1, lowest-2' regalaría filas en un tablero con huecos.
        var occupiedRows = new SortedSet<int>();
        foreach (var cell in board.Occupied) occupiedRows.Add(cell.y);

        var taken = new HashSet<int>();
        foreach (int row in occupiedRows.Reverse())
        {
            if (taken.Count == rows) break;
            taken.Add(row);
        }

        foreach (var cell in board.Occupied) if (taken.Contains(cell.y)) cells.Add(cell);

        cells.Sort((a, b) => a.y != b.y ? b.y.CompareTo(a.y) : a.x.CompareTo(b.x));

        return cells;
    }

    // En qué orden se aplican los poderes de inicio cuando el jugador trae varios.
    //
    // No es el orden en que los equipó: el orden CAMBIA el resultado, y hay uno que es el justo.
    //
    // El caso que lo destapó: con la Perla y la Marea Baja juntas, si la Perla sembraba primero,
    // la Marea se llevaba por delante los comodines recién puestos en las filas de abajo. El
    // jugador pagaba dos poderes y recibía uno y medio (reportado por Diego).
    //
    // La regla es que cada uno trabaje sobre el tablero DEFINITIVO:
    //
    //   1. los que QUITAN burbujas     — dejan el tablero con el que van a jugar los demás
    //   2. los que CONVIERTEN colores  — sobre lo que de verdad se queda
    //   3. los que SIEMBRAN            — lo último, para que nada se lleve lo recién puesto
    //   4. los que no tocan el tablero — da igual cuándo, van al final
    //
    // Un poder nuevo que no se nombre acá cae en el grupo 3, que es el más seguro: como mucho
    // siembra sobre un tablero que ya nadie va a tocar.
    public static int StartOrder(Booster booster) => booster switch
    {
        Booster.LowTide    => 0,
        Booster.ClearWater => 1,
        Booster.Rainbow => 2,
        Booster.Oxygen  => 3,
        _               => 2,
    };

    // A cuántas burbujas llega la Tinta de Pulpo, hasta dónde revienta y cuánto más allá busca a
    // las que pinta (GDD §3.2).
    public const int INK_SPLASH = 4;
    public const int INK_RADIUS = 2;

    // Cuántas salpica y hasta dónde busca, leídos del catálogo. El número sale por tres sitios —
    // el tablero, el tutorial y el texto que lo anuncia — y tienen que decir los tres lo mismo.
    public static int InkSplash => ArtFor(Booster.Ink)?.inkSplash > 0 ? ArtFor(Booster.Ink).inkSplash : INK_SPLASH;
    public static int InkRadius => ArtFor(Booster.Ink)?.inkRadius > 0 ? ArtFor(Booster.Ink).inkRadius : INK_RADIUS;

    // Lo que se lleva el Punto Débil: el racimo del color señalado —sin el mínimo de tres— más,
    // si el catálogo lo pide, un radio alrededor que va sin mirar el color.
    //
    // El radio por defecto es CERO y no es un descuido: con radio, este poder hace lo mismo que
    // la Bomba de Coral y además el match, así que la Bomba dejaría de tener sentido. Queda como
    // palanca para afinarlo, con el aviso puesto en el tooltip.
    public static int WeakSpotRadius => ArtFor(Booster.WeakSpot)?.weakSpotRadius ?? 0;

    static List<Vector2Int> WeakSpotCells(Vector2Int cell, IBoardView board)
    {
        var cells  = GroupAt(cell, board);
        int radius = WeakSpotRadius;

        if (radius <= 0) return cells;

        foreach (var around in HexGridMath.CellsWithinRadius(cell, radius))
            if (!cells.Contains(around)) cells.Add(around);

        return cells;
    }

    // Hasta dónde llega una mina plantada al estallar. Va aparte de BOMB_RADIUS porque son dos
    // poderes distintos: la Bomba se apunta y cuesta un disparo, la mina estalla sola cuando algo
    // la roza. Que cada una valga lo que vale se balancea moviendo su radio sin tocar la otra.
    public const int MINE_RADIUS = 2;

    public static int MineRadius => ArtFor(Booster.Mine)?.mineRadius > 0
                                  ? ArtFor(Booster.Mine).mineRadius : MINE_RADIUS;

    // El arte de la salpicadura, también en un solo sitio: lo piden el tablero y el tutorial, y
    // una mancha que no se parezca a la otra enseña el poder al revés.
    public const float INK_DROP_SIZE = 1.15f;

    public static Sprite InkDrop     => ArtFor(Booster.Ink)?.inkDrop != null
                                      ? ArtFor(Booster.Ink).inkDrop : SparkleTextures.Drop;
    public static float  InkDropSize => ArtFor(Booster.Ink)?.inkDropSize > 0f
                                      ? ArtFor(Booster.Ink).inkDropSize : INK_DROP_SIZE;

    // El color de la mancha: el que va a repartir, salvo que el catálogo mande otro. Un sprite ya
    // coloreado se tiñe de blanco desde el catálogo para que se vea tal cual.
    public static Color InkDropTint(BubbleColor color) =>
        ArtFor(Booster.Ink)?.inkDropColor.a > 0f ? ArtFor(Booster.Ink).inkDropColor : color.Tint();

    // Qué burbujas salpica la Tinta: cuatro al azar del radio del impacto.
    //
    // Al azar y no las más cercanas: eligiendo las de al lado dejaría siempre una mancha compacta
    // pegada al punto de impacto, que es una bomba pequeña con otro color. Repartidas, el jugador
    // tiene que mirar dónde cayeron y decidir cuál aprovecha — y cada tiro sale distinto.
    //
    // Se saltan los comodines: teñir uno le quita al jugador su mejor carta a cambio de un color
    // corriente, que es lo contrario de ayudar.
    public static List<Vector2Int> InkCells(Vector2Int center, IBoardView board, int count, int radius)
    {
        var pool = new List<Vector2Int>();

        if (board == null) return pool;

        foreach (var cell in HexGridMath.CellsWithinRadius(center, radius))
        {
            if (cell == center) continue;
            if (!board.TryGetColor(cell, out var color) || color == BubbleColor.Rainbow) continue;

            pool.Add(cell);
        }

        // Las del racimo que el impacto se lleva no se pintan: cuando la tinta llegue ya no van a
        // estar, y gastar salpicaduras en ellas dejaría la mancha más pobre de lo que se pagó.
        // Quien llame ya las habrá quitado del tablero si resolvió el golpe primero; esto cubre
        // el caso de que pregunte antes.

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        if (pool.Count > count) pool.RemoveRange(count, pool.Count - count);

        return pool;
    }

    // El racimo al que pertenece una burbuja, para que la gota que cae encima se lleve la cadena
    // entera (pedido de Diego).
    //
    // Es lo que convierte la Tinta en un poder y no en cuatro burbujas sueltas: cayendo sobre una
    // que está acompañada, el color se extiende por todas sus vecinas y el jugador se encuentra
    // un racimo listo para reventar.
    //
    // Sin comodines: el grupo puede pasar por uno —enlaza con todo— pero pintarlo le quitaría al
    // jugador su mejor carta a cambio de un color corriente.
    public static List<Vector2Int> InkChain(Vector2Int cell, IBoardView board)
    {
        var chain = new List<Vector2Int>();

        if (board == null) return chain;

        foreach (var member in GroupAt(cell, board))
            if (board.TryGetColor(member, out var color) && color != BubbleColor.Rainbow)
                chain.Add(member);

        // Desde donde cayó la gota hacia afuera: el color se extiende, no aparece de golpe.
        Vector2 from = HexGridMath.CellToLocalPos(cell);
        chain.Sort((a, b) => Vector2.Distance(from, HexGridMath.CellToLocalPos(a))
                     .CompareTo(Vector2.Distance(from, HexGridMath.CellToLocalPos(b))));

        return chain;
    }

    // Si este poder TIÑE en vez de reventar. La Tinta es el único hasta ahora, y el camino del
    // impacto necesita saberlo antes de empezar a quitar burbujas.
    public static bool Tints(Booster booster) => booster == Booster.Ink;

    // Qué dos colores toca el Agua Clara: el más escaso se convierte en el más abundante (GDD §3.2).
    //
    // Convierte y no borra. Borrar resolvería parte del objetivo solo, y dispararía la caída de
    // todo lo que colgara de esas burbujas — un efecto que nadie previó al balancear el nivel.
    // Convirtiéndolas, el tablero sigue igual de lleno pero queda un racimo grande que el jugador
    // revienta CON SU DISPARO: el mérito es suyo.
    //
    // Devuelve None en los dos si no hay nada que hacer: con un solo color en el tablero no hay
    // "el más escaso", y gastar el poder ahí sería cobrarle por nada.
    public static (BubbleColor from, BubbleColor to) ClearWaterColors(Dictionary<BubbleColor, int> counts)
    {
        if (counts == null || counts.Count < 2) return (BubbleColor.Rainbow, BubbleColor.Rainbow);

        var from = BubbleColor.Rainbow;
        var to   = BubbleColor.Rainbow;
        int few  = int.MaxValue;
        int many = -1;

        foreach (var pair in counts)
        {
            // Empates por el orden del enum, no por el del diccionario: la elección tiene que ser
            // la misma cada vez que se pregunte, y el recorrido de un Dictionary no lo garantiza.
            if (pair.Value < few  || (pair.Value == few  && pair.Key < from)) { few  = pair.Value; from = pair.Key; }
            if (pair.Value > many || (pair.Value == many && pair.Key < to))   { many = pair.Value; to   = pair.Key; }
        }

        return from == to ? (BubbleColor.Rainbow, BubbleColor.Rainbow) : (from, to);
    }

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

        // Lo que toca revienta CON SU RACIMO, como un match normal y sin mínimo de tres — igual
        // que la Madre. NO un área como la bomba: ese trabajo ya lo hace ella, y lo que esta
        // aporta es la tinta que reparte después (pedido de Diego).
        Booster.Ink         => GroupAt(struck ?? cell, board),
        Booster.Mine        => HexGridMath.CellsWithinRadius(cell, MineRadius),

        // Lo que tenga, sin el mínimo de tres: la gracia del poder es que vale para la burbuja
        // suelta que no se puede matchear de ninguna manera. Con el mínimo normal, justo el caso
        // que viene a resolver sería el único que no resuelve.
        Booster.WeakSpot    => WeakSpotCells(cell, board),
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
        Seed   = 1 << 4,   // convierte burbujas del tablero al abrir el nivel
        Tide   = 1 << 5,   // se lleva filas enteras por abajo al abrir el nivel
        Clear  = 1 << 6,   // pasa las burbujas de un color a otro al abrir el nivel
        Ink    = 1 << 7,   // salpica color sobre las burbujas que alcanza, sin reventarlas
        Mine   = 1 << 8,   // planta bombas sensibles en el tablero al abrir el nivel
        Pick   = 1 << 9,   // se usa eligiendo una burbuja del tablero, no disparando
    }

    const Uses CANNON = Uses.Bubble | Uses.Loaded | Uses.Burst;

    public static Uses UsesOf(Booster booster) => booster switch
    {
        // Reparte comodines por el nivel: se dibujan en el tablero y contagian su color al
        // estallar, pero nunca se queda cargado esperando — se aplica solo al abrir.
        Booster.Rainbow => Uses.Bubble | Uses.Burst | Uses.Claim | Uses.Seed,

        // Como los otros tres del cañón —se carga, vuela y revienta el racimo que toca— más lo
        // suyo propio: las gotas que salen después. Su 'Burst Clip' es el cristal quebrándose.
        Booster.Ink => CANNON | Uses.Ink,

        // No dibuja ninguna burbuja ni estalla en ningún sitio: solo sube el contador de tiros al
        // abrir. Todo lo que el Inspector enseñaría de él serían campos que no lee nadie.
        Booster.Oxygen => Uses.None,

        // Tampoco se carga ni produce burbuja, pero SÍ estalla: se lleva varias filas enteras y
        // eso necesita su sonido, su destello y su temblor, además de lo suyo propio.
        Booster.LowTide => Uses.Burst | Uses.Tide,

        // Este no revienta nada: convierte. Solo sus propios ajustes.
        Booster.ClearWater => Uses.Clear,

        // No se carga ni dibuja burbuja: se usa señalando el tablero. Estalla donde señales, así
        // que sí necesita su sonido, su destello y su temblor.
        Booster.WeakSpot => Uses.Burst | Uses.Pick,

        // Siembra como la Perla y estalla como la Bomba, pero ninguna de las dos cosas pasa
        // cuando se dispara: planta al abrir, y lo que estalla después son las minas, cada una
        // por su cuenta. Por eso Seed y Burst sin Loaded — nunca se carga en la recámara.
        Booster.Mine => Uses.Bubble | Uses.Burst | Uses.Seed | Uses.Mine,

        _ => CANNON,
    };

    // Qué marca la MIRA, que no siempre es todo lo que el poder se va a llevar.
    //
    // La Madre elige sus dos objetivos sola. Enseñarlos antes de disparar convierte el poder en
    // un cálculo —el jugador comprueba si le conviene el reparto y si no, apunta a otro sitio— y
    // lo que tiene que ser es una sorpresa que se entiende al verla ocurrir. Lo que sí se promete
    // es dónde va a pegar, que es lo único que el jugador decide.
    public static List<Vector2Int> CellsAimedBy(Booster booster, Vector2Int cell, Vector2Int? struck = null, IBoardView board = null) =>
        booster switch
        {
            Booster.Mother => new List<Vector2Int> { struck ?? cell },

            // La Tinta no marca nada: se apunta como un disparo normal (pedido de Diego).
            //
            // Marcaba la zona en la que iba a repartir, y sobraba por dos motivos: a qué burbujas
            // en concreto va se sortea al impactar —así que el hexágono prometía más precisión de
            // la que hay— y el color ya se ve DENTRO de la burbuja, que es de donde salió la idea
            // del líquido. Enseñar las dos cosas era decir lo mismo dos veces y tapar el tablero
            // justo cuando hay que leerlo.
            Booster.Ink    => new List<Vector2Int>(),

            _              => CellsHitBy(booster, cell, struck, board),
        };

    // Si la mira señala el hueco donde la burbuja va a quedar pegada.
    //
    // La Madre no lo señala: lo suyo no pasa donde se posa sino en el racimo que GOLPEA y en dos
    // sitios más del tablero. Marcar el hueco promete el sitio equivocado, y encima es el único
    // de los tres donde no va a ocurrir nada.
    public static bool ShowsLandingSpot(Booster booster) => booster != Booster.Mother;

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

        // Y las hijas van a las más solas DE LO QUE SE VE, de cualquier color (pedido de Diego).
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

            // Y solo entre las que se VEN. Cuando el tablero se retiró del cañón, lo de arriba
            // queda fuera de cuadro: una hija mandada ahí se va por el borde de la pantalla y el
            // jugador la ve escaparse, no acertar. Las de arriba son además las más viejas del
            // nivel, con lo que casi siempre son también las más solas — sin este filtro el poder
            // tiende a gastar sus dos hijas justo en lo único que no se ve (reportado por Diego).
            if (!board.IsVisible(other)) continue;

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

        // Entre las que están IGUAL DE SOLAS se sortea, en vez de coger siempre la primera. El
        // orden de arriba es estricto y repetible, y eso hacía que dos partidas del mismo nivel
        // mandaran las hijas exactamente al mismo sitio (reportado por Diego). El criterio no
        // cambia —siguen yendo a lo más suelto—, lo que cambia es cuál de las empatadas toca.
        //
        // La semilla incluye la celda tocada y cuántas burbujas quedan, así que dos Madres
        // distintas en la misma partida tampoco repiten entre sí.
        if (ranked.Count > 1)
        {
            int best = ranked[0].friends;
            int pool = 0;

            while (pool < ranked.Count && ranked[pool].friends <= best + MOTHER_SLACK) pool++;

            var dice = new System.Random(Variety ^ (struck.x * 73856093) ^ (struck.y * 19349663) ^ standing.Count);

            for (int i = pool - 1; i > 0; i--)
            {
                int j = dice.Next(i + 1);
                (ranked[i], ranked[j]) = (ranked[j], ranked[i]);
            }
        }

        // Se cuentan las ELEGIDAS, no el tamaño de la lista: con el racimo dentro, 'result' ya
        // pasa de dos antes de empezar y las hijas no salían nunca.
        var chosen = new List<Vector2Int>();

        // Dos objetivos pegados son el MISMO problema, no dos: la Madre acabaría haciendo de bomba
        // pequeña en un rincón, que es justo lo que no tiene que ser. Primera pasada exigiendo que
        // cada uno esté lejos de los otros y del racimo que ya revienta.
        foreach (var (cell, _, _) in ranked)
        {
            if (chosen.Count == MOTHER_TARGETS) break;
            if (FarFromAll(cell, result, MOTHER_SPREAD) && FarFromAll(cell, chosen, MOTHER_SPREAD)) chosen.Add(cell);
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

    // Cuánta compañía de más se le admite a una candidata para seguir entrando en el sorteo. En
    // UNO: las que están igual de solas que la mejor, y las que tienen un amigo más. Subiéndolo,
    // el poder empieza a gastar hijas en montones que el jugador ya sabe resolver.
    const int MOTHER_SLACK = 1;

    // La partida. Se sortea al cargar el nivel y no cambia hasta el siguiente, y es lo que hace
    // que la Madre elija distinto en cada intento del MISMO nivel (reportado por Diego).
    //
    // Es una semilla y no un Random suelto porque la elección tiene que ser repetible DENTRO de
    // una partida: la mira y la explosión son dos llamadas distintas, y con azar de verdad la
    // previsualización señalaría unas burbujas y reventarían otras.
    public static int Variety { get; set; }

    static bool FarFromAll(Vector2Int cell, List<Vector2Int> chosen, float spread)
    {
        Vector2 at    = HexGridMath.CellToLocalPos(cell);
        float   apart = HexGridMath.BubbleDiameter * spread;

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

    // Una hija que cae no solo revienta a la que fue a buscar: se lleva también a las de su color
    // que estuvieran pegadas a ella, como un match. Es lo que convierte a la Madre en algo más
    // que dos burbujas por el precio de una bomba (pedido de Diego).
    //
    // Con DOS basta, no tres. El tercero del match es la propia hija, que llega de ese color: lo
    // que el jugador ve es una burbuja que aterriza y cierra el grupo, y pedirle tres ya puestas
    // sería pedirle un match que se habría hecho solo.
    public const int CHILD_MIN_MATCH = 2;

    public static bool ChildrenMatch(Booster booster) => booster == Booster.Mother;

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
    // JSON del nivel ('tutorials' y 'allowed_boosters_start') es justo lo que se desincroniza.
    //
    // Sin aviso por consola a propósito: la mayoría de los tutoriales no presentan ningún booster
    // ('first_steps', 'rescue'), y para esos None es la respuesta correcta, no un error.
    //
    // Existe para poder REGALAR uno al explicarlo: un tutorial que enseña un poder que el jugador
    // no tiene lo deja leyendo sobre algo que no puede probar, y la primera impresión de un
    // booster es justo lo que decide si después lo compra.
    public static Booster BoosterForTutorial(string tutorialId) => Parse(tutorialId, warnIfUnknown: false);

    // Qué se ve en un hueco de la pantalla previa, con los dos filtros que no tiene sentido
    // repetir en 60 archivos: el nivel dice qué ayudas tienen sentido para su objetivo, no a
    // partir de cuándo existen ni de qué familia son.
    static Booster Offered(Booster booster)
    {
        if (booster == Booster.None) return Booster.None;

        // Uno de gameplay acá es un error de diseño de nivel, no una opción. Desde que el HUD los
        // muestra todos (GDD §3.5) ofrecerlo en la previa no limita nada, y encima gastaría uno de
        // los tres huecos que el jugador necesita para los de inicio, que son los que sí hay que
        // elegir. Se avisa en vez de callar: el que lo escribió esperaba que hiciera algo.
        if (!IsStart(booster))
        {
            Debug.LogWarning($"[BoosterRules] '{NameOf(booster)}' es un booster de gameplay: no va en 'allowed_boosters_start'. Los de gameplay están siempre en el HUD y no se eligen por nivel.");
            return Booster.None;
        }

        // Un poder que el jugador todavía no conoce se muestra bloqueado, aunque el nivel lo
        // ofrezca.
        return IsUnlocked(booster) ? booster : Booster.None;
    }

    // En qué nivel se presenta este poder, o 0 si no lo presenta ninguno.
    //
    // Sale del índice de niveles y no de un campo del catálogo: el desbloqueo lo decide el JSON
    // del nivel que lo explica (GDD §3.3), y un número escrito aparte sería la segunda fuente que
    // se desincroniza el día que se mueva un tutorial — que es justo lo que pasa todo el rato
    // mientras se reparten.
    //
    // El id del tutorial ES el nombre del poder, así que no hay tabla que mantener.
    public static int UnlockLevelFor(Booster booster)
    {
        string id = NameOf(booster);
        if (id.Length == 0) return 0;

        foreach (var entry in LevelLoader.Entries)
        {
            if (entry?.tutorials == null) continue;

            foreach (string tutorial in entry.tutorials)
                if (tutorial == id) return entry.id;
        }

        return 0;
    }

    // El trío que se ofrece cuando el JSON del nivel no dice nada. Existe para que los niveles ya
    // escritos sigan funcionando sin editarlos uno por uno; un nivel que quiera otra cosa lo pone
    // en su 'allowed_boosters_start'.
    static readonly Booster[] DEFAULT_ALLOWED = { Booster.Rainbow, Booster.None, Booster.None };

    // Siempre devuelve SLOTS posiciones, rellenando con None lo que el nivel no ofrezca — así
    // quien lo muestre no tiene que decidir qué hacer con una lista corta: un hueco en None es
    // exactamente lo que el ítem dibuja como candado.
    public static Booster[] AllowedFor(LevelData level)
    {
        var slots = new Booster[SLOTS];

        var names = level?.allowed_boosters_start;
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
        (Booster.Oxygen,      "oxygen"),
        (Booster.LowTide,     "low_tide"),
        (Booster.ClearWater,  "clear_water"),
        (Booster.Ink,         "ink"),
        (Booster.Mine,        "mine"),
        (Booster.WeakSpot,    "weak_spot"),
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
    // Los números que el texto de un poder deja en hueco, para que digan lo que el poder HACE de
    // verdad y no lo que decía cuando se escribió la frase.
    //
    // Un tutorial que promete "tres filas" mientras el catálogo está en cinco es peor que uno sin
    // número: el jugador no se entera de que le dieron más, y en seis idiomas el desajuste se
    // arregla seis veces. Los valores salen de donde se configuran.
    //
    // Devuelve vacío para el que no use ninguno: string.Format no se queja de que sobren.
    public static object[] TextArgs(Booster booster) => booster switch
    {
        Booster.Oxygen  => new object[] { OXYGEN_SHOTS },
        Booster.LowTide => new object[] { ArtFor(booster)?.tideRows > 0 ? ArtFor(booster).tideRows : 3 },
        Booster.Rainbow => new object[] { SEEDS_PER_BAND, RAINBOW_COUNT, BAND_ROWS },
        Booster.Mine    => new object[] { SEEDS_PER_BAND, BAND_ROWS, MineRadius },
        Booster.Ink     => new object[] { InkSplash },
        _               => System.Array.Empty<object>(),
    };

    public static string NameKeyFor(Booster booster)        => $"ui.booster.{NameOf(booster)}.name";
    public static string DescriptionKeyFor(Booster booster) => $"ui.booster.{NameOf(booster)}.description";

    // El texto traducido con sus huecos ya rellenos, que es como tiene que salir a pantalla.
    //
    // Acá y no en cada pantalla porque son varias —el tutorial, el panel de recarga, la previa—
    // y la que se olvide enseña un "{0}" en crudo en seis idiomas.
    //
    // Si la frase no tiene huecos vuelve igual. Y si los tiene mal escritos se queda en crudo con
    // un aviso, en vez de tirar la pantalla abajo: un texto feo se ve y se arregla; una excepción
    // en mitad de un tutorial corta el nivel.
    public static string TextFor(string key, Booster booster)
    {
        string raw  = LocaleManager.Get(key);
        var    args = TextArgs(booster);

        if (args.Length == 0) return raw;

        try { return string.Format(raw, args); }
        catch (System.FormatException)
        {
            Debug.LogWarning($"[BoosterRules] '{key}' tiene los huecos mal escritos — se muestra sin rellenar.");
            return raw;
        }
    }

    public static string NameFor(Booster booster)        => TextFor(NameKeyFor(booster), booster);
    public static string DescriptionFor(Booster booster) => TextFor(DescriptionKeyFor(booster), booster);

    public static Sprite IconFor(Booster booster)         => Catalog != null ? Catalog.IconFor(booster)         : null;
    public static Sprite BubbleSpriteFor(Booster booster) => Catalog != null ? Catalog.BubbleSpriteFor(booster) : null;
    public static Sprite GlowFor(Booster booster)         => Catalog != null ? Catalog.GlowFor(booster)         : null;

    public static BoosterCatalog.Entry ArtFor(Booster booster) => Catalog != null ? Catalog.EntryFor(booster) : null;

    // De qué familia es. Sin entrada en el catálogo se asume de gameplay: un poder sin configurar
    // que aparece en el HUD se ve enseguida; uno que se aplicara solo al empezar no se vería nunca.
    public static BoosterCatalog.Family FamilyOf(Booster booster) =>
        ArtFor(booster)?.family ?? BoosterCatalog.Family.InGame;

    // Si se usa señalando una burbuja del tablero en vez de cargándolo en el cañón.
    //
    // Es una pregunta aparte de la familia: este es de gameplay —el jugador elige dónde y cuándo—
    // pero no pasa por la recámara, así que ni se carga, ni gasta disparo, ni necesita arte de
    // burbuja. Preguntando por la familia, el botón del HUD intentaría cargarlo y fallaría.
    public static bool PicksOnBoard(Booster booster) => booster == Booster.WeakSpot;

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
