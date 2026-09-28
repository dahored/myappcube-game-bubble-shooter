using System.Collections;
using TMPro;
using UnityEngine;

// El nombre del capítulo, que aparece un momento al entrar en uno nuevo y se desvanece.
//
// Mira qué capítulo está centrado en la vista, no por qué nodo pasó el jugador: el mapa se
// arrastra libremente y se puede cruzar la separación entre capítulos sin tocar ningún nodo.
//
// El cambio se cuenta al llegar al PRIMER NODO del capítulo siguiente y no al entrar en la
// separación que hay entre los dos. Ahí en medio no se está todavía en ninguno, y anunciarlo
// antes de tiempo delata que el mapa es una tira continua en vez de sitios distintos.
public class ChapterTitleBanner : MonoBehaviour
{
    [Tooltip("Lo que se enciende y se apaga. Suele ser el envoltorio con el fondo degradado y el texto adentro.")]
    [SerializeField] CanvasGroup group;

    [SerializeField] TMP_Text label;

    [Tooltip("Vacíos: se buscan solos en la escena.")]
    [SerializeField] WorldMapScroll     scroll;
    [SerializeField] WorldMapDefinition definition;

    [Header("Tiempos")]
    [SerializeField] float fadeIn  = 0.35f;
    [SerializeField] float hold    = 2f;
    [SerializeField] float fadeOut = 0.8f;

    [Tooltip("Anunciar también el capítulo en el que se abre el mapa, no solo los que se cruzan después.")]
    [SerializeField] bool announceOnOpen;

    int       _chapter = int.MinValue;
    Coroutine _showing;

    void Start()
    {
        if (scroll     == null) scroll     = FindAnyObjectByType<WorldMapScroll>();
        if (definition == null) definition = FindAnyObjectByType<WorldMapDefinition>();

        if (group == null || label == null || scroll == null || definition == null)
        {
            Debug.LogWarning($"[ChapterTitleBanner] '{name}' está incompleto (falta el CanvasGroup, el texto, el scroll o la definición) — no va a anunciar capítulos.", this);
            enabled = false;
            return;
        }

        group.alpha          = 0f;
        group.blocksRaycasts = false;
        group.interactable   = false;

        // El capítulo de arranque se toma como ya conocido, así no se anuncia solo por existir.
        _chapter = CenteredChapter();
        if (announceOnOpen) Announce(_chapter);
    }

    void LateUpdate()
    {
        int chapter = CenteredChapter();
        if (chapter == _chapter) return;

        _chapter = chapter;
        Announce(chapter);
    }

    void Announce(int chapter)
    {
        var json = Resources.Load<TextAsset>($"Chapters/Chapter_{chapter}");
        var data = json ? JsonUtility.FromJson<ChapterData>(json.text) : null;

        // DisplayName resuelve la traducción y cae al nombre del archivo si falta la clave, así
        // que un capítulo sin traducir muestra algo legible en vez de "chapter.6.name".
        label.text = data != null ? data.DisplayName : $"Chapter {chapter}";

        if (_showing != null) StopCoroutine(_showing);
        _showing = StartCoroutine(Show());
    }

    IEnumerator Show()
    {
        yield return Fade(group.alpha, 1f, fadeIn);
        yield return new WaitForSeconds(hold);
        yield return Fade(1f, 0f, fadeOut);

        _showing = null;
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f) { group.alpha = to; yield break; }

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }

    // El último capítulo que ya empezó en el punto que se está mirando. Mientras se cruza la
    // separación sigue contando el anterior, y cambia justo al alcanzar el primer nodo del nuevo.
    int CenteredChapter()
    {
        var spans = definition.Spans();
        if (spans.Count == 0) return 0;

        float index = scroll.CenteredWorldZ / Mathf.Max(0.0001f, definition.Layout.spacing);
        int   number = spans[0].number;

        foreach (var span in spans)
            if (index >= span.start) number = span.number;

        return number;
    }
}
