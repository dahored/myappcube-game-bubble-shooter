using System.Collections.Generic;
using UnityEngine;

// Las dos texturas del brillo, dibujadas por código una sola vez por sesión.
//
// Van generadas y no importadas porque lo que hace que un destello se vea mágico es el DEGRADADO,
// no la forma. Un sprite vectorial de rayitas tiene bordes duros y queda plano por más que se
// anime; acá el brillo cae de forma continua desde el centro, que es lo que el ojo lee como luz.
//
// Y de paso no hay arte que exportar, reimportar ni mantener a varias densidades.
public static class SparkleTextures
{
    const int GLOW_SIZE = 128;
    const int MOTE_SIZE = 64;
    const int BEACON_SIZE = 128;
    const int RING_SIZE = 256;   // más grande que los otros: un filo fino se escalona si la textura es corta

    // A qué distancia del centro cae el filo del aro, en tanto por uno del radio de la textura.
    // Es público porque quien quiera que el aro rodee algo EXACTO —una burbuja, por ejemplo— no
    // puede darle el tamaño de esa cosa: tiene que dividir por esto, o el aro le queda por dentro.
    public const float RING_EDGE = 0.80f;

    static Sprite _glow;
    static Sprite _beacon;
    static Sprite _mote;

    // Un aro POR grosor, no uno solo. Antes se guardaba el último y se destruía el anterior, y
    // funcionó mientras hubo un único usuario; en cuanto hubo dos con grosores distintos —el aro
    // del cañón y las marcas de la zona de la bomba— cada pedido de uno destruía la textura del
    // otro, que se quedaba con un Sprite muerto y se dibujaba como un cuadro.
    //
    // El grosor se cuantiza al centésimo, que es lo que acota el caché: el rango útil va de 0,01 a
    // 0,4, o sea 40 entradas como mucho. Ese era el motivo de no cachear —afinar el slider en Play
    // generaba una textura por paso—, y redondear lo resuelve sin volver a la destrucción.
    static readonly Dictionary<int, Sprite> _rings = new();

    // Un resplandor redondo y suave. Dos caídas sumadas: una ancha que da el halo y otra muy
    // cerrada que da el núcleo encendido. Con una sola se ve o un disco plano o un puntito.
    public static Sprite Glow => _glow != null ? _glow : _glow = Build(GLOW_SIZE, (x, y) =>
    {
        float d = Mathf.Sqrt(x * x + y * y);
        if (d >= 1f) return 0f;

        float halo = Mathf.Pow(1f - d, 3f) * 0.75f;
        float core = Mathf.Exp(-(d * d) / (0.22f * 0.22f)) * 0.7f;
        return Mathf.Clamp01(halo + core);
    });

    // La luz de algo armado: un disco ENCENDIDO con su halo, no un punto con mucho alrededor.
    //
    // Va aparte de Glow aunque las dos sean redondas y suaves, porque dicen cosas distintas. El
    // núcleo de Glow mide como un quinto de la textura: dibuja un destello, que es lo que hace
    // falta cuando algo chispea. Aquí lo que tiene que leerse es una LUZ, un trozo de color sólido
    // a la escala del dibujo que lleva debajo — con el núcleo de Glow el punto rojo salía al 15%
    // de la burbuja y había que estirar la capa, que solo ensancha el halo (reportado por Diego).
    //
    // Por eso el núcleo es una meseta: lleno hasta bien pasada la mitad y recién ahí se deshace.
    public static Sprite Beacon => _beacon != null ? _beacon : _beacon = Build(BEACON_SIZE, (x, y) =>
    {
        float d = Mathf.Sqrt(x * x + y * y);
        if (d >= 1f) return 0f;

        float core = 1f - Mathf.SmoothStep(0.38f, 0.74f, d);
        float halo = Mathf.Pow(1f - d, 2.2f) * 0.45f;

        return Mathf.Clamp01(core + halo);
    });

