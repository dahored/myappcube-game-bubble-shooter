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

    static Sprite _glow;
    static Sprite _mote;
    static Sprite _ring;
    static float  _ringSoft = -1f;

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

        if (_ring != null && Mathf.Approximately(_ringSoft, softness)) return _ring;

        // Se tira la anterior en vez de guardarla: esto se ajusta arrastrando un slider en pleno
        // Play, y cada paso regenera la textura. Cacheando todas, un rato de afinar el grosor
        // serían decenas de megas de texturas que ya no mira nadie.
        Release(_ring);

        _ringSoft = softness;

        return _ring = Build(RING_SIZE, (x, y) =>
        {
            const float EDGE = 0.80f;   // a qué distancia del centro está el filo
            const float FILL = 0.10f;   // cuánto resplandor queda adentro: apenas, para no apagar la burbuja

            float d = Mathf.Sqrt(x * x + y * y);
            if (d >= 1f) return 0f;

            float ring = Mathf.Exp(-((d - EDGE) * (d - EDGE)) / (softness * softness));
            float fill = Mathf.Pow(1f - d, 2f) * FILL;

            return Mathf.Clamp01(Mathf.Max(ring, fill));
        });
    }

    static void Release(Sprite sprite)
    {
        if (sprite == null) return;

        var texture = sprite.texture;

        if (Application.isPlaying) { Object.Destroy(sprite); if (texture) Object.Destroy(texture); }
        else                       { Object.DestroyImmediate(sprite); if (texture) Object.DestroyImmediate(texture); }
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
