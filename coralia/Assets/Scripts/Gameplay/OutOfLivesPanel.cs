using UnityEngine;
using UnityEngine.UI;

// Se muestra cuando SaveManager.Lives llega a 0 — el chequeo vive en los controllers de
// escena (LevelMapController al iniciar y al tocar un nodo, GameplayController al iniciar),
// no acá: este panel solo presenta y cierra, no sabe nada de cuándo debe aparecer.
//
// refillButton queda deliberadamente sin wirear — depende de la tienda (issue #53),
// todavía no hay a dónde mandarlo.
public class OutOfLivesPanel : UIPanel
{
    [SerializeField] Button closeButton;
    [SerializeField] Button refillButton;

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
        if (closeButton) closeButton.onClick.AddListener(Close);
    }

    void ValidateReferences()
    {
        if (!closeButton)  Debug.LogWarning("[OutOfLivesPanel] Falta asignar 'Close Button' en el Inspector.");
        if (!refillButton) Debug.LogWarning("[OutOfLivesPanel] Falta asignar 'Refill Button' en el Inspector.");
        // refillButton no tiene listener todavía a propósito — depende de la tienda (#53).
    }
}
