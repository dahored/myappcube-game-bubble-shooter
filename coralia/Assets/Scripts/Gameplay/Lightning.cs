using UnityEngine;
using UnityEngine.UI;

// La descarga que recorre una fila del tablero, tramo a tramo, desde donde pegó hacia los dos
// lados a la vez.
//
// Es un efecto aparte y no un ajuste de BombBlast porque no comparte nada con una explosión: una
// onda se abre desde un centro y una descarga VIAJA, y lo que hay que poder afinar en cada una no
// se parece. Mezclarlas dejaba la mitad de los campos sin sentido según cuál estuviera activa.
public class Lightning : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("Marcado: el poder lanza una descarga que recorre la fila. Sin marcar no se dibuja nada y el resto de este bloque se ignora.")]
        public bool enabled;

        [Tooltip("El color del arco. El filamento del centro sale casi blanco pase lo que pase — es lo que lo hace leerse como electricidad y no como una cinta de color.")]
        public Color color = new(0.65f, 0.9f, 1f, 1f);

        [Tooltip("Grosor del arco en píxeles del tablero. Una burbuja mide 92.")]
        [Range(8f, 120f)]
        public float thickness = 46f;

        [Tooltip("Cuánto dura cada tramo desde que aparece. Son chispazos: pasado un cuarto de segundo ya no se lee como un rayo sino como una cinta puesta encima.")]
        [Range(0.05f, 1f)]
        public float duration = 0.22f;

        [Tooltip("Parpadeos por segundo mientras vive. En cero el arco se apaga liso, sin temblor.")]
        [Range(0f, 60f)]
        public float flickerHz = 28f;
    }

    // Cuánto se pasa cada tramo de la distancia que tiene que cubrir. Las puntas de la textura
    // van apagadas, así que dos tramos que midieran justo lo suyo dejarían un hueco en la junta.
    const float OVERLAP = 1.3f;

    // Recorre la fila desde 'fromCol' hacia los dos lados. Se dibuja sobre TODAS las columnas,
    // haya burbuja o no: lo que viaja es la corriente, y una descarga que se saltara los huecos
    // se vería cortada justo donde el jugador está mirando el agujero que acaba de abrir.
    public static void Sweep(RectTransform gridContainer, int row, int fromCol, Settings s, float step)
    {
        if (gridContainer == null || s == null || !s.enabled) return;

        int cols = HexGridMath.ColsInRow(row);
        if (cols <= 0) return;

        fromCol = Mathf.Clamp(fromCol, 0, cols - 1);

        for (int d = 1; d < cols; d++)
        {
            float delay = (d - 1) * step;

            if (fromCol - d >= 0)   Arc(gridContainer, row, fromCol - d + 1, fromCol - d, s, delay);
            if (fromCol + d < cols) Arc(gridContainer, row, fromCol + d - 1, fromCol + d, s, delay);
        }
    }

    static void Arc(RectTransform gridContainer, int row, int fromCol, int toCol, Settings s, float delay)
    {
        Vector2 a = HexGridMath.CellToLocalPos(new Vector2Int(fromCol, row));
        Vector2 b = HexGridMath.CellToLocalPos(new Vector2Int(toCol,   row));

        float distance = Vector2.Distance(a, b);
        if (distance < 0.01f) return;

        var go = new GameObject("Lightning", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
        {
            // Temporal y se destruye solo, pero si el editor guarda la escena mientras vive, sin
            // esto queda serializado dentro del .unity.
            hideFlags = HideFlags.DontSave,
        };

        var rt = (RectTransform)go.transform;
        rt.SetParent(gridContainer, false);
        rt.localScale = Vector3.one;
        rt.anchorMin  = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        rt.anchoredPosition  = (a + b) * 0.5f;
        rt.sizeDelta         = new Vector2(distance * OVERLAP, s.thickness);
        rt.localEulerAngles  = new Vector3(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);

        // Delante de las burbujas: la corriente pasa POR ENCIMA del tablero.
        rt.SetAsLastSibling();

        var image = go.GetComponent<Image>();
        // Una forma distinta por tramo, elegida por su sitio en la fila y no al azar: así la misma
        // jugada se ve igual dos veces, que es lo que permite afinar el efecto mirándolo.
        image.sprite        = SparkleTextures.Bolt(fromCol + toCol + row);
        image.color         = s.color;
        image.raycastTarget = false;

        go.AddComponent<Lightning>().Init(s, delay);
    }

    Image _image;
    Color _color;
    Settings _settings;
    float _delay;
    float _elapsed;

    void Init(Settings s, float delay)
    {
        _image    = GetComponent<Image>();
        _color    = _image.color;
        _settings = s;
        _delay    = delay;

        // Invisible hasta que le toque. Un arco no se enciende poco a poco: o está o no está, así
        // que la espera es transparencia pura y no un desvanecido.
        SetAlpha(0f);
    }

    void Update()
    {
        _elapsed += Time.deltaTime;

        float t = _elapsed - _delay;
        if (t < 0f) return;

        if (t >= _settings.duration) { Destroy(gameObject); return; }

        float fade    = 1f - t / _settings.duration;
        float flicker = _settings.flickerHz <= 0f
            ? 1f
            : 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * _settings.flickerHz * Mathf.PI));

        SetAlpha(_color.a * fade * flicker);
    }

    void SetAlpha(float a)
    {
        var c = _color; c.a = a;
        _image.color = c;
    }
}
