using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Oferta de "seguir jugando pagando monedas" al quedarse sin disparos (GDD §7 habla de
// "gemas" pero el proyecto todavía no tiene esa moneda implementada visualmente — Diego
// confirmó que hoy todo usa el ícono/balance de monedas). Título, "Oops!" y descripción
// son estáticos -> LocalizedText en el Editor, no hace falta código acá. El "+n" del ícono,
// el sugerido ({shots}) y el costo del botón sí son dinámicos.
//
// Si el jugador paga (OnContinuePressed), GameplayController le da ShotsBonus disparos
// extra y la ronda sigue. Si cierra con la X (OnDeclinedPressed), es derrota real ->
// GameplayController abre LosePanel.
public class NoMoreMovesPanel : UIPanel
{
    [SerializeField] TMP_Text shotsIconText;  // el "+n" del ícono -> "+" + ShotsBonus
    [SerializeField] TMP_Text suggestionText; // usa {shots} -> ShotsBonus
    [SerializeField] TMP_Text buyButtonText;  // muestra Cost
    [SerializeField] Button   buyButton;
    [SerializeField] Button   closeButton;
    [SerializeField] Button   quitButton; // mismo efecto que closeButton, para más claridad visual

    [Header("Balance de monedas (ResourcePillView — el botón '+' queda sin asignar hasta tener shop/IAP)")]
    [SerializeField] ResourcePillView balancePill;

    [Header("Costo — sube cada vez que se vuelve a mostrar en la misma partida (por definir con Diego, ajustar acá sin tocar código)")]
    [SerializeField] int baseCost      = 10;
    [SerializeField] int costIncrement = 5;   // se suma por cada vez ya usada en la partida
    [SerializeField] int shotsBonus    = 5;

    public int ShotsBonus => shotsBonus;

    // Costo escalonado: timesUsed = cuántas veces ya se pagó esta oferta en la partida
    // actual (0 la primera vez). GameplayController lo lleva y usa el mismo valor tanto
    // para mostrar el precio acá como para cobrarlo en OnContinuePressed.
    public int GetCost(int timesUsed) => baseCost + costIncrement * timesUsed;

    public event System.Action OnContinuePressed;
    public event System.Action OnDeclinedPressed;

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
        if (buyButton)   buyButton.onClick.AddListener(() => OnContinuePressed?.Invoke());
        if (closeButton) closeButton.onClick.AddListener(() => OnDeclinedPressed?.Invoke());
        if (quitButton)  quitButton.onClick.AddListener(() => OnDeclinedPressed?.Invoke());
    }

    public void Show(int timesUsed)
    {
        if (shotsIconText)  shotsIconText.text  = "+" + shotsBonus;
        if (suggestionText) suggestionText.text = LocaleManager.Get("ui.outofshots.panel.body.suggestion").Replace("{shots}", shotsBonus.ToString());
        if (buyButtonText)  buyButtonText.text   = GetCost(timesUsed).ToString();
        if (balancePill)
        {
            balancePill.SetValue(SaveManager.Coins);
            balancePill.SetPlusVisible(false);
        }
        Open();
    }

    void ValidateReferences()
    {
        if (!buyButton)   Debug.LogWarning("[NoMoreMovesPanel] Falta asignar 'Buy Button' en el Inspector.");
        if (!closeButton) Debug.LogWarning("[NoMoreMovesPanel] Falta asignar 'Close Button' en el Inspector.");
    }
}
