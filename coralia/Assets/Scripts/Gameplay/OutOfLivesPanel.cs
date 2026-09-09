using UnityEngine;
using UnityEngine.UI;

// Se muestra cuando SaveManager.Lives llega a 0 — el chequeo vive en los controllers de
// escena (LevelMapController al iniciar y al tocar un nodo, GameplayController al iniciar),
// no acá: este panel solo presenta y cierra, no sabe nada de cuándo debe aparecer.
public class OutOfLivesPanel : UIPanel
{
    [SerializeField] Button           closeButton;
    [SerializeField] Button           refillButton;
    [SerializeField] RefillLivesPanel refillLivesPanel;

    // Distinto de UIPanel.OnClosed a propósito: GameplayController usa OnClosed para navegar
    // de vuelta al mapa cuando el jugador cierra este modal con la X. OpenRefillLives() de
    // abajo también llama a Close() (para "hacerse a un lado" mientras se muestra el Refill),
    // y eso dispararía esa misma navegación sin querer si compartieran el evento — este es
    // el que representa específicamente "el jugador tocó la X para irse".
    public event System.Action OnCloseButtonPressed;

    // Se dispara cuando una compra en RefillLivesPanel resuelve el problema (ya hay vidas o
    // boost infinito). LevelMapController no necesita escucharlo (el mapa ya está armado
    // detrás, alcanza con que este panel se quede cerrado) — GameplayController sí, porque
    // su guard de entrada corta ANTES de armar el nivel: sin este evento, resolver la compra
    // acá dejaría la escena vacía en vez de retomar la carga.
    public event System.Action OnResolved;

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
        if (closeButton)  closeButton.onClick.AddListener(HandleCloseButton);
        if (refillButton) refillButton.onClick.AddListener(OpenRefillLives);
    }

    void HandleCloseButton()
    {
        Close();
        OnCloseButtonPressed?.Invoke();
    }

    // Variante del patrón de PausedPanel.OpenSubPanel: este panel se cierra y abre
    // RefillLivesPanel — pero a diferencia de Paused/Settings, acá NO siempre conviene
    // reabrirse al volver: si la compra resolvió el problema (ya hay vidas o boost
    // infinito activo), quedarse cerrado y dejar que el jugador siga jugando.
    //
    // Dos eventos distintos de RefillLivesPanel, a propósito:
    // - OnClosed dispara apenas ESE panel se cierra — si ahí todavía no hay vidas (el jugador
    //   cerró con la X sin comprar nada), nos reabrimos ya mismo.
    // - OnPurchaseFlowFinished dispara recién cuando, además, ClaimPanel (la confirmación de
    //   la compra) también se cerró — si hubo compra, hay que esperar A ESO antes de avisar
    //   "resuelto" (antes se avisaba en OnClosed, mientras ClaimPanel todavía se estaba
    //   mostrando, y GameplayController recargaba la escena de golpe — bug reportado por Diego).
    void OpenRefillLives()
    {
        if (!refillLivesPanel)
        {
            Debug.LogWarning("[OutOfLivesPanel] Falta asignar 'Refill Lives Panel' en el Inspector.");
            return;
        }

        Close();
        refillLivesPanel.Open();
        refillLivesPanel.OnClosed               += HandleRefillClosed;
        refillLivesPanel.OnPurchaseFlowFinished += HandlePurchaseFlowFinished;

        void HandleRefillClosed()
        {
            refillLivesPanel.OnClosed -= HandleRefillClosed;
            if (SaveManager.HasLivesAvailable) return; // se resolvió por una compra — esperar OnPurchaseFlowFinished
            refillLivesPanel.OnPurchaseFlowFinished -= HandlePurchaseFlowFinished; // no hubo compra, no hace falta esperarlo
            Open();
        }

        void HandlePurchaseFlowFinished()
        {
            refillLivesPanel.OnPurchaseFlowFinished -= HandlePurchaseFlowFinished;
            OnResolved?.Invoke();
        }
    }

    void ValidateReferences()
    {
        if (!closeButton)       Debug.LogWarning("[OutOfLivesPanel] Falta asignar 'Close Button' en el Inspector.");
        if (!refillButton)      Debug.LogWarning("[OutOfLivesPanel] Falta asignar 'Refill Button' en el Inspector.");
        if (!refillLivesPanel)  Debug.LogWarning("[OutOfLivesPanel] Falta asignar 'Refill Lives Panel' en el Inspector.");
    }
}
