using UnityEngine;
using UnityEngine.UI;

// Confirmación de "salir del nivel" (abierta desde PausedPanel > Quit). closeButton y
// noButton descartan el panel y vuelven a Paused (vía UIPanel.OnClosed, ver
// PausedPanel.OpenSubPanel). yesButton confirma el abandono: -1 vida (mismo costo que
// declinar la oferta de NoMoreMovesPanel, ver GameplayController.ShowRealLoss) y navega
// directo al mapa — self-contenido, no hace falta que GameplayController se entere.
public class QuitPanel : UIPanel
{
    [SerializeField] Button closeButton;
    [SerializeField] Button noButton;
    [SerializeField] Button yesButton;

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
        if (closeButton) closeButton.onClick.AddListener(Close);
        if (noButton)    noButton.onClick.AddListener(Close);
        if (yesButton)   yesButton.onClick.AddListener(ConfirmQuit);
    }

    void ConfirmQuit()
    {
        SaveManager.LoseLife();
        SceneLoader.GoTo(SceneLoader.LEVEL_MAP);
    }

    void ValidateReferences()
    {
        if (!closeButton) Debug.LogWarning("[QuitPanel] Falta asignar 'Close Button' en el Inspector.");
        if (!noButton)     Debug.LogWarning("[QuitPanel] Falta asignar 'No Button' en el Inspector.");
        if (!yesButton)    Debug.LogWarning("[QuitPanel] Falta asignar 'Yes Button' en el Inspector.");
    }
}
