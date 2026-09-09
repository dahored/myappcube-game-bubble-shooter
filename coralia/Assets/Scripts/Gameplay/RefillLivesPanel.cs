using System;
using UnityEngine;
using UnityEngine.UI;

// Modal de recarga de vidas (issue #53). Dos ofertas:
// - "Limit": rellena Lives hasta MAX_LIVES por monedas — oculta si ya está lleno (o si el
//   boost de infinitas ya está activo, no tiene sentido ofrecerla ahí).
// - "Infinite": vidas infinitas por tiempo limitado — siempre visible.
// Se abre desde el "+" del pill de vidas (LivesPillView) y desde OutOfLivesPanel.refillButton.
public class RefillLivesPanel : UIPanel
{
    [SerializeField] Button         closeButton;
    [SerializeField] RefillLiveItem limitItem;
    [SerializeField] RefillLiveItem infiniteItem;
    [SerializeField] ClaimPanel     claimPanel; // confirmación al comprar el rellenar (issue #53 + ClaimPanel)

    // Distinto de UIPanel.OnClosed a propósito: OnClosed dispara apenas ESTE panel termina de
    // cerrarse, que ahora pasa ANTES de mostrar ClaimPanel — quien necesite saber cuándo
    // termina el flujo COMPLETO (compra + confirmación) debe escuchar este en vez de OnClosed
    // (ver OutOfLivesPanel.OpenRefillLives, que antes recargaba la escena de Gameplay mientras
    // ClaimPanel todavía se estaba mostrando — bug reportado por Diego).
    public event System.Action OnPurchaseFlowFinished;

    [Header("Economía — rellenar vidas (pedido de Diego)")]
    [Tooltip("Llenar TODAS las vidas (de 0 a MAX_LIVES) cuesta esto — el precio por vida sale de dividirlo entre MAX_LIVES.")]
    [SerializeField] int coinsToFillAllLives = 20;
    [Tooltip("Alterna por compra vía SaveManager.NextRefillLimitHasDiscount — 1ra compra con descuento, 2da sin, 3ra con, etc.")]
    [SerializeField, Range(0, 100)] int discountPercent = 50;

    [Header("Economía — vidas infinitas")]
    [SerializeField] float durationHours = 3f;
    [Tooltip("A propósito no redondo (ej. no 50) — mismo criterio que Candy Crush (69 monedas por 6h), se siente más 'diseñado' que un número cerrado.")]
    [SerializeField] int infinitePrice = 47;

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
        if (closeButton)  closeButton.onClick.AddListener(Close);
        if (limitItem)    limitItem.OnBuyClicked    += BuyLimit;
        if (infiniteItem) infiniteItem.OnBuyClicked += BuyInfinite;
    }

    public override void Open()
    {
        base.Open();
        Refresh();
    }

    void Refresh()
    {
        bool hideLimitOffer = SaveManager.Lives >= SaveManager.MAX_LIVES || SaveManager.IsInfiniteLivesActive;
        if (limitItem) limitItem.gameObject.SetActive(!hideLimitOffer);

        if (!hideLimitOffer)
        {
            int missing = SaveManager.MAX_LIVES - SaveManager.Lives;
            int discount = SaveManager.NextRefillLimitHasDiscount ? discountPercent : 0;
            int price    = PriceForMissingLives(missing, discount);
            limitItem.SetupLimit(missing, price, discount, SaveManager.Coins >= price);
        }

        infiniteItem?.SetupInfinite(FormatHours(durationHours), infinitePrice, SaveManager.Coins >= infinitePrice);
    }

    // Precio por vida = coinsToFillAllLives / MAX_LIVES — ej. 20/5 = 4 monedas por vida.
    int PriceForMissingLives(int missing, int discount)
    {
        float pricePerLife = (float)coinsToFillAllLives / SaveManager.MAX_LIVES;
        int   price        = Mathf.CeilToInt(missing * pricePerLife);
        if (discount > 0) price = Mathf.Max(1, Mathf.RoundToInt(price * (1f - discount / 100f)));
        return price;
    }

    void BuyLimit()
    {
        int missing = SaveManager.MAX_LIVES - SaveManager.Lives;
        if (missing <= 0) return;

        int discount = SaveManager.NextRefillLimitHasDiscount ? discountPercent : 0;
        int price    = PriceForMissingLives(missing, discount);

        // Sin flujo de compra de monedas todavía (issue #57 — IAP) — si no alcanza, no pasa
        // nada más que este aviso por ahora.
        if (SaveManager.Coins < price)
        {
            Debug.LogWarning("[RefillLivesPanel] Monedas insuficientes para rellenar vidas.");
            return;
        }

        SaveManager.Coins -= price;
        SaveManager.Lives = SaveManager.MAX_LIVES;
        SaveManager.RefillLimitPurchaseCount++; // alterna el descuento para la próxima compra

        // Cierra este panel y muestra la confirmación — mismo patrón inmediato (sin esperar
        // OnClosed) que PausedPanel.OpenSubPanel: las dos animaciones se superponen un toque,
        // se ve bien porque una entra mientras la otra sale.
        // ShowLives recibe 'missing' (cuántas se compraron de verdad), no SaveManager.Lives —
        // ese último siempre es MAX_LIVES acá, mostraría "5" sin importar si faltaba 1 o 4.
        Close();
        ShowClaimThenFinish(() => claimPanel.ShowLives(missing));
    }

    void BuyInfinite()
    {
        if (SaveManager.Coins < infinitePrice)
        {
            Debug.LogWarning("[RefillLivesPanel] Monedas insuficientes para vidas infinitas.");
            return;
        }

        SaveManager.Coins -= infinitePrice;
        SaveManager.GrantInfiniteLives(TimeSpan.FromHours(durationHours));

        Close();
        ShowClaimThenFinish(() => claimPanel.ShowInfiniteLives(FormatHours(durationHours)));
    }

    // "3h" — no se traduce, mismo criterio que otros badges cortos de tiempo en la app.
    static string FormatHours(float hours) => $"{Mathf.RoundToInt(hours)}h";

    // Común a BuyLimit/BuyInfinite: muestra ClaimPanel (vía showClaim) y recién cuando ESE se
    // cierra dispara OnPurchaseFlowFinished. Sin ClaimPanel asignado, el flujo se da por
    // terminado ya mismo — no bloquear a nadie por un campo sin wirear en el Editor.
    void ShowClaimThenFinish(System.Action showClaim)
    {
        if (!claimPanel)
        {
            OnPurchaseFlowFinished?.Invoke();
            return;
        }

        showClaim();
        claimPanel.OnClosed += HandleClaimClosed;

        void HandleClaimClosed()
        {
            claimPanel.OnClosed -= HandleClaimClosed;
            OnPurchaseFlowFinished?.Invoke();
        }
    }

    void ValidateReferences()
    {
        if (!closeButton)   Debug.LogWarning("[RefillLivesPanel] Falta asignar 'Close Button' en el Inspector.");
        if (!limitItem)     Debug.LogWarning("[RefillLivesPanel] Falta asignar 'Limit Item' en el Inspector.");
        if (!infiniteItem)  Debug.LogWarning("[RefillLivesPanel] Falta asignar 'Infinite Item' en el Inspector.");
        if (!claimPanel)    Debug.LogWarning("[RefillLivesPanel] Falta asignar 'Claim Panel' en el Inspector — la compra de rellenar no va a mostrar confirmación.");
    }
}
