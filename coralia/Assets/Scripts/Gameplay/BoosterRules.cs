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

    // Cuántos se regalan al presentar un booster por primera vez. DOS y no uno: el primero se
    // carga solo en el cañón para que el jugador lo dispare sin tener que descubrir el ícono del
    // HUD, y el segundo queda en el inventario para que pueda volver a usarlo cuando ÉL quiera
    // — que es cuando de verdad se aprende para qué sirve. Con uno solo, el único uso sería el
    // que el juego le puso en la mano.
    public const int TUTORIAL_GRANT = 2;

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
    public static BubbleSpecial BoosterForTutorial(string tutorialId) => Parse(tutorialId, warnIfUnknown: false);

    // Un poder que el jugador todavía no conoce se muestra bloqueado, aunque el nivel lo ofrezca.
    // El filtro va acá y no en cada JSON para que el diseñador de niveles no tenga que repetir en
    // 60 archivos una regla que es siempre la misma: el nivel dice qué ayudas tienen sentido para
    // su objetivo, no a partir de cuándo existen.
    static BubbleSpecial Offered(BubbleSpecial booster) => IsUnlocked(booster) ? booster : BubbleSpecial.None;

    // El trío que se ofrece cuando el JSON del nivel no dice nada. Existe para que los niveles ya
    // escritos sigan funcionando sin editarlos uno por uno; un nivel que quiera otra cosa lo pone
    // en su 'allowed_boosters'.
    static readonly BubbleSpecial[] DEFAULT_ALLOWED = { BubbleSpecial.Bomb, BubbleSpecial.None, BubbleSpecial.None };

    // Siempre devuelve SLOTS posiciones, rellenando con None lo que el nivel no ofrezca — así
    // quien lo muestre no tiene que decidir qué hacer con una lista corta: un hueco en None es
    // exactamente lo que el ítem dibuja como candado.
    public static BubbleSpecial[] AllowedFor(LevelData level)
    {
        var slots = new BubbleSpecial[SLOTS];

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
    static readonly (BubbleSpecial booster, string name)[] NAMES =
    {
        (BubbleSpecial.Bomb, "bomb"),
    };

    public static string NameOf(BubbleSpecial booster)
    {
        foreach (var (candidate, name) in NAMES)
            if (candidate == booster) return name;

        return "";
    }

    // El aviso se apaga para quien pregunta por algo que LEGÍTIMAMENTE puede no ser un booster,
    // como el id de un tutorial.
    public static BubbleSpecial Parse(string name, bool warnIfUnknown = true)
    {
        string clean = name?.Trim().ToLowerInvariant() ?? "";

        if (clean.Length == 0 || clean == "none") return BubbleSpecial.None;

        foreach (var (booster, candidate) in NAMES)
            if (candidate == clean) return booster;

        if (warnIfUnknown) Debug.LogWarning($"[BoosterRules] '{name}' no es ningún booster conocido: el hueco se muestra bloqueado.");

        return BubbleSpecial.None;
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
    public static string NameKeyFor(BubbleSpecial booster)        => $"ui.booster.{NameOf(booster)}.name";
    public static string DescriptionKeyFor(BubbleSpecial booster) => $"ui.booster.{NameOf(booster)}.description";

    public static Sprite IconFor(BubbleSpecial booster)         => Catalog != null ? Catalog.IconFor(booster)         : null;
    public static Sprite BubbleSpriteFor(BubbleSpecial booster) => Catalog != null ? Catalog.BubbleSpriteFor(booster) : null;
    public static Sprite GlowFor(BubbleSpecial booster)         => Catalog != null ? Catalog.GlowFor(booster)         : null;

    public static BoosterCatalog.Entry ArtFor(BubbleSpecial booster) => Catalog != null ? Catalog.EntryFor(booster) : null;

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
    public static bool IsUnlocked(BubbleSpecial booster)
    {
        if (booster == BubbleSpecial.None) return false;

        return SaveManager.IsBoosterUnlocked(booster) || SaveManager.HasSeenTutorial(NameOf(booster));
    }
}