    // La chispa de cuatro puntas. Cada brazo es una gaussiana muy fina en un eje por una caída
    // lineal en el otro: fina y brillante en el medio, deshaciéndose hacia la punta.
    public static Sprite Mote => _mote != null ? _mote : _mote = Build(MOTE_SIZE, (x, y) =>
    {
        const float THIN = 0.07f; // grosor del brazo
        const float CORE = 0.13f; // tamaño del punto central

        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        float d  = Mathf.Sqrt(x * x + y * y);

        float armH = Mathf.Exp(-(y * y) / (THIN * THIN)) * Mathf.Pow(Mathf.Max(0f, 1f - ax), 2f);
        float armV = Mathf.Exp(-(x * x) / (THIN * THIN)) * Mathf.Pow(Mathf.Max(0f, 1f - ay), 2f);
        float core = Mathf.Exp(-(d * d) / (CORE * CORE));

        return Mathf.Clamp01(Mathf.Max(core, Mathf.Max(armH, armV)));
    });

    // Un aro con algo de resplandor adentro. A diferencia de Glow, que es un disco y quedaría
    // tapado por lo que tenga encima, el aro deja ver DÓNDE termina: hace falta cuando el borde
    // no es decorativo sino el límite de una zona que el dedo tiene que encontrar.
    //
    // 'softness' es el grosor del filo, en tanto por uno del radio: bajo da una línea nítida y
    // alto un halo difuso. Como la forma vive en la textura y no en el material, cambiarlo
    // obliga a redibujarla.
    public static Sprite Ring(float softness)
    {
        softness = Mathf.Clamp(softness, 0.01f, 0.4f);

        int key = Mathf.RoundToInt(softness * 100f);
        softness = key / 100f;

        // El != null de Unity también cubre una textura destruida por una recarga de dominio: en
        // ese caso se vuelve a dibujar en vez de devolver un Sprite muerto.
        if (_rings.TryGetValue(key, out var cached) && cached != null) return cached;

        return _rings[key] = Build(RING_SIZE, (x, y) =>
        {
            const float FILL = 0.10f;   // cuánto resplandor queda adentro: apenas, para no apagar la burbuja

            float d = Mathf.Sqrt(x * x + y * y);
            if (d >= 1f) return 0f;

            float ring = Mathf.Exp(-((d - RING_EDGE) * (d - RING_EDGE)) / (softness * softness));
            float fill = Mathf.Pow(1f - d, 2f) * FILL;

            return Mathf.Clamp01(Mathf.Max(ring, fill));
        });
    }

    // Un aro ARCOÍRIS: el mismo filo que Ring, pero el color recorre el espectro a lo ancho del
    // anillo — rojo por fuera y violeta por dentro, como un arcoíris de verdad.
    //
    // Hace falta que sea UNA textura y no varios aros de colores superpuestos: varios anillos
    // sueltos se leen como anillos sueltos, y lo que tiene que expandirse por la pantalla es un
    // arcoíris. La diferencia se ve en cuanto la onda es ancha.
    static readonly Dictionary<int, Sprite> _rainbowRings = new();

