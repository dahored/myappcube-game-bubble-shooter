using UnityEngine;
using UnityEngine.UI;

// Confirmación de "reiniciar nivel" (abierta desde PausedPanel > Restart). closeButton y
// noButton descartan el panel y vuelven a Paused (vía UIPanel.OnClosed, ver
// PausedPanel.OpenSubPanel). yesButton confirma el reinicio: -1 vida (mismo costo que
// abandonar desde QuitPanel — reiniciar también gasta el intento actual) y recarga la
// escena Gameplay con el mismo nivel — self-contenido, no hace falta que
// GameplayController se entere.
public class ResetProgressPanel : UIPanel
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
        if (yesButton)   yesButton.onClick.AddListener(ConfirmReset);
    }

    void ConfirmReset()
    {
        SaveManager.LoseLife();
        SceneLoader.GoTo(SceneLoader.GAMEPLAY);
    }

    void ValidateReferences()
    {
        if (!closeButton) Debug.LogWarning("[ResetProgressPanel] Falta asignar 'Close Button' en el Inspector.");
        if (!noButton)     Debug.LogWarning("[ResetProgressPanel] Falta asignar 'No Button' en el Inspector.");
        if (!yesButton)    Debug.LogWarning("[ResetProgressPanel] Falta asignar 'Yes Button' en el Inspector.");
    }
}
