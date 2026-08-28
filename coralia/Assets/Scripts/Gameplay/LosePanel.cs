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

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
        if (tryButton)   tryButton.onClick.AddListener(Retry);
        if (closeButton) closeButton.onClick.AddListener(GoToMap);
    }

    void Retry()  => SceneLoader.GoTo(SceneLoader.GAMEPLAY);
    void GoToMap() => SceneLoader.GoTo(SceneLoader.LEVEL_MAP);

    void ValidateReferences()
    {
        if (!tryButton)   Debug.LogWarning("[LosePanel] Falta asignar 'Try Button' en el Inspector.");
        if (!closeButton) Debug.LogWarning("[LosePanel] Falta asignar 'Close Button' en el Inspector.");
    }
}