    public static Sprite RainbowRing(float softness)
    {
        softness = Mathf.Clamp(softness, 0.01f, 0.4f);

        int key = Mathf.RoundToInt(softness * 100f);
        softness = key / 100f;

        if (_rainbowRings.TryGetValue(key, out var cached) && cached != null) return cached;

        float width = softness * 2.2f;   // a qué distancia del filo se reparte el espectro

        return _rainbowRings[key] = BuildColored(RING_SIZE, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            if (d >= 1f) return Color.clear;

            float alpha = Mathf.Exp(-((d - RING_EDGE) * (d - RING_EDGE)) / (softness * softness));

            // De dentro hacia fuera del anillo, en 0..1 — y de ahí al espectro. Se corta en 0,8
            // porque el último tramo del círculo de tono vuelve al rojo y el arcoíris se cerraría
            // sobre sí mismo.
            float band = Mathf.Clamp01((d - RING_EDGE + width) / (width * 2f));

            var color = Color.HSVToRGB(0.8f - band * 0.8f, 0.85f, 1f);
            color.a   = alpha;
            return color;
        });
    }

    // Un arco eléctrico horizontal, para unir dos puntos del tablero.
    //
    // Se guardan VARIAS y se pide una al azar por tramo: un rayo es irrepetible, y repetir la
    // misma quebrada a lo largo de una fila entera la delata como un patrón en cuanto hay más de
    // tres tramos. Con cuatro ya no se reconoce ninguna.
    //
    // La quebrada es una polilínea con vértices sorteados, no una suma de senos: un rayo cambia
    // de dirección de golpe, y lo que se dibuja con curvas suaves se lee como una serpiente.
    const int BOLT_W = 256;
    const int BOLT_H = 64;
    const int BOLT_VARIANTS = 4;
    const int BOLT_KINKS = 7;    // vértices de la quebrada, extremos incluidos

    static readonly Sprite[] _bolts = new Sprite[BOLT_VARIANTS];

    public static Sprite Bolt(int variant)
    {
        int i = ((variant % BOLT_VARIANTS) + BOLT_VARIANTS) % BOLT_VARIANTS;

        if (_bolts[i] != null) return _bolts[i];

        // Semilla fija por variante: las mismas cuatro formas en cada sesión y en cada aparato,
        // así lo que se afina mirando la pantalla es lo que se ve siempre.
        var random = new System.Random(1000 + i);
        var kinks  = new float[BOLT_KINKS];

        // Los extremos van centrados para que los tramos encadenados se encuentren sin escalón.
        for (int k = 1; k < BOLT_KINKS - 1; k++) kinks[k] = (float)(random.NextDouble() * 2.0 - 1.0) * 0.55f;

        return _bolts[i] = BuildRect(BOLT_W, BOLT_H, (x, y) =>
        {
            const float CORE = 0.10f;  // el filamento blanco del medio
            const float HALO = 0.42f;  // el resplandor alrededor

            // Dónde pasa la quebrada a esta altura de x, interpolando entre sus dos vértices.
            float t     = Mathf.Clamp01((x + 1f) * 0.5f) * (BOLT_KINKS - 1);
            int   seg   = Mathf.Min((int)t, BOLT_KINKS - 2);
            float path  = Mathf.Lerp(kinks[seg], kinks[seg + 1], t - seg);

            float d = Mathf.Abs(y - path);

            float core = Mathf.Exp(-(d * d) / (CORE * CORE));
            float halo = Mathf.Exp(-(d * d) / (HALO * HALO)) * 0.35f;

            // Las puntas se apagan: encadenados, los tramos se funden en vez de marcar la junta.
            float ends = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(x)), 0.35f);

            return Mathf.Clamp01((core + halo) * ends);
        });
    }

    // UN brazo del chisporroteo de una burbuja eléctrica: una quebrada que sale del centro hacia
    // la derecha y se deshace antes del borde. El brazo izquierdo es este mismo espejado por
    // quien lo use, pero pidiendo OTRA variante — si los dos lados salieran de la misma, la
    // burbuja se vería como una mancha de Rorschach en vez de como una descarga.
    //
    // Cada variante sortea su propio número de quiebres, su propio grosor y su propio desvío, así
    // que un lado sale fino y nervioso mientras el otro sale grueso y recto. Ahí está todo el
    // efecto: en que los dos lados nunca se parezcan.
    //
    // El destello del medio NO va aquí. Late a su propio ritmo, mucho más rápido que el cambio de
    // forma, y metido en la textura latiría al mismo paso que los rayos.
    const int SPARK_SIZE = 128;
    public const int SPARK_VARIANTS = 8;

    static readonly Sprite[] _sparkArms = new Sprite[SPARK_VARIANTS];

    public static Sprite SparkArm(int variant)
    {
        int i = ((variant % SPARK_VARIANTS) + SPARK_VARIANTS) % SPARK_VARIANTS;

        if (_sparkArms[i] != null) return _sparkArms[i];

        // Semilla fija por variante: las mismas ocho formas en cada sesión y en cada aparato, que
        // es lo que permite afinar el efecto mirándolo.
        var random = new System.Random(3000 + i);

        double Next() => random.NextDouble();

        int   kinks = 4 + (int)(Next() * 4);                      // 4..7 vértices, o sea 3..6 quiebres
        float thin  = Mathf.Lerp(0.030f, 0.075f, (float)Next());  // de filamento a rayo gordo
        float sway  = Mathf.Lerp(0.18f,  0.42f,  (float)Next());  // cuánto se desvía de la horizontal

        var path = new float[kinks];
        path[0] = 0f;   // nace pegado al destello

        for (int k = 1; k < kinks; k++)
        {
            // Cada quiebre se aleja del anterior, no del eje: así la quebrada deriva en vez de
            // oscilar alrededor de una línea, que es lo que la hacía parecer una onda.
            float step = (float)(Next() * 2.0 - 1.0) * sway;
            path[k] = Mathf.Clamp(path[k - 1] + step, -0.62f, 0.62f);
        }

        return _sparkArms[i] = Build(SPARK_SIZE, (x, y) =>
        {
            if (x < 0f) return 0f;   // solo la mitad derecha: el otro brazo es otra variante espejada

            float t    = Mathf.Clamp01(x) * (kinks - 1);
            int   seg  = Mathf.Min((int)t, kinks - 2);
            float line = Mathf.Lerp(path[seg], path[seg + 1], t - seg);

            float dy  = Mathf.Abs(y - line);
            float arm = Mathf.Exp(-(dy * dy) / (thin * thin));

            // Se deshace antes del borde para que el rayo muera DENTRO del cristal, y nace ancho
            // en el centro para engancharse con el destello sin que se vea la junta.
            arm *= Mathf.Pow(Mathf.Clamp01(1f - x / 0.90f), 0.5f);
            arm += Mathf.Exp(-(x * x) / (0.05f * 0.05f)) * Mathf.Exp(-(y * y) / (0.09f * 0.09f)) * 0.8f;

            return Mathf.Clamp01(arm);
        });
    }

    // Build para una textura que no es cuadrada. Misma convención: x e y en -1..1, el color en
    // blanco y la forma en el alfa.
    // El líquido de dentro de una burbuja.
    //
    // En BLANCO con luminosidad variable, no en alfa plano: quien la use la tiñe multiplicando,
    // así que lo que acá es más claro sale más claro en su color. Es lo que permite el degradado
    // del cuerpo, la cresta luminosa y las burbujas de dentro con un solo sprite y un solo tinte.
    //
    // Lo que hace que se lea como líquido y no como relleno son tres cosas, por orden de peso:
    //
    //   1. la CRESTA — un reborde grueso y casi blanco donde toca el aire. Es el detalle que más
    //      dice "líquido"; sin él, el borde recto se ve como un corte
    //   2. las BURBUJAS de dentro, cada una con su reflejo
    //   3. el cuerpo oscureciéndose hacia el fondo, como algo con profundidad
    //
    // 'fill' es cuánto sube, de 0 (vacía) a 1 (llena). 'variant' desplaza la onda y recoloca las
    // burbujas: capas distintas, olas distintas.
    //
    // Y en DOS PIEZAS, cuerpo y cresta, porque llevan colores distintos: en un líquido la
    // superficie es más clara y más viva que el fondo, y eso no se consigue tiñendo una sola
    // imagen con un color —multiplicando, todo sale del mismo tono— (pedido de Diego).
    // 'frame' de 0 a 'frames'-1 recorre un CICLO COMPLETO de la ola: en el último, la superficie
    // ha vuelto a donde empezó. Pasándolos seguidos, la cresta cambia de forma sola — se levanta
    // por un lado mientras baja por el otro, como un líquido de verdad (pedido de Diego).
    //
    // Eso lo garantizan las frecuencias enteras de abajo: con múltiplos sueltos el ciclo no
    // cerraría y el salto del último frame al primero se vería como un tirón.
    //
    // 'bubblesOnly' devuelve SOLO las burbujas de dentro, para dibujarlas en su propia capa por
    // delante de todo. Metidas en el cuerpo quedaban debajo de las capas de superficie, que son
    // semitransparentes y las lavaban hasta dejarlas en manchas planas (reportado por Diego).
    public static Sprite Liquid(float fill, int frame = 0, int frames = 1, bool withBubbles = true,
                                bool crestOnly = false, bool bubblesOnly = false)
    {
        frames = Mathf.Max(1, frames);
        frame  = ((frame % frames) + frames) % frames;

        int key = (((Mathf.Clamp(Mathf.RoundToInt(fill * 10f), 0, 10) * 64 + frame) * 2
                + (crestOnly ? 1 : 0)) * 2 + (withBubbles ? 1 : 0)) * 2 + (bubblesOnly ? 1 : 0);

        if (_liquid.TryGetValue(key, out var cached) && cached != null) return cached;

        float level = Mathf.Lerp(-1f, 1f, Mathf.Clamp01(fill));
        float shift = frame / (float)frames * Mathf.PI * 2f;

        // Las burbujas de dentro, repartidas a mano. Sorteadas acá serían distintas en cada
        // arranque del juego, y una textura cacheada no puede cambiar entre partidas.
        var bubbles = withBubbles || bubblesOnly ? Bubbles(0) : System.Array.Empty<(float, float, float)>();

        // Las burbujas SUBEN. Un líquido con burbujas quietas no se lee como líquido: se lee como
        // una cara, sobre todo si son pocas y están repartidas simétricamente (reportado por
        // Diego).
        //
        // Suben con el mismo reloj de frames que la ola, cada una a su ritmo, y al llegar a la
        // superficie reaparecen abajo. Como el ciclo es el mismo, el bucle cierra sin salto.
        if (bubblesOnly) return BuildColored(LIQUID_SIZE, (x, y) =>
        {
            // Las mismas dos fronteras que el cuerpo, porque una burbuja de dentro tiene que
            // estar DENTRO: el vidrio por fuera y la superficie por arriba. Sin esto se dibujaban
            // por todo el cuadro de la textura, incluso fuera de la esfera (reportado por Diego).
            float r = Mathf.Sqrt(x * x + y * y);
            if (r > 0.97f) return Color.clear;

            float surface = level
                          + 0.055f * Mathf.Sin(x * 5.5f + shift)
                          + 0.028f * Mathf.Sin(x * 11f  - shift * 2f)
                          + 0.012f * Mathf.Sin(x * 19f  + shift * 3f);

            if (y > surface) return Color.clear;

            float climb = frame / (float)frames;

            foreach (var (bx, by0, br) in bubbles)
            {
                // Cada una recorre el líquido a su propio ritmo: el radio sirve de velocidad, así
                // que las pequeñas suben más deprisa — que es justo lo que hacen de verdad.
                float speed = Mathf.Lerp(1.6f, 0.75f, Mathf.InverseLerp(0.04f, 0.16f, br));
                // El recorrido acaba un pelo POR DEBAJO de la superficie: llegando justo a ella,
                // la burbuja se corta contra la ola en el último tramo de su subida.
                float span  = (level + 1f) - br * 2f - 0.08f;

                // El recorrido se repite dentro del líquido: de abajo del todo a ras de la
                // superficie, y vuelta a empezar.
                float by = -1f + br + Mathf.Repeat((by0 + 1f - br) + climb * speed * span, Mathf.Max(0.1f, span));

                float d = Mathf.Sqrt((x - bx) * (x - bx) + (y - by) * (y - by));
                if (d > br) continue;

                // Relleno oscuro, borde brillante y un reflejo grande arriba a la izquierda. Los
                // tres a la vez son lo que las hace esferas; con dos se leen como un aro.
                float ring  = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(br * 0.45f, br * 0.97f, d));
                float shade = Mathf.Lerp(0.45f, 1f, ring);

                float gx = x - bx + br * 0.33f;
                float gy = y - by - br * 0.33f;

                if (Mathf.Sqrt(gx * gx + gy * gy) < br * 0.24f) shade = 1f;

                // Y un brillo tenue abajo a la derecha: la luz que rebota dentro del líquido. Es
                // el detalle que las despega del fondo en vez de dejarlas pegadas.
                float rx = x - bx - br * 0.38f;
                float ry = y - by + br * 0.38f;

                if (Mathf.Sqrt(rx * rx + ry * ry) < br * 0.18f) shade = Mathf.Max(shade, 0.92f);

                // El borde se suaviza en el último pelo para que no quede dentado. Y se funde
                // contra las dos fronteras, para que una burbuja a medio salir se desvanezca en
                // vez de quedar cortada por la mitad.
                float soft  = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(br, br * 0.9f, d));
                float glass = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.97f, 0.86f, r));
                float under = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(surface, surface - 0.06f, y));

                return new Color(shade, shade, shade, soft * glass * under);
            }

            return Color.clear;
        });

        var sprite = BuildColored(LIQUID_SIZE, (x, y) =>
        {
            // Dentro del círculo y nada más: lo que se sale haría parecer una mancha pegada.
            float r = Mathf.Sqrt(x * x + y * y);
            if (r > 0.97f) return Color.clear;

            // La superficie, con tres senos de distinta frecuencia. El tercero es diminuto y es
            // el que quita la sensación de patrón.
            // Multiplicadores ENTEROS del desfase: es lo que hace que el ciclo cierre y el bucle
            // de frames no dé un tirón al volver al primero.
            float surface = level
                          + 0.055f * Mathf.Sin(x * 5.5f + shift)
                          + 0.028f * Mathf.Sin(x * 11f  - shift * 2f)
                          + 0.012f * Mathf.Sin(x * 19f  + shift * 3f);

            float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.97f, 0.86f, r));

            // Por encima de la superficie solo queda el halo de la cresta: un resplandor corto
            // que sube al aire, como la luz que atraviesa el borde del agua.
            if (y > surface)
            {
                if (!crestOnly) return Color.clear;

                float halo = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(surface + CREST * 1.4f, surface, y));

                return new Color(1f, 1f, 1f, halo * 0.55f * edge);
            }

            float crest = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(surface - CREST, surface, y));

            // La cresta va por su lado, con su propio color: es la pieza que se tiñe de magenta
            // mientras el cuerpo se queda morado. El filo de arriba, casi blanco.
            if (crestOnly)
            {
                float lip = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(surface - CREST * 0.3f, surface, y));

                return new Color(Mathf.Lerp(0.82f, 1f, lip), Mathf.Lerp(0.82f, 1f, lip), Mathf.Lerp(0.82f, 1f, lip),
                                 crest * edge);
            }

            // El cuerpo: un poco más oscuro hacia el fondo, como algo con profundidad.
            //
            // Un POCO. Antes bajaba al 30% con curva cuadrática, así que casi todo el líquido
            // quedaba al tercio de su color y se veía negruzco en vez de morado (reportado por
            // Diego). Lo que da profundidad es el degradado, no que el fondo sea oscuro.
            float depth = Mathf.InverseLerp(-1f, surface, y);
            float body  = Mathf.Lerp(0.72f, 1f, Mathf.Sqrt(depth));

            // Y las burbujas de dentro, con su reflejo arriba a la izquierda.
            foreach (var (bx, by, br) in bubbles)
            {
                float d = Mathf.Sqrt((x - bx) * (x - bx) + (y - by) * (y - by));

                if (d > br) continue;

                // El contorno claro y el relleno OSCURO: el contraste entre los dos es lo único
                // que las hace esferas a tamaño de burbuja. Con el relleno casi igual que el
                // líquido se perdían contra el fondo (reportado por Diego).
                float ring = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(br * 0.5f, br, d));
                body = Mathf.Lerp(body * 0.6f, 1f, ring);

                // Y su reflejo, arriba a la izquierda, a tope de claro.
                float gloss = Mathf.Sqrt((x - bx + br * 0.32f) * (x - bx + br * 0.32f) +
                                         (y - by - br * 0.32f) * (y - by - br * 0.32f));

                if (gloss < br * 0.38f) body = 1f;
            }

            return new Color(body, body, body, edge);
        });

        _liquid[key] = sprite;
        return sprite;
    }

    // Lo gruesa que es la cresta, en tanto por uno del radio.
    //
    // Exagerada a propósito. Esta textura se dibuja a tamaño de burbuja —unos 60 px en el cañón—
    // así que un detalle "realista" del 7% del radio cabe en dos píxeles y desaparece: lo que se
    // veía era un relleno plano (reportado por Diego). Todo lo de acá está dimensionado para
    // leerse a ese tamaño, no para verse bien en grande.
    const float CREST = 0.2f;

    // Dónde van las burbujas de dentro: posición y radio, en coordenadas del sprite (-1..1).
    //
    // A mano y no sorteadas, porque la textura se cachea y tiene que ser la misma siempre.
    //
    // Y SOLO en la capa de abajo. Las de encima existen para que la superficie se mueva, y si
    // llevaran burbujas se verían tres juegos desplazados entre sí — fantasmas, no volumen.
    //
    // Siete y desparejas a propósito: tres grandes y simétricas se leen como una cara. El tamaño
    // además marca a qué velocidad sube cada una, así que la variedad también desordena el
    // movimiento.
    static (float x, float y, float r)[] Bubbles(int variant) => new[]
    {
        (-0.37f, -0.62f, 0.15f),
        ( 0.26f, -0.38f, 0.11f),
        (-0.11f, -0.80f, 0.08f),
        ( 0.44f, -0.70f, 0.07f),
        (-0.52f, -0.34f, 0.06f),
        ( 0.05f, -0.52f, 0.05f),
        (-0.20f, -0.26f, 0.04f),
    };

    static readonly Dictionary<int, Sprite> _liquid = new();
    // Más pequeña que las demás texturas a propósito: de esta hay un juego entero de frames, y
    // se dibuja a tamaño de burbuja (unos 60-90px), así que 160 ya sobra.
    const int LIQUID_SIZE = 160;

    // Una gota: redonda abajo y en punta arriba, como cae de verdad.
    //
    // En blanco con luminosidad, igual que el líquido: quien la use la tiñe, y lo más claro sale
    // más claro en su color. Lleva su reflejo, que es lo que la hace una gota y no una lágrima
    // recortada.
    static Sprite _drop;
    public static Sprite Drop => _drop != null ? _drop : _drop = BuildColored(DROP_SIZE, (x, y) =>
    {
        // El perfil: un círculo abajo que se estrecha hacia la punta. El ancho permitido a cada
        // altura es lo que dibuja la silueta.
        float t = Mathf.InverseLerp(-0.75f, 0.95f, y);
        if (t < 0f || t > 1f) return Color.clear;

        float width = Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)) * Mathf.Lerp(0.72f, 0.2f, t * t);
        float d     = Mathf.Abs(x) / Mathf.Max(0.001f, width);

        if (d > 1f) return Color.clear;

        // Más clara arriba que abajo, como algo con volumen.
        float shade = Mathf.Lerp(0.72f, 1f, Mathf.InverseLerp(-0.75f, 0.4f, y));

        // Y su reflejo, arriba a la izquierda.
        float gx = (x + 0.22f) / 0.26f;
        float gy = (y - 0.12f) / 0.3f;

        if (gx * gx + gy * gy < 1f) shade = 1f;

        return new Color(shade, shade, shade, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.82f, d)));
    });

    const int DROP_SIZE = 128;

    static Sprite BuildRect(int width, int height, System.Func<float, float, float> alphaAt)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
        {
            name       = "BoltTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            hideFlags  = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[width * height];

        for (int py = 0; py < height; py++)
        for (int px = 0; px < width; px++)
        {
            float x = (px + 0.5f) / width  * 2f - 1f;
            float y = (py + 0.5f) / height * 2f - 1f;

            byte a = (byte)(Mathf.Clamp01(alphaAt(x, y)) * 255f);
            pixels[py * width + px] = new Color32(255, 255, 255, a);
        }

        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));
        sprite.name      = "Bolt";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    // Igual que Build pero el color lo decide cada píxel, no solo su alfa.
    static Sprite BuildColored(int size, System.Func<float, float, Color> colorAt)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            name       = "SparkleTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            hideFlags  = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[size * size];

        for (int py = 0; py < size; py++)
        for (int px = 0; px < size; px++)
        {
            float x = (px + 0.5f) / size * 2f - 1f;
            float y = (py + 0.5f) / size * 2f - 1f;

            Color c = colorAt(x, y);
            pixels[py * size + px] = new Color32(
                (byte)(Mathf.Clamp01(c.r) * 255f),
                (byte)(Mathf.Clamp01(c.g) * 255f),
                (byte)(Mathf.Clamp01(c.b) * 255f),
                (byte)(Mathf.Clamp01(c.a) * 255f));
        }

        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        sprite.name      = "Sparkle";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    // El color va siempre en blanco y la forma vive en el alfa: así el tinte lo pone la Image que
    // la use, y la misma textura sirve para cualquier color sin regenerarse.
    static Sprite Build(int size, System.Func<float, float, float> alphaAt)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            name       = "SparkleTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            hideFlags  = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[size * size];

        for (int py = 0; py < size; py++)
        for (int px = 0; px < size; px++)
        {
            // De píxel a coordenadas centradas en [-1, 1], midiendo el CENTRO del píxel.
            float x = (px + 0.5f) / size * 2f - 1f;
            float y = (py + 0.5f) / size * 2f - 1f;

            byte a = (byte)(Mathf.Clamp01(alphaAt(x, y)) * 255f);
            pixels[py * size + px] = new Color32(255, 255, 255, a);
        }

        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        sprite.name      = "Sparkle";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
