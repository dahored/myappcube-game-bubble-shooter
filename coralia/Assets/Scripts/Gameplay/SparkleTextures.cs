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
    const int RING_SIZE = 256;   // más grande que los otros: un filo fino se escalona si la textura es corta

    // A qué distancia del centro cae el filo del aro, en tanto por uno del radio de la textura.
    // Es público porque quien quiera que el aro rodee algo EXACTO —una burbuja, por ejemplo— no
    // puede darle el tamaño de esa cosa: tiene que dividir por esto, o el aro le queda por dentro.
    public const float RING_EDGE = 0.80f;

    static Sprite _glow;
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
