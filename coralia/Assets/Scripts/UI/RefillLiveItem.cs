using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Card reutilizable de RefillLivesPanel (issue #53) — misma jerarquía para las dos ofertas
// (rellenar Lives / vidas infinitas por tiempo), cada una vive en su propia prefab variant
// (RefillLiveItem (limit)/(Infinite)) pisando qué elementos quedan visibles. El panel decide
// QUÉ mostrar (precio, descuento, cantidad) — esta card solo presenta y avisa el click.
public class RefillLiveItem : MonoBehaviour
{
    [SerializeField] TMP_Text   countText;    // "5" — cantidad de vidas de la oferta (oculto en la infinita)
    [SerializeField] GameObject infiniteIcon; // el ∞ dentro del corazón (oculto en la de rellenar)
    [SerializeField] GameObject timeRoot;     // badge de duración (ej. "3h") — solo tiene sentido en la infinita
    [SerializeField] TMP_Text   timeText;
    [SerializeField] GameObject discountCard; // tag "50%" — oculto si esta compra no tiene descuento
    [SerializeField] TMP_Text   discountText;
    [SerializeField] TMP_Text   priceText;
    [SerializeField] Button     buyButton;

    public event System.Action OnBuyClicked;

    void Awake()
    {
        ValidateReferences();
        if (buyButton) buyButton.onClick.AddListener(() => OnBuyClicked?.Invoke());
    }

    public void SetupLimit(int lives, int price, int discountPercent, bool canAfford)
    {
        if (countText)    { countText.gameObject.SetActive(true); countText.text = lives.ToString(); }
        if (infiniteIcon) infiniteIcon.SetActive(false);
        if (timeRoot)     timeRoot.SetActive(false);
        SetDiscount(discountPercent);
        SetPrice(price, canAfford);
    }

    public void SetupInfinite(string durationLabel, int price, bool canAfford)
    {
        if (countText)    countText.gameObject.SetActive(false);
        if (infiniteIcon) infiniteIcon.SetActive(true);
        if (timeRoot)     timeRoot.SetActive(true);
        if (timeText)     timeText.text = durationLabel;
        SetDiscount(0); // sin descuento por ahora — solo aplica a la oferta de rellenar
        SetPrice(price, canAfford);
    }

    void SetDiscount(int percent)
    {
        bool has = percent > 0;
        if (discountCard) discountCard.SetActive(has);
        if (has && discountText) discountText.text = $"{percent}%";
    }

    // canAfford: si no alcanzan las monedas, el botón queda deshabilitado (Button.interactable
    // ya se ve grisado por defecto — es justo el feedback que se busca acá, a diferencia de
    // PopInView, donde ese mismo tinte automático no era deseado durante una animación).
    void SetPrice(int price, bool canAfford)
    {
        if (priceText)  priceText.text = price.ToString();
        if (buyButton)  buyButton.interactable = canAfford;
    }

    void ValidateReferences()
    {
        if (!countText)    Debug.LogWarning("[RefillLiveItem] Falta asignar 'Count Text' en el Inspector.");
        if (!infiniteIcon) Debug.LogWarning("[RefillLiveItem] Falta asignar 'Infinite Icon' en el Inspector.");
        if (!timeRoot)     Debug.LogWarning("[RefillLiveItem] Falta asignar 'Time Root' en el Inspector.");
        if (!timeText)     Debug.LogWarning("[RefillLiveItem] Falta asignar 'Time Text' en el Inspector.");
        if (!discountCard) Debug.LogWarning("[RefillLiveItem] Falta asignar 'Discount Card' en el Inspector.");
        if (!discountText) Debug.LogWarning("[RefillLiveItem] Falta asignar 'Discount Text' en el Inspector.");
        if (!priceText)    Debug.LogWarning("[RefillLiveItem] Falta asignar 'Price Text' en el Inspector.");
        if (!buyButton)    Debug.LogWarning("[RefillLiveItem] Falta asignar 'Buy Button' en el Inspector.");
    }
}
