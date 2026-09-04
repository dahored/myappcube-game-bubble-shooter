using UnityEngine;
using UnityEngine.UI;

// Pantalla final de derrota ("Nivel fallido"). Se abre cuando el jugador cierra
// NoMoreMovesPanel sin pagar — la vida ya se descontó ahí (ver
// GameplayController.OnDeclined), así que este panel no toca SaveManager, solo navega:
// Try again reintenta el mismo nivel, la X vuelve al mapa. Todos los textos son estáticos
// -> LocalizedText en el Editor, no hace falta código para setearlos.
public class LosePanel : UIPanel
{
    [SerializeField] Button tryButton;
    [SerializeField] Button closeButton;

    [Header("Sonido (opcional — dejar vacío hasta tener el clip)")]
    [SerializeField] AudioClip loseClip; // suena apenas se abre el panel — mismo criterio que WinPanel.winClip
    [SerializeField, Range(0f, 1f)] float loseClipVolume = 0.6f; // mismo default que winClipVolume, ajustable si suena "duro"

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
        if (tryButton)   tryButton.onClick.AddListener(Retry);
        if (closeButton) closeButton.onClick.AddListener(GoToMap);
    }

    public override void Open()
    {
        base.Open();
        AudioManager.Instance?.StopMusic(); // corta la música de gameplay al perder (pedido de Diego) — así se escucha bien el sonido de derrota
        AudioManager.Instance?.PlayUi(loseClip, loseClipVolume);
    }

    void Retry()  => SceneLoader.GoTo(SceneLoader.GAMEPLAY);
    void GoToMap() => SceneLoader.GoTo(SceneLoader.LEVEL_MAP);

    void ValidateReferences()
    {
        if (!tryButton)   Debug.LogWarning("[LosePanel] Falta asignar 'Try Button' en el Inspector.");
        if (!closeButton) Debug.LogWarning("[LosePanel] Falta asignar 'Close Button' en el Inspector.");
    }
}
