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

    static Sprite _glow;
    static Sprite _mote;

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
